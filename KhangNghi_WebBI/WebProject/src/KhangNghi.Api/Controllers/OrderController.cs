using System;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/orders")]
    [Route("api/orders")]
    [Authorize(Policy = "SalesAccess")]
    public class OrderController : ControllerBase
    {
        private readonly ISsasRepository _ssas;
        private readonly ILogger<OrderController> _logger;

        private bool UseSalesCube => !User.IsInRole("Director");

        public OrderController(
            ISsasRepository ssas,
            ILogger<OrderController> logger)
        {
            _ssas = ssas;
            _logger = logger;
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrderDetail(string orderId)
        {
            try
            {
                var result = await _ssas.GetOrderDetailFromCubeAsync(
                    UseSalesCube, orderId);

                if (result == null)
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy đơn hàng trong SSAS."
                    });
                }

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Không lấy được chi tiết đơn {OrderId} từ SSAS.",
                    orderId);

                return StatusCode(503, new
                {
                    message = "Không truy vấn được chi tiết đơn hàng từ SSAS."
                });
            }
        }
    }
}