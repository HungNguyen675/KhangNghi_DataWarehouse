using System.Text.Json;
using KhangNghi.Core.Configuration;
using KhangNghi.Infrastructure.Data;
using KhangNghi.Infrastructure.Services;
using Microsoft.Data.SqlClient;

// Only --run opens a SQL connection. --validate-config inspects local files only.
if (args.Length != 3 || (args[0] != "--run" && args[0] != "--validate-config") || args[1] != "--config")
{
    Console.Error.WriteLine("Usage: KhangNghi.EtlRunner (--validate-config|--run) --config <absolute-json-path>");
    return 2;
}

try
{
    EtlRuntime.ValidateLocalFile(args[2]);
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));
    // Require explicit database and ETL targets; never use AppSettings connection defaults.
    string connectionString = Environment.GetEnvironmentVariable("KHANGNGHI_DWH_CONNECTION")
        ?? document.RootElement.GetProperty("ConnectionStrings").GetProperty("DwhConnection").GetString()
        ?? throw new InvalidOperationException("Set ConnectionStrings:DwhConnection.");
    var connection = new SqlConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog))
        throw new InvalidOperationException("Specify SQL Server and database explicitly.");
    var section = document.RootElement.GetProperty("EtlSettings");
    string Setting(string property, string variable) => Environment.GetEnvironmentVariable(variable)
        ?? section.GetProperty(property).GetString() ?? throw new InvalidOperationException($"Set EtlSettings:{property}.");
    var settings = new EtlSettingsOptions
    {
        DtExecPath = Setting("DtExecPath", "KHANGNGHI_DTEXEC_PATH"),
        MasterPackagePath = Setting("MasterPackagePath", "KHANGNGHI_MASTER_PACKAGE"),
        SsasServer = Setting("SsasServer", "KHANGNGHI_SSAS_SERVER"),
        SsasDatabase = Setting("SsasDatabase", "KHANGNGHI_SSAS_DATABASE"),
        ProcessSsasAfterLoad = section.TryGetProperty("ProcessSsasAfterLoad", out var processSsas) && processSsas.GetBoolean()
    };
    EtlRuntime.ValidateLocalFile(settings.DtExecPath);
    EtlRuntime.ValidateLocalFile(settings.MasterPackagePath);
    if (settings.ProcessSsasAfterLoad && (string.IsNullOrWhiteSpace(settings.SsasServer) || string.IsNullOrWhiteSpace(settings.SsasDatabase)))
        throw new InvalidOperationException("Specify SSAS server and database explicitly.");
    if (args[0] == "--validate-config")
    {
        Console.WriteLine("Local configuration valid. SQL Server, SSIS and SSAS were not contacted; package connections still require SSDT validation.");
        return 0;
    }
    // Kết nối kho dữ liệu.
    var factory = new DatabaseConnectionFactory(connection.ConnectionString);

    // Kết nối riêng để ghi nhật ký, giống Desktop App.
    string managementConnectionString = document.RootElement
        .GetProperty("ConnectionStrings")
        .GetProperty("AppManagementConnection")
        .GetString()
        ?? throw new InvalidOperationException(
            "Thiếu ConnectionStrings:AppManagementConnection.");

    var managementConnection =
        new SqlConnectionStringBuilder(managementConnectionString);

    if (!managementConnection.InitialCatalog.Equals(
            "KhangNghi_AppManagement",
            StringComparison.OrdinalIgnoreCase) ||
        !managementConnection.DataSource.Equals(
            connection.DataSource,
            StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Database quản trị phải là KhangNghi_AppManagement " +
            "trên cùng SQL Server với DWH.");
    }

    var managementFactory = new DatabaseConnectionFactory(
        managementConnection.ConnectionString);

    var orchestrator = new EtlOrchestratorService(
        factory,
        new SsasProcessingService(settings),
        settings,
        managementFactory);
    var result = await orchestrator.RunOrchestratedEtlAsync("Incremental", "SQLAgent", progress: new ConsoleProgress());
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 1;
}
catch (EtlAlreadyRunningException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 3;
}
catch (Exception ex)
{
    // No connection strings or full configuration dumped to Agent history.
    Console.Error.WriteLine($"ETL runner failed ({ex.GetType().Name}). Check configuration, permissions and Sys_EtlExecution.");
    return 1;
}

sealed class ConsoleProgress : IProgress<string>
{
    public void Report(string value) => Console.WriteLine(value);
}
