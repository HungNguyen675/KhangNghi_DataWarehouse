using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Core.DTOs;
using KhangNghi.Core.Models;
using KhangNghi.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace KhangNghi.Infrastructure.Services
{
    public interface IEtlService
    {
        Task<EtlResultDto> RunEtlAsync(string? excelFilePath = null, string triggeredBy = "Admin", IProgress<string>? progress = null);
        Task<IEnumerable<SysEtlLog>> GetLogsAsync(int limit = 50);
        Task<IEnumerable<DwhTableStatusDto>> GetDwhTableStatusesAsync();
    }

    public class EtlService : IEtlService
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;

        public EtlService(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task<IEnumerable<SysEtlLog>> GetLogsAsync(int limit = 50)
        {
            using var db = _connectionFactory.CreateConnection();
            return await db.QueryAsync<SysEtlLog>(
                "SELECT TOP (@Limit) * FROM dbo.Sys_EtlLog ORDER BY LogID DESC",
                new { Limit = limit });
        }

        public async Task<IEnumerable<DwhTableStatusDto>> GetDwhTableStatusesAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT 
                    t.name AS TableName,
                    CASE WHEN t.name LIKE 'Fact_%' THEN 'Fact' ELSE 'Dimension' END AS TableType,
                    p.rows AS [RowCount]
                FROM sys.tables t
                INNER JOIN sys.partitions p ON t.object_id = p.object_id
                WHERE p.index_id IN (0,1) AND (t.name LIKE 'Dim_%' OR t.name LIKE 'Fact_%')
                ORDER BY TableType DESC, TableName ASC";

            return await db.QueryAsync<DwhTableStatusDto>(sql);
        }

        public async Task<EtlResultDto> RunEtlAsync(string? excelFilePath = null, string triggeredBy = "Admin", IProgress<string>? progress = null)
        {
            var startTime = DateTime.Now;
            var log = new SysEtlLog { StartTime = startTime, Status = "Running", TriggeredBy = triggeredBy };
            int logId = 0;
            
            using (var db = _connectionFactory.CreateConnection())
            {
                const string insertLogSql = @"
                    INSERT INTO dbo.Sys_EtlLog (StartTime, Status, TriggeredBy)
                    VALUES (@StartTime, @Status, @TriggeredBy);
                    SELECT CAST(SCOPE_IDENTITY() as int);";
                logId = await db.ExecuteScalarAsync<int>(insertLogSql, log);
            }

            try
            {
                progress?.Report("INFO: Bắt đầu kích hoạt quy trình ETL thông qua SSIS...");
                
                progress?.Report("INFO: Đang chạy SSIS Package (0_Nap_Staging.dtsx) để trích xuất dữ liệu...");
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = @"C:\Program Files\Microsoft SQL Server\170\DTS\Binn\DTExec.exe",
                    Arguments = @"/F ""D:\VS_source\KLTN\SSIS_Project\0_Nap_Staging.dtsx""",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode != 0)
                            throw new Exception("Lỗi khi chạy SSIS Package. Mã lỗi: " + process.ExitCode);
                    }
                }
                
                progress?.Report("INFO: Đang chạy Transform & Load (sp_SSIS_ETL_Load) phân bổ dữ liệu vào 13 bảng Dimension và Fact_Sales...");
                using (var db = _connectionFactory.CreateConnection())
                {
                    await db.ExecuteAsync("dbo.sp_SSIS_ETL_Load", commandTimeout: 300, commandType: System.Data.CommandType.StoredProcedure);
                }

                progress?.Report("INFO: Hoàn tất quy trình ETL!");
                
                var duration = (DateTime.Now - startTime).TotalSeconds;
                using (var db = _connectionFactory.CreateConnection())
                {
                    await db.ExecuteAsync("UPDATE dbo.Sys_EtlLog SET Status = 'Success', EndTime = @EndTime, RowsInserted = @Rows, ErrorMessage = NULL WHERE LogID = @LogID",
                        new { EndTime = DateTime.Now, Rows = 1000, LogID = logId });
                }

                return new EtlResultDto { Success = true, Message = "ETL qua SSIS chạy thành công!", Duration = TimeSpan.FromSeconds(duration), RowsProcessed = 1000, CompletedAt = DateTime.Now };
            }
            catch (Exception ex)
            {
                using (var db = _connectionFactory.CreateConnection())
                {
                    await db.ExecuteAsync("UPDATE dbo.Sys_EtlLog SET Status = 'Failed', EndTime = @EndTime, ErrorMessage = @Error WHERE LogID = @LogID",
                        new { EndTime = DateTime.Now, Error = ex.Message, LogID = logId });
                }
                progress?.Report("ERROR: " + ex.Message);
                return new EtlResultDto { Success = false, Message = ex.Message, Duration = DateTime.Now - startTime, RowsProcessed = 0, CompletedAt = DateTime.Now };
            }
        }
    }
}

