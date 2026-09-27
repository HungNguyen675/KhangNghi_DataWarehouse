# HƯỚNG DẪN VẬN HÀNH 2 WEBSITE NGUỒN OLTP - KHANG NGHI DATA WAREHOUSE

Đồ án Khóa Luận Tốt Nghiệp: **KhangNghi Data Warehouse**  
Hệ thống 2 Website nguồn OLTP độc lập đóng vai trò sinh dữ liệu giao dịch thực tế để SSIS trích xuất (ETL) vào **STAGING**, **DDS**, **Data Warehouse** và **SSAS Cube**.

---

## 📌 1. TỔNG QUAN HỆ THỐNG NGUỒN (OLTP SOURCES)

```
                       ┌─────────────────────────┐
                       │  KhangNghi_Sales (DB)   │
                       └────────────┬────────────┘
                                    │ SourceSystem = "SALES_WEB"
                                    ▼
┌──────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│ KhangNghi.Sales  │ ────► │ SSIS ETL        │ ────► │ KhangNghi DWH   │
│ (ASP.NET MVC)    │       │ Package Process │       │ & SSAS OLAP     │
└──────────────────┘       └─────────────────┘       └─────────────────┘
                                    ▲
                                    │ SourceSystem = "PURCHASE_WEB"
                       ┌────────────┴────────────┐
                       │ KhangNghi_Purchase (DB) │
                       └─────────────────────────┘
```

| Tiêu Chí | Website 1: KhangNghi SalesWeb | Website 2: KhangNghi PurchaseWeb |
| :--- | :--- | :--- |
| **Mục đích** | Quản lý Bán hàng & Khách hàng | Quản lý Mua hàng & Nhà cung cấp |
| **Project** | `KhangNghi.SalesWeb` | `KhangNghi.PurchaseWeb` |
| **Database** | `KhangNghi_Sales` (SQL Server) | `KhangNghi_Purchase` (SQL Server) |
| **SourceSystem Tag** | `"SALES_WEB"` | `"PURCHASE_WEB"` |
| **Giao diện Primary** | Blue (Xanh Dương) | Green (Xanh Lá) |
| **URL Khởi chạy** | `http://localhost:5001` | `http://localhost:5002` |

---

## 🗄️ 2. HƯỚNG DẪN TẠO DATABASE SQL SERVER (SỬ DỤNG SCRIPT)

Trong thư mục `KLTN/sql/`, dự án đã cung cấp 2 file SQL Script hoàn chỉnh chứa toàn bộ cấu trúc bảng, khóa chính, khóa ngoại, chỉ mục (Index) và dữ liệu mẫu tiếng Việt phong phú:

