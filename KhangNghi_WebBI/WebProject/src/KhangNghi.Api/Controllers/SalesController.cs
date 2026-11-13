using System;
using System.Linq;
using System.Globalization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/sales")]
    [Authorize(Policy = "SalesAccess")]
    public class SalesController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        private readonly ISsasRepository _ssas;
        private readonly ILogger<SalesController> _logger;
        private bool UseSalesCube => !User.IsInRole("Director");

        public SalesController(IDwhAnalyticsRepository repo, ISsasRepository ssas,
            ILogger<SalesController> logger)
        {
            _repo = repo;
            _ssas = ssas;
            _logger = logger;
        }

        private async Task<IActionResult> RunChartAsync(Func<Task<object>> query)
        {
            try { return Ok(await query()); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSAS chart query failed");
                return StatusCode(503, new { message = "Không truy vấn được biểu đồ từ SSAS. Vui lòng kiểm tra nhật ký máy chủ." });
            }
        }

        // Q01: Tổng doanh thu bán hàng theo từng năm
        [HttpGet("by-year")]
        public Task<IActionResult> GetRevenueByYear([FromQuery] int? year = null) =>
            RunChartAsync(async () => await _ssas.GetSalesChartFromCubeAsync("year", UseSalesCube, year));

        // Doanh thu, lợi nhuận từng tháng (biểu đồ xu hướng trang Tổng quan, Director/SalesStaff)
        [HttpGet("by-month")]
        public Task<IActionResult> GetRevenueByMonth([FromQuery] int? year = null) =>
            RunChartAsync(async () => await _ssas.GetSalesChartFromCubeAsync("month", UseSalesCube, year));

        // Chi tiết một năm: doanh thu, lợi nhuận theo danh mục, khu vực và sản phẩm đứng đầu
        [HttpGet("year-breakdown")]
        public Task<IActionResult> GetYearBreakdown([FromQuery] int year) =>
            RunChartAsync(() => _ssas.GetYearBreakdownFromCubeAsync(UseSalesCube, year));

        // Q02: Doanh thu thay đổi theo quý
        [HttpGet("by-quarter")]
        [Authorize(Policy = "SalesAccess")]
        public Task<IActionResult> GetRevenueByQuarter([FromQuery] int? year = null) =>
            RunChartAsync(async () => await _ssas.GetSalesChartFromCubeAsync("quarter", UseSalesCube, year));

        // Q03: Tháng có doanh thu cao nhất
        [HttpGet("peak-month")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetPeakMonth() => Ok(await _repo.Q03_PeakMonthRevenueAsync());

        // Q04: Quý có lợi nhuận cao nhất
        [HttpGet("peak-quarter-profit")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetPeakQuarterProfit() => Ok(await _repo.Q04_PeakQuarterProfitAsync());

        // Q05: Số lượng sản phẩm bán ra theo thời gian
        [HttpGet("quantity-over-time")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetQuantityOverTime() => Ok(await _repo.Q05_QuantitySoldOverTimeAsync());

        // Q06: Top sản phẩm theo doanh thu từ SSAS
        [HttpGet("top-products-revenue")]
        public Task<IActionResult> GetTopProductsRevenue(
            [FromQuery] int top = 10) =>
            RunChartAsync(async () =>
            {
                if (top < 1 || top > 100)
                    throw new ArgumentException(
                        "Số sản phẩm phải nằm trong khoảng 1–100.");

                var products = await _ssas.GetSalesChartFromCubeAsync(
                    "product", UseSalesCube);

                return products
                    .OrderByDescending(p => Convert.ToDouble(
                        p["Revenue"], CultureInfo.InvariantCulture))
                    .Take(top)
                    .ToList();
            });

        // Q07: Top sản phẩm theo lợi nhuận từ SSAS
        [HttpGet("top-products-profit")]
        [Authorize(Policy = "ProductAccess")]
        public Task<IActionResult> GetTopProductsProfit(
            [FromQuery] int top = 10) =>
            RunChartAsync(async () =>
            {
                if (top < 1 || top > 100)
                    throw new ArgumentException(
                        "Số sản phẩm phải nằm trong khoảng 1–100.");

                var products = await _ssas.GetSalesChartFromCubeAsync(
                    "product", UseSalesCube);

                return products
                    .OrderByDescending(p => Convert.ToDouble(
                        p["Profit"], CultureInfo.InvariantCulture))
                    .Take(top)
                    .ToList();
            });

        // Q08: Danh mục sản phẩm có doanh thu cao nhất
        [HttpGet("by-category")]
        public Task<IActionResult> GetRevenueByCategory([FromQuery] int? year = null) =>
            RunChartAsync(async () => await _ssas.GetSalesChartFromCubeAsync("category", UseSalesCube, year));

        // Q09: Sub-Category bán được số lượng nhiều nhất
[HttpGet("by-sub-category")]
[Authorize(Policy = "ProductAccess")]
public Task<IActionResult> GetQuantityBySubCategory() =>
    RunChartAsync(async () =>
    {
        var rows = await _ssas.GetSalesChartFromCubeAsync(
            "sub-category", UseSalesCube);

        return rows
            .OrderByDescending(r => Convert.ToInt64(
                r["TotalQuantity"],
                CultureInfo.InvariantCulture))
            .ToList();
    });

        // Q10: Sản phẩm có chiết khấu cao nhưng lợi nhuận thấp
        [HttpGet("high-discount-low-profit")]
        [Authorize(Policy = "ProductAccess")]
        public async Task<IActionResult> GetHighDiscountLowProfit([FromQuery] int top = 10) => Ok(await _repo.Q10_HighDiscountLowProfitProductsAsync(top));

        // Q11: Khách hàng có doanh thu cao nhất
[HttpGet("top-customers-revenue")]
public async Task<IActionResult> GetTopCustomersRevenue(
    [FromQuery] int top = 10)
{
    if (top < 1 || top > 1000)
        return BadRequest(new { message = "Top phải từ 1 đến 1000." });

    var rows = await _ssas.GetCustomerAnalyticsFromCubeAsync(
        UseSalesCube, byCustomer: true);

    return Ok(rows
        .OrderByDescending(r =>
            System.Convert.ToDouble(r["Revenue"]))
        .Take(top));
}

        // Q12: Khách hàng tạo ra lợi nhuận cao nhất
 [HttpGet("top-customers-profit")]
[Authorize(Policy = "SalesAccess")]
public async Task<IActionResult> GetTopCustomersProfit(
    [FromQuery] int top = 10)
{
    if (top < 1 || top > 1000)
        return BadRequest(new { message = "Top phải từ 1 đến 1000." });

    var rows = await _ssas.GetCustomerAnalyticsFromCubeAsync(
        UseSalesCube, byCustomer: true);

    return Ok(rows
        .OrderByDescending(r =>
            System.Convert.ToDouble(r["Profit"]))
        .Take(top));
}

        // Q13: Phân khúc khách hàng có doanh thu cao nhất
[HttpGet("by-segment")]
[Authorize(Policy = "SalesAccess")]
public async Task<IActionResult> GetRevenueBySegment()
{
    var rows = await _ssas.GetCustomerAnalyticsFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderByDescending(r =>
        System.Convert.ToDouble(r["Revenue"])));
}

        // Q14: Phân khúc khách hàng mua số lượng nhiều nhất
        [HttpGet("quantity-by-segment")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetQuantityBySegment() => Ok(await _repo.Q14_QuantityBySegmentAsync());

        // Q15: Khu vực có doanh thu bán hàng cao nhất
        [HttpGet("by-region")]
        public Task<IActionResult> GetRevenueByRegion([FromQuery] int? year = null) =>
            RunChartAsync(async () => await _ssas.GetSalesChartFromCubeAsync("region", UseSalesCube, year));

        // Q16: Quốc gia có lợi nhuận cao nhất
        [HttpGet("profit-by-country")]
        [Authorize(Policy = "GeographyAccess")]
        public async Task<IActionResult> GetProfitByCountry() => Ok(await _repo.Q16_ProfitByCountryAsync());

// Q17: Thành phố có số lượng sản phẩm bán ra nhiều nhất
[HttpGet("top-cities-quantity")]
[Authorize(Policy = "GeographyAccess")]
public Task<IActionResult> GetTopCitiesQuantity(
    [FromQuery] int top = 10) =>
    RunChartAsync(async () =>
    {
        if (top < 1 || top > 100)
            throw new ArgumentException(
                "Số thành phố phải nằm trong khoảng 1–100.");

        var cities = await _ssas.GetSalesChartFromCubeAsync(
            "city", UseSalesCube);

        return cities
            .OrderByDescending(r => Convert.ToInt64(
                r["TotalQuantity"],
                CultureInfo.InvariantCulture))
            .Take(top)
            .ToList();
    });

        // Q18: Doanh thu của từng khu vực theo năm/quý
        [HttpGet("regional-revenue-trend")]
        [Authorize(Policy = "GeographyAccess")]
        public async Task<IActionResult> GetRegionalRevenueTrend() => Ok(await _repo.Q18_RegionalRevenueTrendAsync());

        // Q19: Nhà sản xuất có sản phẩm tạo ra doanh thu cao nhất
        [HttpGet("top-manufacturers-revenue")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetTopManufacturersRevenue() => Ok(await _repo.Q19_TopManufacturersByRevenueAsync());

        // Q20: Nhà sản xuất có sản phẩm mang lại lợi nhuận cao nhất
        [HttpGet("top-manufacturers-profit")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetTopManufacturersProfit() => Ok(await _repo.Q20_TopManufacturersByProfitAsync());
    }
}
