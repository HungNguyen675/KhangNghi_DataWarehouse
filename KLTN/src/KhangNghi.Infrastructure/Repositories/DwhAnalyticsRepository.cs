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
        Task<dynamic?> GetCustomerDetailByNaturalIdAsync(string customerId);
        Task<dynamic?> GetOrderDetailByNaturalIdAsync(string orderId);
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
        Task<IEnumerable<PurchaseOrderSummaryDto>> GetPurchaseOrdersSummaryAsync(int? year = null);
        Task<IEnumerable<PurchaseOrderDetailDto>> GetPurchaseOrderDetailAsync(string orderId, int top = 50);
        Task<IEnumerable<PurchaseCategorySpendDto>> GetPurchaseCategorySpendAsync(int? year = null);

        // Nhóm 3: Tồn kho (Q26 - Q30)
        Task<IEnumerable<dynamic>> Q26_HighestStockProductsAsync(int top = 10);
        Task<IEnumerable<dynamic>> Q27_LowestStockProductsAsync(int top = 10);
        Task<IEnumerable<InventoryAlertDto>> Q28_ReorderLevelAlertsAsync(int top = 50);
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
        // Diễn giải số ngày tồn kho của một mặt hàng (drill-down từ Q38/Q39)
        Task<(IEnumerable<dynamic> Stock, IEnumerable<dynamic> Sales)> GetStockCoverDetailAsync(int productKey);
        Task<IEnumerable<dynamic>> Q40_RevenueVsShippingEfficiencyAsync();
        Task<object?> GetQuickChartAsync(string id);
        Task<object?> GetMultiDimAnalysisAsync(string measure, string rowDim, string colDim);

        // Chi tiết doanh thu, lợi nhuận trong một năm (drill-down từ biểu đồ)
        Task<(IEnumerable<dynamic> Categories, IEnumerable<dynamic> Regions, IEnumerable<dynamic> Products)> GetYearBreakdownAsync(int year, int top = 10);

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
            const string sql = @"
                SELECT TOP (@Top) p.ProductKey, p.ProductName, p.Category, p.SubCategory, 
                                   ROUND(SUM(f.SalesAmount), 2) AS Revenue, SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.ProductKey, p.ProductName, p.Category, p.SubCategory
                ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q07_TopProductsByProfitAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top) p.ProductKey, p.ProductName, p.Category, p.SubCategory, 
                                   ROUND(SUM(f.ProfitAmount), 2) AS Profit, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.ProductKey, p.ProductName, p.Category, p.SubCategory
                ORDER BY Profit DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
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
            const string sql = @"
                SELECT TOP (@Top) p.ProductName, p.Category, 
                                   ROUND(AVG(f.DiscountAmount) * 100, 1) AS AvgDiscountPercent, 
                                   ROUND(SUM(f.ProfitAmount), 2) AS TotalProfit,
                                   ROUND(SUM(f.SalesAmount), 2) AS TotalRevenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                GROUP BY p.ProductName, p.Category
                HAVING AVG(f.DiscountAmount) > 0.15
                ORDER BY TotalProfit ASC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q11_TopCustomersByRevenueAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top) c.CustomerKey, c.CustomerID, c.CustomerName, c.Segment, 
                                   ROUND(SUM(f.SalesAmount), 2) AS Revenue, COUNT(DISTINCT f.OrderID) AS OrderCount
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                GROUP BY c.CustomerKey, c.CustomerID, c.CustomerName, c.Segment ORDER BY Revenue DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
        }

        public async Task<IEnumerable<dynamic>> Q12_TopCustomersByProfitAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top) c.CustomerKey, c.CustomerID, c.CustomerName, c.Segment, 
                                   ROUND(SUM(f.ProfitAmount), 2) AS Profit, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                GROUP BY c.CustomerKey, c.CustomerID, c.CustomerName, c.Segment ORDER BY Profit DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
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
            const string sql = @"
                SELECT TOP (@Top) l.City, l.Region, SUM(f.Quantity) AS TotalQuantity, ROUND(SUM(f.SalesAmount), 2) AS Revenue
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
                GROUP BY l.City, l.Region ORDER BY TotalQuantity DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
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
            const string sql = @"
                SELECT TOP (@Top) 
                    pr.ProductName,
                    ISNULL(pr.Category, N'Vật tư thiết bị') AS Category,
                    ROUND(SUM(p.TotalCost), 2) AS TotalPurchaseAmount,
                    SUM(p.PurchaseQuantity) AS TotalQuantity
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Product pr ON p.ProductKey = pr.ProductKey
                WHERE pr.ProductName IS NOT NULL
                GROUP BY pr.ProductName, pr.Category
                ORDER BY TotalPurchaseAmount DESC";
            return await db.QueryAsync(sql, new { Top = top });
        }

        public async Task<IEnumerable<dynamic>> Q24_PurchasePriceTrendAsync(string? productName = null)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    pr.ProductName,
                    CONCAT(t.Month, '/', t.Year) AS Period, t.Year, t.Month,
                    ROUND(AVG(p.UnitCost), 2) AS AvgUnitCost
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Product pr ON p.ProductKey = pr.ProductKey
                JOIN dbo.Dim_Time t ON p.TimeKey = t.TimeKey
                WHERE pr.ProductName IS NOT NULL
                  AND (@ProductName IS NULL OR pr.ProductName LIKE '%' + @ProductName + '%')
                GROUP BY pr.ProductName, t.Year, t.Month 
                ORDER BY t.Year, t.Month";
            return await db.QueryAsync(sql, new { ProductName = productName });
        }

        public async Task<IEnumerable<dynamic>> Q25_ImportValueByRegionAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    w.WarehouseName,
                    ISNULL(l.Region, N'Không xác định') AS Region,
                    ROUND(SUM(p.TotalCost), 2) AS TotalImportValue,
                    SUM(p.PurchaseQuantity) AS TotalQuantity
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Warehouse w ON p.WarehouseKey = w.WarehouseKey
                LEFT JOIN dbo.Dim_Location l ON p.LocationKey = l.LocationKey
                GROUP BY w.WarehouseName, ISNULL(l.Region, N'Không xác định')
                ORDER BY TotalImportValue DESC";
            return await db.QueryAsync(sql);
        }

        public async Task<IEnumerable<PurchaseOrderSummaryDto>> GetPurchaseOrdersSummaryAsync(int? year = null)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    p.PurchaseOrderID,
                    CONVERT(varchar(10), MIN(t.FullDate), 120) AS OrderDate,
                    s.SupplierKey,
                    s.SupplierName,
                    w.WarehouseKey,
                    w.WarehouseName,
                    COUNT(*) AS LineItemCount,
                    SUM(p.PurchaseQuantity) AS TotalQuantity,
                    ROUND(SUM(p.TotalCost), 2) AS TotalCost,
                    ROUND(AVG(p.UnitCost), 2) AS AvgUnitCost
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Supplier s ON p.SupplierKey = s.SupplierKey
                JOIN dbo.Dim_Warehouse w ON p.WarehouseKey = w.WarehouseKey
                JOIN dbo.Dim_Time t ON p.TimeKey = t.TimeKey
                WHERE (@Year IS NULL OR t.Year = @Year)
                GROUP BY p.PurchaseOrderID, s.SupplierKey, s.SupplierName, w.WarehouseKey, w.WarehouseName
                ORDER BY TotalCost DESC";
            return await db.QueryAsync<PurchaseOrderSummaryDto>(sql, new { Year = year });
        }

        public async Task<IEnumerable<PurchaseOrderDetailDto>> GetPurchaseOrderDetailAsync(string orderId, int top = 50)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top)
                    p.PurchaseOrderID,
                    CONVERT(varchar(10), t.FullDate, 120) AS OrderDate,
                    s.SupplierName,
                    w.WarehouseName,
                    pr.ProductID,
                    COALESCE(pr.ProductName, pr_ref.ProductName, pr.ProductID) AS ProductName,
                    COALESCE(pr.Category, pr_ref.Category, N'Vật tư thiết bị') AS Category,
                    COALESCE(pr.SubCategory, pr_ref.SubCategory, N'Khác') AS SubCategory,
                    p.PurchaseQuantity,
                    ROUND(p.UnitCost, 2) AS UnitCost,
                    ROUND(p.TotalCost, 2) AS TotalCost
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Supplier s ON p.SupplierKey = s.SupplierKey
                JOIN dbo.Dim_Warehouse w ON p.WarehouseKey = w.WarehouseKey
                JOIN dbo.Dim_Time t ON p.TimeKey = t.TimeKey
                JOIN dbo.Dim_Product pr ON p.ProductKey = pr.ProductKey
                LEFT JOIN dbo.Dim_Product pr_ref 
                    ON REPLACE(pr.ProductID, 'PUR_', '') = pr_ref.ProductID 
                    AND pr_ref.ProductName IS NOT NULL
                WHERE p.PurchaseOrderID = @OrderId
                ORDER BY p.TotalCost DESC";
            return await db.QueryAsync<PurchaseOrderDetailDto>(sql, new { OrderId = orderId, Top = top });
        }

        public async Task<IEnumerable<PurchaseCategorySpendDto>> GetPurchaseCategorySpendAsync(int? year = null)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT 
                    COALESCE(pr.Category, N'Vật tư thiết bị') AS Category,
                    ROUND(SUM(p.TotalCost), 2) AS TotalCost,
                    SUM(p.PurchaseQuantity) AS TotalQuantity
                FROM dbo.Fact_Purchase p
                JOIN dbo.Dim_Product pr ON p.ProductKey = pr.ProductKey
                JOIN dbo.Dim_Time t ON p.TimeKey = t.TimeKey
                WHERE (@Year IS NULL OR t.Year = @Year)
                GROUP BY COALESCE(pr.Category, N'Vật tư thiết bị')
                ORDER BY TotalCost DESC";
            return await db.QueryAsync<PurchaseCategorySpendDto>(sql, new { Year = year });
        }


        // ==================== NHÓM 3: TỒN KHO ====================
        public async Task<IEnumerable<dynamic>> Q26_HighestStockProductsAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top) pr.ProductName, pr.Category, SUM(i.QuantityOnHand) AS TotalOnHand,
                                   AVG(i.ReorderLevel) AS AvgReorderLevel
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Product pr ON i.ProductKey = pr.ProductKey
                GROUP BY pr.ProductName, pr.Category ORDER BY TotalOnHand DESC";
            return await db.QueryAsync(sql, new { Top = top });
        }

        public async Task<IEnumerable<dynamic>> Q27_LowestStockProductsAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top) pr.ProductName, pr.Category, SUM(i.QuantityOnHand) AS TotalOnHand,
                                   AVG(i.ReorderLevel) AS AvgReorderLevel
                FROM dbo.Fact_Inventory i
                JOIN dbo.Dim_Product pr ON i.ProductKey = pr.ProductKey
                GROUP BY pr.ProductName, pr.Category ORDER BY TotalOnHand ASC";
            return await db.QueryAsync(sql, new { Top = top });
        }

        public async Task<IEnumerable<InventoryAlertDto>> Q28_ReorderLevelAlertsAsync(int top = 50)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT TOP (@Top)
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
            return await db.QueryAsync<InventoryAlertDto>(sql, new { Top = top });
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
        // Q38 & Q39 dùng chung thước đo "số ngày tồn kho" (Days of Inventory) = tồn hiện tại / bán TB mỗi ngày (3 tháng gần nhất).
        // Tồn lấy theo snapshot mới nhất của từng sản phẩm/kho (tồn kho là số đo bán cộng dồn, không cộng qua thời gian).
        // Chu kỳ nhập hàng theo tuần: trên 7 ngày hoặc không bán được -> Tồn thừa; dưới 3 ngày hoặc dưới mức đặt lại -> Sắp hết.
        private const string StockCoverSql = @"
                WITH LatestInv AS (
                    SELECT ProductKey, QuantityOnHand, ReorderLevel,
                           ROW_NUMBER() OVER (PARTITION BY ProductKey, WarehouseKey, LocationKey ORDER BY TimeKey DESC) AS rn
                    FROM dbo.Fact_Inventory WITH (NOLOCK)
                ),
                Stock AS (
                    SELECT ProductKey, SUM(QuantityOnHand) AS OnHand, SUM(ReorderLevel) AS ReorderLevel
                    FROM LatestInv WHERE rn = 1 GROUP BY ProductKey
                ),
                SalesM AS (
                    SELECT f.ProductKey, t.Year * 12 + t.Month AS YM, f.Quantity
                    FROM dbo.Fact_Sales f WITH (NOLOCK)
                    JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                ),
                LastYM AS (SELECT MAX(YM) AS MaxYM FROM SalesM),
                Sales AS (
                    SELECT sm.ProductKey,
                           SUM(CASE WHEN sm.YM > ly.MaxYM - 3 THEN sm.Quantity ELSE 0 END) / 3.0 AS AvgMonthly
                    FROM SalesM sm CROSS JOIN LastYM ly
                    GROUP BY sm.ProductKey
                ),
                Base AS (
                    SELECT p.ProductKey, p.ProductName, p.Category,
                           ISNULL(s.AvgMonthly, 0) AS AvgMonthly,
                           ISNULL(st.OnHand, 0) AS OnHand,
                           ISNULL(st.ReorderLevel, 0) AS ReorderLevel,
                           ISNULL(st.OnHand, 0) * 30.0 / NULLIF(s.AvgMonthly, 0) AS Cover
                    FROM dbo.Dim_Product p WITH (NOLOCK)
                    LEFT JOIN Sales s ON p.ProductKey = s.ProductKey
                    LEFT JOIN Stock st ON p.ProductKey = st.ProductKey
                    WHERE ISNULL(s.AvgMonthly, 0) > 0 OR ISNULL(st.OnHand, 0) > 0
                )
                SELECT TOP (@Top)
                    ProductKey, ProductName, Category,
                    ROUND(AvgMonthly, 0) AS AvgMonthlySold,
                    OnHand AS QuantityOnHand,
                    ReorderLevel,
                    FLOOR(Cover * 10) / 10.0 AS DaysOfCover, -- làm tròn xuống: 2,96 ngày hiện 2,9 cho khớp mức Sắp hết (< 3)
                    CASE
                        WHEN Cover IS NULL OR Cover > 7 THEN N'Tồn thừa'
                        WHEN Cover < 3 OR OnHand < ReorderLevel THEN N'Sắp hết'
                        ELSE N'An toàn'
                    END AS StockStatus
                FROM Base";

        // Q38: Mặt hàng nào sắp hết hàng? (số ngày tồn kho ít nhất lên đầu)
        public async Task<IEnumerable<dynamic>> Q38_HighSalesLowStockRiskAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = StockCoverSql + @"
                ORDER BY ISNULL(Cover, 9999) ASC, AvgMonthly DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
        }

        // Q39: Mặt hàng nào tồn quá nhiều so với sức bán? (số ngày tồn kho nhiều nhất lên đầu, không bán được xếp đầu)
        public async Task<IEnumerable<dynamic>> Q39_HighPurchaseLowSalesRiskAsync(int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = StockCoverSql + @"
                ORDER BY ISNULL(Cover, 9999) DESC, OnHand DESC";
            return await db.QueryAsync(sql, new { Top = top }, commandTimeout: 60);
        }

        // Số liệu gốc của StockCoverSql cho một mặt hàng: tồn mới nhất từng kho + bán từng tháng trong 3 tháng gần nhất
        public async Task<(IEnumerable<dynamic> Stock, IEnumerable<dynamic> Sales)> GetStockCoverDetailAsync(int productKey)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                WITH LatestInv AS (
                    SELECT i.*, ROW_NUMBER() OVER (PARTITION BY i.WarehouseKey, i.LocationKey ORDER BY i.TimeKey DESC) AS rn
                    FROM dbo.Fact_Inventory i WITH (NOLOCK)
                    WHERE i.ProductKey = @ProductKey
                )
                SELECT ISNULL(w.WarehouseName, N'Chưa xác định') AS Warehouse, l.City,
                       t.FullDate AS SnapshotDate, li.QuantityOnHand, li.ReorderLevel
                FROM LatestInv li
                LEFT JOIN dbo.Dim_Warehouse w WITH (NOLOCK) ON li.WarehouseKey = w.WarehouseKey
                LEFT JOIN dbo.Dim_Location l WITH (NOLOCK) ON li.LocationKey = l.LocationKey
                LEFT JOIN dbo.Dim_Time t WITH (NOLOCK) ON li.TimeKey = t.TimeKey
                WHERE li.rn = 1
                ORDER BY Warehouse;

                WITH SalesM AS (
                    SELECT f.ProductKey, t.Year * 12 + t.Month AS YM, f.Quantity
                    FROM dbo.Fact_Sales f WITH (NOLOCK)
                    JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                ),
                Months AS (
                    SELECT MAX(YM) - v.n AS YM FROM SalesM CROSS JOIN (VALUES (0), (1), (2)) v(n) GROUP BY v.n
                )
                SELECT (m.YM - 1) / 12 AS [Year], (m.YM - 1) % 12 + 1 AS [Month], ISNULL(SUM(s.Quantity), 0) AS Quantity
                FROM Months m
                LEFT JOIN SalesM s ON s.YM = m.YM AND s.ProductKey = @ProductKey
                GROUP BY m.YM
                ORDER BY m.YM;";
            using var multi = await db.QueryMultipleAsync(sql, new { ProductKey = productKey }, commandTimeout: 30);
            var stock = await multi.ReadAsync();
            var sales = await multi.ReadAsync();
            return (stock, sales);
        }


        
        public async Task<object?> GetQuickChartAsync(string id)
        {
            using var db = _connectionFactory.CreateConnection();
            var normId = (id ?? string.Empty).Trim().ToUpperInvariant();

            switch (normId)
            {
                case "MDX-19":
                {
                    const string sql = @"
                        SELECT e.EmployeeName AS Label,
                               COALESCE(e.Title, N'Nhân viên kinh doanh') AS SubLabel,
                               ROUND(SUM(f.SalesAmount), 0) AS Revenue,
                               ROUND(SUM(f.ProfitAmount), 0) AS Profit,
                               SUM(f.Quantity) AS Quantity,
                               COUNT(*) AS OrderCount
                        FROM dbo.Fact_Sales f WITH (NOLOCK)
                        JOIN dbo.Dim_Employee e WITH (NOLOCK) ON f.EmployeeKey = e.EmployeeKey
                        GROUP BY e.EmployeeName, e.Title
                        ORDER BY Revenue DESC";
                    var rows = await db.QueryAsync(sql, commandTimeout: 60);
                    return new
                    {
                        id = "MDX-19",
                        title = "Đánh giá KPI Doanh số bán hàng theo từng Nhân viên",
                        purpose = "Đánh giá năng suất của đội ngũ bán hàng và tính hoa hồng kinh doanh.",
                        section = "Nhân sự & Khuyến mãi",
                        chartType = "bar",
                        dimension = "Nhân viên (Dim_Employee)",
                        measures = new[] { "Doanh thu", "Lợi nhuận", "Số lượng bán" },
                        relatedRoute = "sales",
                        data = rows
                    };
                }
                case "MDX-20":
                {
                    const string sql = @"
                        SELECT p.PromotionName AS Label,
                               ROUND(SUM(f.SalesAmount), 0) AS Revenue,
                               ROUND(SUM(f.DiscountAmount), 0) AS DiscountAmount,
                               ROUND(SUM(f.ProfitAmount), 0) AS Profit,
                               COUNT(*) AS OrderCount
                        FROM dbo.Fact_Sales f WITH (NOLOCK)
                        JOIN dbo.Dim_Promotion p WITH (NOLOCK) ON f.PromotionKey = p.PromotionKey
                        GROUP BY p.PromotionName
                        ORDER BY Revenue DESC";
                    var rows = await db.QueryAsync(sql, commandTimeout: 60);
                    return new
                    {
                        id = "MDX-20",
                        title = "Đánh giá hiệu quả các chương trình Khuyến mãi",
                        purpose = "Phân tích lượng tiền chiết khấu (Discount) đã bỏ ra so với doanh thu mang lại của từng đợt khuyến mãi.",
                        section = "Nhân sự & Khuyến mãi",
                        chartType = "donut",
                        dimension = "Chương trình khuyến mãi (Dim_Promotion)",
                        measures = new[] { "Doanh thu", "Chiết khấu", "Lợi nhuận" },
                        relatedRoute = "sales",
                        data = rows
                    };
                }
                case "MDX-21":
                {
                    const string sql = @"
                        SELECT m.ManufacturerName AS Label,
                               COALESCE(m.Country, N'Chưa rõ') AS Country,
                               ROUND(SUM(f.SalesAmount), 0) AS Revenue,
                               ROUND(SUM(f.ProfitAmount), 0) AS Profit,
                               SUM(f.Quantity) AS Quantity,
                               COUNT(*) AS OrderCount
                        FROM dbo.Fact_Sales f WITH (NOLOCK)
                        JOIN dbo.Dim_Manufacturer m WITH (NOLOCK) ON f.ManufacturerKey = m.ManufacturerKey
                        GROUP BY m.ManufacturerName, m.Country
                        ORDER BY Revenue DESC";
                    var rows = await db.QueryAsync(sql, commandTimeout: 60);
                    return new
                    {
                        id = "MDX-21",
                        title = "Doanh số sản phẩm bán ra theo Hãng sản xuất",
                        purpose = "Xác định thương hiệu/hãng sản xuất nào đang được thị trường ưa chuộng nhất.",
                        section = "Nhân sự & Khuyến mãi",
                        chartType = "bar",
                        dimension = "Hãng sản xuất (Dim_Manufacturer)",
                        measures = new[] { "Doanh thu", "Số lượng bán" },
                        relatedRoute = "product",
                        data = rows
                    };
                }
                case "MDX-22":
                {
                    const string sql = @"
                        SELECT p.Category AS Label,
                               ROUND(SUM(f.SalesAmount), 0) AS Revenue,
                               ROUND(SUM(f.ProfitAmount), 0) AS Profit,
                               ROUND(CASE WHEN SUM(f.SalesAmount) = 0 THEN 0 ELSE (SUM(f.ProfitAmount) / SUM(f.SalesAmount)) * 100 END, 2) AS ProfitMarginPct,
                               COUNT(*) AS OrderCount
                        FROM dbo.Fact_Sales f WITH (NOLOCK)
                        JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                        GROUP BY p.Category
                        ORDER BY Revenue DESC";
                    var rows = await db.QueryAsync(sql, commandTimeout: 60);
                    return new
                    {
                        id = "MDX-22",
                        title = "Tính Tỷ suất lợi nhuận (Profit Margin %) theo Danh mục",
                        purpose = "Dùng Calculated Member tính toán tỷ lệ Lợi nhuận / Doanh thu tức thời mà không cần can thiệp CSDL.",
                        section = "Nhân sự & Khuyến mãi",
                        chartType = "bar",
                        dimension = "Danh mục sản phẩm (Dim_Product)",
                        measures = new[] { "Doanh thu", "Lợi nhuận", "Tỷ suất LN %" },
                        relatedRoute = "sales",
                        data = rows
                    };
                }
                case "MDX-23":
                {
                    const string sql = @"
                        SELECT s.ServiceName AS Label,
                               ROUND(SUM(f.SalesAmount), 0) AS Revenue,
                               COUNT(*) AS OrderCount
                        FROM dbo.Fact_Sales f WITH (NOLOCK)
                        JOIN dbo.Dim_Service s WITH (NOLOCK) ON f.ServiceKey = s.ServiceKey
                        GROUP BY s.ServiceName
                        ORDER BY Revenue DESC";
                    var rows = await db.QueryAsync(sql, commandTimeout: 60);
                    return new
                    {
                        id = "MDX-23",
                        title = "Phân tích Doanh thu theo Gói Dịch vụ đính kèm",
                        purpose = "Đo lường doanh thu từ các gói bảo hành, dịch vụ vận chuyển và lắp đặt hoàn thiện.",
                        section = "Nhân sự & Khuyến mãi",
                        chartType = "bar",
                        dimension = "Gói dịch vụ (Dim_Service)",
                        measures = new[] { "Doanh thu" },
                        relatedRoute = "sales",
                        data = rows
                    };
                }
                case "MDX-24":
                {
                    const string sql = @"
                        SELECT sc.ChannelName AS Channel,
                               t.Quarter AS Quarter,
                               ROUND(SUM(f.SalesAmount), 0) AS Revenue,
                               COUNT(*) AS OrderCount
                        FROM dbo.Fact_Sales f WITH (NOLOCK)
                        JOIN dbo.Dim_SalesChannel sc WITH (NOLOCK) ON f.ChannelKey = sc.ChannelKey
                        JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                        GROUP BY sc.ChannelName, t.Quarter
                        ORDER BY sc.ChannelName, t.Quarter";
                    var rows = await db.QueryAsync(sql, commandTimeout: 60);
                    return new
                    {
                        id = "MDX-24",
                        title = "Ma trận tích chéo (CROSSJOIN) giữa Kênh bán hàng và Quý trong Năm",
                        purpose = "Đối chiếu diễn biến doanh thu của từng kênh bán lẻ/online qua các quý trong năm 2026.",
                        section = "Nhân sự & Khuyến mãi",
                        chartType = "matrix",
                        dimension = "Kênh bán hàng (Dim_SalesChannel) x Quý (Dim_Time)",
                        measures = new[] { "Doanh thu" },
                        relatedRoute = "cross-analysis",
                        data = rows
                    };
                }
                default:
                    return null;
            }
        }

        public async Task<object?> GetMultiDimAnalysisAsync(string measure, string rowDim, string colDim)
        {
            using var db = _connectionFactory.CreateConnection();

            // 1. Resolve Measure
            string measureSql;
            string measureName;
            string measureFormat = "vnd";

            var mKey = (measure ?? string.Empty).Trim().ToLowerInvariant();
            if (mKey.Contains("profit") || mKey.Contains("lợi nhuận"))
            {
                measureSql = "ROUND(ISNULL(SUM(f.ProfitAmount), 0), 0)";
                measureName = "Lợi nhuận";
            }
            else if (mKey.Contains("quantity") || mKey.Contains("số lượng") || mKey.Contains("sản lượng"))
            {
                measureSql = "ISNULL(SUM(f.Quantity), 0)";
                measureName = "Số lượng bán";
                measureFormat = "number";
            }
            else if (mKey.Contains("discount") || mKey.Contains("chiết khấu"))
            {
                measureSql = "ROUND(ISNULL(SUM(f.DiscountAmount), 0), 0)";
                measureName = "Tiền chiết khấu";
            }
            else if (mKey.Contains("shipping") || mKey.Contains("vận chuyển"))
            {
                measureSql = "ROUND(ISNULL(SUM(f.ShippingCost), 0), 0)";
                measureName = "Chi phí vận chuyển";
            }
            else
            {
                measureSql = "ROUND(ISNULL(SUM(f.SalesAmount), 0), 0)";
                measureName = "Doanh thu";
            }

            // 2. Resolve Dimension helper
            (string selectExpr, string joinClause, string label) ResolveDimension(string dimKey)
            {
                var d = (dimKey ?? string.Empty).Trim().ToLowerInvariant();
                if (d.Contains("channel") || d.Contains("kênh"))
                    return ("ISNULL(sc.ChannelName, N'Khác')", "JOIN dbo.Dim_SalesChannel sc WITH (NOLOCK) ON f.ChannelKey = sc.ChannelKey", "Kênh bán hàng");
                if (d.Contains("region") || d.Contains("khu vực"))
                    return ("ISNULL(loc.Region, N'Khác')", "JOIN dbo.Dim_Location loc WITH (NOLOCK) ON f.LocationKey = loc.LocationKey", "Khu vực");
                if (d.Contains("city") || d.Contains("thành phố") || d.Contains("tỉnh"))
                    return ("ISNULL(loc.City, N'Khác')", "JOIN dbo.Dim_Location loc WITH (NOLOCK) ON f.LocationKey = loc.LocationKey", "Thành phố");
                if (d.Contains("segment") || d.Contains("phân khúc"))
                    return ("ISNULL(cust.Segment, N'Khác')", "JOIN dbo.Dim_Customer cust WITH (NOLOCK) ON f.CustomerKey = cust.CustomerKey", "Phân khúc khách hàng");
                if (d.Contains("customer") || d.Contains("khách hàng"))
                    return ("ISNULL(cust.CustomerName, N'Khách vãng lai')", "JOIN dbo.Dim_Customer cust WITH (NOLOCK) ON f.CustomerKey = cust.CustomerKey", "Khách hàng");
                if (d.Contains("employee") || d.Contains("nhân viên"))
                    return ("ISNULL(emp.EmployeeName, N'Nhân viên')", "JOIN dbo.Dim_Employee emp WITH (NOLOCK) ON f.EmployeeKey = emp.EmployeeKey", "Nhân viên");
                if (d.Contains("manufacturer") || d.Contains("hãng"))
                    return ("ISNULL(p.Manufacturer, N'Khác')", "JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey", "Hãng sản xuất");
                if (d.Contains("sub category") || d.Contains("nhóm"))
                    return ("ISNULL(p.SubCategory, N'Khác')", "JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey", "Nhóm sản phẩm");
                if (d.Contains("year") || d.Contains("năm"))
                    return ("CAST(t.Year AS VARCHAR(10))", "JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey", "Năm");
                if (d.Contains("quarter") || d.Contains("quý"))
                    return ("CONCAT(N'Quý ', t.Quarter)", "JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey", "Quý");
                if (d.Contains("month") || d.Contains("tháng"))
                    return ("CONCAT(N'Tháng ', t.Month)", "JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey", "Tháng");
                
                // Mặc định: Category
                return ("ISNULL(p.Category, N'Khác')", "JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey", "Danh mục sản phẩm");
            }

            var rowInfo = ResolveDimension(rowDim);
            var colKeyRaw = (colDim ?? string.Empty).Trim().ToLowerInvariant();
            bool hasCol = !string.IsNullOrEmpty(colKeyRaw) && colKeyRaw != "none" && colKeyRaw != "không dùng" && colKeyRaw != "khong dung" && colKeyRaw != (rowDim ?? string.Empty).Trim().ToLowerInvariant();

            var joins = new HashSet<string>();
            joins.Add(rowInfo.joinClause);

            if (hasCol)
            {
                var colInfo = ResolveDimension(colDim);
                joins.Add(colInfo.joinClause);
                string joinText = string.Join("\n", joins);

                string sql = $@"
                    SELECT 
                        {rowInfo.selectExpr} AS RowKey,
                        {colInfo.selectExpr} AS ColKey,
                        {measureSql} AS MetricValue
                    FROM dbo.Fact_Sales f WITH (NOLOCK)
                    {joinText}
                    GROUP BY {rowInfo.selectExpr}, {colInfo.selectExpr}
                    ORDER BY RowKey, ColKey";

                var rawData = (await db.QueryAsync(sql, commandTimeout: 60)).ToList();

                var distinctRows = rawData.Select(x => (string)x.RowKey).Distinct().OrderBy(x => x).ToList();
                var distinctCols = rawData.Select(x => (string)x.ColKey).Distinct().OrderBy(x => x).ToList();

                // Tạo series cho ApexCharts (mỗi cột là 1 series)
                var seriesList = new List<object>();
                foreach (var col in distinctCols)
                {
                    var dataPoints = new List<double>();
                    foreach (var row in distinctRows)
                    {
                        var match = rawData.FirstOrDefault(x => (string)x.RowKey == row && (string)x.ColKey == col);
                        dataPoints.Add(match != null && match.MetricValue != null ? Convert.ToDouble(match.MetricValue) : 0);
                    }
                    seriesList.Add(new { name = col, data = dataPoints });
                }

                // Tạo rows ma trận cho Table
                var tableRows = new List<Dictionary<string, object>>();
                double grandTotal = 0;
                string topRowName = "";
                double topRowValue = -1;

                foreach (var row in distinctRows)
                {
                    var dict = new Dictionary<string, object> { ["RowName"] = row };
                    double rowSum = 0;
                    foreach (var col in distinctCols)
                    {
                        var match = rawData.FirstOrDefault(x => (string)x.RowKey == row && (string)x.ColKey == col);
                        double val = match != null && match.MetricValue != null ? Convert.ToDouble(match.MetricValue) : 0;
                        dict[col] = val;
                        rowSum += val;
                    }
                    dict["Total"] = rowSum;
                    tableRows.Add(dict);

                    grandTotal += rowSum;
                    if (rowSum > topRowValue)
                    {
                        topRowValue = rowSum;
                        topRowName = row;
                    }
                }

                // Cột table
                var tableCols = new List<object>
                {
                    new { key = "RowName", label = rowInfo.label }
                };
                foreach (var col in distinctCols)
                {
                    tableCols.Add(new { key = col, label = col, format = measureFormat });
                }
                tableCols.Add(new { key = "Total", label = "Tổng cả kỳ", format = measureFormat });

                return new
                {
                    isMultiDim = true,
                    measure = measureName,
                    measureFormat = measureFormat,
                    rowLabel = rowInfo.label,
                    colLabel = colInfo.label,
                    summary = new
                    {
                        total = grandTotal,
                        topRow = topRowName,
                        topRowValue = topRowValue,
                        rowCount = distinctRows.Count,
                        colCount = distinctCols.Count
                    },
                    chart = new
                    {
                        type = "groupedBar",
                        categories = distinctRows,
                        series = seriesList
                    },
                    table = new
                    {
                        columns = tableCols,
                        rows = tableRows
                    }
                };
            }
            else
            {
                // Phân tích 1 chiều
                string joinText = string.Join("\n", joins);
                string sql = $@"
                    SELECT TOP 15
                        {rowInfo.selectExpr} AS RowKey,
                        {measureSql} AS MetricValue
                    FROM dbo.Fact_Sales f WITH (NOLOCK)
                    {joinText}
                    GROUP BY {rowInfo.selectExpr}
                    ORDER BY MetricValue DESC";

                var rawData = (await db.QueryAsync(sql, commandTimeout: 60)).ToList();
                double grandTotal = 0;
                foreach (var x in rawData)
                {
                    if (x.MetricValue != null) grandTotal += Convert.ToDouble(x.MetricValue);
                }

                string topLabel = "";
                double topVal = 0;
                var items = new List<object>();
                foreach (var x in rawData)
                {
                    string lbl = (string)x.RowKey;
                    double val = x.MetricValue != null ? Convert.ToDouble(x.MetricValue) : 0;
                    if (string.IsNullOrEmpty(topLabel))
                    {
                        topLabel = lbl;
                        topVal = val;
                    }
                    double pct = grandTotal > 0 ? Math.Round((val / grandTotal) * 100, 1) : 0;
                    items.Add(new
                    {
                        label = lbl,
                        value = val,
                        percent = pct
                    });
                }

                var tableCols = new List<object>
                {
                    new { key = "label", label = rowInfo.label },
                    new { key = "value", label = measureName, format = measureFormat },
                    new { key = "percent", label = "Tỷ trọng (%)", format = "percent" }
                };

                return new
                {
                    isMultiDim = false,
                    measure = measureName,
                    measureFormat = measureFormat,
                    rowLabel = rowInfo.label,
                    colLabel = "Không chia cột",
                    summary = new
                    {
                        total = grandTotal,
                        topRow = !string.IsNullOrEmpty(topLabel) ? topLabel : "—",
                        topRowValue = topVal,
                        rowCount = items.Count,
                        colCount = 1
                    },
                    chart = new
                    {
                        type = "bar",
                        items = items
                    },
                    table = new
                    {
                        columns = tableCols,
                        rows = items
                    }
                };
            }
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

        // ==================== CHI TIẾT DOANH THU, LỢI NHUẬN TRONG MỘT NĂM ====================
        public async Task<(IEnumerable<dynamic> Categories, IEnumerable<dynamic> Regions, IEnumerable<dynamic> Products)> GetYearBreakdownAsync(int year, int top = 10)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT p.Category,
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue,
                       ROUND(SUM(f.ProfitAmount), 2) AS Profit,
                       SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                WHERE t.Year = @Year
                GROUP BY p.Category
                ORDER BY Revenue DESC;

                SELECT l.Region,
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue,
                       ROUND(SUM(f.ProfitAmount), 2) AS Profit,
                       SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
                WHERE t.Year = @Year
                GROUP BY l.Region
                ORDER BY Revenue DESC;

                SELECT TOP (@Top) p.ProductName, p.Category,
                       ROUND(SUM(f.SalesAmount), 2) AS Revenue,
                       ROUND(SUM(f.ProfitAmount), 2) AS Profit,
                       SUM(f.Quantity) AS Quantity
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                WHERE t.Year = @Year
                GROUP BY p.ProductKey, p.ProductName, p.Category
                ORDER BY Revenue DESC";
            using var multi = await db.QueryMultipleAsync(sql, new { Year = year, Top = top }, commandTimeout: 60);
            var categories = await multi.ReadAsync();
            var regions = await multi.ReadAsync();
            var products = await multi.ReadAsync();
            return (categories, regions, products);
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

        public async Task<dynamic?> GetCustomerDetailByNaturalIdAsync(string customerId)
        {
            using var db = _connectionFactory.CreateConnection();
            
            string customerSql = @"
                SELECT TOP 1 CustomerKey, CustomerID, CustomerName, Segment, SourceSystem, CreatedDate
                FROM dbo.Dim_Customer WITH (NOLOCK)
                WHERE CustomerID = @CustomerID";
            var customer = await db.QueryFirstOrDefaultAsync<dynamic>(customerSql, new { CustomerID = customerId });
            if (customer == null) return null;

            int customerKey = (int)customer.CustomerKey;

            string summarySql = @"
                SELECT 
                    ROUND(SUM(SalesAmount), 2) AS TotalRevenue,
                    ROUND(SUM(ProfitAmount), 2) AS TotalProfit,
                    COUNT(DISTINCT OrderID) AS TotalOrders,
                    SUM(Quantity) AS TotalQuantity
                FROM dbo.Fact_Sales WITH (NOLOCK)
                WHERE CustomerKey = @CustomerKey";
            var summary = await db.QueryFirstOrDefaultAsync<dynamic>(summarySql, new { CustomerKey = customerKey });

            string ordersSql = @"
                SELECT TOP 50
                    f.OrderID,
                    t.FullDate AS OrderDate,
                    p.ProductID,
                    p.ProductName,
                    f.Quantity,
                    ROUND(f.SalesAmount, 2) AS SalesAmount,
                    ROUND(f.ProfitAmount, 2) AS ProfitAmount,
                    f.SourceSystem
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                LEFT JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                LEFT JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                WHERE f.CustomerKey = @CustomerKey
                ORDER BY t.FullDate DESC, f.OrderID DESC";
            var orders = await db.QueryAsync<dynamic>(ordersSql, new { CustomerKey = customerKey });

            int totalOrdersCount = summary?.TotalOrders != null ? (int)summary.TotalOrders : orders.Count();

            var lineage = new
            {
                Source = (string)(customer.SourceSystem ?? "Excel_Superstore"),
                NaturalKey = $"CustomerID = {customer.CustomerID}",
                Dimension = "Dim_Customer",
                SurrogateKey = $"CustomerKey = {customer.CustomerKey}",
                Fact = "Fact_Sales",
                RelatedRecords = $"{totalOrdersCount} orders"
            };

            return new
            {
                Customer = customer,
                Summary = summary,
                Lineage = lineage,
                Orders = orders
            };
        }

        public async Task<dynamic?> GetOrderDetailByNaturalIdAsync(string orderId)
        {
            using var db = _connectionFactory.CreateConnection();
            
            string sql = @"
                SELECT 
                    f.OrderID,
                    c.CustomerKey,
                    c.CustomerID,
                    c.CustomerName,
                    c.Segment AS CustomerSegment,
                    p.ProductKey,
                    p.ProductID,
                    p.ProductName,
                    p.Category,
                    p.SubCategory,
                    t.FullDate AS OrderDate,
                    f.Quantity,
                    ROUND(f.SalesAmount, 2) AS SalesAmount,
                    ROUND(f.DiscountAmount, 2) AS DiscountAmount,
                    ROUND(f.ProfitAmount, 2) AS ProfitAmount,
                    f.SourceSystem
                FROM dbo.Fact_Sales f WITH (NOLOCK)
                LEFT JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
                LEFT JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
                LEFT JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
                WHERE f.OrderID = @OrderID";
            
            var items = await db.QueryAsync<dynamic>(sql, new { OrderID = orderId });
            if (!items.Any()) return null;

            var first = items.First();
            var lineage = new
            {
                Source = (string)(first.SourceSystem ?? "Excel_Superstore"),
                NaturalKey = $"OrderID = {first.OrderID}",
                Dimension = "Dim_Customer & Dim_Product",
                SurrogateKey = $"CustomerKey = {first.CustomerKey}, ProductKey = {first.ProductKey}",
                Fact = "Fact_Sales",
                RelatedRecords = $"{items.Count()} items in order"
            };

            return new
            {
                OrderHeader = new
                {
                    OrderID = first.OrderID,
                    CustomerID = first.CustomerID,
                    CustomerName = first.CustomerName,
                    CustomerSegment = first.CustomerSegment,
                    OrderDate = first.OrderDate,
                    ItemCount = items.Count(),
                    SourceSystem = (string)(first.SourceSystem ?? "Excel_Superstore")
                },
                Lineage = lineage,
                Items = items
            };
        }
    }
}







