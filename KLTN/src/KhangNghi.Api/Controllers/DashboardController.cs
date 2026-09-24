using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDwhAnalyticsRepository _analyticsRepo;

        public DashboardController(IDwhAnalyticsRepository analyticsRepo)
        {
            _analyticsRepo = analyticsRepo;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? year = null, [FromQuery] string? region = null)
        {
            var result = await _analyticsRepo.GetDashboardSummaryAsync(year, region);
            return Ok(result);
        }
    }
}
