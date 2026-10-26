using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Core.Configuration;
using KhangNghi.Core.DTOs;
using KhangNghi.Core.Models;
using KhangNghi.Infrastructure.Data;

namespace KhangNghi.Infrastructure.Services
{
    public interface IEtlOrchestratorService
    {
        Task<EtlResultDto> RunOrchestratedEtlAsync(string loadType = "Incremental", string triggeredBy = "Manual", int retryAttempt = 0, IProgress<string>? progress = null);
        Task<IEnumerable<SysEtlExecution>> GetExecutionHistoryAsync(int limit = 50);
        Task<IEnumerable<SysEtlStepLog>> GetStepLogsAsync(int executionId);
        Task<IEnumerable<DwhTableStatusDto>> GetDwhTableStatusesAsync();
    }

    public class EtlOrchestratorService : IEtlOrchestratorService
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private readonly IDatabaseConnectionFactory _managementConnectionFactory;
        private readonly ISsasProcessingService _ssasProcessingService;

        private readonly string _dtExecPath;
        private readonly string _masterEtlPath;
        private readonly string _ssasServer;
        private readonly string _ssasDatabase;
        private readonly bool _processSsasAfterLoad;

        public EtlOrchestratorService(
            IDatabaseConnectionFactory connectionFactory,
            ISsasProcessingService ssasProcessingService,
            EtlSettingsOptions? etlSettings = null,
            IDatabaseConnectionFactory? managementConnectionFactory = null)
        {
            _connectionFactory = connectionFactory;
            _managementConnectionFactory = managementConnectionFactory ?? connectionFactory;
            _ssasProcessingService = ssasProcessingService;
            _dtExecPath = Environment.GetEnvironmentVariable("KHANGNGHI_DTEXEC_PATH") ?? etlSettings?.DtExecPath ?? @"C:\Program Files\Microsoft SQL Server\170\DTS\Binn\DTExec.exe";
            _masterEtlPath = Environment.GetEnvironmentVariable("KHANGNGHI_MASTER_PACKAGE") ?? etlSettings?.MasterPackagePath ?? @"E:\CNTT_KLCN235_Nguyễn Trường Hưng\KhangNghi_DataWarehouse\KhangNghi_ETL\Master_ETL.dtsx";
            _ssasServer = Environment.GetEnvironmentVariable("KHANGNGHI_SSAS_SERVER") ?? etlSettings?.SsasServer ?? @"localhost\SQL2025";
            _processSsasAfterLoad = bool.TryParse(Environment.GetEnvironmentVariable("KHANGNGHI_PROCESS_SSAS_AFTER_LOAD"), out var processSsas) ? processSsas : etlSettings?.ProcessSsasAfterLoad ?? false;
            _ssasDatabase = Environment.GetEnvironmentVariable("KHANGNGHI_SSAS_DATABASE") ?? etlSettings?.SsasDatabase ?? @"KhangNghi_SSAS";
        }

        public async Task<IEnumerable<SysEtlExecution>> GetExecutionHistoryAsync(int limit = 50)
        {
            using var db = _managementConnectionFactory.CreateConnection();
            const string sql = "SELECT TOP (@Limit) * FROM dbo.Sys_EtlExecution ORDER BY ExecutionId DESC";
            return await db.QueryAsync<SysEtlExecution>(sql, new { Limit = Math.Clamp(limit, 1, 500) });
        }

        public async Task<IEnumerable<SysEtlStepLog>> GetStepLogsAsync(int executionId)
        {
            using var db = _managementConnectionFactory.CreateConnection();
            const string sql = "SELECT * FROM dbo.Sys_EtlStepLog WHERE ExecutionId = @ExecutionId ORDER BY StepNumber ASC";
            return await db.QueryAsync<SysEtlStepLog>(sql, new { ExecutionId = executionId });
        }

        public async Task<IEnumerable<DwhTableStatusDto>> GetDwhTableStatusesAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT 
                    t.name AS TableName,
                    CASE WHEN t.name LIKE 'Fact_%' THEN 'Fact' ELSE 'Dimension' END AS TableType,
                    SUM(p.rows) AS [RowCount]
                FROM sys.tables t
                INNER JOIN sys.partitions p ON t.object_id = p.object_id
                WHERE t.schema_id = SCHEMA_ID(N'dbo') AND p.index_id IN (0,1) AND (t.name LIKE 'Dim[_]%' OR t.name LIKE 'Fact[_]%')
                GROUP BY t.name
                ORDER BY TableType DESC, TableName ASC";

