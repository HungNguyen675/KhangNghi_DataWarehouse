using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public InventoryController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        // Q26: Sản phẩm hiện có lượng tồn kho cao nhất
        [HttpGet("highest-stock")]
        public async Task<IActionResult> GetHighestStock([FromQuery] int top = 10) => Ok(await _repo.Q26_HighestStockProductsAsync(top));

        // Q27: Sản phẩm có lượng tồn kho thấp nhất
        [HttpGet("lowest-stock")]
        public async Task<IActionResult> GetLowestStock([FromQuery] int top = 10) => Ok(await _repo.Q27_LowestStockProductsAsync(top));

        // Q28: Sản phẩm có QuantityOnHand thấp hơn ReorderLevel và cần nhập thêm (Cảnh báo Red/Yellow/Green)
        [HttpGet("alerts")]
        public async Task<IActionResult> GetReorderAlerts() => Ok(await _repo.Q28_ReorderLevelAlertsAsync());

        // Q29: Khu vực đang lưu trữ lượng tồn kho lớn nhất
        [HttpGet("by-region")]
        public async Task<IActionResult> GetStockByRegion() => Ok(await _repo.Q29_StockByRegionAsync());

        // Q30: Lượng nhập - xuất - tồn của sản phẩm theo thời gian
        [HttpGet("movement-over-time")]
        public async Task<IActionResult> GetMovementOverTime() => Ok(await _repo.Q30_InventoryMovementOverTimeAsync());
    }
}
