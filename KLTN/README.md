# KHÓA LUẬN TỐT NGHIỆP: XÂY DỰNG KHO DỮ LIỆU HỖ TRỢ PHÂN TÍCH HIỆU QUẢ HOẠT ĐỘNG KINH DOANH CÔNG TY KHANG NGHỊ

Hệ thống Kho dữ liệu (**Enterprise Data Warehouse - DWH**) và Nền tảng Business Intelligence (**BI Analytics Platform**) đa nền tảng (**WPF Desktop Admin + Web BI SPA**) phục vụ giám sát, phân tích chuỗi cung ứng toàn diện và dự báo doanh số bán hàng bằng Machine Learning cho **Công ty Cổ phần Khang Nghị**.

---

## 📌 1. TỔNG QUAN HỆ THỐNG VÀ KIẾN TRÚC DWH

### 1.1. Mô hình Kho Dữ Liệu (Fact Constellation Schema - Mô hình Chòm Sao)
Kho dữ liệu `DWH_KhangNghi` được thiết kế theo chuẩn Kimball Enterprise Data Warehouse, kết nối đa nghiệp vụ:

* **07 Bảng Chiều (Dimensions):**
  * `Dim_Time`: Phân tích thời gian đa cấp bậc (Năm, Quý, Tháng, Tuần, Ngày, Thứ trong tuần).
  * `Dim_Location`: Địa điểm địa lý (Thành phố, Bang/Tỉnh, Quốc gia, 23 Khu vực toàn cầu).
  * `Dim_Customer`: Thông tin khách hàng, phân khúc thị trường (*Consumer, Corporate, Home Office*).
  * `Dim_Product`: Danh mục sản phẩm (*Category, SubCategory, Product Name*).
  * `Dim_Supplier`: Nhà cung cấp vật tư, hàng hóa.
  * `Dim_Manufacturer`: Hãng sản xuất thiết bị.
  * `Dim_Shipper`: Đơn vị vận chuyển và phương thức giao hàng (*First Class, Same Day, Second Class, Standard Class*).

* **04 Bảng Sự thật (Fact Tables):**
  * `Fact_Order`: Doanh thu, số lượng, chiết khấu, lợi nhuận bán hàng (~51,290 bản ghi).
  * `Fact_Purchase`: Đơn đặt hàng nhập khẩu từ nhà cung cấp (~889 bản ghi).
  * `Fact_Inventory`: Tồn kho thực tế theo kho và định mức an toàn tái đặt hàng (~2,000 bản ghi).
  * `Fact_Shipping`: Chi phí vận chuyển, thời gian giao hàng, độ trễ SLA (~25,749 bản ghi).

* **03 Bảng Quản trị & Vận hành:**
  * `Sys_User`: Quản lý tài khoản, mã hóa BCrypt, phân quyền 5 vai trò (RBAC).
  * `Sys_EtlLog`: Nhật ký theo dõi lịch sử chạy ETL (Start/End time, số bản ghi xử lý, trạng thái).
  * `Sys_Schedule`: Cấu hình lịch tự động kích hoạt ETL định kỳ.

---

## 💻 2. NỀN TẢNG 1: DESKTOP APP (WPF C# .NET 9) - DÀNH CHO ADMIN
* **Quản trị người dùng & Phân quyền (RBAC):** Tạo tài khoản, đổi mật khẩu, phân quyền cho các phòng ban.
* **Giám sát sức khỏe DWH:** Kiểm tra kết nối SQL Server, thống kê dòng dữ liệu thực tế của tất cả các bảng Dim & Fact.
* **Quy trình ETL Tốc độ cao:** 
  * Trích xuất (Extract) từ nguồn `Data.xlsx`.
  * Chuyển đổi dữ liệu (Transform) và Nạp dữ liệu (Load) gia tăng (*Incremental Load*) bằng `SqlBulkCopy` và `MERGE`.
  * Thanh tiến trình (Progress Bar) và Console Log chi tiết thời gian thực.
