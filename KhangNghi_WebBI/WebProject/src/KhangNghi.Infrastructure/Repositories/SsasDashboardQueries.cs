using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using KhangNghi.Core.DTOs;

namespace KhangNghi.Infrastructure.Repositories
{
    // Extension methods: reuse the existing MSOLAP executor without changing its interface.
    public static class SsasDashboardQueries
    {
        public static async Task<DashboardSummaryDto> GetDashboardSummaryFromCubeAsync(
            this ISsasRepository repository,
            int? year,
            string? region,
            bool includeSales,
            bool includeInventory,
            bool useSalesCube)
        {
            if (year.HasValue && (year.Value < 1900 || year.Value > 2100))
                throw new ArgumentException("Năm phải nằm trong khoảng 1900–2100.");

            region = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
            if (region != null && region != "Miền Bắc" && region != "Miền Trung" && region != "Miền Nam")
                throw new ArgumentException("Khu vực phải là Miền Bắc, Miền Trung hoặc Miền Nam.");

            var result = new DashboardSummaryDto();
            if (includeSales)
            {
                string cube = useSalesCube ? "Cube_Sales" : "KhangNghi_Cube";
                string mdx = @"
WITH
MEMBER [Measures].[Web Customer Count] AS
    COUNT(NONEMPTY(
        [Dim Customer].[Customer ID].[Customer ID].MEMBERS,
        { [Measures].[Order Count] }
    ))
SELECT
{
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Quantity],
    [Measures].[Order Count],
    [Measures].[Shipping Cost],
    [Measures].[Web Customer Count]
} ON COLUMNS
FROM [" + cube + "]" + Slicer(year, region) + ";";

                var rows = await repository.ExecuteMdxQueryAsync(mdx);
                if (rows.Count != 1)
                    throw new InvalidOperationException("SSAS không trả đúng một dòng KPI bán hàng.");

                var row = rows[0];
                result.TotalRevenue = Number(row, "Sales Amount");
                result.TotalProfit = Number(row, "Profit Amount");
                result.TotalQuantitySold = Integer(row, "Quantity");
                result.TotalOrders = Integer(row, "Order Count");
                result.TotalShippingCost = Number(row, "Shipping Cost");
                result.TotalCustomers = Integer(row, "Web Customer Count");
                result.ProfitMargin = result.TotalRevenue == 0 ? 0 :
                    Math.Round(100.0 * result.TotalProfit / result.TotalRevenue, 2,
                        MidpointRounding.AwayFromZero);
            }

            if (includeInventory)
            {
                string candidateDates = year.HasValue
                    ? "EXISTS([Dim Time].[Full Date].[Full Date].MEMBERS, " +
                      "{ [Dim Time].[Year].&[" + year.Value.ToString(CultureInfo.InvariantCulture) + "] })"
                    : "[Dim Time].[Full Date].[Full Date].MEMBERS";

                string mdx = @"
WITH
SET [Web Latest Inventory Date] AS
    TAIL(ORDER(
        NONEMPTY(" + candidateDates + @", { [Measures].[Fact Inventory Count] }),
        [Dim Time].[Full Date].CURRENTMEMBER.MEMBER_VALUE,
        BASC
    ), 1)
MEMBER [Measures].[Web Low Stock Alert Count] AS
    COUNT(FILTER(
        NONEMPTY(
            [Dim Product].[Product Key].[Product Key].MEMBERS
            * [Dim Warehouse].[Warehouse Key].[Warehouse Key].MEMBERS
            * [Dim Location].[Location Key].[Location Key].MEMBERS,
            { [Measures].[Fact Inventory Count] }
        ),
        NOT ISEMPTY([Measures].[Quantity On Hand])
        AND NOT ISEMPTY([Measures].[Reorder Level])
        AND [Measures].[Quantity On Hand] < [Measures].[Reorder Level]
    ))
SELECT
{
    [Measures].[Fact Inventory Count],
    [Measures].[Web Low Stock Alert Count]
} ON COLUMNS,
[Web Latest Inventory Date] ON ROWS
FROM [Cube_Inventory]" + Slicer(null, region) + ";";

                var rows = await repository.ExecuteMdxQueryAsync(mdx);
                if (rows.Count > 1)
                    throw new InvalidOperationException("SSAS trả nhiều hơn một snapshot tồn kho.");

                // No snapshot in the requested scope => no available inventory observation.
                // Report an error rather than silently presenting zero alerts as a verified result.
                if (rows.Count == 0)
                    throw new InvalidOperationException("Không có snapshot tồn kho trong phạm vi đã chọn.");

                result.LowStockAlertCount = Integer(rows[0], "Web Low Stock Alert Count");
            }

            return result;
        }

        private static string Slicer(int? year, string? region)
        {
            var members = new List<string>();
            if (year.HasValue)
                members.Add("[Dim Time].[Year].&[" + year.Value.ToString(CultureInfo.InvariantCulture) + "]");
            if (region != null)
                members.Add("[Dim Location].[Region].&[" + region + "]");
            return members.Count == 0 ? string.Empty : "\nWHERE (" + string.Join(", ", members) + ")";
        }

        private static object? Value(Dictionary<string, object?> row, string key)
        {
            if (!row.TryGetValue(key, out var value))
                throw new InvalidOperationException("Thiếu cột kết quả MDX: " + key);
            return value == DBNull.Value ? null : value;
        }

        private static double Number(Dictionary<string, object?> row, string key)
        {
            var value = Value(row, key);
            return value == null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static int Integer(Dictionary<string, object?> row, string key)
        {
            var value = Value(row, key);
            return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
