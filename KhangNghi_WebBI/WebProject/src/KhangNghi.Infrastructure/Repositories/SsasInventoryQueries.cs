using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KhangNghi.Infrastructure.Repositories
{
    public static class SsasInventoryQueries
    {
        public static async Task<object> GetStockCoverDetailFromCubeAsync(
    this ISsasRepository repository,
    int productKey)
{
    if (productKey <= 0)
        throw new ArgumentException("Khóa sản phẩm không hợp lệ.");

    string productMember =
        "[Dim Product].[Product Key].&[" +
        productKey.ToString(CultureInfo.InvariantCulture) + "]";

    // Tồn kho sử dụng cách tổng hợp của measure trong SSAS.
    // Ngày chốt lấy từ ngày cuối có dữ liệu tồn của từng kho/địa điểm.
    string stockMdx = $@"
WITH
MEMBER [Measures].[Snapshot Time Key] AS
    MAX(
        NONEMPTY(
            [Dim Time].[Full Date].[Full Date].MEMBERS,
            {{[Measures].[Quantity On Hand]}}
        ),
        VAL(
            FORMAT(
                CDATE(
                    [Dim Time].[Full Date].CURRENTMEMBER.PROPERTIES('KEY0')
                ),
                ""yyyyMMdd""
            )
        )
    )

SELECT
{{
    [Measures].[Quantity On Hand],
    [Measures].[Reorder Level],
    [Measures].[Snapshot Time Key]
}} ON COLUMNS,

NONEMPTY(
    [Dim Warehouse].[Warehouse Key].[Warehouse Key].MEMBERS
    *
    [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS
    *
    [Dim Location].[Location Key].[Location Key].MEMBERS
    *
    [Dim Location].[City].[City].MEMBERS,
    {{
        [Measures].[Quantity On Hand],
        [Measures].[Reorder Level]
    }}
) ON ROWS

FROM [KhangNghi_Cube]
WHERE ({productMember});";

    // Xác định tháng bán hàng mới nhất của toàn bộ dữ liệu.
    // Không lọc sản phẩm ở truy vấn này.
    const string allMonthsMdx = @"
SELECT
    {[Measures].[Quantity]} ON COLUMNS,

NONEMPTY(
    [Dim Time].[Year].[Year].MEMBERS
    *
    [Dim Time].[Month].[Month].MEMBERS,
    {[Measures].[Quantity]}
) ON ROWS

FROM [KhangNghi_Cube];";

    // Lấy lượng bán theo tháng của sản phẩm đang xem.
    string productSalesMdx = $@"
SELECT
    {{[Measures].[Quantity]}} ON COLUMNS,

NONEMPTY(
    [Dim Time].[Year].[Year].MEMBERS
    *
    [Dim Time].[Month].[Month].MEMBERS,
    {{[Measures].[Quantity]}}
) ON ROWS

FROM [KhangNghi_Cube]
WHERE ({productMember});";

    var stockRows = await repository.ExecuteMdxQueryAsync(stockMdx);
    var allMonths = await repository.ExecuteMdxQueryAsync(allMonthsMdx);
    var salesRows = await repository.ExecuteMdxQueryAsync(productSalesMdx);

    double Number(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
            throw new InvalidOperationException(
                "SSAS thiếu cột bắt buộc: " + key);

        return value == null || value == DBNull.Value
            ? 0
            : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    string? Text(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) ||
            value == null || value == DBNull.Value)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    int YearMonth(Dictionary<string, object?> row)
    {
        int year = checked((int)Number(row, "Year"));
        int month = checked((int)Number(row, "Month"));

        if (year < 1900 || year > 2100 || month < 1 || month > 12)
            throw new InvalidOperationException(
                "SSAS trả về năm/tháng không hợp lệ.");

        return year * 12 + month;
    }

    var stock = stockRows.Select(row =>
    {
        string? snapshotDate = null;
        string timeKey = Number(row, "Snapshot Time Key")
            .ToString("0", CultureInfo.InvariantCulture);

        if (DateTime.TryParseExact(
            timeKey,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            snapshotDate = date.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return new Dictionary<string, object?>
        {
            ["Warehouse"] = Text(row, "Warehouse Name"),
            ["City"] = Text(row, "City"),
            ["SnapshotDate"] = snapshotDate,
            ["QuantityOnHand"] = Number(row, "Quantity On Hand"),
            ["ReorderLevel"] = Number(row, "Reorder Level")
        };
    }).ToList();

    var sales = new List<Dictionary<string, object?>>();

    if (allMonths.Count > 0)
    {
        int lastMonth = allMonths.Max(row => YearMonth(row));

        var quantityByMonth = salesRows
            .GroupBy(row => YearMonth(row))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => Number(row, "Quantity")));

        // Luôn trả đủ ba tháng, kể cả tháng không bán.
        for (int ym = lastMonth - 2; ym <= lastMonth; ym++)
        {
            sales.Add(new Dictionary<string, object?>
            {
                ["Year"] = (ym - 1) / 12,
                ["Month"] = (ym - 1) % 12 + 1,
                ["Quantity"] = quantityByMonth.TryGetValue(
                    ym, out var quantity) ? quantity : 0
            });
        }
    }

    return new { Stock = stock, Sales = sales };
}
        public static async Task<List<Dictionary<string, object?>>>
            GetInventoryByRegionFromCubeAsync(
                this ISsasRepository repository,
                bool useInventoryCube,
                 int? year = null)
        {
            string cube = useInventoryCube
                ? "Cube_Inventory"
                : "KhangNghi_Cube";
if (year.HasValue &&
    (year.Value < 1900 || year.Value > 2100))
{
    throw new ArgumentException(
        "Năm phải nằm trong khoảng 1900–2100.");
}

string slicer = year.HasValue
    ? " WHERE ([Dim Time].[Year].&[" +
      year.Value.ToString(CultureInfo.InvariantCulture) + "])"
    : "";
            string mdx = $@"
SELECT
{{
    [Measures].[Quantity On Hand],
    [Measures].[Quantity In],
    [Measures].[Quantity Out]
}} ON COLUMNS,

NONEMPTY(
    [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS,
    {{
        [Measures].[Quantity On Hand],
        [Measures].[Quantity In],
        [Measures].[Quantity Out]
    }}
) ON ROWS

FROM [{cube}]{slicer};";

            var rows = await repository.ExecuteMdxQueryAsync(mdx);

            return rows.Select(row => new Dictionary<string, object?>
            {
                ["Region"] = row["Warehouse Name"],
                ["TotalOnHand"] = Convert.ToInt64(
                    row["Quantity On Hand"],
                    CultureInfo.InvariantCulture),
                ["TotalIn"] = Convert.ToInt64(
                    row["Quantity In"],
                    CultureInfo.InvariantCulture),
                ["TotalOut"] = Convert.ToInt64(
                    row["Quantity Out"],
                    CultureInfo.InvariantCulture)
            })
            .OrderByDescending(row => Convert.ToInt64(
                row["TotalOnHand"],
                CultureInfo.InvariantCulture))
            .ToList();
        }
        public static async Task<List<Dictionary<string, object?>>>
    GetInventoryMovementFromCubeAsync(
        this ISsasRepository repository,
        bool useInventoryCube)
{
    string cube = useInventoryCube
        ? "Cube_Inventory"
        : "KhangNghi_Cube";

    string mdx = $@"
SELECT
{{
    [Measures].[Quantity In],
    [Measures].[Quantity Out],
    [Measures].[Quantity On Hand]
}} ON COLUMNS,

NONEMPTY(
    [Dim Time].[Year].[Year].MEMBERS
    *
    [Dim Time].[Month].[Month].MEMBERS,
    {{
        [Measures].[Quantity In],
        [Measures].[Quantity Out],
        [Measures].[Quantity On Hand]
    }}
) ON ROWS

FROM [{cube}];";

    var rows = await repository.ExecuteMdxQueryAsync(mdx);

    return rows.Select(row =>
    {
        int year = Convert.ToInt32(
            row["Year"], CultureInfo.InvariantCulture);

        int month = Convert.ToInt32(
            row["Month"], CultureInfo.InvariantCulture);

        return new Dictionary<string, object?>
        {
            ["Year"] = year,
            ["Month"] = month,
            ["Period"] = $"Tháng {month}/{year}",
            ["TotalQuantityIn"] = Convert.ToInt64(
                row["Quantity In"],
                CultureInfo.InvariantCulture),
            ["TotalQuantityOut"] = Convert.ToInt64(
                row["Quantity Out"],
                CultureInfo.InvariantCulture),
            ["TotalQuantityOnHand"] = Convert.ToInt64(
                row["Quantity On Hand"],
                CultureInfo.InvariantCulture)
        };
    })
    .OrderBy(row => Convert.ToInt32(row["Year"]))
    .ThenBy(row => Convert.ToInt32(row["Month"]))
    .ToList();
}
public static async Task<List<Dictionary<string, object?>>>
    GetStockCoverFromCubeAsync(
        this ISsasRepository repository,
        int top = 500,
        int? year = null)
{
    if (top < 1 || top > 5000)
        throw new ArgumentException(
            "Số sản phẩm phải nằm trong khoảng 1–5000.");

    // Cube tổng có cả measure tồn kho và bán hàng.
    const string cube = "KhangNghi_Cube";
if (year.HasValue &&
    (year.Value < 1900 || year.Value > 2100))
{
    throw new ArgumentException(
        "Năm phải nằm trong khoảng 1900–2100.");
}

string stockSlicer = year.HasValue
    ? " WHERE ([Dim Time].[Year].&[" +
      year.Value.ToString(CultureInfo.InvariantCulture) + "])"
    : "";
    string productAxis = @"
        [Dim Product].[Product Key].[Product Key].MEMBERS
        *
        [Dim Product].[Product Name].[Product Name].MEMBERS
        *
        [Dim Product].[Category].[Category].MEMBERS";

    string stockMdx = $@"
WITH
MEMBER [Measures].[Stock Product Key] AS
    [Dim Product].[Product Key].CURRENTMEMBER.PROPERTIES('KEY0')

SELECT
{{
    [Measures].[Stock Product Key],
    [Measures].[Quantity On Hand],
    [Measures].[Reorder Level]
}} ON COLUMNS,

NONEMPTY(
    {productAxis},
    {{
        [Measures].[Quantity On Hand],
        [Measures].[Reorder Level]
    }}
) ON ROWS

FROM [{cube}]{stockSlicer};";

    string salesMdx = $@"
WITH
MEMBER [Measures].[Stock Product Key] AS
    [Dim Product].[Product Key].CURRENTMEMBER.PROPERTIES('KEY0')

SELECT
{{
    [Measures].[Stock Product Key],
    [Measures].[Quantity]
}} ON COLUMNS,

NONEMPTY(
    ({productAxis})
    *
    [Dim Time].[Year].[Year].MEMBERS
    *
    [Dim Time].[Month].[Month].MEMBERS,
    {{[Measures].[Quantity]}}
) ON ROWS

FROM [{cube}];";

    var stocks = await repository.ExecuteMdxQueryAsync(stockMdx);
    var sales = await repository.ExecuteMdxQueryAsync(salesMdx);
    // Loại dữ liệu bán sau năm đang xem.
// Vẫn giữ năm trước để cửa sổ 3 tháng có thể đi qua năm.
if (year.HasValue)
{
    sales = sales.Where(row =>
        Convert.ToInt32(
            row["Year"], CultureInfo.InvariantCulture)
        <= year.Value).ToList();
}

    double Number(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
            throw new InvalidOperationException(
                "SSAS thiếu cột bắt buộc: " + key);

        return value == null || value == DBNull.Value
            ? 0
            : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    int ProductKey(Dictionary<string, object?> row)
    {
        if (!row.TryGetValue("Stock Product Key", out var value) ||
            value == null || value == DBNull.Value)
        {
            throw new InvalidOperationException(
                "SSAS không trả về khóa sản phẩm.");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    int YearMonth(Dictionary<string, object?> row) =>
        checked((int)Number(row, "Year") * 12
            + (int)Number(row, "Month"));

    int? lastMonth = sales.Count == 0
        ? null
        : sales.Max(row => YearMonth(row));

    var recentSales = sales
        .Where(row => lastMonth.HasValue &&
            YearMonth(row) > lastMonth.Value - 3)
        .GroupBy(row => ProductKey(row))
        .ToDictionary(
            group => group.Key,
            group => group.Sum(row => Number(row, "Quantity")) / 3.0);

    var stockByProduct = stocks.ToDictionary(
        row => ProductKey(row));

    // Có thể có sản phẩm có bán nhưng chưa có bản ghi tồn.
    var products = stocks.Concat(sales)
        .GroupBy(row => ProductKey(row))
        .Select(group => group.First());

    var calculated = products.Select(product =>
    {
        int key = ProductKey(product);

        double onHand = stockByProduct.TryGetValue(key, out var stock)
            ? Number(stock, "Quantity On Hand")
            : 0;

        double reorder = stock != null
            ? Number(stock, "Reorder Level")
            : 0;

        double avgMonthly = recentSales.TryGetValue(key, out var avg)
            ? avg
            : 0;

        double? cover = avgMonthly > 0
            ? onHand * 30.0 / avgMonthly
            : null;

        // Giữ thứ tự phân loại của truy vấn SQL hiện tại.
        string status =
            !cover.HasValue || cover.Value > 7
                ? "Tồn thừa"
                : cover.Value < 3 || onHand < reorder
                    ? "Sắp hết"
                    : "An toàn";

        return new
        {
            Product = product,
            Key = key,
            OnHand = onHand,
            Reorder = reorder,
            AvgMonthly = avgMonthly,
            Cover = cover,
            Status = status
        };
    })
    .Where(item => item.OnHand > 0 || item.AvgMonthly > 0)
    .OrderBy(item => item.Cover ?? double.MaxValue)
    .ThenByDescending(item => item.AvgMonthly)
    .Take(top);

    return calculated.Select(item => new Dictionary<string, object?>
    {
        ["ProductKey"] = item.Key,
        ["ProductName"] = item.Product["Product Name"],
        ["Category"] = item.Product["Category"],
        ["QuantityOnHand"] = item.OnHand,
        ["ReorderLevel"] = item.Reorder,
        ["AvgMonthlySold"] = Math.Round(
            item.AvgMonthly, 0, MidpointRounding.AwayFromZero),
        ["DaysOfCover"] = item.Cover.HasValue
            ? (double?)(Math.Floor(item.Cover.Value * 10) / 10.0)
            : null,
        ["StockStatus"] = item.Status
    }).ToList();
}
    }
}