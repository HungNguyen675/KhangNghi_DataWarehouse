using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KhangNghi.Infrastructure.Repositories
{
    // Chi doc SSAS. Khong fallback sang SQL khi truy van MDX that bai.
    public static class SsasSalesChartQueries
    {
        public static async Task<List<Dictionary<string, object?>>> GetSalesChartFromCubeAsync(
            this ISsasRepository repository, string group, bool useSalesCube, int? year = null)
        {
            if (year.HasValue && (year.Value < 1900 || year.Value > 2100))
                throw new ArgumentException("Năm phải nằm trong khoảng 1900–2100.");

            string cube = useSalesCube ? "Cube_Sales" : "KhangNghi_Cube";
            string years = year.HasValue
                ? "{[Dim Time].[Year].&[" + year.Value.ToString(CultureInfo.InvariantCulture) + "]}"
                : "[Dim Time].[Year].[Year].MEMBERS";
            bool timeGroup = group == "year" || group == "month" || group == "quarter";
            string axis = group switch
            {
                "year" => years,
                "month" => "(" + years + " * [Dim Time].[Month].[Month].MEMBERS)",
                "quarter" => "(" + years + " * [Dim Time].[Quarter].[Quarter].MEMBERS)",
                "category" => "[Dim Product].[Category].[Category].MEMBERS",
                "sub-category" =>
    @"(
        [Dim Product].[Category].[Category].MEMBERS
        *
        [Dim Product].[Sub Category].[Sub Category].MEMBERS
    )",
                "region" => "[Dim Location].[Region].[Region].MEMBERS",
                "city" => @"(
    [Dim Location].[Region].[Region].MEMBERS *
    [Dim Location].[City].[City].MEMBERS
)",
"product" => @"(
    [Dim Product].[Product Key].[Product Key].MEMBERS *
    [Dim Product].[Product Name].[Product Name].MEMBERS *
    [Dim Product].[Category].[Category].MEMBERS *
    [Dim Product].[Sub Category].[Sub Category].MEMBERS
)",
                _ => throw new ArgumentException("Nhóm biểu đồ không hợp lệ.")
            };
            string slicer = !timeGroup && year.HasValue ? " WHERE (" + years.Trim('{', '}') + ")" : "";
            string calculatedMember = group == "product"
    ? @"
WITH
MEMBER [Measures].[Product Key Value] AS
    [Dim Product].[Product Key].CURRENTMEMBER.PROPERTIES(""KEY0"")
"
    : "";

string extraMeasure = group == "product"
    ? ", [Measures].[Product Key Value]"
    : "";

string mdx = $@"
{calculatedMember}
SELECT
{{
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Quantity]
    {extraMeasure}
}} ON COLUMNS,

NONEMPTY(
    {axis},
    {{[Measures].[Sales Amount]}}
) ON ROWS

FROM [{cube}]{slicer};";
            var source = await repository.ExecuteMdxQueryAsync(mdx);
            var result = new List<Dictionary<string, object?>>();
            foreach (var row in source)
            {
                double revenue = Number(row, "Sales Amount");
                double profit = Number(row, "Profit Amount");
                var item = new Dictionary<string, object?>
                {
                    ["Revenue"] = Math.Round(revenue, 2, MidpointRounding.AwayFromZero),
                    ["Profit"] = Math.Round(profit, 2, MidpointRounding.AwayFromZero),
                    ["Quantity"] = Convert.ToInt64(Number(row, "Quantity"))
                };
                if (timeGroup) item["Year"] = Integer(row, "Year");
                if (group == "month") item["Month"] = Integer(row, "Month");
                if (group == "quarter")
                {
                    int quarter = Integer(row, "Quarter");
                    item["Quarter"] = quarter;
                    item["Period"] = $"Q{quarter}/{item["Year"]}";
                }
                if (group == "category"
    || group == "product"
    || group == "sub-category")
{
    item["Category"] = Text(row, "Category");
}

if (group == "sub-category")
{
    item["SubCategory"] = Text(row, "Sub Category");
    item["TotalQuantity"] = Convert.ToInt64(
        Number(row, "Quantity"));
}
                if (group == "category") item["ProfitMargin"] = revenue == 0 ? 0 :
                    Math.Round(100.0 * profit / revenue, 2, MidpointRounding.AwayFromZero);
                if (group == "region")
{
    item["Region"] = Text(row, "Region");
}    
                if (group == "city")
{
    item["City"] = Text(row, "City");
    item["Region"] = Text(row, "Region");
    item["TotalQuantity"] = Convert.ToInt64(
        Number(row, "Quantity"));
}
                if (group == "product")
{
    item["ProductKey"] = Convert.ToInt64(
        Value(row, "Product Key Value"),
        CultureInfo.InvariantCulture);

    item["ProductName"] = Text(row, "Product Name");
    item["SubCategory"] = Text(row, "Sub Category");
}
                result.Add(item);
            }
            return timeGroup
                ? result.OrderBy(r => Convert.ToInt32(r["Year"]))
                    .ThenBy(r => group == "month" ? Convert.ToInt32(r["Month"]) :
                        group == "quarter" ? Convert.ToInt32(r["Quarter"]) : 0).ToList()
                : result.OrderByDescending(r => Convert.ToDouble(r["Revenue"], CultureInfo.InvariantCulture)).ToList();
        }

        public static async Task<object> GetYearBreakdownFromCubeAsync(
            this ISsasRepository repository, bool useSalesCube, int year)
        {
            // Moi truy van mo ket noi rieng; thuc hien tuan tu de de kiem tra log.
            var categories = await repository.GetSalesChartFromCubeAsync("category", useSalesCube, year);
            var regions = await repository.GetSalesChartFromCubeAsync("region", useSalesCube, year);
            var products = await repository.GetSalesChartFromCubeAsync("product", useSalesCube, year);
            return new { Categories = categories, Regions = regions, Products = products.Take(10).ToList() };
        }

        private static object? Value(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var value)) return value;
            throw new InvalidOperationException("SSAS không trả về cột bắt buộc: " + key);
        }
        private static double Number(Dictionary<string, object?> row, string key)
        {
            var value = Value(row, key);
            return value == null || value == DBNull.Value ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        private static int Integer(Dictionary<string, object?> row, string key) =>
            Convert.ToInt32(Value(row, key), CultureInfo.InvariantCulture);
        private static string Text(Dictionary<string, object?> row, string key) =>
            Convert.ToString(Value(row, key), CultureInfo.InvariantCulture) ?? "Chưa xác định";
    }
}
