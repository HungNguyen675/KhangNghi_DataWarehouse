using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
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
        private readonly ILogger<NlqController> _logger;

        public NlqController(IDwhAnalyticsRepository repo, ILlmCopilotService llm, ILogger<NlqController> logger)
        {
            _repo = repo;
            _llm = llm;
            _logger = logger;
        }

        // Số tiền rút gọn: từ 1 tỷ hiện "tỷ ₫", nhỏ hơn hiện "triệu ₫"
        private static string Short(double v) => Math.Abs(v) >= 1e9 ? $"{v / 1e9:N2} tỷ ₫" : $"{v / 1e6:N2} triệu ₫";

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
                    Answer = $"Kho dữ liệu DWH_KhangNghi hiện chỉ lưu trữ dữ liệu hoạt động kinh doanh giai đoạn **2022 – 2025**. Năm **{outYears}** không có dữ liệu trong hệ thống.",
                    Data = (object?)null,
                    Source = "intent_guard",
                    QueriesUsed = Array.Empty<string>()
                });
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
            IActionResult FallbackOk(string answer, object? data, string[]? queries = null)
            {
                if (queries != null && queries.Length > 0)
                {
                    foreach (var qId in queries)
                    {
                        if (!BiQueryCatalog.IsAllowed(qId, role))
                        {
                            return Ok(new
                            {
                                Answer = $"Bạn không có quyền truy cập dữ liệu nghiệp vụ này theo phân quyền vai trò ({role}).\n\n*(Đang hiển thị dữ liệu tra cứu trực tiếp từ kho DWH Khang Nghị)*",
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
                    Answer = answer + "\n\n*(Đang hiển thị dữ liệu tra cứu trực tiếp từ kho DWH Khang Nghị)*",
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
                    return FallbackOk("Hệ thống từ chối yêu cầu. Các câu lệnh thao tác cơ sở dữ liệu và yêu cầu tiết lộ thông tin bí mật không được phép thực thi.", null);
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
                            string ans = $"Lợi nhuận của Công ty Khang Nghị năm {year} đạt **{profit:N0} ₫** (khoảng **{profit / 1e9:N2} tỷ ₫**).";
                            var filtered = new[] { new { Year = year, Profit = row.Profit } };
                            return FallbackOk(ans, filtered, new[] { "Q01" });
                        }
                        else
                        {
                            double revenue = Convert.ToDouble(row.Revenue);
                            string ans = $"Doanh thu của Công ty Khang Nghị năm {year} đạt **{revenue:N0} ₫** (khoảng **{revenue / 1e9:N2} tỷ ₫**).";
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
                        string ans = $"Năm {year}, Công ty Khang Nghị đạt Doanh thu: **{rev:N0} ₫** ({rev / 1e9:N2} tỷ ₫) và Lợi nhuận: **{prf:N0} ₫** ({prf / 1e9:N2} tỷ ₫, Biên lợi nhuận: **{margin:N2}%**).";
                        var filtered = new[] { new { Year = year, Revenue = row.Revenue, Profit = row.Profit } };
                        return FallbackOk(ans, filtered, new[] { "Q01" });
                    }
                }

                // Xử lý Trường hợp E: So sánh giữa các năm cụ thể (VD: So sánh doanh thu 2022 và 2025)
                if (intent.ResponseType == QuestionResponseType.Comparison && intent.TargetYears.Count >= 2)
                {
                    var rawData = (await _repo.Q01_RevenueByYearAsync(null)).ToList();
                    var filtered = new List<dynamic>();
                    var sbComp = new StringBuilder();
                    sbComp.AppendLine($"So sánh kết quả kinh doanh giữa các năm ({string.Join(" và ", intent.TargetYears)}):");
                    foreach (dynamic r in rawData)
                    {
                        int y = Convert.ToInt32(r.Year);
                        if (intent.TargetYears.Contains(y))
                        {
                            filtered.Add(r);
                            double rev = Convert.ToDouble(r.Revenue);
                            double prf = Convert.ToDouble(r.Profit);
                            sbComp.AppendLine($"- Năm {y}: Doanh thu **{rev:N0} ₫** ({rev / 1e9:N2} tỷ ₫), Lợi nhuận **{prf:N0} ₫** ({prf / 1e9:N2} tỷ ₫).");
                        }
                    }
                    return FallbackOk(sbComp.ToString().TrimEnd(), filtered, new[] { "Q01" });
                }

                // Xử lý Trường hợp E2: Doanh thu các năm (toàn bộ 2022 - 2025)
                if (intent.ResponseType == QuestionResponseType.Comparison && intent.IsAllYears)
                {
                    var data = await _repo.Q01_RevenueByYearAsync(null);
                    return FallbackOk("Tổng doanh thu và lợi nhuận của Công ty Khang Nghị qua các năm: 2022, 2023, 2024, 2025:", data, new[] { "Q01" });
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
                        "ProfitMargin" => $"**{v:N2}%**",
                        "Quantity" => $"**{v:N0}** sản phẩm",
                        _ => $"**{v:N0} ₫** ({Short(v)})"
                    };
                    var first = ranked[0];
                    string ans = ranked.Count == 1
                        ? $"Nhóm sản phẩm có {name} {(asc ? "thấp" : "cao")} nhất là **{first["Category"]}**: {Fmt(Val(first))}."
                        : $"{char.ToUpper(name[0]) + name[1..]} theo nhóm sản phẩm, xếp {(asc ? "tăng" : "giảm")} dần:";
                    return FallbackOk(ans, ranked, new[] { "Q08" });
                }

                // Xử lý Trường hợp C: Top N / Khách hàng / Sản phẩm
                if (intent.Dimension == "customer")
                {
                    // Chưa có truy vấn xếp khách hàng tăng dần: báo rõ thay vì trả nhầm danh sách cao nhất
                    if (intent.Order == BiOrder.Lowest)
                        return FallbackOk("Hệ thống chưa có truy vấn xếp hạng khách hàng theo doanh thu **thấp nhất**. Bạn có thể hỏi: 'top 10 khách hàng doanh thu cao nhất'.", null);
                    int top = intent.Limit ?? 10;
                    var data = await _repo.Q11_TopCustomersByRevenueAsync(top);
                    string ans = top == 1
                        ? "Khách hàng có doanh số mua hàng lớn nhất tại Khang Nghị (Khách hàng VIP đứng đầu):"
                        : $"Top {top} khách hàng thân thiết có doanh số mua hàng lớn nhất:";
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
                    if (rows.Count == 0) return FallbackOk("Chưa có sản phẩm nào phát sinh bán để xếp hạng.", null, new[] { qid });

                    string scope = asc ? " (trong số sản phẩm đã phát sinh bán)" : "";
                    dynamic f0 = rows[0];
                    double v0 = Convert.ToDouble(byProfit ? f0.Profit : f0.Revenue);
                    string ans = top == 1
                        ? $"Sản phẩm có {metric} {(asc ? "thấp" : "cao")} nhất{scope} là **{f0.ProductName}**: **{v0:N0} ₫** ({Short(v0)})."
                        : $"Top {top} sản phẩm có {metric} {(asc ? "thấp" : "cao")} nhất{scope}, xếp {(asc ? "tăng" : "giảm")} dần:";
                    return FallbackOk(ans, rows, new[] { qid });
                }

                // Xử lý Hàng tồn kho / Cảnh báo sắp hết
                if (intent.Dimension == "inventory")
                {
                    int top = intent.Limit ?? 10;
                    var data = await _repo.Q28_ReorderLevelAlertsAsync(top);
                    return FallbackOk("Danh sách sản phẩm có số lượng tồn kho thấp dưới mức an toàn cần đặt hàng lại:", data, new[] { "Q28" });
                }

                // Xử lý Quý
                if (text.Contains("quý"))
                {
                    int? qYear = intent.TargetYears.Count == 1 ? intent.TargetYears[0] : (int?)null;
                    var data = await _repo.Q02_RevenueByQuarterAsync(qYear);
                    string ans = qYear.HasValue
                        ? $"Tổng hợp doanh thu và lợi nhuận theo từng quý của năm {qYear.Value}:"
                        : "Tổng hợp doanh thu và lợi nhuận theo từng quý của các năm:";
                    return FallbackOk(ans, data, new[] { "Q02" });
                }

                // Vận chuyển
                if (intent.Dimension == "shipping" || text.Contains("vận chuyển") || text.Contains("giao hàng") || text.Contains("phí ship") || text.Contains("shipper") || text.Contains("cước"))
                {
                    var data = await _repo.Q32_ShippingCostByShipperAsync();
                    return FallbackOk("Thống kê chi phí vận chuyển phân bổ theo từng đơn vị vận chuyển:", data, new[] { "Q32" });
                }

                // Mua hàng / NCC
                if (intent.Dimension == "purchase" || text.Contains("nhà cung cấp") || text.Contains("ncc") || text.Contains("nhập hàng"))
                {
                    var data = await _repo.Q21_TopSuppliersByAmountAsync();
                    return FallbackOk("Top các nhà cung cấp có tổng giá trị nhập hàng lớn nhất:", data, new[] { "Q21" });
                }

                // Khu vực địa lý
                if (text.Contains("khu vực") || text.Contains("vùng") || text.Contains("địa lý"))
                {
                    var data = await _repo.Q15_RevenueByRegionAsync();
                    return FallbackOk("Doanh thu và lợi nhuận phân bổ theo từng khu vực địa lý:", data, new[] { "Q15" });
                }

                // Tổng quan (Sử dụng đúng mã truy vấn OVERVIEW đã đăng ký trong BiQueryCatalog)
                if (intent.ResponseType == QuestionResponseType.Overview || text.Contains("tổng quan") || text.Contains("tóm tắt") || text.Contains("kpi") || text.Contains("tổng thể"))
                {
                    var data = await _repo.GetDashboardSummaryAsync(null, null);
                    return FallbackOk($"Tổng quan tình hình kinh doanh: Tổng doanh thu = {data.TotalRevenue:N0} ₫, Tổng lợi nhuận = {data.TotalProfit:N0} ₫, Biên lợi nhuận trung bình = {data.ProfitMargin:N2}%, Số đơn hàng = {data.TotalOrders:N0}.", data, new[] { "OVERVIEW" });
                }

                // Mặc định câu hỏi doanh thu chung nếu không có năm cụ thể
                if (text.Contains("doanh thu") || text.Contains("tiền bán") || text.Contains("doanh số") || text.Contains("lợi nhuận") || text.Contains("tiền lãi") || text.Contains("lãi"))
                {
                    var data = await _repo.Q01_RevenueByYearAsync(null);
                    return FallbackOk("Tổng doanh thu và lợi nhuận của Công ty Khang Nghị qua các năm: 2022, 2023, 2024, 2025:", data, new[] { "Q01" });
                }

                return FallbackOk(CopilotResponseRules.EmptyDataMessage + " Gợi ý câu hỏi: 'doanh thu năm 2022', 'doanh thu các năm', 'top 5 sản phẩm', 'hàng sắp hết', 'khách hàng doanh thu cao nhất', 'tổng quan kinh doanh'.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi truy vấn DWH trong cơ chế offline fallback của NLQ");
                return StatusCode(500, new { Answer = CopilotResponseRules.ConnectionErrorMessage, Data = (object?)null });
            }
        }
    }
}
