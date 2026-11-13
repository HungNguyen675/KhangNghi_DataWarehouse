using System;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "Director")]
    [Route("api/v1/ssas")]
    public class SsasAnalysisController : ControllerBase
    {
        private readonly ISsasRepository _ssas;
        public SsasAnalysisController(ISsasRepository ssas) { _ssas=ssas; }

        [HttpGet("multidim-analysis")]
        public async Task<IActionResult> GetMultiDim([FromQuery] string measure="revenue",[FromQuery] string row="category",[FromQuery] string col="year")
        {
            try { Response.Headers["X-Data-Source"]="SSAS"; return Ok(new {success=true,data=await _ssas.GetMultiDimFromCubeAsync(measure,row,col)}); }
            catch(ArgumentException ex) { return BadRequest(new {success=false,message=ex.Message}); }
        }
        [HttpGet("olap-explore")]
        public async Task<IActionResult> GetOlap([FromQuery] string hierarchy="time",[FromQuery] string[]? path=null,[FromQuery] string measure="revenue",[FromQuery] int[]? years=null,[FromQuery] string[]? regions=null,[FromQuery] string[]? channels=null,[FromQuery] string[]? categories=null)
        {
            try { Response.Headers["X-Data-Source"]="SSAS"; return Ok(new {success=true,data=await _ssas.GetOlapFromCubeAsync(hierarchy,path??Array.Empty<string>(),measure,years??Array.Empty<int>(),regions??Array.Empty<string>(),channels??Array.Empty<string>(),categories??Array.Empty<string>())}); }
            catch(ArgumentException ex) { return BadRequest(new {success=false,message=ex.Message}); }
        }
        [HttpGet("olap-options")]
        public async Task<IActionResult> GetOptions()
        {
            Response.Headers["X-Data-Source"]="SSAS";
            return Ok(new {success=true,data=await _ssas.GetOlapOptionsFromCubeAsync()});
        }
    }
}
