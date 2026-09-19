using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Core.DTOs;
using KhangNghi.Infrastructure.Data;

namespace KhangNghi.Infrastructure.Repositories
{
    public interface IDwhAnalyticsRepository
    {
        // KPI Tổng quan
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? year = null, string? region = null);

        // Nhóm 1: Bán hàng (Q1 - Q20)
        Task<IEnumerable<dynamic>> Q01_RevenueByYearAsync();
        Task<IEnumerable<dynamic>> Q02_RevenueByQuarterAsync(int? year = null);
        Task<dynamic?> Q03_PeakMonthRevenueAsync();
        Task<dynamic?> Q04_PeakQuarterProfitAsync();
        Task<IEnumerable<dynamic>> Q05_QuantitySoldOverTimeAsync();
        Task<IEnumerable<dynamic>> Q06_TopProductsByRevenueAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q07_TopProductsByProfitAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q08_RevenueByCategoryAsync();
        Task<IEnumerable<dynamic>> Q09_QuantityBySubCategoryAsync();
        Task<IEnumerable<dynamic>> Q10_HighDiscountLowProfitProductsAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q11_TopCustomersByRevenueAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q12_TopCustomersByProfitAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q13_RevenueBySegmentAsync();
        Task<IEnumerable<dynamic>> Q14_QuantityBySegmentAsync();
        Task<IEnumerable<dynamic>> Q15_RevenueByRegionAsync();
        Task<IEnumerable<dynamic>> Q16_ProfitByCountryAsync();
        Task<IEnumerable<dynamic>> Q17_TopCitiesByQuantityAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q18_RegionalRevenueTrendAsync();
        Task<IEnumerable<dynamic>> Q19_TopManufacturersByRevenueAsync();
        Task<IEnumerable<dynamic>> Q20_TopManufacturersByProfitAsync();

        // Nhóm 2: Mua hàng (Q21 - Q25)
        Task<IEnumerable<dynamic>> Q21_TopSuppliersByAmountAsync();
        Task<IEnumerable<dynamic>> Q22_TopSuppliersByQuantityAsync();
        Task<IEnumerable<dynamic>> Q23_TopPurchasedProductsAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q24_PurchasePriceTrendAsync(string? productName = null);
        Task<IEnumerable<dynamic>> Q25_ImportValueByRegionAsync();

        // Nhóm 3: Tồn kho (Q26 - Q30)
        Task<IEnumerable<dynamic>> Q26_HighestStockProductsAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q27_LowestStockProductsAsync(int top = 10);
        Task<IEnumerable<InventoryAlertDto>> Q28_ReorderLevelAlertsAsync();
        Task<IEnumerable<dynamic>> Q29_StockByRegionAsync();
        Task<IEnumerable<dynamic>> Q30_InventoryMovementOverTimeAsync();

        // Nhóm 4: Vận chuyển (Q31 - Q37)
        Task<IEnumerable<dynamic>> Q31_OrdersByShipperAsync();
        Task<IEnumerable<dynamic>> Q32_ShippingCostByShipperAsync();
        Task<IEnumerable<dynamic>> Q33_AvgShippingCostByShipperAsync();
        Task<IEnumerable<dynamic>> Q34_FastestDeliveryShippersAsync();
        Task<IEnumerable<dynamic>> Q35_HighestLateDaysShippersAsync();
        Task<IEnumerable<dynamic>> Q36_ShippingCostByRegionAsync();
        Task<IEnumerable<dynamic>> Q37_LateDeliveryRateByRegionAsync();

        // Nhóm 5: Phân tích chéo (Q38 - Q40)
        Task<IEnumerable<dynamic>> Q38_HighSalesLowStockRiskAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q39_HighPurchaseLowSalesRiskAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q40_RevenueVsShippingEfficiencyAsync();

