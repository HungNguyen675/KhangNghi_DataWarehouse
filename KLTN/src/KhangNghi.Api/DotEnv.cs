using System;
using System.Collections.Generic;
using System.IO;

namespace KhangNghi.Api
{
    /// <summary>
    /// Nạp biến môi trường từ file .env (dạng KEY=VALUE) trước khi ASP.NET Core đọc cấu hình.
    /// Không ghi đè biến đã có sẵn trong môi trường hệ thống.
    /// </summary>
    public static class DotEnv
    {
        public static string? Load()
        {
            foreach (var dir in CandidateDirectories())
            {
                var path = Path.Combine(dir, ".env");
                if (!File.Exists(path)) continue;

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..].Trim();
                    if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                        value = value[1..^1];

                    if (Environment.GetEnvironmentVariable(key) == null)
                        Environment.SetEnvironmentVariable(key, value);
                }
                return path;
            }
            return null;
        }

        // dotnet run chạy tại thư mục project; bản publish chạy tại thư mục bin
        private static IEnumerable<string> CandidateDirectories()
        {
            yield return Directory.GetCurrentDirectory();
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
                yield return dir.FullName;
        }
    }
}
