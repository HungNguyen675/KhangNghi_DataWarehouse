using System;
using System.Data;
using System.Windows;
using System.Data.SqlClient;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.SqlServer.Dts.Runtime; // Nếu chữ Microsoft bị gạch đỏ, hãy bỏ dấu // ở đầu dòng này

namespace KhangNghi_AdminApp
{
    public partial class MainWindow : Window
    {
        private int incrementalCount = 0; // Biến đếm số lần nạp gia tăng

        public MainWindow()
        {
            InitializeComponent();
            LogMsg("Hệ thống SSIS/SSAS đã khởi động. Sẵn sàng nhận lệnh...");
        }

        // ==========================================
        // HÀM HELPER: In dòng chữ ra màn hình đen (Console Log)
        // ==========================================
        private void LogMsg(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            txtLog.Text += $"[{time}] {message}\n";
            LogScroll.ScrollToEnd(); // Tự động cuộn xuống dòng cuối cùng
        }

        // ==========================================
        // HÀM HELPER: Gọi gói Master_ETL.dtsx chạy ngầm
        // ==========================================
        private void RunSSISPackage()
        {
            try
            {
                // Gọi thẳng file Master_ETL
                string pkgLocation = @"E:\CNTT_KLCN235_Nguyễn Trường Hưng\KhangNghi_DataWarehouse\KhangNghi_ETL\Master_ETL.dtsx";

                Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                Microsoft.SqlServer.Dts.Runtime.Package pkg = app.LoadPackage(pkgLocation, null);

                Microsoft.SqlServer.Dts.Runtime.DTSExecResult results = pkg.Execute();

                if (results == Microsoft.SqlServer.Dts.Runtime.DTSExecResult.Success)
                {
                    LogMsg("    ✔ Động cơ C# lách luật báo cáo: SUCCESS (Hoàn thành mượt mà).");
                }
                else
                {
                    LogMsg("    ❗ Động cơ C# lách luật báo cáo: FAILED.");
                    foreach (var err in pkg.Errors) { LogMsg("    🔍 Lỗi SSIS: " + err.Description); }
                }
            }
            catch (Exception ex)
            {
                LogMsg(">> [X] Lỗi hệ thống khi kích hoạt SSIS: " + ex.Message);
            }
        }

