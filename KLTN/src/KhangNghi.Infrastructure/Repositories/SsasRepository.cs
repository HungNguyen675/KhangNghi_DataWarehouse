using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Threading.Tasks;
using KhangNghi.Core.DTOs;
using Microsoft.Extensions.Configuration;

namespace KhangNghi.Infrastructure.Repositories
{
    public interface ISsasRepository
    {
        Task<List<Dictionary<string, object?>>> ExecuteMdxQueryAsync(string mdxQuery);
        Task<List<Dictionary<string, object?>>> GetRevenueByYearFromCubeAsync();
        Task<List<Dictionary<string, object?>>> GetTopProductsFromCubeAsync(int top = 10);
        Task<List<Dictionary<string, object?>>> GetRevenueByCategoryFromCubeAsync();
        Task<SsasSystemStatusDto> GetSsasStatusAsync();
        List<MdxCatalogItemDto> Get40MdxQueries();
    }

    public class SsasRepository : ISsasRepository
    {
        private readonly string _ssasConnectionString;

        public SsasRepository(IConfiguration configuration)
        {
            _ssasConnectionString = configuration.GetConnectionString("SsasConnection")
                ?? "Provider=MSOLAP;Data Source=.\\SSAS_MULTI;Initial Catalog=KhangNghi_SSAS;Integrated Security=SSPI;Connect Timeout=5;";
        }

        private const string AllMemberLabel = "Tổng cộng (All)";

        /// <summary>
        /// Mô tả một cột trong flattened rowset trả về từ MSOLAP.
        /// - Cột trục (axis): [Dim].[Hierarchy].[Level].[MEMBER_CAPTION] → hiển thị theo tên Level (vd: "Product Name", "Year").
        /// - Cột measure: [Measures].[Sales Amount] → hiển thị "Sales Amount".
        /// </summary>
        private sealed class MdxColumnInfo
        {
            public int Ordinal { get; init; }
            public string DisplayName { get; set; } = string.Empty;
            public string? HierarchyKey { get; init; }   // "[Dim].[Hierarchy]" nếu là cột trục
            public bool IsAllLevel { get; init; }          // Level "(All)"
            public bool IsAxisColumn => HierarchyKey != null;
        }

        private static readonly System.Text.RegularExpressions.Regex MdxTokenRegex =
            new(@"(&?)\[((?:[^\]]|\]\])*)\]|\.([A-Za-z_]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Tách tên MDX thành các token, vd "[Dim Time].[Quarter].&amp;[1]" → ["Dim Time", "Quarter", "&amp;1"].
        /// Token key (&amp;[..]) được đánh dấu bằng tiền tố "&amp;".
        /// </summary>
        private static string[] SplitMdxName(string name)
        {
            var tokens = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in MdxTokenRegex.Matches(name))
            {
                if (m.Groups[2].Success && (m.Groups[2].Length > 0 || m.Value.Contains('[')))
                    tokens.Add(m.Groups[1].Value + m.Groups[2].Value.Replace("]]", "]"));
                else if (m.Groups[3].Success)
                    tokens.Add(m.Groups[3].Value);
            }
            return tokens.Count > 0 ? tokens.ToArray() : new[] { name };
        }

        /// <summary>
        /// Tên hiển thị cho cột giá trị (measure hoặc member trên trục COLUMNS).
        /// </summary>
        private static string BuildValueColumnName(string[] parts)
        {
            if (parts.Length == 0) return string.Empty;
            string last = parts[^1];

            // [Measures].[Sales Amount] → Sales Amount
            if (parts.Length == 2 && parts[0].Equals("Measures", StringComparison.OrdinalIgnoreCase))
                return last;

            if (parts.Length >= 2)
            {
                if (last.Equals("UNKNOWNMEMBER", StringComparison.OrdinalIgnoreCase))
                    return "Không xác định (Unknown)";
                if (last.Equals("All", StringComparison.OrdinalIgnoreCase))
                    return AllMemberLabel;
                // [Dim Time].[Quarter].&[1] → Quarter 1 ; [Dim Location].[Region].&[Canada] → Canada
                if (last.StartsWith("&"))
                {
                    string key = last[1..];
                    return double.TryParse(key, out _) ? $"{parts[^2]} {key}" : key;
                }
            }
            return last;
        }

