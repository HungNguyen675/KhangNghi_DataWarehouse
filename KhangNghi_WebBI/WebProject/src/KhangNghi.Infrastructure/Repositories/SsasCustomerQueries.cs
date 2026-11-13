using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace KhangNghi.Infrastructure.Repositories
{
    public static class SsasCustomerQueries
    {
        public static async Task<List<Dictionary<string, object?>>>
            GetCustomerAnalyticsFromCubeAsync(
                this ISsasRepository repository,
                bool useSalesCube,
                bool byCustomer = false)
        {
            string cube = useSalesCube
                ? "Cube_Sales"
                : "KhangNghi_Cube";

            string axis = byCustomer
                ? @"(
                    [Dim Customer].[Customer Key].[Customer Key].MEMBERS
                    *
                    [Dim Customer].[Customer ID].[Customer ID].MEMBERS
                    *
                    [Dim Customer].[Customer Name].[Customer Name].MEMBERS
                    *
                    [Dim Customer].[Segment].[Segment].MEMBERS
                )"
                : "[Dim Customer].[Segment].[Segment].MEMBERS";

            string calculatedMember = byCustomer
                ? @"
WITH
MEMBER [Measures].[Customer Key Value] AS
    [Dim Customer].[Customer Key].CURRENTMEMBER.PROPERTIES(""KEY0"")
"
                : "";

            string extraMeasure = byCustomer
                ? ", [Measures].[Customer Key Value]"
                : "";

            string mdx = $@"
{calculatedMember}
SELECT
{{
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Order Count]
    {extraMeasure}
}} ON COLUMNS,

NONEMPTY(
    {axis},
    {{[Measures].[Sales Amount]}}
) ON ROWS

FROM [{cube}];";

            var rows = await repository.ExecuteMdxQueryAsync(mdx);
            var result = new List<Dictionary<string, object?>>();

            foreach (var row in rows)
            {
                object Required(string key)
                {
                    if (!row.TryGetValue(key, out var value)
                        || value == null
                        || value == DBNull.Value)
                    {
                        throw new InvalidOperationException(
                            "SSAS thiếu dữ liệu: " + key);
                    }

                    return value;
                }

                var item = new Dictionary<string, object?>
                {
                    ["Segment"] = Convert.ToString(
                        Required("Segment"),
                        CultureInfo.InvariantCulture),

                    ["Revenue"] = Math.Round(
                        Convert.ToDouble(
                            Required("Sales Amount"),
                            CultureInfo.InvariantCulture), 2),

                    ["Profit"] = Math.Round(
                        Convert.ToDouble(
                            Required("Profit Amount"),
                            CultureInfo.InvariantCulture), 2),

                    ["OrderCount"] = Convert.ToInt64(
                        Required("Order Count"),
                        CultureInfo.InvariantCulture)
                };

                if (byCustomer)
                {
                    item["CustomerKey"] = Convert.ToInt64(
                        Required("Customer Key Value"),
                        CultureInfo.InvariantCulture);

                    item["CustomerID"] = Convert.ToString(
                        Required("Customer ID"),
                        CultureInfo.InvariantCulture);

                    item["CustomerName"] = Convert.ToString(
                        Required("Customer Name"),
                        CultureInfo.InvariantCulture);
                }

                result.Add(item);
            }

            return result;
        }
    public static async Task<object?>
    GetCustomerDetailFromCubeAsync(
        this ISsasRepository repository,
        bool useSalesCube,
        string customerId)
{
    if (string.IsNullOrWhiteSpace(customerId)
        || customerId.Length > 50)
    {
        throw new ArgumentException("Mã khách hàng không hợp lệ.");
    }

    string cube = useSalesCube
        ? "Cube_Sales"
        : "KhangNghi_Cube";

    string escapedId = customerId.Replace("]", "]]");
    string customerMember =
        "[Dim Customer].[Customer ID].&[" + escapedId + "]";

    object Required(
        Dictionary<string, object?> row,
        string key)
    {
        if (!row.TryGetValue(key, out var value)
            || value == null
            || value == DBNull.Value)
        {
            throw new InvalidOperationException(
                "SSAS thiếu dữ liệu: " + key);
        }

        return value;
    }

    double Number(
        Dictionary<string, object?> row,
        string key)
    {
        return Convert.ToDouble(
            Required(row, key),
            CultureInfo.InvariantCulture);
    }

    string Text(
        Dictionary<string, object?> row,
        string key)
    {
        return Convert.ToString(
            Required(row, key),
            CultureInfo.InvariantCulture) ?? "";
    }

    string headerMdx = $@"
WITH
MEMBER [Measures].[Customer Key Value] AS
    [Dim Customer].[Customer Key].CURRENTMEMBER.PROPERTIES(""KEY0"")

SELECT
{{
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Order Count],
    [Measures].[Quantity],
    [Measures].[Customer Key Value]
}} ON COLUMNS,

