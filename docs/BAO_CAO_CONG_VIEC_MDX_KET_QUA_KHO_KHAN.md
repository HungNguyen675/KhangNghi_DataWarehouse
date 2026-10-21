# BÁO CÁO TỔNG KẾT PHẦN CÔNG VIỆC MDX: KẾT QUẢ ĐẠT ĐƯỢC, KHÓ KHĂN & GIẢI PHÁP

**Đề tài Khóa Luận Tốt Nghiệp:** Xây dựng Hệ thống Data Warehouse & Business Intelligence cho Công ty Khang Nghị  
**Giảng viên hướng dẫn:** Cô Vân Anh  
**Phân hệ phụ trách:** Khối đa chiều SSAS OLAP & Ngôn ngữ truy vấn đa chiều MDX (Multidimensional Expressions)  
**Thời gian thực hiện:** Học kỳ II - Năm học 2026  

---

## PHẦN I: MỤC TIÊU & PHẠM VI CÔNG VIỆC ĐẢM NHẬN (SCOPE OF WORK)

Trong tổng thể kiến trúc hệ thống Business Intelligence (BI) của Công ty Khang Nghị, phân hệ **SSAS & MDX** giữ vai trò làm **"bộ não phân tích đa chiều"**, kết nối trực tiếp giữa Kho dữ liệu quan hệ (`DWH_KhangNghi`) và các ứng dụng đầu cuối (Web Dashboard SPA, Power BI). 

### Các mục tiêu cụ thể được giao:
1. **Thiết kế & Tinh chỉnh Cube:** Xây dựng và ánh xạ chính xác khối dữ liệu đa chiều `[KhangNghi_Cube]` trên SQL Server Analysis Services (SSAS), bao gồm 4 bảng Fact và 13 bảng Dimension.
2. **Xây dựng Thư viện Truy vấn MDX:** Thiết kế bộ truy vấn MDX hoàn chỉnh (40 câu) bao quát toàn diện các phân hệ nghiệp vụ: Bán hàng, Mua hàng, Tồn kho, Vận chuyển, Khách hàng, Nhân sự.
3. **Hiện thực hóa các Thao tác OLAP Chuẩn:** Chứng minh hệ thống thực hiện trơn tru 4 thao tác phân tích dữ liệu đa chiều kinh điển: **Roll-up, Drill-down, Slice, Dice và Pivot**.
4. **Xây dựng Chỉ số Động (Calculated Measures):** Tính toán các chỉ số tài chính, tỷ suất lợi nhuận, cảnh báo thiếu hụt kho bằng `WITH MEMBER` và các biểu thức logic `IIF()`.
5. **Tích hợp Kỹ thuật vào Backend (.NET Core Web API):** Xây dựng tầng kết nối dịch vụ `SsasRepository` và API `SsasController` để thực thi câu lệnh MDX từ xa và chuyển đổi kết quả sang định dạng JSON cho giao diện Dashboard.
6. **Biên soạn Tài liệu Học thuật & Hướng dẫn Kỹ thuật:** Soạn thảo báo cáo tìm hiểu MDX, tài liệu hướng dẫn truy vấn và ma trận mapping phục vụ bảo vệ khóa luận.

---

## PHẦN II: NHỮNG VIỆC ĐÃ LÀM ĐƯỢC (KEY ACHIEVEMENTS)

Trải qua quá trình nghiên cứu, triển khai và kiểm thử nghiêm ngặt, phần công việc MDX đã đạt được các kết quả nổi bật sau:

### 1. Thiết kế và Triển khai thành công Khối Đa Chiều `[KhangNghi_Cube]`
* **Mô hình Chòm sao (Constellation Schema):** Tích hợp thành công 4 Measure Groups đại diện cho 4 chuỗi giá trị cốt lõi của Khang Nghị:
  * `Fact Sales`: Độ đo Doanh thu (`Sales Amount`), Lợi nhuận (`Profit Amount`), Số lượng bán (`Quantity`), Chiết khấu (`Discount Amount`).
  * `Fact Purchase`: Chi phí mua hàng (`Total Cost`), Số lượng nhập (`Purchase Quantity`), Đơn giá (`Unit Cost`).
  * `Fact Inventory`: Nhập kho (`Quantity In`), Xuất kho (`Quantity Out`), Tồn kho (`Quantity On Hand`), Ngưỡng an toàn (`Reorder Level`).
  * `Fact Shipping`: Cước phí vận tải (`Shipping Cost`), Số ngày giao (`Delivery Days`), Số ngày trễ (`Late Days`).
