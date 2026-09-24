# MA TRẬN MAPPING DỮ LIỆU THỰC TẾ (REAL DATA MAPPING MATRIX)
**Dự án:** Kho dữ liệu hỗ trợ phân tích hiệu quả hoạt động kinh doanh Công ty Khang Nghị
**Tác giả:** Senior Data Architect & BI Engineer

---

## 1. TỔNG QUAN VỀ NGUỒN DỮ LIỆU VÀ MỨC ĐỘ HỖ TRỢ (DATA SUPPORT STATUS)

| Phân hệ / Bảng Fact | Bảng Dimension liên quan | Trạng thái nguồn dữ liệu (Source Excel) | Mức độ hỗ trợ DWH & API | Ghi chú |
| :--- | :--- | :--- | :--- | :--- |
| **Fact_Sales** | Dim_Time, Dim_Customer, Dim_Product, Dim_Location, Dim_Employee, Dim_Manufacturer, Dim_Promotion, Dim_SalesChannel, Dim_PaymentMethod, Dim_Service | **Có sẵn đầy đủ** trong Excel mẫu (`KhangNghiData.xlsx`) | **FULL SUPPORT** | 20 chỉ số KPI Bán hàng (Q01-Q20) được hỗ trợ hoàn toàn. |
| **Fact_Shipping** | Dim_Time, Dim_Location, Dim_Shipper, Dim_Warehouse | **Có sẵn một phần** (Có Ship Date, Shipper Name, Ship Mode, Freight Cost) | **FULL SUPPORT** | 7 chỉ số KPI Vận chuyển (Q31-Q37) được trích xuất và tính toán từ dữ liệu giao hàng. |
| **Fact_Purchase** | Dim_Time, Dim_Product, Dim_Supplier, Dim_Location, Dim_Warehouse, Dim_PaymentMethod | **KHÔNG CÓ TRONG SOURCE EXCEL** | **NOT SUPPORTED BY RAW SOURCE DATA** *(Cần Script/Staging sinh dữ liệu)* | File Excel chỉ có dữ liệu Bán hàng. Dữ liệu Mua hàng (Q21-Q25) phải được khởi tạo từ script staging sinh lập lịch mua hàng. |
| **Fact_Inventory** | Dim_Time, Dim_Product, Dim_Location, Dim_Warehouse | **KHÔNG CÓ TRONG SOURCE EXCEL** | **NOT SUPPORTED BY RAW SOURCE DATA** *(Cần Script/Staging sinh dữ liệu)* | File Excel không chứa tồn kho định kỳ. Dữ liệu Tồn kho (Q26-Q30) được tính lũy kế từ (Nhập - Xuất) hoặc script staging. |

---

## 2. MA TRẬN MAPPING CHI TIẾT THEO CHUỖI GIAO TIẾP (END-TO-END DATA MAPPING)

Chuỗi Mapping:
`Web KPI` ➔ `API Endpoint` ➔ `DTO Class` ➔ `SQL Query` ➔ `Fact Table` ➔ `Dimension Tables` ➔ `Database Columns`

---

### PHẦN I: PHÂN HỆ TỔNG QUAN (EXECUTIVE OVERVIEW)

#### 1. Doanh thu tổng (Total Revenue)
* **Web KPI:** `Total Revenue` (Trang Tổng quan / Sales Dashboard)
* **API Endpoint:** `GET /api/dashboard/summary`
* **DTO Class:** `DashboardSummaryDto.TotalRevenue` (`double`)
* **SQL Query:** 
  ```sql
  SELECT ROUND(ISNULL(SUM(SalesAmount), 0), 2) AS TotalRevenue FROM dbo.Fact_Sales WITH (NOLOCK);
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** N/A (Tổng hợp toàn bộ)
* **Database Column:** `Fact_Sales.SalesAmount`

#### 2. Lợi nhuận tổng (Total Profit)
* **Web KPI:** `Total Profit`
* **API Endpoint:** `GET /api/dashboard/summary`
* **DTO Class:** `DashboardSummaryDto.TotalProfit` (`double`)
* **SQL Query:** 
  ```sql
  SELECT ROUND(ISNULL(SUM(ProfitAmount), 0), 2) AS TotalProfit FROM dbo.Fact_Sales WITH (NOLOCK);
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** N/A
* **Database Column:** `Fact_Sales.ProfitAmount`

