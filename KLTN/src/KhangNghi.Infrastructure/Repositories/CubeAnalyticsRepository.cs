using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KhangNghi.Core.DTOs;
using Microsoft.Extensions.Configuration;

namespace KhangNghi.Infrastructure.Repositories
{
    /// <summary>
    /// Ghi lại nguồn dữ liệu đã dùng trong một request (SSAS và/hoặc SQL) để API trả về header X-Data-Source.
    /// </summary>
    public class DataSourceTracker
    {
        private readonly HashSet<string> _used = new();

        public void Mark(string source)
        {
            lock (_used) _used.Add(source);
        }

        public string? Summary
        {
            get
            {
                lock (_used)
                {
                    if (_used.Count == 0) return null;
                    return string.Join("+", new[] { "SSAS", "SQL" }.Where(_used.Contains));
                }
            }
        }
    }

    /// <summary>
    /// Web BI đọc số liệu từ khối KhangNghi_Cube (SSAS) bằng MDX.
    /// Cấu hình Analytics:DataSource:
    ///   Auto (mặc định) - chạy MDX, SSAS không kết nối được hoặc MDX lỗi thì dùng SQL trên DWH_KhangNghi;
    ///   Ssas - chỉ dùng SSAS, lỗi thì báo lỗi (để kiểm tra khối khi demo);
    ///   Sql  - chỉ dùng SQL.
    /// Các truy vấn khối không có độ đo tương ứng (đếm đơn hàng, chi tiết từng dòng, tồn kho mới nhất, cảnh báo tồn)
    /// luôn chạy SQL.
    /// </summary>
    public class CubeAnalyticsRepository : IDwhAnalyticsRepository
    {
        private const string Cube = "[KhangNghi_Cube]";

        // Thuộc tính trong khối (attribute hierarchy) - tên khớp các file .dim của project KhangNghi_SSAS
        private const string Year = "[Dim Time].[Year]";
        private const string Quarter = "[Dim Time].[Quarter]";
        private const string Month = "[Dim Time].[Month]";
        private const string ProductKey = "[Dim Product].[Product Key]";
        private const string ProductName = "[Dim Product].[Product Name]";
        private const string Category = "[Dim Product].[Category]";
        private const string SubCategory = "[Dim Product].[Sub Category]";
        private const string CustomerKey = "[Dim Customer].[Customer Key]";
        private const string CustomerId = "[Dim Customer].[Customer ID]";
        private const string CustomerName = "[Dim Customer].[Customer Name]";
        private const string Segment = "[Dim Customer].[Segment]";
        private const string Region = "[Dim Location].[Region]";
        private const string Country = "[Dim Location].[Country]";
        private const string City = "[Dim Location].[City]";
        private const string ManufacturerName = "[Dim Manufacturer].[Manufacturer Name]";
        private const string ManufacturerCountry = "[Dim Manufacturer].[Country]";
        private const string SupplierName = "[Dim Supplier].[Supplier Name]";
        private const string WarehouseName = "[Dim Warehouse].[Warehouse Name]";
        private const string ShipperName = "[Dim Shipper].[Shipper Name]";
        private const string ServiceLevel = "[Dim Shipper].[Service Level]";
        private const string ShippingMethod = "[Dim Shipper].[Shipping Method]";
        private const string ChannelName = "[Dim Sales Channel].[Channel Name]";
        private const string EmployeeName = "[Dim Employee].[Employee Name]";

        // Độ đo
        private const string SalesAmount = "[Measures].[Sales Amount]";
        private const string ProfitAmount = "[Measures].[Profit Amount]";
        private const string SalesQty = "[Measures].[Quantity]";
        private const string DiscountAmount = "[Measures].[Discount Amount]";
        private const string PurchaseQty = "[Measures].[Purchase Quantity]";
        private const string TotalCost = "[Measures].[Total Cost]";
        private const string QtyIn = "[Measures].[Quantity In]";
        private const string QtyOut = "[Measures].[Quantity Out]";
        private const string QtyOnHand = "[Measures].[Quantity On Hand]";
        private const string ShippingCost = "[Measures].[Shipping Cost]";
        private const string ShippingCount = "[Measures].[Fact Shipping Count]";

        // SSAS không kết nối được thì tạm dùng SQL một thời gian, không thử lại ở mọi biểu đồ
        private static DateTime _ssasDownUntil = DateTime.MinValue;

        private readonly DwhAnalyticsRepository _sql;
        private readonly ISsasRepository _ssas;
        private readonly DataSourceTracker _tracker;
        private readonly string _mode;

        public CubeAnalyticsRepository(DwhAnalyticsRepository sql, ISsasRepository ssas, DataSourceTracker tracker, IConfiguration configuration)
        {
            _sql = sql;
            _ssas = ssas;
            _tracker = tracker;
            _mode = (configuration["Analytics:DataSource"] ?? "Auto").Trim().ToLowerInvariant();
        }

        // ==================== KHUNG CHẠY MDX / DỰ PHÒNG SQL ====================

        private async Task<T> Run<T>(string name, Func<Task<T>> cube, Func<Task<T>> sql)
        {
            if (_mode == "sql" || (_mode != "ssas" && DateTime.UtcNow < _ssasDownUntil))
                return await Sql(sql);
            try
            {
                var result = await cube();
                _tracker.Mark("SSAS");
                return result;
            }
            catch (Exception ex) when (_mode != "ssas")
            {
                if (ex is SsasUnavailableException)
                {
                    // macOS/Linux không có MSOLAP: không thử lại trong 30 phút; còn lại thử lại sau 1 phút
                    var wait = ex.InnerException is PlatformNotSupportedException ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(1);
                    _ssasDownUntil = DateTime.UtcNow + wait;
                    Console.WriteLine($"[SSAS] Không kết nối được khối, dùng SQL trong {wait.TotalMinutes:0} phút: {ex.Message}");
                }
                else
                {
                    Console.WriteLine($"[SSAS] {name}: MDX lỗi, dùng SQL. {ex.Message}");
                }
                return await Sql(sql);
            }
        }

        private Task<T> Sql<T>(Func<Task<T>> sql)
        {
            _tracker.Mark("SQL");
            return sql();
        }

        private Task<MdxTable> Mdx(string mdx) => _ssas.ExecuteMdxTableAsync(mdx);

        // [Dim Time].[Year] -> [Dim Time].[Year].[Year].MEMBERS (bỏ thành viên All)
        private static string Members(string attr) => $"{attr}.{attr[attr.LastIndexOf('[')..]}.MEMBERS";
        private static string Cross(params string[] attrs) => string.Join(" * ", attrs.Select(Members));
        private static string Member(string attr, object key) => attr + "." + DwhAnalyticsRepository.MdxKey(Convert.ToString(key, CultureInfo.InvariantCulture) ?? "");

        private static string Select(string rows, string measures, string? where = null, string? with = null) =>
            $"{(with != null ? "WITH " + with + "\n" : "")}SELECT {{ {measures} }} ON COLUMNS,\n    NON EMPTY {rows} ON ROWS\nFROM {Cube}" +
            (string.IsNullOrEmpty(where) ? "" : $"\nWHERE ( {where} )");