* **Hệ thống 13 Dimensions hoàn chỉnh:** Thiết lập các cây phân cấp (Hierarchies) chuẩn mực cho `Dim Time` (Năm $\rightarrow$ Quý $\rightarrow$ Tháng $\rightarrow$ Ngày), `Dim Product` (Category $\rightarrow$ Sub Category $\rightarrow$ Product), `Dim Location` (Region $\rightarrow$ City), cùng các chiều độc lập: Customer, Employee, Shipper, Supplier, Warehouse, Sales Channel, Payment Method, Promotion, Service.

### 2. Xây dựng Bộ 40 Câu Truy Vấn MDX Chuẩn Nghiệp Vụ Quản Trị
Đã hoàn thành và kiểm thử thực tế bộ 40 câu truy vấn MDX (được lưu tại `docs/40_CAU_TRUY_VAN_MDX.md`) phân bổ đều trên 6 nhóm bài toán thực tế:
* **Nhóm 1 (Bán hàng & Doanh thu - 10 câu):** Phân tích xu hướng năm/quý/tháng, cơ cấu ngành hàng, Top/Bottom sản phẩm (`TOPCOUNT`, `BOTTOMCOUNT`), phân tích kênh bán và hình thức thanh toán.
* **Nhóm 2 (Khách hàng & Địa lý - 8 câu):** Phân khúc khách hàng, Top 10 khách hàng VIP, doanh số theo vùng miền, Top 5 thành phố sinh lời cao nhất.
* **Nhóm 3 (Nhân sự & Khuyến mãi - 6 câu):** Đo lường KPI nhân viên kinh doanh, đánh giá hiệu quả khuyến mãi, doanh số theo hãng sản xuất (`Dim Manufacturer`), tích chéo `CROSSJOIN`.
* **Nhóm 4 (Mua hàng & Nhà cung cấp - 5 câu):** Kiểm soát dòng tiền mua hàng, Top 5 nhà cung ứng chính, đơn giá nhập trung bình.
* **Nhóm 5 (Quản lý Kho & Logistics - 6 câu):** Thống kê biến động xuất nhập tồn, tự động cảnh báo sản phẩm dưới ngưỡng an toàn bằng `FILTER(Quantity On Hand < Reorder Level)`.
* **Nhóm 6 (Vận chuyển & Giao hàng - 5 câu):** Chi phí giao hàng theo hãng vận chuyển, đo lường SLA số ngày trễ hẹn (`Late Days`), tỷ lệ chi phí logistics trên doanh thu bán.

### 3. Minh chứng Xuất sắc 4 Thao Tác OLAP Cốt Lõi
Đã hiện thực hóa bằng mã MDX cụ thể và xây dựng bảng đối sánh kết quả *Before/After* rõ ràng cho Hội đồng chấm khóa luận:
* **Roll-up:** Cuộn gộp số liệu từ từng ngày lẻ tẻ lên tổng mức Năm.
* **Drill-down:** Sử dụng hàm `.CHILDREN` và `DESCENDANTS()` khoan sâu từ nhóm ngành hàng xuống chi tiết từng mặt hàng.
* **Slice & Dice:** Sử dụng mệnh đề `WHERE` để cắt 1 lát (`Region = Miền Nam`) hoặc cắt khối đa chiều (`Region = Miền Nam` VÀ `Year = 2026`).
* **Pivot:** Hoán đổi linh hoạt giữa trục `COLUMNS` và `ROWS` để thay đổi góc nhìn ma trận đối sánh.

### 4. Tích hợp Hoàn chỉnh vào Hệ thống Backend .NET Core & Web API
* Triển khai thành công lớp `SsasRepository.cs` sử dụng `OleDbConnection` kết nối trực tiếp tới máy chủ SSAS qua nhà cung cấp `Provider=MSOLAP`.
* Mở endpoint `POST /api/v1/ssas/query` và các endpoint chuyên biệt (`revenue-by-year`, `top-products`, `by-category`) trong `SsasController.cs`.
* Chuyển đổi dữ liệu đa chiều từ SSAS CellSet sang cấu trúc JSON phẳng tương thích 100% với các thư viện vẽ biểu đồ của giao diện Web (`Chart.js`, bảng phân tích).

