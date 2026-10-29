using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using KhangNghi.Infrastructure.Services;
using KhangNghi.Infrastructure.Services.Copilot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/nlq")]
    [Authorize]
    public class NlqController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;
        private readonly ILlmCopilotService _llm;
        private readonly ISalesForecastService _forecast;
        private readonly ILogger<NlqController> _logger;

        public NlqController(IDwhAnalyticsRepository repo, ILlmCopilotService llm, ISalesForecastService forecast, ILogger<NlqController> logger)
        {
            _repo = repo;
            _llm = llm;
            _forecast = forecast;
            _logger = logger;
        }

        // Định dạng web dùng chung (số kiểu Việt Nam, kết luận in đậm, gạch đầu dòng)
        private static string Short(double v) => CopilotWebFormat.Money(v);
        private static double D(IDictionary<string, object> r, string key) => Convert.ToDouble(r.TryGetValue(key, out var v) ? v ?? 0 : 0);
        // Tên hiển thị của dòng: ưu tiên cột *Name (khách hàng, nhà cung cấp…), không thì cột chữ đầu tiên
        private static string Label(IDictionary<string, object> r) =>
            (r.FirstOrDefault(kv => kv.Key.EndsWith("Name", StringComparison.OrdinalIgnoreCase) && kv.Value is string).Value
             ?? r.Values.FirstOrDefault(v => v is string))?.ToString() ?? "-";
        private static List<IDictionary<string, object>> Rows(IEnumerable<dynamic> d) => d.Cast<IDictionary<string, object>>().ToList();

        // Doanh thu các năm: kết luận năm gần nhất và mức tăng so với năm đầu kỳ
        private static string AllYearsAnswer(List<IDictionary<string, object>> years)
        {
            if (years.Count == 0) return CopilotResponseRules.EmptyDataMessage;
            var sorted = years.OrderBy(r => Convert.ToInt32(r["Year"])).ToList();
            var (first, last) = (sorted[0], sorted[^1]);
            var best = sorted.OrderByDescending(r => D(r, "Revenue")).First();
            return CopilotWebFormat.Answer(
                $"Tổng doanh thu {first["Year"]} – {last["Year"]}: **{Short(sorted.Sum(r => D(r, "Revenue")))}**; năm cao nhất là **{best["Year"]}** ({Short(D(best, "Revenue"))}).",
                $"Năm {last["Year"]} **{CopilotWebFormat.Growth(D(last, "Revenue"), D(first, "Revenue"))}** so với năm {first["Year"]}.",
                $"Tổng lợi nhuận: {Short(sorted.Sum(r => D(r, "Profit")))}.");
        }

        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Vui lòng nhập câu hỏi.");
            string text = q.ToLower().Trim();

            // Trích xuất vai trò bảo đảm tương thích với mọi biến thể của JWT claim type
            var role = User.FindFirstValue(ClaimTypes.Role)
                ?? User.FindFirstValue("role")
                ?? User.Claims.FirstOrDefault(c => c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase) || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase))?.Value
                ?? "Director";

            // 1. Phân tích ý định câu hỏi (Question Intent Parser)
            var intent = QuestionIntentParser.Parse(q);
            _logger.LogInformation("AI Copilot intent: q=\"{Question}\" dimension={Dimension} order={Order} metrics=[{Metrics}] limit={Limit} years=[{Years}] type={Type} query={Query}",
                q, intent.Dimension ?? "-", intent.Order, string.Join(",", intent.Metrics), intent.Limit?.ToString() ?? "-",
                string.Join(",", intent.TargetYears), intent.ResponseType, intent.SuggestedQueryId ?? "-");

            // Kiểm soát nghiêm ngặt Trường hợp F: Năm ngoài phạm vi (ngoài 2022 - 2025)
            if (intent.ResponseType == QuestionResponseType.OutOfRange)
            {
                var outYears = intent.OutOfRangeYears.Count > 0 ? string.Join(", ", intent.OutOfRangeYears) : "yêu cầu";
                return Ok(new
                {
                    Answer = CopilotWebFormat.Answer($"Năm **{outYears}** chưa có dữ liệu thực tế trong hệ thống.",
                        "Kho DWH_KhangNghi lưu dữ liệu kinh doanh giai đoạn **2022 – 2025**.",
                        intent.OutOfRangeYears.FirstOrDefault(y => y is "2026" or "2027") is { } fy
                            ? "Cần số **dự báo**? " + CopilotWebFormat.Suggest($"dự báo doanh thu năm {fy}")
                            : CopilotWebFormat.Suggest("doanh thu năm 2025", "so sánh doanh thu các năm")),
                    Data = (object?)null,
                    Source = "intent_guard",
                    QueriesUsed = Array.Empty<string>()
                });
            }

            // Câu hỏi dự báo: kiểm tra quyền, phạm vi mô hình và chỉ số trước khi gọi Gemini
            if (intent.IsForecast)
            {
                if (!BiQueryCatalog.IsAllowed("FORECAST", role))
                    return Ok(new { Answer = CopilotWebFormat.Answer("Bạn **không có quyền** xem dự báo doanh thu.", $"Vai trò hiện tại: {role}. Dự báo dành cho Ban Giám đốc và phòng Kinh doanh."), Data = (object?)null, Source = "intent_guard", QueriesUsed = Array.Empty<string>() });
                if (intent.Forecast == null)
                    return Ok(new { Answer = intent.ForecastProblem ?? $"Mô hình chỉ dự báo doanh thu trong giai đoạn **{CopilotForecast.ModelRangeText}**.", Data = (object?)null, Source = "intent_guard", QueriesUsed = Array.Empty<string>() });
            }

            // Tầng 3: Nếu Gemini hoặc LLM được cấu hình, ưu tiên xử lý qua Gemini Function Calling
            // để áp dụng Model Response Rules (phân tích nghiệp vụ, chọn tool BI, cấu trúc báo cáo Markdown và định dạng số liệu)
            // fallbackReason: lý do dùng chế độ offline, gửi cho giao diện (not_configured | auth | quota | overloaded | timeout | network | bad_response | error)
            string fallbackReason = "not_configured";
            if (_llm.IsConfigured)
            {
                var started = DateTime.UtcNow;
                try
                {
                    var result = await _llm.AskAsync(q, role, HttpContext.RequestAborted);
                    return Ok(new { Answer = result.Answer, Data = result.Data, Source = "llm", QueriesUsed = result.QueriesUsed });
                }
                catch (LlmException ex)
                {
                    fallbackReason = ex.Reason;
                    _logger.LogWarning("AI Copilot: Gemini lỗi {Kind} (HTTP {Status}) sau {Elapsed:N1}s, chuyển sang chế độ offline.",
                        ex.Kind, ex.StatusCode?.ToString() ?? "-", (DateTime.UtcNow - started).TotalSeconds);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !HttpContext.RequestAborted.IsCancellationRequested)
                {
                    fallbackReason = "error";
                    _logger.LogWarning(ex, "AI Copilot: lỗi không xác định khi gọi Gemini, chuyển sang chế độ offline.");
                }
            }

            // Tầng 2: Fallback khi chưa có API key hoặc khi Gemini gặp lỗi kết nối/hết quota
            // Lấy dữ liệu thật 100% từ DWH_KhangNghi qua repository
            // Nhãn nguồn và chế độ offline do giao diện hiển thị, nên câu trả lời không lặp lại; footer chỉ dùng cho ghi chú bắt buộc
            IActionResult FallbackOk(string answer, object? data, string[]? queries = null, string? footer = null)
            {
                if (queries != null && queries.Length > 0)
                {
                    foreach (var qId in queries)
                    {
                        if (!BiQueryCatalog.IsAllowed(qId, role))
                        {
                            return Ok(new
                            {
                                Answer = CopilotWebFormat.Answer("Bạn **không có quyền** xem dữ liệu nghiệp vụ này.", $"Vai trò hiện tại: {role}. Liên hệ quản trị viên nếu cần cấp thêm quyền."),
                                Data = (object?)null,
                                Source = "fallback",
                                FallbackReason = fallbackReason,
                                QueriesUsed = Array.Empty<string>()
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Answer = footer == null ? answer : answer + "\n\n" + footer,
                    Data = data,
                    Source = "fallback",
                    FallbackReason = fallbackReason,
                    QueriesUsed = queries ?? Array.Empty<string>()
                });
            }

            try
            {
                // Kiểm tra bảo mật Prompt Injection
                if (text.Contains("drop table") || text.Contains("select * from") || text.Contains("delete from") || text.Contains("gemini_api_key") || text.Contains("api_key"))
                {
                    return FallbackOk(CopilotWebFormat.Answer("Yêu cầu **bị từ chối**.", "Trợ lý không thực thi câu lệnh thao tác cơ sở dữ liệu và không tiết lộ thông tin cấu hình bí mật."), null);
                }

                // Trường hợp G: Dự báo doanh thu bằng mô hình ModelKLTN (khoảng thời gian đã được kiểm tra ở trên)
                if (intent.IsForecast && intent.Forecast != null)
                {
                    var summary = await CopilotForecast.RunAsync(_forecast, intent.Forecast);
                    return FallbackOk(CopilotForecast.BuildAnswer(summary, intent.Order == BiOrder.Lowest),
                        CopilotForecast.TableRows(summary).ToList(), new[] { "FORECAST" },
                        "*(Số liệu dự báo từ mô hình ModelKLTN, không phải doanh thu thực tế)*");
                }

                // Xử lý Trường hợp A: Hỏi 1 chỉ số cụ thể trong 1 năm (VD: Doanh thu năm 2022, Lợi nhuận năm 2023)
                if (intent.ResponseType == QuestionResponseType.SingleMetric && intent.TargetYears.Count == 1)
                {
                    int year = intent.TargetYears[0];
                    var rawData = (await _repo.Q01_RevenueByYearAsync(year)).ToList();
                    if (rawData.Count > 0)
                    {
                        dynamic row = rawData[0];
                        if (intent.Metrics.Contains(BiMetric.Profit))
                        {
                            double profit = Convert.ToDouble(row.Profit);
                            string ans = CopilotWebFormat.Answer($"Lợi nhuận năm {year}: **{Short(profit)}**.", $"Giá trị chính xác: {CopilotWebFormat.Vnd(profit)}.");
                            var filtered = new[] { new { Year = year, Profit = row.Profit } };
                            return FallbackOk(ans, filtered, new[] { "Q01" });
                        }
                        else
                        {
                            double revenue = Convert.ToDouble(row.Revenue);
                            string ans = CopilotWebFormat.Answer($"Doanh thu năm {year}: **{Short(revenue)}**.", $"Giá trị chính xác: {CopilotWebFormat.Vnd(revenue)}.");
                            var filtered = new[] { new { Year = year, Revenue = row.Revenue } };
                            return FallbackOk(ans, filtered, new[] { "Q01" });
                        }
                    }
                }

                // Xử lý Trường hợp B: Hỏi nhiều chỉ số trong 1 năm (VD: Doanh thu và lợi nhuận năm 2022)
                if (intent.ResponseType == QuestionResponseType.MultiMetric && intent.TargetYears.Count == 1)
                {
                    int year = intent.TargetYears[0];
                    var rawData = (await _repo.Q01_RevenueByYearAsync(year)).ToList();
                    if (rawData.Count > 0)
                    {
                        dynamic row = rawData[0];
                        double rev = Convert.ToDouble(row.Revenue);
                        double prf = Convert.ToDouble(row.Profit);
                        double margin = rev > 0 ? (prf / rev * 100.0) : 0;
                        string ans = CopilotWebFormat.Answer($"Năm {year}: doanh thu **{Short(rev)}**, lợi nhuận **{Short(prf)}**.",
                            $"Biên lợi nhuận: **{CopilotWebFormat.Pct(margin)}**.",
                            $"Giá trị chính xác: doanh thu {CopilotWebFormat.Vnd(rev)}, lợi nhuận {CopilotWebFormat.Vnd(prf)}.");
                        var filtered = new[] { new { Year = year, Revenue = row.Revenue, Profit = row.Profit } };
                        return FallbackOk(ans, filtered, new[] { "Q01" });
                    }
                }

                // Xử lý Trường hợp E: So sánh giữa các năm cụ thể (VD: So sánh doanh thu 2022 và 2025)
                if (intent.ResponseType == QuestionResponseType.Comparison && intent.TargetYears.Count >= 2)
                {
                    var picked = Rows(await _repo.Q01_RevenueByYearAsync(null))
                        .Where(r => intent.TargetYears.Contains(Convert.ToInt32(r["Year"]))).OrderBy(r => Convert.ToInt32(r["Year"])).ToList();
                    if (picked.Count < 2) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q01" });
                    var (r0, rN) = (picked[0], picked[^1]);
                    string ans = CopilotWebFormat.Answer(
                        $"Doanh thu năm {rN["Year"]} đạt **{Short(D(rN, "Revenue"))}**, **{CopilotWebFormat.Growth(D(rN, "Revenue"), D(r0, "Revenue"))}** so với năm {r0["Year"]} ({Short(D(r0, "Revenue"))}).",
                        $"Lợi nhuận: {Short(D(r0, "Profit"))} → **{Short(D(rN, "Profit"))}** ({CopilotWebFormat.Growth(D(rN, "Profit"), D(r0, "Profit"))}).");
                    return FallbackOk(ans, picked, new[] { "Q01" });
                }

                // Xử lý Trường hợp E2: Doanh thu các năm (toàn bộ 2022 - 2025)
                if (intent.ResponseType == QuestionResponseType.Comparison && intent.IsAllYears)
                {
                    var years = Rows(await _repo.Q01_RevenueByYearAsync(null));
                    return FallbackOk(AllYearsAnswer(years), years, new[] { "Q01" });
                }

                // Nhóm sản phẩm (Category): tổng hợp theo nhóm bằng Q08 rồi xếp hạng theo đúng chỉ số được hỏi
                if (intent.Dimension == "product_group")
                {
                    var (key, name) = intent.Metrics.Contains(BiMetric.ProfitMargin) ? ("ProfitMargin", "biên lợi nhuận")
                        : intent.Metrics.Contains(BiMetric.Profit) ? ("Profit", "lợi nhuận")
                        : intent.Metrics.Contains(BiMetric.Quantity) ? ("Quantity", "số lượng bán")
                        : ("Revenue", "doanh thu");
                    bool asc = intent.Order == BiOrder.Lowest;
                    var rows = (await _repo.Q08_RevenueByCategoryAsync()).Cast<IDictionary<string, object>>().ToList();
                    double Val(IDictionary<string, object> r) => Convert.ToDouble(r[key] ?? 0);
                    var ranked = (asc ? rows.OrderBy(Val) : rows.OrderByDescending(Val)).ToList();
                    if (intent.Limit.HasValue) ranked = ranked.Take(intent.Limit.Value).ToList();
                    if (ranked.Count == 0) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q08" });

                    string Fmt(double v) => key switch
                    {
                        "ProfitMargin" => CopilotWebFormat.Pct(v),
                        "Quantity" => CopilotWebFormat.Num(v) + " sản phẩm",
                        _ => Short(v)
                    };
                    var first = ranked[0];
                    string ans = ranked.Count == 1
                        ? CopilotWebFormat.Answer($"Nhóm sản phẩm có {name} {(asc ? "thấp" : "cao")} nhất: **{first["Category"]}** – **{Fmt(Val(first))}**.",
                            key is "Revenue" or "Profit" ? $"Chiếm {CopilotWebFormat.Pct(Val(first) / rows.Sum(Val) * 100, 1)} tổng {name} giai đoạn 2022 – 2025." : null)
                        : CopilotWebFormat.Answer($"{(asc ? "Thấp" : "Cao")} nhất là **{first["Category"]}** với {name} **{Fmt(Val(first))}**.",
                            $"Bảng dưới xếp {ranked.Count} nhóm sản phẩm theo {name} {(asc ? "tăng" : "giảm")} dần.");
                    return FallbackOk(ans, ranked, new[] { "Q08" });
                }

                // Xử lý Trường hợp C: Top N / Khách hàng / Sản phẩm
                if (intent.Dimension == "customer")
                {
                    // Chưa có truy vấn xếp khách hàng tăng dần: báo rõ thay vì trả nhầm danh sách cao nhất
                    if (intent.Order == BiOrder.Lowest)
                        return FallbackOk(CopilotWebFormat.Answer("Hệ thống **chưa có truy vấn** xếp hạng khách hàng theo doanh thu thấp nhất.", CopilotWebFormat.Suggest("top 10 khách hàng doanh thu cao nhất")), null);
                    int top = intent.Limit ?? 10;
                    var data = Rows(await _repo.Q11_TopCustomersByRevenueAsync(top));
                    if (data.Count == 0) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q11" });
                    string ans = top == 1
                        ? CopilotWebFormat.Answer($"Khách hàng có doanh thu cao nhất: **{Label(data[0])}** – **{Short(D(data[0], "Revenue"))}**.",
                            $"Số đơn hàng: {CopilotWebFormat.Num(D(data[0], "OrderCount"))}.")
                        : CopilotWebFormat.Answer($"Dẫn đầu top {data.Count} khách hàng là **{Label(data[0])}** với **{Short(D(data[0], "Revenue"))}**.",
                            $"Tổng doanh thu của {data.Count} khách hàng: {Short(data.Sum(r => D(r, "Revenue")))}.");
                    return FallbackOk(ans, data, new[] { "Q11" });
                }

                if (intent.Dimension == "product")
                {
                    int top = intent.Limit ?? 10;
                    // Chỉ hỏi lợi nhuận -> xếp theo lợi nhuận; còn lại xếp theo doanh thu
                    bool byProfit = intent.Metrics.Contains(BiMetric.Profit) && !intent.Metrics.Contains(BiMetric.Revenue);
                    string metric = byProfit ? "lợi nhuận" : "doanh thu";
                    bool asc = intent.Order == BiOrder.Lowest;
                    string qid = (byProfit ? "Q07" : "Q06") + (asc ? "_LOW" : "");

                    var rows = (asc
                        ? await _repo.GetLowestProductsAsync(top, byProfit)
                        : byProfit ? await _repo.Q07_TopProductsByProfitAsync(top) : await _repo.Q06_TopProductsByRevenueAsync(top)).ToList();
                    if (rows.Count == 0) return FallbackOk("**Chưa có sản phẩm** nào phát sinh bán để xếp hạng.", null, new[] { qid });

                    string scope = asc ? " (trong số sản phẩm đã phát sinh bán)" : "";
                    dynamic f0 = rows[0];
                    double v0 = Convert.ToDouble(byProfit ? f0.Profit : f0.Revenue);
                    string ans = top == 1
                        ? CopilotWebFormat.Answer($"Sản phẩm có {metric} {(asc ? "thấp" : "cao")} nhất{scope}: **{f0.ProductName}** – **{Short(v0)}**.",
                            $"Giá trị chính xác: {CopilotWebFormat.Vnd(v0)}.")
                        : CopilotWebFormat.Answer($"{(asc ? "Thấp" : "Cao")} nhất là **{f0.ProductName}** với {metric} **{Short(v0)}**.",
                            $"Bảng dưới liệt kê top {rows.Count} sản phẩm{scope}, xếp {(asc ? "tăng" : "giảm")} dần.");
                    return FallbackOk(ans, rows, new[] { qid });
                }

                // Xử lý Hàng tồn kho / Cảnh báo sắp hết
                if (intent.Dimension == "inventory")
                {
                    int top = intent.Limit ?? 10;
                    var data = (await _repo.Q28_ReorderLevelAlertsAsync(top)).ToList();
                    if (data.Count == 0) return FallbackOk(CopilotWebFormat.Answer("**Không có sản phẩm** nào tồn kho dưới mức đặt hàng lại."), data, new[] { "Q28" });
                    var worst = data.OrderByDescending(x => x.Deficit).First();
                    string ans = CopilotWebFormat.Answer($"**{data.Count}** sản phẩm đang tồn kho dưới mức đặt hàng lại{(data.Count == top ? $" (hiển thị {top} sản phẩm đầu)" : "")}.",
                        $"Thiếu nhiều nhất: **{worst.ProductName}**, tồn {CopilotWebFormat.Num(worst.QuantityOnHand)} / mức đặt lại {CopilotWebFormat.Num(worst.ReorderLevel)} (thiếu {CopilotWebFormat.Num(worst.Deficit)}).");
                    return FallbackOk(ans, data, new[] { "Q28" });
                }

                // Xử lý Quý
                if (text.Contains("quý"))
                {
                    int? qYear = intent.TargetYears.Count == 1 ? intent.TargetYears[0] : (int?)null;
                    var data = Rows(await _repo.Q02_RevenueByQuarterAsync(qYear));
                    if (data.Count == 0) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q02" });
                    var bestQ = data.OrderByDescending(r => D(r, "Revenue")).First();
                    string ans = CopilotWebFormat.Answer(
                        $"Quý có doanh thu cao nhất{(qYear.HasValue ? $" năm {qYear.Value}" : " giai đoạn 2022 – 2025")}: **{Label(bestQ)}** – **{Short(D(bestQ, "Revenue"))}**.",
                        qYear.HasValue ? $"Tổng doanh thu năm {qYear.Value}: {Short(data.Sum(r => D(r, "Revenue")))}; lợi nhuận {Short(data.Sum(r => D(r, "Profit")))}." : null);
                    return FallbackOk(ans, data, new[] { "Q02" });
                }

                // Vận chuyển
                if (intent.Dimension == "shipping" || text.Contains("vận chuyển") || text.Contains("giao hàng") || text.Contains("phí ship") || text.Contains("shipper") || text.Contains("cước"))
                {
                    var data = Rows(await _repo.Q32_ShippingCostByShipperAsync());
                    if (data.Count == 0) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q32" });
                    double totalShip = data.Sum(r => D(r, "TotalShippingCost"));
                    string ans = CopilotWebFormat.Answer($"Tổng chi phí vận chuyển: **{Short(totalShip)}**.",
                        $"Cao nhất: **{Label(data[0])}** – {Short(D(data[0], "TotalShippingCost"))} ({CopilotWebFormat.Pct(D(data[0], "TotalShippingCost") / totalShip * 100, 1)}).");
                    return FallbackOk(ans, data, new[] { "Q32" });
                }

                // Mua hàng / NCC
                if (intent.Dimension == "purchase" || text.Contains("nhà cung cấp") || text.Contains("ncc") || text.Contains("nhập hàng"))
                {
                    var data = Rows(await _repo.Q21_TopSuppliersByAmountAsync());
                    if (data.Count == 0) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q21" });
                    string ans = CopilotWebFormat.Answer($"Nhà cung cấp có giá trị nhập hàng lớn nhất: **{Label(data[0])}** – **{Short(D(data[0], "TotalPurchaseAmount"))}**.",
                        $"Bảng dưới xếp {data.Count} nhà cung cấp theo giá trị nhập giảm dần.");
                    return FallbackOk(ans, data, new[] { "Q21" });
                }

                // Khu vực địa lý
                if (text.Contains("khu vực") || text.Contains("vùng") || text.Contains("địa lý"))
                {
                    var data = Rows(await _repo.Q15_RevenueByRegionAsync());
                    if (data.Count == 0) return FallbackOk(CopilotResponseRules.EmptyDataMessage, null, new[] { "Q15" });
                    double totalReg = data.Sum(r => D(r, "Revenue"));
                    string ans = CopilotWebFormat.Answer($"Khu vực có doanh thu cao nhất: **{Label(data[0])}** – **{Short(D(data[0], "Revenue"))}**.",
                        $"Chiếm {CopilotWebFormat.Pct(D(data[0], "Revenue") / totalReg * 100, 1)} tổng doanh thu; lợi nhuận {Short(D(data[0], "Profit"))}.");
                    return FallbackOk(ans, data, new[] { "Q15" });
                }

                // Tổng quan (Sử dụng đúng mã truy vấn OVERVIEW đã đăng ký trong BiQueryCatalog)
                if (intent.ResponseType == QuestionResponseType.Overview || text.Contains("tổng quan") || text.Contains("tóm tắt") || text.Contains("kpi") || text.Contains("tổng thể"))
                {
                    var data = await _repo.GetDashboardSummaryAsync(null, null);
                    string ans = CopilotWebFormat.Answer($"Tổng doanh thu giai đoạn 2022 – 2025: **{Short(data.TotalRevenue)}**.",
                        $"Lợi nhuận: **{Short(data.TotalProfit)}** (biên lợi nhuận {CopilotWebFormat.Pct(data.ProfitMargin)}).",
                        $"Số đơn hàng: {CopilotWebFormat.Num(data.TotalOrders)}; khách hàng: {CopilotWebFormat.Num(data.TotalCustomers)}.",
                        data.LowStockAlertCount > 0 ? $"Cảnh báo tồn kho: {CopilotWebFormat.Num(data.LowStockAlertCount)} dòng tồn dưới mức đặt hàng lại." : null);
                    return FallbackOk(ans, data, new[] { "OVERVIEW" });
                }

                // Mặc định câu hỏi doanh thu chung nếu không có năm cụ thể
                if (text.Contains("doanh thu") || text.Contains("tiền bán") || text.Contains("doanh số") || text.Contains("lợi nhuận") || text.Contains("tiền lãi") || text.Contains("lãi"))
                {
                    var years = Rows(await _repo.Q01_RevenueByYearAsync(null));
                    return FallbackOk(AllYearsAnswer(years), years, new[] { "Q01" });
                }

                return FallbackOk(CopilotWebFormat.Answer("Trợ lý **chưa tìm được dữ liệu** phù hợp với câu hỏi này.",
                    "Trợ lý trả lời về doanh thu, lợi nhuận, sản phẩm, khách hàng, tồn kho, nhập hàng, vận chuyển và dự báo doanh thu.",
                    CopilotWebFormat.Suggest("doanh thu năm 2025", "top 5 sản phẩm doanh thu cao nhất", "hàng sắp hết", "tổng quan kinh doanh", "dự báo doanh thu 6 tháng tới")), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi truy vấn DWH trong cơ chế offline fallback của NLQ");
                return StatusCode(500, new { Answer = CopilotResponseRules.ConnectionErrorMessage, Data = (object?)null });
            }
        }
    }
}
