# BÁO CÁO NGHIỆM THU TÍNH NĂNG QUICK CHART MODAL (MDX-19 ĐẾN MDX-24)
## DỰ ÁN HỆ THỐNG BI KHANG NGHỊ (WEB BI KHANG NGHỊ)

---

### 1. TỔNG QUAN YÊU CẦU
Bổ sung tính năng **Quick Chart Modal** cho 6 câu truy vấn MDX (`MDX-19`, `MDX-20`, `MDX-21`, `MDX-22`, `MDX-23`, `MDX-24`) thuộc phân hệ **Nhân sự & Khuyến mãi / Bán hàng** tại trang danh sách câu hỏi phân tích (`/#/question-explorer`).
Khi người dùng bấm nút **"Xem biểu đồ"** tại cột Thao tác, hệ thống sẽ mở modal tại chỗ hiển thị biểu đồ trực quan (ApexCharts) và bảng số liệu chi tiết, lấy trực tiếp từ kho dữ liệu DWH mà không cần rời trang hoặc mở cửa sổ mới.

---

### 2. DANH SÁCH FILE ĐÃ THAY ĐỔI

| STT | File / Đường dẫn | Loại thay đổi | Mô tả chi tiết |
|:---:|:---|:---:|:---|
| 1 | `Backend/KhangNghi_BI.Application/Interfaces/IDwhAnalyticsRepository.cs` | Backend Interface | Bổ sung phương thức `Task<object?> GetQuickChartAsync(string id);` |
| 2 | `Backend/KhangNghi_BI.Infrastructure/Repositories/DwhAnalyticsRepository.cs` | Backend Repository | Hiện thực hàm `GetQuickChartAsync` sử dụng Dapper truy vấn SQL trực tiếp trên kho dữ liệu `DWH_KhangNghi`, kết nối các bảng `Fact_Sales`, `Dim_Employee`, `Dim_Promotion`, `Dim_Manufacturer`, `Dim_Product`, `Dim_Service`, `Dim_SalesChannel`, `Dim_Time`. |
| 3 | `Backend/KhangNghi_BI.Api/Controllers/SsasController.cs` | Backend API | Bổ sung endpoint `[HttpGet("quick-chart/{id}")]` có bảo mật JWT `[Authorize]`, trả về dữ liệu định dạng chuẩn cho modal. |
| 4 | `Frontend/js/services/analyticsService.js` | Frontend Service | Bổ sung phương thức `getQuickChart(mdxId)` và export vào object `KhangNghiAnalytics`. |
| 5 | `Frontend/index.html` | Frontend UI | Thêm cấu trúc HTML của component tái sử dụng `#quick-chart-modal` gồm: header metadata chips, container biểu đồ `#quick-chart-container`, bảng số liệu `#quick-chart-table-container`, các trạng thái Loading / Empty / Error, nút Đóng và nút Mở phân hệ liên quan. |
| 6 | `Frontend/js/charts.js` | Frontend Chart Engine | Bổ sung hàm `groupedBar(id, categories, series, opts)` hỗ trợ vẽ biểu đồ cột nhóm nhiều chuỗi theo danh mục, tương thích chế độ Dark Mode và quản lý huỷ instance ApexCharts. |
| 7 | `Frontend/js/app.js` | Frontend Logic | - Cập nhật bảng câu hỏi: hiển thị nút `Xem biểu đồ` cho MDX-19 đến MDX-24.<br>- Hiện thực các hàm điều khiển modal: `openQuickChart`, `closeQuickChart`, `renderQuickChartVisuals`.<br>- Tự động vẽ biểu đồ ApexCharts (Bar, Donut, Grouped Bar) và format bảng tiền tệ VND.<br>- Bổ sung timeout tối ưu cho container trước khi vẽ biểu đồ, phím Escape, khoá cuộn trang (`overflow: hidden`), huỷ biểu đồ cũ (`chart.destroy()`) tránh rò rỉ bộ nhớ. |

---

### 3. API VÀ MÔ HÌNH DỮ LIỆU SỬ DỤNG

- **Endpoint:** `GET /api/Ssas/quick-chart/{id}` (với `{id}` là `MDX-19` đến `MDX-24`)
- **Cơ chế xác thực:** Bearer JWT Token (`Authorization: Bearer <token>`)
- **Định dạng dữ liệu trả về:**
```json
{
  "id": "MDX-19",
  "title": "MDX-19: KPI doanh số theo nhân viên",
  "chartType": "bar",
  "measures": ["Doanh thu", "Sản lượng"],
  "dimensions": ["Nhân viên"],
  "data": [
    {
      "EmployeeName": "Bùi Tấn Vương",
      "EmployeeCode": "NV001",
      "Department": "Kinh Doanh",
      "TotalRevenue": 43500000000,
      "TotalQuantity": 1250
    }
  ]
}
```

---

### 4. KẾT QUẢ ÁNH XẠ VÀ KIỂM THỬ THỰC TẾ TRÊN KHO DỮ LIỆU

