using System;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/ssas")]
    public class SsasController : ControllerBase
    {
        private readonly ISsasRepository _ssasRepo;

        public SsasController(ISsasRepository ssasRepo)
        {
            _ssasRepo = ssasRepo;
        }

        /// <summary>
        /// Truy vấn doanh thu theo năm trực tiếp từ SSAS OLAP Cube
        /// </summary>
        [HttpGet("revenue-by-year")]
        public async Task<IActionResult> GetRevenueByYear()
        {
            try
            {
                var result = await _ssasRepo.GetRevenueByYearFromCubeAsync();
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi kết nối hoặc truy vấn SSAS: " + ex.Message });
            }
        }

        /// <summary>
        /// Truy vấn Top sản phẩm mang lại lợi nhuận cao nhất từ SSAS Cube
        /// </summary>
        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int top = 10)
        {
            try
            {
                var result = await _ssasRepo.GetTopProductsFromCubeAsync(top);
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi kết nối hoặc truy vấn SSAS: " + ex.Message });
            }
        }

        /// <summary>
        /// Truy vấn doanh thu theo danh mục từ SSAS Cube
        /// </summary>
        [HttpGet("by-category")]
        public async Task<IActionResult> GetRevenueByCategory()
        {
            try
            {
                var result = await _ssasRepo.GetRevenueByCategoryFromCubeAsync();
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi kết nối hoặc truy vấn SSAS: " + ex.Message });
            }
        }

        /// <summary>
        /// Thực thi truy vấn MDX tùy chỉnh trên SSAS Cube
        /// </summary>
        [HttpPost("query")]
        public async Task<IActionResult> ExecuteMdx([FromBody] MdxQueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.MdxQuery))
            {
                return BadRequest(new { success = false, message = "Truy vấn MDX không được để trống" });
            }

            try
            {
                var result = await _ssasRepo.ExecuteMdxQueryAsync(request.MdxQuery);
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi thực thi MDX: " + ex.Message });
            }
        }
    }

    public class MdxQueryRequest
    {
        public string MdxQuery { get; set; } = string.Empty;
    }
}
