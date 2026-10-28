using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/cross-analysis")]
    [Authorize] // Trang Phân tích chéo mở cho mọi vai trò đã đăng nhập
    public class CrossAnalysisController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public CrossAnalysisController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        // Q38: Mặt hàng nào sắp hết hàng? (số ngày tồn kho thấp nhất)
        [HttpGet("stockout-risk")]
        public async Task<IActionResult> GetStockoutRisk([FromQuery] int top = 10) => Ok(await _repo.Q38_HighSalesLowStockRiskAsync(top));

        // Q39: Mặt hàng nào tồn quá nhiều so với sức bán? (số ngày tồn kho cao nhất)
        [HttpGet("overstock-risk")]
        public async Task<IActionResult> GetOverstockRisk([FromQuery] int top = 10) => Ok(await _repo.Q39_HighPurchaseLowSalesRiskAsync(top));

        // Diễn giải số ngày tồn kho của một mặt hàng: tồn mới nhất từng kho + bán 3 tháng gần nhất
        [HttpGet("stock-cover/{productKey:int}")]
        public async Task<IActionResult> GetStockCoverDetail(int productKey)
        {
            var (stock, sales) = await _repo.GetStockCoverDetailAsync(productKey);
            return Ok(new { Stock = stock, Sales = sales });
        }

        // Q40: Khu vực nào tốn phí giao hàng nhiều nhất trên mỗi đồng doanh thu?
        [HttpGet("shipping-efficiency")]
        public async Task<IActionResult> GetShippingEfficiency() => Ok(await _repo.Q40_RevenueVsShippingEfficiencyAsync());
    }
}