#### 3. Tỷ suất lợi nhuận (Profit Margin %)
* **Web KPI:** `Profit Margin (%)`
* **API Endpoint:** `GET /api/dashboard/summary`
* **DTO Class:** `DashboardSummaryDto.ProfitMargin` (`double`)
* **SQL Query:** 
  ```sql
  SELECT ROUND(CASE WHEN SUM(SalesAmount) > 0 THEN (SUM(ProfitAmount) / SUM(SalesAmount)) * 100 ELSE 0 END, 2) AS ProfitMargin FROM dbo.Fact_Sales WITH (NOLOCK);
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** N/A
* **Database Column:** `Fact_Sales.SalesAmount`, `Fact_Sales.ProfitAmount`

#### 4. Tổng số đơn hàng (Total Orders)
* **Web KPI:** `Total Orders`
* **API Endpoint:** `GET /api/dashboard/summary`
* **DTO Class:** `DashboardSummaryDto.TotalOrders` (`int`)
* **SQL Query:** 
  ```sql
  SELECT COUNT(DISTINCT OrderID) AS TotalOrders FROM dbo.Fact_Sales WITH (NOLOCK);
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** N/A
* **Database Column:** `Fact_Sales.OrderID`

---

### PHẦN II: PHÂN HỆ BÁN HÀNG (SALES ANALYTICS - Q01 THỚI Q20)

#### Q01: Doanh thu & Lợi nhuận theo Năm (Revenue & Profit by Year)
* **Web KPI:** Biểu đồ xu hướng Doanh thu theo Năm
* **API Endpoint:** `GET /api/sales/revenue-by-year`
* **DTO Class:** `IEnumerable<RevenueByYearDto>` (`Year`, `Revenue`, `Profit`, `Quantity`)
* **SQL Query:** 
  ```sql
  SELECT t.Year, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit, SUM(f.Quantity) AS Quantity
  FROM dbo.Fact_Sales f WITH (NOLOCK)
  JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
  GROUP BY t.Year ORDER BY t.Year ASC;
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** `Dim_Time`
* **Database Column:** `Fact_Sales.SalesAmount`, `Fact_Sales.ProfitAmount`, `Fact_Sales.Quantity`, `Dim_Time.Year`

#### Q06: Top Sản phẩm có doanh thu cao nhất (Top Products by Revenue)
* **Web KPI:** Bảng Top 10 Sản Phẩm Doanh Thu Cao
* **API Endpoint:** `GET /api/sales/top-products-by-revenue?top=10`
* **DTO Class:** `IEnumerable<ProductRevenueDto>` (`ProductKey`, `ProductName`, `Category`, `SubCategory`, `Revenue`, `Quantity`)
* **SQL Query:** 
  ```sql
  SELECT TOP (10) p.ProductKey, p.ProductName, p.Category, p.SubCategory, ROUND(SUM(f.SalesAmount), 2) AS Revenue, SUM(f.Quantity) AS Quantity
  FROM dbo.Fact_Sales f WITH (NOLOCK)
  JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
  GROUP BY p.ProductKey, p.ProductName, p.Category, p.SubCategory ORDER BY Revenue DESC;
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** `Dim_Product`
* **Database Column:** `Fact_Sales.SalesAmount`, `Fact_Sales.Quantity`, `Dim_Product.ProductName`, `Dim_Product.Category`, `Dim_Product.SubCategory`

