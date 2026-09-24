using System.Threading.Tasks;
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

        [HttpPost("trigger")]
        public async Task<IActionResult> TriggerEtl([FromQuery] string? filePath = null, [FromQuery] string triggeredBy = "Web/Api")
        {
            var result = await _etlService.RunEtlAsync(filePath, triggeredBy);
            return Ok(result);
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
