using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KhangNghi.Core.DTOs;

namespace KhangNghi.Infrastructure.Services.Copilot
{
    /// <summary>Khoảng thời gian cần dự báo, tách từ câu hỏi tự nhiên.</summary>
    public sealed record ForecastWindow(DateTime Start, DateTime End, string Granularity, string Label, bool IsRelative);

    public sealed record ForecastRow(string Period, double Forecast, double Lower, double Upper, double SamePeriodActual);

    public sealed record ForecastSummary(
        string ModelName, ForecastWindow Window, double Total, ForecastRow? Peak, ForecastRow? Low,
        double SamePeriodActualTotal, double Mae, double Rmse, IReadOnlyList<ForecastRow> Rows);

    /// <summary>
    /// Quy tắc ngôn ngữ tự nhiên cho câu hỏi dự báo doanh thu và lời gọi mô hình dự báo (ModelKLTN).
    /// Mô hình chỉ dự báo DOANH THU toàn công ty từ 01/2026 đến 12/2027 (sau kỳ dữ liệu thực tế cuối cùng 12/2025).
    /// </summary>
    public static class CopilotForecast
    {
        public static readonly DateTime ModelStart = new(2026, 1, 1);
        public static readonly DateTime ModelEnd = new(2027, 12, 31);
        public const string ModelRangeText = "01/2026 – 12/2027";

