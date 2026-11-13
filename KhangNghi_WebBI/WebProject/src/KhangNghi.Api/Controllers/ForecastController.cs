using System.Threading.Tasks;
using KhangNghi.Core.DTOs;
using KhangNghi.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading;
using System;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/forecast")]
    [Authorize(Policy = "SalesAccess")]
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
        [HttpGet("prophet")]
public async Task<IActionResult> PredictProphet(
    [FromServices] IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken,
    [FromQuery] int months = 6)
{
    if (months is not (3 or 6))
        return BadRequest(new { message = "Chỉ hỗ trợ dự báo 3 hoặc 6 tháng." });

    try
    {
        using var client = httpClientFactory.CreateClient("ProphetForecast");
        using var response = await client.GetAsync(
            $"forecast?months={months}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return StatusCode(502, new { message = "FastAPI trả về lỗi dự báo." });

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return Content(json, "application/json");
    }
    catch (HttpRequestException)
    {
        return StatusCode(503, new { message = "Không kết nối được dịch vụ dự báo." });
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return StatusCode(504, new { message = "Dịch vụ dự báo phản hồi quá chậm." });
    }
}
[HttpGet("prophet-detail")]
public async Task<IActionResult> PredictProphetDetail(
    [FromServices] IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken,
    [FromQuery] string granularity = "day",
    [FromQuery] string? startDate = null,
    [FromQuery] string? endDate = null)
{
    if (granularity is not ("day" or "week" or "month"))
        return BadRequest(new { message = "Chọn day, week hoặc month." });

    var url = $"forecast/detail?granularity={granularity}";

    if (!string.IsNullOrWhiteSpace(startDate))
        url += $"&start_date={Uri.EscapeDataString(startDate)}";

    if (!string.IsNullOrWhiteSpace(endDate))
        url += $"&end_date={Uri.EscapeDataString(endDate)}";

    try
    {
        using var client = httpClientFactory.CreateClient("ProphetForecast");
        using var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(
                response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity
                    ? 422
                    : 502,
                new { message = "Kiểm tra khoảng ngày trong 01/01–30/06/2026." });
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return Content(json, "application/json");
    }
    catch (HttpRequestException)
    {
        return StatusCode(503, new { message = "Không kết nối được dịch vụ dự báo." });
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return StatusCode(504, new { message = "Dịch vụ dự báo phản hồi quá chậm." });
    }
}
[HttpGet("model-comparison")]
public async Task<IActionResult> GetModelComparison(
    [FromServices] IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken)
{
    try
    {
        using var client = httpClientFactory.CreateClient("ProphetForecast");
        using var response = await client.GetAsync(
            "models/comparison", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return StatusCode(
                502,
                new { message = "Chưa lấy được kết quả so sánh mô hình." });

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return Content(json, "application/json");
    }
    catch (HttpRequestException)
    {
        return StatusCode(
            503,
            new { message = "Không kết nối được dịch vụ dự báo." });
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return StatusCode(
            504,
            new { message = "Dịch vụ dự báo phản hồi quá chậm." });
    }
}
    }
}
