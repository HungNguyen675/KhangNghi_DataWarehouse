using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/customers")]
    [Route("api/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _repo;

        public CustomerController(IDwhAnalyticsRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Lookup Customer details, aggregated stats, orders, and Data Lineage using Natural Key (CustomerID)
        /// </summary>
        [HttpGet("{customerId}")]
        public async Task<IActionResult> GetCustomerDetail(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return BadRequest(new { message = "CustomerID is required" });
            }

            var result = await _repo.GetCustomerDetailByNaturalIdAsync(customerId);
            if (result == null)
            {
                return NotFound(new { message = $"Customer with Natural Key '{customerId}' not found", naturalKey = customerId, status = "N/A" });
            }

            return Ok(result);
        }
    }
}