        private static string MdxString(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

        private static string S(object?[] r, int i) => Convert.ToString(r[i], CultureInfo.InvariantCulture)?.Trim() ?? "";
        private static double D(object?[] r, int i) => r[i] == null ? 0 : Convert.ToDouble(r[i], CultureInfo.InvariantCulture);
        private static int I(object?[] r, int i) => (int)Math.Round(D(r, i));
        private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        private static double R(double v, int digits = 2) => Math.Round(v, digits);
        private static bool Blank(string s) => string.IsNullOrEmpty(s) || s.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

        // Dòng kết quả cùng tên cột với câu SQL tương ứng (frontend đọc theo tên cột)
        private static dynamic Row(params (string Key, object? Value)[] fields)
        {
            IDictionary<string, object?> row = new ExpandoObject();
            foreach (var (key, value) in fields) row[key] = value;
            return row;
        }

        // ==================== TỔNG QUAN ====================

        // Doanh thu, lợi nhuận, số lượng và phí vận chuyển lấy từ khối; số đơn, số khách và cảnh báo tồn kho
        // khối không có độ đo tương ứng nên vẫn lấy từ SQL
        public Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? year = null, string? region = null) =>
            Run(nameof(GetDashboardSummaryAsync), async () =>
            {
                var slicers = new List<string>();
                if (year.HasValue) slicers.Add(Member(Year, year.Value));
                if (!string.IsNullOrEmpty(region)) slicers.Add(Member(Region, region));
                var sales = await Mdx($"SELECT {{ {SalesAmount}, {ProfitAmount}, {SalesQty} }} ON COLUMNS\nFROM {Cube}" +
                                      (slicers.Count > 0 ? $"\nWHERE ( {string.Join(", ", slicers)} )" : ""));
                var ship = await Mdx($"SELECT {{ {ShippingCost} }} ON COLUMNS\nFROM {Cube}");

                var dto = await Sql(() => _sql.GetDashboardSummaryAsync(year, region));
                if (sales.Rows.Count > 0)
                {
                    var r = sales.Rows[0];
                    dto.TotalRevenue = R(D(r, sales.Col(SalesAmount)));
                    dto.TotalProfit = R(D(r, sales.Col(ProfitAmount)));
                    dto.TotalQuantitySold = I(r, sales.Col(SalesQty));
                    dto.ProfitMargin = dto.TotalRevenue > 0 ? R(dto.TotalProfit / dto.TotalRevenue * 100) : 0;
                }
                if (ship.Rows.Count > 0) dto.TotalShippingCost = R(D(ship.Rows[0], ship.Col(ShippingCost)));
                return dto;
            }, () => _sql.GetDashboardSummaryAsync(year, region));

        // ==================== NHÓM 1: BÁN HÀNG ====================

