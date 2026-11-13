using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/inventory")]
    [Authorize(Policy = "InventoryAccess")]
    public class InventoryController : ControllerBase
    {
private readonly IDwhAnalyticsRepository _repo;
private readonly ISsasRepository _ssas;
private readonly ILogger<InventoryController> _logger;

private bool UseInventoryCube => !User.IsInRole("Director");

public InventoryController(
    IDwhAnalyticsRepository repo,
    ISsasRepository ssas,
    ILogger<InventoryController> logger)
{
    _repo = repo;
    _ssas = ssas;
    _logger = logger;
}

        // Q26: Sản phẩm hiện có lượng tồn kho cao nhất
        [HttpGet("highest-stock")]
        public async Task<IActionResult> GetHighestStock([FromQuery] int top = 10) => Ok(await _repo.Q26_HighestStockProductsAsync(top));

        // Q27: Sản phẩm có lượng tồn kho thấp nhất
        [HttpGet("lowest-stock")]
        public async Task<IActionResult> GetLowestStock([FromQuery] int top = 10) => Ok(await _repo.Q27_LowestStockProductsAsync(top));

        // Q28: Sản phẩm có QuantityOnHand thấp hơn ReorderLevel và cần nhập thêm (Cảnh báo Red/Yellow/Green)
        [HttpGet("alerts")]
        public async Task<IActionResult> GetReorderAlerts([FromQuery] int top = 50) => Ok(await _repo.Q28_ReorderLevelAlertsAsync(top));

        // Số ngày tồn kho từng mặt hàng (tồn mới nhất ÷ bán TB ngày 3 tháng gần nhất), ít nhất lên đầu.
        // Thay cho cảnh báo theo ReorderLevel: dữ liệu tồn luôn bằng đúng mức đặt lại nên cảnh báo đó luôn rỗng.
[HttpGet("stock-cover")]
public async Task<IActionResult> GetStockCover(
    [FromQuery] int top = 500,
     [FromQuery] int? year = null)
{
    try
    {
        var cover = await _ssas.GetStockCoverFromCubeAsync(top, year);
        return Ok(cover);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không tính được số ngày tồn kho từ SSAS.");

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được cảnh báo tồn kho từ SSAS."
        });
    }
}

        // Q29: Khu vực đang lưu trữ lượng tồn kho lớn nhất
[HttpGet("by-region")]
public async Task<IActionResult> GetStockByRegion([FromQuery] int? year = null)
{
    try
    {
        var regions =
            await _ssas.GetInventoryByRegionFromCubeAsync(
                UseInventoryCube, year);

        return Ok(regions);
    }
    catch (ArgumentException ex)
{
    return BadRequest(new { message = ex.Message });
}
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không lấy được tồn kho theo khu vực từ SSAS.");

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được tồn kho theo khu vực từ SSAS."
        });
    }
}

        // Q30: Lượng nhập - xuất - tồn của sản phẩm theo thời gian
[HttpGet("movement-over-time")]
public async Task<IActionResult> GetMovementOverTime()
{
    try
    {
        var movement =
            await _ssas.GetInventoryMovementFromCubeAsync(
                UseInventoryCube);

        return Ok(movement);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không lấy được biến động tồn kho từ SSAS.");

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được biến động tồn kho từ SSAS."
        });
    }
}
    }
}
