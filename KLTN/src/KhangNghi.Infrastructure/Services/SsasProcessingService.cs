using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using KhangNghi.Core.Configuration;
using Microsoft.AnalysisServices;

namespace KhangNghi.Infrastructure.Services
{
    public interface ISsasProcessingService
    {
        Task<(bool Success, string Message, List<string> ProcessedCubes)> ProcessCubesAsync(string? serverAddress = null, string? databaseName = null, bool processFull = true, IProgress<string>? progress = null);
        Task<List<(string CubeName, string State, DateTime LastProcessed)>> GetCubesStatusAsync(string? serverAddress = null, string? databaseName = null);
    }

    public class SsasProcessingService : ISsasProcessingService
    {
        private readonly string _defaultServer;
        private readonly string _defaultDatabase;

        public SsasProcessingService(EtlSettingsOptions? etlSettings = null)
        {
            _defaultServer = etlSettings?.SsasServer ?? @"localhost\SQL2025";
            _defaultDatabase = etlSettings?.SsasDatabase ?? "KhangNghi_SSAS";
        }

        public async Task<(bool Success, string Message, List<string> ProcessedCubes)> ProcessCubesAsync(
            string? serverAddress = null,
            string? databaseName = null,
            bool processFull = true,
            IProgress<string>? progress = null)
        {
            serverAddress ??= _defaultServer;
            databaseName ??= _defaultDatabase;
            return await Task.Run(() =>
            {
                var processedCubes = new List<string>();
                var sw = Stopwatch.StartNew();

                try
                {
                    progress?.Report($"[SSAS] Đang kết nối tới Analysis Services tại '{serverAddress}'...");
                    using var server = new Server();
                    server.Connect(serverAddress);

                    if (!server.Databases.Contains(databaseName))
                    {
                        string err = $"[SSAS] Không tìm thấy cơ sở dữ liệu '{databaseName}' trên server '{serverAddress}'!";
                        progress?.Report(err);
                        return (false, err, processedCubes);
                    }

                    var db = server.Databases[databaseName];
                    progress?.Report($"[SSAS] Đã kết nối cơ sở dữ liệu '{db.Name}'. Bắt đầu xử lý đa chiều (ProcessType: {(processFull ? "ProcessFull" : "ProcessDefault")})...");

                    // Xử lý cơ sở dữ liệu SSAS (bao gồm tất cả chiều và cube)
                    var processType = processFull ? ProcessType.ProcessFull : ProcessType.ProcessDefault;
                    db.Process(processType);

                    foreach (Cube cube in db.Cubes)
                    {
                        processedCubes.Add(cube.Name);
                        progress?.Report($"[SSAS] ✔ Đã xử lý hoàn tất Cube: {cube.Name} (Trạng thái: {cube.State})");
                    }

                    server.Disconnect();
                    sw.Stop();
                    string msg = $"Xử lý SSAS thành công {processedCubes.Count} Cubes trong {sw.Elapsed.TotalSeconds:N1}s!";
                    progress?.Report($"[SSAS] {msg}");
                    return (true, msg, processedCubes);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    string errorMsg = $"Lỗi xử lý SSAS: {ex.Message}";
                    progress?.Report($"[SSAS] ❌ {errorMsg}");
                    return (false, errorMsg, processedCubes);
                }
            });
        }

        public async Task<List<(string CubeName, string State, DateTime LastProcessed)>> GetCubesStatusAsync(
            string? serverAddress = null,
            string? databaseName = null)
        {
            serverAddress ??= _defaultServer;
            databaseName ??= _defaultDatabase;
            return await Task.Run(() =>
            {
                var list = new List<(string, string, DateTime)>();
                try
                {
                    using var server = new Server();
                    server.Connect(serverAddress);
                    if (server.Databases.Contains(databaseName))
                    {
                        var db = server.Databases[databaseName];
                        foreach (Cube c in db.Cubes)
                        {
                            list.Add((c.Name, c.State.ToString(), c.LastProcessed));
                        }
                    }
                    server.Disconnect();
                }
                catch
                {
                    // Trả về danh sách rỗng nếu không kết nối được
                }
                return list;
            });
        }
    }
}
