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
private readonly ISsasRepository _ssas;

public CrossAnalysisController(
    IDwhAnalyticsRepository repo,
    ISsasRepository ssas)
{
    _repo = repo;
    _ssas = ssas;
}
[HttpGet("stockout-risk")]
public async Task<IActionResult> GetStockoutRisk(
    [FromQuery] int top = 10)
{
    if (top < 1 || top > 5000)
    {
        return BadRequest(new
        {
            message = "Số sản phẩm phải nằm trong khoảng 1–5000."
        });
    }

    var result = await _ssas.GetStockCoverFromCubeAsync(top);
    return Ok(result);
}

        // Q39: Mặt hàng nào tồn quá nhiều so với sức bán? (số ngày tồn kho cao nhất)
        [HttpGet("overstock-risk")]
        public async Task<IActionResult> GetOverstockRisk([FromQuery] int top = 10) => Ok(await _repo.Q39_HighPurchaseLowSalesRiskAsync(top));

        // Diễn giải số ngày tồn kho của một mặt hàng: tồn mới nhất từng kho + bán 3 tháng gần nhất
[HttpGet("stock-cover/{productKey:int}")]
public async Task<IActionResult> GetStockCoverDetail(int productKey)
{
    if (productKey <= 0)
    {
        return BadRequest(new
        {
            message = "Khóa sản phẩm không hợp lệ."
        });
    }

    var result = await _ssas.GetStockCoverDetailFromCubeAsync(productKey);
    return Ok(result);
}

        // Q40: Khu vực nào tốn phí giao hàng nhiều nhất trên mỗi đồng doanh thu?
[HttpGet("shipping-efficiency")]
public async Task<IActionResult> GetShippingEfficiency()
    => Ok(await _ssas.GetShippingEfficiencyFromCubeAsync());
    }
}
