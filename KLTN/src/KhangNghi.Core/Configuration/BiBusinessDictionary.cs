using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KhangNghi.Core.Configuration
{
    public class BiBusinessDictionary
    {
        public SystemInfo System { get; set; } = new();
        public List<MetricDefinition> Metrics { get; set; } = new();
        public List<IntentMapping> IntentDictionary { get; set; } = new();

        public static BiBusinessDictionary Load(string? basePath = null)
        {
            var searchPaths = new List<string>();
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                searchPaths.Add(Path.Combine(basePath, "BiBusinessDictionary.json"));
                searchPaths.Add(Path.Combine(basePath, "Configuration", "BiBusinessDictionary.json"));
            }

            var appDir = AppContext.BaseDirectory;
            searchPaths.Add(Path.Combine(appDir, "BiBusinessDictionary.json"));
            searchPaths.Add(Path.Combine(appDir, "Configuration", "BiBusinessDictionary.json"));

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var dict = JsonSerializer.Deserialize<BiBusinessDictionary>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (dict != null) return dict;
                    }
                    catch { /* Fallback to default */ }
                }
            }

            return new BiBusinessDictionary();
        }
    }

    public class SystemInfo
    {
        public string Name { get; set; } = "Khang Nghi Business Intelligence AI Copilot";
        public string Warehouse { get; set; } = "DWH_KhangNghi";
        public string Cube { get; set; } = "KhangNghi_Cube";
        public string TimeRange { get; set; } = "2022 - 2025";
        public string Currency { get; set; } = "VNĐ";
    }

    public class MetricDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Measure { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public List<string> Synonyms { get; set; } = new();
        public string Description { get; set; } = string.Empty;
    }

    public class IntentMapping
    {
        public string Intent { get; set; } = string.Empty;
        public string QueryId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public List<string> Synonyms { get; set; } = new();
        public List<string> Parameters { get; set; } = new();
        public List<string> SampleQuestions { get; set; } = new();
    }
}
