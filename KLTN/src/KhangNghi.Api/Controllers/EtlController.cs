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
            try
            {
                var logs = await _etlService.GetLogsAsync(limit);
                return Ok(logs);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
            {
                // 208 = Invalid object name: CSDL chưa chạy script tạo bảng điều khiển ETL
                return StatusCode(503, new
                {
                    code = "ETL_LOG_NOT_INITIALIZED",
                    message = "Chưa khởi tạo bảng nhật ký ETL (dbo.Sys_EtlExecution). Chạy script KhangNghi.Infrastructure/Data/001_Etl_Control.sql trên DWH_KhangNghi."
                });
            }
        }

        [HttpGet("tables-status")]
        public async Task<IActionResult> GetTablesStatus()
        {
            var statuses = await _etlService.GetDwhTableStatusesAsync();
            return Ok(statuses);
        }
    }
}
