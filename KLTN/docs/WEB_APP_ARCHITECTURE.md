# Khang Nghi BI Web Application - System Architecture

## 1. Overview
The Khang Nghi BI Web Application is an Enterprise Single-Page Application (SPA) designed for Business Intelligence, executive reporting, decision support, predictive analytics, scenario simulation, and business question exploration.

---

## 2. Layered Architecture

```
+-----------------------------------------------------------------------+
|                         Presentation Layer                            |
|             (HTML5 / Vanilla CSS Design Tokens / ApexCharts)           |
+-----------------------------------------------------------------------+
                                   |
                                   v
+-----------------------------------------------------------------------+
|                          Service Layer                                |
|   (analyticsService.js, searchService.js, authService.js, router.js)  |
+-----------------------------------------------------------------------+
                                   |
                                   v
+-----------------------------------------------------------------------+
|                    Mock REST API Engine Layer                         |
|                         (mock-api.js)                                 |
+-----------------------------------------------------------------------+
                                   |
                                   v
+-----------------------------------------------------------------------+
|                    Enterprise Mock Dataset Layer                      |
|                         (mock-data.js)                                |
+-----------------------------------------------------------------------+
```

---

## 3. Design System & UX Standards
- **Typography**: Inter / SF Pro font family.
- **Color Tokens**: CSS variables in `variables.css` (`--color-primary`, `--color-success`, `--color-warning`, `--color-danger`).
- **Cards & Visual Containers**: Glassmorphism (`backdrop-filter: blur(16px)`), CSS Grid layouts (`grid-kpi-4`, `grid-charts-2`).
- **Interactive Controls**: Smart Search with Ctrl+K shortcut, Smart Filter Bar, Active Filter Chips, Metric Switchers, Granularity Selectors, Detail Drawers, Modal Overlays.

---

## 4. Future API Integration (ASP.NET Core)
The frontend mock service abstraction (`analyticsService.js` -> `mock-api.js`) is designed for drop-in replacement with real ASP.NET Core REST API endpoints without redesigning frontend components:
- `GET /api/v1/sales/overview`
- `GET /api/v1/purchase/overview`
- `GET /api/v1/inventory/overview`
- `GET /api/v1/shipping/overview`
- `GET /api/v1/customers/overview`
- `GET /api/v1/products/matrix`
- `GET /api/v1/suppliers/spend`
- `GET /api/v1/cross-analysis`
- `GET /api/v1/forecast/sales`
- `POST /api/v1/what-if/simulate`
- `GET /api/v1/bi-questions`
- `GET /api/v1/system/status`
