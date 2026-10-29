using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Infrastructure.Services.Copilot
{
    /// <summary>
    /// Cấu hình LLM cho AI Copilot (Google Gemini qua Google AI Studio API hoặc API tương thích OpenAI).
    /// </summary>
    public sealed class LlmOptions
    {
        // Giá trị mặc định duy nhất; ghi đè bằng GEMINI_BASE_URL / GEMINI_MODEL / GEMINI_TIMEOUT_SECONDS / GEMINI_MAX_RETRIES
        public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai";
        public const string DefaultModel = "models/gemini-3.5-flash-lite";
        public const int DefaultTimeoutSeconds = 20;

        public string BaseUrl { get; set; } = DefaultBaseUrl;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = DefaultModel;
        // Tổng thời gian chờ Gemini cho cả vòng Function Calling (không phải từng lượt), hết hạn thì chuyển offline
        public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
        // Chỉ retry khi Gemini quá tải (503) và còn đủ thời gian; không retry 429 hoặc lỗi xác thực
        public int MaxRetries { get; set; } = 1;
        public int MaxToolRounds { get; set; } = 4;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
    }

    /// <summary>Loại lỗi khi gọi Gemini, dùng để ghi log và báo lý do chuyển sang chế độ offline.</summary>
    public enum LlmFailureKind { Auth, Quota, Overloaded, Timeout, Network, BadResponse }

    public sealed class LlmException : Exception
    {
        public LlmFailureKind Kind { get; }
        public int? StatusCode { get; }

        public LlmException(LlmFailureKind kind, string message, int? statusCode = null, Exception? inner = null)
            : base(message, inner)
        {
            Kind = kind;
            StatusCode = statusCode;
        }

        // Mã lý do gửi cho giao diện (không chứa nội dung phản hồi của Google)
        public string Reason => Kind switch
        {
            LlmFailureKind.Auth => "auth",
            LlmFailureKind.Quota => "quota",
            LlmFailureKind.Overloaded => "overloaded",
            LlmFailureKind.Timeout => "timeout",
            LlmFailureKind.Network => "network",
            _ => "bad_response"
        };
    }

    public sealed class CopilotAnswer
    {
        public string Answer { get; init; } = string.Empty;
        public object? Data { get; init; }
        public IReadOnlyList<string> QueriesUsed { get; init; } = Array.Empty<string>();
    }

    public interface ILlmCopilotService
    {
        bool IsConfigured { get; }
        Task<CopilotAnswer> AskAsync(string question, string? role, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tầng 3 của AI Copilot: LLM hiểu câu hỏi tự nhiên tiếng Việt, tự chọn truy vấn trong BiQueryCatalog qua Function Calling,
    /// rồi tổng hợp câu trả lời từ số liệu thật của DWH.
    /// </summary>
    public sealed class LlmCopilotService : ILlmCopilotService
    {
        private const string ToolName = "query_bi_warehouse";
        private const int MaxRowsForModel = 15;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        private readonly HttpClient _http;
        private readonly LlmOptions _options;
        private readonly IDwhAnalyticsRepository _repo;
        private readonly ISalesForecastService _forecast;
        private readonly ILogger<LlmCopilotService> _logger;

        public LlmCopilotService(HttpClient http, LlmOptions options, IDwhAnalyticsRepository repo, ISalesForecastService forecast, ILogger<LlmCopilotService> logger)
        {
            _http = http;
            _options = options;
            _repo = repo;
            _forecast = forecast;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<CopilotAnswer> AskAsync(string question, string? role, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Copilot chưa được cấu hình API key hoặc Model (GEMINI_API_KEY hoặc GEMINI_MODEL).");

            var allowed = BiQueryCatalog.ForRole(role);
            if (allowed.Count == 0)
            {
                return new CopilotAnswer
                {
                    Answer = "Vai trò hiện tại của bạn chưa được cấp quyền truy vấn kho dữ liệu qua AI Copilot.",
                    Data = null,
                    QueriesUsed = Array.Empty<string>()
                };
            }

            // 1. Phân tích ý định câu hỏi (Question Intent Parser)
            var intent = QuestionIntentParser.Parse(question);

            // Kiểm soát Trường hợp F: Năm không có dữ liệu trong kho DWH_KhangNghi (ngoài 2022 - 2025)
            if (intent.ResponseType == QuestionResponseType.OutOfRange)
            {
                var outYears = intent.OutOfRangeYears.Count > 0 ? string.Join(", ", intent.OutOfRangeYears) : "yêu cầu";
                return new CopilotAnswer
                {
                    Answer = $"Kho dữ liệu DWH_KhangNghi hiện chỉ lưu trữ dữ liệu hoạt động kinh doanh giai đoạn **2022 – 2025**. Năm **{outYears}** không có dữ liệu trong hệ thống.",
                    Data = null,
                    QueriesUsed = Array.Empty<string>()
                };
            }

            var messages = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = BuildSystemPrompt(role, intent) },
                new JsonObject { ["role"] = "user", ["content"] = question }
            };

            var tools = BuildTools(allowed);
            var queriesUsed = new List<string>();
            object? lastData = null;

            // Ngân sách thời gian chung cho mọi lượt gọi Gemini của câu hỏi này
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, _options.TimeoutSeconds));

            for (var round = 0; round < _options.MaxToolRounds; round++)
            {
                var message = await CompleteAsync(messages, tools, budget.Token, deadline, cancellationToken);
                var toolCalls = message["tool_calls"]?.AsArray();

                if (toolCalls == null || toolCalls.Count == 0)
                {
                    var content = message["content"]?.GetValue<string>() ?? string.Empty;
                    return new CopilotAnswer
                    {
                        Answer = content.Trim(),
                        Data = lastData,
                        QueriesUsed = queriesUsed
                    };
                }

                messages.Add(message);

                foreach (var call in toolCalls)
                {
                    var function = call?["function"];
                    var callId = call?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
                    var functionName = function?["name"]?.GetValue<string>();
                    var callArgs = function?["arguments"]?.GetValue<string>() ?? "{}";

                    if (!string.Equals(functionName, ToolName, StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(new JsonObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = callId,
                            ["content"] = Error($"Công cụ '{functionName}' không được hỗ trợ.")
                        });
                        continue;
                    }

                    var (resultJson, queryId, toolData) = await RunToolAsync(callArgs, allowed, intent);
                    if (queryId != null && !queriesUsed.Contains(queryId)) queriesUsed.Add(queryId);
                    if (toolData != null) lastData = toolData;

                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = callId,
                        ["content"] = resultJson
                    });
                }
            }

            return new CopilotAnswer
            {
                Answer = "Câu hỏi cần quá nhiều bước truy vấn tổng hợp, bạn hãy thử tách nhỏ câu hỏi để nhận kết quả chi tiết hơn.",
                Data = lastData,
                QueriesUsed = queriesUsed
            };
        }

        private async Task<(string ResultJson, string? QueryId, object? Data)> RunToolAsync(string argsJson, IReadOnlyList<BiQueryDefinition> allowed, QuestionIntent intent)
        {
            JsonNode? args;
            try { args = JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson); }
            catch (JsonException) { return (Error("Tham số Function Call không phải JSON hợp lệ."), null, null); }

            var queryId = EnforceIntentQuery(ReadString(args, "query_id"), intent);
            var definition = allowed.FirstOrDefault(q => string.Equals(q.Id, queryId, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
                return (Error($"Mã truy vấn '{queryId}' không tồn tại trong danh mục hoặc vai trò hiện tại không được phép truy cập."), null, null);

            var readYear = ReadInt(args, "year");
            var readTop = ReadInt(args, "top");

            // Kiểm soát tham số từ QuestionIntent để bảo đảm tính chính xác tuyệt đối
            if (!readYear.HasValue && intent.TargetYears.Count == 1)
            {
                readYear = intent.TargetYears[0];
            }
            if (intent.Limit.HasValue && (!readTop.HasValue || readTop.Value > intent.Limit.Value))
            {
                readTop = intent.Limit.Value;
            }

            // Câu hỏi dự báo: khoảng thời gian do backend tách từ câu hỏi là căn cứ cuối cùng
            var fw = intent.IsForecast ? intent.Forecast : null;
            var biArgs = new BiQueryArgs(
                Top: readTop,
                Year: readYear,
                Region: ReadString(args, "region"),
                ProductName: ReadString(args, "product_name"),
                Id: ReadString(args, "id"),
                StartDate: fw != null ? fw.Start.ToString("yyyy-MM-dd") : ReadString(args, "start_date"),
                EndDate: fw != null ? fw.End.ToString("yyyy-MM-dd") : ReadString(args, "end_date"),
                Granularity: fw?.Granularity ?? ReadString(args, "granularity"),
                Forecast: _forecast);

            object? data;
            try { data = await definition.Execute(_repo, biArgs); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi thực thi truy vấn BI {QueryId}", definition.Id);
                return (Error($"Truy vấn {definition.Id} phát sinh lỗi: {ex.Message}"), null, null);
            }

            // Backend Data Filter & Enforcement
            object? rowsForModel = data;
            int? totalRows = null;
            object? shares = null;
            if (data is IEnumerable sequence && data is not string)
            {
                var list = sequence.Cast<object?>().ToList();

                // Lọc theo năm nếu có yêu cầu năm cụ thể
                if (intent.TargetYears.Count > 0)
                {
                    var filtered = new List<object?>();
                    foreach (var item in list)
                    {
                        var y = ExtractYear(item);
                        if (y.HasValue && intent.TargetYears.Contains(y.Value))
                        {
                            filtered.Add(item);
                        }
                    }
                    if (filtered.Count > 0) list = filtered;
                }

                // Q08 trả theo doanh thu giảm dần: xếp lại theo đúng chỉ số và chiều được hỏi trước khi cắt Top N
                if (definition.Id == "Q08")
                {
                    var key = intent.Metrics.Contains(BiMetric.ProfitMargin) ? "ProfitMargin"
                        : intent.Metrics.Contains(BiMetric.Profit) ? "Profit"
                        : intent.Metrics.Contains(BiMetric.Quantity) ? "Quantity" : "Revenue";
                    list = (intent.Order == BiOrder.Lowest
                        ? list.OrderBy(it => ReadNumber(it, key))
                        : list.OrderByDescending(it => ReadNumber(it, key))).ToList();
                }

                // Cắt theo Limit nếu có yêu cầu Top N
                if (intent.Limit.HasValue && list.Count > intent.Limit.Value)
                {
                    list = list.Take(intent.Limit.Value).ToList();
                }

                // Lọc bỏ cột thừa nếu chỉ hỏi SingleMetric
                if (intent.ResponseType == QuestionResponseType.SingleMetric)
                {
                    if (intent.Metrics.Contains(BiMetric.Revenue))
                    {
                        list = list.Select(it => (object?)ProjectItem(it, "Year", "Revenue")).ToList();
                    }
                    else if (intent.Metrics.Contains(BiMetric.Profit))
                    {
                        list = list.Select(it => (object?)ProjectItem(it, "Year", "Profit")).ToList();
                    }
                }
                else if (intent.ResponseType == QuestionResponseType.MultiMetric)
                {
                    if (intent.Metrics.Contains(BiMetric.Revenue) && intent.Metrics.Contains(BiMetric.Profit))
                    {
                        list = list.Select(it => (object?)ProjectItem(it, "Year", "Revenue", "Profit")).ToList();
                    }
                }

                data = list;
                totalRows = list.Count;
                rowsForModel = list.Take(MaxRowsForModel).ToList();

                // Tỷ trọng tính sẵn ở máy chủ để model không tự chia (Gemini từng tính sai % theo khu vực); chỉ khi có đủ các dòng
                double totalRevenue = list.Sum(it => ReadNumber(it, "Revenue"));
                if (list.Count > 1 && !intent.Limit.HasValue && totalRevenue > 0)
                {
                    double totalProfit = list.Sum(it => ReadNumber(it, "Profit"));
                    shares = new
                    {
                        total_revenue = totalRevenue,
                        total_profit = totalProfit,
                        revenue_share_pct = list.Select(it => Math.Round(ReadNumber(it, "Revenue") / totalRevenue * 100, 2)).ToList(),
                        profit_share_pct = totalProfit > 0 ? list.Select(it => Math.Round(ReadNumber(it, "Profit") / totalProfit * 100, 2)).ToList() : null
                    };
                }
            }

            var payload = JsonSerializer.Serialize(new { query_id = definition.Id, total_rows = totalRows, rows = rowsForModel, shares }, JsonOptions);
            return (payload, definition.Id, data);
        }

        // Model chọn truy vấn theo mô tả nên có thể nhầm; ý định đã phân tích ở backend là căn cứ cuối cùng
        private string? EnforceIntentQuery(string? queryId, QuestionIntent intent)
        {
            if (queryId == null) return null;
            var id = queryId.ToUpperInvariant();
            string? mapped = null;
            if (intent.IsForecast && id != "FORECAST")
                mapped = "FORECAST";
            else if (intent.Dimension == "product_group" && id is "Q06" or "Q07" or "Q06_LOW" or "Q07_LOW")
                mapped = "Q08";
            else if (intent.Order == BiOrder.Lowest && id is "Q06" or "Q07")
                mapped = id + "_LOW";
            else if (intent.Order == BiOrder.Highest && id is "Q06_LOW" or "Q07_LOW")
                mapped = id[..3];

            if (mapped == null) return queryId;
            _logger.LogInformation("AI Copilot: đổi truy vấn {From} -> {To} theo ý định câu hỏi", queryId, mapped);
            return mapped;
        }

        private static double ReadNumber(object? item, string key)
        {
            object? v = null;
            if (item is IDictionary<string, object> dict)
                v = dict.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
            else if (item != null)
                v = item.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.GetValue(item);
            return v != null && double.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
        }

        private static int? ExtractYear(object? item)
        {
            if (item is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    if (string.Equals(kvp.Key, "Year", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                    {
                        if (int.TryParse(kvp.Value.ToString(), out var y)) return y;
                    }
                }
            }
            else if (item != null)
            {
                var prop = item.GetType().GetProperty("Year");
                if (prop != null)
                {
                    var val = prop.GetValue(item);
                    if (val != null && int.TryParse(val.ToString(), out var y)) return y;
                }
            }
            return null;
        }

        private static object ProjectItem(object? item, params string[] allowedKeys)
        {
            var res = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (item is IDictionary<string, object> dict)
            {
                foreach (var k in allowedKeys)
                {
                    var match = dict.FirstOrDefault(x => string.Equals(x.Key, k, StringComparison.OrdinalIgnoreCase));
                    if (match.Key != null) res[match.Key] = match.Value;
                }
            }
            else if (item != null)
            {
                foreach (var k in allowedKeys)
                {
                    var prop = item.GetType().GetProperty(k, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (prop != null) res[prop.Name] = prop.GetValue(item);
                }
            }
            return res.Count > 0 ? res : item!;
        }

        private async Task<JsonObject> CompleteAsync(JsonArray messages, JsonArray? tools, CancellationToken budgetToken, DateTime deadline, CancellationToken requestAborted)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await CompleteOnceAsync(messages, tools, budgetToken);
                }
                catch (LlmException ex) when (ex.Kind == LlmFailureKind.Overloaded && attempt < _options.MaxRetries
                                              && (deadline - DateTime.UtcNow).TotalSeconds >= 8)
                {
                    _logger.LogWarning("Gemini quá tải (HTTP 503), thử lại lần {Attempt} sau 1 giây", attempt + 1);
                    await Task.Delay(TimeSpan.FromSeconds(1), budgetToken);
                }
                catch (OperationCanceledException ex) when (!requestAborted.IsCancellationRequested)
                {
                    // Hết ngân sách thời gian (không phải người dùng hủy yêu cầu)
                    throw new LlmException(LlmFailureKind.Timeout, $"Gemini không phản hồi trong {_options.TimeoutSeconds} giây.", null, ex);
                }
            }
        }

        private async Task<JsonObject> CompleteOnceAsync(JsonArray messages, JsonArray? tools, CancellationToken cancellationToken)
        {
            var body = new JsonObject
            {
                ["model"] = _options.Model,
                ["messages"] = messages.DeepClone()
            };
            if (tools != null)
            {
                body["tools"] = tools.DeepClone();
                body["tool_choice"] = "auto";
            }

            var endpoint = _options.BaseUrl.TrimEnd('/') + "/chat/completions";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new LlmException(LlmFailureKind.Network, "Không kết nối được tới Gemini API: " + ex.Message, null, ex);
            }

            using (response)
            {
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    var kind = status switch
                    {
                        400 when responseText.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) => LlmFailureKind.Auth,
                        401 or 403 => LlmFailureKind.Auth,
                        429 => LlmFailureKind.Quota,
                        500 or 502 or 503 or 504 => LlmFailureKind.Overloaded,
                        _ => LlmFailureKind.BadResponse
                    };
                    // Nội dung lỗi của Google không chứa API key; vẫn cắt ngắn để log gọn
                    _logger.LogError("Gemini API lỗi {Kind} (HTTP {StatusCode}): {ErrorResponse}", kind, status, Truncate(responseText, 300));
                    throw new LlmException(kind, $"Gemini API trả về HTTP {status}.", status);
                }

                JsonNode? root;
                try { root = JsonNode.Parse(responseText); }
                catch (JsonException ex) { throw new LlmException(LlmFailureKind.BadResponse, "Phản hồi Gemini không phải JSON hợp lệ.", null, ex); }

                // Tách bản sao khỏi cây JSON gốc: message sẽ được thêm vào danh sách hội thoại (một node chỉ có một cha)
                return (root?["choices"]?[0]?["message"] as JsonObject)?.DeepClone().AsObject()
                    ?? throw new LlmException(LlmFailureKind.BadResponse, "Phản hồi Gemini không có choices[0].message.");
            }
        }

        private static JsonArray BuildTools(IReadOnlyList<BiQueryDefinition> allowed)
        {
            var catalog = string.Join("\n", allowed.Select(q =>
                $"- {q.Id}: {q.Description}" + (q.Parameters.Length > 0 ? $" (tham số: {string.Join(", ", q.Parameters)})" : string.Empty)));

            return new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = ToolName,
                        ["description"] = "Chạy một truy vấn phân tích có sẵn trên kho dữ liệu DWH_KhangNghi và trả về số liệu thực tế (JSON). " +
                                          "Chỉ truyền các tham số mà truy vấn hỗ trợ. Danh mục truy vấn được phép:\n" + catalog,
                        ["parameters"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["query_id"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["enum"] = new JsonArray(allowed.Select(q => (JsonNode?)JsonValue.Create(q.Id)).ToArray()),
                                    ["description"] = "Mã truy vấn trong danh mục chuẩn hóa BiQueryCatalog"
                                },
                                ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "Số dòng cần lấy cho truy vấn Top N (mặc định 10, tối đa 50)" },
                                ["year"] = new JsonObject { ["type"] = "integer", ["description"] = "Năm cần lọc (2022 - 2025)" },
                                ["region"] = new JsonObject { ["type"] = "string", ["description"] = "Tên khu vực cần lọc (chỉ dùng cho OVERVIEW)" },
                                ["product_name"] = new JsonObject { ["type"] = "string", ["description"] = "Tên sản phẩm cần lọc (cho Q24)" },
                                ["id"] = new JsonObject { ["type"] = "string", ["description"] = "Mã khách hàng hoặc mã đơn hàng (cho CUSTOMER hoặc ORDER)" },
                                ["start_date"] = new JsonObject { ["type"] = "string", ["description"] = "Ngày bắt đầu kỳ dự báo YYYY-MM-DD (chỉ cho FORECAST, trong 2026-01-01 .. 2027-12-31)" },
                                ["end_date"] = new JsonObject { ["type"] = "string", ["description"] = "Ngày kết thúc kỳ dự báo YYYY-MM-DD (chỉ cho FORECAST)" },
                                ["granularity"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("month", "week", "day"), ["description"] = "Mức chi tiết dự báo (chỉ cho FORECAST)" }
                            },
                            ["required"] = new JsonArray("query_id")
                        }
                    }
                }
            };
        }

        private static string BuildSystemPrompt(string? role, QuestionIntent? intent = null) =>
            CopilotResponseRules.BuildSystemPrompt(role, ToolName, intent);

        private static int? ReadInt(JsonNode? args, string name)
        {
            var node = args?[name];
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<double>(out var d)) return (int)d;
            return value.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed) ? parsed : null;
        }

        private static string? ReadString(JsonNode? args, string name) =>
            args?[name] is JsonValue value && value.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

        private static string Error(string message) => JsonSerializer.Serialize(new { error = message }, JsonOptions);

        private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "...";
    }
}
