using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/sales")]
    [Authorize] // Các API dùng trên trang Tổng quan (Q01, Q06, Q08, Q11, Q15) mở cho mọi vai trò đã đăng nhập
    public class SalesController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public SalesController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        // Q01: Tổng doanh thu bán hàng theo từng năm
        [HttpGet("by-year")]
        public async Task<IActionResult> GetRevenueByYear([FromQuery] int? year = null) => Ok(await _repo.Q01_RevenueByYearAsync(year));

        // Doanh thu, lợi nhuận từng tháng (biểu đồ xu hướng trang Tổng quan, mở cho mọi vai trò)
        [HttpGet("by-month")]
        public async Task<IActionResult> GetRevenueByMonth() => Ok(await _repo.GetRevenueByMonthAsync());

        // Chi tiết một năm: doanh thu, lợi nhuận theo danh mục, khu vực và sản phẩm đứng đầu
        [HttpGet("year-breakdown")]
        public async Task<IActionResult> GetYearBreakdown([FromQuery] int year)
        {
            var (categories, regions, products) = await _repo.GetYearBreakdownAsync(year);
            return Ok(new { Categories = categories, Regions = regions, Products = products });
        }

        // Q02: Doanh thu thay đổi theo quý
        [HttpGet("by-quarter")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetRevenueByQuarter([FromQuery] int? year = null) => Ok(await _repo.Q02_RevenueByQuarterAsync(year));

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

        // Q06: Top sản phẩm có doanh thu cao nhất
        [HttpGet("top-products-revenue")]
        public async Task<IActionResult> GetTopProductsRevenue([FromQuery] int top = 10) => Ok(await _repo.Q06_TopProductsByRevenueAsync(top));

        // Q07: Top sản phẩm có lợi nhuận cao nhất
        [HttpGet("top-products-profit")]
        [Authorize(Policy = "ProductAccess")]
        public async Task<IActionResult> GetTopProductsProfit([FromQuery] int top = 10) => Ok(await _repo.Q07_TopProductsByProfitAsync(top));

        // Q08: Danh mục sản phẩm có doanh thu cao nhất
        [HttpGet("by-category")]
        public async Task<IActionResult> GetRevenueByCategory() => Ok(await _repo.Q08_RevenueByCategoryAsync());

        // Q09: Sub-Category bán được số lượng nhiều nhất
        [HttpGet("by-sub-category")]
        [Authorize(Policy = "ProductAccess")]
        public async Task<IActionResult> GetQuantityBySubCategory() => Ok(await _repo.Q09_QuantityBySubCategoryAsync());

        // Q10: Sản phẩm có chiết khấu cao nhưng lợi nhuận thấp
        [HttpGet("high-discount-low-profit")]
        [Authorize(Policy = "ProductAccess")]
        public async Task<IActionResult> GetHighDiscountLowProfit([FromQuery] int top = 10) => Ok(await _repo.Q10_HighDiscountLowProfitProductsAsync(top));

        // Q11: Khách hàng có doanh thu cao nhất
        [HttpGet("top-customers-revenue")]
        public async Task<IActionResult> GetTopCustomersRevenue([FromQuery] int top = 10) => Ok(await _repo.Q11_TopCustomersByRevenueAsync(top));

        // Q12: Khách hàng tạo ra lợi nhuận cao nhất
        [HttpGet("top-customers-profit")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetTopCustomersProfit([FromQuery] int top = 10) => Ok(await _repo.Q12_TopCustomersByProfitAsync(top));

        // Q13: Phân khúc khách hàng có doanh thu cao nhất
        [HttpGet("by-segment")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetRevenueBySegment() => Ok(await _repo.Q13_RevenueBySegmentAsync());

        // Q14: Phân khúc khách hàng mua số lượng nhiều nhất
        [HttpGet("quantity-by-segment")]
        [Authorize(Policy = "SalesAccess")]
        public async Task<IActionResult> GetQuantityBySegment() => Ok(await _repo.Q14_QuantityBySegmentAsync());

        // Q15: Khu vực có doanh thu bán hàng cao nhất
        [HttpGet("by-region")]
        public async Task<IActionResult> GetRevenueByRegion() => Ok(await _repo.Q15_RevenueByRegionAsync());

        // Q16: Quốc gia có lợi nhuận cao nhất
        [HttpGet("profit-by-country")]
        [Authorize(Policy = "GeographyAccess")]
        public async Task<IActionResult> GetProfitByCountry() => Ok(await _repo.Q16_ProfitByCountryAsync());

        // Q17: Thành phố có số lượng sản phẩm bán ra nhiều nhất
        [HttpGet("top-cities-quantity")]
        [Authorize(Policy = "GeographyAccess")]
        public async Task<IActionResult> GetTopCitiesQuantity([FromQuery] int top = 10) => Ok(await _repo.Q17_TopCitiesByQuantityAsync(top));

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