        public Task<IEnumerable<dynamic>> Q01_RevenueByYearAsync(int? year = null) =>
            Run(nameof(Q01_RevenueByYearAsync), async () =>
            {
                var rows = year.HasValue ? $"{{ {Member(Year, year.Value)} }}" : Members(Year);
                var t = await Mdx(Select(rows, $"{SalesAmount}, {ProfitAmount}, {SalesQty}"));
                int y = t.Col(Year), rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount), qty = t.Col(SalesQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, y)))
                    .Select(r => Row(("Year", Int(S(r, y))), ("Revenue", R(D(r, rev))), ("Profit", R(D(r, prf))), ("Quantity", I(r, qty))))
                    .OrderBy(r => (int)r.Year).ToList();
            }, () => _sql.Q01_RevenueByYearAsync(year));

        public Task<IEnumerable<dynamic>> GetRevenueByMonthAsync() =>
            Run(nameof(GetRevenueByMonthAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Year, Month), $"{SalesAmount}, {ProfitAmount}"));
                int y = t.Col(Year), m = t.Col(Month), rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, m)))
                    .Select(r => Row(("Year", Int(S(r, y))), ("Month", Int(S(r, m))), ("Revenue", R(D(r, rev))), ("Profit", R(D(r, prf)))))
                    .OrderBy(r => (int)r.Year).ThenBy(r => (int)r.Month).ToList();
            }, () => _sql.GetRevenueByMonthAsync());

        public Task<IEnumerable<dynamic>> Q02_RevenueByQuarterAsync(int? year = null) =>
            Run(nameof(Q02_RevenueByQuarterAsync), async () =>
            {
                var years = year.HasValue ? $"{{ {Member(Year, year.Value)} }}" : Members(Year);
                var t = await Mdx(Select($"{years} * {Members(Quarter)}", $"{SalesAmount}, {ProfitAmount}"));
                int y = t.Col(Year), q = t.Col(Quarter), rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, q)))
                    .Select(r => Row(("Period", $"Q{S(r, q)} - {S(r, y)}"), ("Year", Int(S(r, y))), ("Quarter", Int(S(r, q))),
                                     ("Revenue", R(D(r, rev))), ("Profit", R(D(r, prf)))))
                    .OrderBy(r => (int)r.Year).ThenBy(r => (int)r.Quarter).ToList();
            }, () => _sql.Q02_RevenueByQuarterAsync(year));

        public Task<dynamic?> Q03_PeakMonthRevenueAsync() =>
            Run<dynamic?>(nameof(Q03_PeakMonthRevenueAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Year, Month), SalesAmount));
                int y = t.Col(Year), m = t.Col(Month), rev = t.Col(SalesAmount);
                var top = t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, m))).OrderByDescending(r => D(r, rev)).FirstOrDefault();
                return top == null ? null
                    : Row(("MonthYear", $"Tháng {S(top, m)}/{S(top, y)}"), ("Year", Int(S(top, y))), ("Month", Int(S(top, m))), ("Revenue", R(D(top, rev))));
            }, () => _sql.Q03_PeakMonthRevenueAsync());

        public Task<dynamic?> Q04_PeakQuarterProfitAsync() =>
            Run<dynamic?>(nameof(Q04_PeakQuarterProfitAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Year, Quarter), ProfitAmount));
                int y = t.Col(Year), q = t.Col(Quarter), prf = t.Col(ProfitAmount);
                var top = t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, q))).OrderByDescending(r => D(r, prf)).FirstOrDefault();
                return top == null ? null
                    : Row(("Period", $"Quý {S(top, q)}/{S(top, y)}"), ("Year", Int(S(top, y))), ("Quarter", Int(S(top, q))), ("Profit", R(D(top, prf))));
            }, () => _sql.Q04_PeakQuarterProfitAsync());

        public Task<IEnumerable<dynamic>> Q05_QuantitySoldOverTimeAsync() =>
            Run(nameof(Q05_QuantitySoldOverTimeAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Year, Month), SalesQty));
                int y = t.Col(Year), m = t.Col(Month), qty = t.Col(SalesQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, m)))
                    .Select(r => Row(("Period", $"{S(r, m)}/{S(r, y)}"), ("Year", Int(S(r, y))), ("Month", Int(S(r, m))), ("TotalQuantity", I(r, qty))))
                    .OrderBy(r => (int)r.Year).ThenBy(r => (int)r.Month).ToList();
            }, () => _sql.Q05_QuantitySoldOverTimeAsync());

        // Doanh thu, lợi nhuận, số lượng theo từng sản phẩm (mã, tên, danh mục, phân nhóm)
        private async Task<List<dynamic>> ProductSalesAsync()
        {
            var t = await Mdx(Select(Cross(ProductKey, ProductName, Category, SubCategory), $"{SalesAmount}, {ProfitAmount}, {SalesQty}"));
            int k = t.Col(ProductKey), n = t.Col(ProductName), c = t.Col(Category), sc = t.Col(SubCategory);
            int rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount), qty = t.Col(SalesQty);
            return t.Rows.Where(r => !Blank(S(r, k)))
                .Select(r => Row(("ProductKey", Int(S(r, k))), ("ProductName", S(r, n)), ("Category", S(r, c)), ("SubCategory", S(r, sc)),
                                 ("Revenue", R(D(r, rev))), ("Profit", R(D(r, prf))), ("Quantity", I(r, qty))))
                .ToList();
        }

        public Task<IEnumerable<dynamic>> Q06_TopProductsByRevenueAsync(int top = 10) =>
            Run(nameof(Q06_TopProductsByRevenueAsync), async () =>
                (IEnumerable<dynamic>)(await ProductSalesAsync()).OrderByDescending(r => (double)r.Revenue).Take(top).ToList(),
                () => _sql.Q06_TopProductsByRevenueAsync(top));

        public Task<IEnumerable<dynamic>> GetLowestProductsAsync(int top, bool byProfit) =>
            Run(nameof(GetLowestProductsAsync), async () =>
            {
                var rows = await ProductSalesAsync();
                var n = Math.Clamp(top, 1, 50);
                return (IEnumerable<dynamic>)(byProfit ? rows.OrderBy(r => (double)r.Profit) : rows.OrderBy(r => (double)r.Revenue)).Take(n).ToList();
            }, () => _sql.GetLowestProductsAsync(top, byProfit));

        public Task<IEnumerable<dynamic>> Q07_TopProductsByProfitAsync(int top = 10) =>
            Run(nameof(Q07_TopProductsByProfitAsync), async () =>
                (IEnumerable<dynamic>)(await ProductSalesAsync()).OrderByDescending(r => (double)r.Profit).Take(top).ToList(),
                () => _sql.Q07_TopProductsByProfitAsync(top));

        public Task<IEnumerable<dynamic>> Q08_RevenueByCategoryAsync() =>
            Run(nameof(Q08_RevenueByCategoryAsync), async () =>
            {
                var t = await Mdx(Select(Members(Category), $"{SalesAmount}, {ProfitAmount}, {SalesQty}"));
                int c = t.Col(Category), rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount), qty = t.Col(SalesQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, c)))
                    .Select(r =>
                    {
                        double revenue = R(D(r, rev)), profit = R(D(r, prf));
                        return Row(("Category", S(r, c)), ("Revenue", revenue), ("Profit", profit), ("Quantity", I(r, qty)),
                                   ("ProfitMargin", revenue > 0 ? R(profit / revenue * 100) : 0d));
                    })
                    .OrderByDescending(r => (double)r.Revenue).ToList();
            }, () => _sql.Q08_RevenueByCategoryAsync());

        public Task<IEnumerable<dynamic>> Q09_QuantityBySubCategoryAsync() =>
            Run(nameof(Q09_QuantityBySubCategoryAsync), async () =>
            {
                var t = await Mdx(Select(Cross(SubCategory, Category), $"{SalesQty}, {SalesAmount}"));
                int sc = t.Col(SubCategory), c = t.Col(Category), qty = t.Col(SalesQty), rev = t.Col(SalesAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, sc)))
                    .Select(r => Row(("SubCategory", S(r, sc)), ("Category", S(r, c)), ("TotalQuantity", I(r, qty)), ("Revenue", R(D(r, rev)))))
                    .OrderByDescending(r => (int)r.TotalQuantity).ToList();
            }, () => _sql.Q09_QuantityBySubCategoryAsync());

        // Cần chiết khấu trung bình trên từng dòng bán: khối không có độ đo đếm dòng Fact_Sales
        public Task<IEnumerable<dynamic>> Q10_HighDiscountLowProfitProductsAsync(int top = 10) => Sql(() => _sql.Q10_HighDiscountLowProfitProductsAsync(top));

        // Cần đếm số đơn hàng (COUNT DISTINCT OrderID): khối không có chiều đơn hàng
        public Task<IEnumerable<dynamic>> Q11_TopCustomersByRevenueAsync(int top = 10) => Sql(() => _sql.Q11_TopCustomersByRevenueAsync(top));

        public Task<IEnumerable<dynamic>> Q12_TopCustomersByProfitAsync(int top = 10) =>
            Run(nameof(Q12_TopCustomersByProfitAsync), async () =>
            {
                var t = await Mdx(Select(Cross(CustomerKey, CustomerId, CustomerName, Segment), $"{ProfitAmount}, {SalesAmount}"));
                int k = t.Col(CustomerKey), id = t.Col(CustomerId), n = t.Col(CustomerName), seg = t.Col(Segment);
                int prf = t.Col(ProfitAmount), rev = t.Col(SalesAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, k)))
                    .Select(r => Row(("CustomerKey", Int(S(r, k))), ("CustomerID", S(r, id)), ("CustomerName", S(r, n)), ("Segment", S(r, seg)),
                                     ("Profit", R(D(r, prf))), ("Revenue", R(D(r, rev)))))
                    .OrderByDescending(r => (double)r.Profit).Take(top).ToList();
            }, () => _sql.Q12_TopCustomersByProfitAsync(top));

        // Chi tiết một khách hàng / một đơn hàng là dữ liệu từng dòng, không phải số tổng hợp
        public Task<dynamic?> GetCustomerDetailByNaturalIdAsync(string customerId) => Sql(() => _sql.GetCustomerDetailByNaturalIdAsync(customerId));
        public Task<dynamic?> GetOrderDetailByNaturalIdAsync(string orderId) => Sql(() => _sql.GetOrderDetailByNaturalIdAsync(orderId));

        // Cần đếm số đơn hàng
        public Task<IEnumerable<dynamic>> Q13_RevenueBySegmentAsync() => Sql(() => _sql.Q13_RevenueBySegmentAsync());

        public Task<IEnumerable<dynamic>> Q14_QuantityBySegmentAsync() =>
            Run(nameof(Q14_QuantityBySegmentAsync), async () =>
            {
                var t = await Mdx(Select(Members(Segment), SalesQty));
                int seg = t.Col(Segment), qty = t.Col(SalesQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, seg)))
                    .Select(r => Row(("Segment", S(r, seg)), ("TotalQuantity", I(r, qty))))
                    .OrderByDescending(r => (int)r.TotalQuantity).ToList();
            }, () => _sql.Q14_QuantityBySegmentAsync());

        public Task<IEnumerable<dynamic>> Q15_RevenueByRegionAsync() =>
            Run(nameof(Q15_RevenueByRegionAsync), async () =>
            {
                var t = await Mdx(Select(Members(Region), $"{SalesAmount}, {ProfitAmount}, {SalesQty}"));
                int g = t.Col(Region), rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount), qty = t.Col(SalesQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, g)))
                    .Select(r => Row(("Region", S(r, g)), ("Revenue", R(D(r, rev))), ("Profit", R(D(r, prf))), ("Quantity", I(r, qty))))
                    .OrderByDescending(r => (double)r.Revenue).ToList();
            }, () => _sql.Q15_RevenueByRegionAsync());

        public Task<IEnumerable<dynamic>> Q16_ProfitByCountryAsync() =>
            Run(nameof(Q16_ProfitByCountryAsync), async () =>
            {
                var t = await Mdx(Select(Members(Country), $"{ProfitAmount}, {SalesAmount}"));
                int c = t.Col(Country), prf = t.Col(ProfitAmount), rev = t.Col(SalesAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, c)))
                    .Select(r => Row(("Country", S(r, c)), ("Profit", R(D(r, prf))), ("Revenue", R(D(r, rev)))))
                    .OrderByDescending(r => (double)r.Profit).ToList();
            }, () => _sql.Q16_ProfitByCountryAsync());

        public Task<IEnumerable<dynamic>> Q17_TopCitiesByQuantityAsync(int top = 10) =>
            Run(nameof(Q17_TopCitiesByQuantityAsync), async () =>
            {
                var t = await Mdx(Select(Cross(City, Region), $"{SalesQty}, {SalesAmount}"));
                int c = t.Col(City), g = t.Col(Region), qty = t.Col(SalesQty), rev = t.Col(SalesAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, c)))
                    .Select(r => Row(("City", S(r, c)), ("Region", S(r, g)), ("TotalQuantity", I(r, qty)), ("Revenue", R(D(r, rev)))))
                    .OrderByDescending(r => (int)r.TotalQuantity).Take(top).ToList();
            }, () => _sql.Q17_TopCitiesByQuantityAsync(top));

        public Task<IEnumerable<dynamic>> Q18_RegionalRevenueTrendAsync() =>
            Run(nameof(Q18_RegionalRevenueTrendAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Region, Year), SalesAmount));
                int g = t.Col(Region), y = t.Col(Year), rev = t.Col(SalesAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, g)) && !Blank(S(r, y)))
                    .Select(r => Row(("Region", S(r, g)), ("Year", Int(S(r, y))), ("Revenue", R(D(r, rev)))))
                    .OrderBy(r => (int)r.Year).ThenBy(r => (string)r.Region, StringComparer.Ordinal).ToList();
            }, () => _sql.Q18_RegionalRevenueTrendAsync());

        // Số sản phẩm có bán của mỗi hãng: đếm thành viên Product Key không rỗng (calculated member)
        private const string ProductCountMember =
            "MEMBER [Measures].[Product Count] AS COUNT(NONEMPTY([Dim Product].[Product Key].[Product Key].MEMBERS, [Measures].[Sales Amount]))";

        public Task<IEnumerable<dynamic>> Q19_TopManufacturersByRevenueAsync() =>
            Run(nameof(Q19_TopManufacturersByRevenueAsync), async () =>
            {
                var t = await Mdx(Select(Cross(ManufacturerName, ManufacturerCountry), $"{SalesAmount}, [Measures].[Product Count]", with: ProductCountMember));
                int n = t.Col(ManufacturerName), c = t.Col(ManufacturerCountry), rev = t.Col(SalesAmount), cnt = t.Col("[Measures].[Product Count]");
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)) && r[rev] != null)
                    .Select(r => Row(("ManufacturerName", S(r, n)), ("Country", S(r, c)), ("Revenue", R(D(r, rev))), ("ProductCount", I(r, cnt))))
                    .OrderByDescending(r => (double)r.Revenue).ToList();
            }, () => _sql.Q19_TopManufacturersByRevenueAsync());

        public Task<IEnumerable<dynamic>> Q20_TopManufacturersByProfitAsync() =>
            Run(nameof(Q20_TopManufacturersByProfitAsync), async () =>
            {
                var t = await Mdx(Select(Cross(ManufacturerName, ManufacturerCountry), $"{ProfitAmount}, {SalesAmount}"));
                int n = t.Col(ManufacturerName), c = t.Col(ManufacturerCountry), prf = t.Col(ProfitAmount), rev = t.Col(SalesAmount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)))
                    .Select(r => Row(("ManufacturerName", S(r, n)), ("Country", S(r, c)), ("Profit", R(D(r, prf))), ("Revenue", R(D(r, rev)))))
                    .OrderByDescending(r => (double)r.Profit).ToList();
            }, () => _sql.Q20_TopManufacturersByProfitAsync());

        // ==================== NHÓM 2: MUA HÀNG ====================

        private async Task<List<dynamic>> SupplierPurchaseAsync()
        {
            var t = await Mdx(Select(Members(SupplierName), $"{TotalCost}, {PurchaseQty}"));
            int n = t.Col(SupplierName), cost = t.Col(TotalCost), qty = t.Col(PurchaseQty);
            return t.Rows.Where(r => !Blank(S(r, n)))
                .Select(r => Row(("SupplierName", S(r, n)), ("TotalPurchaseAmount", R(D(r, cost))), ("TotalQuantity", I(r, qty))))
                .ToList();
        }

        public Task<IEnumerable<dynamic>> Q21_TopSuppliersByAmountAsync() =>
            Run(nameof(Q21_TopSuppliersByAmountAsync), async () =>
                (IEnumerable<dynamic>)(await SupplierPurchaseAsync()).OrderByDescending(r => (double)r.TotalPurchaseAmount).ToList(),
                () => _sql.Q21_TopSuppliersByAmountAsync());

        public Task<IEnumerable<dynamic>> Q22_TopSuppliersByQuantityAsync() =>
            Run(nameof(Q22_TopSuppliersByQuantityAsync), async () =>
                (IEnumerable<dynamic>)(await SupplierPurchaseAsync()).OrderByDescending(r => (int)r.TotalQuantity).ToList(),
                () => _sql.Q22_TopSuppliersByQuantityAsync());

        public Task<IEnumerable<dynamic>> Q23_TopPurchasedProductsAsync(int top = 10) =>
            Run(nameof(Q23_TopPurchasedProductsAsync), async () =>
            {
                var t = await Mdx(Select(Cross(ProductName, Category), $"{TotalCost}, {PurchaseQty}"));
                int n = t.Col(ProductName), c = t.Col(Category), cost = t.Col(TotalCost), qty = t.Col(PurchaseQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)))
                    .Select(r => Row(("ProductName", S(r, n)), ("Category", Blank(S(r, c)) ? "Vật tư thiết bị" : S(r, c)),
                                     ("TotalPurchaseAmount", R(D(r, cost))), ("TotalQuantity", I(r, qty))))
                    .OrderByDescending(r => (double)r.TotalPurchaseAmount).Take(top).ToList();
            }, () => _sql.Q23_TopPurchasedProductsAsync(top));

        // Giá nhập bình quân = tổng đơn giá / số dòng nhập (calculated member)
        public Task<IEnumerable<dynamic>> Q24_PurchasePriceTrendAsync(string? productName = null) =>
            Run(nameof(Q24_PurchasePriceTrendAsync), async () =>
            {
                var products = string.IsNullOrWhiteSpace(productName)
                    ? Members(ProductName)
                    : $"FILTER({Members(ProductName)}, InStr(1, LCase({ProductName}.CurrentMember.Name), LCase({MdxString(productName.Trim())})) > 0)";
                const string with = "MEMBER [Measures].[Avg Unit Cost] AS IIF([Measures].[Fact Purchase Count] = 0, NULL, [Measures].[Unit Cost] / [Measures].[Fact Purchase Count])";
                var t = await Mdx(Select($"{products} * {Members(Year)} * {Members(Month)}", "[Measures].[Avg Unit Cost]", with: with));
                int n = t.Col(ProductName), y = t.Col(Year), m = t.Col(Month), avg = t.Col("[Measures].[Avg Unit Cost]");
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)) && !Blank(S(r, y)) && !Blank(S(r, m)))
                    .Select(r => Row(("ProductName", S(r, n)), ("Period", $"{S(r, m)}/{S(r, y)}"), ("Year", Int(S(r, y))), ("Month", Int(S(r, m))),
                                     ("AvgUnitCost", R(D(r, avg)))))
                    .OrderBy(r => (int)r.Year).ThenBy(r => (int)r.Month).ToList();
            }, () => _sql.Q24_PurchasePriceTrendAsync(productName));

        public Task<IEnumerable<dynamic>> Q25_ImportValueByRegionAsync() =>
            Run(nameof(Q25_ImportValueByRegionAsync), async () =>
            {
                var t = await Mdx(Select(Cross(WarehouseName, Region), $"{TotalCost}, {PurchaseQty}"));
                int w = t.Col(WarehouseName), g = t.Col(Region), cost = t.Col(TotalCost), qty = t.Col(PurchaseQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, w)))
                    .Select(r => Row(("WarehouseName", S(r, w)), ("Region", Blank(S(r, g)) ? "Không xác định" : S(r, g)),
                                     ("TotalImportValue", R(D(r, cost))), ("TotalQuantity", I(r, qty))))
                    .OrderByDescending(r => (double)r.TotalImportValue).ToList();
            }, () => _sql.Q25_ImportValueByRegionAsync());

        // Danh sách và chi tiết đơn nhập là dữ liệu từng phiếu
        public Task<IEnumerable<PurchaseOrderSummaryDto>> GetPurchaseOrdersSummaryAsync(int? year = null) => Sql(() => _sql.GetPurchaseOrdersSummaryAsync(year));
        public Task<IEnumerable<PurchaseOrderDetailDto>> GetPurchaseOrderDetailAsync(string orderId, int top = 50) => Sql(() => _sql.GetPurchaseOrderDetailAsync(orderId, top));

        public Task<IEnumerable<PurchaseCategorySpendDto>> GetPurchaseCategorySpendAsync(int? year = null) =>
            Run(nameof(GetPurchaseCategorySpendAsync), async () =>
            {
                var t = await Mdx(Select(Members(Category), $"{TotalCost}, {PurchaseQty}", year.HasValue ? Member(Year, year.Value) : null));
                int c = t.Col(Category), cost = t.Col(TotalCost), qty = t.Col(PurchaseQty);
                return (IEnumerable<PurchaseCategorySpendDto>)t.Rows
                    .GroupBy(r => Blank(S(r, c)) ? "Vật tư thiết bị" : S(r, c))
                    .Select(g => new PurchaseCategorySpendDto
                    {
                        Category = g.Key,
                        TotalCost = R(g.Sum(r => D(r, cost))),
                        TotalQuantity = g.Sum(r => I(r, qty))
                    })
                    .OrderByDescending(x => x.TotalCost).ToList();
            }, () => _sql.GetPurchaseCategorySpendAsync(year));

        // ==================== NHÓM 3: TỒN KHO ====================

        // Tồn kho theo sản phẩm; mức đặt lại trung bình = tổng / số dòng tồn (AVG trên cột số nguyên nên lấy phần nguyên như SQL)
        private async Task<List<dynamic>> ProductStockAsync()
        {
            const string with = "MEMBER [Measures].[Avg Reorder Level] AS IIF([Measures].[Fact Inventory Count] = 0, NULL, [Measures].[Reorder Level] / [Measures].[Fact Inventory Count])";
            var t = await Mdx(Select(Cross(ProductName, Category), $"{QtyOnHand}, [Measures].[Avg Reorder Level]", with: with));
            int n = t.Col(ProductName), c = t.Col(Category), onHand = t.Col(QtyOnHand), avg = t.Col("[Measures].[Avg Reorder Level]");
            return t.Rows.Where(r => !Blank(S(r, n)))
                .Select(r => Row(("ProductName", S(r, n)), ("Category", S(r, c)), ("TotalOnHand", I(r, onHand)), ("AvgReorderLevel", (int)Math.Truncate(D(r, avg)))))
                .ToList();
        }

        public Task<IEnumerable<dynamic>> Q26_HighestStockProductsAsync(int top = 10) =>
            Run(nameof(Q26_HighestStockProductsAsync), async () =>
                (IEnumerable<dynamic>)(await ProductStockAsync()).OrderByDescending(r => (int)r.TotalOnHand).Take(top).ToList(),
                () => _sql.Q26_HighestStockProductsAsync(top));

        public Task<IEnumerable<dynamic>> Q27_LowestStockProductsAsync(int top = 10) =>
            Run(nameof(Q27_LowestStockProductsAsync), async () =>
                (IEnumerable<dynamic>)(await ProductStockAsync()).OrderBy(r => (int)r.TotalOnHand).Take(top).ToList(),
                () => _sql.Q27_LowestStockProductsAsync(top));

        // So sánh tồn với mức đặt lại trên từng dòng tồn kho
        public Task<IEnumerable<InventoryAlertDto>> Q28_ReorderLevelAlertsAsync(int top = 50) => Sql(() => _sql.Q28_ReorderLevelAlertsAsync(top));

        public Task<IEnumerable<dynamic>> Q29_StockByRegionAsync() =>
            Run(nameof(Q29_StockByRegionAsync), async () =>
            {
                var t = await Mdx(Select(Members(Region), $"{QtyOnHand}, {QtyIn}, {QtyOut}"));
                int g = t.Col(Region), onHand = t.Col(QtyOnHand), qin = t.Col(QtyIn), qout = t.Col(QtyOut);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, g)))
                    .Select(r => Row(("Region", S(r, g)), ("TotalOnHand", I(r, onHand)), ("TotalIn", I(r, qin)), ("TotalOut", I(r, qout))))
                    .OrderByDescending(r => (int)r.TotalOnHand).ToList();
            }, () => _sql.Q29_StockByRegionAsync());

        public Task<IEnumerable<dynamic>> Q30_InventoryMovementOverTimeAsync() =>
            Run(nameof(Q30_InventoryMovementOverTimeAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Year, Month), $"{QtyIn}, {QtyOut}, {QtyOnHand}"));
                int y = t.Col(Year), m = t.Col(Month), qin = t.Col(QtyIn), qout = t.Col(QtyOut), onHand = t.Col(QtyOnHand);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, m)))
                    .Select(r => Row(("Period", $"Tháng {S(r, m)}/{S(r, y)}"), ("Year", Int(S(r, y))), ("Month", Int(S(r, m))),
                                     ("TotalQuantityIn", I(r, qin)), ("TotalQuantityOut", I(r, qout)), ("TotalQuantityOnHand", I(r, onHand))))
                    .OrderBy(r => (int)r.Year).ThenBy(r => (int)r.Month).ToList();
            }, () => _sql.Q30_InventoryMovementOverTimeAsync());

        // ==================== NHÓM 4: VẬN CHUYỂN ====================

        private const string AvgShipCostMember = "MEMBER [Measures].[Avg Shipping Cost] AS IIF([Measures].[Fact Shipping Count] = 0, NULL, [Measures].[Shipping Cost] / [Measures].[Fact Shipping Count])";

        public Task<IEnumerable<dynamic>> Q31_OrdersByShipperAsync() =>
            Run(nameof(Q31_OrdersByShipperAsync), async () =>
            {
                var t = await Mdx(Select(Cross(ShipperName, ServiceLevel), ShippingCount));
                int n = t.Col(ShipperName), lv = t.Col(ServiceLevel), cnt = t.Col(ShippingCount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)))
                    .Select(r => Row(("ShipperName", S(r, n)), ("ServiceLevel", S(r, lv)), ("TotalOrders", I(r, cnt))))
                    .OrderByDescending(r => (int)r.TotalOrders).ToList();
            }, () => _sql.Q31_OrdersByShipperAsync());

        public Task<IEnumerable<dynamic>> Q32_ShippingCostByShipperAsync() =>
            Run(nameof(Q32_ShippingCostByShipperAsync), async () =>
            {
                var t = await Mdx(Select(Members(ShipperName), ShippingCost));
                int n = t.Col(ShipperName), cost = t.Col(ShippingCost);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)))
                    .Select(r => Row(("ShipperName", S(r, n)), ("TotalShippingCost", R(D(r, cost)))))
                    .OrderByDescending(r => (double)r.TotalShippingCost).ToList();
            }, () => _sql.Q32_ShippingCostByShipperAsync());

        public Task<IEnumerable<dynamic>> Q33_AvgShippingCostByShipperAsync() =>
            Run(nameof(Q33_AvgShippingCostByShipperAsync), async () =>
            {
                var t = await Mdx(Select(Members(ShipperName), $"[Measures].[Avg Shipping Cost], {ShippingCount}", with: AvgShipCostMember));
                int n = t.Col(ShipperName), avg = t.Col("[Measures].[Avg Shipping Cost]"), cnt = t.Col(ShippingCount);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)))
                    .Select(r => Row(("ShipperName", S(r, n)), ("AvgCostPerOrder", R(D(r, avg))), ("TotalOrders", I(r, cnt))))
                    .OrderBy(r => (double)r.AvgCostPerOrder).ToList();
            }, () => _sql.Q33_AvgShippingCostByShipperAsync());

        public Task<IEnumerable<dynamic>> Q34_FastestDeliveryShippersAsync() =>
            Run(nameof(Q34_FastestDeliveryShippersAsync), async () =>
            {
                const string with = "MEMBER [Measures].[Avg Delivery Days] AS IIF([Measures].[Fact Shipping Count] = 0, NULL, [Measures].[Delivery Days] / [Measures].[Fact Shipping Count])";
                var t = await Mdx(Select(Cross(ShipperName, ShippingMethod), "[Measures].[Avg Delivery Days]", with: with));
                int n = t.Col(ShipperName), m = t.Col(ShippingMethod), avg = t.Col("[Measures].[Avg Delivery Days]");
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, n)))
                    .Select(r => Row(("ShipperName", S(r, n)), ("ShippingMethod", S(r, m)), ("AvgDeliveryDays", R(D(r, avg), 1))))
                    .OrderBy(r => (double)r.AvgDeliveryDays).ToList();
            }, () => _sql.Q34_FastestDeliveryShippersAsync());

        // Cần đếm số chuyến giao trễ (LateDays > 0) trên từng dòng: khối không có độ đo này
        public Task<IEnumerable<dynamic>> Q35_HighestLateDaysShippersAsync() => Sql(() => _sql.Q35_HighestLateDaysShippersAsync());

        public Task<IEnumerable<dynamic>> Q36_ShippingCostByRegionAsync() =>
            Run(nameof(Q36_ShippingCostByRegionAsync), async () =>
            {
                var t = await Mdx(Select(Members(Region), $"{ShippingCost}, [Measures].[Avg Shipping Cost]", with: AvgShipCostMember));
                int g = t.Col(Region), cost = t.Col(ShippingCost), avg = t.Col("[Measures].[Avg Shipping Cost]");
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, g)))
                    .Select(r => Row(("Region", S(r, g)), ("TotalShippingCost", R(D(r, cost))), ("AvgCostPerOrder", R(D(r, avg)))))
                    .OrderByDescending(r => (double)r.TotalShippingCost).ToList();
            }, () => _sql.Q36_ShippingCostByRegionAsync());

        public Task<IEnumerable<dynamic>> Q37_LateDeliveryRateByRegionAsync() => Sql(() => _sql.Q37_LateDeliveryRateByRegionAsync());

        // ==================== NHÓM 5: PHÂN TÍCH CHÉO ====================

        // Số ngày tồn kho dùng tồn của lần chụp mới nhất từng kho: độ đo tồn trong khối cộng dồn qua thời gian nên vẫn dùng SQL
        public Task<IEnumerable<dynamic>> Q38_HighSalesLowStockRiskAsync(int top = 10) => Sql(() => _sql.Q38_HighSalesLowStockRiskAsync(top));
        public Task<IEnumerable<dynamic>> Q39_HighPurchaseLowSalesRiskAsync(int top = 10) => Sql(() => _sql.Q39_HighPurchaseLowSalesRiskAsync(top));
        public Task<(IEnumerable<dynamic> Stock, IEnumerable<dynamic> Sales)> GetStockCoverDetailAsync(int productKey) => Sql(() => _sql.GetStockCoverDetailAsync(productKey));

        // Doanh thu (Fact Sales) và phí vận chuyển (Fact Shipping) cùng chiều Khu vực trong một câu MDX
        public Task<IEnumerable<dynamic>> Q40_RevenueVsShippingEfficiencyAsync() =>
            Run(nameof(Q40_RevenueVsShippingEfficiencyAsync), async () =>
            {
                var t = await Mdx(Select(Members(Region), $"{SalesAmount}, {ProfitAmount}, {ShippingCost}"));
                int g = t.Col(Region), rev = t.Col(SalesAmount), prf = t.Col(ProfitAmount), ship = t.Col(ShippingCost);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, g)) && r[rev] != null)
                    .Select(r =>
                    {
                        double revenue = R(D(r, rev)), profit = R(D(r, prf)), cost = R(D(r, ship));
                        return Row(("Region", S(r, g)), ("TotalRevenue", revenue), ("TotalProfit", profit), ("ShippingCost", cost),
                                   ("ShippingCostRatioPercent", revenue != 0 ? (object)R(cost / revenue * 100) : null),
                                   ("NetProfitAfterShipping", R(profit - cost)));
                    })
                    .OrderByDescending(r => (double)r.TotalRevenue).ToList();
            }, () => _sql.Q40_RevenueVsShippingEfficiencyAsync());

        // Các biểu đồ nhanh có cột Số đơn hàng (COUNT dòng Fact_Sales) mà khối không có
        public Task<object?> GetQuickChartAsync(string id) => Sql(() => _sql.GetQuickChartAsync(id));

        // ==================== PHÂN TÍCH ĐA CHIỀU: SO SÁNH CHÉO ====================

        private sealed record CubeDim(string Attr, string Label, string Fallback, Func<string, string> Caption, bool TimeOrGeo);

        // Cùng thứ tự nhận diện như ResolveDimension của SQL
        private static CubeDim ResolveCubeDim(string? dimKey)
        {
            var d = (dimKey ?? string.Empty).Trim().ToLowerInvariant();
            Func<string, string> same = s => s;
            if (d.Contains("channel") || d.Contains("kênh")) return new(ChannelName, "Kênh bán hàng", "Khác", same, false);
            if (d.Contains("region") || d.Contains("khu vực")) return new(Region, "Khu vực", "Khác", same, true);
            if (d.Contains("city") || d.Contains("thành phố") || d.Contains("tỉnh")) return new(City, "Thành phố", "Khác", same, true);
            if (d.Contains("segment") || d.Contains("phân khúc")) return new(Segment, "Phân khúc khách hàng", "Khác", same, false);
            if (d.Contains("customer") || d.Contains("khách hàng")) return new(CustomerName, "Khách hàng", "Khách vãng lai", same, false);
            if (d.Contains("employee") || d.Contains("nhân viên")) return new(EmployeeName, "Nhân viên", "Nhân viên", same, false);
            if (d.Contains("manufacturer") || d.Contains("hãng")) return new(ManufacturerName, "Hãng sản xuất", "Khác", same, false);
            if (d.Contains("sub category") || d.Contains("nhóm")) return new(SubCategory, "Nhóm sản phẩm", "Khác", same, false);
            if (d.Contains("year") || d.Contains("năm")) return new(Year, "Năm", "Khác", same, true);
            if (d.Contains("quarter") || d.Contains("quý")) return new(Quarter, "Quý", "Khác", s => "Quý " + s, true);
            if (d.Contains("month") || d.Contains("tháng")) return new(Month, "Tháng", "Khác", s => "Tháng " + s, true);
            return new(Category, "Danh mục sản phẩm", "Khác", same, false);
        }

        public Task<object?> GetMultiDimAnalysisAsync(string measure, string rowDim, string colDim) =>
            Run<object?>(nameof(GetMultiDimAnalysisAsync), async () =>
            {
                var mKey = (measure ?? string.Empty).Trim().ToLowerInvariant();
                string measureMdx, measureName, measureFormat = "vnd";
                if (mKey.Contains("profit") || mKey.Contains("lợi nhuận")) { measureMdx = ProfitAmount; measureName = "Lợi nhuận"; }
                else if (mKey.Contains("quantity") || mKey.Contains("số lượng") || mKey.Contains("sản lượng")) { measureMdx = SalesQty; measureName = "Số lượng bán"; measureFormat = "number"; }
                else if (mKey.Contains("discount") || mKey.Contains("chiết khấu")) { measureMdx = DiscountAmount; measureName = "Tiền chiết khấu"; }
                else if (mKey.Contains("shipping") || mKey.Contains("vận chuyển")) { measureMdx = ShippingCost; measureName = "Chi phí vận chuyển"; }
                else { measureMdx = SalesAmount; measureName = "Doanh thu"; }

                var row = ResolveCubeDim(rowDim);
                var colKeyRaw = (colDim ?? string.Empty).Trim().ToLowerInvariant();
                bool hasCol = !string.IsNullOrEmpty(colKeyRaw) && colKeyRaw != "none" && colKeyRaw != "không dùng" && colKeyRaw != "khong dung"
                              && colKeyRaw != (rowDim ?? string.Empty).Trim().ToLowerInvariant();
                var col = hasCol ? ResolveCubeDim(colDim) : null;
                if (col != null && col.Attr == row.Attr) col = null;

                // Phí vận chuyển nằm ở Fact Shipping, chỉ chia được theo thời gian và địa lý
                if (measureMdx == ShippingCost && (!row.TimeOrGeo || (col != null && !col.TimeOrGeo)))
                    throw new NotSupportedException("Phí vận chuyển không chia được theo chiều đã chọn trong khối.");

                double Val(object?[] r, int i) => measureFormat == "number" ? D(r, i) : Math.Round(D(r, i), 0);
                string Cap(CubeDim dim, object?[] r, int i) => Blank(S(r, i)) ? dim.Fallback : dim.Caption(S(r, i));

                if (col != null)
                {
                    var t = await Mdx(Select($"{Members(row.Attr)} * {Members(col.Attr)}", measureMdx));
                    int ri = t.Col(row.Attr), ci = t.Col(col.Attr), vi = t.Col(measureMdx);
                    var raw = t.Rows.Select(r => (Cap(row, r, ri), Cap(col, r, ci), Val(r, vi))).ToList();
                    return (object?)DwhAnalyticsRepository.BuildMultiDimResult(measureName, measureFormat, row.Label, col.Label, raw);
                }
                else
                {
                    var t = await Mdx(Select(Members(row.Attr), measureMdx));
                    int ri = t.Col(row.Attr), vi = t.Col(measureMdx);
                    var raw = t.Rows.GroupBy(r => Cap(row, r, ri)).Select(g => (g.Key, g.Sum(r => Val(r, vi))))
                        .OrderByDescending(x => x.Item2).Take(15).ToList();
                    return (object?)DwhAnalyticsRepository.BuildRankingResult(measureName, measureFormat, row.Label, raw);
                }
            }, () => _sql.GetMultiDimAnalysisAsync(measure, rowDim, colDim));

        // ==================== PHÂN TÍCH ĐA CHIỀU: XEM CHI TIẾT & LỌC ====================

        public Task<object?> GetOlapExploreAsync(string hierarchy, IReadOnlyList<string> path, string measure,
            IReadOnlyList<int> years, IReadOnlyList<string> regions, IReadOnlyList<string> channels, IReadOnlyList<string> categories) =>
            Run(nameof(GetOlapExploreAsync),
                () => OlapFromCubeAsync(hierarchy, path, measure, years, regions, channels, categories),
                () => _sql.GetOlapExploreAsync(hierarchy, path, measure, years, regions, channels, categories));

        // Roll-up/Drill-down: đường khoan sâu thành điều kiện trên thuộc tính của từng cấp;
        // Slice/Dice: bộ lọc thành tập thành viên ở WHERE. Cấp đang xem nằm trên ROWS.
        private async Task<object?> OlapFromCubeAsync(string hierarchy, IReadOnlyList<string> path, string measure,
            IReadOnlyList<int> years, IReadOnlyList<string> regions, IReadOnlyList<string> channels, IReadOnlyList<string> categories)
        {
            var (levels, _, hierarchyName) = DwhAnalyticsRepository.ResolveOlapHierarchy(hierarchy);
            var hKey = (hierarchy ?? string.Empty).Trim().ToLowerInvariant();
            if (hKey != "geo" && hKey != "product") hKey = "time";
            var dim = hKey switch { "geo" => "[Dim Location]", "product" => "[Dim Product]", _ => "[Dim Time]" };
            string Attr(DwhAnalyticsRepository.OlapLevel l) => $"{dim}.[{l.CubeLevel}]";

            var (measureMdx, measureName, measureFormat) = (measure ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "profit" => (ProfitAmount, "Lợi nhuận", "vnd"),
                "quantity" => (SalesQty, "Số lượng bán", "number"),
                "discount" => (DiscountAmount, "Tiền chiết khấu", "vnd"),
                _ => (SalesAmount, "Doanh thu", "vnd")
            };

            var cleanPath = (path ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            if (cleanPath.Count > levels.Length - 1) cleanPath = cleanPath.Take(levels.Length - 1).ToList();
            var depth = cleanPath.Count;
            var level = levels[depth];
            for (int i = 0; i < depth; i++)
                if (levels[i].Numeric && !int.TryParse(cleanPath[i], out _)) return null;

            static List<string> Clean(IReadOnlyList<string>? xs) =>
                (xs ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().Take(20).ToList();
            var fYears = (years ?? Array.Empty<int>()).Distinct().OrderBy(y => y).Take(20).ToList();
            var fRegions = Clean(regions);
            var fChannels = Clean(channels);
            var fCategories = Clean(categories);

            // Điều kiện theo thuộc tính; cùng thuộc tính xuất hiện ở đường khoan sâu và bộ lọc thì lấy giao
            var conditions = new List<(string Attr, List<string> Keys)>();
            void Restrict(string attr, IEnumerable<string> keys)
            {
                var list = keys.ToList();
                if (list.Count == 0) return;
                var at = conditions.FindIndex(c => c.Attr == attr);
                if (at < 0) conditions.Add((attr, list));
                else conditions[at] = (attr, conditions[at].Keys.Intersect(list).ToList());
            }
            for (int i = 0; i < depth; i++) Restrict(Attr(levels[i]), new[] { cleanPath[i] });
            Restrict(Year, fYears.Select(y => y.ToString(CultureInfo.InvariantCulture)));
            Restrict(Region, fRegions);
            Restrict(ChannelName, fChannels);
            Restrict(Category, fCategories);

            var levelAttr = Attr(level);
            string SetOf(string attr, List<string> keys) => keys.Count == 1 ? Member(attr, keys[0]) : "{ " + string.Join(", ", keys.Select(k => Member(attr, k))) + " }";
            var onLevel = conditions.FirstOrDefault(c => c.Attr == levelAttr);
            var rowSet = onLevel.Keys != null ? "{ " + string.Join(", ", onLevel.Keys.Select(k => Member(levelAttr, k))) + " }" : Members(levelAttr);
            var slicers = conditions.Where(c => c.Attr != levelAttr).Select(c => SetOf(c.Attr, c.Keys)).ToList();
            var mdx = Select(rowSet, measureMdx, slicers.Count > 0 ? string.Join(",\n        ", slicers) : null);

            var raw = new List<(string Key, double Value)>();
            if (conditions.All(c => c.Keys.Count > 0))
            {
                var t = await Mdx(mdx);
                int ki = t.Col(levelAttr), vi = t.Col(measureMdx);
                raw = t.Rows.Where(r => !Blank(S(r, ki))).Select(r => (S(r, ki), R(D(r, vi)))).ToList();
                raw = level.Numeric ? raw.OrderBy(x => Int(x.Key)).ToList() : raw.OrderByDescending(x => x.Value).ToList();
            }

            return DwhAnalyticsRepository.BuildOlapResult(hKey, hierarchyName, measureName, measureFormat, levels, cleanPath,
                fYears, fRegions, fChannels, fCategories, raw, mdx + ";");
        }

        public Task<object> GetOlapOptionsAsync() =>
            Run<object>(nameof(GetOlapOptionsAsync), async () =>
            {
                async Task<List<string>> List(string attr)
                {
                    var t = await Mdx(Select(Members(attr), SalesAmount));
                    int i = t.Col(attr);
                    return t.Rows.Select(r => S(r, i)).Where(s => !Blank(s)).Distinct().ToList();
                }
                var years = (await List(Year)).Select(Int).OrderBy(y => y).ToList();
                var regions = (await List(Region)).OrderBy(x => x, StringComparer.InvariantCulture).ToList();
                var channels = (await List(ChannelName)).OrderBy(x => x, StringComparer.InvariantCulture).ToList();
                var categories = (await List(Category)).OrderBy(x => x, StringComparer.InvariantCulture).ToList();
                return (object)new { years, regions, channels, categories };
            }, () => _sql.GetOlapOptionsAsync());

        // ==================== CHI TIẾT MỘT NĂM ====================

        public Task<(IEnumerable<dynamic> Categories, IEnumerable<dynamic> Regions, IEnumerable<dynamic> Products)> GetYearBreakdownAsync(int year, int top = 10) =>
            Run(nameof(GetYearBreakdownAsync), async () =>
            {
                var where = Member(Year, year);
                const string measures = SalesAmount + ", " + ProfitAmount + ", " + SalesQty;

                var tc = await Mdx(Select(Members(Category), measures, where));
                int c = tc.Col(Category), crev = tc.Col(SalesAmount), cprf = tc.Col(ProfitAmount), cqty = tc.Col(SalesQty);
                IEnumerable<dynamic> categories = tc.Rows.Where(r => !Blank(S(r, c)))
                    .Select(r => Row(("Category", S(r, c)), ("Revenue", R(D(r, crev))), ("Profit", R(D(r, cprf))), ("Quantity", I(r, cqty))))
                    .OrderByDescending(r => (double)r.Revenue).ToList();

                var tg = await Mdx(Select(Members(Region), measures, where));
                int g = tg.Col(Region), grev = tg.Col(SalesAmount), gprf = tg.Col(ProfitAmount), gqty = tg.Col(SalesQty);
                IEnumerable<dynamic> regions = tg.Rows.Where(r => !Blank(S(r, g)))
                    .Select(r => Row(("Region", S(r, g)), ("Revenue", R(D(r, grev))), ("Profit", R(D(r, gprf))), ("Quantity", I(r, gqty))))
                    .OrderByDescending(r => (double)r.Revenue).ToList();

                var tp = await Mdx(Select(Cross(ProductKey, ProductName, Category), measures, where));
                int pk = tp.Col(ProductKey), pn = tp.Col(ProductName), pc = tp.Col(Category);
                int prev = tp.Col(SalesAmount), pprf = tp.Col(ProfitAmount), pqty = tp.Col(SalesQty);
                IEnumerable<dynamic> products = tp.Rows.Where(r => !Blank(S(r, pk)))
                    .Select(r => Row(("ProductName", S(r, pn)), ("Category", S(r, pc)), ("Revenue", R(D(r, prev))), ("Profit", R(D(r, pprf))), ("Quantity", I(r, pqty))))
                    .OrderByDescending(r => (double)r.Revenue).Take(top).ToList();

                return (categories, regions, products);
            }, () => _sql.GetYearBreakdownAsync(year, top));

        // ==================== DỮ LIỆU HUẤN LUYỆN ML ====================

        public Task<IEnumerable<dynamic>> GetMonthlySalesForTrainingAsync(string? category = null) =>
            Run(nameof(GetMonthlySalesForTrainingAsync), async () =>
            {
                var t = await Mdx(Select(Cross(Year, Month), $"{SalesAmount}, {SalesQty}", string.IsNullOrEmpty(category) ? null : Member(Category, category)));
                int y = t.Col(Year), m = t.Col(Month), rev = t.Col(SalesAmount), qty = t.Col(SalesQty);
                return (IEnumerable<dynamic>)t.Rows.Where(r => !Blank(S(r, y)) && !Blank(S(r, m)))
                    .Select(r =>
                    {
                        int yy = Int(S(r, y)), mm = Int(S(r, m));
                        return Row(("Year", yy), ("Month", mm), ("YearMonth", $"{yy}-{mm:00}"), ("Sales", R(D(r, rev))), ("Quantity", I(r, qty)));
                    })
                    .OrderBy(r => (int)r.Year).ThenBy(r => (int)r.Month).ToList();
            }, () => _sql.GetMonthlySalesForTrainingAsync(category));
    }
}
