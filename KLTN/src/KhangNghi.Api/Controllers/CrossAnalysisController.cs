using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/cross-analysis")]
    public class CrossAnalysisController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public CrossAnalysisController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        // Q38: Sản phẩm bán chạy nhưng lượng tồn kho thấp, có nguy cơ thiếu hàng
        [HttpGet("stockout-risk")]
        public async Task<IActionResult> GetStockoutRisk([FromQuery] int top = 10) => Ok(await _repo.Q38_HighSalesLowStockRiskAsync(top));

        // Q39: Sản phẩm nhập nhiều nhưng bán ít, có nguy cơ tồn kho cao
        [HttpGet("overstock-risk")]
        public async Task<IActionResult> GetOverstockRisk([FromQuery] int top = 10) => Ok(await _repo.Q39_HighPurchaseLowSalesRiskAsync(top));

        // Q40: Khu vực có doanh thu cao nhưng chi phí vận chuyển lớn ảnh hưởng hiệu quả kinh doanh
        [HttpGet("shipping-efficiency")]
        public async Task<IActionResult> GetShippingEfficiency() => Ok(await _repo.Q40_RevenueVsShippingEfficiencyAsync());
    }
}
