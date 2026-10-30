using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Infrastructure.Data;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhangNghi.Api.Controllers
{
    /// <summary>
    /// Câu trả lời trọng tâm cho từng câu trong 50 câu hỏi phân tích (MDX-01 .. MDX-50), truy vấn trực tiếp DWH.
    /// Mỗi truy vấn trả về các cột thống nhất: Label, Value, Value2, Value3 (tùy câu), Grp (nhóm/cột ma trận), Note (ngữ cảnh, vd. năm).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/question-answer")]
    public class QuestionAnswerController : ControllerBase
    {
        private readonly IDatabaseConnectionFactory _db;
        private readonly IDwhAnalyticsRepository _repo;
        private readonly IAuthorizationService _auth;

        public QuestionAnswerController(IDatabaseConnectionFactory db, IDwhAnalyticsRepository repo, IAuthorizationService auth)
        {
            _db = db;
            _repo = repo;
            _auth = auth;
        }

        private const string LastSalesYear = "(SELECT MAX(t.Year) FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey)";

        // Tồn kho là snapshot định kỳ: chỉ lấy bản ghi mới nhất của mỗi sản phẩm tại mỗi kho, không cộng dồn
        private const string LatestStock = @"WITH Lt AS (
            SELECT i.*, ROW_NUMBER() OVER (PARTITION BY i.ProductKey, i.WarehouseKey, i.LocationKey ORDER BY i.TimeKey DESC) AS rn
            FROM Fact_Inventory i) ";

        private static readonly Dictionary<int, string> Sql = new()
        {
            [1] = @"SELECT CAST(t.Year AS nvarchar(10)) AS Label, SUM(f.SalesAmount) AS Value, SUM(f.ProfitAmount) AS Value2, SUM(f.Quantity) AS Value3
                    FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey GROUP BY t.Year ORDER BY t.Year",
            [2] = $@"SELECT N'Quý ' + CAST(t.Quarter AS nvarchar(2)) AS Label, SUM(f.SalesAmount) AS Value, t.Year AS Note
                    FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey WHERE t.Year = {LastSalesYear}
                    GROUP BY t.Year, t.Quarter ORDER BY t.Quarter",
            [3] = $@"SELECT N'T' + CAST(t.Month AS nvarchar(2)) AS Label, SUM(f.SalesAmount) AS Value, t.Year AS Note
                    FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey WHERE t.Year = {LastSalesYear}
                    GROUP BY t.Year, t.Month ORDER BY t.Month",
            [4] = @"SELECT p.Category AS Label, SUM(f.SalesAmount) AS Value, SUM(f.ProfitAmount) AS Value2
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.Category ORDER BY Value DESC",
            [5] = @"SELECT p.SubCategory AS Label, p.Category AS Grp, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey
                    GROUP BY p.Category, p.SubCategory ORDER BY p.Category, Value DESC",
            [6] = @"SELECT TOP 10 p.ProductName AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.ProductName ORDER BY Value DESC",
            [7] = @"SELECT TOP 10 p.ProductName AS Label, SUM(f.ProfitAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.ProductName ORDER BY Value DESC",
            [8] = @"SELECT TOP 10 p.ProductName AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.ProductName ORDER BY Value ASC",
            [9] = @"SELECT c.ChannelName AS Label, SUM(f.SalesAmount) AS Value, SUM(f.DiscountAmount) AS Value2
                    FROM Fact_Sales f JOIN Dim_SalesChannel c ON c.ChannelKey = f.ChannelKey GROUP BY c.ChannelName ORDER BY Value DESC",
            [10] = @"SELECT m.MethodType AS Label, SUM(f.SalesAmount) AS Value, COUNT(DISTINCT f.OrderID) AS Value2
                    FROM Fact_Sales f JOIN Dim_PaymentMethod m ON m.PaymentKey = f.PaymentKey GROUP BY m.MethodType ORDER BY Value DESC",
            [11] = @"SELECT c.Segment AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Customer c ON c.CustomerKey = f.CustomerKey GROUP BY c.Segment ORDER BY Value DESC",
            [12] = @"SELECT TOP 10 c.CustomerName AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Customer c ON c.CustomerKey = f.CustomerKey GROUP BY c.CustomerName ORDER BY Value DESC",
            [13] = @"SELECT l.Region AS Label, SUM(f.SalesAmount) AS Value, SUM(f.ProfitAmount) AS Value2
                    FROM Fact_Sales f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.Region ORDER BY Value DESC",
            [14] = @"SELECT Label, Grp, Value FROM (
                        SELECT l.City AS Label, l.Region AS Grp, SUM(f.SalesAmount) AS Value,
                               ROW_NUMBER() OVER (PARTITION BY l.Region ORDER BY SUM(f.SalesAmount) DESC) AS rn
                        FROM Fact_Sales f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.Region, l.City) x
                    WHERE rn <= 5 ORDER BY Grp, Value DESC",
            [15] = $@"SELECT p.Category AS Label, SUM(f.SalesAmount) AS Value, t.Year AS Note
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey JOIN Dim_Time t ON t.TimeKey = f.TimeKey
                    WHERE t.Year = {LastSalesYear} GROUP BY t.Year, p.Category ORDER BY Value DESC",
            [16] = $@"SELECT p.Category AS Label, SUM(f.SalesAmount) AS Value, t.Year AS Note
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey JOIN Dim_Location l ON l.LocationKey = f.LocationKey
                    JOIN Dim_Time t ON t.TimeKey = f.TimeKey
                    WHERE l.Region = N'Miền Nam' AND t.Year = {LastSalesYear} GROUP BY t.Year, p.Category ORDER BY Value DESC",
            [17] = @"SELECT p.Category AS Label, l.Region AS Grp, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey JOIN Dim_Location l ON l.LocationKey = f.LocationKey
                    GROUP BY p.Category, l.Region ORDER BY p.Category, l.Region",
            [18] = @"SELECT TOP 10 l.City AS Label, SUM(f.ProfitAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.City ORDER BY Value DESC",
            [19] = @"SELECT TOP 10 e.EmployeeName AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Employee e ON e.EmployeeKey = f.EmployeeKey GROUP BY e.EmployeeName ORDER BY Value DESC",
            [20] = @"SELECT pr.PromotionName AS Label, SUM(f.SalesAmount) AS Value, SUM(f.DiscountAmount) AS Value2, SUM(f.ProfitAmount) AS Value3
                    FROM Fact_Sales f JOIN Dim_Promotion pr ON pr.PromotionKey = f.PromotionKey GROUP BY pr.PromotionName ORDER BY Value3 DESC",
            [21] = @"SELECT m.ManufacturerName AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Manufacturer m ON m.ManufacturerKey = f.ManufacturerKey GROUP BY m.ManufacturerName ORDER BY Value DESC",
            [22] = @"SELECT p.Category AS Label, SUM(f.ProfitAmount) * 100.0 / NULLIF(SUM(f.SalesAmount), 0) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.Category ORDER BY Value DESC",
            [23] = @"SELECT s.ServiceName AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Service s ON s.ServiceKey = f.ServiceKey GROUP BY s.ServiceName ORDER BY Value DESC",
            [24] = $@"SELECT c.ChannelName AS Label, N'Quý ' + CAST(t.Quarter AS nvarchar(2)) AS Grp, SUM(f.SalesAmount) AS Value, t.Year AS Note
                    FROM Fact_Sales f JOIN Dim_SalesChannel c ON c.ChannelKey = f.ChannelKey JOIN Dim_Time t ON t.TimeKey = f.TimeKey
                    WHERE t.Year = {LastSalesYear} GROUP BY t.Year, c.ChannelName, t.Quarter ORDER BY c.ChannelName, t.Quarter",
            [25] = @"SELECT CAST(t.Year AS nvarchar(10)) AS Label, SUM(f.TotalCost) AS Value, SUM(f.PurchaseQuantity) AS Value2
                    FROM Fact_Purchase f JOIN Dim_Time t ON t.TimeKey = f.TimeKey GROUP BY t.Year ORDER BY t.Year",
            [26] = @"SELECT TOP 10 s.SupplierName AS Label, SUM(f.TotalCost) AS Value
                    FROM Fact_Purchase f JOIN Dim_Supplier s ON s.SupplierKey = f.SupplierKey GROUP BY s.SupplierName ORDER BY Value DESC",
            [27] = @"SELECT p.Category AS Label, SUM(f.TotalCost) AS Value
                    FROM Fact_Purchase f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.Category ORDER BY Value DESC",
            [28] = @"SELECT s.SupplierName AS Label, SUM(f.TotalCost) / NULLIF(SUM(f.PurchaseQuantity), 0) AS Value
                    FROM Fact_Purchase f JOIN Dim_Supplier s ON s.SupplierKey = f.SupplierKey GROUP BY s.SupplierName ORDER BY Value ASC",
            [29] = @"SELECT w.WarehouseName AS Label, COUNT(DISTINCT f.PurchaseOrderID) AS Value, SUM(f.PurchaseQuantity) AS Value2
                    FROM Fact_Purchase f JOIN Dim_Warehouse w ON w.WarehouseKey = f.WarehouseKey GROUP BY w.WarehouseName ORDER BY Value DESC",
            [30] = LatestStock + @"SELECT w.WarehouseName AS Label,
                        SUM(CASE WHEN Lt.rn = 1 THEN Lt.QuantityOnHand ELSE 0 END) AS Value,
                        SUM(Lt.QuantityIn) AS Value2, SUM(Lt.QuantityOut) AS Value3
                    FROM Lt JOIN Dim_Warehouse w ON w.WarehouseKey = Lt.WarehouseKey GROUP BY w.WarehouseName ORDER BY Value DESC",
            [31] = LatestStock + @"SELECT p.Category AS Label, SUM(Lt.QuantityOnHand) AS Value
                    FROM Lt JOIN Dim_Product p ON p.ProductKey = Lt.ProductKey WHERE Lt.rn = 1 GROUP BY p.Category ORDER BY Value DESC",
            [34] = LatestStock + @"SELECT TOP 10 p.ProductName AS Label, SUM(Lt.QuantityOnHand) AS Value
                    FROM Lt JOIN Dim_Product p ON p.ProductKey = Lt.ProductKey WHERE Lt.rn = 1 GROUP BY p.ProductName ORDER BY Value DESC",
            [35] = @"SELECT Label, Value FROM (
                        SELECT TOP 12 RIGHT('0' + CAST(t.Month AS varchar(2)), 2) + '/' + CAST(t.Year AS varchar(4)) AS Label,
                               SUM(i.QuantityOut) AS Value, t.Year * 12 + t.Month AS YM
                        FROM Fact_Inventory i JOIN Dim_Time t ON t.TimeKey = i.TimeKey
                        GROUP BY t.Year, t.Month ORDER BY YM DESC) x ORDER BY YM",
            [36] = @"SELECT s.ShipperName AS Label, SUM(f.ShippingCost) AS Value, COUNT(*) AS Value2
                    FROM Fact_Shipping f JOIN Dim_Shipper s ON s.ShipperKey = f.ShipperKey GROUP BY s.ShipperName ORDER BY Value DESC",
            [37] = @"SELECT s.ShippingMethod AS Label, AVG(CAST(f.DeliveryDays AS float)) AS Value,
                        AVG(CASE WHEN f.LateDays > 0 THEN 0.0 ELSE 100.0 END) AS Value2, COUNT(*) AS Value3
                    FROM Fact_Shipping f JOIN Dim_Shipper s ON s.ShipperKey = f.ShipperKey GROUP BY s.ShippingMethod ORDER BY Value ASC",
            [38] = @"SELECT s.ShipperName AS Label, SUM(f.LateDays) AS Value, SUM(CASE WHEN f.LateDays > 0 THEN 1 ELSE 0 END) AS Value2
                    FROM Fact_Shipping f JOIN Dim_Shipper s ON s.ShipperKey = f.ShipperKey GROUP BY s.ShipperName ORDER BY Value DESC",
            [39] = @"SELECT l.Region AS Label, SUM(f.ShippingCost) AS Value
                    FROM Fact_Shipping f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.Region ORDER BY Value DESC",
            [40] = @"WITH R AS (SELECT l.Region, SUM(f.SalesAmount) AS Rev FROM Fact_Sales f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.Region),
                         S AS (SELECT l.Region, SUM(f.ShippingCost) AS Ship FROM Fact_Shipping f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.Region)
                    SELECT R.Region AS Label, ISNULL(S.Ship, 0) * 100.0 / NULLIF(R.Rev, 0) AS Value, ISNULL(S.Ship, 0) AS Value2, R.Rev AS Value3,
                        (SELECT SUM(Ship) FROM S) * 100.0 / NULLIF((SELECT SUM(Rev) FROM R), 0) AS Note
                    FROM R LEFT JOIN S ON S.Region = R.Region ORDER BY Value DESC",

            // Mục 7: thao tác OLAP theo vai trò (Roll-up, Drill-down, Slice, Dice, Pivot, cùng kỳ)
            [41] = @"SELECT l.Region AS Label, SUM(f.SalesAmount) AS Value, SUM(f.ProfitAmount) AS Value2, COUNT(DISTINCT l.City) AS Value3
                    FROM Fact_Sales f JOIN Dim_Location l ON l.LocationKey = f.LocationKey GROUP BY l.Region ORDER BY Value DESC",
            [42] = @"SELECT p.Category AS Label, SUM(f.SalesAmount) AS Value, SUM(f.ProfitAmount) AS Value2,
                        SUM(f.ProfitAmount) * 100.0 / NULLIF(SUM(f.SalesAmount), 0) AS Value3, COUNT(DISTINCT p.ProductName) AS Note
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey GROUP BY p.Category ORDER BY Value DESC",
            [43] = $@"SELECT N'Tháng ' + CAST(t.Month AS nvarchar(2)) AS Label, SUM(f.SalesAmount) AS Value, t.Year AS Note
                    FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey WHERE t.Year = {LastSalesYear} AND t.Quarter = 4
                    GROUP BY t.Year, t.Month ORDER BY t.Month",
            [44] = @"SELECT p.Category AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey JOIN Dim_Location l ON l.LocationKey = f.LocationKey
                    WHERE l.Region = N'Miền Nam' GROUP BY p.Category ORDER BY Value DESC",
            [45] = @"SELECT CAST(t.Year AS nvarchar(10)) AS Label, SUM(f.SalesAmount) AS Value, SUM(f.ProfitAmount) AS Value2
                    FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey JOIN Dim_SalesChannel c ON c.ChannelKey = f.ChannelKey
                    WHERE c.ChannelName = N'Website Khang Nghị' GROUP BY t.Year ORDER BY t.Year",
            [46] = @"SELECT p.Category AS Label, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Product p ON p.ProductKey = f.ProductKey JOIN Dim_Time t ON t.TimeKey = f.TimeKey
                    JOIN Dim_Location l ON l.LocationKey = f.LocationKey JOIN Dim_SalesChannel c ON c.ChannelKey = f.ChannelKey
                    WHERE t.Year IN (2024, 2025) AND l.Region IN (N'Miền Bắc', N'Miền Trung') AND c.ChannelName IN (N'Đại lý', N'Cửa hàng')
                    GROUP BY p.Category ORDER BY Value DESC",
            [47] = @"SELECT s.SupplierName AS Label, SUM(f.TotalCost) AS Value, SUM(f.PurchaseQuantity) AS Value2
                    FROM Fact_Purchase f JOIN Dim_Supplier s ON s.SupplierKey = f.SupplierKey JOIN Dim_Time t ON t.TimeKey = f.TimeKey
                    JOIN Dim_Warehouse w ON w.WarehouseKey = f.WarehouseKey
                    WHERE t.Year = 2025 AND w.WarehouseName = N'Kho TP. Hồ Chí Minh' GROUP BY s.SupplierName ORDER BY Value DESC",
            [48] = @"SELECT CAST(t.Year AS nvarchar(10)) AS Label, l.Region AS Grp, SUM(f.SalesAmount) AS Value
                    FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey JOIN Dim_Location l ON l.LocationKey = f.LocationKey
                    GROUP BY t.Year, l.Region ORDER BY t.Year, l.Region",
            [49] = $@"WITH Q AS (SELECT t.Year, t.Quarter, SUM(f.SalesAmount) AS Rev
                        FROM Fact_Sales f JOIN Dim_Time t ON t.TimeKey = f.TimeKey GROUP BY t.Year, t.Quarter)
                    SELECT N'Quý ' + CAST(c.Quarter AS nvarchar(2)) AS Label, (c.Rev / NULLIF(p.Rev, 0) - 1) * 100.0 AS Value,
                        c.Rev AS Value2, p.Rev AS Value3, c.Year AS Note
                    FROM Q c LEFT JOIN Q p ON p.Year = c.Year - 1 AND p.Quarter = c.Quarter
                    WHERE c.Year = {LastSalesYear} ORDER BY c.Quarter",
            [50] = LatestStock + @"SELECT p.Category AS Label, w.WarehouseName AS Grp, SUM(Lt.QuantityOnHand) AS Value
                    FROM Lt JOIN Dim_Product p ON p.ProductKey = Lt.ProductKey JOIN Dim_Warehouse w ON w.WarehouseKey = Lt.WarehouseKey
                    WHERE Lt.rn = 1 GROUP BY p.Category, w.WarehouseName ORDER BY p.Category, w.WarehouseName"
        };

        private static string PolicyFor(int n) => n switch
        {
            47 => "PurchaseAccess",
            50 => "InventoryAccess",
            <= 24 or > 40 => "SalesAccess",
            <= 29 => "PurchaseAccess",
            <= 35 => "InventoryAccess",
            _ => "ShippingAccess"
        };

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var digits = new string((id ?? string.Empty).Where(char.IsDigit).ToArray());
            if (!int.TryParse(digits, out var n) || n < 1 || n > 50)
                return NotFound(new { success = false, message = "Không có câu hỏi " + id });

            if (!(await _auth.AuthorizeAsync(User, PolicyFor(n))).Succeeded)
                return Forbid();

            // MDX-32, MDX-33: tồn trong DWH luôn bằng mức đặt hàng lại, nên dùng số ngày tồn kho (như trang Phân tích chéo)
            if (n is 32 or 33)
            {
                var cover = (await _repo.Q38_HighSalesLowStockRiskAsync(10))
                    .Cast<IDictionary<string, object>>()
                    .Select(r =>
                    {
                        var avgDaily = Convert.ToDouble(r["AvgMonthlySold"] ?? 0) / 30.0;
                        var onHand = Convert.ToDouble(r["QuantityOnHand"] ?? 0);
                        var days = r["DaysOfCover"] == null ? (double?)null : Convert.ToDouble(r["DaysOfCover"]);
                        // Thiếu bao nhiêu để đủ bán 3 ngày (ngưỡng an toàn tối thiểu); cùng ngưỡng với số ngày đã làm tròn của MDX-32
                        var deficit = days is < 3 ? Math.Max(0, Math.Ceiling(avgDaily * 3 - onHand)) : 0;
                        return new { Label = r["ProductName"], Value = n == 32 ? days : deficit, Value2 = onHand, Value3 = n == 32 ? deficit : days };
                    })
                    .ToList();
                return Ok(new { success = true, id = $"MDX-{n:00}", data = cover });
            }

            using var db = _db.CreateConnection();
            var rows = await db.QueryAsync(Sql[n]);
            return Ok(new { success = true, id = $"MDX-{n:00}", data = rows });
        }
    }
}
