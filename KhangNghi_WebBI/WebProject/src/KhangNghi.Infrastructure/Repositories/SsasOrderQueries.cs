using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KhangNghi.Infrastructure.Repositories
{
    public static class SsasOrderQueries
    {
        public static async Task<object?> GetOrderDetailFromCubeAsync(
            this ISsasRepository repository,
            bool useSalesCube,
            string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)
                || orderId.Length > 50)
            {
                throw new ArgumentException("Mã đơn hàng không hợp lệ.");
            }

            string cube = useSalesCube
                ? "Cube_Sales"
                : "KhangNghi_Cube";

            string escapedId = orderId.Replace("]", "]]");

            string mdx = $@"
WITH
MEMBER [Measures].[Order Date Value] AS
    [Dim Time].[Full Date].CURRENTMEMBER.MEMBER_VALUE

SELECT
{{
    [Measures].[Quantity],
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Discount Amount],
    [Measures].[Order Date Value]
}} ON COLUMNS,

NONEMPTY(
    [Dim Customer].[Customer ID].[Customer ID].MEMBERS
    *
    [Dim Customer].[Customer Name].[Customer Name].MEMBERS
    *
    [Dim Time].[Full Date].[Full Date].MEMBERS
    *
    [Dim Product].[Product ID].[Product ID].MEMBERS
    *
    [Dim Product].[Product Name].[Product Name].MEMBERS
    *
    [Dim Product].[Category].[Category].MEMBERS,
    {{[Measures].[Sales Amount]}}
) ON ROWS

FROM [{cube}]

WHERE (
    [Dim Sales Order].[Sales Order ID].&[{escapedId}]
);";

            var rows = await repository.ExecuteMdxQueryAsync(mdx);

            if (rows.Count == 0)
                return null;

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

            string Text(
                Dictionary<string, object?> row,
                string key)
            {
                return Convert.ToString(
                    Required(row, key),
                    CultureInfo.InvariantCulture) ?? "";
            }

            double Money(
                Dictionary<string, object?> row,
                string key)
            {
                return Math.Round(
                    Convert.ToDouble(
                        Required(row, key),
                        CultureInfo.InvariantCulture), 2);
            }

            string Date(
                Dictionary<string, object?> row)
            {
                return Convert.ToDateTime(
                    Required(row, "Order Date Value"),
                    CultureInfo.InvariantCulture
                ).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            var first = rows[0];

            string customerId = Text(first, "Customer ID");
            string customerName = Text(first, "Customer Name");
            string orderDate = Date(first);

            if (rows.Any(row =>
                Text(row, "Customer ID") != customerId
                || Date(row) != orderDate))
            {
                throw new InvalidOperationException(
                    "Một mã đơn có nhiều khách hàng hoặc ngày đặt khác nhau.");
            }

            var items = new List<Dictionary<string, object?>>();

            foreach (var row in rows)
            {
                items.Add(new Dictionary<string, object?>
                {
                    ["ProductID"] = Text(row, "Product ID"),
                    ["ProductName"] = Text(row, "Product Name"),
                    ["Category"] = Text(row, "Category"),

                    ["Quantity"] = Convert.ToInt64(
                        Required(row, "Quantity"),
                        CultureInfo.InvariantCulture),

                    ["DiscountAmount"] = Money(row, "Discount Amount"),
                    ["SalesAmount"] = Money(row, "Sales Amount"),
                    ["ProfitAmount"] = Money(row, "Profit Amount")
                });
            }

            return new
            {
                orderHeader = new
                {
                    orderID = orderId,
                    customerID = customerId,
                    customerName,
                    orderDate,
                    itemCount = items.Count,
                    totalSales = items.Sum(item =>
                        Convert.ToDouble(
                            item["SalesAmount"],
                            CultureInfo.InvariantCulture))
                },

                items = items
                    .OrderBy(item => (string)item["ProductID"]!)
                    .ToList()
            };
        }
    }
}