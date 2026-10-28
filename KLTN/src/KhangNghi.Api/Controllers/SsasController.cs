using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/ssas")]
    [Authorize] // Danh mục 40 câu hỏi BI (MDX) mở cho mọi vai trò đã đăng nhập
    public class SsasController : ControllerBase
    {
        private readonly ISsasRepository _ssasRepo;
        private readonly IDwhAnalyticsRepository _dwhRepo;

        public SsasController(ISsasRepository ssasRepo, IDwhAnalyticsRepository dwhRepo)
        {
            _ssasRepo = ssasRepo;
            _dwhRepo = dwhRepo;
        }

        /// <summary>
        /// Kiểm tra trạng thái kết nối SSAS Server, Catalog và Cube KhangNghi
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var status = await _ssasRepo.GetSsasStatusAsync();
            return Ok(new { success = true, data = status });
        }

        /// <summary>
        /// Danh mục 40 câu truy vấn MDX chuẩn hóa theo đề cương khóa luận Khang Nghi
        /// </summary>
        [HttpGet("catalog-40")]
        public IActionResult GetCatalog40()
        {
            var catalog = _ssasRepo.Get40MdxQueries();
            return Ok(new { success = true, count = catalog.Count, data = catalog });
        }

        /// <summary>
        /// Truy vấn doanh thu theo năm trực tiếp từ SSAS OLAP Cube
        /// </summary>
        [HttpGet("revenue-by-year")]
        public async Task<IActionResult> GetRevenueByYear()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _ssasRepo.GetRevenueByYearFromCubeAsync();
                sw.Stop();
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result, executionTimeMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return StatusCode(500, new { success = false, message = "Lỗi kết nối hoặc truy vấn SSAS: " + ex.Message, executionTimeMs = sw.ElapsedMilliseconds });
            }
        }

        /// <summary>
        /// Truy vấn Top sản phẩm mang lại lợi nhuận cao nhất từ SSAS Cube
        /// </summary>
        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int top = 10)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _ssasRepo.GetTopProductsFromCubeAsync(top);
                sw.Stop();
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result, executionTimeMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return StatusCode(500, new { success = false, message = "Lỗi kết nối hoặc truy vấn SSAS: " + ex.Message, executionTimeMs = sw.ElapsedMilliseconds });
            }
        }

        /// <summary>
        /// Truy vấn doanh thu theo danh mục từ SSAS Cube
        /// </summary>
        [HttpGet("by-category")]
        public async Task<IActionResult> GetRevenueByCategory()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _ssasRepo.GetRevenueByCategoryFromCubeAsync();
                sw.Stop();
                return Ok(new { success = true, source = "SSAS OLAP Cube", data = result, executionTimeMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return StatusCode(500, new { success = false, message = "Lỗi kết nối hoặc truy vấn SSAS: " + ex.Message, executionTimeMs = sw.ElapsedMilliseconds });
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
                return BadRequest(new { success = false, message = "Truy vấn MDX không được để trống." });
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _ssasRepo.ExecuteMdxQueryAsync(request.MdxQuery);
                sw.Stop();
                var columns = result.Count > 0 ? result[0].Keys.ToList() : new List<string>();
                return Ok(new
                {
                    success = true,
                    source = "SSAS OLAP Cube (MSOLAP)",
                    data = result,
                    columns = columns,
                    rowCount = result.Count,
                    executionTimeMs = sw.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi thực thi MDX: " + ex.Message,
                    executionTimeMs = sw.ElapsedMilliseconds
                });
            }
        }

        /// <summary>
        /// Dữ liệu Quick Chart trực tiếp cho các câu hỏi phân tích MDX
        /// </summary>
        [HttpGet("quick-chart/{id}")]
        public async Task<IActionResult> GetQuickChart([FromRoute] string id)
        {
            var result = await _dwhRepo.GetQuickChartAsync(id);
            if (result == null)
            {
                return NotFound(new { success = false, message = $"Chưa hỗ trợ Quick Chart cho câu hỏi '{id}'." });
            }
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// Phân tích kinh doanh đa chiều (Multi-dimensional business analytics)
        /// </summary>
        [HttpGet("multidim-analysis")]
        public async Task<IActionResult> GetMultiDimAnalysis([FromQuery] string measure = "revenue", [FromQuery] string row = "category", [FromQuery] string col = "year")
        {
            var result = await _dwhRepo.GetMultiDimAnalysisAsync(measure, row, col);
            if (result == null)
            {
                return BadRequest(new { success = false, message = "Không thể phân tích đa chiều với các tham số đã chọn." });
            }
            return Ok(new { success = true, data = result });
        }
    }

    public class MdxQueryRequest
    {
        public string MdxQuery { get; set; } = string.Empty;



    }
}
