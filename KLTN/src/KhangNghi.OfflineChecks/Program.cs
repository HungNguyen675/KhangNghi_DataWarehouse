using System.Data;
using KhangNghi.Core.Configuration;
using KhangNghi.Infrastructure.Data;
using KhangNghi.Infrastructure.Services;

int checks = 0;
async Task Reject<T>(string name, Func<Task> action) where T : Exception
{
    try { await action(); throw new Exception($"FAILED: {name} did not reject the request."); }
    catch (T) { checks++; Console.WriteLine($"PASS: {name}"); }
}
var variables = new[] { "KHANGNGHI_DTEXEC_PATH", "KHANGNGHI_MASTER_PACKAGE", "KHANGNGHI_ALLOW_INITIAL_RESET", "KHANGNGHI_RUNNER_PATH", "KHANGNGHI_RUNNER_CONFIG" };
var saved = variables.ToDictionary(v => v, Environment.GetEnvironmentVariable);
try
{
    foreach (string variable in variables) Environment.SetEnvironmentVariable(variable, null);
    var factory = new NoDatabaseFactory();
    var ssas = new NoSsas();
    string localFile = typeof(EtlOrchestratorService).Assembly.Location;
    var settings = new EtlSettingsOptions { DtExecPath = localFile, MasterPackagePath = localFile, SsasServer = "TEST", SsasDatabase = "TEST" };
    var etl = new EtlOrchestratorService(factory, ssas, settings);
    await Reject<ArgumentException>("invalid load type is blocked before SQL", () => etl.RunOrchestratedEtlAsync("Unknown"));
    await Reject<InvalidOperationException>("Initial reset is disabled by default", () => etl.RunOrchestratedEtlAsync("Initial"));
    await Reject<ArgumentException>("empty trigger identity is blocked", () => etl.RunOrchestratedEtlAsync(triggeredBy: ""));
    await Reject<ArgumentException>("negative retry is blocked", () => etl.RunOrchestratedEtlAsync(retryAttempt: -1));
    var missing = new EtlSettingsOptions { DtExecPath = Path.Combine(AppContext.BaseDirectory, "missing-dtexec.exe"), MasterPackagePath = localFile };
    await Reject<FileNotFoundException>("missing DTExec is blocked before SQL", () => new EtlOrchestratorService(factory, ssas, missing).RunOrchestratedEtlAsync());
    await Reject<ArgumentException>("API file override cannot bypass orchestrator", () => new EtlService(factory, etl).RunEtlAsync("source.xlsx"));
    var agent = new SqlAgentService(factory, settings);
    await Reject<ArgumentException>("Agent requires explicit runner paths", () => agent.EnsureJobExistsAsync());
    await Reject<ArgumentException>("Agent cannot revert to direct DTExec", () => agent.EnsureJobExistsAsync(localFile, localFile));
    await Reject<ArgumentException>("invalid schedule hour cannot create job", () => agent.UpdateScheduleAsync(true, 24, 0));
    await Reject<ArgumentException>("Once requires unsupported date and is rejected", () => agent.UpdateScheduleAsync(true, 2, 0, "Once"));
    await Reject<ArgumentException>("command expansion is rejected", () => { EtlRuntime.ValidateCommandPath(@"C:\%TEMP%\runner.exe"); return Task.CompletedTask; });
    if (factory.Attempts != 0) throw new Exception("Offline checks attempted SQL access.");
    using var stream = typeof(EtlOrchestratorService).Assembly.GetManifestResourceStream("KhangNghi.Infrastructure.Data.002_Validate_ETL_Schema.sql");
    if (stream == null) throw new Exception("Schema preflight was not embedded.");
    checks++;
    Console.WriteLine($"Passed {checks} offline checks. No SQL connection or SSIS/SSAS process was started.");
    return 0;
}
finally
{
    foreach (var pair in saved) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
}

sealed class NoDatabaseFactory : IDatabaseConnectionFactory
{
    public int Attempts { get; private set; }
    public string ConnectionString => throw new Exception("SQL access forbidden in offline checks.");
    public IDbConnection CreateConnection() { Attempts++; throw new Exception("SQL access forbidden in offline checks."); }
    public void UpdateConnectionString(string value) => throw new Exception("SQL access forbidden.");
    public bool TestConnection(out string message) => throw new Exception("SQL access forbidden.");
}
sealed class NoSsas : ISsasProcessingService
{
    public Task<(bool Success, string Message, List<string> ProcessedCubes)> ProcessCubesAsync(string? serverAddress = null, string? databaseName = null, bool processFull = true, IProgress<string>? progress = null) => throw new Exception("SSAS access forbidden.");
    public Task<List<(string CubeName, string State, DateTime LastProcessed)>> GetCubesStatusAsync(string? serverAddress = null, string? databaseName = null) => throw new Exception("SSAS access forbidden.");
}
