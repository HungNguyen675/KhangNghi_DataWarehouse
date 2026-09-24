---
name: scalable-bi-architecture
description: >-
  Guidelines and architecture rules for adding, organizing, and maintaining scalable Business Intelligence (BI) business questions, measures, dimensions, API endpoints, dashboard components, and the BI Question Catalog.
---

# Scalable BI Question Architecture & Implementation Workflow

Use this skill whenever adding new BI business questions, extending existing domain dashboards/controllers, performing DWH impact analysis, or maintaining the central BI Question Catalog.

---

## 1. Domain Organization & Identifier Standard

Never hard-code architecture around a static number of questions. Organize questions by **Business Domain** and assign a unique Question Identifier (`QuestionId`):

### Business Domains
- **Overview** (`OVW-xxx`)
- **Sales** (`SAL-xxx`)
- **Purchase** (`PUR-xxx`)
- **Inventory** (`INV-xxx`)
- **Shipping** (`SHP-xxx`)
- **Customer** (`CUS-xxx`)
- **Product** (`PRD-xxx`)
- **Supplier** (`SUP-xxx`)
- **Geographic Analysis** (`GEO-xxx`)
- **Time Analysis** (`TIM-xxx`)
- **Cross Analysis** (`CROSS-xxx`)
- **Forecasting** (`ML-xxx`)
- **What-If Analysis** (`WIF-xxx`)
- *Other future domains*

### Question Schema Document Format

Each BI question must be documented with the following metadata:

```yaml
QuestionId: SAL-001
Title: Doanh thu theo nhóm sản phẩm
Domain: Sales

Measures:
  - Sales

Dimensions:
  - Time
  - Product.Category

FactTables:
  - Fact_Order

DimensionTables:
  - Dim_Time
  - Dim_Product

API: GET /api/v1/sales/revenue-by-category
Visualization: Bar Chart
Roles: CEO, Sales
Status: Implemented
```

---

## 2. Rule & Workflow for New Questions

When a new BI business question is requested, follow this 14-step workflow:

1. **Analyze** the business meaning and intent.
2. **Identify** required measures.
3. **Identify** required dimensions.
4. **Identify** target Fact table(s).
5. **Identify** target Dimension table(s).
6. **Verify** that required data exists in the system.
7. **Determine** whether the existing DWH supports the question without schema changes.
8. **Reuse** an existing API endpoint or Dapper query when possible.
9. **Otherwise**, create a new repository query/service method.
10. **Create or extend** a domain-scoped API endpoint.
11. **Select** an appropriate visualization (Bar Chart, Line Chart, Pie Chart, Heatmap, KPI Card, DataTable).
12. **Add** the question to the central `docs/BI_Question_Catalog.md`.
13. **Test** the query result against SQL Server DWH.
14. **Document** the question metadata and update the catalog.

> [!IMPORTANT]
> Do **NOT** automatically create a new Fact table, Dimension table, API Controller, or Dashboard page for every new question. Maximize architecture reuse.

---

## 3. DWH Impact Analysis (Levels 1 - 4)

Before implementing a new BI question, classify its impact level:

* **LEVEL 1 (Query/UI Only):** Existing Fact + Existing Dimension + Existing Measure.
  * *Action:* Add query logic, API method, or UI component. No DWH schema changes.
* **LEVEL 2 (Calculated Measure):** Existing Fact/Dimension, but requires a new derived calculation.
  * *Action:* Implement calculation in Repository SQL / Dapper.
* **LEVEL 3 (Dimension Extension):** Required attribute is missing from an existing Dimension.
  * *Action:* Evaluate Dimension extension and ETL pipeline update.
* **LEVEL 4 (New Fact / Source Data):** Required business process is not represented in existing Fact tables.
  * *Action:* Evaluate new Fact table design, source data extraction, and ETL workflow.

> [!WARNING]
> Never modify the DWH schema for a **LEVEL 1** or **LEVEL 2** question.

---

## 4. Web Dashboard & API Scalability

### Web Dashboard Scalability
* Do **NOT** create a separate Web page for every single business question.
* Group questions into domain dashboards (*Sales Dashboard, Inventory Dashboard, Cross Analysis Dashboard, etc.*).
* Use reusable frontend components: `KpiCard`, `ChartCard`, `DataTable`, `FilterBar`, `DateFilter`, `RegionFilter`, `CategoryFilter`, `LoadingState`, `EmptyState`, `ErrorState`.

### API Scalability
* Do **NOT** create one Controller per question.
* Group endpoints by business domain:
  * `SalesController` (`/api/v1/sales/*`)
  * `PurchaseController` (`/api/v1/purchase/*`)
  * `InventoryController` (`/api/v1/inventory/*`)
  * `ShippingController` (`/api/v1/shipping/*`)
  * `CrossAnalysisController` (`/api/v1/cross-analysis/*`)
  * `ForecastController` (`/api/v1/forecast/*`)

---

## 5. End-to-End Traceability Pattern

Every BI question must be fully traceable across all architectural layers:

$$\text{BUSINESS QUESTION} \longrightarrow \text{MEASURE + DIMENSION} \longrightarrow \text{FACT + DIM} \longrightarrow \text{SQL / DAPPER QUERY} \longrightarrow \text{API ENDPOINT} \longrightarrow \text{WEB VISUALIZATION}$$

**Example:**
* `SAL-001`: "Lợi nhuận theo nhóm hàng?"
* **Measure + Dimension:** Profit + Product.Category
* **Fact + Dim:** `Fact_Order` + `Dim_Product`
* **Repository:** `SalesRepository.GetProfitByCategoryAsync()`
* **API Endpoint:** `GET /api/v1/sales/profit-by-category`
* **Visualization:** Bar Chart (`ApexCharts`)

---

## 6. Batch Execution Rule for Multiple Questions

When the user requests multiple new BI questions:

1. **Catalog & Classify:** Add questions to `docs/BI_Question_Catalog.md`.
2. **Deduplicate:** Detect duplicate or equivalent questions.
3. **Group:** Group questions by Business Domain.
4. **Shared Queries:** Identify shared queries, SQL measures, and components.
5. **Coverage:** Verify DWH coverage (Levels 1 - 4).
6. **Plan Batches:** Produce a coherent batch implementation plan:
   - *Batch 1:* Sales
   - *Batch 2:* Purchase
   - *Batch 3:* Inventory
   - *Batch 4:* Shipping
   - *Batch 5:* Cross Analysis
   - *Batch 6:* Forecast / What-If
7. **Build & Verify:** Implement, test against SQL, and verify each batch before moving to the next.
