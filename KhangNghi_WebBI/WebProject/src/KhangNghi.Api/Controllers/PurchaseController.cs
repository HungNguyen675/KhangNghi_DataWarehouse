using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/purchase")]
    [Authorize(Policy = "PurchaseAccess")]
    public class PurchaseController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;
        private readonly ISsasRepository _ssas;
        private readonly ILogger<PurchaseController> _logger;

private bool UseInventoryCube => !User.IsInRole("Director");

public PurchaseController(
    IDwhAnalyticsRepository repo,
    ISsasRepository ssas,
    ILogger<PurchaseController> logger)
{
    _repo = repo;
    _ssas = ssas;
    _logger = logger;
}

        // Q21: Nhà cung cấp có tổng giá trị mua hàng lớn nhất
[HttpGet("top-suppliers-amount")]
public async Task<IActionResult> GetTopSuppliersAmount([FromQuery] int? year = null)
{
    try
    {
        var suppliers =
            await _ssas.GetPurchaseSuppliersFromCubeAsync(
                UseInventoryCube, year);

        return Ok(suppliers);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không lấy được xếp hạng nhà cung cấp từ SSAS.");

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được xếp hạng nhà cung cấp từ SSAS."
        });
    }
}

        // Q22: Nhà cung cấp cung cấp số lượng hàng nhiều nhất
        [HttpGet("top-suppliers-quantity")]
        public async Task<IActionResult> GetTopSuppliersQuantity() => Ok(await _repo.Q22_TopSuppliersByQuantityAsync());

        // Q23: Sản phẩm có tổng giá trị mua vào cao nhất
 [HttpGet("top-purchased-products")]
public async Task<IActionResult> GetTopPurchasedProducts(
    [FromQuery] int top = 10,
    [FromQuery] int? year = null)
    
{
    try
    {
        var products =
            await _ssas.GetTopPurchasedProductsFromCubeAsync(
                UseInventoryCube, top, year);

        return Ok(products);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không lấy được Top sản phẩm mua từ SSAS.");

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được Top sản phẩm mua từ SSAS."
        });
    }
}

        // Q24: Giá nhập của từng sản phẩm thay đổi theo thời gian
        [HttpGet("price-trend")]
        public async Task<IActionResult> GetPriceTrend([FromQuery] string? productName = null) => Ok(await _repo.Q24_PurchasePriceTrendAsync(productName));

        // Q25: Khu vực có tổng giá trị nhập hàng cao nhất (theo Kho / Vùng)
        [HttpGet("by-region")]
        public async Task<IActionResult> GetImportValueByRegion() => Ok(await _repo.Q25_ImportValueByRegionAsync());

        // Tổng hợp đơn mua hàng PO (Fact_Purchase + Dim_Supplier + Dim_Warehouse)
[HttpGet("orders-summary")]
public async Task<IActionResult> GetPurchaseOrdersSummary(
    [FromQuery] int? year = null)
{
    try
    {
        var orders = await _ssas.GetPurchaseOrdersFromCubeAsync(
            UseInventoryCube, year);

        return Ok(orders);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không lấy được danh sách đơn mua từ SSAS.");

        return StatusCode(503, new
        {
            message = "Không truy vấn được danh sách đơn mua từ SSAS."
        });
    }
}

        // Chi tiết mặt hàng trong đơn mua hàng PO
 [HttpGet("order-detail/{orderId}")]
public async Task<IActionResult> GetPurchaseOrderDetail(
    string orderId,
    [FromQuery] int top = 50)
{
    try
    {
        var details =
            await _ssas.GetPurchaseOrderDetailFromCubeAsync(
                UseInventoryCube, orderId, top);

        return Ok(details);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Không lấy được chi tiết đơn mua {OrderId} từ SSAS.",
            orderId);

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được chi tiết đơn mua từ SSAS."
        });
    }
}

        // Chi phí mua hàng theo danh mục sản phẩm
[HttpGet("by-category")]
public async Task<IActionResult> GetPurchaseCategorySpend(
    [FromQuery] int? year = null)
{
    try
    {
        var categories =
            await _ssas.GetPurchaseCategoriesFromCubeAsync(
                UseInventoryCube, year);

        return Ok(categories);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex, "Không lấy được chi phí theo danh mục từ SSAS.");

        return StatusCode(503, new
        {
            message =
                "Không truy vấn được chi phí theo danh mục từ SSAS."
        });
    }
}
    }
}