| Mã MDX | Tiêu đề nghiệp vụ | Dữ liệu DWH thực tế | Loại trực quan hóa | Kết quả kiểm thử |
|:---:|:---|:---:|:---:|:---|
| **MDX-19** | KPI doanh số theo nhân viên | 5 nhân viên kinh doanh (`Dim_Employee`), doanh số từ 41.9 tỷ đến 43.5 tỷ VND | **Biểu đồ thanh ngang (Horizontal Bar Chart)** xếp hạng doanh thu + bảng chi tiết có format tiền tệ VND | **Thành công** (Đạt) |
| **MDX-20** | Hiệu quả chương trình khuyến mãi | 5 chương trình KM (`Dim_Promotion`), tổng tiền chiết khấu và lợi nhuận thuần | **Biểu đồ tròn/Donut (ApexCharts Donut)** phân bổ chiết khấu + bảng chi tiết | **Thành công** (Đạt) |
| **MDX-21** | Doanh số theo hãng sản xuất | 5 hãng sản xuất (`Dim_Manufacturer` / `Dim_Product`): Khác, Schneider, LS, Mitsubishi, ABB | **Biểu đồ cột đứng (Column Bar Chart)** so sánh doanh số và sản lượng + bảng chi tiết | **Thành công** (Đạt) |
| **MDX-22** | Tỷ suất lợi nhuận gộp theo danh mục | 7 danh mục sản phẩm, tính theo công thức chuẩn: `Margin % = Profit / Revenue * 100` (dao động từ 21.5% đến 24.0%) | **Biểu đồ thanh kèm tỷ lệ % Margin (Bar Chart)** + bảng chi tiết tỷ suất lợi nhuận | **Thành công** (Đạt) |
| **MDX-23** | Doanh thu theo gói dịch vụ đính kèm | Kho DWH hiện tại có bảng `Dim_Service` nhưng 51,248 dòng `Fact_Sales` có `ServiceKey = NULL` | **Empty State thông minh**: Modal hiển thị thông báo "Chưa có phát sinh giao dịch dịch vụ đính kèm trong kỳ", không gãy giao diện | **Thành công** (Đạt, phản ánh trung thực kho DWH) |
| **MDX-24** | Ma trận kênh bán hàng theo 4 quý | Đủ 16 tổ hợp CROSSJOIN giữa 4 Kênh bán (`Dim_SalesChannel`) x 4 Quý năm 2024 (`Dim_Time`) | **Biểu đồ cột nhóm theo Quý (Grouped Bar / Matrix representation)** + bảng ma trận 2 chiều | **Thành công** (Đạt) |

---

### 5. TRẢI NGHIỆM NGƯỜI DÙNG & TÍNH NĂNG BỔ TRỢ

1. **Hiển thị tại chỗ (In-page Modal):** Nhấn "Xem biểu đồ" mở modal phủ mờ nền ngay lập tức, không reload trang, không nhảy URL ngoài ý muốn.
2. **Khóa cuộn trang (Scroll Lock):** Tự động thêm `overflow: hidden` vào `body` khi mở modal và gỡ bỏ khi đóng modal.
3. **Phím tắt đóng nhanh:** Hỗ trợ đóng modal bằng nút `×`, nút "Đóng", bấm click ra vùng nền tối (backdrop) hoặc nhấn phím **Escape (Esc)** trên bàn phím.
4. **Không rò rỉ bộ nhớ (Memory Management):** ApexCharts instance cũ luôn được gọi `.destroy()` trước khi render biểu đồ mới, đảm bảo khi chuyển liên tục giữa các câu MDX không bị đè biểu đồ cũ hoặc lag trình duyệt.
5. **Phương án dự phòng (Fallback):** Modal luôn có nút **"Mở phân hệ liên quan"** chuyển nhanh sang màn hình Bán hàng / Nhân sự / Báo cáo tương ứng.
6. **Dark Mode:** Màu nền modal, đường viền, màu chữ, tooltip và bảng dữ liệu tuân thủ chuẩn bảng màu Dark Mode hiện hành của Web BI Khang Nghị.

---

### 6. CÁC HẠNG MỤC LƯU Ý VÀ CHƯA XÁC MINH

1. **Cột `ServiceKey` trong `Fact_Sales`:**
   - Dữ liệu hiện tại trong kho DWH cho thấy `Fact_Sales` chưa có dòng nào phát sinh `ServiceKey` hợp lệ (tất cả là `NULL`). Do đó MDX-23 hiển thị trạng thái Empty State là hoàn toàn chính xác theo nguyên tắc không tự bịa mock data. Khi quy trình ETL nạp dữ liệu gói dịch vụ, biểu đồ sẽ tự động hiển thị mà không cần sửa code.
2. **Cơ chế SSAS MSOLAP trên Linux:**
   - Container Docker `kn-api` chạy trên Linux x64 (.NET 9), thư viện OLE DB Provider (`MSOLAP`) của Microsoft chỉ hỗ trợ Windows. Giải pháp truy vấn Dapper SQL trực tiếp trên kho dữ liệu `DWH_KhangNghi` (cùng schema với Cube) đã giải quyết triệt để vấn đề này, mang lại hiệu năng cao (<50ms).

---

### 7. HƯỚNG DẪN KIỂM TRA CHO NGƯỜI DÙNG

1. Truy cập Web BI tại: **`http://localhost:5000`**
2. Đăng nhập tài khoản quản trị:
   - Tên đăng nhập: `admin` (hoặc `giamdoc`)
   - Mật khẩu: `Admin@KhangNghi2026#` (hoặc `Ceo@KhangNghi2026!`)
3. Vào menu **Khám phá câu hỏi** (`/#/question-explorer`).
4. Tìm đến nhóm câu hỏi **Nhân sự & Khuyến mãi** (từ `MDX-19` đến `MDX-24`).
5. Bấm nút **"Xem biểu đồ"** trên từng câu hỏi để trải nghiệm Quick Chart Modal.
