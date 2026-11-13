using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KhangNghi.Infrastructure.Services.Copilot
{
    public enum QuestionResponseType
    {
        SingleMetric,   // 1 chỉ số cụ thể trong 1 năm (VD: Doanh thu năm 2022)
        MultiMetric,    // Nhiều chỉ số trong 1 năm (VD: Doanh thu và lợi nhuận năm 2022)
        TopN,           // Danh sách giới hạn N dòng (VD: Top 5 sản phẩm)
        Overview,       // Báo cáo tổng quan kinh doanh
        Comparison,     // So sánh giữa các năm (VD: So sánh doanh thu 2022 và 2025, hoặc Doanh thu các năm)
        ListOrTable,    // Danh sách bảng biểu nghiệp vụ (VD: Hàng sắp hết)
        OutOfRange,     // Năm được hỏi không có dữ liệu trong kho (ngoài 2022 - 2025)
        General         // Câu hỏi chung khác
    }

    public enum BiMetric
    {
        Revenue,
        Profit,
        ProfitMargin,
        Quantity,
        PurchaseCost,
        ShippingCost,
        Inventory,
        Customer,
        Product,
        Supplier,
        Overview
    }

    // Chiều xếp hạng: "cao nhất / nhiều nhất" hay "thấp nhất / ít nhất"
    public enum BiOrder
    {
        Highest,
        Lowest
    }

    public sealed class QuestionIntent
    {
        public string RawQuestion { get; init; } = string.Empty;
        public List<BiMetric> Metrics { get; init; } = new();
        public List<int> TargetYears { get; init; } = new();
        public bool IsAllYears { get; init; }
        public bool IsOutOfRangeYear { get; init; }
        public List<string> OutOfRangeYears { get; init; } = new();
        public int? Limit { get; init; }
        public string? Dimension { get; init; }   // customer | product_group | product | inventory | shipping | purchase | region | quarter
        public BiOrder Order { get; init; } = BiOrder.Highest;
        public QuestionResponseType ResponseType { get; init; }
        public string? SuggestedQueryId { get; init; }
    }

    public static class QuestionIntentParser
    {
        private static readonly HashSet<int> ValidDwhYears = new() { 2022, 2023, 2024, 2025 };

        public static QuestionIntent Parse(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return new QuestionIntent { RawQuestion = string.Empty, ResponseType = QuestionResponseType.General };
            }

            var raw = question.Trim();
            var lower = raw.ToLowerInvariant();

            var metrics = new List<BiMetric>();
            var targetYears = new List<int>();
            var outOfRangeYears = new List<string>();
            bool isAllYears = false;
            bool isOutOfRange = false;
            int? limit = null;
            string? dimension = null;
            string? suggestedQueryId = null;

            // 1. Kiểm tra năm cụ thể bằng Regex
            var yearMatches = Regex.Matches(lower, @"\b(19\d{2}|20\d{2})\b");
            foreach (Match match in yearMatches)
            {
                if (int.TryParse(match.Value, out var y))
                {
                    if (ValidDwhYears.Contains(y))
                    {
                        if (!targetYears.Contains(y)) targetYears.Add(y);
                    }
                    else
                    {
                        isOutOfRange = true;
                        if (!outOfRangeYears.Contains(y.ToString())) outOfRangeYears.Add(y.ToString());
                    }
                }
            }

            // Kiểm tra trường hợp đặc biệt: "năm không có dữ liệu" hoặc "năm chưa có dữ liệu"
            if (lower.Contains("năm không có dữ liệu") || lower.Contains("năm chưa có dữ liệu") || lower.Contains("năm nào không có dữ liệu"))
            {
                isOutOfRange = true;
                if (outOfRangeYears.Count == 0) outOfRangeYears.Add("yêu cầu");
            }

            // Kiểm tra "các năm", "qua các năm", "tất cả các năm"
            if (lower.Contains("các năm") || lower.Contains("qua các năm") || lower.Contains("từng năm") || lower.Contains("toàn bộ năm") || lower.Contains("2022 - 2025") || lower.Contains("2022-2025") || lower.Contains("2022 đến 2025"))
            {
                isAllYears = true;
            }

            // 2. Nhận diện Metric
            bool hasRevenue = lower.Contains("doanh thu") || lower.Contains("tiền bán") || lower.Contains("doanh số") || lower.Contains("thu về") || lower.Contains("revenue") || lower.Contains("sales");
            bool hasProfit = lower.Contains("lợi nhuận") || lower.Contains("tiền lãi") || lower.Contains("lãi gộp") || lower.Contains("lãi") || lower.Contains("profit");
            bool hasMargin = lower.Contains("biên lợi nhuận") || lower.Contains("tỷ suất lợi nhuận") || lower.Contains("margin");
            bool hasQuantity = lower.Contains("số lượng") || lower.Contains("sản lượng") || lower.Contains("chiếc") || lower.Contains("cái");
            bool hasInventory = lower.Contains("tồn kho") || lower.Contains("sắp hết") || lower.Contains("hết hàng") || lower.Contains("dưới mức") || lower.Contains("định mức") || lower.Contains("an toàn") || lower.Contains("nhập thêm") || lower.Contains("đặt hàng lại");
            bool hasShipping = lower.Contains("vận chuyển") || lower.Contains("giao hàng") || lower.Contains("phí ship") || lower.Contains("cước") || lower.Contains("shipper");
            bool hasPurchase = lower.Contains("mua hàng") || lower.Contains("nhập hàng") || lower.Contains("nhà cung cấp") || lower.Contains("ncc");
            bool hasOverview = lower.Contains("tổng quan") || lower.Contains("tổng thể") || lower.Contains("kpi") || lower.Contains("tóm tắt");
            bool hasCustomer = lower.Contains("khách hàng") || lower.Contains("khách vip") || (lower.Contains("ai") && lower.Contains("mua"));
            bool hasProduct = lower.Contains("sản phẩm") || lower.Contains("mặt hàng") || lower.Contains("thiết bị");
            // Nhóm sản phẩm (Dim_Product.Category) là thực thể riêng, phải nhận trước "sản phẩm"
            bool hasProductGroup = Regex.IsMatch(lower, @"nhóm sản phẩm|nhóm hàng|nhóm mặt hàng|danh mục|ngành hàng|loại sản phẩm|loại hàng|category");
            bool isLowest = Regex.IsMatch(lower, @"thấp nhất|ít nhất|nhỏ nhất|kém nhất|chậm nhất|bé nhất|tệ nhất|lowest");

            if (hasRevenue) metrics.Add(BiMetric.Revenue);
            if (hasProfit) metrics.Add(BiMetric.Profit);
            if (hasMargin) metrics.Add(BiMetric.ProfitMargin);
            if (hasQuantity) metrics.Add(BiMetric.Quantity);
            if (hasInventory) metrics.Add(BiMetric.Inventory);
            if (hasShipping) metrics.Add(BiMetric.ShippingCost);
            if (hasPurchase) metrics.Add(BiMetric.PurchaseCost);
            if (hasCustomer) metrics.Add(BiMetric.Customer);
            if (hasProduct) metrics.Add(BiMetric.Product);
            if (hasOverview) metrics.Add(BiMetric.Overview);

            // 3. Nhận diện Limit / Top N
            var topMatch = Regex.Match(lower, @"top\s*(\d+)");
            if (topMatch.Success && int.TryParse(topMatch.Groups[1].Value, out var t))
            {
                limit = Math.Clamp(t, 1, 50);
            }
            else
            {
                var numItemMatch = Regex.Match(lower, @"(\d+)\s*(sản phẩm|mặt hàng|khách hàng|nhà cung cấp|đơn vị)");
                if (numItemMatch.Success && int.TryParse(numItemMatch.Groups[1].Value, out var n))
                {
                    limit = Math.Clamp(n, 1, 50);
                }
                else if (isLowest || lower.Contains("cao nhất") || lower.Contains("lớn nhất") || lower.Contains("nhiều nhất") || lower.Contains("đứng đầu") || lower.Contains("bán chạy nhất") || lower.Contains("hot nhất"))
                {
                    // Hỏi dạng số ít ("sản phẩm có doanh thu thấp nhất", "nhóm sản phẩm nào lợi nhuận cao nhất") mà không nói "top N" -> 1 kết quả
                    bool plural = lower.Contains("những") || lower.Contains("các") || lower.Contains("danh sách");
                    if (!plural && (lower.Contains("khách hàng") || hasProductGroup || lower.Contains("sản phẩm") || lower.Contains("mặt hàng")))
                    {
                        limit = 1;
                    }
                }
            }

            // 4. Nhận diện Dimension
            if (hasCustomer) dimension = "customer";
            else if (hasProductGroup) dimension = "product_group";
            else if (hasProduct) dimension = "product";
            else if (hasInventory) dimension = "inventory";
            else if (hasShipping) dimension = "shipping";
            else if (hasPurchase) dimension = "purchase";
            else if (lower.Contains("khu vực") || lower.Contains("vùng") || lower.Contains("địa lý")) dimension = "region";
            else if (lower.Contains("quý")) dimension = "quarter";

            // 5. Xác định ResponseType và SuggestedQueryId
            QuestionResponseType responseType;

            if (isOutOfRange)
            {
                responseType = QuestionResponseType.OutOfRange;
            }
            else if (hasOverview)
            {
                responseType = QuestionResponseType.Overview;
                suggestedQueryId = "OVERVIEW";
            }
            else if (hasInventory)
            {
                responseType = QuestionResponseType.ListOrTable;
                suggestedQueryId = "Q28";
            }
            else if (targetYears.Count == 1 && dimension == null)
            {
                // Chỉ hỏi về thời gian của 1 năm cụ thể
                if (metrics.Contains(BiMetric.Revenue) && metrics.Contains(BiMetric.Profit))
                {
                    responseType = QuestionResponseType.MultiMetric;
                    suggestedQueryId = "Q01";
                }
                else if (metrics.Contains(BiMetric.Revenue) || metrics.Contains(BiMetric.Profit) || metrics.Contains(BiMetric.Quantity))
                {
                    responseType = QuestionResponseType.SingleMetric;
                    suggestedQueryId = "Q01";
                }
                else
                {
                    responseType = QuestionResponseType.SingleMetric;
                    suggestedQueryId = "Q01";
                }
            }
            else if (targetYears.Count >= 2 || (isAllYears && (hasRevenue || hasProfit || metrics.Count == 0)))
            {
                responseType = QuestionResponseType.Comparison;
                suggestedQueryId = "Q01";
            }
            else if (dimension == "product_group")
            {
                // Tổng hợp theo nhóm (Q08) rồi xếp hạng theo đúng chỉ số được hỏi
                responseType = limit.HasValue ? QuestionResponseType.TopN : QuestionResponseType.ListOrTable;
                suggestedQueryId = "Q08";
            }
            else if (dimension == "customer" && hasRevenue)
            {
                responseType = QuestionResponseType.TopN;
                suggestedQueryId = "Q11";
                if (!limit.HasValue) limit = 10;
            }
            else if (dimension == "product" && (hasRevenue || lower.Contains("bán chạy") || lower.Contains("hot")))
            {
                responseType = QuestionResponseType.TopN;
                suggestedQueryId = isLowest ? "Q06_LOW" : "Q06";
                if (!limit.HasValue) limit = 10;
            }
            else if (dimension == "product" && hasProfit)
            {
                responseType = QuestionResponseType.TopN;
                suggestedQueryId = isLowest ? "Q07_LOW" : "Q07";
                if (!limit.HasValue) limit = 10;
            }
            else if (limit.HasValue)
            {
                responseType = QuestionResponseType.TopN;
                suggestedQueryId = hasProfit ? (isLowest ? "Q07_LOW" : "Q07") : (isLowest ? "Q06_LOW" : "Q06");
            }
            else if (dimension == "shipping")
            {
                responseType = QuestionResponseType.ListOrTable;
                suggestedQueryId = "Q32";
            }
            else if (dimension == "purchase")
            {
                responseType = QuestionResponseType.ListOrTable;
                suggestedQueryId = "Q21";
            }
            else
            {
                responseType = QuestionResponseType.General;
            }

            return new QuestionIntent
            {
                RawQuestion = question,
                Metrics = metrics,
                TargetYears = targetYears.OrderBy(y => y).ToList(),
                IsAllYears = isAllYears,
                IsOutOfRangeYear = isOutOfRange,
                OutOfRangeYears = outOfRangeYears,
                Limit = limit,
                Dimension = dimension,
                Order = isLowest ? BiOrder.Lowest : BiOrder.Highest,
                ResponseType = responseType,
                SuggestedQueryId = suggestedQueryId
            };
        }
    }
}