            return await db.QueryAsync<DwhTableStatusDto>(sql);
        }

        public async Task<EtlResultDto> RunOrchestratedEtlAsync(
            string loadType = "Incremental",
            string triggeredBy = "Manual",
            int retryAttempt = 0,
            IProgress<string>? progress = null)
        {
            var sw = Stopwatch.StartNew();
            using var executionDb = _connectionFactory.CreateConnection();
            using var db = _managementConnectionFactory.CreateConnection();
            int executionId = 0;
            int nextRunNumber = 0;
            bool initialResetCommitted = false;
            bool ssisSucceeded = false;
            try
            {
                if (!loadType.Equals("Incremental", StringComparison.OrdinalIgnoreCase) &&
                    !loadType.Equals("Initial", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("LoadType must be Initial or Incremental.", nameof(loadType));
                // Validate before creating logs or resetting any data.
                EtlRuntime.ValidateLocalFile(_dtExecPath);
                EtlRuntime.ValidateLocalFile(_masterEtlPath);
                if (_processSsasAfterLoad && (string.IsNullOrWhiteSpace(_ssasServer) || string.IsNullOrWhiteSpace(_ssasDatabase)))
                    throw new InvalidOperationException("Configure SSAS server and database before running ETL.");
                if (retryAttempt < 0 || string.IsNullOrWhiteSpace(triggeredBy) || triggeredBy.Length > 100)
                    throw new ArgumentException("Invalid retry attempt or trigger identity.");
                if (loadType.Equals("Initial", StringComparison.OrdinalIgnoreCase) &&
                    Environment.GetEnvironmentVariable("KHANGNGHI_ALLOW_INITIAL_RESET") != "true")
                    throw new InvalidOperationException("Initial reset requires KHANGNGHI_ALLOW_INITIAL_RESET=true.");
                ValidatePackageTarget(_masterEtlPath, executionDb.ConnectionString);
                executionDb.Open();
                await using var executionLock = await EtlExecutionLock.AcquireAsync(executionDb);
                await ValidateNativeDatabasesAsync(executionDb, db);
                var beforeFacts = await ReadFactCountsAsync(executionDb);


                {
                    // Lấy RunNumber tiếp theo
                    nextRunNumber = await db.ExecuteScalarAsync<int>("SELECT NEXT VALUE FOR dbo.Seq_EtlRunNumber");

                    const string insertExecSql = @"
                    INSERT INTO dbo.Sys_EtlExecution (RunNumber, LoadType, TriggeredBy, StartTime, Status, RetryAttempt)
                    VALUES (@RunNumber, @LoadType, @TriggeredBy, GETDATE(), 'Running', @RetryAttempt);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                    executionId = await db.ExecuteScalarAsync<int>(insertExecSql, new
                    {
                        RunNumber = nextRunNumber,
                        LoadType = loadType,
                        TriggeredBy = triggeredBy,
                        RetryAttempt = retryAttempt
                    });
                }

                progress?.Report($"============================================================");
                progress?.Report($"[DWMS] BẮT ĐẦU CHU TRÌNH ĐIỀU PHỐI ETL (Run #{nextRunNumber} | Chế độ: {loadType})");
                progress?.Report($"[DWMS] TriggeredBy: {triggeredBy} | Retry: {retryAttempt} | Execution ID: {executionId}");
                progress?.Report($"============================================================");

                if (loadType.Equals("Initial", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report("[DWMS] Initial: reset 17 bảng Dim/Fact, hai bảng staging và 5 watermark. Giữ nguyên nguồn và database quản trị.");
                    await executionDb.ExecuteAsync(InitialResetSql, commandTimeout: 300);
                    initialResetCommitted = true;
                    beforeFacts = await ReadFactCountsAsync(executionDb);
                }
                progress?.Report("[DWMS] Chạy Master SSIS gốc: Staging -> Dimensions -> Facts. Mỗi package tự chốt watermark; toàn bộ pipeline không phải một transaction.");

                // 2. Chạy Master_ETL.dtsx bằng DTExec
                progress?.Report($"[DWMS] Đang khởi chạy DTExec với gói Master: {Path.GetFileName(_masterEtlPath)}");

                // DTExec only reports the master outcome; child results need SSISDB logging.
                var stepLogs = new List<(int Step, string Name, string Pkg, string Table)>{
                    (1, "Execute SSIS Master", Path.GetFileName(_masterEtlPath), "DWH")
                };

                // Ghi nhận trước các bước vào Sys_EtlStepLog

                {
                    foreach (var s in stepLogs)
                    {
                        await db.ExecuteAsync(@"
                            INSERT INTO dbo.Sys_EtlStepLog (ExecutionId, StepNumber, StepName, PackageName, TargetTable, StartTime, Status)
                            VALUES (@ExecutionId, @Step, @Name, @Pkg, @Table, GETDATE(), 'Running')",
                            new { ExecutionId = executionId, Step = s.Step, Name = s.Name, Pkg = s.Pkg, Table = s.Table });
                    }
                }

                int ssisExitCode = await ExecuteDtexecPackageAsync(_masterEtlPath, progress);

                if (ssisExitCode != 0)
                {
                    throw new Exception($"SSIS Master thất bại (DTExec exit code {ssisExitCode}). Kiểm tra log SSIS.");
                }
                ssisSucceeded = true;

                // Cập nhật trạng thái các bước SSIS thành Success

                {
                    await db.ExecuteAsync(@"
                        UPDATE dbo.Sys_EtlStepLog 
                        SET Status = 'Success', EndTime = GETDATE()
                        WHERE ExecutionId = @ExecutionId AND StepNumber = 1",
                        new { ExecutionId = executionId });
                }

                if (_processSsasAfterLoad)
                {
                    // 3. Xử lý SSAS Multidimensional sau ETL
                    progress?.Report("\n[DWMS] [Bước 2/2] Bắt đầu tự động cập nhật SSAS Multidimensional Cube...");
                    int ssasStepId = 2;

                    {
                        await db.ExecuteAsync(@"
                        INSERT INTO dbo.Sys_EtlStepLog (ExecutionId, StepNumber, StepName, PackageName, TargetTable, StartTime, Status)
                        VALUES (@ExecutionId, @Step, N'Xử lý SSAS Multidimensional Cubes', @SsasDb, 'SSAS Database', GETDATE(), 'Running')",
                            new { ExecutionId = executionId, Step = ssasStepId, SsasDb = _ssasDatabase });
                    }

                    var ssasResult = await _ssasProcessingService.ProcessCubesAsync(
                        _ssasServer,
                        _ssasDatabase,
                        processFull: true,
                        progress: progress);


                    {
                        await db.ExecuteAsync(@"
                        UPDATE dbo.Sys_EtlStepLog 
                        SET Status = @Status, EndTime = GETDATE(), ErrorMessage = @Err
                        WHERE ExecutionId = @ExecutionId AND StepNumber = 2",
                            new { ExecutionId = executionId, Status = ssasResult.Success ? "Success" : "Failed", Err = ssasResult.Success ? null : ssasResult.Message });
                    }

                    if (!ssasResult.Success)
                    {
                        throw new InvalidOperationException("SSAS processing failed: " + ssasResult.Message);
                    }

                }
                else
                {
                    progress?.Report("[DWMS] Đã tắt xử lý SSAS trong cấu hình. Dữ liệu cube chưa được cập nhật bởi lần chạy này.");
                }
                var afterFacts = await ReadFactCountsAsync(executionDb);
                long changedRows = 0;
                foreach (var fact in afterFacts)
                {
                    long delta = fact.Value - beforeFacts[fact.Key];
                    if (delta < 0)
                        throw new InvalidOperationException($"Số dòng {fact.Key} giảm sau khi nạp; cần đối soát dữ liệu.");
                    changedRows = checked(changedRows + delta);
                    progress?.Report($"[DWMS] {fact.Key}: trước {beforeFacts[fact.Key]:N0}, sau {fact.Value:N0}, thêm ròng {delta:N0} dòng.");
                }
                progress?.Report("[DWMS] Số dòng thêm ròng không bao gồm cập nhật các dòng đã có hoặc thay đổi bảng chiều.");

                // Tính tổng số dòng Fact sau khi nạp
                long totalRows = 0;

                {
                    totalRows = afterFacts.Values.Aggregate(0L, (sum, count) => checked(sum + count));

                    // Cập nhật Execution thành công
                    await db.ExecuteAsync(@"
                        UPDATE dbo.Sys_EtlExecution 
                        SET Status = 'Success', EndTime = GETDATE(), TotalRowsProcessed = @ChangedRows, FactRowCount = @Rows, ErrorMessage = NULL
                        WHERE ExecutionId = @ExecutionId",
                        new { ExecutionId = executionId, Rows = totalRows, ChangedRows = checked((int)Math.Min(changedRows, int.MaxValue)) });
                }

                sw.Stop();
                progress?.Report($"============================================================");
                progress?.Report($"[DWMS] ✔ CHU TRÌNH ĐIỀU PHỐI ETL HOÀN TẤT THÀNH CÔNG! ({sw.Elapsed.TotalSeconds:N1}s)");
                progress?.Report($"[DWMS] Tổng số bản ghi Fact hiện có: {totalRows:N0}");
                progress?.Report($"============================================================\n");

                return new EtlResultDto
                {
                    Success = true,
                    Message = $"ETL Run #{nextRunNumber} ({loadType}) thành công trong {sw.Elapsed.TotalSeconds:N1}s. Fact thêm ròng: {changedRows:N0}; tổng: {totalRows:N0}. " + (_processSsasAfterLoad ? "Đã xử lý SSAS." : "Chưa xử lý SSAS."),
                    RowsProcessed = checked((int)Math.Min(changedRows, int.MaxValue)),
                    FactRowCount = totalRows,
                    Duration = sw.Elapsed,
                    CompletedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                string errMsg = ex.Message;
                if (initialResetCommitted)
                    errMsg += " Reset Initial đã commit; SSIS/SSAS lỗi không tự phục hồi dữ liệu trước reset.";
                if (ssisSucceeded)
                    errMsg += " SSIS đã thành công trước lỗi này; cần kiểm tra bước SSAS hoặc ghi nhật ký.";
                progress?.Report($"\n[DWMS] ❌ LỖI TRONG CHU TRÌNH ETL: {errMsg}");


                try
                {
                    await db.ExecuteAsync(@"
                        UPDATE dbo.Sys_EtlExecution 
                        SET Status = 'Failed', EndTime = GETDATE(), ErrorMessage = @Error
                        WHERE ExecutionId = @ExecutionId",
                        new { ExecutionId = executionId, Error = errMsg });

                    await db.ExecuteAsync(@"
                        UPDATE dbo.Sys_EtlStepLog 
                        SET Status = 'Failed', EndTime = GETDATE(), ErrorMessage = @Error
                        WHERE ExecutionId = @ExecutionId AND Status = 'Running'",
                        new { ExecutionId = executionId, Error = errMsg });
                }

                catch (Exception logError)
                {
                    progress?.Report($"[DWMS] Không ghi được log lỗi: {logError.Message}");
                    errMsg += " (Không thể cập nhật log execution; cần kiểm tra trạng thái Running.)";
                }

                return new EtlResultDto
                {
                    Success = false,
                    Message = errMsg,
                    RowsProcessed = 0,
                    Duration = sw.Elapsed,
                    CompletedAt = DateTime.Now
                };
            }
        }

        private static readonly string[] NativeTables =
        {
            "Fact_Sales", "Fact_Purchase", "Fact_Inventory", "Fact_Shipping",
            "Dim_Time", "Dim_Location", "Dim_Customer", "Dim_Product", "Dim_Employee",
            "Dim_Manufacturer", "Dim_Shipper", "Dim_Supplier", "Dim_Promotion",
            "Dim_SalesChannel", "Dim_Warehouse", "Dim_PaymentMethod", "Dim_Service"
        };

        private static async Task<Dictionary<string, long>> ReadFactCountsAsync(System.Data.IDbConnection db)
        {
            var rows = await db.QueryAsync<FactCount>("""
                SELECT N'Fact_Sales' TableName, COUNT_BIG(*) AS [RowCount] FROM dbo.Fact_Sales
                UNION ALL SELECT N'Fact_Purchase', COUNT_BIG(*) FROM dbo.Fact_Purchase
                UNION ALL SELECT N'Fact_Inventory', COUNT_BIG(*) FROM dbo.Fact_Inventory
                UNION ALL SELECT N'Fact_Shipping', COUNT_BIG(*) FROM dbo.Fact_Shipping;
                """, commandTimeout: 120);
            return rows.ToDictionary(r => r.TableName, r => r.RowCount, StringComparer.OrdinalIgnoreCase);
        }

        private sealed class FactCount
        {
            public string TableName { get; set; } = "";
            public long RowCount { get; set; }
        }

        private static async Task ValidateNativeDatabasesAsync(
            System.Data.IDbConnection dwh, System.Data.IDbConnection management)
        {
            int valid = await dwh.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM sys.tables
                WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN @Tables;
                """, new { Tables = NativeTables });
            if (valid != NativeTables.Length)
                throw new InvalidOperationException("DWH thiếu bảng Dim/Fact cần cho bộ SSIS hiện tại.");
            int stagingValid = await dwh.ExecuteScalarAsync<int>("""
                SELECT CASE WHEN
                    OBJECT_ID(N'KhangNghi_Staging.dbo.Stg_KhangNghiData', N'U') IS NOT NULL AND
                    OBJECT_ID(N'KhangNghi_Staging.dbo.Stg_PurchaseHistory', N'U') IS NOT NULL AND
                    OBJECT_ID(N'KhangNghi_Staging.dbo.ETL_Config', N'U') IS NOT NULL AND
                    (SELECT COUNT(*) FROM KhangNghi_Staging.dbo.ETL_Config WHERE TableName IN
                     (N'KhangNghi_Orders',N'KhangNghi_Sales',N'KhangNghi_Purchases',N'KhangNghi_Inventory',N'KhangNghi_Shipping'))=5
                    AND (SELECT COUNT(DISTINCT TableName) FROM KhangNghi_Staging.dbo.ETL_Config WHERE TableName IN
                     (N'KhangNghi_Orders',N'KhangNghi_Sales',N'KhangNghi_Purchases',N'KhangNghi_Inventory',N'KhangNghi_Shipping'))=5
                    THEN 1 ELSE 0 END;
                """);
            if (stagingValid != 1)
                throw new InvalidOperationException("Staging thiếu bảng hoặc 5 watermark không hợp lệ; chưa bắt đầu nạp.");
            var target = new SqlConnectionStringBuilder(dwh.ConnectionString);
            var log = new SqlConnectionStringBuilder(management.ConnectionString);
            if (!log.InitialCatalog.Equals("KhangNghi_AppManagement", StringComparison.OrdinalIgnoreCase) ||
                !log.DataSource.Equals(target.DataSource, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Nhật ký phải ghi vào KhangNghi_AppManagement trên cùng SQL Server với DWH.");
            int logValid = await management.ExecuteScalarAsync<int>("""
                SELECT CASE WHEN
                    OBJECT_ID(N'dbo.Sys_EtlExecution',N'U') IS NOT NULL AND
                    OBJECT_ID(N'dbo.Sys_EtlStepLog',N'U') IS NOT NULL AND
                    COL_LENGTH(N'dbo.Sys_EtlExecution',N'FactRowCount') IS NOT NULL AND
                    OBJECT_ID(N'dbo.Seq_EtlRunNumber',N'SO') IS NOT NULL
                    THEN 1 ELSE 0 END;
                """);
            if (logValid != 1)
                throw new InvalidOperationException("Database quản trị thiếu bảng nhật ký hoặc sequence; chưa bắt đầu nạp.");
        }

        // Chỉ được gọi khi người dùng chọn Initial và gate môi trường đã được bật.
        // Transaction này bảo vệ thao tác reset; không bao gồm quá trình SSIS/SSAS sau đó.
        private const string InitialResetSql = """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            IF DB_NAME()<>N'DWH_KhangNghi'
                THROW 51000,N'Sai database đích, không reset.',1;
            DECLARE @Targets TABLE(TableName sysname PRIMARY KEY, ObjectId int);
            INSERT @Targets(TableName) VALUES
              (N'Fact_Sales'),(N'Fact_Purchase'),(N'Fact_Inventory'),(N'Fact_Shipping'),
              (N'Dim_Time'),(N'Dim_Location'),(N'Dim_Customer'),(N'Dim_Product'),
              (N'Dim_Employee'),(N'Dim_Manufacturer'),(N'Dim_Shipper'),(N'Dim_Supplier'),
              (N'Dim_Promotion'),(N'Dim_SalesChannel'),(N'Dim_Warehouse'),
              (N'Dim_PaymentMethod'),(N'Dim_Service');
            UPDATE @Targets SET ObjectId=OBJECT_ID(N'dbo.'+QUOTENAME(TableName),N'U');
            IF EXISTS(SELECT 1 FROM @Targets WHERE ObjectId IS NULL)
                THROW 51001,N'Thiếu bảng đích; không reset.',1;
            IF EXISTS(SELECT 1 FROM sys.foreign_keys fk
                      JOIN @Targets t ON t.ObjectId=fk.referenced_object_id
                      WHERE fk.is_disabled=0 AND NOT EXISTS
                      (SELECT 1 FROM @Targets p WHERE p.ObjectId=fk.parent_object_id))
                THROW 51002,N'Có bảng ngoài phạm vi tham chiếu Dim/Fact; không reset.',1;
            IF (SELECT COUNT(*) FROM KhangNghi_Staging.dbo.ETL_Config WHERE TableName IN
                (N'KhangNghi_Orders',N'KhangNghi_Sales',N'KhangNghi_Purchases',N'KhangNghi_Inventory',N'KhangNghi_Shipping'))<>5
                THROW 51003,N'Watermark không hợp lệ; không reset.',1;
            BEGIN TRY
                BEGIN TRANSACTION;
                DECLARE @Remaining TABLE(TableName sysname PRIMARY KEY,ObjectId int);
                INSERT @Remaining SELECT TableName,ObjectId FROM @Targets;
                DECLARE @Name sysname,@Id int,@Sql nvarchar(max);
                WHILE EXISTS(SELECT 1 FROM @Remaining)
                BEGIN
                    SET @Name=NULL;
                    SELECT TOP(1) @Name=t.TableName,@Id=t.ObjectId FROM @Remaining t
                    WHERE NOT EXISTS(SELECT 1 FROM sys.foreign_keys fk
                      JOIN @Remaining child ON child.ObjectId=fk.parent_object_id
                      WHERE fk.referenced_object_id=t.ObjectId AND fk.is_disabled=0)
                    ORDER BY CASE WHEN t.TableName LIKE N'Fact[_]%' THEN 0 ELSE 1 END,t.TableName;
                    IF @Name IS NULL
                        THROW 51004,N'Không sắp được thứ tự khóa ngoại; rollback reset.',1;
                    SET @Sql=N'DELETE FROM dbo.'+QUOTENAME(@Name)+N';';
                    EXEC sys.sp_executesql @Sql;
                    DELETE FROM @Remaining WHERE ObjectId=@Id;
                END;
                -- Không reseed identity; khóa chiều mới không cần trùng lần nạp trước.
                TRUNCATE TABLE KhangNghi_Staging.dbo.Stg_KhangNghiData;
                TRUNCATE TABLE KhangNghi_Staging.dbo.Stg_PurchaseHistory;
                UPDATE KhangNghi_Staging.dbo.ETL_Config
                    SET Last_Load_Date=CONVERT(date,'19000101')
                    WHERE TableName IN(N'KhangNghi_Orders',N'KhangNghi_Sales',
                      N'KhangNghi_Purchases',N'KhangNghi_Inventory',N'KhangNghi_Shipping');
                IF @@ROWCOUNT<>5 THROW 51005,N'Sai số watermark; rollback reset.',1;
                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """;

        internal static void ValidatePackageTarget(string packagePath, string connectionString)
        {
            XNamespace dts = "www.microsoft.com/SqlServer/Dts";
            var target = new SqlConnectionStringBuilder(connectionString);
            if (!target.InitialCatalog.Equals("DWH_KhangNghi", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bộ package này chỉ dành cho DWH_KhangNghi; không tự chuyển sang database khác.");
            string root = Path.GetFullPath(Path.GetDirectoryName(packagePath)!) + Path.DirectorySeparatorChar;
            var pending = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Push(Path.GetFullPath(packagePath));
            int dwhConnections = 0;
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Package con nằm ngoài thư mục Master: " + Path.GetFileName(current));
                if (!visited.Add(current)) continue;
                EtlRuntime.ValidateLocalFile(current);
                var document = XDocument.Load(current);
                if (document.Descendants().Any(e => e.Attributes().Any(a =>
                    a.Name.LocalName == "SqlStatementSource" &&
                    (a.Value.Contains("etl.usp_RunVNLoad", StringComparison.OrdinalIgnoreCase) ||
                     a.Value.Contains("etl.usp_DesignerPrepare", StringComparison.OrdinalIgnoreCase)))))
                    throw new InvalidOperationException("Đang chọn package runtime cũ; cần bộ Master SSIS gốc.");
                foreach (var manager in document.Descendants(dts + "ConnectionManager")
                    .Where(c => c.Attribute(dts + "CreationName") != null))
                {
                    string kind = (string?)manager.Attribute(dts + "CreationName") ?? "";
                    if (manager.Descendants(dts + "PropertyExpression")
                        .Any(e => ((string?)e.Attribute(dts + "Name")) == "ConnectionString"))
                        throw new InvalidOperationException("ConnectionString có expression; cần kiểm tra đích thực tế trước khi chạy.");
                    string? value = manager.Descendants().Attributes(dts + "ConnectionString")
                        .Select(a => a.Value).FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    if (kind.Equals("FILE", StringComparison.OrdinalIgnoreCase))
                    {
                        string child = Path.GetFullPath(Path.IsPathRooted(value)
                            ? value : Path.Combine(Path.GetDirectoryName(current)!, value));
                        if (!child.EndsWith(".dtsx", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("FILE connection không phải package SSIS; cần kiểm tra thủ công.");
                        pending.Push(child);
                    }
                    else if (kind.StartsWith("OLEDB", StringComparison.OrdinalIgnoreCase))
                    {
                        var package = new DbConnectionStringBuilder { ConnectionString = value };
                        string catalog = package.TryGetValue("Initial Catalog", out var c) ? Convert.ToString(c) ?? "" : "";
                        string server = package.TryGetValue("Data Source", out var s) ? Convert.ToString(s) ?? "" : "";
                        if (catalog.StartsWith("DWH_", StringComparison.OrdinalIgnoreCase) ||
                            catalog.StartsWith("KhangNghi_Staging", StringComparison.OrdinalIgnoreCase))
                        {
                            string expected = catalog.StartsWith("DWH_", StringComparison.OrdinalIgnoreCase)
                                ? target.InitialCatalog : "KhangNghi_Staging";
                            if (!catalog.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                                !NormalizeServer(server).Equals(NormalizeServer(target.DataSource), StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Đích DWH/Staging không khớp Desktop: " + Path.GetFileName(current));
                            if (catalog.StartsWith("DWH_", StringComparison.OrdinalIgnoreCase)) dwhConnections++;
                        }
                    }
                }
            }
            foreach (string required in new[] { "Master_ETL", "0_Nap_Staging", "1_Load_Dimensions", "2_Load_Fact" }.Concat(NativeTables))
                if (!visited.Contains(Path.GetFullPath(Path.Combine(root, required + ".dtsx"))))
                    throw new InvalidOperationException("Thiếu package trong cây điều phối: " + required);
            if (dwhConnections == 0)
                throw new InvalidOperationException("Không xác minh được kết nối DWH trong package con.");
            static string NormalizeServer(string name)
            {
                var parts = name.Trim().Split('\\', 2);
                var host = parts[0];
                if (host.Equals(".", StringComparison.OrdinalIgnoreCase) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("(local)", StringComparison.OrdinalIgnoreCase)) host = Environment.MachineName;
                return host + (parts.Length == 2 ? "\\" + parts[1] : "");
            }
        }

        private async Task<int> ExecuteDtexecPackageAsync(string packagePath, IProgress<string>? progress)
        {
            return await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _dtExecPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                psi.ArgumentList.Add("/F");
                psi.ArgumentList.Add(packagePath);
                // Master SSIS hiện tại không có biến LoadType và RunToken.
                // Chế độ Initial sẽ chuẩn bị lại đích và watermark trước khi gọi Master.
                // Chế độ Incremental giữ nguyên dữ liệu và watermark.
                psi.WorkingDirectory = Path.GetDirectoryName(packagePath)
                    ?? throw new InvalidOperationException(
                        "Không xác định được thư mục chứa Master SSIS.");

                using var proc = new Process { StartInfo = psi };
                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        // Lọc các dòng log quan trọng để hiển thị ngắn gọn
                        string line = e.Data.Trim();
                        if (line.Contains("DTExec:") || line.Contains("Started:") || line.Contains("Finished:") || line.Contains("Progress:") || line.Contains("Error:") || line.Contains("DTSER_"))
                        {
                            progress?.Report($"  [SSIS Engine] {line}");
                        }
                    }
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        progress?.Report($"  [SSIS Engine ERR] {e.Data.Trim()}");
                    }
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                return proc.ExitCode;
            });
        }
    }
}