#### Q08: Doanh thu theo Danh mục sản phẩm (Revenue by Category)
* **Web KPI:** Biểu đồ cơ cấu Doanh thu Danh mục (Category)
* **API Endpoint:** `GET /api/sales/revenue-by-category`
* **DTO Class:** `IEnumerable<CategoryRevenueDto>` (`Category`, `Revenue`, `Profit`, `Quantity`, `ProfitMargin`)
* **SQL Query:** 
  ```sql
  SELECT p.Category, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit, SUM(f.Quantity) AS Quantity
  FROM dbo.Fact_Sales f WITH (NOLOCK)
  JOIN dbo.Dim_Product p WITH (NOLOCK) ON f.ProductKey = p.ProductKey
  GROUP BY p.Category ORDER BY Revenue DESC;
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** `Dim_Product`
* **Database Column:** `Fact_Sales.SalesAmount`, `Fact_Sales.ProfitAmount`, `Dim_Product.Category`

#### Q11: Top Khách hàng theo Doanh thu (Top Customers by Revenue)
* **Web KPI:** Bảng Top Khách Hàng VIP
* **API Endpoint:** `GET /api/sales/top-customers-by-revenue?top=10`
* **DTO Class:** `IEnumerable<CustomerRevenueDto>` (`CustomerKey`, `CustomerName`, `Segment`, `Revenue`, `OrderCount`)
* **SQL Query:** 
  ```sql
  SELECT TOP (10) c.CustomerKey, c.CustomerName, c.Segment, ROUND(SUM(f.SalesAmount), 2) AS Revenue, COUNT(DISTINCT f.OrderID) AS OrderCount
  FROM dbo.Fact_Sales f WITH (NOLOCK)
  JOIN dbo.Dim_Customer c WITH (NOLOCK) ON f.CustomerKey = c.CustomerKey
  GROUP BY c.CustomerKey, c.CustomerName, c.Segment ORDER BY Revenue DESC;
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** `Dim_Customer`
* **Database Column:** `Fact_Sales.SalesAmount`, `Fact_Sales.OrderID`, `Dim_Customer.CustomerName`, `Dim_Customer.Segment`

#### Q15: Doanh thu theo Vùng miền (Revenue by Region)
* **Web KPI:** Bản đồ / Biểu đồ Doanh thu Vùng miền
* **API Endpoint:** `GET /api/sales/revenue-by-region`
* **DTO Class:** `IEnumerable<RegionRevenueDto>` (`Region`, `Revenue`, `Profit`, `Quantity`)
* **SQL Query:** 
  ```sql
  SELECT l.Region, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit, SUM(f.Quantity) AS Quantity
  FROM dbo.Fact_Sales f WITH (NOLOCK)
  JOIN dbo.Dim_Location l WITH (NOLOCK) ON f.LocationKey = l.LocationKey
  GROUP BY l.Region ORDER BY Revenue DESC;
  ```
* **Fact Table:** `Fact_Sales`
* **Dimension Tables:** `Dim_Location`
* **Database Column:** `Fact_Sales.SalesAmount`, `Fact_Sales.ProfitAmount`, `Dim_Location.Region`

---

### PHẦN III: PHÂN HỆ MUA HÀNG (PURCHASE ANALYTICS - Q21 THỚI Q25)
> **Ghi chú quan trọng:** Dữ liệu phân hệ Mua hàng dựa trên bảng `Fact_Purchase` & `Dim_Supplier`. Trong file Excel nguồn dữ liệu bán hàng thô không có sheet Order Mua hàng. Dữ liệu này được sinh lập lịch (Staging population script) vào DWH.

#### Q21: Top Nhà cung cấp theo Giá trị Mua hàng (Top Suppliers by Purchase Amount)
* **Web KPI:** Bảng Top Nhà Cung Cấp
* **API Endpoint:** `GET /api/purchase/top-suppliers-by-amount`
* **DTO Class:** `IEnumerable<SupplierPurchaseDto>` (`SupplierName`, `TotalPurchaseAmount`, `TotalQuantity`)
* **SQL Query:** 
  ```sql
  SELECT s.SupplierName, ROUND(SUM(p.TotalCost), 2) AS TotalPurchaseAmount, SUM(p.PurchaseQuantity) AS TotalQuantity
  FROM dbo.Fact_Purchase p WITH (NOLOCK)
  JOIN dbo.Dim_Supplier s WITH (NOLOCK) ON p.SupplierKey = s.SupplierKey
  GROUP BY s.SupplierName ORDER BY TotalPurchaseAmount DESC;
  ```
* **Fact Table:** `Fact_Purchase`
* **Dimension Tables:** `Dim_Supplier`
* **Database Column:** `Fact_Purchase.TotalCost`, `Fact_Purchase.PurchaseQuantity`, `Dim_Supplier.SupplierName`

