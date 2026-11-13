using System;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    [Authorize(Roles = "Director,SalesStaff,InventoryStaff")]
    public class DashboardController : ControllerBase
    {
        private readonly ISsasRepository _ssasRepo;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ISsasRepository ssasRepo, ILogger<DashboardController> logger)
        {
            _ssasRepo = ssasRepo;
            _logger = logger;
        }

        [HttpGet("connection-status")]
        public async Task<IActionResult> GetConnectionStatus()
        {
            string cube = User.IsInRole("Director") ? "KhangNghi_Cube"
                : User.IsInRole("InventoryStaff") ? "Cube_Inventory" : "Cube_Sales";
            string measure = cube == "Cube_Inventory" ? "Fact Inventory Count" : "Sales Amount";
            try
            {
                var rows = await _ssasRepo.ExecuteMdxQueryAsync(
                    "SELECT { [Measures].[" + measure + "] } ON COLUMNS FROM [" + cube + "];");
                if (rows.Count == 0)
                    throw new InvalidOperationException("Không nhận được kết quả kiểm tra cube.");

                return Ok(new { connected = true, source = "SSAS", cube, checkedAt = DateTimeOffset.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard SSAS connection check failed for {Cube}", cube);
                return StatusCode(503, new
                {
                    connected = false,
                    source = "SSAS",
                    cube,
                    message = "Tạm thời không truy vấn được cube phân tích.",
                    checkedAt = DateTimeOffset.UtcNow
                });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? year = null, [FromQuery] string? region = null)
        {
            bool director = User.IsInRole("Director");
            bool warehouse = !director && User.IsInRole("InventoryStaff");
            bool sales = !director && !warehouse && User.IsInRole("SalesStaff");
            if (!director && !warehouse && !sales) return Forbid();

            try
            {
                var result = await _ssasRepo.GetDashboardSummaryFromCubeAsync(
                    year, region,
                    includeSales: director || sales,
                    includeInventory: director || warehouse,
                    useSalesCube: sales);

                if (director) return Ok(result);
                if (warehouse) return Ok(new { result.LowStockAlertCount });
                return Ok(new
                {
                    result.TotalRevenue,
                    result.TotalProfit,
                    result.ProfitMargin,
                    result.TotalOrders,
                    result.TotalQuantitySold,
                    result.TotalShippingCost,
                    result.TotalCustomers
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard MDX summary failed for year {Year}, region {Region}", year, region);
                return StatusCode(503, new { message = "Chưa lấy được KPI từ SSAS. Xem nhật ký API để kiểm tra." });
            }
        }
    }
}