        // Từ khóa dự báo rõ ràng: luôn là câu hỏi dự báo
        private static readonly Regex ForecastWords = new(
            @"dự báo|dự đoán|dự kiến|ước tính|ước lượng|tiên lượng|forecast|predict|sẽ đạt|sẽ là bao nhiêu",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Cụm chỉ tương lai: chỉ tính là dự báo khi câu hỏi nói về doanh thu (tránh nhầm "top 5 sản phẩm tiếp theo")
        private static readonly Regex FutureWords = new(
            @"sắp tới|tới đây|tương lai|(tháng|quý|năm|tuần|ngày) (tới|sau|tiếp theo|kế tiếp)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool IsForecastQuestion(string lower, bool hasRevenue) =>
            ForecastWords.IsMatch(lower) || (hasRevenue && FutureWords.IsMatch(lower));

        /// <summary>
        /// Tách khoảng thời gian, theo thứ tự ưu tiên:
        /// từ … đến … → tháng M/Y → quý Q/Y → nửa đầu/cuối năm Y → "N tháng/tuần/ngày/quý tới" → "tháng/quý/năm tới" → năm Y → mặc định 6 tháng.
        /// Mốc "tới / sắp tới" tính từ kỳ đầu tiên của mô hình (01/2026, ngay sau dữ liệu thực tế cuối cùng).
        /// </summary>
        public static ForecastWindow ParseWindow(string lower)
        {
            string gran = Regex.IsMatch(lower, @"(theo|từng|hàng|mỗi) tuần") ? "week"
                : Regex.IsMatch(lower, @"(theo|từng|hàng|mỗi) ngày") ? "day" : "month";

            Match m;
            // từ tháng 3/2026 đến tháng 8/2026
            m = Regex.Match(lower, @"từ\s*(?:tháng\s*|t)?(\d{1,2})\s*[/\-]\s*(\d{4})\s*(?:đến|tới|-|–)\s*(?:tháng\s*|t)?(\d{1,2})\s*[/\-]\s*(\d{4})");
            if (m.Success && TryMonth(m, 1, 2, out var a) && TryMonth(m, 3, 4, out var b) && b >= a)
                return new(a, EndOfMonth(b), gran, $"từ tháng {a:MM/yyyy} đến tháng {b:MM/yyyy}", false);

            // tháng 3/2026, tháng 3 năm 2026, t3/2026
            m = Regex.Match(lower, @"(?:tháng|\bt)\s*(\d{1,2})\s*(?:/|-|năm)\s*(\d{4})");
            if (m.Success && TryMonth(m, 1, 2, out var mon))
                return new(mon, EndOfMonth(mon), gran, $"tháng {mon:MM/yyyy}", false);

            // quý 2/2026, quý 2 năm 2026, q2 2026
            m = Regex.Match(lower, @"(?:quý|\bq)\s*([1-4])\s*(?:/|-|năm)?\s*(\d{4})");
            if (m.Success)
            {
                int q = int.Parse(m.Groups[1].Value), y = int.Parse(m.Groups[2].Value);
                var s = new DateTime(y, (q - 1) * 3 + 1, 1);
                return new(s, EndOfMonth(s.AddMonths(2)), gran, $"quý {q}/{y}", false);
            }

            // nửa đầu năm 2026, 6 tháng cuối năm 2027
            m = Regex.Match(lower, @"(nửa đầu|6 tháng đầu|nửa cuối|6 tháng cuối)\s*(?:năm\s*)?(\d{4})");
            if (m.Success)
            {
                int y = int.Parse(m.Groups[2].Value);
                bool first = m.Groups[1].Value.Contains("đầu");
                var s = new DateTime(y, first ? 1 : 7, 1);
                return new(s, EndOfMonth(s.AddMonths(5)), gran, $"6 tháng {(first ? "đầu" : "cuối")} năm {y}", false);
            }

            // 6 tháng tới, 3 quý tiếp theo, 4 tuần sắp tới, 10 ngày tới
            m = Regex.Match(lower, @"(\d{1,3})\s*(tháng|quý|tuần|ngày)\s*(?:tới|sắp tới|tiếp theo|kế tiếp|sau)");
            if (m.Success)
            {
                int n = Math.Clamp(int.Parse(m.Groups[1].Value), 1, 366);
                string unit = m.Groups[2].Value;
                var (end, g, text) = unit switch
                {
                    "quý" => (EndOfMonth(ModelStart.AddMonths(3 * n - 1)), gran, $"{n} quý tới"),
                    "tuần" => (ModelStart.AddDays(7 * n - 1), "week", $"{n} tuần tới"),
                    "ngày" => (ModelStart.AddDays(n - 1), "day", $"{n} ngày tới"),
                    _ => (EndOfMonth(ModelStart.AddMonths(n - 1)), gran, $"{n} tháng tới")
                };
                return new(ModelStart, end, g, text, true);
            }

            // tháng tới / quý tới / năm tới (không có số)
            if (Regex.IsMatch(lower, @"(tháng) (tới|sau|sắp tới|tiếp theo)"))
                return new(ModelStart, EndOfMonth(ModelStart), gran, "tháng tới", true);
            if (Regex.IsMatch(lower, @"(quý) (tới|sau|sắp tới|tiếp theo)"))
                return new(ModelStart, EndOfMonth(ModelStart.AddMonths(2)), gran, "quý tới", true);
            if (Regex.IsMatch(lower, @"(năm) (tới|sau|sắp tới|tiếp theo)"))
                return new(ModelStart, new DateTime(ModelStart.Year, 12, 31), gran, $"năm tới ({ModelStart.Year})", true);

            // năm 2027
            m = Regex.Match(lower, @"\b(20\d{2})\b");
            if (m.Success)
            {
                int y = int.Parse(m.Groups[1].Value);
                return new(new DateTime(y, 1, 1), new DateTime(y, 12, 31), gran, $"năm {y}", false);
            }

            // Không nói thời gian: 6 tháng tới, giống mặc định của trang Dự báo
            return new(ModelStart, EndOfMonth(ModelStart.AddMonths(5)), gran, "6 tháng tới", true);
        }

        /// <summary>Khoảng hỏi có nằm trọn trong phạm vi mô hình không; nằm một phần thì cắt về phạm vi.</summary>
        public static (ForecastWindow? Window, string? Problem) FitToModel(ForecastWindow w)
        {
            if (w.End < ModelStart)
                return (null, CopilotWebFormat.Answer($"Giai đoạn {w.Label} đã có số liệu **thực tế**, không cần dự báo.",
                    "Kho DWH_KhangNghi lưu doanh thu thực tế giai đoạn **2022 – 2025**.",
                    CopilotWebFormat.Suggest($"doanh thu {w.Label}")));
            if (w.Start > ModelEnd)
                return (null, CopilotWebFormat.Answer($"Chưa dự báo được doanh thu {w.Label}.",
                    $"Mô hình chỉ dự báo trong giai đoạn **{ModelRangeText}**.",
                    CopilotWebFormat.Suggest("dự báo doanh thu năm 2027")));
            if (w.Start >= ModelStart && w.End <= ModelEnd) return (w, null);
            var clipped = w with
            {
                Start = w.Start < ModelStart ? ModelStart : w.Start,
                End = w.End > ModelEnd ? ModelEnd : w.End,
                Label = $"{w.Label} (phần nằm trong phạm vi mô hình {ModelRangeText})"
            };
            return (clipped, null);
        }

        public static async Task<ForecastSummary> RunAsync(ISalesForecastService service, ForecastWindow w)
        {
            int months = Math.Max(1, (w.End.Year - w.Start.Year) * 12 + w.End.Month - w.Start.Month + 1);
            var res = await service.PredictSalesAsync(new ForecastRequest
            {
                Granularity = w.Granularity,
                HorizonMonths = months,
                StartDate = w.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDate = w.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });

            var rows = res.Points.Where(p => p.IsFuture)
                .Select(p => new ForecastRow(p.Period, Math.Round(p.ForecastValue), Math.Round(p.LowerBound), Math.Round(p.UpperBound), Math.Round(p.SamePeriodActualValue)))
                .ToList();
            var peak = rows.OrderByDescending(r => r.Forecast).FirstOrDefault();
            var low = rows.OrderBy(r => r.Forecast).FirstOrDefault();
            // So sánh cùng kỳ chỉ có nghĩa khi cả khoảng nằm trong năm 2026 (cùng kỳ = số thực tế 2025)
            double same = w.End.Year == ModelStart.Year && rows.All(r => r.SamePeriodActual > 0) ? rows.Sum(r => r.SamePeriodActual) : 0;
            return new ForecastSummary(res.ModelName, w, rows.Sum(r => r.Forecast), peak, low, same, res.MeanAbsoluteError, res.RootMeanSquaredError, rows);
        }

        /// <summary>Câu trả lời chuẩn cho giao diện web: một câu kết luận, các ý phụ ngắn, nói rõ đây là số DỰ BÁO.</summary>
        public static string BuildAnswer(ForecastSummary s, bool askLowest)
        {
            if (s.Rows.Count == 0) return $"Mô hình **chưa có kết quả dự báo** cho {s.Window.Label}.";
            var lines = new List<string>
            {
                s.Rows.Count == 1
                    ? $"Doanh thu **dự báo** {s.Window.Label}: **{Money(s.Total)}**."
                    : $"Tổng doanh thu **dự báo** {s.Window.Label}: **{Money(s.Total)}**."
            };
            if (s.Rows.Count > 1 && s.Peak != null && s.Low != null)
            {
                lines.Add(askLowest
                    ? $"- Kỳ thấp nhất: **{s.Low.Period}** ({Money(s.Low.Forecast)}); cao nhất: {s.Peak.Period} ({Money(s.Peak.Forecast)})."
                    : $"- Kỳ cao nhất: **{s.Peak.Period}** ({Money(s.Peak.Forecast)}); thấp nhất: {s.Low.Period} ({Money(s.Low.Forecast)}).");
            }
            if (s.SamePeriodActualTotal > 0)
            {
                lines.Add($"- So với thực tế cùng kỳ năm 2025 ({Money(s.SamePeriodActualTotal)}): **{CopilotWebFormat.Growth(s.Total, s.SamePeriodActualTotal)}**.");
            }
            if (s.Rows.Count == 1)
                lines.Add($"- Khoảng dự báo: {Money(s.Rows[0].Lower)} – {Money(s.Rows[0].Upper)}.");
            if (s.Window.IsRelative)
                lines.Add($"- \"Sắp tới\" được tính từ tháng 01/2026, ngay sau kỳ dữ liệu thực tế cuối cùng (12/2025).");
            lines.Add($"- Mô hình: {s.ModelName}; sai số trên dữ liệu kiểm tra MAE ≈ {Money(s.Mae)}, RMSE ≈ {Money(s.Rmse)} mỗi {GranularityText(s.Window.Granularity)}.");
            return string.Join("\n", lines);
        }

        /// <summary>Dòng dữ liệu cho bảng trong khung chat (tên cột khớp NLQ_COLUMNS ở giao diện).</summary>
        public static IEnumerable<object> TableRows(ForecastSummary s) =>
            s.Rows.Select(r => new { Period = r.Period, ForecastRevenue = r.Forecast, LowerBound = r.Lower, UpperBound = r.Upper });

        public static string Money(double v) => CopilotWebFormat.Money(v);

        private static string GranularityText(string g) => g switch { "day" => "ngày", "week" => "tuần", _ => "tháng" };

        private static bool TryMonth(Match m, int monthGroup, int yearGroup, out DateTime value)
        {
            value = default;
            int mo = int.Parse(m.Groups[monthGroup].Value), y = int.Parse(m.Groups[yearGroup].Value);
            if (mo < 1 || mo > 12 || y < 2000 || y > 2100) return false;
            value = new DateTime(y, mo, 1);
            return true;
        }

        private static DateTime EndOfMonth(DateTime d) => new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1);
    }
}