        private static List<MdxColumnInfo> BuildColumnMap(System.Data.Common.DbDataReader reader)
        {
            var columns = new List<MdxColumnInfo>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string raw = reader.GetName(i);
                var parts = SplitMdxName(raw);

                // Cột thuộc tính member trên trục: [Dim].[Hierarchy].[Level].[MEMBER_CAPTION]
                if (parts.Length >= 2 && parts[^1].StartsWith("MEMBER_", StringComparison.OrdinalIgnoreCase))
                {
                    // Chỉ lấy caption; bỏ các thuộc tính kỹ thuật khác (MEMBER_UNIQUE_NAME, ...)
                    if (!parts[^1].Equals("MEMBER_CAPTION", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string level = parts[^2];
                    string hierarchy = parts.Length >= 4 ? parts[^3] : level;
                    string dimension = parts.Length >= 4 ? parts[^4] : hierarchy;
                    string display = level.Equals("MeasuresLevel", StringComparison.OrdinalIgnoreCase) ? "Measure" : level;
                    columns.Add(new MdxColumnInfo
                    {
                        Ordinal = i,
                        DisplayName = display,
                        HierarchyKey = $"[{dimension}].[{hierarchy}]",
                        IsAllLevel = level.Equals("(All)", StringComparison.OrdinalIgnoreCase)
                    });
                }
                else
                {
                    // Measure hoặc member trên trục COLUMNS, vd [Measures].[Sales Amount] → Sales Amount
                    columns.Add(new MdxColumnInfo { Ordinal = i, DisplayName = BuildValueColumnName(parts) });
                }
            }

            // Đảm bảo tên hiển thị không trùng (vd 2 dimension cùng có level "Name")
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columns.Where(c => !c.IsAllLevel))
            {
                string baseName = col.DisplayName;
                string name = baseName;
                if (used.Contains(name) && col.HierarchyKey != null)
                {
                    // Thêm tên dimension để phân biệt, vd "Dim Customer - Name"
                    name = $"{SplitMdxName(col.HierarchyKey)[0]} - {baseName}";
                }
                int suffix = 2;
                string candidate = name;
                while (used.Contains(candidate))
                {
                    candidate = $"{name} ({suffix++})";
                }
                used.Add(candidate);
                col.DisplayName = candidate;
            }

            return columns;
        }

        public async Task<List<Dictionary<string, object?>>> ExecuteMdxQueryAsync(string mdxQuery)
        {
            var result = new List<Dictionary<string, object?>>();

            using var conn = new OleDbConnection(_ssasConnectionString);
            await conn.OpenAsync();

            using var cmd = new OleDbCommand(mdxQuery, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var columns = BuildColumnMap(reader);
            var hierarchies = columns.Where(c => c.IsAxisColumn)
                                     .GroupBy(c => c.HierarchyKey!)
                                     .ToList();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();

                // 1. Các cột trục (dimension members), xử lý theo từng hierarchy
                foreach (var h in hierarchies)
                {
                    var levelCols = h.Where(c => !c.IsAllLevel).ToList();
                    var allCol = h.FirstOrDefault(c => c.IsAllLevel);

                    bool allLevelsNull = levelCols.All(c => reader.IsDBNull(c.Ordinal));

                    for (int k = 0; k < levelCols.Count; k++)
                    {
                        var col = levelCols[k];
                        object? val = reader.IsDBNull(col.Ordinal) ? null : reader.GetValue(col.Ordinal);

                        // Member [All] của hierarchy: mọi level đều null → ghi nhãn tổng vào level đầu tiên
                        if (val == null && allLevelsNull && k == 0)
                            val = AllMemberLabel;

                        row[col.DisplayName] = val;
                    }

                    // Truy vấn chỉ trả về level (All) (không có level con)
                    if (levelCols.Count == 0 && allCol != null)
                    {
                        row[allCol.DisplayName] = reader.IsDBNull(allCol.Ordinal)
                            ? AllMemberLabel
                            : reader.GetValue(allCol.Ordinal);
                    }
                }

                // 2. Các cột measure
                foreach (var col in columns.Where(c => !c.IsAxisColumn))
                {
                    row[col.DisplayName] = reader.IsDBNull(col.Ordinal) ? null : reader.GetValue(col.Ordinal);
                }

                result.Add(row);
            }

            return result;
        }

