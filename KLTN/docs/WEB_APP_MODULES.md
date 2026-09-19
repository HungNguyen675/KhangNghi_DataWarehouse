# Khang Nghi BI Web Application - Module Specifications

This document describes all 14 modules implemented in the Khang Nghi Business Intelligence Platform.

---

## Module Index

1. **Executive Overview (`#/overview`)**: C-level executive view providing high-level KPIs, monthly revenue/profit trends, category composition, global region breakdown, and AI forecast preview.
2. **Sales Analytics (`#/sales`)**: Detailed sales monitoring workspace with smart filter bar, active filter chips, metric switchers (Revenue, Profit, Quantity, Margin), time granularity (Monthly, Quarterly, Yearly), compare mode (2015 vs 2014), and CSV/PNG exports.
3. **Purchase Analytics (`#/purchase`)**: Procurement workspace monitoring purchase cost, PO count, purchased quantity, weighted average unit cost, supplier spend breakdown, supplier concentration, manufacturer analysis, cost variance, supplier-category matrix, and PO detail drawer.
4. **Inventory Analytics (`#/inventory`)**: Stock health control center monitoring stock on hand, inventory value, low stock, out of stock, overstock, stock coverage, inventory health gauge bar, warehouse distribution, stock movement time-series, and safety stock alerts.
5. **Shipping Analytics (`#/shipping`)**: Logistics SLA monitor tracking total shipments, shipping cost, average delivery days, on-time rate %, late shipments, shipper performance ranking, and late delivery analysis table.
6. **Customer Analytics (`#/customer`)**: Customer behavior workspace analyzing customer count, active customers, revenue per customer, AOV, repeat rate, customer segment composition, customer value scatter matrix, and top customer ranking.
7. **Product Analytics (`#/product`)**: Product performance workspace tracking active products, revenue, profit, average margin, category hierarchy, profitability scatter matrix, and product master table.
8. **Supplier Analytics (`#/supplier`)**: Vendor management workspace tracking active suppliers, purchase cost, purchased quantity, top 1/3/5 supplier concentration, spend breakdown bar/donut, and supplier comparison mode.
9. **Geographic Analytics (`#/geography`)**: Market revenue and shipment breakdown across global regions (North America, South East Asia, Western Europe, East Asia, etc.).
10. **Cross Analysis (`#/cross-analysis`)**: Multi-domain correlation workspace providing dynamic X/Y metric controls, group-by selectors, scatter matrix visualization, non-causal relationship summaries, and correlation data tables.
11. **AI Sales Forecast (`#/forecast`)**: Predictive analytics UI prototype powered by ML.NET SSA showing historical actuals, 2016 forecasts, upper/lower confidence bands, model metadata card, and projected growth summary.
12. **What-If Scenario Simulator (`#/what-if`)**: Decision support tool featuring interactive sliders (Sales growth %, Discount %, Shipping cost %, Purchase cost %), baseline vs scenario profit comparison, and LocalStorage scenario saving.
13. **BI Question Catalog Explorer (`#/question-explorer`)**: Business question registry allowing users to search, filter by domain, inspect question schema metadata, and jump directly to relevant dashboards using `Open Analysis`.
14. **Data Status & ETL Lineage (`#/system-status`)**: System health dashboard monitoring database, API, and ETL status, visual pipeline diagram (Source -> Staging -> Transform -> DWH -> API -> Dashboard), and historical ETL execution log.
