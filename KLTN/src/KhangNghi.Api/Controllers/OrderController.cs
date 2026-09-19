using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/orders")]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public OrderController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Lookup Order details and Data Lineage using Natural Key (OrderID)
        /// </summary>
        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrderDetail(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest(new { message = "OrderID is required" });
            }

            var result = await _repo.GetOrderDetailByNaturalIdAsync(orderId);
            if (result == null)
            {
                return NotFound(new { message = $"Order with Natural Key '{orderId}' not found", naturalKey = orderId, status = "N/A" });
            }

            return Ok(result);
        }
    }
}
