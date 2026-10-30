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
        Task<MdxTable> ExecuteMdxTableAsync(string mdxQuery);
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

        /// <summary>
        /// Chạy MDX và trả bảng thô giữ nguyên tên cột MSOLAP (vd "[Dim Time].[Year].[Year].[MEMBER_CAPTION]", "[Measures].[Sales Amount]")
        /// để lớp phân tích đọc đúng cột theo tên duy nhất. Không mở được kết nối thì ném SsasUnavailableException.
        /// </summary>
        public async Task<MdxTable> ExecuteMdxTableAsync(string mdxQuery)
        {
            OleDbConnection conn;
            try
            {
                conn = new OleDbConnection(WithConnectTimeout(_ssasConnectionString));
                await conn.OpenAsync();
            }
            catch (Exception ex)
            {
                throw new SsasUnavailableException(ex.Message, ex);
            }

            using (conn)
            {
                using var cmd = new OleDbCommand(mdxQuery, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                var columns = new string[reader.FieldCount];
                for (int i = 0; i < columns.Length; i++) columns[i] = reader.GetName(i);
                var rows = new List<object?[]>();
                while (await reader.ReadAsync())
                {
                    var row = new object?[columns.Length];
                    for (int i = 0; i < columns.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
                return new MdxTable(columns, rows);
            }
        }

        // SSAS không chạy thì chờ tối đa 5 giây rồi chuyển sang SQL, không treo cả trang
        private static string WithConnectTimeout(string cs) =>
            cs.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase) ? cs : cs.TrimEnd(';') + ";Connect Timeout=5;";

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
                    BusinessPurpose = "Báo cáo tăng trưởng dài hạn cho Ban Giám đốc. Dữ liệu cấp dòng đơn hàng theo ngày được cộng gộp lên cấp năm.",
                    OperationType = "Roll-up", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Quantity] } ON COLUMNS,\n    NON EMPTY [Dim Time].[PhanCap_ThoiGian].[Year].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-02", OrderNumber = 2, Section = "Bán hàng & Doanh thu",
                    Title = "Doanh thu năm 2025 khoan sâu xuống Quý và Tháng",
                    BusinessPurpose = "Ban Giám đốc theo dõi tiến độ doanh thu năm gần nhất có đủ dữ liệu, từ tổng năm xuống từng quý rồi từng tháng.",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    DESCENDANTS(\n        [Dim Time].[PhanCap_ThoiGian].[Year].&[2025],\n        [Dim Time].[PhanCap_ThoiGian].[Month],\n        SELF_AND_BEFORE\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-03", OrderNumber = 3, Section = "Bán hàng & Doanh thu",
                    Title = "Doanh số bán hàng chi tiết theo từng Tháng trong năm (Xu hướng chu kỳ mùa vụ)",
                    BusinessPurpose = "Đánh giá biến động doanh số qua các tháng (gộp mọi năm) để lập kế hoạch nhập hàng theo mùa vụ.",
                    OperationType = "Time Trend", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Time].[Month].[Month].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-04", OrderNumber = 4, Section = "Bán hàng & Doanh thu",
                    Title = "Doanh thu và Lợi nhuận theo từng Danh mục sản phẩm (Category)",
                    BusinessPurpose = "Xác định nhóm hàng mũi nhọn trong 7 danh mục thiết bị điện – tự động hóa (Biến tần, Thiết bị tự động hóa, Thiết bị điều khiển, …).",
                    OperationType = "Category Aggregation", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-05", OrderNumber = 5, Section = "Bán hàng & Doanh thu",
                    Title = "Khoan sâu từ Danh mục \"Biến tần\" xuống Phân nhóm sản phẩm",
                    BusinessPurpose = "Danh mục Biến tần chiếm hơn một nửa doanh thu. Trưởng phòng Kinh doanh cần biết phân nhóm nào tạo ra doanh thu đó.",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Product].[Hierarchy].[Category].&[Biến tần].CHILDREN ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-06", OrderNumber = 6, Section = "Bán hàng & Doanh thu",
                    Title = "Top 10 Sản phẩm mang lại Doanh thu cao nhất (Top-N Analysis)",
                    BusinessPurpose = "Lọc ra 10 mặt hàng đóng góp doanh thu lớn nhất toàn công ty.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,\n    TOPCOUNT([Dim Product].[Product Name].[Product Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-07", OrderNumber = 7, Section = "Bán hàng & Doanh thu",
                    Title = "Top 10 Sản phẩm có Lợi nhuận gộp cao nhất",
                    BusinessPurpose = "Phát hiện 10 sản phẩm sinh lời tốt nhất, độc lập với doanh số.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Profit Amount], [Measures].[Sales Amount] } ON COLUMNS,\n    TOPCOUNT([Dim Product].[Product Name].[Product Name].MEMBERS, 10, [Measures].[Profit Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-08", OrderNumber = 8, Section = "Bán hàng & Doanh thu",
                    Title = "Top 5 Sản phẩm bán chậm nhất (Bottom-N Analysis)",
                    BusinessPurpose = "Nhận diện mặt hàng bán chậm để có giải pháp xả hàng hoặc ngừng nhập.",
                    OperationType = "Bottom-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Quantity], [Measures].[Sales Amount] } ON COLUMNS,\n    BOTTOMCOUNT(\n        NONEMPTY([Dim Product].[Product Name].[Product Name].MEMBERS, [Measures].[Quantity]),\n        5, [Measures].[Quantity]\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-09", OrderNumber = 9, Section = "Bán hàng & Doanh thu",
                    Title = "Cơ cấu Doanh thu và Chiết khấu theo Kênh bán hàng (Sales Channel)",
                    BusinessPurpose = "So sánh hiệu quả 4 kênh: Đại lý, Telesale trực tiếp, Website Khang Nghị và Cửa hàng.",
                    OperationType = "Dimension Slicing", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,\n    NON EMPTY [Dim Sales Channel].[Channel Name].[Channel Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-10", OrderNumber = 10, Section = "Bán hàng & Doanh thu",
                    Title = "Phân bổ Doanh thu theo Hình thức thanh toán (Payment Method)",
                    BusinessPurpose = "Đánh giá cơ cấu thanh toán của khách hàng để quản lý công nợ.",
                    OperationType = "Dimension Slicing", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Payment Method].[Method Type].[Method Type].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 2: KHÁCH HÀNG & THỊ TRƯỜNG ĐỊA LÝ (CÂU 11 - 18)
                new() {
                    Id = "MDX-11", OrderNumber = 11, Section = "Khách hàng & Thị trường",
                    Title = "Doanh thu theo Phân khúc khách hàng (Segment)",
                    BusinessPurpose = "Đo tỷ trọng doanh số của 6 phân khúc: Hộ kinh doanh nhỏ lẻ, Xưởng sản xuất, Nhà thầu xây dựng, Thợ điện dân dụng, Nhà máy và Chủ nhà.",
                    OperationType = "Customer Segmentation", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Customer].[Segment].[Segment].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-12", OrderNumber = 12, Section = "Khách hàng & Thị trường",
                    Title = "Top 10 Khách hàng có giá trị mua hàng lớn nhất",
                    BusinessPurpose = "Chăm sóc khách hàng trọng điểm và xây dựng chính sách chiết khấu riêng.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,\n    TOPCOUNT([Dim Customer].[Customer Name].[Customer Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-13", OrderNumber = 13, Section = "Khách hàng & Thị trường",
                    Title = "Doanh thu và Lợi nhuận theo Khu vực địa lý (Miền Bắc, Miền Trung, Miền Nam)",
                    BusinessPurpose = "Đánh giá hiệu quả kinh doanh theo vùng miền.",
                    OperationType = "Geographic Aggregation", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-14", OrderNumber = 14, Section = "Khách hàng & Thị trường",
                    Title = "Khoan sâu Doanh số từ \"Miền Nam\" xuống Tỉnh/Thành phố",
                    BusinessPurpose = "Miền Nam chiếm khoảng 66% doanh thu. Cần biết tỉnh/thành nào trong vùng tiêu thụ mạnh nhất.",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY DESCENDANTS(\n        [Dim Location].[PhanCap_Diali].[Region].&[Miền Nam],\n        [Dim Location].[PhanCap_Diali].[City]\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-15", OrderNumber = 15, Section = "Khách hàng & Thị trường",
                    Title = "Doanh thu sản phẩm (theo danh mục) trong năm 2025",
                    BusinessPurpose = "Cố định một lát cắt trên chiều Thời gian (năm 2025) bằng mệnh đề WHERE để xem cơ cấu doanh thu sản phẩm trong năm.",
                    OperationType = "Slice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Dim Time].[Year].&[2025] );"
                },
                new() {
                    Id = "MDX-16", OrderNumber = 16, Section = "Khách hàng & Thị trường",
                    Title = "Doanh thu sản phẩm đồng thời theo Năm 2025 và Khu vực \"Miền Nam\"",
                    BusinessPurpose = "Thu hẹp khối dữ liệu theo đồng thời hai chiều (Thời gian và Địa lý) để đánh giá thị trường trọng điểm trong năm gần nhất.",
                    OperationType = "Dice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE (\n    [Dim Time].[Year].&[2025],\n    [Dim Location].[Region].&[Miền Nam]\n);"
                },
                new() {
                    Id = "MDX-17", OrderNumber = 17, Section = "Khách hàng & Thị trường",
                    Title = "Thao tác PIVOT (Xoay chiều) – Ma trận chéo giữa Danh mục sản phẩm và Vùng miền",
                    BusinessPurpose = "Đưa Vùng miền lên cột và Danh mục xuống dòng để so sánh tỷ trọng chéo.",
                    OperationType = "Pivot", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Measures].[Sales Amount] );"
                },
                new() {
                    Id = "MDX-18", OrderNumber = 18, Section = "Khách hàng & Thị trường",
                    Title = "Top 5 Thành phố đóng góp Lợi nhuận cao nhất cả nước",
                    BusinessPurpose = "Xác định các đô thị sinh lời cao nhất để ưu tiên nguồn lực bán hàng.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Profit Amount] ON COLUMNS,\n    TOPCOUNT([Dim Location].[City].[City].MEMBERS, 5, [Measures].[Profit Amount]) ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 3: NHÂN SỰ, KHUYẾN MÃI & TÍNH TOÁN NÂNG CAO (CÂU 19 - 24)
                new() {
                    Id = "MDX-19", OrderNumber = 19, Section = "Nhân sự & Khuyến mãi",
                    Title = "Đánh giá KPI Doanh số bán hàng theo từng Nhân viên (Dim Employee)",
                    BusinessPurpose = "Đánh giá năng suất đội ngũ bán hàng làm căn cứ tính hoa hồng.",
                    OperationType = "Ranking Order", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    ORDER(\n        NONEMPTY([Dim Employee].[Employee Name].[Employee Name].MEMBERS, [Measures].[Sales Amount]),\n        [Measures].[Sales Amount], BDESC\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-20", OrderNumber = 20, Section = "Nhân sự & Khuyến mãi",
                    Title = "Đánh giá hiệu quả các chương trình Khuyến mãi (Promotion Name)",
                    BusinessPurpose = "So sánh tiền chiết khấu đã chi với doanh thu mang lại của từng chương trình.",
                    OperationType = "Promotion Analysis", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,\n    NON EMPTY [Dim Promotion].[Promotion Name].[Promotion Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-21", OrderNumber = 21, Section = "Nhân sự & Khuyến mãi",
                    Title = "Doanh số sản phẩm bán ra theo Hãng sản xuất (Dim Manufacturer)",
                    BusinessPurpose = "Xác định thương hiệu được thị trường ưa chuộng để ưu tiên nhập hàng.",
                    OperationType = "Manufacturer Breakdown", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,\n    NON EMPTY [Dim Manufacturer].[Manufacturer Name].[Manufacturer Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-22", OrderNumber = 22, Section = "Nhân sự & Khuyến mãi",
                    Title = "Calculated Member – Tỷ suất lợi nhuận (Profit Margin %) theo Danh mục",
                    BusinessPurpose = "Dùng WITH MEMBER tính Lợi nhuận / Doanh thu tức thời mà không sửa CSDL.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Profit Margin Pct] AS\n    IIF([Measures].[Sales Amount] = 0, NULL, [Measures].[Profit Amount] / [Measures].[Sales Amount]),\n    FORMAT_STRING = \"0.00%\"\nSELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-23", OrderNumber = 23, Section = "Nhân sự & Khuyến mãi",
                    Title = "Phân tích Doanh thu theo Gói Dịch vụ đính kèm (Dim Service)",
                    BusinessPurpose = "Đo lường doanh thu từ các gói bảo hành, dịch vụ vận chuyển và lắp đặt hoàn thiện.",
                    OperationType = "Service Breakdown", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Service].[Service Name].[Service Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-24", OrderNumber = 24, Section = "Nhân sự & Khuyến mãi",
                    Title = "Ma trận CROSSJOIN giữa Kênh bán hàng và Quý của năm 2025",
                    BusinessPurpose = "Đối chiếu diễn biến doanh thu của từng kênh qua các quý năm 2025.",
                    OperationType = "Crossjoin", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    NON EMPTY [Dim Time].[Quarter].[Quarter].MEMBERS ON COLUMNS,\n    NON EMPTY CROSSJOIN(\n        [Dim Sales Channel].[Channel Name].[Channel Name].MEMBERS,\n        { [Measures].[Sales Amount] }\n    ) ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Dim Time].[Year].&[2025] );"
                },

                // MỤC 4: MUA HÀNG & NHÀ CUNG CẤP (CÂU 25 - 29)
                new() {
                    Id = "MDX-25", OrderNumber = 25, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Tổng Chi phí mua hàng và Số lượng nhập theo từng Năm",
                    BusinessPurpose = "Kiểm soát dòng tiền chi mua hàng qua các năm tài chính.",
                    OperationType = "Roll-up", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,\n    NON EMPTY [Dim Time].[Year].[Year].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-26", OrderNumber = 26, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Top 5 Nhà cung cấp có giá trị mua vào lớn nhất",
                    BusinessPurpose = "Đánh giá mức độ phụ thuộc vào các nhà cung ứng trọng yếu.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,\n    TOPCOUNT([Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS, 5, [Measures].[Total Cost]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-27", OrderNumber = 27, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Chi phí nhập hàng phân bổ theo từng Danh mục sản phẩm",
                    BusinessPurpose = "Đối chiếu tỷ trọng vốn nhập hàng giữa các nhóm sản phẩm.",
                    OperationType = "Category Aggregation", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    [Measures].[Total Cost] ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-28", OrderNumber = 28, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Đơn giá bình quân nhập hàng theo Nhà cung cấp (Calculated Measure)",
                    BusinessPurpose = "So sánh giá nhập bình quân giữa các nhà cung ứng để đàm phán giá.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH MEMBER [Measures].[Avg Purchase Price] AS\n    IIF([Measures].[Purchase Quantity] = 0, NULL, [Measures].[Total Cost] / [Measures].[Purchase Quantity]),\n    FORMAT_STRING = \"#,##0\"\nSELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity], [Measures].[Avg Purchase Price] } ON COLUMNS,\n    NON EMPTY [Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-29", OrderNumber = 29, Section = "Mua hàng & Nhà cung cấp",
                    Title = "Số lượt nhập hàng theo từng Kho tiếp nhận (Dim Warehouse)",
                    BusinessPurpose = "Đánh giá tần suất nhập hàng tại từng kho.",
                    OperationType = "Warehouse Aggregation", SourceFactTable = "Fact_Purchase", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Fact Purchase Count], [Measures].[Purchase Quantity] } ON COLUMNS,\n    NON EMPTY [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 5: QUẢN LÝ KHO & TỒN KHO (CÂU 30 - 35)
                new() {
                    Id = "MDX-30", OrderNumber = 30, Section = "Quản lý Kho & Tồn kho",
                    Title = "Tổng Nhập, Xuất và Tồn kho cuối kỳ theo từng Kho hàng",
                    BusinessPurpose = "Thủ kho kiểm soát dòng luân chuyển và tồn hiện hành của từng kho.",
                    OperationType = "Inventory Status", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH MEMBER [Measures].[Ton Cuoi Ky] AS\n    ( [Measures].[Quantity On Hand],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )\nSELECT \n    { [Measures].[Quantity In], [Measures].[Quantity Out], [Measures].[Ton Cuoi Ky] } ON COLUMNS,\n    NON EMPTY [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-31", OrderNumber = 31, Section = "Quản lý Kho & Tồn kho",
                    Title = "Tồn kho cuối kỳ phân bổ theo Danh mục sản phẩm",
                    BusinessPurpose = "Tránh tồn đọng vốn quá mức ở một danh mục.",
                    OperationType = "Category Aggregation", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH MEMBER [Measures].[Ton Cuoi Ky] AS\n    ( [Measures].[Quantity On Hand],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )\nSELECT \n    [Measures].[Ton Cuoi Ky] ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-32", OrderNumber = 32, Section = "Quản lý Kho & Tồn kho",
                    Title = "Cảnh báo Tồn kho chạm ngưỡng tái đặt hàng (FILTER + WITH MEMBER)",
                    BusinessPurpose = "Lọc các sản phẩm có tồn kỳ cuối **nhỏ hơn hoặc bằng** ngưỡng tái đặt hàng để Trưởng phòng Mua hàng lên kế hoạch nhập.",
                    OperationType = "Filter Condition", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH \nMEMBER [Measures].[Ton Cuoi Ky] AS\n    ( [Measures].[Quantity On Hand],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )\nMEMBER [Measures].[Nguong Cuoi Ky] AS\n    ( [Measures].[Reorder Level],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Reorder Level]), 1).ITEM(0) )\nSELECT \n    { [Measures].[Ton Cuoi Ky], [Measures].[Nguong Cuoi Ky] } ON COLUMNS,\n    FILTER(\n        NONEMPTY([Dim Product].[Product Name].[Product Name].MEMBERS, [Measures].[Quantity On Hand]),\n        [Measures].[Ton Cuoi Ky] <= [Measures].[Nguong Cuoi Ky]\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-33", OrderNumber = 33, Section = "Quản lý Kho & Tồn kho",
                    Title = "Mức thiếu hụt so với tồn kho an toàn (Stock Deficit) cho từng mặt hàng",
                    BusinessPurpose = "Tính số lượng cần bổ sung để đạt **gấp đôi** ngưỡng tái đặt hàng (mức an toàn đề xuất). Hệ số 2 là tham số nghiệp vụ, có thể điều chỉnh.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH \nMEMBER [Measures].[Ton Cuoi Ky] AS\n    ( [Measures].[Quantity On Hand],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )\nMEMBER [Measures].[Nguong Cuoi Ky] AS\n    ( [Measures].[Reorder Level],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Reorder Level]), 1).ITEM(0) )\nMEMBER [Measures].[Stock Deficit] AS\n    2 * [Measures].[Nguong Cuoi Ky] - [Measures].[Ton Cuoi Ky]\nSELECT \n    { [Measures].[Ton Cuoi Ky], [Measures].[Nguong Cuoi Ky], [Measures].[Stock Deficit] } ON COLUMNS,\n    FILTER(\n        NONEMPTY([Dim Product].[Product Name].[Product Name].MEMBERS, [Measures].[Quantity On Hand]),\n        [Measures].[Stock Deficit] > 0\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-34", OrderNumber = 34, Section = "Quản lý Kho & Tồn kho",
                    Title = "Top 10 Sản phẩm có Tồn kho cuối kỳ cao nhất (Nguy cơ ứ đọng vốn)",
                    BusinessPurpose = "Giúp quản lý kho lên phương án giải phóng hàng tồn.",
                    OperationType = "Top-N", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "WITH MEMBER [Measures].[Ton Cuoi Ky] AS\n    ( [Measures].[Quantity On Hand],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )\nSELECT \n    [Measures].[Ton Cuoi Ky] ON COLUMNS,\n    TOPCOUNT([Dim Product].[Product Name].[Product Name].MEMBERS, 10, [Measures].[Ton Cuoi Ky]) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-35", OrderNumber = 35, Section = "Quản lý Kho & Tồn kho",
                    Title = "Biến động lượng hàng Xuất kho theo từng Tháng của năm 2025",
                    BusinessPurpose = "Phân tích nhịp độ xuất kho để bố trí nhân lực đóng gói và giao hàng. Quantity Out là độ đo cộng dồn được, nên được phép Sum theo thời gian.",
                    OperationType = "Time Trend", SourceFactTable = "Fact_Inventory", VerificationStatus = "DESIGN_TEMPLATE",
                    MdxQuery = "SELECT \n    { [Measures].[Quantity Out] } ON COLUMNS,\n    NON EMPTY [Dim Time].[Month].[Month].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Dim Time].[Year].&[2025] );"
                },

                // MỤC 6: VẬN CHUYỂN & GIAO NHẬN (CÂU 36 - 40)
                new() {
                    Id = "MDX-36", OrderNumber = 36, Section = "Vận chuyển & Logistics",
                    Title = "Tổng Chi phí vận chuyển theo từng Đơn vị/Phương thức vận chuyển",
                    BusinessPurpose = "Theo dõi ngân sách cước vận chuyển.",
                    OperationType = "Shipper Aggregation", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Shipping Cost], [Measures].[Fact Shipping Count] } ON COLUMNS,\n    NON EMPTY [Dim Shipper].[Shipper Name].[Shipper Name].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-37", OrderNumber = 37, Section = "Vận chuyển & Logistics",
                    Title = "Hiệu suất giao hàng theo Phương thức vận chuyển",
                    BusinessPurpose = "So sánh cước phí và **số ngày giao trung bình** giữa các phương thức.",
                    OperationType = "Method Breakdown", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Avg Delivery Days] AS\n    IIF([Measures].[Fact Shipping Count] = 0, NULL,\n        [Measures].[Delivery Days] / [Measures].[Fact Shipping Count]),\n    FORMAT_STRING = \"0.00\"\nSELECT \n    { [Measures].[Shipping Cost], [Measures].[Fact Shipping Count], [Measures].[Avg Delivery Days] } ON COLUMNS,\n    NON EMPTY [Dim Shipper].[Shipping Method].[Shipping Method].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-38", OrderNumber = 38, Section = "Vận chuyển & Logistics",
                    Title = "Tổng số ngày trễ hẹn giao hàng (Late Days) theo Đơn vị vận chuyển",
                    BusinessPurpose = "Đo cam kết giao hàng đúng hẹn (SLA).",
                    OperationType = "SLA Performance", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Late Days], [Measures].[Fact Shipping Count] } ON COLUMNS,\n    ORDER(\n        NONEMPTY([Dim Shipper].[Shipper Name].[Shipper Name].MEMBERS, [Measures].[Fact Shipping Count]),\n        [Measures].[Late Days], BDESC\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-39", OrderNumber = 39, Section = "Vận chuyển & Logistics",
                    Title = "Chi phí vận chuyển phân bổ theo Khu vực giao nhận (Region)",
                    BusinessPurpose = "Phân tích chi phí logistics theo vùng miền.",
                    OperationType = "Geographic Aggregation", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Shipping Cost] ON COLUMNS,\n    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-40", OrderNumber = 40, Section = "Vận chuyển & Logistics",
                    Title = "Tỷ lệ Chi phí vận chuyển trên Doanh thu (Logistics Cost Ratio)",
                    BusinessPurpose = "Kiểm soát để cước vận chuyển không bào mòn lợi nhuận.",
                    OperationType = "Calculated Member", SourceFactTable = "Fact_Shipping", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Shipping Ratio Pct] AS\n    IIF([Measures].[Sales Amount] = 0, NULL, [Measures].[Shipping Cost] / [Measures].[Sales Amount]),\n    FORMAT_STRING = \"0.00%\"\nSELECT \n    { [Measures].[Sales Amount], [Measures].[Shipping Cost], [Measures].[Shipping Ratio Pct] } ON COLUMNS,\n    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },

                // MỤC 7: THAO TÁC OLAP THEO VAI TRÒ NGƯỜI DÙNG (CÂU 41 - 50)
                new() {
                    Id = "MDX-41", OrderNumber = 41, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Gộp doanh thu từ Thành phố lên Khu vực",
                    BusinessPurpose = "Giám đốc: Mỗi miền đóng góp bao nhiêu doanh thu và lợi nhuận?",
                    OperationType = "Roll-up", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Location].[PhanCap_Diali].[Region].MEMBERS ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-42", OrderNumber = 42, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Gộp doanh thu từ Sản phẩm lên Danh mục, kèm tỷ suất lợi nhuận",
                    BusinessPurpose = "Giám đốc, Trưởng phòng Kinh doanh: Ngành hàng nào vừa lớn về doanh thu vừa có biên lợi nhuận tốt?",
                    OperationType = "Roll-up", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Profit Margin Pct] AS\n    IIF([Measures].[Sales Amount] = 0, NULL, [Measures].[Profit Amount] / [Measures].[Sales Amount]),\n    FORMAT_STRING = \"0.00%\"\nSELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,\n    ORDER(\n        NONEMPTY([Dim Product].[Hierarchy].[Category].MEMBERS, [Measures].[Sales Amount]),\n        [Measures].[Sales Amount], BDESC\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-43", OrderNumber = 43, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Khoan sâu Quý 4/2025 xuống từng Tháng",
                    BusinessPurpose = "Giám đốc: Quý 4/2025 tăng mạnh nhất năm, tháng nào đóng góp nhiều nhất?",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Time].[PhanCap_ThoiGian].[Year].&[2025].&[4].CHILDREN ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-44", OrderNumber = 44, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Doanh thu sản phẩm tại riêng \"Miền Nam\" (mọi năm)",
                    BusinessPurpose = "Trưởng phòng Kinh doanh: Ở thị trường lớn nhất, nhóm hàng nào bán chạy?",
                    OperationType = "Slice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Dim Location].[Region].&[Miền Nam] );"
                },
                new() {
                    Id = "MDX-45", OrderNumber = 45, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Doanh thu theo năm của riêng kênh \"Website Khang Nghị\"",
                    BusinessPurpose = "Trưởng phòng Kinh doanh: Kênh bán hàng trực tuyến tăng trưởng thế nào qua các năm?",
                    OperationType = "Slice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,\n    NON EMPTY [Dim Time].[Year].[Year].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Dim Sales Channel].[Channel Name].&[Website Khang Nghị] );"
                },
                new() {
                    Id = "MDX-46", OrderNumber = 46, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Doanh thu sản phẩm theo năm 2024–2025 × Miền Bắc, Miền Trung × kênh Đại lý, Cửa hàng",
                    BusinessPurpose = "Trưởng phòng Kinh doanh: Trong 2 năm gần nhất, kênh trực tiếp (Đại lý, Cửa hàng) ở thị trường ngoài Miền Nam bán nhóm hàng nào?",
                    OperationType = "Dice", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    [Measures].[Sales Amount] ON COLUMNS,\n    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE (\n    { [Dim Time].[Year].&[2024], [Dim Time].[Year].&[2025] },\n    { [Dim Location].[Region].&[Miền Bắc], [Dim Location].[Region].&[Miền Trung] },\n    { [Dim Sales Channel].[Channel Name].&[Đại lý], [Dim Sales Channel].[Channel Name].&[Cửa hàng] }\n);"
                },
                new() {
                    Id = "MDX-47", OrderNumber = 47, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Giá trị nhập hàng theo Nhà cung cấp, năm 2025, tại \"Kho TP. Hồ Chí Minh\"",
                    BusinessPurpose = "Trưởng phòng Mua hàng: Năm 2025, kho chính nhập hàng chủ yếu từ nhà cung cấp nào?",
                    OperationType = "Dice", SourceFactTable = "Fact_Purchase", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,\n    ORDER(\n        NONEMPTY([Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS, [Measures].[Total Cost]),\n        [Measures].[Total Cost], BDESC\n    ) ON ROWS\nFROM [KhangNghi_Cube]\nWHERE (\n    [Dim Time].[Year].&[2025],\n    [Dim Warehouse].[Warehouse Name].&[Kho TP. Hồ Chí Minh]\n);"
                },
                new() {
                    Id = "MDX-48", OrderNumber = 48, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Ma trận Năm × Khu vực",
                    BusinessPurpose = "Giám đốc: Doanh thu từng miền thay đổi thế nào qua các năm? (Năm trên dòng, Miền trên cột. Đổi trục để xoay góc nhìn.)",
                    OperationType = "Pivot", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "SELECT \n    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON COLUMNS,\n    NON EMPTY [Dim Time].[Year].[Year].MEMBERS ON ROWS\nFROM [KhangNghi_Cube]\nWHERE ( [Measures].[Sales Amount] );"
                },
                new() {
                    Id = "MDX-49", OrderNumber = 49, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Tăng trưởng doanh thu từng quý 2025 so với cùng kỳ 2024",
                    BusinessPurpose = "Giám đốc: Quý nào năm 2025 tăng trưởng mạnh nhất so với cùng kỳ năm trước?",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Sales", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH \nMEMBER [Measures].[Doanh Thu Cung Ky] AS\n    ( [Measures].[Sales Amount],\n      PARALLELPERIOD([Dim Time].[PhanCap_ThoiGian].[Year], 1,\n                     [Dim Time].[PhanCap_ThoiGian].CURRENTMEMBER) ),\n    FORMAT_STRING = \"#,##0\"\nMEMBER [Measures].[Tang Truong YoY] AS\n    IIF([Measures].[Doanh Thu Cung Ky] = 0, NULL,\n        [Measures].[Sales Amount] / [Measures].[Doanh Thu Cung Ky] - 1),\n    FORMAT_STRING = \"0.00%\"\nSELECT \n    { [Measures].[Sales Amount], [Measures].[Doanh Thu Cung Ky], [Measures].[Tang Truong YoY] } ON COLUMNS,\n    [Dim Time].[PhanCap_ThoiGian].[Year].&[2025].CHILDREN ON ROWS\nFROM [KhangNghi_Cube];"
                },
                new() {
                    Id = "MDX-50", OrderNumber = 50, Section = "Xem tổng hợp, chi tiết & lọc",
                    Title = "Tồn kho cuối kỳ của từng kho, khoan sâu theo Danh mục",
                    BusinessPurpose = "Quản lý kho: Tại kỳ kiểm kê gần nhất, mỗi kho đang giữ bao nhiêu hàng của từng danh mục?",
                    OperationType = "Drill-down", SourceFactTable = "Fact_Inventory", VerificationStatus = "VERIFIED",
                    MdxQuery = "WITH MEMBER [Measures].[Ton Cuoi Ky] AS\n    ( [Measures].[Quantity On Hand],\n      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )\nSELECT \n    [Measures].[Ton Cuoi Ky] ON COLUMNS,\n    NON EMPTY CROSSJOIN(\n        [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS,\n        [Dim Product].[Category].[Category].MEMBERS\n    ) ON ROWS\nFROM [KhangNghi_Cube];"
                }
            };
        }
    }

    /// <summary>Không kết nối được SSAS (dịch vụ tắt, sai chuỗi kết nối, hoặc hệ điều hành không có MSOLAP).</summary>
    public class SsasUnavailableException : Exception
    {
        public SsasUnavailableException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Kết quả MDX dạng bảng phẳng; tra cột theo tên duy nhất của thuộc tính hoặc độ đo.</summary>
    public sealed class MdxTable
    {
        public IReadOnlyList<string> Columns { get; }
        public List<object?[]> Rows { get; }

        public MdxTable(IReadOnlyList<string> columns, List<object?[]> rows)
        {
            Columns = columns;
            Rows = rows;
        }

        /// <summary>
        /// Vị trí cột: "[Measures].[Sales Amount]" cho độ đo, "[Dim Time].[Year]" cho caption của thuộc tính
        /// (khớp "[Dim Time].[Year].[Year].[MEMBER_CAPTION]"). Không có thì ném lỗi để chuyển sang SQL.
        /// </summary>
        public int Col(string name)
        {
            static string Norm(string x) => x.Replace(" ", string.Empty).ToLowerInvariant();
            var n = Norm(name);
            for (int i = 0; i < Columns.Count; i++)
                if (Norm(Columns[i]) == n) return i;
            for (int i = 0; i < Columns.Count; i++)
            {
                var c = Norm(Columns[i]);
                if (c.StartsWith(n + ".") && c.EndsWith("[member_caption]") && !c.Contains("[(all)]")) return i;
            }
            throw new InvalidOperationException($"Kết quả MDX không có cột {name}. Các cột: {string.Join(", ", Columns)}");
        }
    }
}
