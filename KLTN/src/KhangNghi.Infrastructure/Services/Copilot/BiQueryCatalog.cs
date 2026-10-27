using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KhangNghi.Infrastructure.Repositories;

namespace KhangNghi.Infrastructure.Services.Copilot
{
    /// <summary>Tham số model truyền vào khi gọi tool run_bi_query.</summary>
    public sealed record BiQueryArgs(int? Top, int? Year, string? Region, string? ProductName, string? Id);

    /// <summary>Một truy vấn phân tích có sẵn mà AI Copilot (tầng 3) được phép gọi.</summary>
    public sealed class BiQueryDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Domain { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string[] Parameters { get; init; } = Array.Empty<string>();
        public Func<IDwhAnalyticsRepository, BiQueryArgs, Task<object?>> Execute { get; init; } = null!;
    }

    /// <summary>
    /// Danh mục truy vấn (Q01 - Q40 + tra cứu) cho LLM gọi qua tool calling.
    /// Model chỉ được chọn truy vấn trong danh mục này, không tự sinh SQL, nên không thể ghi/xóa dữ liệu.
    /// </summary>
    public static class BiQueryCatalog
    {
        // Quyền theo ma trận RBAC trong README: mỗi phòng ban chỉ thấy bảng Fact của mình.
        private static readonly Dictionary<string, string[]> DomainRoles = new()
        {
            ["Overview"] = new[] { "Admin", "Director" },
            ["Sales"] = new[] { "Admin", "Director", "SalesManager" },
            ["Purchase"] = new[] { "Admin", "Director", "PurchaseManager" },
            ["Inventory"] = new[] { "Admin", "Director", "InventoryManager" },
            ["Shipping"] = new[] { "Admin", "Director", "ShippingStaff" },
            ["Cross"] = new[] { "Admin", "Director" }
        };