#### Q24: Xu hướng Giá Nhập Sản phẩm (Purchase Price Trend)
* **Web KPI:** Biểu đồ biến động giá nhập theo thời gian
* **API Endpoint:** `GET /api/purchase/purchase-price-trend`
* **DTO Class:** `IEnumerable<PurchasePriceTrendDto>` (`ProductName`, `Period`, `Year`, `Month`, `AvgUnitCost`)
* **SQL Query:** 
  ```sql
  SELECT TOP 30 pr.ProductName, CONCAT(t.Month, '/', t.Year) AS Period, t.Year, t.Month, ROUND(AVG(p.UnitCost), 2) AS AvgUnitCost
  FROM dbo.Fact_Purchase p WITH (NOLOCK)
  JOIN dbo.Dim_Product pr WITH (NOLOCK) ON p.ProductKey = pr.ProductKey
  JOIN dbo.Dim_Time t WITH (NOLOCK) ON p.TimeKey = t.TimeKey
  GROUP BY pr.ProductName, t.Year, t.Month ORDER BY t.Year, t.Month;
  ```
* **Fact Table:** `Fact_Purchase`
* **Dimension Tables:** `Dim_Product`, `Dim_Time`
* **Database Column:** `Fact_Purchase.UnitCost`, `Dim_Product.ProductName`, `Dim_Time.Year`, `Dim_Time.Month`

---

### PHẦN IV: PHÂN HỆ TỒN KHO (INVENTORY ANALYTICS - Q26 THỚI Q30)

#### Q26: Top Sản phẩm Tồn kho Cao nhất (Highest Stock Products)
* **Web KPI:** Thống kê sản phẩm tồn kho đọng vốn
* **API Endpoint:** `GET /api/inventory/highest-stock?top=10`
* **DTO Class:** `IEnumerable<ProductStockDto>` (`ProductName`, `Category`, `TotalOnHand`, `AvgReorderLevel`)
* **SQL Query:** 
  ```sql
  SELECT TOP (10) pr.ProductName, pr.Category, SUM(i.QuantityOnHand) AS TotalOnHand, AVG(i.ReorderLevel) AS AvgReorderLevel
  FROM dbo.Fact_Inventory i WITH (NOLOCK)
  JOIN dbo.Dim_Product pr WITH (NOLOCK) ON i.ProductKey = pr.ProductKey
  GROUP BY pr.ProductName, pr.Category ORDER BY TotalOnHand DESC;
  ```
* **Fact Table:** `Fact_Inventory`
* **Dimension Tables:** `Dim_Product`
* **Database Column:** `Fact_Inventory.QuantityOnHand`, `Fact_Inventory.ReorderLevel`, `Dim_Product.ProductName`

#### Q28: Cảnh báo Sản phẩm Dưới Ngưỡng Tồn Kho (Reorder Level Alerts)
* **Web KPI:** Danh sách Cảnh báo Nhập hàng gấp
* **API Endpoint:** `GET /api/inventory/reorder-alerts`
* **DTO Class:** `IEnumerable<InventoryAlertDto>` (`ProductKey`, `ProductName`, `QuantityOnHand`, `ReorderLevel`, `ShortageQuantity`, `WarehouseName`)
* **SQL Query:** 
  ```sql
  SELECT pr.ProductKey, pr.ProductName, i.QuantityOnHand, i.ReorderLevel, (i.ReorderLevel - i.QuantityOnHand) AS ShortageQuantity, w.WarehouseName
  FROM dbo.Fact_Inventory i WITH (NOLOCK)
  JOIN dbo.Dim_Product pr WITH (NOLOCK) ON i.ProductKey = pr.ProductKey
  JOIN dbo.Dim_Warehouse w WITH (NOLOCK) ON i.WarehouseKey = w.WarehouseKey
  WHERE i.QuantityOnHand < i.ReorderLevel ORDER BY ShortageQuantity DESC;
  ```
* **Fact Table:** `Fact_Inventory`
* **Dimension Tables:** `Dim_Product`, `Dim_Warehouse`
* **Database Column:** `Fact_Inventory.QuantityOnHand`, `Fact_Inventory.ReorderLevel`, `Dim_Warehouse.WarehouseName`

---

### PHẦN V: PHÂN HỆ VẬN CHUYỂN (SHIPPING ANALYTICS - Q31 THỚI Q37)

