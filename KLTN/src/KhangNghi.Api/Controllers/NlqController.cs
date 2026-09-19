using System.Linq;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/nlq")]
    [Authorize]
    public class NlqController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public NlqController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Vui lòng nhập câu hỏi.");
            string text = q.ToLower();

            try
            {
                if (text.Contains("hàng sắp hết") || text.Contains("tồn kho thấp") || text.Contains("cảnh báo") || text.Contains("hết hàng"))
                {
                    var data = await _repo.Q28_ReorderLevelAlertsAsync();
                    return Ok(new { Answer = "Dưới đây là danh sách các sản phẩm đang có số lượng tồn kho thấp dưới mức an toàn, cần nhập thêm hàng:", Data = data });
                }
                
                if (text.Contains("doanh thu") && (text.Contains("năm") || text.Contains("các năm")))
                {
                    var data = await _repo.Q01_RevenueByYearAsync();
                    return Ok(new { Answer = "Tổng doanh thu theo từng năm của công ty như sau:", Data = data });
                }

                if (text.Contains("sản phẩm") && text.Contains("doanh thu") && (text.Contains("cao nhất") || text.Contains("top")))
                {
                    var data = await _repo.Q06_TopProductsByRevenueAsync();
                    return Ok(new { Answer = "Đây là Top 10 sản phẩm bán chạy nhất, mang lại doanh thu cao nhất:", Data = data });
                }

                if (text.Contains("sản phẩm") && text.Contains("lợi nhuận"))
                {
                    var data = await _repo.Q07_TopProductsByProfitAsync();
                    return Ok(new { Answer = "Đây là Top 10 sản phẩm mang lại lợi nhuận cao nhất cho công ty:", Data = data });
                }
                
                if (text.Contains("khách hàng") && (text.Contains("doanh thu") || text.Contains("mua nhiều") || text.Contains("vip")))
                {
                    var data = await _repo.Q11_TopCustomersByRevenueAsync();
                    return Ok(new { Answer = "Danh sách Top 10 khách hàng thân thiết có doanh số mua hàng cao nhất (Khách hàng VIP):", Data = data });
                }

                if (text.Contains("nhà cung cấp") && (text.Contains("top") || text.Contains("lớn nhất") || text.Contains("mua nhiều")))
                {
                    var data = await _repo.Q21_TopSuppliersByAmountAsync();
                    return Ok(new { Answer = "Top các nhà cung cấp có tổng giá trị nhập hàng lớn nhất:", Data = data });
                }
                
                if (text.Contains("chi phí vận chuyển") || text.Contains("phí ship"))
                {
                    var data = await _repo.Q32_ShippingCostByShipperAsync();
                    return Ok(new { Answer = "Thống kê chi phí vận chuyển được phân bổ theo từng đơn vị vận chuyển:", Data = data });
                }

                if (text.Contains("tổng quan") || text.Contains("tóm tắt"))
                {
                    var data = await _repo.GetDashboardSummaryAsync(null, null);
                    return Ok(new { Answer = $"Tổng quan tình hình kinh doanh: Tổng doanh thu = {data.TotalRevenue:N0}, Tổng lợi nhuận = {data.TotalProfit:N0}, Biên lợi nhuận trung bình = {data.ProfitMargin:P2}.", Data = data });
                }

                return Ok(new { Answer = "Xin lỗi, tôi chưa hiểu rõ ý của bạn. Hãy thử hỏi các câu có chứa từ khóa như: 'doanh thu các năm', 'top sản phẩm doanh thu', 'hàng sắp hết', 'tổng quan', 'chi phí vận chuyển', 'top khách hàng'.", Data = (object?)null });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Answer = $"Đã xảy ra lỗi hệ thống: {ex.Message}", Data = (object?)null });
            }
        }
    }
}
