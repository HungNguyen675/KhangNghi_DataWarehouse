using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using KhangNghi.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/etl")]
    public class EtlController : ControllerBase
    {
        private readonly IEtlService _etlService;

        public EtlController(IEtlService etlService)
        {
            _etlService = etlService;
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("trigger")]
        public async Task<IActionResult> TriggerEtl([FromQuery] string? filePath = null, [FromQuery] string triggeredBy = "Web/Api")
        {
            if (!string.IsNullOrWhiteSpace(filePath))
                return BadRequest("Configure sources in SSIS; filePath overrides are not supported.");
            try
            {
                var result = await _etlService.RunEtlAsync(null, User.Identity?.Name ?? "Web/Api");
                return Ok(result);
            }
            catch (EtlAlreadyRunningException ex) { return Conflict(ex.Message); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (InvalidOperationException ex) { return Problem(ex.Message, statusCode: 503); }
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs([FromQuery] int limit = 20)
        {
            var logs = await _etlService.GetLogsAsync(limit);
            return Ok(logs);
        }

        [HttpGet("tables-status")]
        public async Task<IActionResult> GetTablesStatus()
        {
            var statuses = await _etlService.GetDwhTableStatusesAsync();
            return Ok(statuses);
        }
    }
}
