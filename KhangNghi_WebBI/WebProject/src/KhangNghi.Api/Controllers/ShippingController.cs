using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/shipping")]
    [Authorize(Policy = "ShippingAccess")]
    public class ShippingController : ControllerBase
    {
private readonly IDwhAnalyticsRepository _repo;
private readonly ISsasRepository _ssas;

private bool UseSalesCube => !User.IsInRole("Director");

public ShippingController(
    IDwhAnalyticsRepository repo,
    ISsasRepository ssas)
{
    _repo = repo;
    _ssas = ssas;
}

// Q31: Số shipment theo đơn vị vận chuyển
[HttpGet("orders-by-shipper")]
public async Task<IActionResult> GetOrdersByShipper()
{
    var rows = await _ssas.GetShippingCostsFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderByDescending(
        r => System.Convert.ToInt64(r["TotalOrders"])));
}

// Q32: Tổng chi phí vận chuyển
[HttpGet("cost-by-shipper")]
public async Task<IActionResult> GetCostByShipper()
{
    var rows = await _ssas.GetShippingCostsFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderByDescending(
        r => System.Convert.ToDouble(r["TotalShippingCost"])));
}

// Q33: Chi phí trung bình trên mỗi shipment
[HttpGet("avg-cost-by-shipper")]
public async Task<IActionResult> GetAvgCostByShipper()
{
    var rows = await _ssas.GetShippingCostsFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderBy(
        r => System.Convert.ToDouble(r["AvgCostPerOrder"])));
}

// Q34: Thời gian giao hàng trung bình theo đơn vị vận chuyển
[HttpGet("fastest-delivery")]
public async Task<IActionResult> GetFastestDelivery()
{
    var rows = await _ssas.GetShippingDeliveryFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderBy(r =>
        r["AvgDeliveryDays"] == null
            ? double.MaxValue
            : System.Convert.ToDouble(r["AvgDeliveryDays"])));
}

// Q35: Số shipment trễ và số ngày trễ trung bình
[HttpGet("highest-late-days")]
public async Task<IActionResult> GetHighestLateDays()
{
    var rows = await _ssas.GetShippingDeliveryFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderByDescending(r =>
        r["AvgLateDays"] == null
            ? double.MinValue
            : System.Convert.ToDouble(r["AvgLateDays"])));
}
[HttpGet("cost-by-region")]
public async Task<IActionResult> GetCostByRegion()
{
    var rows = await _ssas.GetShippingRegionsFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderByDescending(r =>
        System.Convert.ToDouble(r["TotalShippingCost"])));
}

// Q37: Tỷ lệ giao trễ theo vùng giao hàng
[HttpGet("late-delivery-by-region")]
public async Task<IActionResult> GetLateDeliveryByRegion()
{
    var rows = await _ssas.GetShippingRegionsFromCubeAsync(
        UseSalesCube);

    return Ok(rows.OrderByDescending(r =>
        r["LateRatePercent"] == null
            ? double.MinValue
            : System.Convert.ToDouble(r["LateRatePercent"])));
}
    }
}
