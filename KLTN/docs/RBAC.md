# Role-Based Access Control (RBAC) Specification

The Khang Nghi BI Platform implements a strict Role-Based Access Control (RBAC) mechanism. Route guards prevent unauthorized access, while UI navigation items and AI Copilot responses adapt based on the active user role.

---

## Role Permissions Matrix

| Route / Feature | CEO | Sales | Purchase | Warehouse | Logistics | Admin |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Executive Overview (`#/overview`)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Sales Analytics (`#/sales`)** | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **Purchase Analytics (`#/purchase`)** | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| **Inventory Control (`#/inventory`)** | ✅ | ❌ | ❌ | ✅ | ❌ | ✅ |
| **Shipping & SLA (`#/shipping`)** | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Customer Analytics (`#/customer`)** | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **Product Analytics (`#/product`)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Supplier Analytics (`#/supplier`)** | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| **Geographic Analysis (`#/geography`)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Cross Analysis (`#/cross-analysis`)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **AI Forecast (`#/forecast`)** | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **What-If Simulator (`#/what-if`)** | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **BI Question Explorer (`#/question-explorer`)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Data Status & ETL (`#/system-status`)** | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |

---

## Access Violation Behavior
When a user attempts to navigate to a route outside their role permissions:
1. The SPA router blocks navigation.
2. The user is redirected to the `403 Access Denied` view.
3. The AI Copilot checks role permissions before answering domain questions and displays a role restriction message if access is denied.