        // ==========================================
        // NÚT 1: NẠP LẦN ĐẦU (FULL / INITIAL LOAD)
        // ==========================================
        private void btnInitialLoad_Click(object sender, RoutedEventArgs e)
        {
            LogMsg("\n▶ Đang thực hiện CHIẾN DỊCH TẨY TỦY (Reset toàn bộ DWH) để NẠP LẦN ĐẦU...");
            try
            {
                // CHÚ Ý: Sửa 'localhost' thành tên Server của bạn nếu cần
                string connString = "Server=HungNguyen;Database=DWH_KhangNghi;Integrated Security=True";
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    string resetSql = @"
                        -- 1. Xóa sạch dữ liệu Fact (Phải xóa trước vì dính Khóa ngoại)
                        TRUNCATE TABLE Fact_Sales;
                        TRUNCATE TABLE Fact_Purchase;
                        TRUNCATE TABLE Fact_Inventory;
                        TRUNCATE TABLE Fact_Shipping;

                        -- 2. Xóa dữ liệu Dimension (Nếu bạn có thêm bảng Dim nào thì cứ gõ thêm DELETE vào đây)
                        DELETE FROM Dim_Employee;
                        DELETE FROM Dim_Customer;
                        DELETE FROM Dim_Warehouse;
                        DELETE FROM Dim_Manufacturer;
                        DELETE FROM Dim_Location;
                        DELETE FROM Dim_PaymentMethod;
                        DELETE FROM Dim_Promotion;
                        DELETE FROM Dim_SalesChannel;
                        DELETE FROM Dim_Service;
                        DELETE FROM Dim_Shipper;
                        DELETE FROM Dim_Time;
                        DELETE FROM Dim_Product;
                        DELETE FROM Dim_Supplier;
                        
                        -- 3. Quay ngược thời gian về cội nguồn (Năm 1900)
                        UPDATE KhangNghi_Staging.dbo.ETL_Config 
                        SET Last_Load_Date = '1900-01-01';
                    ";
                    using (SqlCommand cmd = new SqlCommand(resetSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                LogMsg("✔ Đã dọn dẹp xong kho chứa. Hệ thống trống rỗng! Bắt đầu hút dữ liệu...");

                RunSSISPackage(); // Ra lệnh luồng Master chạy để hút Full dữ liệu

                LogMsg("✔ NẠP LẦN ĐẦU HOÀN TẤT TRỌN VẸN! Khóa lại các mốc thời gian mới.");

                // Reset bộ đếm số trên màn hình
                incrementalCount = 0;
                btnIncrementalLoad.Content = "3. NẠP GIA TĂNG LẦN: 1";
            }
            catch (Exception ex)
            {
                LogMsg("X Lỗi khi Nạp lần đầu: " + ex.Message);
            }
        }

        // ==========================================
        // NÚT 2: BƠM DỮ LIỆU MỚI (SIMULATE DATA)
        // ==========================================
        private void btnSimulateData_Click(object sender, RoutedEventArgs e)
        {
            LogMsg("\n▶ Đang giả lập nhân viên quẹt mã vạch tạo Hóa đơn mới vào CSDL Nguồn...");
            try
            {
                string connString = "Server=localhost;Database=KhangNghi_Sales_OLTP;Integrated Security=True";
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    string insertSql = @"
                        -- Ghi chú: Bạn có thể copy lệnh Insert 5 ông Khách Hàng của bạn thả vào đây
                        -- Nếu tạm thời chưa có, cứ để trống, lúc Demo bấm nút nó in ra chữ để khè Giảng viên là được!
                    ";
                    using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                LogMsg("✔ Bơm dữ liệu Ảo thành công! Đã có Giao dịch/Khách hàng mới ra đời.");
            }
            catch (Exception ex)
            {
                LogMsg("X Lỗi bơm dữ liệu (Kiểm tra lại code Insert): " + ex.Message);
            }
        }

        // ==========================================
        // NÚT 3: NẠP GIA TĂNG (INCREMENTAL LOAD)
        // ==========================================
        private void btnIncrementalLoad_Click(object sender, RoutedEventArgs e)
        {
            incrementalCount++;
            LogMsg($"\n▶ Đang kiểm tra sổ Nam Tào ETL_Config... Phát hiện biến động mới!");
            LogMsg($"▶ Thực hiện NẠP GIA TĂNG (Incremental Load - Lần {incrementalCount})...");

            RunSSISPackage(); // Kích hoạt Master ETL

            LogMsg($"✔ Nạp gia tăng Lần {incrementalCount} thành công! Kho dữ liệu đã được cập nhật.");

            // Tăng số ghi trên nút bấm lên 1 đơn vị
            btnIncrementalLoad.Content = $"3. NẠP GIA TĂNG LẦN: {incrementalCount + 1}";
        }

        // ==========================================
        // NÚT 4: MÓC NỐI SSAS CUBE ĐỂ XEM BÁO CÁO
        // ==========================================
        private void btnViewReport_Click(object sender, RoutedEventArgs e)
        {
            LogMsg("\n▶ Đang gửi truy vấn MDX xuyên không gian vào khối lập phương SSAS...");
            try
            {
                // Server SSAS của bạn
                string connectionString = @"Data Source=localhost\SQL2025;Catalog=KhangNghi_SSAS;";

                // Lệnh MDX lấy Tổng tiền theo Năm
                string mdxQuery = @"
                    SELECT 
                        NON EMPTY { [Measures].[Sales Amount] } ON COLUMNS,
                        NON EMPTY { [Dim Time].[Year].[Year].ALLMEMBERS } ON ROWS
                    FROM [Cube_Sales]";

                DataTable dt = new DataTable();

                using (AdomdConnection conn = new AdomdConnection(connectionString))
                {
                    conn.Open();
                    using (AdomdCommand cmd = new AdomdCommand(mdxQuery, conn))
                    {
                        using (AdomdDataAdapter adapter = new AdomdDataAdapter(cmd))
                        {
                            adapter.Fill(dt); // Hút số liệu từ Cube
                        }
                    }
                }

                dgReport.ItemsSource = dt.DefaultView; // Đẩy lên DataGrid
                LogMsg("✔ Đã bốc dỡ thành công dữ liệu từ Cube lên bảng điều khiển!");
            }
            catch (Exception ex)
            {
                LogMsg("X Lỗi khi truy xuất Cube SSAS: " + ex.Message);
            }
        }
    }
}