* **Lập lịch tự động (Auto-Scheduler):** Cài đặt hẹn giờ chạy ETL định kỳ không cần thao tác thủ công.

---

## 🌐 3. NỀN TẢNG 2: WEB BI DASHBOARD & API (.NET 9 + APPLE GLASSMORPHISM)
Giao diện Web Single Page Application hiện đại theo phong cách **Apple Glassmorphism UI**, tích hợp:
1. **Bộ 40 Câu Hỏi Phân Tích DWH Toàn Diện:**
   * **Bán hàng (Q01 - Q20):** Doanh thu theo quý/năm, xu hướng tăng trưởng, phân khúc khách hàng, Top sản phẩm, phân tích chiết khấu ăn mòn lợi nhuận.
   * **Mua hàng & NCC (Q21 - Q25):** Chi phí nhập hàng, xếp hạng nhà cung cấp chiến lược, cơ cấu nhập khẩu theo vùng địa lý.
   * **Tồn kho & Cảnh báo (Q26 - Q30):** Giám sát hàng tồn kho với chỉ thị màu **ĐỎ (Nguy cấp) - VÀNG (Cảnh báo) - XANH (An toàn)** kèm số lượng thiếu hụt và khuyến nghị đặt hàng.
   * **Vận chuyển (Q31 - Q37):** Đơn vị giao hàng nhanh nhất, tỷ lệ giao trễ SLA theo từng khu vực.
   * **Phân tích Chéo Đa Nghiệp Vụ (Q38 - Q40):** Hàng bán chạy nhưng tồn thấp (nguy cơ đứt hàng), hàng nhập nhiều nhưng bán ít (nguy cơ ứ đọng vốn).
2. **Bản đồ Nhiệt Địa lý Tương tác (World Geo Heatmap):** Ghim tương tác 23 khu vực toàn cầu, hiển thị tức thì doanh số, số đơn hàng và tỷ lệ trễ hạn.
3. **Bộ lọc Toàn diện theo Năm (2012 - 2015) & Khu vực (23 khu vực):** Tự động đồng bộ hóa lát cắt số liệu cho tất cả các chỉ số KPI, biểu đồ và bảng dữ liệu.
4. **Mô phỏng Kịch bản What-If (What-If Scenario Simulation):** Cho phép kéo thanh trượt điều chỉnh Chiết khấu, Chi phí Ship và Tỷ lệ đứt hàng để dự báo tác động trực tiếp đến Lợi nhuận ròng.
5. **Dự báo Doanh số bằng Trí tuệ Nhân tạo (Machine Learning ML.NET):** 
   * Thuật toán chuỗi thời gian **SSA (Singular Spectrum Analysis)** dự báo doanh thu 3, 6, 12 tháng tới kèm khoảng tin cậy 95% (*Upper/Lower Bound*).
6. **Trợ lý AI Copilot Trực Tuyến:** Truy vấn trực tiếp kho DWH, trả lời tức thì số liệu chính xác bằng bảng và dẫn hướng đến biểu đồ tương ứng.

---

## 🛡️ 4. MA TRẬN PHÂN QUYỀN CHẶT CHẼ THEO VAI TRÒ (5 ROLES RBAC)

Hệ thống phân bổ logic hiển thị bám sát bảng Fact tương ứng của từng phòng ban:

