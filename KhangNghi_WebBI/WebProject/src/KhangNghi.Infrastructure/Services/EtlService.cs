using Dapper;
using KhangNghi.Core.DTOs;
using KhangNghi.Core.Models;
using KhangNghi.Infrastructure.Data;

namespace KhangNghi.Infrastructure.Services;

public interface IEtlService
{
    Task<EtlResultDto> RunEtlAsync(string? excelFilePath = null, string triggeredBy = "Admin", IProgress<string>? progress = null);
    Task<IEnumerable<SysEtlLog>> GetLogsAsync(int limit = 50);
    Task<IEnumerable<DwhTableStatusDto>> GetDwhTableStatusesAsync();
}

// Compatibility adapter: API and WPF share one execution pipeline and log.
public class EtlService(IDatabaseConnectionFactory connectionFactory, IEtlOrchestratorService orchestrator) : IEtlService
{
    public Task<EtlResultDto> RunEtlAsync(string? excelFilePath = null, string triggeredBy = "Admin", IProgress<string>? progress = null)
    {
        if (!string.IsNullOrWhiteSpace(excelFilePath))
            throw new ArgumentException("Configure the SSIS source connection before running; filePath overrides are not supported.", nameof(excelFilePath));
        return orchestrator.RunOrchestratedEtlAsync("Incremental", triggeredBy, progress: progress);
    }

    public async Task<IEnumerable<SysEtlLog>> GetLogsAsync(int limit = 50)
    {
        using var db = connectionFactory.CreateConnection();
        return await db.QueryAsync<SysEtlLog>("""
            SELECT TOP (@Limit) ExecutionId AS LogID, StartTime, EndTime, Status,
                TotalRowsProcessed AS RowsInserted, FactRowCount, ErrorMessage, TriggeredBy
            FROM dbo.Sys_EtlExecution ORDER BY ExecutionId DESC
            """, new { Limit = Math.Clamp(limit, 1, 500) });
    }

    public Task<IEnumerable<DwhTableStatusDto>> GetDwhTableStatusesAsync() => orchestrator.GetDwhTableStatusesAsync();
}
