using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using KhangNghi.Infrastructure.Repositories;

namespace KhangNghi.DesktopAdmin;

public partial class MainWindow
{
    private ISsasRepository? _desktopReportRepository;
    private int _desktopReportVersion;

    private static object? ReadReportCell(Dictionary<string, object?> row, string alias)
    {
        string suffix = "[" + alias + "]";
        var matches = row.Where(x => x.Key.Equals(alias, StringComparison.OrdinalIgnoreCase)
            || x.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException("Không xác định được cột SSAS: " + alias);
        return matches[0].Value;
    }

    private async Task LoadDesktopAnalyticalReportAsync()
    {
        if (ReportWebView?.CoreWebView2 == null || _desktopReportRepository == null) return;
        int version = ++_desktopReportVersion;
        int? userId = _currentUser?.UserID;
        bool sales = RadReportSales.IsChecked == true;
        string? role = _currentUser?.Role;
        bool allowed = sales
            ? role is "Admin" or "Director" or "SalesStaff"
            : role is "Admin" or "Director" or "InventoryStaff";
        if (!allowed)
        {
            ReportWebView.NavigateToString("<meta charset='utf-8'><p>Không có quyền xem báo cáo này.</p>");
            PbiLoadingOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        PbiLoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            // Chỉ dùng measure hiện có; không tự đổi cấu trúc hoặc xử lý cube.
            string mdx = sales ? """
                WITH
                MEMBER [Measures].[Report Label] AS [Dim Time].[Year].CURRENTMEMBER.MEMBER_CAPTION
                MEMBER [Measures].[Report Value1] AS [Measures].[Sales Amount]
                MEMBER [Measures].[Report Value2] AS [Measures].[Profit Amount]
                SELECT {[Measures].[Report Label], [Measures].[Report Value1], [Measures].[Report Value2]} ON COLUMNS,
                    NONEMPTY([Dim Time].[Year].[Year].MEMBERS, {[Measures].[Sales Amount]}) ON ROWS
                FROM [KhangNghi_Cube]
                """ : """
                WITH
                MEMBER [Measures].[Report Label] AS [Dim Warehouse].[Warehouse Name].CURRENTMEMBER.MEMBER_CAPTION
                MEMBER [Measures].[Report Value1] AS [Measures].[Quantity On Hand]
                MEMBER [Measures].[Report Value2] AS [Measures].[Quantity In]
                MEMBER [Measures].[Report Value3] AS [Measures].[Quantity Out]
                SELECT {[Measures].[Report Label], [Measures].[Report Value1], [Measures].[Report Value2], [Measures].[Report Value3]} ON COLUMNS,
                    NONEMPTY([Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS,
                        {[Measures].[Quantity On Hand]}) ON ROWS
                FROM [KhangNghi_Cube]
                """;
            var rawRows = await _desktopReportRepository.ExecuteMdxQueryAsync(mdx);
            if (version != _desktopReportVersion || _currentUser?.UserID != userId) return;
            var rows = rawRows.Select(r => (
                Label: Convert.ToString(ReadReportCell(r, "Report Label")) ?? "",
                Value1: Convert.ToDecimal(ReadReportCell(r, "Report Value1") ?? 0, CultureInfo.InvariantCulture),
                Value2: Convert.ToDecimal(ReadReportCell(r, "Report Value2") ?? 0, CultureInfo.InvariantCulture),
                Value3: sales ? 0m : Convert.ToDecimal(ReadReportCell(r, "Report Value3") ?? 0, CultureInfo.InvariantCulture)
            )).ToList();

            string H(string text) => WebUtility.HtmlEncode(text);
            string N(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
            string title = sales ? "Doanh thu và lợi nhuận theo năm" : "Nhập – xuất – tồn theo kho";
            string unit = sales ? "đồng" : "sản phẩm";
            string header = sales
                ? "<th>Năm</th><th>Doanh thu (đ)</th><th>Lợi nhuận (đ)</th>"
                : "<th>Kho</th><th>Tồn gần nhất</th><th>Tổng nhập</th><th>Tổng xuất</th>";
            var html = new StringBuilder("""
                <!DOCTYPE html><html lang='vi'><head><meta charset='utf-8'>
                <meta name='viewport' content='width=device-width,initial-scale=1'>
                <style>
                *{box-sizing:border-box}body{font:14px 'Segoe UI',sans-serif;color:#252430;background:#fff;margin:0;padding:24px}
                h1{font-size:23px;margin:0 0 8px}p{line-height:1.6}.muted{color:#696779}.badge{display:inline-block;padding:6px 10px;border-radius:6px;background:#eee9fb;color:#6554c0}
                .summary{padding:18px;background:#f8f7fc;border:1px solid #e6e5eb;border-radius:10px;margin:18px 0}
                .summary strong{display:block;font-size:26px;margin-top:8px}.barrow{display:grid;grid-template-columns:180px 1fr 170px;align-items:center;gap:12px;margin:14px 0}
                .track{height:22px;background:#f0edf7;border-radius:5px;overflow:hidden}.bar{height:100%;background:#6554c0}.value{text-align:right}
                table{border-collapse:collapse;width:100%;margin-top:24px}th,td{padding:12px;border-bottom:1px solid #e6e5eb;text-align:right}th{background:#f8f7fc}th:first-child,td:first-child{text-align:left}
                @media(max-width:700px){.barrow{grid-template-columns:120px 1fr}.value{grid-column:2}body{padding:14px}}
                </style></head><body>
                """);
            html.Append("<span class='badge'>Nguồn: SSAS · KhangNghi_Cube</span><h1 style='margin-top:16px'>")
                .Append(H(title)).Append("</h1><p class='muted'>Truy vấn lúc ")
                .Append(H(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"))).Append(" · Theo dữ liệu cube đã xử lý.</p>");
            html.Append("<p class='muted'>").Append(sales
                ? "Hiển thị các năm có doanh thu. Giai đoạn 2022–2025 dùng cho phân tích đầy đủ; năm 2026 là dữ liệu demo nạp gia tăng, chưa đủ kỳ."
                : "Tồn sử dụng measure Quantity On Hand của SSAS; nhập và xuất là tổng toàn kỳ. Nhãn kho giữ nguyên theo cube.").Append("</p>");
            if (rows.Count == 0) html.Append("<p>Không có dữ liệu cho báo cáo này.</p>");
            else
            {
                html.Append("<div class='summary'>").Append(sales ? "Tổng doanh thu" : "Tổng tồn gần nhất")
                    .Append("<strong>").Append(N(rows.Sum(r => r.Value1))).Append(" ").Append(unit).Append("</strong></div>");
                decimal max = rows.Max(r => Math.Abs(r.Value1));
                html.Append("<h2 style='font-size:16px'>").Append(sales ? "Doanh thu theo năm" : "Tồn theo kho").Append("</h2>");
                foreach (var row in rows)
                {
                    string width = (max == 0 ? 0 : Math.Abs(row.Value1) * 100 / max).ToString("0.##", CultureInfo.InvariantCulture);
                    html.Append("<div class='barrow'><span>").Append(H(row.Label))
                        .Append("</span><div class='track'><div class='bar' style='width:").Append(width)
                        .Append("%'></div></div><span class='value'>").Append(N(row.Value1)).Append("</span></div>");
                }
                html.Append("<table><thead><tr>").Append(header).Append("</tr></thead><tbody>");
                foreach (var row in rows)
                {
                    html.Append("<tr><td>").Append(H(row.Label)).Append("</td><td>").Append(N(row.Value1))
                        .Append("</td><td>").Append(N(row.Value2)).Append("</td>");
                    if (!sales) html.Append("<td>").Append(N(row.Value3)).Append("</td>");
                    html.Append("</tr>");
                }
                html.Append("</tbody></table>");
            }
            html.Append("<p class='muted'>Dùng nút mở báo cáo Power BI phía trên để khai thác bộ lọc và các trang phân tích chi tiết trong Power BI Desktop.</p></body></html>");
            ReportWebView.NavigateToString(html.ToString());
        }
        catch (Exception ex)
        {
            if (version == _desktopReportVersion && _currentUser?.UserID == userId)
                ReportWebView.NavigateToString("<meta charset='utf-8'><div style='font:14px Segoe UI;padding:24px'><h2>Không tải được báo cáo SSAS</h2><p>"
                    + WebUtility.HtmlEncode(ex.Message) + "</p><p>Kiểm tra kết nối SSAS và thử chọn lại báo cáo.</p></div>");
        }
        finally
        {
            if (version == _desktopReportVersion) PbiLoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
