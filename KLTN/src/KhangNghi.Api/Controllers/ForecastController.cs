using System.Threading.Tasks;
using KhangNghi.Core.DTOs;
using KhangNghi.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/forecast")]
    [Authorize] // Biểu đồ dự báo hiển thị trên trang Tổng quan của mọi vai trò
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
        public async Task<IActionResult> PredictSalesGet(
            [FromQuery] string granularity = "month",
            [FromQuery] int horizon = 6,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] string? category = null)
        {
            var req = new ForecastRequest
            {
                Granularity = granularity,
                HorizonMonths = horizon,
                StartDate = startDate,
                EndDate = endDate,
                Category = category
            };
            var result = await _forecastService.PredictSalesAsync(req);
            return Ok(result);
        }

        [HttpGet("month-detail")]
        public async Task<IActionResult> GetMonthDrilldown(
            [FromQuery] int year = 2026,
            [FromQuery] int month = 1)
        {
            var result = await _forecastService.GetMonthDrilldownAsync(year, month);
            return Ok(result);
        }
    }
}