1. **Tạo Database Bán hàng (`KhangNghi_Sales`)**:
   - File Script: [`KLTN/sql/01_Create_KhangNghi_Sales_DB.sql`](file:///d:/Kh%C3%B3a%20Lu%E1%BA%ADn%20T%E1%BB%91t%20Nghi%E1%BB%87p%20C%C3%B4%20V%C3%A2n%20Anh/KhangNghi_DataWarehouse_Git/KLTN/sql/01_Create_KhangNghi_Sales_DB.sql)
   - Chứa: 20+ Sản phẩm, 20 Khách hàng, 50 Đơn hàng kèm Chi tiết đơn hàng (Cả Miền Bắc & Miền Nam).

2. **Tạo Database Mua hàng (`KhangNghi_Purchase`)**:
   - File Script: [`KLTN/sql/02_Create_KhangNghi_Purchase_DB.sql`](file:///d:/Kh%C3%B3a%20Lu%E1%BA%ADn%20T%E1%BB%91t%20Nghi%E1%BB%87p%20C%C3%B4%20V%C3%A2n%20Anh/KhangNghi_DataWarehouse_Git/KLTN/sql/02_Create_KhangNghi_Purchase_DB.sql)
   - Chứa: 15 Nhà cung cấp, 20 Sản phẩm, 50 Phiếu mua hàng kèm Chi tiết phiếu mua (Cả Miền Bắc & Miền Nam).

### Cách thực thi trên SQL Server Management Studio (SSMS):
1. Mở SSMS và kết nối tới SQL Server Server (`localhost\SQLEXPRESS` hoặc `.\MSSQLSERVER2025`).
2. Mở file `01_Create_KhangNghi_Sales_DB.sql` -> nhấn `Execute (F5)`.
3. Mở file `02_Create_KhangNghi_Purchase_DB.sql` -> nhấn `Execute (F5)`.

---

## ⚙️ 3. CẤU HÌNH CONNECTION STRING

Mở file `appsettings.json` trong mỗi dự án để chỉnh sửa tên Server SQL Server nếu cần:

- **SalesWeb (`src/KhangNghi.SalesWeb/appsettings.json`)**:
  ```json
  "ConnectionStrings": {
    "SalesDbConnection": "Server=.\\MSSQLSERVER2025;Database=KhangNghi_Sales;Integrated Security=True;TrustServerCertificate=True;"
  }
  ```

- **PurchaseWeb (`src/KhangNghi.PurchaseWeb/appsettings.json`)**:
  ```json
  "ConnectionStrings": {
    "PurchaseDbConnection": "Server=.\\MSSQLSERVER2025;Database=KhangNghi_Purchase;Integrated Security=True;TrustServerCertificate=True;"
  }
  ```

---

## 🚀 4. HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG

### Cách 1: Chạy từ Terminal / Command Prompt

1. **Khởi chạy SalesWeb (Cổng 5001)**:
   ```bash
   dotnet run --project KLTN/src/KhangNghi.SalesWeb/KhangNghi.SalesWeb.csproj --urls http://localhost:5001
   ```

2. **Khởi chạy PurchaseWeb (Cổng 5002)**:
   ```bash
   dotnet run --project KLTN/src/KhangNghi.PurchaseWeb/KhangNghi.PurchaseWeb.csproj --urls http://localhost:5002
   ```

### Cách 2: Mở Solution trên Visual Studio
1. Mở `KLTN/KhangNghi_Solution.sln`.
2. Chuột phải vào Solution -> Chọn `Configure Startup Projects...` -> Chọn `Multiple startup projects`.
3. Đặt `KhangNghi.SalesWeb` và `KhangNghi.PurchaseWeb` thành `Start`.
4. Nhấn `F5` để chạy cả 2 website cùng lúc.

---

## 🧪 5. KIỂM TRA CHỨC NĂNG NGHIỆP VỤ

### Website 1: KhangNghi SalesWeb (`http://localhost:5001`)
- **Dashboard**: Thống kê Tổng số đơn hàng, Tổng doanh thu, Doanh thu Miền Bắc vs Miền Nam.
- **Quản lý Sản Phẩm**: Xem danh sách, thêm/sửa/xóa, lọc theo Danh mục và Miền (`MIEN_BAC`/`MIEN_NAM`).
- **Quản lý Khách Hàng**: Xem danh sách, thêm/sửa/xóa khách hàng.
- **Quản lý Đơn Hàng**:
  - Tạo đơn hàng: Chọn Khách hàng, chọn Sản phẩm, tự động nhảy đơn giá & tính Thành tiền, áp dụng Discount (%).
  - Tự động ghi nhận `SourceSystem = "SALES_WEB"`.
  - Tìm kiếm theo Mã đơn hàng, lọc theo Ngày và Khu vực.

### Website 2: KhangNghi PurchaseWeb (`http://localhost:5002`)
- **Dashboard**: Thống kê Tổng số phiếu mua, Tổng tiền mua hàng, Tổng số nhà cung cấp, Mua hàng Miền Bắc vs Miền Nam.
- **Quản lý Nhà Cung Cấp**: Xem danh sách, thêm/sửa/xóa nhà cung cấp.
- **Quản lý Sản Phẩm Nhập**: Xem danh sách, thêm/sửa/xóa sản phẩm.
- **Quản lý Phiếu Mua Hàng**:
  - Tạo phiếu mua: Chọn Nhà cung cấp, chọn Sản phẩm, nhập giá nhập & số lượng, tự động tính Thành tiền.
  - Tự động ghi nhận `SourceSystem = "PURCHASE_WEB"`.
  - Tìm kiếm theo Mã phiếu mua, lọc theo Ngày và Khu vực.

---

## 🛠️ 6. CHECKLIST SẴN SÀNG CHO SSIS ETL & DATA WAREHOUSE

- [x] Độc lập CSDL: `KhangNghi_Sales` và `KhangNghi_Purchase` tách biệt.
- [x] Đánh dấu hệ thống nguồn rõ ràng: `SALES_WEB` và `PURCHASE_WEB`.
- [x] Chuẩn hóa kiểu dữ liệu: `DECIMAL(18,2)` cho tiền, `DATETIME` cho ngày tháng, `VARCHAR/NVARCHAR` có độ dài tối ưu.
- [x] Các trường nghiệp vụ phục vụ Join trong SSIS: `ProductCode`, `CustomerCode`, `SupplierCode`, `OrderCode`, `PurchaseCode`, `Region`, `OrderDate`, `PurchaseDate`.
- [x] Build Solution thành công `0 Error(s)`.
