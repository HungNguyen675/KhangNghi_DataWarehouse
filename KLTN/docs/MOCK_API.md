# Mock REST API Specification & Future ASP.NET Core Endpoints

This document maps all frontend services in `js/mock-api.js` to their future ASP.NET Core backend endpoints.

---

## Endpoint Mapping Table

| Frontend Mock Function | HTTP Method | Target ASP.NET Core Endpoint | Return Schema |
| :--- | :---: | :--- | :--- |
| `calculateBaseMetrics(filters)` | `GET` | `/api/v1/sales/summary` | `{ revenue, profit, orders, aov, margin }` |
| `getSalesByCategory(filters)` | `GET` | `/api/v1/sales/by-category` | `Array<{ category, revenue, profit }>` |
| `calculatePurchaseBaseMetrics(filters)`| `GET` | `/api/v1/purchase/summary` | `{ totalPurchaseCost, poCount, weightedUnitCost }` |
| `getPurchaseTrend(filters, gran)` | `GET` | `/api/v1/purchase/trend` | `Array<{ label, cost, qty, unitCost }>` |
| `getSupplierSpend(filters)` | `GET` | `/api/v1/purchase/supplier-spend` | `Array<{ id, name, cost, sharePct }>` |
| `getInventoryFullMetrics(filters)` | `GET` | `/api/v1/inventory/summary` | `{ inventoryValue, stockOnHand, health }` |
| `getShippingFullMetrics(filters)` | `GET` | `/api/v1/shipping/summary` | `{ totalShipments, shippingCost, onTimeRate }` |
| `getCustomerFullMetrics(filters)` | `GET` | `/api/v1/customers/summary` | `{ totalCustomers, revenuePerCustomer, segments }` |
| `getProductFullMetrics(filters)` | `GET` | `/api/v1/products/summary` | `{ totalProducts, activeProducts, matrixData }` |
| `getSupplierFullMetrics(filters)` | `GET` | `/api/v1/suppliers/summary` | `{ totalSuppliers, purchaseCost, spend }` |
| `getCrossAnalysisData(mx, my, gb)` | `GET` | `/api/v1/cross-analysis` | `{ metricXLabel, metricYLabel, table }` |
| `getForecastFullData(horizon, metric)` | `GET` | `/api/v1/forecast/sales` | `{ modelCard, summary, series }` |
| `simulateWhatIfScenario(d, s, g)` | `POST` | `/api/v1/what-if/simulate` | `{ currentProfit, projectedProfit, difference }` |
| `getSystemFullStatus()` | `GET` | `/api/v1/system/status` | `{ health, etlHistory }` |