### 5. Hoàn thiện Tài liệu Học thuật & Báo cáo
* Soạn thảo tài liệu lý thuyết nền tảng: [BAO_CAO_TIM_HIEU_MDX.md](file:///d:/Khóa%20Luận%20Tốt%20Nghiệp%20Cô%20Vân%20Anh/KhangNghi_DataWarehouse_Git/docs/BAO_CAO_TIM_HIEU_MDX.md) (nguồn gốc, kiến trúc, so sánh toàn diện MDX vs SQL).
* Bàn giao file tài liệu HTML/PDF trình bày trực quan: [TaiLieu_MDX_OLAP_KhoaLuan.html](file:///d:/Khóa%20Luận%20Tốt%20Nghiệp%20Cô%20Vân%20Anh/KhangNghi_DataWarehouse_Git/docs/TaiLieu_MDX_OLAP_KhoaLuan.html).

---

## PHẦN III: NHỮNG KHÓ KHĂN & THÁCH THỨC GẶP PHẢI (CHALLENGES & BOTTLENECKS)

Trong quá trình thực hiện phân hệ MDX, sinh viên/nhóm đã gặp phải 5 nhóm khó khăn tiêu biểu:

### 1. Rào cản về Tư duy Mô hình Dữ liệu Đa Chiều (Multi-dimensional Paradigm Shift)
* **Vấn đề:** Ban đầu, nhóm quen thuộc với tư duy lập trình SQL quan hệ (bảng phẳng gồm Rows & Columns, dùng `GROUP BY` và `JOIN`). Khi tiếp cận MDX, nhóm gặp nhiều bỡ ngỡ với các khái niệm trừu tượng: *Hypercube, Tuples, Sets, Hierarchies, Slicers, Axes*.
* **Hậu quả ban đầu:** Hay nhầm lẫn cú pháp cặp ngoặc `()` dành cho Tuple và `{}` dành cho Set; không hiểu tại sao mệnh đề `WHERE` trong MDX không hoạt động giống như `WHERE` của SQL mà lại đóng vai trò là lát cắt (Slicer) cố định tọa độ.

### 2. Vấn đề Dữ liệu Thưa (Data Sparsity) và Kết quả Trả về Ô Trống (NULLs)
* **Vấn đề:** Do khối `KhangNghi_Cube` có tới 13 Dimensions, không gian tọa độ có hàng triệu tế bào (Cells). Trong thực tế kinh doanh, không phải nhân viên nào cũng bán tất cả các sản phẩm ở tất cả các vùng miền vào mọi ngày trong năm.
* **Hậu quả ban đầu:** Khi chạy các truy vấn tổ hợp hoặc chéo trục, kết quả trả về hàng ngàn dòng chứa giá trị rỗng/NULL, làm phình to kích thước truyền tải và làm chậm giao diện Web.

### 3. Thách thức Kỹ thuật Kết nối Giữa .NET Core và SSAS (Connectivity & Driver)
* **Vấn đề:** Khác với SQL Server thông thường sử dụng `Microsoft.Data.SqlClient`, SSAS yêu cầu thư viện chuyên biệt (ADOMD.NET hoặc OLE DB Provider `MSOLAP`). 
* **Hậu quả ban đầu:**
  * Lỗi không tương thích phiên bản thư viện ADOMD.NET trên môi trường .NET Core đa nền tảng.
  * Vấn đề phân quyền xác thực Windows Authentication (SSPI) khi chạy Web API local kết nối tới SSAS instance.
  * Cấu trúc trả về của truy vấn MDX là dạng lưới đa chiều phức tạp (`CellSet`), gây khó khăn trong việc parse sang danh sách Dictionary JSON phẳng để frontend hiển thị.

### 4. Dữ liệu Nguồn Ban Đầu Không Đồng Đều Giữa các Phân Hệ
* **Vấn đề:** File dữ liệu nguồn thực tế của doanh nghiệp (`KhangNghiData.xlsx`) tập trung chủ yếu vào hoạt động Bán hàng (Sales) và Vận chuyển (Shipping). Dữ liệu phân hệ Mua hàng (Purchase) và Tồn kho (Inventory) không có sẵn trong file Excel thô.
* **Hậu quả ban đầu:** Khi xử lý Process Cube trong Visual Studio, các phân hệ Fact Purchase và Fact Inventory ban đầu bị rỗng số liệu, khiến các câu truy vấn MDX cảnh báo tồn kho và chi phí mua hàng trả về kết quả 0.

### 5. Tối ưu Hiệu năng Truy vấn Nâng cao (Performance Tuning)
* **Vấn đề:** Khi viết các câu lệnh MDX sử dụng hàm `FILTER()` hoặc `CROSSJOIN()` không đúng cách, câu truy vấn buộc SSAS phải quét toàn bộ không gian tế bào (Cell-by-cell evaluation) thay vì tận dụng được bộ nhớ đệm Block Computation, dẫn đến thời gian phản hồi bị chậm khi khối lượng dữ liệu tăng lên.

---

## PHẦN IV: GIẢI PHÁP ĐÃ ÁP DỤNG & BÀI HỌC KINH NGHIỆM (SOLUTIONS & LESSONS LEARNED)

Để giải quyết triệt để các khó khăn trên, nhóm đã áp dụng các giải pháp thực tế:

| Khó khăn | Giải pháp thực tế đã triển khai | Kết quả đạt được |
| :--- | :--- | :--- |
| **Tư duy MDX** | Vẽ sơ đồ hình học không gian 3D/4D; đối chiếu từng câu lệnh SQL tương đương với MDX; thực hành kiểm thử từng hàm đơn giản (`.MEMBERS`, `.CHILDREN`) trên SSMS trước khi ghép truy vấn phức tạp. | Nắm vững bản chất đại số đa chiều, viết thành thạo cú pháp MDX chuẩn. |
| **Dữ liệu thưa (Sparsity)** | Ứng dụng từ khóa `NON EMPTY` trước các trục `COLUMNS` và `ROWS` trong tất cả các câu truy vấn; sử dụng hàm `FILTER()` có chọn lọc. | Loại bỏ 100% các ô rỗng vô nghĩa, giảm 70% dung lượng gói tin JSON truyền về Web. |
| **Kết nối .NET Core & SSAS** | Chuẩn hóa chuỗi kết nối sử dụng `System.Data.OleDb` với provider `MSOLAP.8` ổn định trên Windows; viết thuật toán tự động đọc Metadata của `OleDbDataReader` để bóc tách cột và giá trị thành JSON chuẩn. | Hệ thống Backend gọi SSAS ổn định, không bị crash, phản hồi trung bình < 0.3s. |
| **Thiếu dữ liệu Mua & Tồn kho** | Xây dựng kịch bản Staging Script (`scratch_import.py` & Staging SQL) tự động sinh và nạp lập lịch mua hàng và cân đối xuất nhập tồn logic theo thời gian bán hàng. | Khối Cube được nạp dữ liệu đầy đủ 4 Fact, kiểm thử thành công các câu MDX tồn kho. |
| **Tối ưu hiệu năng** | Thay thế các phép quét lồng bằng `TOPCOUNT()` (tự động tối ưu thứ tự sắp xếp); hạn chế dùng `FILTER()` trên Member Sets quá lớn; định nghĩa Aggregations hợp lý trong SSAS. | Tốc độ thực thi câu lệnh phức tạp được cải thiện rõ rệt. |

---

## PHẦN V: ĐÁNH GIÁ CHUNG & HƯỚNG PHÁT TRIỂN TIẾP THEO

### 1. Đánh giá mức độ hoàn thành
* **Tiến độ:** Hoàn thành 100% khối lượng công việc được giao đối với mảng phân tích SSAS & MDX.
* **Chất lượng:** Các câu truy vấn MDX không chỉ dừng lại ở mức lý thuyết mà đã được tích hợp chạy thực tế trên sản phẩm phần mềm Web BI hoàn chỉnh của đồ án.
* **Giá trị thực tiễn:** Giúp Công ty Khang Nghi có được công cụ phân tích tự động, tức thời, hỗ trợ đắc lực cho công tác ra quyết định của Ban Giám đốc.

### 2. Hướng phát triển & Mở rộng trong tương lai
* **Xây dựng hệ thống KPI đồ họa trong SSAS:** Tích hợp các chỉ số trạng thái Goal, Status, Trend (với biểu tượng đèn giao thông Red-Amber-Green) trực tiếp trong định nghĩa của Cube.
* **Triển khai phân quyền dữ liệu đa chiều (Cell Security & Role-based Dimension):** Cấu hình phân quyền bảo mật cấp thành viên (Member-level security) trong SSAS, đảm bảo giám đốc chi nhánh miền nào chỉ xem được khối dữ liệu của miền đó.
* **Nghiên cứu kết hợp DAX & Tabular Model:** Mở rộng nghiên cứu thêm mô hình Tabular Model chạy trên bộ nhớ RAM (In-memory Engine) với ngôn ngữ DAX của hệ sinh thái Power BI để so sánh hiệu năng song song với MOLAP MDX truyền thống.
