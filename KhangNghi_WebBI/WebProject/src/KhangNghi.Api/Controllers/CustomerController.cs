using System;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/customers")]
    [Route("api/customers")]
    [Authorize(Policy = "SalesAccess")]
    public class CustomerController : ControllerBase
    {
        private readonly ISsasRepository _ssas;
        private readonly ILogger<CustomerController> _logger;

        private bool UseSalesCube => !User.IsInRole("Director");

        public CustomerController(
            ISsasRepository ssas,
            ILogger<CustomerController> logger)
        {
            _ssas = ssas;
            _logger = logger;
        }

        [HttpGet("{customerId}")]
        public async Task<IActionResult> GetCustomerDetail(
            string customerId)
        {
            try
            {
                var result =
                    await _ssas.GetCustomerDetailFromCubeAsync(
                        UseSalesCube, customerId);

                if (result == null)
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy giao dịch của khách hàng."
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
                    "Không lấy được chi tiết khách hàng {CustomerId} từ SSAS.",
                    customerId);

                return StatusCode(503, new
                {
                    message = "Không truy vấn được chi tiết khách hàng từ SSAS."
                });
            }
        }
    }
}