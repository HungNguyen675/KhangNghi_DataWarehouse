# Central BI Question Catalog

This catalog documents all Business Intelligence (BI) business questions supported by `DWH_KhangNghi`.

---

## Question Catalog Schema

| Question ID | Domain | Business Question Title | Measures | Dimensions | Fact Table(s) | API Endpoint | Visualization | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **OVW-001** | Overview | Doanh thu, Lợi nhuận và số lượng đơn hàng tổng quan theo thời gian | Revenue, Profit, Orders | Time, Region | `Fact_Order` | `GET /api/v1/overview/summary` | Line Chart, KPI Cards | Implemented |
| **SAL-001** | Sales | Doanh thu & Lợi nhuận bán hàng theo Quý/Năm | Revenue, Profit | Time, Category | `Fact_Order` | `GET /api/v1/sales/revenue-trend` | Line Chart | Implemented |
| **SAL-002** | Sales | Top 10 sản phẩm có doanh thu và lợi nhuận cao nhất | Revenue, Margin | Product | `Fact_Order` | `GET /api/v1/sales/top-products` | Bar Chart | Implemented |
| **PUR-001** | Purchase | Chi phí nhập hàng theo Nhà cung cấp | PurchaseCost | Supplier | `Fact_Purchase` | `GET /api/v1/purchase/cost-by-supplier` | Bar Chart | Implemented |
| **PUR-002** | Purchase | Biến động giá nhập trung bình trọng số (Weighted Avg Unit Cost) | UnitCost | Product, Time | `Fact_Purchase` | `GET /api/v1/purchase/unit-cost-variance` | Line Chart / Table | Implemented |
| **INV-001** | Inventory | Giám sát mức tồn kho thực tế và cảnh báo dưới định mức Safety Stock | StockQty, SafetyStock | Product, Warehouse | `Fact_Inventory` | `GET /api/v1/inventory/stock-levels` | Status Bar, Table | Implemented |
| **INV-002** | Inventory | Tồn kho theo Warehouse và nhóm hàng | InventoryValue | Warehouse, Category | `Fact_Inventory` | `GET /api/v1/inventory/by-warehouse` | Bar Chart | Implemented |
| **SHP-001** | Shipping | Tỷ lệ giao hàng đúng hạn SLA theo Đơn vị vận chuyển | OnTimeRate, LateRate | Shipper, ShipMode | `Fact_Shipping` | `GET /api/v1/shipping/sla-rate` | Donut / Table | Implemented |
| **CUS-001** | Customer | Cơ cấu doanh thu và lợi nhuận theo Phân khúc khách hàng | Revenue, Profit | Customer.Segment | `Fact_Order` | `GET /api/v1/customers/segment` | Donut Chart | Implemented |
| **PRD-001** | Product | Ma trận sinh lời sản phẩm Margin vs Revenue | Revenue, Margin | Product | `Fact_Order` | `GET /api/v1/products/profitability-matrix` | Scatter Matrix | Implemented |
| **SUP-001** | Supplier | Mức độ tập trung nhà cung cấp (Top 1/3/5 Concentration) | PurchaseCost, SharePct | Supplier | `Fact_Purchase` | `GET /api/v1/suppliers/concentration` | Donut Chart | Implemented |
| **CROSS-001**| Cross | Sản phẩm doanh thu cao nhưng giá trị tồn kho thấp | Revenue, InventoryValue | Product, Category | `Fact_Order`, `Fact_Inventory` | `GET /api/v1/cross-analysis/sales-vs-inventory` | Scatter / Table | Implemented |
| **ML-001** | Forecast | Dự báo doanh số 3 - 12 tháng tới (ML.NET SSA) | ForecastSales, Bounds | Time | `Fact_Order` | `GET /api/v1/forecast/sales-predict` | Area Band Chart | Implemented |
| **WIF-001** | What-If | Mô phỏng kịch bản tăng trưởng, chiết khấu và chi phí vận chuyển | ProjectedProfit | Discount, Growth | `Fact_Order` | `GET /api/v1/what-if/simulate` | Interactive Slider Chart | Implemented |

---

## Domain Legend
- **OVW**: Overview
- **SAL**: Sales
- **PUR**: Purchase
- **INV**: Inventory
- **SHP**: Shipping
- **CUS**: Customer
- **PRD**: Product
- **SUP**: Supplier
- **GEO**: Geography
- **CROSS**: Cross Analysis
- **ML**: Machine Learning / Forecast
- **WIF**: What-If Analysis
