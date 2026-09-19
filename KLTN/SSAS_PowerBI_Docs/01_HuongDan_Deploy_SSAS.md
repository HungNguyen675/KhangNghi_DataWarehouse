# Hướng dẫn Deploy Khối dữ liệu đa chiều (OLAP Cube) lên SSAS

Tài liệu này hướng dẫn các bước để deploy cấu trúc Data Warehouse (DWH_KhangNghi) từ SQL Server Relational Database thành khối dữ liệu đa chiều (Multidimensional Cube hoặc Tabular Model) trên **SQL Server Analysis Services (SSAS)**.

## Bước 1: Chuẩn bị Môi trường
1. Cài đặt **SQL Server** kèm theo tính năng **Analysis Services** (chọn chế độ Multidimensional hoặc Tabular).
2. Cài đặt **SQL Server Data Tools (SSDT)** tích hợp vào Visual Studio.
3. Đảm bảo Database `DWH_KhangNghi` đã được tạo và nạp đầy đủ dữ liệu thông qua ETL (chạy trên Desktop Admin App).

## Bước 2: Tạo dự án Analysis Services trong Visual Studio
1. Mở Visual Studio -> `Create a new project`.
2. Chọn template **Analysis Services Multidimensional and Data Mining Project** (hoặc Tabular tùy yêu cầu). Đặt tên là `KhangNghi_SSAS_Cube`.
3. Trong cửa sổ Solution Explorer, nhấp chuột phải vào **Data Sources** -> `New Data Source`.
   - Tạo kết nối đến CSDL `DWH_KhangNghi`.
   - Sử dụng Windows Authentication hoặc tài khoản `sa`.
4. Nhấp chuột phải vào **Data Source Views** -> `New Data Source View`.
   - Chọn Data Source vừa tạo.
   - Kéo tất cả các bảng `Fact_...` và `Dim_...` sang vùng Included Objects.
   - SSDT sẽ tự động nhận diện các quan hệ (Relationships) dựa trên Khóa chính/Khóa ngoại có sẵn.

## Bước 3: Tạo khối dữ liệu (Cube)
1. Nhấp chuột phải vào **Cubes** -> `New Cube`.
2. Chọn `Use existing tables`.
3. Chọn các bảng Fact làm **Measure Group tables**: `Fact_Order`, `Fact_Purchase`, `Fact_Inventory`, `Fact_Shipping`.
4. Chọn các bảng Dimension: `Dim_Product`, `Dim_Customer`, `Dim_Location`, `Dim_Time`, `Dim_Shipper`.
5. Review cấu trúc và nhấn Finish.

## Bước 4: Tinh chỉnh Dimension và Measure
- Mở từng Dimension, ví dụ `Dim_Time`, thiết lập Type = `Time`, cấu hình phân cấp (Hierarchy): `Year -> Quarter -> Month -> Date`.
- Mở Cube, đổi tên các Measure cho thân thiện: `Total Revenue`, `Total Quantity`, `Total Shipping Cost`, v.v.

## Bước 5: Build & Deploy
1. Click chuột phải vào Project `KhangNghi_SSAS_Cube` -> chọn **Properties**.
2. Thẻ Deployment -> Đặt Server name là `localhost` hoặc tên máy chủ cài SSAS. Database = `KhangNghi_SSAS_DB`.
3. Click chuột phải vào Project -> chọn **Deploy**.
4. Quá trình xử lý (Process) sẽ nạp dữ liệu từ DDS lên khối OLAP.

## Bước 6: Truy vấn khối OLAP
- Sau khi Deploy xong, mở **SQL Server Management Studio (SSMS)**, kết nối vào loại máy chủ là **Analysis Services**.
- Mở cơ sở dữ liệu `KhangNghi_SSAS_DB`, bạn có thể dùng ngôn ngữ **MDX** (Multidimensional Expressions) để truy vấn.
*(Xem file `02_MDX_Queries.mdx` để biết các câu truy vấn mẫu)*.