        // Dữ liệu huấn luyện ML.NET
        Task<IEnumerable<dynamic>> GetMonthlySalesForTrainingAsync(string? category = null);
    }

    public class DwhAnalyticsRepository : IDwhAnalyticsRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;

        public DwhAnalyticsRepository(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? year = null, string? region = null)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql;

            if (year.HasValue || !string.IsNullOrEmpty(region))
            {
                sql = @"
                    SELECT 
                        ROUND(ISNULL(SUM(f.SalesAmount), 0), 2) AS TotalRevenue,
                        ROUND(ISNULL(SUM(f.ProfitAmount), 0), 2) AS TotalProfit,
                        ROUND(CASE WHEN SUM(f.SalesAmount) > 0 THEN (SUM(f.ProfitAmount) / SUM(f.SalesAmount)) * 100 ELSE 0 END, 2) AS ProfitMargin,
                        COUNT(f.OrderID) AS TotalOrders,
                        ISNULL(SUM(f.Quantity), 0) AS TotalQuantitySold,
                        APPROX_COUNT_DISTINCT(f.CustomerKey) AS TotalCustomers
                    FROM dbo.Fact_Sales f WITH (NOLOCK)
                    " + (year.HasValue ? "JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey AND t.Year = @Year " : "") + @"
                    " + (!string.IsNullOrEmpty(region) ? "JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey AND l.Region = @Region " : "") + @"
                    OPTION (OPTIMIZE FOR UNKNOWN);

                    SELECT ROUND(ISNULL(SUM(ShippingCost), 0), 2) FROM dbo.Fact_Shipping WITH (NOLOCK);
                    SELECT COUNT(*) FROM dbo.Fact_Inventory WITH (NOLOCK) WHERE QuantityOnHand < ReorderLevel;";
            }
            else
            {
                sql = @"
                    SELECT 
                        ROUND(ISNULL(SUM(SalesAmount), 0), 2) AS TotalRevenue,
                        ROUND(ISNULL(SUM(ProfitAmount), 0), 2) AS TotalProfit,
                        ROUND(CASE WHEN SUM(SalesAmount) > 0 THEN (SUM(ProfitAmount) / SUM(SalesAmount)) * 100 ELSE 0 END, 2) AS ProfitMargin,
                        COUNT(*) AS TotalOrders,
                        ISNULL(SUM(Quantity), 0) AS TotalQuantitySold,
                        APPROX_COUNT_DISTINCT(CustomerKey) AS TotalCustomers
                    FROM dbo.Fact_Sales WITH (NOLOCK);

                    SELECT ROUND(ISNULL(SUM(ShippingCost), 0), 2) FROM dbo.Fact_Shipping WITH (NOLOCK);
                    SELECT COUNT(*) FROM dbo.Fact_Inventory WITH (NOLOCK) WHERE QuantityOnHand < ReorderLevel;";
            }

            using var multi = await db.QueryMultipleAsync(sql, new { Year = year, Region = region }, commandTimeout: 30);
            var summary = await multi.ReadFirstOrDefaultAsync<DashboardSummaryDto>() ?? new DashboardSummaryDto();
            summary.TotalShippingCost = await multi.ReadFirstOrDefaultAsync<double>();
            summary.LowStockAlertCount = await multi.ReadFirstOrDefaultAsync<int>();

            return summary;
        }

        // ==================== NHÓM 1: BÁN HÀNG ====================
        public async Task<IEnumerable<dynamic>> Q01_RevenueByYearAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT t.Year, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit, SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                GROUP BY t.Year ORDER BY t.Year ASC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q02_RevenueByQuarterAsync(int? year = null)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT CONCAT('Q', t.Quarter, ' - ', t.Year) AS Period, t.Year, t.Quarter, 
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                WHERE (@Year IS NULL OR t.Year = @Year)
                GROUP BY t.Year, t.Quarter ORDER BY t.Year, t.Quarter";
            return await db.QueryAsync(sql, new { Year = year }, commandTimeout: 60);
        }

        public async Task<dynamic?> Q03_PeakMonthRevenueAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP 1 CONCAT('Tháng ', t.Month, '/', t.Year) AS MonthYear, t.Year, t.Month, 
                             ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                GROUP BY t.Year, t.Month ORDER BY Revenue DESC";
            return await db.QueryFirstOrDefaultAsync(sql, commandTimeout: 60);
        }

        public async Task<dynamic?> Q04_PeakQuarterProfitAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP 1 CONCAT('Quý ', t.Quarter, '/', t.Year) AS Period, t.Year, t.Quarter, 
                             ROUND(SUM(f.ProfitAmount), 2) AS Profit
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                GROUP BY t.Year, t.Quarter ORDER BY Profit DESC";
            return await db.QueryFirstOrDefaultAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q05_QuantitySoldOverTimeAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT CONCAT(t.Month, '/', t.Year) AS Period, t.Year, t.Month, SUM(f.Quantity) AS TotalQuantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                GROUP BY t.Year, t.Month ORDER BY t.Year, t.Month";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q06_TopProductsByRevenueAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) p.ProductKey, p.ProductName, p.Category, p.SubCategory, 
                                   ROUND(SUM(f.SalesAmount), 2) AS Revenue, SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.ProductKey, p.ProductName, p.Category, p.SubCategory
                ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q07_TopProductsByProfitAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) p.ProductKey, p.ProductName, p.Category, p.SubCategory, 
                                   ROUND(SUM(f.ProfitAmount), 2) AS Profit, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.ProductKey, p.ProductName, p.Category, p.SubCategory
                ORDER BY Profit DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q08_RevenueByCategoryAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT p.Category, 
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue, 
                       ROUND(SUM(f.ProfitAmount), 2) AS Profit,
                       SUM(f.Quantity) AS Quantity,
                       ROUND(CASE WHEN SUM(f.SalesAmount) > 0 THEN (SUM(f.ProfitAmount) / SUM(f.SalesAmount)) * 100 ELSE 0 END, 2) AS ProfitMargin
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.Category ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q09_QuantityBySubCategoryAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT p.SubCategory, p.Category, SUM(f.Quantity) AS TotalQuantity, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.SubCategory, p.Category ORDER BY TotalQuantity DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q10_HighDiscountLowProfitProductsAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) p.ProductName, p.Category, 
                                   ROUND(AVG(f.DiscountAmount) * 100, 1) AS AvgDiscountPercent, 
                                   ROUND(SUM(f.ProfitAmount), 2) AS TotalProfit,
                                   ROUND(SUM(f.SalesAmount), 2) AS TotalRevenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.ProductName, p.Category
                HAVING AVG(f.DiscountAmount) > 0.15
                ORDER BY TotalProfit ASC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q11_TopCustomersByRevenueAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) c.CustomerKey, c.CustomerName, c.Segment, 
                                   ROUND(SUM(f.SalesAmount), 2) AS Revenue, COUNT(DISTINCT f.OrderID) AS OrderCount
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                GROUP BY c.CustomerKey, c.CustomerName, c.Segment ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q12_TopCustomersByProfitAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) c.CustomerKey, c.CustomerName, c.Segment, 
                                   ROUND(SUM(f.ProfitAmount), 2) AS Profit, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                GROUP BY c.CustomerKey, c.CustomerName, c.Segment ORDER BY Profit DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q13_RevenueBySegmentAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT c.Segment, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit,
                       COUNT(DISTINCT f.OrderID) AS OrderCount
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                GROUP BY c.Segment ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q14_QuantityBySegmentAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT c.Segment, SUM(f.Quantity) AS TotalQuantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                GROUP BY c.Segment ORDER BY TotalQuantity DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q15_RevenueByRegionAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Region, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit,
                       SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
                GROUP BY l.Region ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q16_ProfitByCountryAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Country, ROUND(SUM(f.ProfitAmount), 2) AS Profit, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
                GROUP BY l.Country ORDER BY Profit DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q17_TopCitiesByQuantityAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) l.City, l.Region, SUM(f.Quantity) AS TotalQuantity, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
                GROUP BY l.City, l.Region ORDER BY TotalQuantity DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q18_RegionalRevenueTrendAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Region, t.Year, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                GROUP BY l.Region, t.Year ORDER BY t.Year, l.Region";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q19_TopManufacturersByRevenueAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT m.ManufacturerName, m.Country, 
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue, 
                       COUNT(DISTINCT p.ProductKey) AS ProductCount
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                JOIN dbo.Dim_Manufacturer m WITH (NOLOCK) ON f.ManufacturerKey = m.ManufacturerKey
                GROUP BY m.ManufacturerName, m.Country ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q20_TopManufacturersByProfitAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT m.ManufacturerName, m.Country, 
                       ROUND(SUM(f.ProfitAmount), 2) AS Profit, 
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                JOIN dbo.Dim_Manufacturer m WITH (NOLOCK) ON f.ManufacturerKey = m.ManufacturerKey
                GROUP BY m.ManufacturerName, m.Country ORDER BY Profit DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        // ==================== NHÓM 2: MUA HÀNG ====================
        public async Task<IEnumerable<dynamic>> Q21_TopSuppliersByAmountAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.SupplierName, ROUND(SUM(p.TotalCost), 2) AS TotalPurchaseAmount,
                       SUM(p.PurchaseQuantity) AS TotalQuantity
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Supplier s ON p.SupplierKey = s.SupplierKey
                GROUP BY s.SupplierName ORDER BY TotalPurchaseAmount DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q22_TopSuppliersByQuantityAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.SupplierName, SUM(p.PurchaseQuantity) AS TotalQuantity, 
                       ROUND(SUM(p.TotalCost), 2) AS TotalPurchaseAmount
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Supplier s ON p.SupplierKey = s.SupplierKey
                GROUP BY s.SupplierName ORDER BY TotalQuantity DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q23_TopPurchasedProductsAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) pr.ProductName, pr.Category, 
                                   ROUND(SUM(p.TotalCost), 2) AS TotalPurchaseAmount,
                                   SUM(p.PurchaseQuantity) AS TotalQuantity
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Product pr ON p.ProductKey = pr.ProductKey
                GROUP BY pr.ProductName, pr.Category ORDER BY TotalPurchaseAmount DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q24_PurchasePriceTrendAsync(string? productName = null)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP 30 pr.ProductName, CONCAT(t.Month, '/', t.Year) AS Period, t.Year, t.Month,
                              ROUND(AVG(p.UnitCost), 2) AS AvgUnitCost
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Product pr ON p.ProductKey = pr.ProductKey
                JOIN dbo.Dim_Time t ON p.TimeKey = t.TimeKey
                WHERE (@ProductName IS NULL OR pr.ProductName LIKE '%' + @ProductName + '%')
                GROUP BY pr.ProductName, t.Year, t.Month ORDER BY t.Year, t.Month";
            return await db.QueryAsync(sql, new { ProductName = productName });
        }

        public async Task<IEnumerable<dynamic>> Q25_ImportValueByRegionAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Region, ROUND(SUM(p.TotalCost), 2) AS TotalImportValue,
                       SUM(p.PurchaseQuantity) AS TotalQuantity
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Location l ON p.LocationKey = l.LocationKey
                GROUP BY l.Region ORDER BY TotalImportValue DESC";
            return await db.QueryAsync(sql);
        }

        // ==================== NHÓM 3: TỒN KHO ====================
        public async Task<IEnumerable<dynamic>> Q26_HighestStockProductsAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) pr.ProductName, pr.Category, SUM(i.QuantityOnHand) AS TotalOnHand,
                                   AVG(i.ReorderLevel) AS AvgReorderLevel
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Product pr ON i.ProductKey = pr.ProductKey
                GROUP BY pr.ProductName, pr.Category ORDER BY TotalOnHand DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q27_LowestStockProductsAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) pr.ProductName, pr.Category, SUM(i.QuantityOnHand) AS TotalOnHand,
                                   AVG(i.ReorderLevel) AS AvgReorderLevel
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Product pr ON i.ProductKey = pr.ProductKey
                GROUP BY pr.ProductName, pr.Category ORDER BY TotalOnHand ASC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<InventoryAlertDto>> Q28_ReorderLevelAlertsAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT 
                    pr.ProductKey, 
                    pr.ProductName, 
                    pr.Category, 
                    pr.SubCategory, 
                    CONCAT(l.City, ' (', l.Region, ')') AS Location, 
                    i.QuantityOnHand, 
                    i.ReorderLevel, 
                    (i.ReorderLevel - i.QuantityOnHand) AS Deficit,
                    CASE 
                        WHEN i.QuantityOnHand < (i.ReorderLevel * 0.5) THEN 'Red'
                        WHEN i.QuantityOnHand < i.ReorderLevel THEN 'Yellow'
                        ELSE 'Green'
                    END AS StatusColor,
                    CASE 
                        WHEN i.QuantityOnHand < (i.ReorderLevel * 0.5) THEN N'Khẩn cấp: Tồn kho chạm đáy dưới 50% mức an toàn!'
                        WHEN i.QuantityOnHand < i.ReorderLevel THEN N'Cảnh báo: Tồn kho thấp hơn định mức, cần lên đơn nhập!'
                        ELSE N'Tồn kho an toàn'
                    END AS Recommendation
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Product pr ON i.ProductKey = pr.ProductKey
                JOIN dbo.Dim_Location l ON i.LocationKey = l.LocationKey
                WHERE i.QuantityOnHand < i.ReorderLevel
                ORDER BY Deficit DESC";
            return await db.QueryAsync<InventoryAlertDto>(sql);
        }

        public async Task<IEnumerable<dynamic>> Q29_StockByRegionAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Region, SUM(i.QuantityOnHand) AS TotalOnHand,
                       SUM(i.QuantityIn) AS TotalIn, SUM(i.QuantityOut) AS TotalOut
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Location l ON i.LocationKey = l.LocationKey
                GROUP BY l.Region ORDER BY TotalOnHand DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q30_InventoryMovementOverTimeAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT CONCAT('Tháng ', t.Month, '/', t.Year) AS Period, t.Year, t.Month,
                       SUM(i.QuantityIn) AS TotalQuantityIn,
                       SUM(i.QuantityOut) AS TotalQuantityOut,
                       SUM(i.QuantityOnHand) AS TotalQuantityOnHand
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Time t ON i.TimeKey = t.TimeKey
                GROUP BY t.Year, t.Month ORDER BY t.Year, t.Month";
            return await db.QueryAsync(sql);
        }

        // ==================== NHÓM 4: VẬN CHUYỂN ====================
        public async Task<IEnumerable<dynamic>> Q31_OrdersByShipperAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.ShipperName, s.ServiceLevel, COUNT(f.OrderID) AS TotalOrders
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Shipper s ON f.ShipperKey = s.ShipperKey
                GROUP BY s.ShipperName, s.ServiceLevel ORDER BY TotalOrders DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q32_ShippingCostByShipperAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.ShipperName, ROUND(SUM(f.ShippingCost), 2) AS TotalShippingCost
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Shipper s ON f.ShipperKey = s.ShipperKey
                GROUP BY s.ShipperName ORDER BY TotalShippingCost DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q33_AvgShippingCostByShipperAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.ShipperName, ROUND(AVG(f.ShippingCost), 2) AS AvgCostPerOrder,
                       COUNT(f.OrderID) AS TotalOrders
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Shipper s ON f.ShipperKey = s.ShipperKey
                GROUP BY s.ShipperName ORDER BY AvgCostPerOrder ASC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q34_FastestDeliveryShippersAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.ShipperName, s.ShippingMethod, ROUND(AVG(CAST(f.DeliveryDays AS FLOAT)), 1) AS AvgDeliveryDays
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Shipper s ON f.ShipperKey = s.ShipperKey
                GROUP BY s.ShipperName, s.ShippingMethod ORDER BY AvgDeliveryDays ASC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q35_HighestLateDaysShippersAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT s.ShipperName, 
                       ROUND(AVG(CAST(f.LateDays AS FLOAT)), 2) AS AvgLateDays,
                       COUNT(CASE WHEN f.LateDays > 0 THEN 1 END) AS LateOrdersCount,
                       COUNT(f.OrderID) AS TotalOrders
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Shipper s ON f.ShipperKey = s.ShipperKey
                GROUP BY s.ShipperName ORDER BY AvgLateDays DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q36_ShippingCostByRegionAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Region, ROUND(SUM(f.ShippingCost), 2) AS TotalShippingCost,
                       ROUND(AVG(f.ShippingCost), 2) AS AvgCostPerOrder
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Location l ON f.LocationKey = l.LocationKey
                GROUP BY l.Region ORDER BY TotalShippingCost DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<dynamic>> Q37_LateDeliveryRateByRegionAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT l.Region, 
                       COUNT(CASE WHEN f.LateDays > 0 THEN 1 END) AS LateOrderCount, 
                       COUNT(f.OrderID) AS TotalOrders, 
                       ROUND(COUNT(CASE WHEN f.LateDays > 0 THEN 1 END) * 100.0 / COUNT(f.OrderID), 2) AS LateRatePercent
                FROM dbo.Fact_Shipping f
                JOIN dbo.Dim_Location l ON f.LocationKey = l.LocationKey
                GROUP BY l.Region ORDER BY LateRatePercent DESC";
            return await db.QueryAsync(sql);
        }

        // ==================== NHÓM 5: PHÂN TÍCH CHÉO ====================
        public async Task<IEnumerable<dynamic>> Q38_HighSalesLowStockRiskAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) 
                    p.ProductKey, p.ProductName, p.Category, 
                    SUM(o.Quantity) AS SoldQuantity, 
                    ISNULL(inv.OnHand, 0) AS QuantityOnHand,
                    CASE 
                        WHEN ISNULL(inv.OnHand, 0) < SUM(o.Quantity) * 0.1 THEN N'Rất cao' 
                        ELSE N'Cao' 
                    END AS StockoutRisk
                FROM dbo.Fact_Sales o WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON o.ProductKey = p.ProductKey
                LEFT JOIN (SELECT ProductKey, SUM(QuantityOnHand) AS OnHand FROM dbo.Fact_Inventory WITH (NOLOCK) GROUP BY ProductKey) inv 
                    ON p.ProductKey = inv.ProductKey
                GROUP BY p.ProductKey, p.ProductName, p.Category, inv.OnHand
                ORDER BY SoldQuantity DESC, QuantityOnHand ASC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q39_HighPurchaseLowSalesRiskAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql = $@"
                SELECT TOP ({top}) 
                    p.ProductKey, p.ProductName, p.Category, 
                    ISNULL(pur.PurQty, 0) AS PurchasedQty, 
                    ISNULL(ord.SoldQty, 0) AS SoldQty, 
                    (ISNULL(pur.PurQty, 0) - ISNULL(ord.SoldQty, 0)) AS ExcessQty, 
                    N'Nguy cơ ứ đọng vốn' AS RiskNote
                FROM dbo.Dim_Product p WITH (NOLOCK)
                LEFT JOIN (SELECT ProductKey, SUM(PurchaseQuantity) AS PurQty FROM dbo.Fact_Purchase WITH (NOLOCK) GROUP BY ProductKey) pur 
                    ON p.ProductKey = pur.ProductKey
                LEFT JOIN (SELECT ProductKey, SUM(Quantity) AS SoldQty FROM dbo.Fact_Sales WITH (NOLOCK) GROUP BY ProductKey) ord 
                    ON p.ProductKey = ord.ProductKey
                WHERE ISNULL(pur.PurQty, 0) > ISNULL(ord.SoldQty, 0)
                ORDER BY ExcessQty DESC";
            return await db.QueryAsync(sql, commandTimeout: 60);
        }


        public async Task<IEnumerable<dynamic>> Q40_RevenueVsShippingEfficiencyAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                WITH RegionSales AS (
                    SELECT l.Region,
                           ROUND(SUM(o.SalesAmount), 2)  AS TotalRevenue,
                           ROUND(SUM(o.ProfitAmount), 2) AS TotalProfit
                    FROM dbo.Fact_Sales o WITH (NOLOCK)
                    JOIN dbo.Dim_Location l WITH (NOLOCK) ON o.LocationKey = l.LocationKey
                    GROUP BY l.Region
                ),
                RegionShipping AS (
                    SELECT l.Region,
                           ROUND(SUM(s.ShippingCost), 2) AS ShippingCost
                    FROM dbo.Fact_Shipping s WITH (NOLOCK)
                    JOIN dbo.Dim_Location l WITH (NOLOCK) ON s.LocationKey = l.LocationKey
                    GROUP BY l.Region
                )
                SELECT rs.Region,
                       rs.TotalRevenue,
                       rs.TotalProfit,
                       ISNULL(rsh.ShippingCost, 0) AS ShippingCost,
                       ROUND(ISNULL(rsh.ShippingCost, 0) / NULLIF(rs.TotalRevenue, 0) * 100, 2) AS ShippingCostRatioPercent,
                       ROUND(rs.TotalProfit - ISNULL(rsh.ShippingCost, 0), 2) AS NetProfitAfterShipping
                FROM RegionSales rs
                LEFT JOIN RegionShipping rsh ON rs.Region = rsh.Region
                ORDER BY rs.TotalRevenue DESC";
            return await db.QueryAsync(sql, commandTimeout: 30);
        }

        // ==================== DỮ LIỆU HUẤN LUYỆN ML ====================
        public async Task<IEnumerable<dynamic>> GetMonthlySalesForTrainingAsync(string? category = null)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT 
                    t.Year, 
                    t.Month, 
                    CONCAT(t.Year, '-', RIGHT('0' + CAST(t.Month AS VARCHAR(2)), 2)) AS YearMonth,
                    ROUND(SUM(f.SalesAmount), 2) AS Sales,
                    SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                LEFT JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                WHERE (@Category IS NULL OR p.Category = @Category)
                GROUP BY t.Year, t.Month
                ORDER BY t.Year, t.Month ASC";
            return await db.QueryAsync(sql, new { Category = category });
        }
    }
}







