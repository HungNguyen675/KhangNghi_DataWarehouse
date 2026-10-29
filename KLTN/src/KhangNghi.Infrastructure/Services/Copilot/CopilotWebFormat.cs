using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KhangNghi.Infrastructure.Services.Copilot
{
    /// <summary>
    /// Định dạng câu trả lời Copilot cho khung chat web: số kiểu Việt Nam (38,08 tỷ ₫ / 38.081.049.190 ₫),
    /// câu kết luận in đậm số chính, gạch đầu dòng ngắn và gợi ý câu hỏi.
    /// Không phụ thuộc culture của máy chủ (Docker chạy invariant).
    /// </summary>
    public static class CopilotWebFormat
    {
        private static readonly NumberFormatInfo Vi = new()
        {
            NumberDecimalSeparator = ",",
            NumberGroupSeparator = ".",
            PercentDecimalSeparator = ",",
            PercentGroupSeparator = "."
        };

        /// <summary>Tiền rút gọn: từ 1 tỷ hiện "tỷ ₫", nhỏ hơn hiện "triệu ₫" (2 chữ số thập phân).</summary>
        public static string Money(double v) =>
            Math.Abs(v) >= 1e9 ? (v / 1e9).ToString("N2", Vi) + " tỷ ₫" : (v / 1e6).ToString("N2", Vi) + " triệu ₫";

        /// <summary>Tiền đầy đủ: 38.081.049.190 ₫.</summary>
        public static string Vnd(double v) => v.ToString("N0", Vi) + " ₫";

        public static string Num(double v) => v.ToString("N0", Vi);

        /// <summary>Phần trăm có sẵn giá trị % (22.36 → "22,36%").</summary>
        public static string Pct(double v, int decimals = 2) => v.ToString("N" + decimals, Vi) + "%";

        /// <summary>Tăng/giảm % so với mốc so sánh, ví dụ "tăng 25,7%".</summary>
        public static string Growth(double current, double baseline) =>
            baseline == 0 ? "không xác định" : $"{((current - baseline) >= 0 ? "tăng" : "giảm")} {Pct(Math.Abs((current - baseline) / baseline * 100), 1)}";

        /// <summary>Ghép câu kết luận và các gạch đầu dòng thành Markdown cho khung chat.</summary>
        public static string Answer(string conclusion, params string?[] bullets) =>
            string.Join("\n", new[] { conclusion }.Concat(bullets.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => "- " + b)));

        /// <summary>Dòng gợi ý câu hỏi: Gợi ý: "a" · "b".</summary>
        public static string Suggest(params string[] questions) =>
            "Gợi ý: " + string.Join(" · ", questions.Select(q => $"*\"{q}\"*"));
    }
}
