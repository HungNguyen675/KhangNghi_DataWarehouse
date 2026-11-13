using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace KhangNghi.Infrastructure.Services;

public sealed record DwhBackupOutcome(bool Success, string? FilePath, string Message);

// Chỉ sao lưu DWH. Không phục hồi, không ghi đè hoặc xóa file backup cũ.
public sealed class DwhBackupService(IDatabaseConnectionFactory factory)
{
    public async Task<DwhBackupOutcome> RunAsync(
        int executionId, int runNumber, string configuredDirectory,
        int timeoutSeconds, IProgress<string>? progress = null)
    {
        string? backupPath = null;
        try
        {
            if (executionId <= 0 || runNumber <= 0)
                throw new InvalidOperationException("Thiếu mã lần chạy ETL để ghi nhận bản sao lưu.");
            if (timeoutSeconds is < 60 or > 7200)
                throw new InvalidOperationException("BackupTimeoutSeconds phải từ 60 đến 7200 giây.");

            // Tạo kết nối khác để giữ nguyên session đang nắm khóa ETL.
            using var source = factory.CreateConnection();
            using var db = new SqlConnection(source.ConnectionString);
            await db.OpenAsync();
            string? database = await db.ExecuteScalarAsync<string>("SELECT DB_NAME();");
            if (!string.Equals(database, "DWH_KhangNghi", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chức năng này chỉ sao lưu DWH_KhangNghi.");

            string directory = configuredDirectory?.Trim() ?? "";
            if (directory.Length == 0)
                directory = await db.ExecuteScalarAsync<string>(
                    "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));") ?? "";
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)
                || directory.IndexOfAny(new[] { '\r', '\n', '*', '?', '"' }) >= 0)
                throw new InvalidOperationException("Không xác định được thư mục backup tuyệt đối trên máy SQL Server.");
            directory = Path.GetFullPath(directory);
            string fileName = "DWH_KhangNghi_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture)
                + "_UTC_Run" + runNumber.ToString(CultureInfo.InvariantCulture)
                + "_" + Guid.NewGuid().ToString("N") + ".bak";
            backupPath = Path.Combine(directory, fileName);
            if (backupPath.Length > 255)
                throw new InvalidOperationException("Đường dẫn backup vượt 255 ký tự; hãy chọn thư mục ngắn hơn.");

            progress?.Report("[BACKUP] Đang sao lưu kho dữ liệu. Thư mục thuộc máy SQL Server: " + directory);
            // Không INIT/FORMAT: tên file duy nhất, không ghi đè bản có sẵn.
            await db.ExecuteAsync("""
                BACKUP DATABASE [DWH_KhangNghi]
                TO DISK = @BackupPath
                WITH COPY_ONLY, CHECKSUM, STOP_ON_ERROR, NAME = @BackupName;
                """, new { BackupPath = backupPath, BackupName = "DWH sau ETL Run " + runNumber },
                commandTimeout: timeoutSeconds);
            progress?.Report("[BACKUP] Đã tạo file; đang kiểm tra khả năng đọc và checksum...");
            await db.ExecuteAsync("""
                RESTORE VERIFYONLY FROM DISK = @BackupPath
                WITH FILE = 1, CHECKSUM, STOP_ON_ERROR;
                """, new { BackupPath = backupPath }, commandTimeout: timeoutSeconds);
            return new DwhBackupOutcome(true, backupPath,
                "Đã sao lưu DWH và kiểm tra VERIFYONLY thành công. File: " + backupPath);
        }
        catch (Exception ex)
        {
            // File có thể đã được tạo trước khi lỗi/timeout. Không tự xóa hoặc tự thử lại.
            return new DwhBackupOutcome(false, backupPath,
                "Sao lưu hoặc kiểm tra bản sao lưu chưa thành công: " + ex.Message
                + (backupPath == null ? "" : " Đường dẫn cần kiểm tra: " + backupPath));
        }
    }
}