        public async Task<SsasSystemStatusDto> GetSsasStatusAsync()
        {
            var status = new SsasSystemStatusDto
            {
                ServerInstance = ".\\SSAS_MULTI",
                CatalogName = "KhangNghi_SSAS",
                CubeName = "KhangNghi_Cube",
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                using var conn = new OleDbConnection(_ssasConnectionString);
                await conn.OpenAsync();
                status.IsServerConnected = true;

                // 1. Kiểm tra catalog KhangNghi_SSAS tồn tại trong SSAS
                try
                {
                    using var cmdCat = new OleDbCommand("SELECT [CATALOG_NAME] FROM $SYSTEM.DBSCHEMA_CATALOGS WHERE [CATALOG_NAME] = 'KhangNghi_SSAS'", conn);
                    using var readerCat = await cmdCat.ExecuteReaderAsync();
                    status.CatalogExists = await readerCat.ReadAsync();
                    readerCat.Close();
                }
                catch
                {
                    status.CatalogExists = true; // Fallback nếu DMV quyền hạn chế
                }

                // 2. Kiểm tra trạng thái truy vấn Cube
                try
                {
                    using var cmdCube = new OleDbCommand("SELECT {[Measures].[Sales Amount]} ON COLUMNS FROM [KhangNghi_Cube]", conn);
                    using var readerCube = await cmdCube.ExecuteReaderAsync();
                    status.CubeStatus = "OPERATIONAL";
                    status.ActionRequired = "Hệ thống SSAS và Cube KhangNghi hoạt động bình thường.";
                }
                catch (Exception cubeEx)
                {
                    status.CubeStatus = "METADATA_MISMATCH";
                    status.ErrorMessage = cubeEx.Message;
                    status.ActionRequired = "Cube đã triển khai nhưng metadata measure group Fact Sales có lỗi mapping thuộc tính (Employee Key). Cần xử lý Process Cube trong SQL Server Data Tools / Visual Studio.";
                }
            }
            catch (Exception ex)
            {
                status.IsServerConnected = false;
                status.CubeStatus = "UNAVAILABLE";
                status.ErrorMessage = ex.Message;
                status.ActionRequired = "Dịch vụ MSOLAP$SSAS_MULTI chưa khởi động hoặc chuỗi kết nối không hợp lệ.";
            }

            return status;
        }

        public Task<List<Dictionary<string, object?>>> GetRevenueByYearFromCubeAsync()
        {
            string mdx = @"
                SELECT 
                    [Measures].[Sales Amount] ON COLUMNS,
                    [Dim Time].[Year].MEMBERS ON ROWS
                FROM [KhangNghi_Cube]
            ";
            return ExecuteMdxQueryAsync(mdx);
        }

        public Task<List<Dictionary<string, object?>>> GetTopProductsFromCubeAsync(int top = 10)
        {
            string mdx = $@"
                SELECT 
                    [Measures].[Profit Amount] ON COLUMNS,
                    TOPCOUNT([Dim Product].[Product Name].MEMBERS, {top}, [Measures].[Profit Amount]) ON ROWS
                FROM [KhangNghi_Cube]
            ";
            return ExecuteMdxQueryAsync(mdx);
        }

        public Task<List<Dictionary<string, object?>>> GetRevenueByCategoryFromCubeAsync()
        {
            string mdx = @"
                SELECT 
                    [Measures].[Sales Amount] ON COLUMNS,
                    [Dim Product].[Category].MEMBERS ON ROWS
                FROM [KhangNghi_Cube]
            ";
            return ExecuteMdxQueryAsync(mdx);
        }

