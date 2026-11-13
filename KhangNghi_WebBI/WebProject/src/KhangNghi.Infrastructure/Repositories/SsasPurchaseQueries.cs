using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KhangNghi.Infrastructure.Repositories
{
    public static class SsasPurchaseQueries
    {
        public static async Task<List<Dictionary<string, object?>>>
            GetPurchaseOrdersFromCubeAsync(
                this ISsasRepository repository,
                bool useInventoryCube,
                int? year = null)
        {
            if (year.HasValue &&
                (year.Value < 1900 || year.Value > 2100))
            {
                throw new ArgumentException(
                    "Năm phải nằm trong khoảng 1900–2100.");
            }

            string cube = useInventoryCube
                ? "Cube_Inventory"
                : "KhangNghi_Cube";

            string slicer = year.HasValue
                ? " WHERE ([Dim Time].[Year].&[" +
                  year.Value.ToString(CultureInfo.InvariantCulture) + "])"
                : "";

            string mdx = $@"
WITH
MEMBER [Measures].[Order Date Value] AS
    [Dim Time].[Full Date].CURRENTMEMBER.MEMBER_VALUE

SELECT
{{
    [Measures].[Fact Purchase Count],
    [Measures].[Purchase Quantity],
    [Measures].[Total Cost],
    [Measures].[Order Date Value]
}} ON COLUMNS,

NONEMPTY(
    [Dim Purchase Order].[Purchase Order ID]
        .[Purchase Order ID].MEMBERS
    *
    [Dim Time].[Full Date].[Full Date].MEMBERS
    *
    [Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS
    *
    [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS,
    {{[Measures].[Total Cost]}}
) ON ROWS

FROM [{cube}]{slicer};";

            var rows = await repository.ExecuteMdxQueryAsync(mdx);
            var result = new List<Dictionary<string, object?>>();

            foreach (var row in rows)
            {
                object Required(string key)
                {
                    if (!row.TryGetValue(key, out var value) ||
                        value == null || value == DBNull.Value)
                    {
                        throw new InvalidOperationException(
                            "SSAS thiếu dữ liệu bắt buộc: " + key);
                    }

                    return value;
                }

                DateTime orderDate = Convert.ToDateTime(
                    Required("Order Date Value"),
                    CultureInfo.InvariantCulture);

                result.Add(new Dictionary<string, object?>
                {
                    ["PurchaseOrderID"] = Convert.ToString(
                        Required("Purchase Order ID")),
                    ["OrderDate"] = orderDate.ToString(
                        "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["SupplierName"] = Convert.ToString(
                        Required("Supplier Name")),
                    ["WarehouseName"] = Convert.ToString(
                        Required("Warehouse Name")),
                    ["LineItemCount"] = Convert.ToInt32(
                        Required("Fact Purchase Count"),
                        CultureInfo.InvariantCulture),
                    ["TotalQuantity"] = Convert.ToInt32(
                        Required("Purchase Quantity"),
                        CultureInfo.InvariantCulture),
                    ["TotalCost"] = Convert.ToDouble(
                        Required("Total Cost"),
                        CultureInfo.InvariantCulture)
                });
            }

            return result.OrderByDescending(row =>
                Convert.ToDouble(
                    row["TotalCost"],
                    CultureInfo.InvariantCulture)).ToList();
        }
        public static async Task<List<Dictionary<string, object?>>>
    GetPurchaseCategoriesFromCubeAsync(
        this ISsasRepository repository,
        bool useInventoryCube,
        int? year = null)
{
    if (year.HasValue &&
        (year.Value < 1900 || year.Value > 2100))
    {
        throw new ArgumentException(
            "Năm phải nằm trong khoảng 1900–2100.");
    }

    string cube = useInventoryCube
        ? "Cube_Inventory"
        : "KhangNghi_Cube";

    string slicer = year.HasValue
        ? " WHERE ([Dim Time].[Year].&[" +
          year.Value.ToString(CultureInfo.InvariantCulture) + "])"
        : "";

    string mdx = $@"
SELECT
{{
    [Measures].[Total Cost],
    [Measures].[Purchase Quantity]
}} ON COLUMNS,

NONEMPTY(
    [Dim Product].[Category].[Category].MEMBERS,
    {{[Measures].[Total Cost]}}
) ON ROWS

FROM [{cube}]{slicer};";

    var rows = await repository.ExecuteMdxQueryAsync(mdx);

    return rows.Select(row => new Dictionary<string, object?>
    {
        ["Category"] = row["Category"],
        ["TotalCost"] = Convert.ToDouble(
            row["Total Cost"], CultureInfo.InvariantCulture),
        ["TotalQuantity"] = Convert.ToInt32(
            row["Purchase Quantity"], CultureInfo.InvariantCulture)
    })
    .OrderByDescending(row => Convert.ToDouble(
        row["TotalCost"], CultureInfo.InvariantCulture))
    .ToList();
}
    public static async Task<List<Dictionary<string, object?>>>
    GetPurchaseSuppliersFromCubeAsync(
        this ISsasRepository repository,
        bool useInventoryCube,
        int? year = null
        )
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
    [Measures].[Total Cost],
    [Measures].[Purchase Quantity]
}} ON COLUMNS,

NONEMPTY(
    [Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS,
    {{[Measures].[Total Cost]}}
) ON ROWS

FROM [{cube}]{slicer};";

    var rows = await repository.ExecuteMdxQueryAsync(mdx);

    return rows.Select(row => new Dictionary<string, object?>
    {
        ["SupplierName"] = row["Supplier Name"],
        ["TotalPurchaseAmount"] = Convert.ToDouble(
            row["Total Cost"], CultureInfo.InvariantCulture),
        ["TotalQuantity"] = Convert.ToInt32(
            row["Purchase Quantity"], CultureInfo.InvariantCulture)
    })
    .OrderByDescending(row => Convert.ToDouble(
        row["TotalPurchaseAmount"], CultureInfo.InvariantCulture))
    .ToList();
}
public static async Task<List<Dictionary<string, object?>>>
    GetTopPurchasedProductsFromCubeAsync(
        this ISsasRepository repository,
        bool useInventoryCube,
        int top = 10,
        int? year = null)
{
    if (top < 1 || top > 100)
        throw new ArgumentException(
            "Số sản phẩm phải nằm trong khoảng 1–100.");

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
    [Measures].[Total Cost],
    [Measures].[Purchase Quantity]
}} ON COLUMNS,

TOPCOUNT(
    NONEMPTY(
        [Dim Product].[Product Key].[Product Key].MEMBERS
        *
        [Dim Product].[Product Name].[Product Name].MEMBERS
        *
        [Dim Product].[Category].[Category].MEMBERS,
        {{[Measures].[Total Cost]}}
    ),
    {top},
    [Measures].[Total Cost]
) ON ROWS

FROM [{cube}]{slicer};";

    var rows = await repository.ExecuteMdxQueryAsync(mdx);

    return rows.Select(row => new Dictionary<string, object?>
    {
        ["ProductName"] = row["Product Name"],
        ["Category"] = row["Category"],
        ["TotalPurchaseAmount"] = Convert.ToDouble(
            row["Total Cost"], CultureInfo.InvariantCulture),
        ["TotalQuantity"] = Convert.ToInt32(
            row["Purchase Quantity"], CultureInfo.InvariantCulture)
    })
    .OrderByDescending(row => Convert.ToDouble(
        row["TotalPurchaseAmount"], CultureInfo.InvariantCulture))
    .ToList();
}
public static async Task<List<Dictionary<string, object?>>>
    GetPurchaseOrderDetailFromCubeAsync(
        this ISsasRepository repository,
        bool useInventoryCube,
        string orderId,
        int top = 50)
{
    if (string.IsNullOrWhiteSpace(orderId) || orderId.Length > 200)
        throw new ArgumentException("Mã đơn mua không hợp lệ.");

    if (top < 1 || top > 500)
        throw new ArgumentException(
            "Số dòng phải nằm trong khoảng 1–500.");

    string cube = useInventoryCube
        ? "Cube_Inventory"
        : "KhangNghi_Cube";

    // Escape dấu đóng ngoặc trong khóa member MDX.
    string orderKey = orderId.Replace("]", "]]");

    string mdx = $@"
WITH
MEMBER [Measures].[Order Date Value] AS
    [Dim Time].[Full Date].CURRENTMEMBER.MEMBER_VALUE

SELECT
{{
    [Measures].[Purchase Quantity],
    [Measures].[Total Cost],
    [Measures].[Order Date Value]
}} ON COLUMNS,

TOPCOUNT(
    NONEMPTY(
        [Dim Product].[Product Key].[Product Key].MEMBERS
        *
        [Dim Product].[Product ID].[Product ID].MEMBERS
        *
        [Dim Product].[Product Name].[Product Name].MEMBERS
        *
        [Dim Product].[Category].[Category].MEMBERS
        *
        [Dim Time].[Full Date].[Full Date].MEMBERS
        *
        [Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS
        *
        [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS,
        {{[Measures].[Total Cost]}}
    ),
    {top},
    [Measures].[Total Cost]
) ON ROWS

FROM [{cube}]

WHERE (
    [Dim Purchase Order].[Purchase Order ID].&[{orderKey}]
);";

    var rows = await repository.ExecuteMdxQueryAsync(mdx);
    var result = new List<Dictionary<string, object?>>();

    foreach (var row in rows)
    {
        object Required(string key)
        {
            if (!row.TryGetValue(key, out var value) ||
                value == null || value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "SSAS thiếu dữ liệu bắt buộc: " + key);
            }

            return value;
        }

        int quantity = Convert.ToInt32(
            Required("Purchase Quantity"),
            CultureInfo.InvariantCulture);

        double cost = Convert.ToDouble(
            Required("Total Cost"),
            CultureInfo.InvariantCulture);

        DateTime date = Convert.ToDateTime(
            Required("Order Date Value"),
            CultureInfo.InvariantCulture);

        result.Add(new Dictionary<string, object?>
        {
            ["PurchaseOrderID"] = orderId,
            ["OrderDate"] = date.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["SupplierName"] = Required("Supplier Name"),
            ["WarehouseName"] = Required("Warehouse Name"),
            ["ProductID"] = Required("Product ID"),
            ["ProductName"] = Required("Product Name"),
            ["Category"] = Required("Category"),
            ["PurchaseQuantity"] = quantity,
            ["UnitCost"] = quantity == 0
                ? (double?)null
                : Math.Round(
                    cost / quantity, 2,
                    MidpointRounding.AwayFromZero),
            ["TotalCost"] = cost
        });
    }

    return result.OrderByDescending(row =>
        Convert.ToDouble(
            row["TotalCost"], CultureInfo.InvariantCulture))
        .ToList();
}
    }

}