using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/shipping")]
    public class ShippingController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public ShippingController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        // Q31: Đơn vị vận chuyển xử lý nhiều đơn hàng nhất
        [HttpGet("orders-by-shipper")]
        public async Task<IActionResult> GetOrdersByShipper() => Ok(await _repo.Q31_OrdersByShipperAsync());

        // Q32: Đơn vị vận chuyển có tổng chi phí vận chuyển cao nhất
        [HttpGet("cost-by-shipper")]
        public async Task<IActionResult> GetCostByShipper() => Ok(await _repo.Q32_ShippingCostByShipperAsync());

        // Q33: Đơn vị vận chuyển có chi phí vận chuyển trung bình thấp nhất
        [HttpGet("avg-cost-by-shipper")]
        public async Task<IActionResult> GetAvgCostByShipper() => Ok(await _repo.Q33_AvgShippingCostByShipperAsync());

        // Q34: Đơn vị vận chuyển có thời gian giao hàng trung bình nhanh nhất
        [HttpGet("fastest-delivery")]
        public async Task<IActionResult> GetFastestDelivery() => Ok(await _repo.Q34_FastestDeliveryShippersAsync());

        // Q35: Đơn vị vận chuyển có số ngày giao trễ trung bình cao nhất
        [HttpGet("highest-late-days")]
        public async Task<IActionResult> GetHighestLateDays() => Ok(await _repo.Q35_HighestLateDaysShippersAsync());

        // Q36: Khu vực có chi phí vận chuyển cao nhất
        [HttpGet("cost-by-region")]
        public async Task<IActionResult> GetCostByRegion() => Ok(await _repo.Q36_ShippingCostByRegionAsync());

        // Q37: Khu vực thường xuyên xảy ra giao hàng trễ
        [HttpGet("late-delivery-by-region")]
        public async Task<IActionResult> GetLateDeliveryByRegion() => Ok(await _repo.Q37_LateDeliveryRateByRegionAsync());
    }
}
