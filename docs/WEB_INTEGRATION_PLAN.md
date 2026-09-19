# KẾ HOẠCH TÍCH HỢP HỆ THỐNG WEB BI VỚI KHO DỮ LIỆU (WEB INTEGRATION PLAN)
**Dự án:** Kho dữ liệu hỗ trợ phân tích hiệu quả hoạt động kinh doanh Công ty Khang Nghị  
**Tác giả:** Senior Software Architect & .NET Engineer  

---

## 1. MỤC TIÊU KIẾN TRÚC & NGUYÊN TẮC THIẾT KẾ

### 1.1 Sơ đồ Kiến trúc Tổng thể (Architecture Diagram)

```
[ Excel Data Source ] ──(SSIS ETL Packages)──► [ SQL Server DWH (DWH_KhangNghi) ]
                                                            │
                                                     (Dapper / ADO.NET)
                                                            ▼
                                             [ ASP.NET Core .NET 9 Web API ]
                                                            │
                                                (REST API + JWT Bearer Auth)
                                                            ▼
                                              [ Web BI Dashboard (Vanilla SPA) ]
```

### 1.2 Nguyên tắc Bảo mật và Cách ly
1. **Không truy cập trực tiếp Database từ Browser:** Trình duyệt Web BI Dashboard tuyệt đối không kết nối trực tiếp đến SQL Server. Tất cả yêu cầu dữ liệu phải đi qua Web API.
2. **Giao tiếp qua RESTful HTTP API:** Tất cả dữ liệu phân tích, báo cáo, chỉ số KPI được truyền tải qua JSON format.
3. **Bảo tồn Giao diện Hiện tại (Web Prototype):** Giữ nguyên thiết kế UI/UX, CSS styling, bố cục Layout của `web-prototype` hiện tại. Không redesign, không thay đổi theme màu sắc hoặc cấu trúc HTML trừ khi cập nhật script gọi API.
4. **Kiểm soát Truy cập dựa trên Vai trò (RBAC):** Backend ASP.NET Core tự xác thực JWT Token và phân quyền (Roles: Admin, Director, SalesManager, PurchaseManager, InventoryManager, ShippingStaff).

---

## 2. HIỆN TRẠNG VÀ LỘ TRÌNH CHUYỂN ĐỔI (TRANSITION ROADMAP)

### 2.1 Hiện trạng (Current State)
* **Frontend:** Đang sử dụng các file JavaScript giả lập (`web-prototype/js/mock-api.js` & `web-prototype/js/mock-data.js`) thông qua lớp trừu tượng `analyticsService.js`.
* **Backend:** ASP.NET Core .NET 9 (`KhangNghi.Api`) đã xây dựng sẵn 10 Controllers REST API đầy đủ với 40 hàm Dapper SQL Query (`DwhAnalyticsRepository.cs`).
* **Database:** `DWH_KhangNghi` đã có script DDL hoàn chỉnh với 13 bảng Dimension và 4 bảng Fact.

### 2.2 Các bước chuyển đổi từ Mock sang Real API (Steps to Switch to Live API)

#### Bước 1: Tạo HTTP Client Service chuẩn hóa trên Frontend (`apiClient.js`)
Tạo module quản lý HTTP Request tập trung hỗ trợ:
* Tự động đính kèm Header: `Authorization: Bearer <JWT_TOKEN>`.
* Xử lý lỗi HTTP (401 Unauthorized, 403 Forbidden, 500 Internal Server Error).
* Cấu hình `baseURL` linh hoạt (mặc định `http://localhost:5000/api` hoặc relative URL `/api`).

#### Bước 2: Chuyển đổi `analyticsService.js` sang gọi REST API
Thay thế các hàm tính toán Mock trong `analyticsService.js` bằng lệnh `fetch()` đến Backend Controllers:
* `getExecutiveOverview()` ➔ `GET /api/dashboard/summary` & `GET /api/sales/revenue-by-category`
* `getSalesOverview()` ➔ `GET /api/sales/revenue-by-year`, `GET /api/sales/top-products-by-revenue`
* `getPurchaseOverview()` ➔ `GET /api/purchase/top-suppliers-by-amount`, `GET /api/purchase/purchase-price-trend`
* `getInventoryOverview()` ➔ `GET /api/inventory/highest-stock`, `GET /api/inventory/reorder-alerts`
* `getShippingOverview()` ➔ `GET /api/shipping/orders-by-shipper`, `GET /api/shipping/highest-late-days`

#### Bước 3: Tích hợp Đăng nhập thật (Real Authentication Flow)
* Cập nhật `authService.js` gọi `POST /api/auth/login`.
* Lưu trữ `token` và thông tin User Role vào `localStorage`.
* Xóa bỏ hardcode tài khoản frontend giả lập.

#### Bước 4: Nhúng Web Dashboard vào `wwwroot` của ASP.NET Core API
* Copy toàn bộ thư mục `web-prototype` vào `src/KhangNghi.Api/wwwroot`.
* ASP.NET Core API vừa làm REST API server, vừa phục vụ file tĩnh Single Page Application (SPA).

---

## 3. CÁC PHÂN HỆ VÀ REST API ENDPOINTS TƯƠNG ỨNG

