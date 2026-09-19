using System.Threading.Tasks;
using KhangNghi.Core.DTOs;
using KhangNghi.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/forecast")]
    public class ForecastController : ControllerBase
    {
        private readonly ISalesForecastService _forecastService;

        public ForecastController(ISalesForecastService forecastService)
        {
            _forecastService = forecastService;
        }

        [HttpPost("sales")]
        public async Task<IActionResult> PredictSales([FromBody] ForecastRequest request)
        {
            var result = await _forecastService.PredictSalesAsync(request);
            return Ok(result);
        }

        [HttpGet("sales")]
        public async Task<IActionResult> PredictSalesGet([FromQuery] int horizon = 6, [FromQuery] string? category = null)
        {
            var req = new ForecastRequest { HorizonMonths = horizon, Category = category };
            var result = await _forecastService.PredictSalesAsync(req);
            return Ok(result);
        }
    }
}