        public List<MdxCatalogItemDto> Get40MdxQueries()
        {
            return new List<MdxCatalogItemDto>
            {
                // MỤC 1: BÁN HÀNG & DOANH THU (CÂU 01 - 10)
                new() {
                    Id = "MDX-01", OrderNumber = 1, Section = "Bán hàng & Doanh thu",
                    Title = "Tổng Doanh thu, Lợi nhuận và Số lượng bán theo từng Năm",
                    BusinessPurpose = "Báo cáo tăng trưởng dài hạn cấp quản trị cao nhất, tổng hợp dữ liệu từ ngày/tháng lên mức năm (Roll-up).",
                    OperationType = "Roll-up", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Quantity] } ON COLUMNS,\n    [Dim Time].[Year].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-02", OrderNumber = 2, Section = "Bán hàng & Doanh thu",
                    Title = "Doanh thu theo từng Quý của một Năm cụ thể",
                    BusinessPurpose = "Ban giám đốc theo dõi tiến độ hoàn thành chỉ tiêu kinh doanh theo từng quý trong năm 2026 (Drill-down).",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    [Dim Time].[Year].&[2026].CHILDREN ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-03", OrderNumber = 3, Section = "Bán hàng & Doanh thu",
                    Title = "Doanh số bán hàng chi tiết theo từng Tháng trong năm",
                    BusinessPurpose = "Đánh giá xu hướng biến động doanh số qua các tháng để chuẩn bị kế hoạch sản xuất và nhập hàng.",
                    OperationType = "Time Trend", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    [Dim Time].[Month].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-04", OrderNumber = 4, Section = "Bán hàng & Doanh thu",
                    Title = "Doanh thu và Lợi nhuận theo từng Danh mục sản phẩm (Category)",
                    BusinessPurpose = "Xác định nhóm ngành hàng mũi nhọn (Nội thất văn phòng, gia đình, phụ kiện...) đóng góp vào dòng tiền.",
                    OperationType = "Category Aggregation", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-05", OrderNumber = 5, Section = "Bán hàng & Doanh thu",
                    Title = "Khoan sâu từ Danh mục xuống Phân nhóm sản phẩm (Sub Category)",
                    BusinessPurpose = "Khảo sát chi tiết cơ cấu các dòng sản phẩm con trong từng danh mục chính.",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    [Dim Product].[Sub Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-06", OrderNumber = 6, Section = "Bán hàng & Doanh thu",
                    Title = "Top 10 Sản phẩm mang lại Doanh thu cao nhất (Top-N Analysis)",
                    BusinessPurpose = "Lọc ra 10 mặt hàng gà đẻ trứng vàng về mặt doanh thu của toàn công ty.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,\n    TOPCOUNT([Dim Product].[Product Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-07", OrderNumber = 7, Section = "Bán hàng & Doanh thu",
                    Title = "Top 10 Sản phẩm có Lợi nhuận gộp cao nhất",
                    BusinessPurpose = "Phát hiện 10 sản phẩm có biên lợi nhuận ròng tốt nhất, không chỉ phụ thuộc vào doanh số bán thuần.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Profit Amount], [Measures].[Sales Amount] } ON COLUMNS,\n    TOPCOUNT([Dim Product].[Product Name].MEMBERS, 10, [Measures].[Profit Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-08", OrderNumber = 8, Section = "Bán hàng & Doanh thu",
                    Title = "Top 5 Sản phẩm bán chậm nhất (Bottom-N Analysis)",
                    BusinessPurpose = "Nhận diện các mặt hàng ế ẩm để có giải pháp xả hàng tồn hoặc ngừng kinh doanh.",
                    OperationType = "Bottom-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Quantity], [Measures].[Sales Amount] } ON COLUMNS,\n    BOTTOMCOUNT([Dim Product].[Product Name].MEMBERS, 5, [Measures].[Quantity]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-09", OrderNumber = 9, Section = "Bán hàng & Doanh thu",
                    Title = "Cơ cấu Doanh thu và Chiết khấu theo Kênh bán hàng (Sales Channel)",
                    BusinessPurpose = "So sánh hiệu quả giữa các kênh bán hàng (Bán lẻ cửa hàng, Website TMĐT, Đại lý sỉ...).",
                    OperationType = "Dimension Slicing", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,\n    [Dim Sales Channel].[Channel Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-10", OrderNumber = 10, Section = "Bán hàng & Doanh thu",
                    Title = "Phân bổ Doanh thu theo Hình thức thanh toán (Payment Method)",
                    BusinessPurpose = "Đánh giá xu hướng thanh toán của khách hàng (Tiền mặt, Chuyển khoản, Thẻ tín dụng, Trả góp...).",
                    OperationType = "Dimension Slicing", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    [Dim Payment Method].[Method Type].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 2: KHÁCH HÀNG & THỊ TRƯỜNG ĐỊA LÝ (CÂU 11 - 18)
                new() {
                    Id = "MDX-11", OrderNumber = 11, Section = "Khách hàng & Thị trường",
                    Title = "Doanh thu theo Phân khúc khách hàng (Segment)",
                    BusinessPurpose = "Đo lường tỷ trọng doanh số giữa khách hàng cá nhân, doanh nghiệp và văn phòng gia đình.",
                    OperationType = "Customer Segmentation", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    [Dim Customer].[Segment].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-12", OrderNumber = 12, Section = "Khách hàng & Thị trường",
                    Title = "Top 10 Khách hàng VIP có giá trị mua hàng lớn nhất",
                    BusinessPurpose = "Chăm sóc khách hàng trọng điểm (Key Accounts) và triển khai chính sách tri ân đặc biệt.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,\n    TOPCOUNT([Dim Customer].[Customer Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-13", OrderNumber = 13, Section = "Khách hàng & Thị trường",
                    Title = "Doanh thu và Lợi nhuận theo Khu vực địa lý (Region)",
                    BusinessPurpose = "Đánh giá hiệu quả kinh doanh của các chi nhánh vùng miền trên cả nước.",
                    OperationType = "Geographic Aggregation", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    [Dim Location].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-14", OrderNumber = 14, Section = "Khách hàng & Thị trường",
                    Title = "Khoan sâu Doanh số từ Khu vực xuống Tỉnh / Thành phố (City)",
                    BusinessPurpose = "Xác định các tỉnh/thành phố trọng điểm tiêu thụ hàng hóa mạnh nhất trong từng vùng.",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    [Dim Location].[City].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-15", OrderNumber = 15, Section = "Khách hàng & Thị trường",
                    Title = "Thao tác SLICE (Cắt lát) – Doanh thu Danh mục tại riêng Miền Nam",
                    BusinessPurpose = "Cố định 1 lát cắt phân tích chỉ riêng cho thị trường trọng điểm Miền Nam bằng mệnh đề WHERE.",
                    OperationType = "Slice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ([Dim Location].[Region].&[Miền Nam]);"
                },
                new() {
                    Id = "MDX-16", OrderNumber = 16, Section = "Khách hàng & Thị trường",
                    Title = "Thao tác DICE (Cắt khối đa chiều) – Phân tích Danh mục tại Miền Nam trong Năm 2026",
                    BusinessPurpose = "Thu hẹp phạm vi khối dữ liệu thỏa mãn đồng thời 2 điều kiện chiều (Không gian & Thời gian).",
                    OperationType = "Dice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE (\n    [Dim Location].[Region].&[Miền Nam],\n    [Dim Time].[Year].&[2026]\n);"
                },
                new() {
                    Id = "MDX-17", OrderNumber = 17, Section = "Khách hàng & Thị trường",
                    Title = "Thao tác PIVOT (Xoay chiều) – Ma trận chéo Danh mục x Vùng miền",
                    BusinessPurpose = "Xoay ma trận tọa độ hiển thị Vùng miền trên Cột và Danh mục trên Hàng để so sánh tỷ trọng chéo.",
                    OperationType = "Pivot", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Dim Location].[Region].MEMBERS ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ([Measures].[Sales Amount]);"
                },
                new() {
                    Id = "MDX-18", OrderNumber = 18, Section = "Khách hàng & Thị trường",
                    Title = "Top 5 Thành phố đóng góp Lợi nhuận cao nhất cả nước",
                    BusinessPurpose = "Tìm ra các đô thị mang lại tỷ suất lợi nhuận cao nhất để tối ưu ngân sách quảng bá.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Profit Amount] ON COLUMNS,\n    TOPCOUNT([Dim Location].[City].MEMBERS, 5, [Measures].[Profit Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 3: NHÂN SỰ, KHUYẾN MÃI & TÍNH TOÁN NÂNG CAO (CÂU 19 - 24)
                new() {
                    Id = "MDX-19", OrderNumber = 19, Section = "Nhân sự & Khuyến mãi",
                    Title = "Đánh giá KPI Doanh số bán hàng theo từng Nhân viên",
                    BusinessPurpose = "Đánh giá năng suất của đội ngũ bán hàng và tính hoa hồng kinh doanh.",
                    OperationType = "Ranking Order", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    ORDER([Dim Employee].[Employee Name].MEMBERS, [Measures].[Sales Amount], BDESC) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-20", OrderNumber = 20, Section = "Nhân sự & Khuyến mãi",
                    Title = "Đánh giá hiệu quả các chương trình Khuyến mãi",
                    BusinessPurpose = "Phân tích lượng tiền chiết khấu (Discount) đã bỏ ra so với doanh thu mang lại của từng đợt khuyến mãi.",
                    OperationType = "Promotion Analysis", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,\n    [Dim Promotion].[Promotion Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-21", OrderNumber = 21, Section = "Nhân sự & Khuyến mãi",
                    Title = "Doanh số sản phẩm bán ra theo Hãng sản xuất",
                    BusinessPurpose = "Xác định thương hiệu/hãng sản xuất nào đang được thị trường ưa chuộng nhất.",
                    OperationType = "Manufacturer Breakdown", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,\n    [Dim Manufacturer].[Manufacturer Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-22", OrderNumber = 22, Section = "Nhân sự & Khuyến mãi",
                    Title = "Tạo trường tính toán động (Calculated Member) – Tính Tỷ suất lợi nhuận (Profit Margin %)",
                    BusinessPurpose = "Dùng WITH MEMBER tính toán tỷ lệ Lợi nhuận / Doanh thu tức thời mà không cần can thiệp CSDL.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Profit Margin Pct] AS\n    IIF([Measures].[Sales Amount] = 0, 0, ([Measures].[Profit Amount] / [Measures].[Sales Amount]) * 100),\n    FORMAT_STRING = \"0.00%\"\nSELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-23", OrderNumber = 23, Section = "Nhân sự & Khuyến mãi",
                    Title = "Phân tích Doanh thu theo Gói Dịch vụ đính kèm",
                    BusinessPurpose = "Đo lường doanh thu từ các gói bảo hành, dịch vụ vận chuyển và lắp đặt hoàn thiện.",
                    OperationType = "Service Breakdown", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    [Dim Service].[Service Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-24", OrderNumber = 24, Section = "Nhân sự & Khuyến mãi",
                    Title = "Ma trận tích chéo (CROSSJOIN) giữa Kênh bán hàng và Quý trong Năm",
                    BusinessPurpose = "Đối chiếu diễn biến doanh thu của từng kênh bán lẻ/online qua các quý trong năm 2026.",
                    OperationType = "Crossjoin", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Dim Time].[Quarter].MEMBERS ON COLUMNS,\n    CROSSJOIN(\n        [Dim Sales Channel].[Channel Name].MEMBERS,\n        { [Measures].[Sales Amount] }\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 4: MUA HÀNG & NHÀ CUNG CẤP (CÂU 25 - 29)
                new() {
                    Id = "MDX-25", OrderNumber = 25, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Tổng Chi phí mua hàng và Số lượng nhập theo từng Năm",
                    BusinessPurpose = "Kiểm soát dòng tiền chi mua nguyên vật liệu/hàng hóa qua các năm tài chính.",
                    OperationType = "Roll-up", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,\n    [Dim Time].[Year].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-26", OrderNumber = 26, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Top 5 Nhà cung cấp có giá trị đơn hàng mua vào lớn nhất",
                    BusinessPurpose = "Đánh giá mức độ phụ thuộc công nợ vào các nhà cung ứng trọng yếu.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,\n    TOPCOUNT([Dim Supplier].[Supplier Name].MEMBERS, 5, [Measures].[Total Cost]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-27", OrderNumber = 27, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Chi phí nhập hàng phân bổ theo từng Danh mục sản phẩm",
                    BusinessPurpose = "Đối chiếu tỷ trọng vốn đầu tư nhập hàng giữa các nhóm sản phẩm khác nhau.",
                    OperationType = "Category Aggregation", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    [Measures].[Total Cost] ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-28", OrderNumber = 28, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Tính Đơn giá bình quân nhập hàng theo Nhà cung cấp (Calculated Measure)",
                    BusinessPurpose = "So sánh mức giá nhập bình quân giữa các nhà cung ứng để đàm phán giá tốt nhất.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH MEMBER [Measures].[Avg Purchase Price] AS\n    IIF([Measures].[Purchase Quantity] = 0, 0, [Measures].[Total Cost] / [Measures].[Purchase Quantity]),\n    FORMAT_STRING = \"#,##0\"\nSELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity], [Measures].[Avg Purchase Price] } ON COLUMNS,\n    [Dim Supplier].[Supplier Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-29", OrderNumber = 29, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Thống kê số lượng lượt nhập hàng theo từng Kho tiếp nhận",
                    BusinessPurpose = "Đánh giá tần suất hoạt động nhập nguyên liệu và hàng hóa tại từng kho bãi.",
                    OperationType = "Warehouse Aggregation", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Fact Purchase Count], [Measures].[Purchase Quantity] } ON COLUMNS,\n    [Dim Warehouse].[Warehouse Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 5: QUẢN LÝ KHO & TỒN KHO (CÂU 30 - 35)
                new() {
                    Id = "MDX-30", OrderNumber = 30, Section = "Quản lý Kho & Tồn kho",
                    Title = "Tổng lượng Nhập, Xuất và Tồn kho hiện hành theo từng Kho hàng",
                    BusinessPurpose = "Kiểm soát dung lượng lưu trữ và dòng luân chuyển hàng hóa của từng kho.",
                    OperationType = "Inventory Status", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Quantity In], [Measures].[Quantity Out], [Measures].[Quantity On Hand] } ON COLUMNS,\n    [Dim Warehouse].[Warehouse Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-31", OrderNumber = 31, Section = "Quản lý Kho & Tồn kho",
                    Title = "Mức Tồn kho hiện tại phân bổ theo Danh mục sản phẩm",
                    BusinessPurpose = "Tránh tình trạng tồn đọng vốn quá mức ở một danh mục nhất định.",
                    OperationType = "Category Aggregation", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    [Measures].[Quantity On Hand] ON COLUMNS,\n    [Dim Product].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-32", OrderNumber = 32, Section = "Quản lý Kho & Tồn kho",
                    Title = "Cảnh báo Tồn kho chạm ngưỡng tái đặt hàng (FILTER)",
                    BusinessPurpose = "Tự động lọc các sản phẩm có Tồn kho thực tế nhỏ hơn Ngưỡng đặt hàng để kích hoạt cảnh báo mua sắm.",
                    OperationType = "Filter Condition", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Quantity On Hand], [Measures].[Reorder Level] } ON COLUMNS,\n    FILTER(\n        [Dim Product].[Product Name].MEMBERS,\n        [Measures].[Quantity On Hand] < [Measures].[Reorder Level]\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-33", OrderNumber = 33, Section = "Quản lý Kho & Tồn kho",
                    Title = "Tính toán Mức thiếu hụt Tồn kho (Stock Deficit) cho từng mặt hàng",
                    BusinessPurpose = "Tính chính xác số lượng hàng cần bổ sung thêm để đạt mức tồn kho an toàn tối thiểu.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH MEMBER [Measures].[Stock Deficit] AS\n    [Measures].[Reorder Level] - [Measures].[Quantity On Hand]\nSELECT \n    { [Measures].[Quantity On Hand], [Measures].[Reorder Level], [Measures].[Stock Deficit] } ON COLUMNS,\n    FILTER(\n        [Dim Product].[Product Name].MEMBERS,\n        [Measures].[Stock Deficit] > 0\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-34", OrderNumber = 34, Section = "Quản lý Kho & Tồn kho",
                    Title = "Top 10 Sản phẩm có số lượng Tồn kho cao nhất",
                    BusinessPurpose = "Giúp bộ phận quản lý kho lên phương án giải phóng mặt bằng kho bãi và xả hàng.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    [Measures].[Quantity On Hand] ON COLUMNS,\n    TOPCOUNT([Dim Product].[Product Name].MEMBERS, 10, [Measures].[Quantity On Hand]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-35", OrderNumber = 35, Section = "Quản lý Kho & Tồn kho",
                    Title = "Biến động lượng hàng Xuất kho theo từng Tháng (Monthly Outflow)",
                    BusinessPurpose = "Phân tích nhịp độ xuất bán hàng hóa để chủ động bố trí nhân lực đóng gói, giao hàng.",
                    OperationType = "Time Trend", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Quantity Out] } ON COLUMNS,\n    NON EMPTY [Dim Time].[Month].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 6: VẬN CHUYỂN & GIAO NHẬN (CÂU 36 - 40)
                new() {
                    Id = "MDX-36", OrderNumber = 36, Section = "Vận chuyển & Logistics",
                    Title = "Tổng Chi phí vận chuyển theo từng Đơn vị vận chuyển (Dim Shipper)",
                    BusinessPurpose = "Theo dõi ngân sách cước vận tải chi trả cho các hãng giao hàng (GHTK, Viettel Post, GHN...).",
                    OperationType = "Shipper Aggregation", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Shipping Cost], [Measures].[Fact Shipping Count] } ON COLUMNS,\n    [Dim Shipper].[Shipper Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-37", OrderNumber = 37, Section = "Vận chuyển & Logistics",
                    Title = "Hiệu suất giao hàng theo Phương thức vận chuyển",
                    BusinessPurpose = "So sánh cước phí và thời gian giao trung bình giữa các gói dịch vụ vận chuyển.",
                    OperationType = "Method Breakdown", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Shipping Cost], [Measures].[Delivery Days] } ON COLUMNS,\n    [Dim Shipper].[Shipping Method].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-38", OrderNumber = 38, Section = "Vận chuyển & Logistics",
                    Title = "Thống kê Tổng số ngày trễ hẹn giao hàng (Late Days) theo Đơn vị vận chuyển",
                    BusinessPurpose = "Đo lường chỉ số SLA và uy tín cam kết giao hàng đúng hẹn của các đối tác vận tải.",
                    OperationType = "SLA Performance", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Late Days], [Measures].[Fact Shipping Count] } ON COLUMNS,\n    ORDER([Dim Shipper].[Shipper Name].MEMBERS, [Measures].[Late Days], BDESC) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-39", OrderNumber = 39, Section = "Vận chuyển & Logistics",
                    Title = "Chi phí vận chuyển phân bổ theo Khu vực giao nhận (Region)",
                    BusinessPurpose = "Phân tích chi phí logistics phân bổ đến các vùng miền để tối ưu kho trung chuyển vệ tinh.",
                    OperationType = "Geographic Aggregation", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Shipping Cost] ON COLUMNS,\n    [Dim Location].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-40", OrderNumber = 40, Section = "Vận chuyển & Logistics",
                    Title = "Tỷ lệ Chi phí vận chuyển trên Tổng Doanh thu bán hàng (Logistics Cost Ratio)",
                    BusinessPurpose = "Chỉ số kiểm soát chi phí chuỗi cung ứng, đảm bảo cước vận chuyển không ăn mòn lợi nhuận biên.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Shipping Ratio Pct] AS\n    IIF([Measures].[Sales Amount] = 0, 0, ([Measures].[Shipping Cost] / [Measures].[Sales Amount]) * 100),\n    FORMAT_STRING = \"0.00%\"\nSELECT \n    { [Measures].[Sales Amount], [Measures].[Shipping Cost], [Measures].[Shipping Ratio Pct] } ON COLUMNS,\n    [Dim Location].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                }
            };
        }
    }
}
