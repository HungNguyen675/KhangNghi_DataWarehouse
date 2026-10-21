using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/purchase")]
    public class PurchaseController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public PurchaseController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        // Q21: Nhà cung cấp có tổng giá trị mua hàng lớn nhất
        [HttpGet("top-suppliers-amount")]
        public async Task<IActionResult> GetTopSuppliersAmount() => Ok(await _repo.Q21_TopSuppliersByAmountAsync());

        // Q22: Nhà cung cấp cung cấp số lượng hàng nhiều nhất
        [HttpGet("top-suppliers-quantity")]
        public async Task<IActionResult> GetTopSuppliersQuantity() => Ok(await _repo.Q22_TopSuppliersByQuantityAsync());

        // Q23: Sản phẩm có tổng giá trị mua vào cao nhất
        [HttpGet("top-purchased-products")]
        public async Task<IActionResult> GetTopPurchasedProducts([FromQuery] int top = 10) => Ok(await _repo.Q23_TopPurchasedProductsAsync(top));

        // Q24: Giá nhập của từng sản phẩm thay đổi theo thời gian
        [HttpGet("price-trend")]
        public async Task<IActionResult> GetPriceTrend([FromQuery] string? productName = null) => Ok(await _repo.Q24_PurchasePriceTrendAsync(productName));

        // Q25: Khu vực có tổng giá trị nhập hàng cao nhất (theo Kho / Vùng)
        [HttpGet("by-region")]
        public async Task<IActionResult> GetImportValueByRegion() => Ok(await _repo.Q25_ImportValueByRegionAsync());

        // Tổng hợp đơn mua hàng PO (Fact_Purchase + Dim_Supplier + Dim_Warehouse)
        [HttpGet("orders-summary")]
        public async Task<IActionResult> GetPurchaseOrdersSummary([FromQuery] int? year = null) 
            => Ok(await _repo.GetPurchaseOrdersSummaryAsync(year));

        // Chi tiết mặt hàng trong đơn mua hàng PO
        [HttpGet("order-detail/{orderId}")]
        public async Task<IActionResult> GetPurchaseOrderDetail(string orderId, [FromQuery] int top = 50) 
            => Ok(await _repo.GetPurchaseOrderDetailAsync(orderId, top));

        // Chi phí mua hàng theo danh mục sản phẩm
        [HttpGet("by-category")]
        public async Task<IActionResult> GetPurchaseCategorySpend([FromQuery] int? year = null) 
            => Ok(await _repo.GetPurchaseCategorySpendAsync(year));
    }
}
