using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace KhangNghi.Infrastructure.Services;

public sealed class EtlAlreadyRunningException() : InvalidOperationException("Một chu trình ETL đang chạy trên database này. Hãy chờ hoàn tất.");

internal sealed class EtlExecutionLock(IDbConnection connection) : IAsyncDisposable
{
    public static async Task<EtlExecutionLock> AcquireAsync(IDbConnection connection)
    {
        int result = await connection.ExecuteScalarAsync<int>("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = N'KhangNghi.DWMS.ETL',
                @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
            SELECT @result;
            """);
        if (result == -1) throw new EtlAlreadyRunningException();
        if (result < 0) throw new InvalidOperationException($"Không lấy được khóa ETL (code {result}).");
        return new EtlExecutionLock(connection);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            int result = await connection.ExecuteScalarAsync<int>("""
                DECLARE @result int;
                EXEC @result = sys.sp_releaseapplock @Resource = N'KhangNghi.DWMS.ETL', @LockOwner = 'Session';
                SELECT @result;
                """);
            if (result < 0) throw new InvalidOperationException("Cannot release ETL lock.");
        }
        catch
        {
            // Never return a session that may still hold a lock to the pool.
            if (connection is SqlConnection sql) SqlConnection.ClearPool(sql);
        }
    }
}
