using System.Linq;
using System.Security.Claims;
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

        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Vui lòng nhập câu hỏi.");
            string text = q.ToLower();

            // Tầng 3: Nếu LLM được cấu hình (ví dụ: Google Gemini / OpenAI), ưu tiên xử lý qua LLM
            // để áp dụng đầy đủ Model Response Rules (phân tích nghiệp vụ, tạo MDX, cấu trúc câu trả lời và định dạng Markdown)
            if (_llm.IsConfigured)
            {
                try
                {
                    var role = User.FindFirstValue(ClaimTypes.Role);
                    var result = await _llm.AskAsync(q, role, HttpContext.RequestAborted);
                    return Ok(new { Answer = result.Answer, Data = result.Data, Source = "llm", QueriesUsed = result.QueriesUsed });
                }
                catch (System.Exception ex) when (ex is not System.OperationCanceledException || !HttpContext.RequestAborted.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "AI Copilot tầng 3 không phản hồi, tự động chuyển sang chế độ quy tắc từ khóa fallback.");
                }
            }

            // Fallback (hoặc khi chưa cấu hình LLM): tra cứu theo từ khóa trực tiếp từ DWH
            try
            {
                if (text.Contains("hàng sắp hết") || text.Contains("tồn kho thấp") || text.Contains("cảnh báo") || text.Contains("hết hàng"))
                {
                    var data = await _repo.Q28_ReorderLevelAlertsAsync();
                    return Ok(new { Answer = "Danh sách sản phẩm có số lượng tồn kho thấp dưới mức an toàn cần bổ sung:", Data = data });
                }
                
                if (text.Contains("doanh thu") && (text.Contains("năm") || text.Contains("các năm")))
                {
                    var data = await _repo.Q01_RevenueByYearAsync();
                    return Ok(new { Answer = "Tổng doanh thu và lợi nhuận theo từng năm của công ty:", Data = data });
                }

                if (text.Contains("sản phẩm") && text.Contains("doanh thu") && (text.Contains("cao nhất") || text.Contains("top")))
                {
                    var data = await _repo.Q06_TopProductsByRevenueAsync();
                    return Ok(new { Answer = "Top 10 sản phẩm mang lại doanh thu cao nhất:", Data = data });
                }

                if (text.Contains("sản phẩm") && text.Contains("lợi nhuận"))
                {
                    var data = await _repo.Q07_TopProductsByProfitAsync();
                    return Ok(new { Answer = "Top 10 sản phẩm mang lại lợi nhuận cao nhất:", Data = data });
                }
                
                if (text.Contains("khách hàng") && (text.Contains("doanh thu") || text.Contains("mua nhiều") || text.Contains("vip")))
                {
                    var data = await _repo.Q11_TopCustomersByRevenueAsync();
                    return Ok(new { Answer = "Top 10 khách hàng thân thiết có doanh số mua hàng cao nhất (Khách hàng VIP):", Data = data });
                }

                if (text.Contains("nhà cung cấp") && (text.Contains("top") || text.Contains("lớn nhất") || text.Contains("mua nhiều")))
                {
                    var data = await _repo.Q21_TopSuppliersByAmountAsync();
                    return Ok(new { Answer = "Top các nhà cung cấp có tổng giá trị nhập hàng lớn nhất:", Data = data });
                }
                
                if (text.Contains("chi phí vận chuyển") || text.Contains("phí ship"))
                {
                    var data = await _repo.Q32_ShippingCostByShipperAsync();
                    return Ok(new { Answer = "Thống kê chi phí vận chuyển phân bổ theo từng đơn vị vận chuyển:", Data = data });
                }

                if (text.Contains("tổng quan") || text.Contains("tóm tắt"))
                {
                    var data = await _repo.GetDashboardSummaryAsync(null, null);
                    return Ok(new { Answer = $"Tổng quan tình hình kinh doanh: Tổng doanh thu = {data.TotalRevenue:N0} ₫, Tổng lợi nhuận = {data.TotalProfit:N0} ₫, Biên lợi nhuận trung bình = {data.ProfitMargin:N2}%.", Data = data });
                }

                return Ok(new { Answer = CopilotResponseRules.EmptyDataMessage + " Gợi ý câu hỏi: 'doanh thu các năm', 'top sản phẩm doanh thu', 'hàng sắp hết', 'tổng quan', 'chi phí vận chuyển', 'top khách hàng'.", Data = (object?)null });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi truy vấn DWH cho NLQ query");
                return StatusCode(500, new { Answer = CopilotResponseRules.ConnectionErrorMessage, Data = (object?)null });
            }
        }
    }
}
