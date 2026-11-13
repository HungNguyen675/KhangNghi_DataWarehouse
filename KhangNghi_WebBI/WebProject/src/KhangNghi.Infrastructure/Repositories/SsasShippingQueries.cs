using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace KhangNghi.Infrastructure.Repositories
{
    public static class SsasShippingQueries
    {
        public static async Task<List<Dictionary<string, object?>>>
            GetShippingCostsFromCubeAsync(
                this ISsasRepository repository,
                bool useSalesCube)
        {
            string cube = useSalesCube
                ? "Cube_Sales"
                : "KhangNghi_Cube";

            string mdx = $@"
SELECT
{{
    [Measures].[Fact Shipping Count],
    [Measures].[Shipping Cost]
}} ON COLUMNS,

NON EMPTY
    [Dim Shipper].[Shipper Name].[Shipper Name].MEMBERS
ON ROWS

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

                long shipmentCount = Convert.ToInt64(
                    Required("Fact Shipping Count"),
                    CultureInfo.InvariantCulture);

                double shippingCost = Convert.ToDouble(
                    Required("Shipping Cost"),
                    CultureInfo.InvariantCulture);

                result.Add(new Dictionary<string, object?>
                {
                    ["ShipperName"] = Convert.ToString(
                        Required("Shipper Name"),
                        CultureInfo.InvariantCulture),

                    ["TotalOrders"] = shipmentCount,

                    ["TotalShippingCost"] = Math.Round(
                        shippingCost, 2),

                    ["AvgCostPerOrder"] = shipmentCount > 0
                        ? Math.Round(shippingCost / shipmentCount, 2)
                        : 0d
                });
            }

            return result;
        }
       public static async Task<List<Dictionary<string, object?>>>
    GetShippingDeliveryFromCubeAsync(
        this ISsasRepository repository,
        bool useSalesCube)
{
    string cube = useSalesCube
        ? "Cube_Sales"
        : "KhangNghi_Cube";

    string mdx = $@"
SELECT
{{
    [Measures].[Fact Shipping Count],
    [Measures].[Delivery Days],
    [Measures].[Late Days],
    [Measures].[Known Deliveries],
    [Measures].[On Time Deliveries],
    [Measures].[Late Deliveries]
}} ON COLUMNS,

NON EMPTY
    [Dim Shipper].[Shipper Name].[Shipper Name].MEMBERS
ON ROWS

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

        double Number(string key)
        {
            return Convert.ToDouble(
                Required(key),
                CultureInfo.InvariantCulture);
        }

        long total = Convert.ToInt64(
            Required("Fact Shipping Count"),
            CultureInfo.InvariantCulture);

        long known = Convert.ToInt64(
            Required("Known Deliveries"),
            CultureInfo.InvariantCulture);

        long onTime = Convert.ToInt64(
            Required("On Time Deliveries"),
            CultureInfo.InvariantCulture);

        long late = Convert.ToInt64(
            Required("Late Deliveries"),
            CultureInfo.InvariantCulture);

        double deliveryDays = Number("Delivery Days");
        double lateDays = Number("Late Days");

        result.Add(new Dictionary<string, object?>
        {
            ["ShipperName"] = Convert.ToString(
                Required("Shipper Name"),
                CultureInfo.InvariantCulture),

            ["TotalOrders"] = total,
            ["KnownDeliveries"] = known,
            ["OnTimeDeliveries"] = onTime,
            ["LateOrdersCount"] = late,
            ["TotalLateDays"] = lateDays,

            ["AvgDeliveryDays"] = known > 0
                ? (object)Math.Round(deliveryDays / known, 2)
                : null,

            // Bình quân trên các shipment có đủ ngày giao,
            // bao gồm shipment đúng hạn có số ngày trễ bằng 0.
            ["AvgLateDays"] = known > 0
                ? (object)Math.Round(lateDays / known, 2)
                : null,

            ["OnTimeRatePercent"] = known > 0
                ? (object)Math.Round(onTime * 100d / known, 2)
                : null
        });
    }

    return result;
} 
public static async Task<List<Dictionary<string, object?>>>
    GetShippingRegionsFromCubeAsync(
        this ISsasRepository repository,
        bool useSalesCube)
{
    string cube = useSalesCube
        ? "Cube_Sales"
        : "KhangNghi_Cube";

    string mdx = $@"
SELECT
{{
    [Measures].[Fact Shipping Count],
    [Measures].[Shipping Cost],
    [Measures].[Known Deliveries],
    [Measures].[On Time Deliveries],
    [Measures].[Late Deliveries]
}} ON COLUMNS,

NON EMPTY
    [Dim Location].[Region].[Region].MEMBERS
ON ROWS

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

        long total = Convert.ToInt64(
            Required("Fact Shipping Count"),
            CultureInfo.InvariantCulture);

        long known = Convert.ToInt64(
            Required("Known Deliveries"),
            CultureInfo.InvariantCulture);

        long onTime = Convert.ToInt64(
            Required("On Time Deliveries"),
            CultureInfo.InvariantCulture);

        long late = Convert.ToInt64(
            Required("Late Deliveries"),
            CultureInfo.InvariantCulture);

        double cost = Convert.ToDouble(
            Required("Shipping Cost"),
            CultureInfo.InvariantCulture);

        result.Add(new Dictionary<string, object?>
        {
            ["Region"] = Convert.ToString(
                Required("Region"),
                CultureInfo.InvariantCulture),

            ["TotalOrders"] = total,
            ["KnownDeliveries"] = known,
            ["OnTimeDeliveries"] = onTime,
            ["LateOrderCount"] = late,

            ["TotalShippingCost"] = Math.Round(cost, 2),

            ["AvgCostPerOrder"] = total > 0
                ? (object)Math.Round(cost / total, 2)
                : null,

            ["LateRatePercent"] = known > 0
                ? (object)Math.Round(late * 100d / known, 2)
                : null
        });
    }

    return result;
}
public static async Task<List<Dictionary<string, object?>>>
    GetShippingEfficiencyFromCubeAsync(
        this ISsasRepository repository)
{
    const string mdx = @"
SELECT
{
    [Measures].[Sales Amount],
    [Measures].[Profit Amount],
    [Measures].[Shipping Cost]
} ON COLUMNS,

ORDER(
    NONEMPTY(
        [Dim Location].[Region].[Region].MEMBERS,
        {[Measures].[Sales Amount]}
    ),
    [Measures].[Sales Amount],
    BDESC
) ON ROWS

FROM [KhangNghi_Cube];";

    var rows = await repository.ExecuteMdxQueryAsync(mdx);
    var result = new List<Dictionary<string, object?>>();

    foreach (var row in rows)
    {
        object Required(string key)
        {
            if (!row.TryGetValue(key, out var value) ||
                value == null ||
                value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "SSAS thiếu dữ liệu bắt buộc: " + key);
            }

            return value;
        }

        double revenue = Convert.ToDouble(
            Required("Sales Amount"),
            CultureInfo.InvariantCulture);

        double profit = Convert.ToDouble(
            Required("Profit Amount"),
            CultureInfo.InvariantCulture);

        double shippingCost = Convert.ToDouble(
            Required("Shipping Cost"),
            CultureInfo.InvariantCulture);

        double? shippingRatio = revenue == 0
            ? null
            : Math.Round(
                shippingCost / revenue * 100.0,
                2,
                MidpointRounding.AwayFromZero);

        result.Add(new Dictionary<string, object?>
        {
            ["Region"] = Convert.ToString(
                Required("Region"),
                CultureInfo.InvariantCulture),

            ["TotalRevenue"] = Math.Round(revenue, 2),
            ["TotalProfit"] = Math.Round(profit, 2),
            ["ShippingCost"] = Math.Round(shippingCost, 2),

            ["ShippingCostRatioPercent"] = shippingRatio,

            ["NetProfitAfterShipping"] = Math.Round(
                profit - shippingCost,
                2,
                MidpointRounding.AwayFromZero)
        });
    }

    return result;
}
    }
}