#### Q31: Số lượng Đơn hàng theo Đơn vị Vận chuyển (Orders by Shipper)
* **Web KPI:** Biểu đồ thị phần đơn vị vận chuyển
* **API Endpoint:** `GET /api/shipping/orders-by-shipper`
* **DTO Class:** `IEnumerable<ShipperOrderDto>` (`ShipperName`, `ShippingMethod`, `OrderCount`, `TotalShippingCost`)
* **SQL Query:** 
  ```sql
  SELECT s.ShipperName, s.ShippingMethod, COUNT(DISTINCT f.OrderID) AS OrderCount, ROUND(SUM(f.ShippingCost), 2) AS TotalShippingCost
  FROM dbo.Fact_Shipping f WITH (NOLOCK)
  JOIN dbo.Dim_Shipper s WITH (NOLOCK) ON f.ShipperKey = s.ShipperKey
  GROUP BY s.ShipperName, s.ShippingMethod ORDER BY OrderCount DESC;
  ```
* **Fact Table:** `Fact_Shipping`
* **Dimension Tables:** `Dim_Shipper`
* **Database Column:** `Fact_Shipping.OrderID`, `Fact_Shipping.ShippingCost`, `Dim_Shipper.ShipperName`

#### Q35: Đơn vị Vận chuyển có Trễ hẹn Cao nhất (Highest Late Days Shippers)
* **Web KPI:** Chỉ số đánh giá chất lượng giao hàng trễ
* **API Endpoint:** `GET /api/shipping/highest-late-days`
* **DTO Class:** `IEnumerable<ShipperLateDto>` (`ShipperName`, `AvgLateDays`, `MaxLateDays`, `LateOrderCount`)
* **SQL Query:** 
  ```sql
  SELECT s.ShipperName, AVG(f.LateDays) AS AvgLateDays, MAX(f.LateDays) AS MaxLateDays, COUNT(CASE WHEN f.LateDays > 0 THEN 1 END) AS LateOrderCount
  FROM dbo.Fact_Shipping f WITH (NOLOCK)
  JOIN dbo.Dim_Shipper s WITH (NOLOCK) ON f.ShipperKey = s.ShipperKey
  GROUP BY s.ShipperName ORDER BY AvgLateDays DESC;
  ```
* **Fact Table:** `Fact_Shipping`
* **Dimension Tables:** `Dim_Shipper`
* **Database Column:** `Fact_Shipping.LateDays`, `Dim_Shipper.ShipperName`

---

## 3. ĐÁNH GIÁ CÁC DIMENSION ĐẶC BIỆT & THỦ THUẬT XỬ LÝ (SPECIAL DIMENSIONS ASSESSMENT)

| Dimension | Nguồn dữ liệu thực tế (Excel) | Trạng thái hiện tại | Giải pháp KLTN |
| :--- | :--- | :--- | :--- |
| **Dim_Supplier** | Không có cột Supplier trong Excel | Đã tạo bảng DDL DWH, chưa có package SSIS nguồn Excel | Sinh dữ liệu mẫu hợp lệ gắn với `Dim_Product` trong `Fact_Purchase`. |
| **Dim_Manufacturer** | Có thông tin Hãng sản xuất trong Excel (`Manufacturer`) | Đã có DDL bảng & Package SSIS `Dim_Product` | Trích xuất unique ManufacturerName vào `Dim_Manufacturer` thông qua SSIS transform. |
| **Dim_Warehouse** | Không có kho cụ thể trong Excel | Đã tạo DDL bảng `Dim_Warehouse` | Tạo kho mặc định (Kho Tổng, Kho Miền Bắc, Kho Miền Nam) gán ngẫu nhiên theo `Dim_Location.Region`. |

---

## 4. TỔNG KẾT VỀ MAPPING DATA
1. Tất cả 40 câu hỏi phân tích (Q01 - Q40) đã được xây dựng sẵn hàm Query SQL Dapper trong `DwhAnalyticsRepository.cs`.
2. Dữ liệu từ DWH được map chuẩn hóa 1-1 với Web API Response DTOs.
3. Việc tích hợp frontend chỉ cần thay thế hàm Mock JS bằng lệnh `fetch('/api/...')` gửi JWT Token.