| Vai Trò | Tên Đăng Nhập | Mật Khẩu | Phân Hệ Được Phép Truy Cập | Bảng Fact Khai Thác |
| :--- | :--- | :--- | :--- | :--- |
| **Ban Giám Đốc (CEO)** | `giamdoc` | `giamdoc123` | **Toàn quyền:** Tổng quan, Bán hàng, Mua hàng, Tồn kho, Vận chuyển, Phân tích chéo, Dự báo AI, What-If | Toàn bộ Fact & Cross-Fact |
| **Trưởng Phòng Kinh Doanh** | `kinhdoanh` | `giamdoc123` | **3 Phân hệ:** Bán hàng (Q01-Q20), Dự báo AI (ML.NET) và Mô phỏng What-If | `Fact_Order` |
| **Trưởng Phòng Mua Hàng** | `muahang` | `giamdoc123` | **1 Phân hệ:** Mua hàng & Nhà cung cấp (Q21-Q25) | `Fact_Purchase` |
| **Thủ Kho** | `kho` | `kho123` | **1 Phân hệ:** Tồn kho & Cảnh báo an toàn (Q26-Q30) | `Fact_Inventory` |
| **Trưởng Phòng Logistics** | `vanchuyen` | `vanchuyen123` | **1 Phân hệ:** Vận chuyển & Giám sát giao trễ SLA (Q31-Q37) | `Fact_Shipping` |
| **Quản Trị Viên (Admin)** | `admin` | `admin123` | Toàn quyền kiểm thử và quản trị hệ thống | Quản trị & Cấu hình |

> *Ghi chú: Modal đăng nhập trên Web đã tích hợp sẵn 6 nút đăng nhập nhanh 1-click tương ứng với các vai trò trên.*

---

## 🚀 5. HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG

### 5.1. Yêu cầu môi trường
* Windows 10/11
* .NET SDK 9.0 trở lên
* Microsoft SQL Server (SQLEXPRESS hoặc bản Standard/Enterprise)
* Trình duyệt Web (Chrome, Edge)

### 5.2. Khởi chạy Web BI Dashboard & API (Khuyến nghị 1-Click)
Nhấp đúp chuột vào file:
```cmd
Chay_Web_BI.bat
```
* Hệ thống sẽ tự động bật Web API và mở trình duyệt tại: **`http://localhost:5000/`**
* Swagger API Documentation: **`http://localhost:5000/swagger`**

### 5.3. Khởi chạy Desktop Admin App (Khuyến nghị 1-Click)
Nhấp đúp chuột vào file:
```cmd
Chay_Desktop_Admin.bat
```

### 5.4. Đồng bộ mã nguồn lên GitHub (1-Click)
Nhấp đúp chuột vào file:
```cmd
push_to_github.bat
```

---

## 📂 6. CẤU TRÚC THƯ MỤC DỰ ÁN

```
KLTN/
├── KhangNghi_Solution.sln              # Visual Studio Solution chứa toàn bộ dự án
├── Data.xlsx                           # Dữ liệu nguồn hoạt động của Công ty Khang Nghị
├── README.md                           # Tài liệu thuyết minh đồ án tốt nghiệp
├── Chay_Web_BI.bat                     # File 1-click chạy Web BI & API
├── Chay_Desktop_Admin.bat              # File 1-click chạy Desktop Admin App
├── push_to_github.bat                  # File 1-click đồng bộ mã nguồn lên GitHub
├── SSAS_PowerBI_Docs/                  # Hướng dẫn khối đa chiều OLAP SSAS & nhúng Power BI
└── src/
    ├── KhangNghi.Core/                 # Model thực thể DWH, DTOs, Enums phân tích 40 câu hỏi
    ├── KhangNghi.Infrastructure/        # Tầng hạ tầng: Schema SQL, Bulk ETL, Dapper, ML.NET SSA
    ├── KhangNghi.Api/                  # ASP.NET Core 9 Web API + Frontend Web BI SPA (ApexCharts, Glassmorphism)
    └── KhangNghi.DesktopAdmin/         # WPF .NET 9 Desktop App quản trị kỹ thuật dành cho Admin
```

---
**Sinh viên thực hiện:** Đồ án Khóa Luận Tốt Nghiệp - Đại học Ngân Hàng TP.HCM (HUB)  
**Đề tài:** Xây dựng Kho Dữ Liệu và Hệ Thống Hỗ Trợ Ra Quyết Định Kinh Doanh (BI & Data Warehouse).