NONEMPTY(
    [Dim Customer].[Customer Key].[Customer Key].MEMBERS
    *
    [Dim Customer].[Customer Name].[Customer Name].MEMBERS
    *
    [Dim Customer].[Segment].[Segment].MEMBERS
    *
    [Dim Customer].[Source System].[Source System].MEMBERS,
    {{[Measures].[Sales Amount]}}
) ON ROWS

FROM [{cube}]
WHERE ({customerMember});";

    var headers = await repository.ExecuteMdxQueryAsync(headerMdx);

    if (headers.Count == 0)
        return null;

    if (headers.Count != 1)
    {
        throw new InvalidOperationException(
            "Mã khách hàng tương ứng nhiều bản ghi dimension; cần kiểm tra.");
    }

    var header = headers[0];

    string detailsMdx = $@"
WITH
MEMBER [Measures].[Order Date Value] AS
    [Dim Time].[Full Date].CURRENTMEMBER.MEMBER_VALUE

SELECT
{{
    [Measures].[Quantity],
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Order Date Value]
}} ON COLUMNS,

NONEMPTY(
    [Dim Sales Order].[Sales Order ID].[Sales Order ID].MEMBERS
    *
    [Dim Time].[Full Date].[Full Date].MEMBERS
    *
    [Dim Product].[Product ID].[Product ID].MEMBERS
    *
    [Dim Product].[Product Name].[Product Name].MEMBERS,
    {{[Measures].[Sales Amount]}}
) ON ROWS

FROM [{cube}]
WHERE ({customerMember});";

    var detailRows =
        await repository.ExecuteMdxQueryAsync(detailsMdx);

    var details = new List<Dictionary<string, object?>>();

    foreach (var row in detailRows)
    {
        DateTime orderDate = Convert.ToDateTime(
            Required(row, "Order Date Value"),
            CultureInfo.InvariantCulture);

        details.Add(new Dictionary<string, object?>
        {
            ["OrderID"] = Text(row, "Sales Order ID"),
            ["OrderDate"] = orderDate.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["ProductID"] = Text(row, "Product ID"),
            ["ProductName"] = Text(row, "Product Name"),
            ["Quantity"] = Convert.ToInt64(
                Required(row, "Quantity"),
                CultureInfo.InvariantCulture),
            ["SalesAmount"] = Math.Round(
                Number(row, "Sales Amount"), 2),
            ["ProfitAmount"] = Math.Round(
                Number(row, "Profit Amount"), 2)
        });
    }

    var recentDetails = details
        .OrderByDescending(r => (string)r["OrderDate"]!)
        .ThenByDescending(r => (string)r["OrderID"]!)
        .ThenBy(r => (string)r["ProductID"]!)
        .Take(50)
        .ToList();

    return new
    {
        customer = new Dictionary<string, object?>
        {
            ["CustomerKey"] = Convert.ToInt64(
                Required(header, "Customer Key Value"),
                CultureInfo.InvariantCulture),
            ["CustomerID"] = customerId,
            ["CustomerName"] = Text(header, "Customer Name"),
            ["Segment"] = Text(header, "Segment"),
            ["SourceSystem"] = Text(header, "Source System")
        },

        summary = new Dictionary<string, object?>
        {
            ["TotalRevenue"] = Math.Round(
                Number(header, "Sales Amount"), 2),
            ["TotalProfit"] = Math.Round(
                Number(header, "Profit Amount"), 2),
            ["TotalOrders"] = Convert.ToInt64(
                Required(header, "Order Count"),
                CultureInfo.InvariantCulture),
            ["TotalQuantity"] = Convert.ToInt64(
                Required(header, "Quantity"),
                CultureInfo.InvariantCulture)
        },

        orders = recentDetails
    };
}    
    }
}