        public static IReadOnlyList<BiQueryDefinition> All { get; } = new List<BiQueryDefinition>
        {
            // Tổng quan
            Q("SUMMARY", "Overview", "KPI tổng quan: tổng doanh thu, lợi nhuận, biên lợi nhuận, số đơn, số lượng bán, chi phí vận chuyển; lọc được theo năm và khu vực",
                async (r, a) => await r.GetDashboardSummaryAsync(a.Year, a.Region), "year", "region"),

            // Bán hàng (Q01 - Q20)
            Q("Q01", "Sales", "Doanh thu, lợi nhuận, số lượng bán theo từng năm", async (r, a) => await r.Q01_RevenueByYearAsync()),
            Q("Q02", "Sales", "Doanh thu theo quý (có thể lọc một năm)", async (r, a) => await r.Q02_RevenueByQuarterAsync(a.Year), "year"),
            Q("Q03", "Sales", "Tháng có doanh thu cao nhất", async (r, a) => await r.Q03_PeakMonthRevenueAsync()),
            Q("Q04", "Sales", "Quý có lợi nhuận cao nhất", async (r, a) => await r.Q04_PeakQuarterProfitAsync()),
            Q("Q05", "Sales", "Số lượng bán theo thời gian (xu hướng theo tháng)", async (r, a) => await r.Q05_QuantitySoldOverTimeAsync()),
            Q("Q06", "Sales", "Top sản phẩm theo doanh thu", async (r, a) => await r.Q06_TopProductsByRevenueAsync(Top(a)), "top"),
            Q("Q07", "Sales", "Top sản phẩm theo lợi nhuận", async (r, a) => await r.Q07_TopProductsByProfitAsync(Top(a)), "top"),
            Q("Q08", "Sales", "Doanh thu, lợi nhuận theo nhóm hàng (Category)", async (r, a) => await r.Q08_RevenueByCategoryAsync()),
            Q("Q09", "Sales", "Số lượng bán theo nhóm hàng con (SubCategory)", async (r, a) => await r.Q09_QuantityBySubCategoryAsync()),
            Q("Q10", "Sales", "Sản phẩm chiết khấu cao nhưng lợi nhuận thấp", async (r, a) => await r.Q10_HighDiscountLowProfitProductsAsync(Top(a)), "top"),
            Q("Q11", "Sales", "Top khách hàng theo doanh thu", async (r, a) => await r.Q11_TopCustomersByRevenueAsync(Top(a)), "top"),
            Q("Q12", "Sales", "Top khách hàng theo lợi nhuận", async (r, a) => await r.Q12_TopCustomersByProfitAsync(Top(a)), "top"),
            Q("Q13", "Sales", "Doanh thu theo phân khúc khách hàng (Segment)", async (r, a) => await r.Q13_RevenueBySegmentAsync()),
            Q("Q14", "Sales", "Số lượng mua theo phân khúc khách hàng", async (r, a) => await r.Q14_QuantityBySegmentAsync()),
            Q("Q15", "Sales", "Doanh thu theo khu vực (Region)", async (r, a) => await r.Q15_RevenueByRegionAsync()),
            Q("Q16", "Sales", "Lợi nhuận theo quốc gia", async (r, a) => await r.Q16_ProfitByCountryAsync()),
            Q("Q17", "Sales", "Top thành phố theo số lượng bán", async (r, a) => await r.Q17_TopCitiesByQuantityAsync(Top(a)), "top"),
            Q("Q18", "Sales", "Xu hướng doanh thu theo khu vực qua các năm", async (r, a) => await r.Q18_RegionalRevenueTrendAsync()),
            Q("Q19", "Sales", "Hãng sản xuất có doanh thu cao nhất", async (r, a) => await r.Q19_TopManufacturersByRevenueAsync()),
            Q("Q20", "Sales", "Hãng sản xuất có lợi nhuận cao nhất", async (r, a) => await r.Q20_TopManufacturersByProfitAsync()),
            Q("CUSTOMER", "Sales", "Tra cứu chi tiết một khách hàng theo mã (VD: KN-CUS-0959)", async (r, a) => await r.GetCustomerDetailByNaturalIdAsync(SaleId(a)), "id"),
            Q("ORDER", "Sales", "Tra cứu chi tiết một đơn hàng theo mã đơn", async (r, a) => await r.GetOrderDetailByNaturalIdAsync(SaleId(a)), "id"),

            // Mua hàng (Q21 - Q25)
            Q("Q21", "Purchase", "Nhà cung cấp có tổng giá trị nhập hàng lớn nhất", async (r, a) => await r.Q21_TopSuppliersByAmountAsync()),
            Q("Q22", "Purchase", "Nhà cung cấp có số lượng nhập lớn nhất", async (r, a) => await r.Q22_TopSuppliersByQuantityAsync()),
            Q("Q23", "Purchase", "Top sản phẩm được nhập nhiều nhất", async (r, a) => await r.Q23_TopPurchasedProductsAsync(Top(a)), "top"),
            Q("Q24", "Purchase", "Xu hướng giá nhập theo thời gian (có thể lọc theo tên sản phẩm)", async (r, a) => await r.Q24_PurchasePriceTrendAsync(a.ProductName), "product_name"),
            Q("Q25", "Purchase", "Giá trị nhập hàng theo khu vực", async (r, a) => await r.Q25_ImportValueByRegionAsync()),
            Q("PURCHASE_ORDERS", "Purchase", "Tổng hợp các phiếu nhập hàng (có thể lọc năm)", async (r, a) => await r.GetPurchaseOrdersSummaryAsync(a.Year), "year"),
            Q("PURCHASE_CATEGORY", "Purchase", "Chi phí nhập hàng theo nhóm hàng (có thể lọc năm)", async (r, a) => await r.GetPurchaseCategorySpendAsync(a.Year), "year"),

            // Tồn kho (Q26 - Q30)
            Q("Q26", "Inventory", "Sản phẩm tồn kho nhiều nhất", async (r, a) => await r.Q26_HighestStockProductsAsync(Top(a)), "top"),
            Q("Q27", "Inventory", "Sản phẩm tồn kho ít nhất", async (r, a) => await r.Q27_LowestStockProductsAsync(Top(a)), "top"),
            Q("Q28", "Inventory", "Cảnh báo sản phẩm tồn kho dưới mức đặt hàng lại", async (r, a) => await r.Q28_ReorderLevelAlertsAsync(Top(a)), "top"),
            Q("Q29", "Inventory", "Tồn kho theo khu vực", async (r, a) => await r.Q29_StockByRegionAsync()),
            Q("Q30", "Inventory", "Biến động nhập - xuất - tồn theo thời gian", async (r, a) => await r.Q30_InventoryMovementOverTimeAsync()),

            // Vận chuyển (Q31 - Q37)
            Q("Q31", "Shipping", "Số đơn theo đơn vị / phương thức vận chuyển", async (r, a) => await r.Q31_OrdersByShipperAsync()),
            Q("Q32", "Shipping", "Tổng chi phí vận chuyển theo đơn vị vận chuyển", async (r, a) => await r.Q32_ShippingCostByShipperAsync()),
            Q("Q33", "Shipping", "Chi phí vận chuyển trung bình mỗi đơn theo đơn vị vận chuyển", async (r, a) => await r.Q33_AvgShippingCostByShipperAsync()),
            Q("Q34", "Shipping", "Đơn vị vận chuyển giao hàng nhanh nhất", async (r, a) => await r.Q34_FastestDeliveryShippersAsync()),
            Q("Q35", "Shipping", "Đơn vị vận chuyển có số ngày trễ nhiều nhất", async (r, a) => await r.Q35_HighestLateDaysShippersAsync()),
            Q("Q36", "Shipping", "Chi phí vận chuyển theo khu vực", async (r, a) => await r.Q36_ShippingCostByRegionAsync()),
            Q("Q37", "Shipping", "Tỷ lệ giao hàng trễ theo khu vực", async (r, a) => await r.Q37_LateDeliveryRateByRegionAsync()),

            // Phân tích chéo (Q38 - Q40)
            Q("Q38", "Cross", "Mặt hàng sắp hết hàng: số ngày tồn kho (tồn / bán TB ngày) thấp nhất, dưới 3 ngày", async (r, a) => await r.Q38_HighSalesLowStockRiskAsync(Top(a)), "top"),
            Q("Q39", "Cross", "Mặt hàng tồn quá nhiều so với sức bán: số ngày tồn kho cao nhất, trên 7 ngày", async (r, a) => await r.Q39_HighPurchaseLowSalesRiskAsync(Top(a)), "top"),
            Q("Q40", "Cross", "Khu vực tốn phí giao hàng nhiều nhất trên mỗi đồng doanh thu", async (r, a) => await r.Q40_RevenueVsShippingEfficiencyAsync())
        };

        public static IReadOnlyList<BiQueryDefinition> ForRole(string? role) =>
            All.Where(q => DomainRoles[q.Domain].Contains(role ?? string.Empty, StringComparer.OrdinalIgnoreCase)).ToList();

        private static BiQueryDefinition Q(string id, string domain, string description,
            Func<IDwhAnalyticsRepository, BiQueryArgs, Task<object?>> execute, params string[] parameters) =>
            new() { Id = id, Domain = domain, Description = description, Execute = execute, Parameters = parameters };

        private static int Top(BiQueryArgs a) => Math.Clamp(a.Top ?? 10, 1, 50);

        // Mã tự nhiên bên bán hàng được ETL gắn tiền tố SALE_ trong DWH
        private static string SaleId(BiQueryArgs a)
        {
            var id = (a.Id ?? string.Empty).Trim();
            return id.StartsWith("SALE_", StringComparison.OrdinalIgnoreCase) ? id : "SALE_" + id;
        }
    }
}