| STT | Phân hệ Web BI | Controller Backend | Endpoints chính | Mô tả chức năng |
| :-: | :--- | :--- | :--- | :--- |
| 1 | **Tổng quan (Executive)** | `DashboardController` | `GET /api/dashboard/summary` | KPI Doanh thu, Lợi nhuận, Đơn hàng, Tồn kho |
| 2 | **Bán hàng (Sales)** | `SalesController` | `GET /api/sales/revenue-by-year`<br>`GET /api/sales/top-products-by-revenue`<br>`GET /api/sales/top-customers-by-revenue` | 20 chỉ số phân tích doanh số, khách hàng, khu vực |
| 3 | **Mua hàng (Purchase)** | `PurchaseController` | `GET /api/purchase/top-suppliers-by-amount`<br>`GET /api/purchase/purchase-price-trend` | Phân tích NCC, giá nhập, xu hướng chi phí mua |
| 4 | **Tồn kho (Inventory)** | `InventoryController` | `GET /api/inventory/highest-stock`<br>`GET /api/inventory/reorder-alerts` | Phân tích tồn kho, cảnh báo hết hàng |
| 5 | **Vận chuyển (Shipping)** | `ShippingController` | `GET /api/shipping/orders-by-shipper`<br>`GET /api/shipping/highest-late-days` | Đánh giá hiệu năng và chi phí các nhà vận chuyển |
| 6 | **Phân tích chéo (Cross)** | `CrossAnalysisController` | `GET /api/cross-analysis/high-sales-low-stock` | Phân tích rủi ro lệch pha Bán - Tồn kho - Mua |
| 7 | **Dự báo (Forecast)** | `ForecastController` | `POST /api/forecast/predict` | Dự báo doanh số bằng mô hình ML.NET |
| 8 | **AI Copilot (NLQ)** | `NlqController` | `POST /api/nlq/query` | Trả lời câu hỏi ngôn ngữ tự nhiên từ dữ liệu DWH |
| 9 | **Trạng thái DWH (ETL Status)**| `EtlController` | `GET /api/etl/status`<br>`POST /api/etl/trigger` | Kiểm tra lịch sử chạy SSIS ETL & kích hoạt nạp |
| 10 | **Xác thực & RBAC** | `AuthController` | `POST /api/auth/login`<br>`GET /api/auth/me` | Đăng nhập cấp JWT Token và kiểm tra vai trò |

## 5. TRẠNG THÁI TÍCH HỢP HỆ THỐNG (INTEGRATION STATUS)

| Phân hệ Web BI | Dữ liệu Nguồn DWH | Trạng thái API | Trạng thái Web UI | Ghi chú |
| :--- | :--- | :--- | :--- | :--- |
| **Sales Analytics** | 65,466 records in `Fact_Sales` | `LIVE REST API` | **COMPLETED** | Tích hợp 100% dữ liệu thực DWH (Doanh thu, Lợi nhuận, Đơn hàng, Top SP, Top KH, Vùng miền). |
| **Executive Overview** | 65,466 records in `Fact_Sales` | `LIVE REST API` | **COMPLETED** | KPI Doanh thu ($16.08M), Lợi nhuận ($1.91M), Đơn hàng (65,466) khớp SQL Server 100%. |
| **Customers** | 17,415 records in `Dim_Customer` | `LIVE REST API` | **COMPLETED** | Phân tích Top Khách hàng VIP & Segment từ `Dim_Customer` + `Fact_Sales`. |
| **Products** | 3,788 records in `Dim_Product` | `LIVE REST API` | **COMPLETED** | Phân tích Top Sản phẩm, Danh mục, Nhóm hàng từ `Dim_Product` + `Fact_Sales`. |
| **Geography** | 3,856 records in `Dim_Location` | `LIVE REST API` | **COMPLETED** | Phân tích Doanh thu/Lợi nhuận theo Quốc gia, Tỉnh thành, Vùng miền từ `Dim_Location`. |
| **Purchase Analytics** | 0 records in `Fact_Purchase` | `NO DATA` | **NO DATA (EMPTY STATE)** | Hiển thị Banner "Chưa có dữ liệu mua hàng thực tế trong Kho dữ liệu DWH". |
| **Inventory Control** | 0 records in `Fact_Inventory` | `NO DATA` | **NO DATA (EMPTY STATE)** | Hiển thị Banner "Chưa có dữ liệu tồn kho định kỳ thực tế trong Kho dữ liệu DWH". |
| **Shipping & SLA** | 0 records in `Fact_Shipping` | `NO DATA` | **NO DATA (EMPTY STATE)** | Hiển thị Banner "Chưa có dữ liệu vận chuyển thực tế trong Kho dữ liệu DWH". |
| **Suppliers** | 0 records in `Dim_Supplier` | `NO DATA` | **NO DATA (EMPTY STATE)** | Hiển thị Banner "Chưa có danh mục Nhà cung cấp thực tế trong Kho dữ liệu DWH". |
| **Data Status & ETL** | Audit 18 bảng DWH | `LIVE REST API` | **COMPLETED** | Hiển thị minh bạch số lượng record từng bảng DWH và kết nối Kestrel REST API. |

