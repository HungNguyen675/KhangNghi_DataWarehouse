/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - MAIN APPLICATION CONTROLLER
   Orchestrator for App Shell, Skeleton Shimmer, Drawers, Copilot & What-If
   ========================================================================== */

document.addEventListener("DOMContentLoaded", function () {
  const Svc = window.analyticsService;
  const Comp = window.KhangNghiComponents;
  const Charts = window.KhangNghiCharts;
  const Filters = window.KhangNghiFilters;
  const Router = window.KhangNghiRouter;

  // Initialize App Shell Modules
  Filters.init();
  Router.init();
  initSidebarToggle();
  initThemeToggle();
  initDropdownMenus();
  initCopilotModal();
  initDataLineageModal();
  initWhatIfSliders();
  initDetailDrawer();

  // Initial render & reaction to Filter / Route changes
  renderCurrentView();

  Filters.onChange(() => renderCurrentView());
  Router.onRouteChanged(() => renderCurrentView());

  // --------------------------------------------------------------------------
  // ASYNC SKELETON VIEW DISPATCHER
  // --------------------------------------------------------------------------
  async function renderCurrentView() {
    const route = Router.getCurrentRoute();
    const filterState = Filters.getState();

    // Show Skeleton Loaders during simulated API fetch
    showSkeletonLoaders(route);

    switch (route) {
      case "overview":
        await renderOverviewView(filterState);
        break;
      case "sales":
        await renderSalesView(filterState);
        break;
      case "purchase":
        await renderPurchaseView(filterState);
        break;
      case "inventory":
        await renderInventoryView(filterState);
        break;
      case "shipping":
        await renderShippingView(filterState);
        break;
      case "customer":
        await renderCustomerView(filterState);
        break;
      case "product":
        await renderProductView(filterState);
        break;
      case "supplier":
        await renderSupplierView(filterState);
        break;
      case "geography":
        await renderGeographyView(filterState);
        break;
      case "cross-analysis":
        await renderCrossAnalysisView(filterState);
        break;
      case "forecast":
        await renderForecastView(filterState);
        break;
      case "what-if":
        await renderWhatIfView(filterState);
        break;
      case "question-explorer":
        await renderQuestionExplorerView(filterState);
        break;
      case "system-status":
        await renderSystemStatusView(filterState);
        break;
    }
  }

  function showSkeletonLoaders(route) {
    const kpiEl = document.getElementById(`${route}-kpis`);
    if (kpiEl) {
      kpiEl.innerHTML = Array(4).fill(Comp.renderSkeletonCard()).join("");
    }
  }

  // --------------------------------------------------------------------------
  // 01. EXECUTIVE OVERVIEW RENDERER (ROLE-AWARE)
  // --------------------------------------------------------------------------
  async function renderOverviewView(filterState) {
    const data = await Svc.getExecutiveOverview(filterState);
    const m = data.metrics;
    const auth = window.KhangNghiAuth;
    const user = auth ? auth.getCurrentUser() : null;

    const kpiEl = document.getElementById("overview-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "ov-rev", label: "Tổng Doanh Thu", value: m.revenue, changePct: 14.2, icon: "💰" })}
        ${Comp.renderKpiCard({ id: "ov-prof", label: "Tổng Lợi Nhuận", value: m.profit, changePct: 18.5, icon: "📈" })}
        ${Comp.renderKpiCard({ id: "ov-ord", label: "Tổng Đơn Hàng", value: m.orders, changePct: 9.4, icon: "📦", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "ov-margin", label: "Tỷ Suất Margin", value: parseFloat(m.margin), changePct: 2.1, icon: "📊", isCurrency: false, unit: "%" })}
        ${Comp.renderKpiCard({ id: "ov-cust", label: "Tổng Khách Hàng", value: m.customers || 17442, changePct: 12.0, icon: "👥", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "ov-qty", label: "Số Lượng Bán Ra", value: m.quantity || 231905, changePct: 15.3, icon: "🏷️", isCurrency: false })}
      `;
    }

    setTimeout(() => {
      Charts.renderSparkline("spark-ov-rev", [12, 14, 15, 18, 20, 24, 28], "#0f62fe");
      Charts.renderSparkline("spark-ov-prof", [3, 4, 4.5, 5.2, 6.1, 7.5], "#198038");
      Charts.renderSparkline("spark-ov-ord", [400, 420, 450, 490, 520, 580], "#8a3ffc");
      Charts.renderSparkline("spark-ov-margin", [10, 10.5, 11, 11.8, 12.1], "#b25900");
      Charts.renderSparkline("spark-ov-inv", [90, 88, 85, 84, 82], "#da1e28");
      Charts.renderSparkline("spark-ov-sla", [92, 93, 94, 95, 95.8], "#198038");

      Charts.renderRevenueTrend("chart-ov-trend", data.forecastSeries);
      Charts.renderSalesByCategory("chart-ov-category", data.categoryBreakdown);
      Charts.renderRegionHorizontalBar("chart-ov-region", data.regionBreakdown);
      Charts.renderCustomerSegmentDonut("chart-ov-segment", data.customerSegments);
      Charts.renderForecastChart("chart-ov-forecast-preview", data.forecastSeries);
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 02. SALES VIEW RENDERER (SMART FILTER BAR & CROSS-FILTER INTEGRATION)
  // --------------------------------------------------------------------------
  async function renderSalesView(filterState) {
    // 1. Sync Smart Filter Bar Controls with Filter State
    const searchInp = document.getElementById("sales-search-input");
    if (searchInp && searchInp.value !== (filterState.search || "")) {
      searchInp.value = filterState.search || "";
    }

    const yrSel = document.getElementById("sales-filter-year");
    if (yrSel) yrSel.value = filterState.year || "2015";

    const regSel = document.getElementById("sales-filter-region");
    if (regSel) regSel.value = filterState.region || "All Regions";

    const catSel = document.getElementById("sales-filter-category");
    if (catSel) catSel.value = filterState.category || "All Categories";

    const segSel = document.getElementById("sales-filter-segment");
    if (segSel) segSel.value = filterState.segment || "All Segments";

    // 2. Render Active Filter Chips Container
    if (Filters && Filters.renderChipsContainer) {
      Filters.renderChipsContainer("sales-active-chips");
    }

    // 3. Fetch aggregated Sales data for current filters
    const data = await Svc.getSalesOverview(filterState);
    const m = data.metrics;

    // 4. Dynamic Subtitle Context
    const subEl = document.getElementById("sales-dynamic-subtitle");
    if (subEl) {
      const yrText = filterState.year === "ALL" ? "Tất cả năm" : `Năm ${filterState.year || 2015}`;
      const regText = filterState.region && filterState.region !== "All Regions" ? filterState.region : "Toàn cầu";
      const liveBadge = data.isLiveApi 
        ? ` <span style="background:rgba(37,162,68,0.15); color:#25a244; font-size:11px; padding:2px 8px; border-radius:10px; font-weight:600; margin-left:8px;">● REST API LIVE (DWH)</span>` 
        : ` <span style="background:rgba(241,196,15,0.15); color:#f1c40f; font-size:11px; padding:2px 8px; border-radius:10px; font-weight:600; margin-left:8px;">● MOCK FALLBACK</span>`;
      subEl.innerHTML = `Hiển thị <strong>${m.orders ? m.orders.toLocaleString() : 0}</strong> đơn hàng | Doanh thu: <strong>${Comp.formatCurrency(m.revenue)}</strong> | Lợi nhuận: <strong>${Comp.formatCurrency(m.profit)}</strong> (${yrText}, ${regText})${liveBadge}`;
    }

    // 5. Check empty state (if filtered metrics equal zero or empty)
    const emptyState = document.getElementById("sales-empty-state");
    const chartsContainer = document.getElementById("sales-charts-container");
    if (m.revenue === 0 || m.orders === 0) {
      if (emptyState) emptyState.style.display = "block";
      if (chartsContainer) chartsContainer.style.display = "none";
    } else {
      if (emptyState) emptyState.style.display = "none";
      if (chartsContainer) chartsContainer.style.display = "grid";
    }

    // 6. Render KPI Summary Cards (with Compare Mode delta if active)
    const kpiEl = document.getElementById("sales-kpis");
    if (kpiEl) {
      const isCompare = filterState.compareMode;
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "sal-rev", label: isCompare ? "Doanh Thu (vs 2014)" : "Doanh Thu Bán Hàng", value: m.revenue, changePct: isCompare ? 14.2 : m.priorRevChange, icon: "💵" })}
        ${Comp.renderKpiCard({ id: "sal-prof", label: isCompare ? "Lợi Nhuận (vs 2014)" : "Lợi Nhuận Ròng", value: m.profit, changePct: isCompare ? 18.5 : m.priorProfChange, icon: "💎" })}
        ${Comp.renderKpiCard({ id: "sal-ord", label: isCompare ? "Đơn Hàng (vs 2014)" : "Số Lượng Đơn", value: m.orders, changePct: isCompare ? 9.4 : m.priorOrdChange, icon: "🛒", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "sal-aov", label: "Giá Trị Đơn TB (AOV)", value: m.aov, changePct: 4.5, icon: "🏷️" })}
      `;
    }

    // 7. Render Charts with Cross-Filtering and Switcher Options
    setTimeout(() => {
      // Trend Chart
      Charts.renderRevenueTrend("chart-sal-trend", data.monthlySales, {
        metric: filterState.metric || "revenue",
        granularity: filterState.granularity || "monthly",
        onPointClick: (timeLabel) => {
          Comp.showToast(`📊 Cross-filter: Đã chọn mốc thời gian [${timeLabel}]`);
        }
      });

      // Category / Ranking Chart
      Charts.renderSalesByCategory("chart-sal-cat", data.categories || [], {
        metric: filterState.metric || "revenue",
        rankingMode: filterState.rankingMode || "top10",
        onBarClick: (categoryName) => {
          Comp.showToast(`🔍 Cross-filter: Lọc theo nhóm sản phẩm [${categoryName}]`);
          Filters.setFilter("category", categoryName);
        }
      });
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 03. PURCHASE VIEW RENDERER (WORKSPACES BI & DETAILED PROCUREMENT ENGINE)
  // --------------------------------------------------------------------------
  async function renderPurchaseView(filterState) {
    // 1. Sync Smart Filter Bar Controls with Filter State
    const searchInp = document.getElementById("purchase-search-input");
    if (searchInp && searchInp.value !== (filterState.search || "")) {
      searchInp.value = filterState.search || "";
    }

    const yrSel = document.getElementById("purchase-filter-year");
    if (yrSel) yrSel.value = filterState.year || "2015";

    const supSel = document.getElementById("purchase-filter-supplier");
    if (supSel) supSel.value = filterState.supplier || "All Suppliers";

    const catSel = document.getElementById("purchase-filter-category");
    if (catSel) catSel.value = filterState.category || "All Categories";

    const mfgSel = document.getElementById("purchase-filter-manufacturer");
    if (mfgSel) mfgSel.value = filterState.manufacturer || "All Manufacturers";

    // 2. Render Active Filter Chips Container
    if (Filters && Filters.renderChipsContainer) {
      Filters.renderChipsContainer("purchase-active-chips");
    }

    // 3. Fetch aggregated Purchase overview data
    const data = await Svc.getPurchaseOverview(filterState);
    const m = data.metrics;

    // 4. Dynamic Subtitle Context
    const subEl = document.getElementById("purchase-dynamic-subtitle");
    if (subEl) {
      const yrText = filterState.year === "ALL" ? "Tất cả năm" : `Năm ${filterState.year || 2015}`;
      const supText = filterState.supplier && filterState.supplier !== "All Suppliers" ? filterState.supplier : "Tất cả nhà cung cấp";
      subEl.innerHTML = `Hiển thị <strong>${m.poCount ? m.poCount.toLocaleString() : 0}</strong> đơn mua (PO) | Tổng chi phí mua: <strong>${Comp.formatCurrency(m.totalPurchaseCost)}</strong> | Giá mua TB: <strong>$${m.weightedUnitCost}</strong>/sp (${yrText}, ${supText})`;
    }

    // 5. Check Empty State
    const emptyState = document.getElementById("purchase-empty-state");
    const mainContainer = document.getElementById("purchase-main-container");
    if (m.totalPurchaseCost === 0 || m.poCount === 0) {
      if (emptyState) emptyState.style.display = "block";
      if (mainContainer) mainContainer.style.display = "none";
    } else {
      if (emptyState) emptyState.style.display = "none";
      if (mainContainer) mainContainer.style.display = "block";
    }

    // 6. Render 6 KPI Summary Cards with Tooltips & Compare Mode support
    const kpiEl = document.getElementById("purchase-kpis");
    if (kpiEl) {
      const isCompare = filterState.compareMode;
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "pur-cost", label: isCompare ? "Tổng Chi Phí Mua (vs 2014)" : "TOTAL PURCHASE COST", value: m.totalPurchaseCost, changePct: isCompare ? 20.5 : m.priorCostChange, icon: "🛒", tooltip: "Tổng chi phí mua hàng trong kỳ chọn, được tính bằng tổng giá trị tất cả các đơn đặt hàng nhập (PO)." })}
        ${Comp.renderKpiCard({ id: "pur-orders", label: isCompare ? "Đơn Hàng Mua (vs 2014)" : "PURCHASE ORDERS", value: m.poCount, changePct: isCompare ? 18.1 : m.priorPOChange, icon: "📋", isCurrency: false, tooltip: "Tổng số lượng đơn đặt hàng mua (PO) đã phát hành với các nhà cung cấp." })}
        ${Comp.renderKpiCard({ id: "pur-qty", label: isCompare ? "Số Lượng Mua (vs 2014)" : "PURCHASED QUANTITY", value: m.purchasedQty, changePct: isCompare ? 18.3 : m.priorQtyChange, icon: "📦", isCurrency: false, tooltip: "Tổng số lượng sản phẩm/hàng hóa đã nhập kho trong kỳ chọn." })}
        ${Comp.renderKpiCard({ id: "pur-unit-cost", label: "AVG UNIT COST", value: m.weightedUnitCost, changePct: 1.8, icon: "🏷️", unit: "/sp", tooltip: "Chi phí mua trung bình trên một đơn vị sản phẩm, được tính bằng tổng chi phí mua chia tổng số lượng mua." })}
        ${Comp.renderKpiCard({ id: "pur-active-sup", label: "ACTIVE SUPPLIERS", value: m.activeSuppliers, changePct: 0, icon: "🤝", isCurrency: false, tooltip: "Số lượng nhà cung cấp phát sinh đơn đặt hàng trong kỳ phân tích." })}
        ${Comp.renderKpiCard({ id: "pur-avg-po", label: "AVG PO VALUE", value: m.avgPOValue, changePct: 2.1, icon: "📜", tooltip: "Giá trị trung bình của một đơn đặt hàng mua (PO)." })}
      `;
    }

    // 7. Render Charts & Components
    setTimeout(() => {
      // Main Purchase Trend Chart
      Charts.renderRevenueTrend("chart-pur-trend", data.trend, {
        metric: filterState.metric || "cost",
        granularity: filterState.granularity || "monthly",
        onPointClick: (timeLabel) => {
          Comp.showToast(`📊 Cross-filter Purchase: Mốc thời gian [${timeLabel}]`);
        }
      });

      // Supplier Spend Horizontal Bar
      Charts.renderSupplierBarChart("chart-pur-supplier", data.supplierSpend || [], {
        metric: filterState.metric || "cost",
        rankingMode: filterState.rankingMode || "top5",
        onBarClick: (supplierName) => {
          Comp.showToast(`🚚 Cross-filter: Lọc theo Nhà cung cấp [${supplierName}]`);
          Filters.setFilter("supplier", supplierName);
        }
      });

      // Supplier Donut & Concentration Card
      Charts.renderSupplierDonutChart("chart-pur-sup-donut", data.supplierSpend || []);
      const conc = data.supplierConcentration || {};
      const concTop1 = document.getElementById("sup-conc-top1");
      const concDesc = document.getElementById("sup-conc-desc");
      const concTop3 = document.getElementById("sup-conc-top3");
      const concTop5 = document.getElementById("sup-conc-top5");
      if (concTop1) concTop1.textContent = `${conc.top1SharePct}%`;
      if (concDesc) concDesc.textContent = `Top 1 Supplier (${conc.top1Supplier}) chiếm ${conc.top1SharePct}% tổng spend.`;
      if (concTop3) concTop3.textContent = `${conc.top3SharePct}%`;
      if (concTop5) concTop5.textContent = `${conc.top5SharePct}%`;

      // Purchase Cost by Category Bar
      Charts.renderSalesByCategory("chart-pur-cat", data.categorySpend || [], {
        onBarClick: (categoryName) => {
          Comp.showToast(`📦 Cross-filter: Lọc theo Danh mục [${categoryName}]`);
          Filters.setFilter("category", categoryName);
        }
      });

      // Purchase by Manufacturer Bar
      Charts.renderManufacturerBarChart("chart-pur-mfg", data.manufacturerAnalysis || [], {
        onBarClick: (mfgName) => {
          Comp.showToast(`🏭 Cross-filter: Lọc theo Hãng sản xuất [${mfgName}]`);
          Filters.setFilter("manufacturer", mfgName);
        }
      });

      // Weighted Average Unit Cost Trend
      Charts.renderUnitCostLineChart("chart-pur-unit-cost", data.trend || []);

      // Cost Variance Table
      const varBody = document.getElementById("pur-cost-variance-body");
      if (varBody) {
        varBody.innerHTML = (data.costVariance || []).map(v => `
          <tr>
            <td><strong>${v.name}</strong><br><span style="font-size:10.5px; color:var(--text-muted);">${v.category}</span></td>
            <td>${Comp.formatCurrency(v.prevCost)}</td>
            <td><strong>${Comp.formatCurrency(v.currentCost)}</strong></td>
            <td>
              <span class="badge ${v.changePct > 5 ? 'badge-danger' : 'badge-healthy'}">
                ${v.trend === 'up' ? '↑' : '↓'} ${Math.abs(v.changePct)}%
              </span>
            </td>
          </tr>
        `).join('');
      }

      // Supplier x Category Matrix Table
      const matrixBody = document.getElementById("pur-matrix-body");
      if (matrixBody) {
        matrixBody.innerHTML = (data.supplierCategoryMatrix || []).map(row => {
          const totalRow = row.thietBi + row.noiThat + row.doDung;
          return `
            <tr>
              <td><strong>${row.supplier}</strong></td>
              <td>${row.thietBi > 0 ? Comp.formatCurrency(row.thietBi) : '-'}</td>
              <td>${row.noiThat > 0 ? Comp.formatCurrency(row.noiThat) : '-'}</td>
              <td>${row.doDung > 0 ? Comp.formatCurrency(row.doDung) : '-'}</td>
              <td><strong style="color:var(--color-primary);">${Comp.formatCurrency(totalRow)}</strong></td>
            </tr>
          `;
        }).join('');
      }

      // Purchase Orders Table
      const poTableBody = document.getElementById("pur-orders-table-body");
      if (poTableBody) {
        poTableBody.innerHTML = (data.ordersTable || []).map(po => `
          <tr style="cursor:pointer;" onclick="KhangNghiApp.openPODetailDrawer('${po.id}')" title="Nhấp để xem chi tiết đơn mua ${po.id}">
            <td><strong style="color:var(--color-primary);">${po.id}</strong></td>
            <td>${po.date}</td>
            <td>${po.supplierName}</td>
            <td><strong>${po.productName}</strong></td>
            <td>${po.manufacturer}</td>
            <td>${po.qty.toLocaleString()}</td>
            <td>${Comp.formatCurrency(po.unitCost)}</td>
            <td><strong>${Comp.formatCurrency(po.totalCost)}</strong></td>
          </tr>
        `).join('');
      }

      // Key Insights & Alerts
      const insContainer = document.getElementById("pur-insights-container");
      if (insContainer) {
        insContainer.innerHTML = (data.insights || []).map(i => `<p style="margin-bottom:6px;">${i}</p>`).join('');
      }

      const alertContainer = document.getElementById("pur-alerts-container");
      if (alertContainer) {
        alertContainer.innerHTML = (data.alerts || []).map(a => `
          <div class="badge badge-${a.type === 'danger' ? 'danger' : a.type === 'warning' ? 'warning' : 'info'}" style="display:block; padding:8px 10px; border-radius:6px; font-size:11.5px;">
            <strong>${a.title}:</strong> ${a.desc}
          </div>
        `).join('');
      }

      // Context Cards: Purchase vs Sales & Purchase & Inventory
      const vsSalesBody = document.getElementById("pur-vs-sales-body");
      if (vsSalesBody) {
        const vs = data.vsSales || {};
        vsSalesBody.innerHTML = `
          <div><span style="color:var(--text-muted);">Tổng Chi Phí Mua:</span><br><strong style="font-size:14px; color:var(--color-primary);">${Comp.formatCurrency(vs.purchaseCost)}</strong></div>
          <div><span style="color:var(--text-muted);">Tổng Doanh Thu Bán:</span><br><strong style="font-size:14px; color:var(--color-success);">${Comp.formatCurrency(vs.salesRevenue)}</strong></div>
          <div><span style="color:var(--text-muted);">Tỷ Lệ Cost / Revenue:</span><br><strong>${vs.costToRevRatio}%</strong></div>
          <div><span style="color:var(--text-muted);">Lợi Nhuận Gộp Context:</span><br><strong>${Comp.formatCurrency(vs.grossProfit)}</strong></div>
        `;
      }

      const vsInvBody = document.getElementById("pur-vs-inv-body");
      if (vsInvBody) {
        const vi = data.vsInventory || {};
        vsInvBody.innerHTML = `
          <div><span style="color:var(--text-muted);">Số Lượng Vừa Nhập:</span><br><strong style="font-size:14px;">${(vi.recentlyPurchasedQty || 0).toLocaleString()} sp</strong></div>
          <div><span style="color:var(--text-muted);">Tồn Kho Nguy Cấp:</span><br><strong style="font-size:14px; color:var(--color-danger);">${vi.criticalItemsCount} mặt hàng</strong></div>
          <div><span style="color:var(--text-muted);">Tổng Giá Trị Tồn Kho:</span><br><strong>${Comp.formatCurrency(vi.totalStockVal)}</strong></div>
          <div><span style="color:var(--text-muted);">Cảnh Báo Dưới Safety Stock:</span><br><strong style="color:var(--color-warning);">${vi.lowStockCount} mặt hàng</strong></div>
        `;
      }
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 04. INVENTORY CONTROL RENDERER (ROUND 1)
  // --------------------------------------------------------------------------
  async function renderInventoryView(filterState) {
    const data = await Svc.getInventoryFullOverview(filterState);
    
    // KPI Cards
    const kpiEl = document.getElementById("inventory-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "inv-val", label: "TOTAL INVENTORY VALUE", value: data.inventoryValue, changePct: -2.4, icon: "🏭", tooltip: "Tổng giá trị tiền hàng tồn kho dựa trên đơn giá vốn hiện tại." })}
        ${Comp.renderKpiCard({ id: "inv-hand", label: "STOCK ON HAND", value: data.stockOnHand, changePct: 1.2, icon: "📦", isCurrency: false, tooltip: "Tổng số lượng sản phẩm đang có sẵn trong kho." })}
        ${Comp.renderKpiCard({ id: "inv-low", label: "LOW STOCK ITEMS", value: data.lowStockItems, changePct: 0, icon: "⚠️", isCurrency: false, tooltip: "Số lượng mặt hàng có tồn kho dưới Safety Stock." })}
        ${Comp.renderKpiCard({ id: "inv-out", label: "OUT OF STOCK", value: data.outOfStockItems, changePct: 0, icon: "🚨", isCurrency: false, tooltip: "Số lượng mặt hàng đã hoàn toàn đứt hàng (Stock = 0)." })}
        ${Comp.renderKpiCard({ id: "inv-over", label: "OVERSTOCK ITEMS", value: data.overstockItems, changePct: -4.5, icon: "📦", isCurrency: false, tooltip: "Mặt hàng tồn vượt quá 3 lần mức Safety Stock." })}
        ${Comp.renderKpiCard({ id: "inv-cov", label: "STOCK COVERAGE", value: data.stockCoverageMonths, changePct: 0.5, icon: "⏱️", isCurrency: false, unit: " tháng", tooltip: "Số tháng bán hàng dự kiến bao phủ bởi lượng tồn kho hiện tại." })}
      `;
    }

    // Health visual bar
    const hBarHealthy = document.getElementById("inv-health-bar-healthy");
    const hBarWarning = document.getElementById("inv-health-bar-warning");
    const hBarCritical = document.getElementById("inv-health-bar-critical");
    const hRatioText = document.getElementById("inv-health-ratio-text");

    if (hBarHealthy) hBarHealthy.style.width = data.health.healthyPct + "%";
    if (hBarWarning) hBarWarning.style.width = data.health.warningPct + "%";
    if (hBarCritical) hBarCritical.style.width = data.health.criticalPct + "%";
    if (hRatioText) hRatioText.textContent = `Healthy: ${data.health.healthyPct}% | Warning: ${data.health.warningPct}% | Critical: ${data.health.criticalPct}%`;

    // Charts
    setTimeout(() => {
      Charts.renderSupplierBarChart("chart-inv-warehouse", (data.warehouses || []).map(w => ({ name: w.name, cost: w.stockValue })), { metric: 'cost' });
      Charts.renderStockMovementChart("chart-inv-movement", data.movements);
    }, 50);

    // Table
    const body = document.getElementById("inventory-table-body");
    if (body) {
      body.innerHTML = (data.products || []).map(item => `
        <tr style="cursor:pointer;" onclick="KhangNghiApp.openDetailDrawer('${item.id}')">
          <td><strong style="color:var(--color-primary);">${item.id}</strong></td>
          <td><strong>${item.name}</strong></td>
          <td>${item.category}</td>
          <td>${item.warehouse || 'Kho Miền Bắc (Hà Nội)'}</td>
          <td><strong style="${item.stock <= item.safetyStock ? 'color:var(--color-danger); font-weight:800;' : ''}">${item.stock}</strong></td>
          <td>${item.safetyStock}</td>
          <td>${Comp.formatCurrency(item.stock * item.price)}</td>
          <td>${Comp.renderStatusBadge(item.status)}</td>
          <td><button class="topbar-action-btn" style="width:auto; padding:0 8px; font-size:11px;" onclick="event.stopPropagation(); KhangNghiApp.openDetailDrawer('${item.id}')">Chi Tiết</button></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 05. SHIPPING & SLA RENDERER (ROUND 2)
  // --------------------------------------------------------------------------
  async function renderShippingView(filterState) {
    const data = await Svc.getShippingFullOverview(filterState);
    
    // KPI Cards
    const kpiEl = document.getElementById("shipping-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "shp-count", label: "TOTAL SHIPMENTS", value: data.totalShipments, changePct: 8.4, icon: "🚚", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "shp-cost", label: "SHIPPING COST", value: data.shippingCost, changePct: 2.3, icon: "⛽" })}
        ${Comp.renderKpiCard({ id: "shp-days", label: "AVG DELIVERY DAYS", value: data.avgDeliveryDays, changePct: -4.2, icon: "⏱️", isCurrency: false, unit: " ngày" })}
        ${Comp.renderKpiCard({ id: "shp-ontime", label: "ON-TIME RATE", value: data.onTimeRate, changePct: 1.5, icon: "✅", isCurrency: false, unit: "%" })}
        ${Comp.renderKpiCard({ id: "shp-late", label: "LATE SHIPMENTS", value: data.lateShipments, changePct: -12.5, icon: "🛑", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "shp-avgcost", label: "AVG SHIPPING COST", value: data.avgShippingCost, changePct: -1.2, icon: "🏷️", unit: "/đơn" })}
      `;
    }

    setTimeout(() => {
      Charts.renderRevenueTrend("chart-shp-trend", (data.regionPerformance || []).map(r => ({ label: r.region, cost: r.cost, qty: r.shipments })), { metric: 'cost' });
      Charts.renderSlaDonut("chart-shp-donut", data.slaMonitor);
    }, 50);

    // Shipper Performance Table
    const shpBody = document.getElementById("shipping-shippers-table-body");
    if (shpBody) {
      shpBody.innerHTML = (data.shippers || []).map(s => `
        <tr>
          <td><strong>${s.name}</strong></td>
          <td>${s.avgDays} ngày</td>
          <td><strong style="color:${s.lateRate > 0.05 ? 'var(--color-danger)' : 'var(--color-success)'}">${(s.lateRate * 100).toFixed(1)}%</strong></td>
          <td>${Comp.renderStatusBadge(s.status === 'Healthy' ? 'HEALTHY' : 'WARNING')}</td>
        </tr>
      `).join('');
    }

    // Late Deliveries Table
    const lateBody = document.getElementById("shipping-late-table-body");
    if (lateBody) {
      lateBody.innerHTML = (data.lateDeliveries || []).map(l => `
        <tr>
          <td><strong style="color:var(--color-primary);">${l.orderId}</strong></td>
          <td>${l.shipper}</td>
          <td>${l.expectedDelivery}</td>
          <td>${l.actualDelivery}</td>
          <td><span class="badge badge-danger">+${l.delayDays} ngày</span></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 06. CUSTOMER VIEW RENDERER (ROUND 3)
  // --------------------------------------------------------------------------
  async function renderCustomerView(filterState) {
    const data = await Svc.getCustomerFullOverview(filterState);
    
    // KPI Cards
    const kpiEl = document.getElementById("customer-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "cus-total", label: "TỔNG SỐ KHÁCH HÀNG", value: 17442, changePct: 6.4, icon: "👥", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "cus-active", label: "KHÁCH HÀNG HOẠT ĐỘNG", value: 17442, changePct: 8.2, icon: "👤", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "cus-rev-pc", label: "DOANH THU TB/KHÁCH HÀNG", value: 922, changePct: 12.4, icon: "💰" })}
        ${Comp.renderKpiCard({ id: "cus-ord-pc", label: "ĐƠN HÀNG TB/KHÁCH HÀNG", value: 3.8, changePct: 2.1, icon: "🛒", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "cus-aov", label: "GIÁ TRỊ ĐƠN HÀNG TB (AOV)", value: 246, changePct: 4.5, icon: "🏷️" })}
        ${Comp.renderKpiCard({ id: "cus-repeat", label: "TỶ LỆ KHÁCH HÀNG QUAY LẠI", value: 68.4, changePct: 3.2, icon: "🔄", isCurrency: false, unit: "%" })}
      `;
    }

    setTimeout(() => {
      if (data.segments && Array.isArray(data.segments)) {
        Charts.renderCustomerSegmentDonut("chart-cus-seg", data.segments.map(s => ({ segment: s.segment || s.Segment, revenue: s.revenue || s.Revenue || 0 })));
      }
    }, 50);

    // Customer Table with mapped DTO properties
    const body = document.getElementById("customer-table-body");
    if (body) {
      body.innerHTML = (data.topCustomers || []).map(c => {
        const id = c.customerKey || c.CustomerKey || c.customerID || c.id || "KH-N/A";
        const name = c.customerName || c.CustomerName || c.name || "Khách hàng DWH";
        const segment = c.segment || c.Segment || "Consumer";
        const orders = c.orderCount || c.OrderCount || c.orders || 1;
        const revenue = c.revenue || c.Revenue || c.totalSpent || 0;
        const profit = c.profit || c.Profit || Math.round(revenue * 0.12);
        const margin = revenue > 0 ? ((profit / revenue) * 100).toFixed(1) : "12.0";
        return `
        <tr style="cursor:pointer;" onclick="KhangNghiComponents.showToast('Chi tiết Khách hàng ${name}')">
          <td><strong style="color:var(--color-primary);">${id}</strong></td>
          <td><strong>${name}</strong></td>
          <td><span class="badge badge-info">${segment}</span></td>
          <td>Toàn cầu (DWH)</td>
          <td>${orders.toLocaleString()}</td>
          <td>${Comp.formatCurrency(revenue)}</td>
          <td><strong style="color:var(--color-success);">${Comp.formatCurrency(profit)}</strong></td>
          <td>${margin}%</td>
        </tr>
        `;
      }).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 07. PRODUCT MATRIX RENDERER (ROUND 4)
  // --------------------------------------------------------------------------
  async function renderProductView(filterState) {
    const data = await Svc.getProductFullOverview(filterState);
    
    // KPI Cards
    const kpiEl = document.getElementById("product-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "prd-total", label: "TỔNG SỐ SẢN PHẨM", value: 3788, changePct: 2.1, icon: "📦", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "prd-active", label: "SẢN PHẨM CÓ DOANH THU", value: 3788, changePct: 1.8, icon: "✅", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "prd-rev", label: "TỔNG DOANH THU SẢN PHẨM", value: 16081034, changePct: 14.2, icon: "💰" })}
        ${Comp.renderKpiCard({ id: "prd-prof", label: "TỔNG LỢI NHUẬN SẢN PHẨM", value: 1914938, changePct: 18.5, icon: "💎" })}
        ${Comp.renderKpiCard({ id: "prd-margin", label: "TỶ SUẤT LỢI NHUẬN TB", value: 11.9, changePct: 2.1, icon: "📊", isCurrency: false, unit: "%" })}
        ${Comp.renderKpiCard({ id: "prd-top", label: "SẢN PHẨM TOP 1", value: "Canon ImageCLASS", changePct: 0, icon: "🏆", isCurrency: false })}
      `;
    }

    setTimeout(() => {
      if (data.categories && Array.isArray(data.categories)) {
        Charts.renderSalesByCategory("chart-prd-cat", data.categories.map(c => ({ category: c.category || c.Category, revenue: c.revenue || c.Revenue || 0 })));
      }
    }, 50);

    const body = document.getElementById("product-table-body");
    if (body) {
      const list = data.topRevenueProducts || data.products || [];
      body.innerHTML = list.map(p => {
        const id = p.productKey || p.ProductKey || p.productID || p.id || "SP-N/A";
        const name = p.productName || p.ProductName || p.name || "Sản phẩm DWH";
        const category = p.category || p.Category || "Chung";
        const subCat = p.subCategory || p.SubCategory || "Tổng hợp";
        const revenue = p.revenue || p.Revenue || p.price || 0;
        const qty = p.quantity || p.Quantity || p.qty || 1;
        const profit = p.profit || p.Profit || Math.round(revenue * 0.12);
        const margin = revenue > 0 ? ((profit / revenue) * 100).toFixed(1) : "12.0";
        return `
        <tr style="cursor:pointer;" onclick="KhangNghiComponents.showToast('Sản phẩm ${name}')">
          <td><strong style="color:var(--color-primary);">${id}</strong></td>
          <td><strong>${name}</strong></td>
          <td>${category}</td>
          <td>${subCat}</td>
          <td>${Comp.formatCurrency(revenue)}</td>
          <td><strong style="color:var(--color-success);">${margin}%</strong></td>
          <td>${qty.toLocaleString()} sp</td>
          <td><span class="badge badge-healthy">DWH Active</span></td>
        </tr>
        `;
      }).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 08. SUPPLIER PERFORMANCE RENDERER (ROUND 5)
  // --------------------------------------------------------------------------
  async function renderSupplierView(filterState) {
    const data = await Svc.getSupplierFullOverview(filterState);
    
    // KPI Cards
    const kpiEl = document.getElementById("supplier-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "sup-total", label: "ACTIVE SUPPLIERS", value: data.totalSuppliers, changePct: 0, icon: "🤝", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "sup-cost", label: "TOTAL PURCHASE COST", value: data.purchaseCost, changePct: 20.5, icon: "🛒" })}
        ${Comp.renderKpiCard({ id: "sup-qty", label: "PURCHASED QUANTITY", value: data.purchasedQty, changePct: 18.3, icon: "📦", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "sup-avg-unit", label: "AVG UNIT COST", value: data.avgUnitCost, changePct: 1.8, icon: "🏷️", unit: "/sp" })}
        ${Comp.renderKpiCard({ id: "sup-top", label: "TOP SUPPLIER", value: data.topSupplier, changePct: 0, icon: "🏆", isCurrency: false })}
      `;
    }

    setTimeout(() => {
      Charts.renderSupplierBarChart("chart-sup-rank", data.spend || []);
      Charts.renderSupplierDonutChart("chart-sup-donut", data.spend || []);
    }, 50);

    const body = document.getElementById("supplier-table-body");
    if (body) {
      body.innerHTML = (data.spend || []).map(s => `
        <tr style="cursor:pointer;" onclick="KhangNghiComponents.showToast('NCC ${s.name}: Chi phí nhập $${Comp.formatCurrency(s.cost)}')">
          <td><strong style="color:var(--color-primary);">${s.id}</strong></td>
          <td><strong>${s.name}</strong></td>
          <td>${s.country}</td>
          <td>4 ngày</td>
          <td>⭐ 4.8</td>
          <td><strong>${Comp.formatCurrency(s.cost)}</strong></td>
          <td><strong style="color:var(--color-primary);">${s.sharePct}%</strong></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 09. GEOGRAPHY RENDERER
  // --------------------------------------------------------------------------
  async function renderGeographyView(filterState) {
    const data = await Svc.getExecutiveOverview(filterState);
    setTimeout(() => {
      Charts.renderRegionHorizontalBar("chart-geo-bar", data.regionBreakdown);
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 10. CROSS ANALYSIS RENDERER (ROUND 6)
  // --------------------------------------------------------------------------
  async function renderCrossAnalysisView() {
    const mxEl = document.getElementById("cross-metric-x");
    const myEl = document.getElementById("cross-metric-y");
    const gbEl = document.getElementById("cross-group-by");

    const mx = mxEl ? mxEl.value : "revenue";
    const my = myEl ? myEl.value : "inventoryVal";
    const gb = gbEl ? gbEl.value : "category";

    const data = await Svc.getCrossAnalysisOverview(mx, my, gb);
    
    const insEl = document.getElementById("cross-insight-text");
    if (insEl) insEl.innerHTML = `💡 <strong>Correlation Summary:</strong> ${data.relationshipSummary}`;

    setTimeout(() => {
      Charts.renderCrossMatrix("chart-cross-matrix", [
        { x: 2450000, y: 620000, z: 40 },
        { x: 1820000, y: 450000, z: 30 },
        { x: 890000, y: 210000, z: 20 }
      ]);
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 11. AI SALES FORECAST RENDERER (ROUND 7)
  // --------------------------------------------------------------------------
  async function renderForecastView() {
    const data = await Svc.getForecastOverview("6", "revenue");
    setTimeout(() => {
      Charts.renderForecastChart("chart-forecast-main", data.series);
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 12. WHAT-IF SIMULATOR RENDERER (ROUND 8)
  // --------------------------------------------------------------------------
  async function renderWhatIfView() {
    setTimeout(() => {
      initWhatIfSliders();
      const s = document.getElementById("slider-discount");
      if (s) s.dispatchEvent(new Event('input'));
    }, 50);
  }

  function saveWhatIfScenario() {
    const g = document.getElementById("slider-growth") ? document.getElementById("slider-growth").value : "0";
    const d = document.getElementById("slider-discount") ? document.getElementById("slider-discount").value : "0";
    const s = document.getElementById("slider-ship") ? document.getElementById("slider-ship").value : "0";
    
    const name = prompt("Nhập tên kịch bản mô phỏng:", `Scenario Growth ${g}%`);
    if (!name) return;

    const saved = JSON.parse(localStorage.getItem("kn_whatif_scenarios") || "[]");
    saved.push({
      name,
      date: new Date().toLocaleDateString('vi-VN'),
      params: `Growth: ${g}%, Discount: ${d}%, Ship: ${s}%`,
      projected: document.getElementById("whatif-proj-prof") ? document.getElementById("whatif-proj-prof").textContent : "$588.0K"
    });

    localStorage.setItem("kn_whatif_scenarios", JSON.stringify(saved));
    Comp.showToast(`✅ Đã lưu kịch bản mô phỏng "${name}" vào LocalStorage!`);
    loadSavedWhatIfScenarios();
  }

  function loadSavedWhatIfScenarios() {
    const saved = JSON.parse(localStorage.getItem("kn_whatif_scenarios") || "[]");
    const body = document.getElementById("whatif-saved-table-body");
    if (body) {
      if (!saved.length) {
        body.innerHTML = `<tr><td colspan="4" style="text-align:center; color:var(--text-muted);">Chưa có kịch bản mô phỏng nào được lưu.</td></tr>`;
      } else {
        body.innerHTML = saved.map(item => `
          <tr>
            <td><strong>${item.name}</strong></td>
            <td>${item.date}</td>
            <td>${item.params}</td>
            <td><strong style="color:var(--color-success);">${item.projected}</strong></td>
          </tr>
        `).join('');
      }
    }
  }

  function initWhatIfSliders() {
    const dSlider = document.getElementById("slider-discount");
    const sSlider = document.getElementById("slider-ship");
    const gSlider = document.getElementById("slider-growth");

    const vGrowth = document.getElementById("val-growth");
    const vDiscount = document.getElementById("val-discount");
    const vShip = document.getElementById("val-ship");

    const projEl = document.getElementById("whatif-proj-prof");
    const diffEl = document.getElementById("whatif-diff");

    const baseProfit = 588000;

    function updateSim() {
      if (!dSlider || !sSlider || !gSlider) return;
      const d = parseFloat(dSlider.value);
      const s = parseFloat(sSlider.value);
      const g = parseFloat(gSlider.value);

      if (vGrowth) vGrowth.textContent = `${g}%`;
      if (vDiscount) vDiscount.textContent = `${d}%`;
      if (vShip) vShip.textContent = `${s}%`;

      const simProfit = baseProfit * (1 + g / 100) * (1 - d / 50) * (1 - s / 100);
      const diff = simProfit - baseProfit;

      if (projEl) projEl.textContent = Comp.formatCurrency(simProfit);
      if (diffEl) {
        diffEl.textContent = (diff >= 0 ? "+" : "") + Comp.formatCurrency(diff);
        diffEl.style.color = diff >= 0 ? "var(--color-success)" : "var(--color-danger)";
      }

      Charts.renderWhatIfComparison("chart-whatif-compare", baseProfit, simProfit);
    }

    [dSlider, sSlider, gSlider].forEach(slider => {
      if (slider) slider.addEventListener("input", updateSim);
    });

    loadSavedWhatIfScenarios();
  }

  // --------------------------------------------------------------------------
  // 13. BI QUESTION EXPLORER CATALOG (ROUND 9)
  // --------------------------------------------------------------------------
  async function renderQuestionExplorerView() {
    const catalog = [
      { id: "SAL-001", domain: "Sales", question: "Doanh thu & Lợi nhuận theo Quý/Năm?", measures: "Revenue, Profit", dim: "Time", status: "Implemented", targetRoute: "sales" },
      { id: "SAL-002", domain: "Sales", question: "Top 10 sản phẩm có lợi nhuận cao nhất?", measures: "Profit, Margin", dim: "Product", status: "Implemented", targetRoute: "sales" },
      { id: "PUR-001", domain: "Purchase", question: "Chi phí nhập hàng theo Nhà cung cấp?", measures: "PurchaseCost", dim: "Supplier", status: "Implemented", targetRoute: "purchase" },
      { id: "INV-001", domain: "Inventory", question: "Sản phẩm nào đang dưới mức tồn kho an toàn?", measures: "StockQty", dim: "Product, Warehouse", status: "Implemented", targetRoute: "inventory" },
      { id: "SHP-001", domain: "Shipping", question: "Tỷ lệ đơn hàng giao trễ SLA theo đơn vị vận chuyển?", measures: "LateCount", dim: "Shipper", status: "Implemented", targetRoute: "shipping" },
      { id: "CUS-001", domain: "Customer", question: "Doanh thu và lợi nhuận theo Phân khúc khách hàng?", measures: "Revenue, Profit", dim: "Customer.Segment", status: "Implemented", targetRoute: "customer" },
      { id: "PRD-001", domain: "Product", question: "Ma trận sinh lời sản phẩm Margin vs Revenue?", measures: "Margin, Revenue", dim: "Product", status: "Implemented", targetRoute: "product" },
      { id: "CROSS-001", domain: "Cross Analysis", question: "Sản phẩm bán chạy nhưng lượng tồn kho thấp?", measures: "SalesQty, StockQty", dim: "Product", status: "Implemented", targetRoute: "cross-analysis" },
      { id: "ML-001", domain: "Forecast", question: "Dự báo doanh số 3 - 12 tháng tới (ML.NET SSA)?", measures: "ForecastSales", dim: "Time", status: "Implemented", targetRoute: "forecast" }
    ];

    const auth = window.KhangNghiAuth;
    const user = auth ? auth.getCurrentUser() : null;

    let filteredCatalog = catalog;
    if (user && user.role !== "CEO" && user.role !== "Admin") {
      if (user.role === "Sales") filteredCatalog = catalog.filter(q => q.id.startsWith("SAL") || q.id.startsWith("ML") || q.id.startsWith("CROSS") || q.id.startsWith("CUS"));
      else if (user.role === "Purchase") filteredCatalog = catalog.filter(q => q.id.startsWith("PUR"));
      else if (user.role === "Warehouse") filteredCatalog = catalog.filter(q => q.id.startsWith("INV"));
      else if (user.role === "Logistics") filteredCatalog = catalog.filter(q => q.id.startsWith("SHP"));
    }

    const body = document.getElementById("explorer-table-body");
    if (body) {
      body.innerHTML = filteredCatalog.map(q => `
        <tr>
          <td><strong style="color:var(--color-primary);">${q.id}</strong></td>
          <td><span class="badge badge-info">${q.domain}</span></td>
          <td><strong>${q.question}</strong></td>
          <td>${q.measures}</td>
          <td>${q.dim}</td>
          <td><span class="badge badge-healthy">${q.status}</span></td>
          <td><button class="topbar-action-btn" style="width:auto; padding:0 8px; font-size:11px; background:var(--color-primary); color:white; border:none;" onclick="window.location.hash='#/${q.targetRoute}'">🚀 Open Analysis</button></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 14. DATA STATUS & ETL LOG (ROUND 11)
  // --------------------------------------------------------------------------
  async function renderSystemStatusView() {
    const data = await Svc.getSystemStatusOverview();
    const body = document.getElementById("system-etl-table-body");
    if (body) {
      body.innerHTML = (data.tablesStatus || []).map(t => `
        <tr>
          <td><strong style="color:var(--color-primary);">${t.name}</strong></td>
          <td><span class="badge ${t.type === 'FACT' ? 'badge-info' : 'badge-secondary'}" style="padding:2px 8px; font-size:10px;">${t.type}</span></td>
          <td><strong>${t.rows.toLocaleString()}</strong> records</td>
          <td>${t.description}</td>
          <td><span class="badge ${t.status === 'AVAILABLE' ? 'badge-healthy' : 'badge-warning'}" style="padding:2px 8px; font-size:10px; background:${t.status === 'AVAILABLE' ? 'rgba(37,162,68,0.15)' : 'rgba(243,156,18,0.15)'}; color:${t.status === 'AVAILABLE' ? '#25a244' : '#f39c12'};">${t.status}</span></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 15. SETTINGS & PROFILE (ROUND 11)
  // --------------------------------------------------------------------------
  async function renderSettingsView() {
    if (Router) Router.updateNavigationUI();
  }

  // Helper functions for refresh & search handlers
  function refreshInventoryData() {
    Comp.showToast("🔄 Đang làm mới dữ liệu Inventory Control Center...");
    renderCurrentView();
  }

  function exportInventoryCSV() {
    Svc.exportToCSV("Inventory_Report_2015.csv", window.KhangNghiMockData.products || []);
    Comp.showToast("📥 Đã xuất file Inventory_Report_2015.csv thành công!");
  }

  function onInventorySearchInput(val) {
    Filters.setFilter("search", val);
  }

  function updateCrossAnalysis() {
    renderCrossAnalysisView();
  }

  function filterQuestionCatalog(val) {
    const q = val.toLowerCase();
    document.querySelectorAll("#explorer-table-body tr").forEach(row => {
      row.style.display = row.textContent.toLowerCase().includes(q) ? "" : "none";
    });
  }

  function filterQuestionCatalogByDomain(domain) {
    document.querySelectorAll("#explorer-table-body tr").forEach(row => {
      if (domain === "ALL") {
        row.style.display = "";
      } else {
        row.style.display = row.textContent.includes(domain) ? "" : "none";
      }
    });
  }

  // --------------------------------------------------------------------------
  // APP SHELL INTERACTION CONTROLLERS
  // --------------------------------------------------------------------------
  function initSidebarToggle() {
    const toggleBtn = document.getElementById("sidebar-toggle");
    if (toggleBtn) {
      toggleBtn.addEventListener("click", () => {
        document.body.classList.toggle("sidebar-collapsed");
        renderCurrentView();
      });
    }
  }

  function initThemeToggle() {
    const themeBtn = document.getElementById("btn-theme-toggle");
    if (themeBtn) {
      themeBtn.addEventListener("click", () => {
        const currentTheme = document.body.getAttribute("data-theme") || "light";
        const newTheme = currentTheme === "light" ? "dark" : "light";
        document.body.setAttribute("data-theme", newTheme);
        themeBtn.innerHTML = newTheme === "dark" ? "☀️" : "🌙";
        renderCurrentView();
      });
    }
  }

  function initDropdownMenus() {
    const notifBtn = document.getElementById("btn-notif");
    const notifMenu = document.getElementById("notif-dropdown");
    const profileBtn = document.getElementById("btn-profile");
    const profileMenu = document.getElementById("profile-dropdown");

    if (notifBtn && notifMenu) {
      notifBtn.addEventListener("click", (e) => {
        e.stopPropagation();
        notifMenu.classList.toggle("active");
        if (profileMenu) profileMenu.classList.remove("active");
      });
    }

    if (profileBtn && profileMenu) {
      profileBtn.addEventListener("click", (e) => {
        e.stopPropagation();
        profileMenu.classList.toggle("active");
        if (notifMenu) notifMenu.classList.remove("active");
      });
    }

    document.addEventListener("click", () => {
      if (notifMenu) notifMenu.classList.remove("active");
      if (profileMenu) profileMenu.classList.remove("active");
    });
  }

  // --------------------------------------------------------------------------
  // AUTHENTICATION & LOGIN HANDLERS
  // --------------------------------------------------------------------------
  async function handleLogin() {
    const emailInput = document.getElementById("login-email");
    const passwordInput = document.getElementById("login-password");
    const rememberInput = document.getElementById("login-remember");
    const alertBox = document.getElementById("login-alert-box");
    const alertMsg = document.getElementById("login-alert-msg");
    const submitBtn = document.getElementById("login-submit-btn");
    const btnText = document.getElementById("login-btn-text");

    if (!emailInput || !passwordInput) return;

    const email = emailInput.value.trim();
    const password = passwordInput.value;
    const remember = rememberInput ? rememberInput.checked : false;

    // Loading state
    if (submitBtn) submitBtn.disabled = true;
    if (btnText) btnText.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Đang xác thực...`;
    if (alertBox) alertBox.style.display = "none";

    const res = await window.KhangNghiAuth.login(email, password, remember);

    if (submitBtn) submitBtn.disabled = false;
    if (btnText) btnText.textContent = "Đăng Nhập";

    if (res.success) {
      Comp.showToast(`Chào mừng ${res.user.fullName} (${res.user.roleTitle}) đăng nhập hệ thống!`);
      window.location.hash = "#/overview";
    } else {
      if (alertBox && alertMsg) {
        alertMsg.textContent = res.message;
        alertBox.style.display = "flex";
      }
    }
  }

  function fillDemoAccount(email) {
    const emailInput = document.getElementById("login-email");
    const passwordInput = document.getElementById("login-password");
    if (emailInput) emailInput.value = email;
    if (passwordInput) passwordInput.value = "123456";
    handleLogin();
  }

  function togglePasswordVisibility() {
    const input = document.getElementById("login-password");
    const icon = document.getElementById("password-toggle-icon");
    if (!input || !icon) return;

    if (input.type === "password") {
      input.type = "text";
      icon.className = "fa-solid fa-eye-slash";
    } else {
      input.type = "password";
      icon.className = "fa-solid fa-eye";
    }
  }

  function switchDemoRole(roleName) {
    if (window.KhangNghiAuth && window.KhangNghiAuth.switchDemoRole(roleName)) {
      const user = window.KhangNghiAuth.getCurrentUser();
      Comp.showToast(`🎭 Switched Demo Role: ${user.roleTitle}`);
      if (Router) Router.updateNavigationUI();
      renderCurrentView();
    }
  }

  // --------------------------------------------------------------------------
  // COPILOT ASSISTANT (ROLE-AWARE & PERMISSION-CHECKED)
  // --------------------------------------------------------------------------
  function askCopilot(text) {
    if (!text) return;
    const overlay = document.getElementById("copilot-modal");
    const chatBody = document.getElementById("copilot-chat-body");
    const input = document.getElementById("copilot-input");

    if (overlay) overlay.classList.add("active");
    if (!chatBody) return;

    const auth = window.KhangNghiAuth;
    const user = auth ? auth.getCurrentUser() : null;

    chatBody.innerHTML += `<div style="text-align:right; margin-bottom:10px;"><span style="background:var(--color-primary); color:white; padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px;">${text}</span></div>`;
    if (input) input.value = "";
    chatBody.scrollTop = chatBody.scrollHeight;

    setTimeout(() => {
      let reply = `🤖 <strong>Khang Nghi AI Copilot:</strong> `;

      // Role permission check simulation
      if (user) {
        if (user.role === "Sales" && (text.includes("nhập hàng") || text.includes("nhà cung cấp") || text.includes("Purchase"))) {
          reply += `⚠️ <strong>Access Restricted:</strong> Tài khoản của bạn thuộc vai trò <strong>${user.roleTitle}</strong>. Bạn không có quyền truy cập nhóm dữ liệu <em>Purchase / Nhà cung cấp</em>.`;
          chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--color-warning-bg); border:1px solid var(--color-warning); color:var(--text-main); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">${reply}</span></div>`;
          chatBody.scrollTop = chatBody.scrollHeight;
          return;
        }
        if (user.role === "Purchase" && (text.includes("khách hàng") || text.includes("Customer") || text.includes("Dự báo"))) {
          reply += `⚠️ <strong>Access Restricted:</strong> Tài khoản của bạn thuộc vai trò <strong>${user.roleTitle}</strong>. Bạn không có quyền truy cập nhóm dữ liệu <em>Sales & Analytics Khách hàng</em>.`;
          chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--color-warning-bg); border:1px solid var(--color-warning); color:var(--text-main); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">${reply}</span></div>`;
          chatBody.scrollTop = chatBody.scrollHeight;
          return;
        }
        if (user.role === "Warehouse" && (text.includes("SLA") || text.includes("trễ") || text.includes("giao hàng"))) {
          reply += `⚠️ <strong>Access Restricted:</strong> Tài khoản của bạn thuộc vai trò <strong>${user.roleTitle}</strong>. Bạn không có quyền truy cập nhóm dữ liệu <em>Logistics & Shipping SLA</em>.`;
          chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--color-warning-bg); border:1px solid var(--color-warning); color:var(--text-main); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">${reply}</span></div>`;
          chatBody.scrollTop = chatBody.scrollHeight;
          return;
        }
      }

      if (text.includes("Doanh thu")) {
        reply += `Doanh thu năm 2015 đạt <strong>$5.50M</strong> (+22.8% YoY). Nhóm <em>Thiết bị Văn phòng</em> chiếm 48% tổng doanh số.`;
      } else if (text.includes("lợi nhuận") || text.includes("Top")) {
        reply += `Top 1: <em>Máy in Laser Canon LBP2900</em> ($980K, Margin 24.5%), Top 2: <em>Ghế Ergonomic X5</em> ($1.12M, Margin 29.0%).`;
      } else if (text.includes("đứt hàng") || text.includes("Tồn")) {
        reply += `🚨 <strong>Cảnh báo tồn kho:</strong> <em>Canon LBP2900</em> còn 4 sp (Safety: 25). ⚠️ <em>Ghế Ergonomic X5</em> còn 12 sp (Safety: 20).`;
      } else if (text.includes("SLA") || text.includes("giao trễ")) {
        reply += `Tỷ lệ giao hàng đúng hạn SLA đạt <strong>95.8%</strong>. <em>Vietnam Post</em> có tỷ lệ trễ cao nhất 9.0%.`;
      } else if (text.includes("Dự báo")) {
        reply += `Mô hình <strong>ML.NET SSA</strong> dự báo doanh thu Q1/2016 đạt <strong>$1.865M</strong> (+8.4% YoY) với độ tin cậy 95%.`;
      } else {
        reply += `Dữ liệu DWH Khang Nghị ghi nhận sự tăng trưởng ổn định qua các năm 2012-2015. Mời bạn tra cứu thêm tại BI Question Catalog.`;
      }

      chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--bg-input); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">${reply}</span></div>`;
      chatBody.scrollTop = chatBody.scrollHeight;
    }, 350);
  }

  function initCopilotModal() {
    const trigger = document.getElementById("copilot-trigger");
    const topbarBtn = document.getElementById("topbar-copilot-btn");
    const overlay = document.getElementById("copilot-modal");
    const closeBtn = document.getElementById("copilot-close");
    const sendBtn = document.getElementById("copilot-send");
    const input = document.getElementById("copilot-input");

    if (trigger && overlay) trigger.addEventListener("click", () => overlay.classList.add("active"));
    if (topbarBtn && overlay) topbarBtn.addEventListener("click", () => overlay.classList.add("active"));
    if (closeBtn && overlay) closeBtn.addEventListener("click", () => overlay.classList.remove("active"));

    if (sendBtn && input) {
      sendBtn.addEventListener("click", () => askCopilot(input.value.trim()));
      input.addEventListener("keypress", (e) => { if (e.key === "Enter") askCopilot(input.value.trim()); });
    }
  }

  function initDataLineageModal() {
    const badge = document.getElementById("lineage-badge");
    const overlay = document.getElementById("lineage-modal");
    const closeBtn = document.getElementById("lineage-close");
    if (badge && overlay) badge.addEventListener("click", () => overlay.classList.add("active"));
    if (closeBtn && overlay) closeBtn.addEventListener("click", () => overlay.classList.remove("active"));
  }

  function initDetailDrawer() {
    const drawer = document.getElementById("detail-drawer");
    const closeBtn = document.getElementById("detail-drawer-close");
    if (closeBtn && drawer) closeBtn.addEventListener("click", () => drawer.classList.remove("active"));
  }

  async function openDetailDrawer(productId) {
    const drawer = document.getElementById("detail-drawer");
    const body = document.getElementById("detail-drawer-body");
    if (!drawer || !body) return;

    const p = await Svc.getProductDetails(productId);
    body.innerHTML = `
      <div style="margin-bottom:16px;">
        <span class="badge badge-healthy">${p.id}</span>
        <h3 style="font-size:18px; font-weight:700; margin-top:6px;">${p.name}</h3>
        <span style="font-size:12px; color:var(--text-secondary);">${p.category} > ${p.subcategory}</span>
      </div>

      <div class="glass-card" style="margin-bottom:16px;">
        <div style="display:flex; justify-content:space-between; margin-bottom:8px;">
          <span>Đơn Giá:</span><strong>${Comp.formatCurrency(p.price)}</strong>
        </div>
        <div style="display:flex; justify-content:space-between; margin-bottom:8px;">
          <span>Margin Lợi Nhuận:</span><strong style="color:var(--color-success);">${p.margin}%</strong>
        </div>
        <div style="display:flex; justify-content:space-between;">
          <span>Tồn Kho Hiện Tại:</span><strong>${p.stock} (Safety: ${p.safetyStock})</strong>
        </div>
      </div>

      <button class="topbar-action-btn" style="width:100%; height:38px; background:var(--color-primary); color:white; font-weight:600;" onclick="KhangNghiComponents.showToast('Mở chi tiết phân tích sản phẩm ${p.id}')">Xem Phân Tích Đầy Đủ</button>
    `;
    drawer.classList.add("active");
  }

  // --------------------------------------------------------------------------
  // SALES ANALYTICS INTERACTION CONTROLLERS & SEARCH DISPATCHER
  // --------------------------------------------------------------------------
  function switchSalesMetric(metric, btnEl) {
    if (btnEl) {
      const container = btnEl.closest(".segmented-control");
      if (container) {
        container.querySelectorAll("button").forEach(b => {
          b.classList.remove("active");
          b.style.color = "var(--text-muted)";
        });
        btnEl.classList.add("active");
        btnEl.style.color = "var(--text-main)";
      }
    }
    Filters.setFilter("metric", metric);
  }

  function switchGranularity(granularity, btnEl) {
    if (btnEl) {
      const container = btnEl.closest(".segmented-control");
      if (container) {
        container.querySelectorAll("button").forEach(b => {
          b.classList.remove("active");
          b.style.color = "var(--text-muted)";
        });
        btnEl.classList.add("active");
        btnEl.style.color = "var(--text-main)";
      }
    }
    Filters.setFilter("granularity", granularity);
  }

  function switchRankingMode(mode, btnEl) {
    if (btnEl) {
      const container = btnEl.closest(".segmented-control");
      if (container) {
        container.querySelectorAll("button").forEach(b => {
          b.classList.remove("active");
          b.style.color = "var(--text-muted)";
        });
        btnEl.classList.add("active");
        btnEl.style.color = "var(--text-main)";
      }
    }
    const titleEl = document.getElementById("sales-ranking-title");
    if (titleEl) {
      titleEl.innerHTML = mode === "bottom10" 
        ? `<i class="fa-solid fa-arrow-down-short-wide" style="color:var(--color-danger);"></i> Bottom 10 Nhóm Doanh Thu Thấp Nhất`
        : `<i class="fa-solid fa-chart-pie" style="color:var(--color-accent);"></i> Cơ Cấu Doanh Thu (Top 10)`;
    }
    Filters.setFilter("rankingMode", mode);
  }

  function toggleCompareMode() {
    const currentState = Filters.getState();
    const nextCompare = !currentState.compareMode;
    const btn = document.getElementById("sales-compare-btn");

    if (btn) {
      if (nextCompare) {
        btn.classList.add("active");
        btn.style.background = "var(--color-primary-light)";
        btn.style.borderColor = "var(--color-primary)";
        btn.style.color = "var(--color-primary)";
      } else {
        btn.classList.remove("active");
        btn.style.background = "";
        btn.style.borderColor = "";
        btn.style.color = "";
      }
    }

    Comp.showToast(nextCompare ? "⚖️ Đã bật Chế độ So sánh (2015 vs 2014)" : "ℹ️ Đã tắt Chế độ So sánh");
    Filters.setFilter("compareMode", nextCompare);
  }

  function saveCurrentPreset() {
    const currentState = Filters.getState();
    const presetName = prompt("Nhập tên cho Filter Preset mới:", `Custom View (${new Date().toLocaleTimeString('vi-VN', {hour:'2-digit', minute:'2-digit'})})`);
    if (!presetName) return;

    Filters.saveCustomPreset(presetName, currentState);
    Comp.showToast(`✅ Đã lưu preset: "${presetName}"`);

    const presetSelect = document.getElementById("sales-preset-select");
    if (presetSelect) {
      const opt = document.createElement("option");
      opt.value = presetName;
      opt.textContent = `View: ${presetName}`;
      opt.selected = true;
      presetSelect.appendChild(opt);
    }
  }

  function toggleExportMenu() {
    const dropdown = document.getElementById("sales-export-dropdown");
    if (dropdown) {
      dropdown.style.display = dropdown.style.display === "block" ? "none" : "block";
    }
  }

  function exportSalesCSV() {
    const state = Filters.getState();
    const sampleData = [
      { OrderID: "SO-2015-001", Date: "2015-01-15", Customer: "Công ty An Phát", Category: "Thiết bị Văn phòng", Region: state.region || "North America", Revenue: 1450, Profit: 180 },
      { OrderID: "SO-2015-002", Date: "2015-01-18", Customer: "Tập đoàn Minh Khang", Category: "Nội thất Văn phòng", Region: state.region || "South East Asia", Revenue: 3200, Profit: 420 },
      { OrderID: "SO-2015-003", Date: "2015-02-04", Customer: "Công ty Hoàng Hà", Category: "Đồ dùng Văn phòng", Region: state.region || "Western Europe", Revenue: 890, Profit: 95 }
    ];
    Svc.exportToCSV("Sales_Analytics_Report_2015.csv", sampleData);
    Comp.showToast("📥 Đã xuất file Sales_Analytics_Report_2015.csv thành công!");
    toggleExportMenu();
  }

  function exportChartPNG() {
    Comp.showToast("🖼️ Biểu đồ Sales Analytics đã được xuất thành ảnh PNG!");
    toggleExportMenu();
  }

  function printSalesView() {
    toggleExportMenu();
    window.print();
  }

  function refreshSalesData() {
    const icon = document.getElementById("sales-refresh-icon");
    if (icon) icon.classList.add("fa-spin");
    Comp.showToast("🔄 Đang đồng bộ và làm mới dữ liệu Sales Analytics...");

    setTimeout(() => {
      if (icon) icon.classList.remove("fa-spin");
      renderCurrentView();
      Comp.showToast("✅ Đã cập nhật xong dữ liệu Sales Analytics mới nhất!");
    }, 400);
  }

  function showSearchSuggestions() {
    const inp = document.getElementById("sales-search-input");
    const container = document.getElementById("sales-search-suggestions");
    if (!inp || !container) return;

    const val = inp.value.trim();
    const suggestions = window.KhangNghiSearch ? window.KhangNghiSearch.getSuggestions(val) : [];

    if (!suggestions.length) {
      container.style.display = "none";
      return;
    }

    container.innerHTML = suggestions.map(item => `
      <div style="padding:8px 14px; cursor:pointer; font-size:12px; display:flex; align-items:center; justify-content:space-between; transition:background 0.15s; border-bottom:1px solid var(--border-color);" onmouseover="this.style.background='var(--bg-app)'" onmouseout="this.style.background='transparent'" onclick="KhangNghiApp.selectSearchSuggestion('${item.value}', '${item.type}')">
        <div>
          <span style="font-weight:600; color:var(--text-main);">${item.title}</span>
          <span style="font-size:11px; color:var(--text-muted); margin-left:6px;">(${item.subtitle})</span>
        </div>
        <span class="badge badge-info" style="font-size:10px;">${item.type}</span>
      </div>
    `).join("");

    container.style.display = "block";
  }

  function onSearchInput(val) {
    showSearchSuggestions();
    Filters.setFilter("search", val);
  }

  function selectSearchSuggestion(val, type) {
    const container = document.getElementById("sales-search-suggestions");
    if (container) container.style.display = "none";

    if (type === "category") {
      Filters.setFilter("category", val);
    } else if (type === "region") {
      Filters.setFilter("region", val);
    } else {
      Filters.setFilter("search", val);
    }
  }

  // --------------------------------------------------------------------------
  // PURCHASE ANALYTICS INTERACTION CONTROLLERS & PO DRAWER
  // --------------------------------------------------------------------------
  function switchPurchaseMetric(metric, btnEl) {
    if (btnEl) {
      const container = btnEl.closest(".segmented-control");
      if (container) {
        container.querySelectorAll("button").forEach(b => {
          b.classList.remove("active");
          b.style.color = "var(--text-muted)";
        });
        btnEl.classList.add("active");
        btnEl.style.color = "var(--text-main)";
      }
    }
    Filters.setFilter("metric", metric);
  }

  function switchPurchaseGranularity(granularity, btnEl) {
    if (btnEl) {
      const container = btnEl.closest(".segmented-control");
      if (container) {
        container.querySelectorAll("button").forEach(b => {
          b.classList.remove("active");
          b.style.color = "var(--text-muted)";
        });
        btnEl.classList.add("active");
        btnEl.style.color = "var(--text-main)";
      }
    }
    Filters.setFilter("granularity", granularity);
  }

  function switchSupplierRankingMode(mode, btnEl) {
    if (btnEl) {
      const container = btnEl.closest(".segmented-control");
      if (container) {
        container.querySelectorAll("button").forEach(b => {
          b.classList.remove("active");
          b.style.color = "var(--text-muted)";
        });
        btnEl.classList.add("active");
        btnEl.style.color = "var(--text-main)";
      }
    }
    Filters.setFilter("rankingMode", mode);
  }

  function togglePurchaseCompareMode() {
    const currentState = Filters.getState();
    const nextCompare = !currentState.compareMode;
    const btn = document.getElementById("purchase-compare-btn");

    if (btn) {
      if (nextCompare) {
        btn.classList.add("active");
        btn.style.background = "var(--color-primary-light)";
        btn.style.borderColor = "var(--color-primary)";
        btn.style.color = "var(--color-primary)";
      } else {
        btn.classList.remove("active");
        btn.style.background = "";
        btn.style.borderColor = "";
        btn.style.color = "";
      }
    }

    Comp.showToast(nextCompare ? "⚖️ Đã bật Chế độ So sánh Purchase (2015 vs 2014)" : "ℹ️ Đã tắt Chế độ So sánh Purchase");
    Filters.setFilter("compareMode", nextCompare);
  }

  function savePurchasePreset() {
    const currentState = Filters.getState();
    const presetName = prompt("Nhập tên cho Filter Preset Purchase mới:", `Purchase View (${new Date().toLocaleTimeString('vi-VN', {hour:'2-digit', minute:'2-digit'})})`);
    if (!presetName) return;

    Filters.saveCurrentView(presetName);
    Comp.showToast(`✅ Đã lưu Purchase Preset: "${presetName}"`);

    const presetSelect = document.getElementById("purchase-preset-select");
    if (presetSelect) {
      const opt = document.createElement("option");
      opt.value = presetName;
      opt.textContent = `View: ${presetName}`;
      opt.selected = true;
      presetSelect.appendChild(opt);
    }
  }

  function togglePurchaseExportMenu() {
    const dropdown = document.getElementById("purchase-export-dropdown");
    if (dropdown) {
      dropdown.style.display = dropdown.style.display === "block" ? "none" : "block";
    }
  }

  function exportPurchaseCSV() {
    const state = Filters.getState();
    const orders = window.KhangNghiApi ? window.KhangNghiApi.getPurchaseTable(state) : [];
    Svc.exportToCSV("Purchase_Analytics_Report_2015.csv", orders);
    Comp.showToast("📥 Đã xuất file Purchase_Analytics_Report_2015.csv thành công!");
    togglePurchaseExportMenu();
  }

  function exportPurchaseChartPNG() {
    Comp.showToast("🖼️ Biểu đồ Purchase Analytics đã được xuất thành ảnh PNG!");
    togglePurchaseExportMenu();
  }

  function printPurchaseView() {
    togglePurchaseExportMenu();
    window.print();
  }

  function refreshPurchaseData() {
    const icon = document.getElementById("purchase-refresh-icon");
    if (icon) icon.classList.add("fa-spin");
    Comp.showToast("🔄 Đang làm mới dữ liệu Purchase Analytics...");

    setTimeout(() => {
      if (icon) icon.classList.remove("fa-spin");
      renderCurrentView();
      Comp.showToast("✅ Đã cập nhật xong dữ liệu Purchase Analytics mới nhất!");
    }, 400);
  }

  async function openPODetailDrawer(poId) {
    const drawer = document.getElementById("po-detail-drawer");
    const body = document.getElementById("po-detail-drawer-body");
    if (!drawer || !body) return;

    const po = await Svc.getPurchasePODetails(poId);
    body.innerHTML = `
      <div style="margin-bottom:16px;">
        <span class="badge badge-info">${po.id}</span>
        <h3 style="font-size:18px; font-weight:700; margin-top:6px;">${po.supplierName}</h3>
        <span style="font-size:12px; color:var(--text-secondary);">Ngày nhập: <strong>${po.date}</strong></span>
      </div>

      <div class="glass-card" style="margin-bottom:16px;">
        <h4 style="font-size:13px; font-weight:700; margin-bottom:10px;">Chi Tiết Mặt Hàng Import</h4>
        <div style="font-size:12.5px; line-height:1.7;">
          <div><strong>Sản phẩm:</strong> ${po.productName} (${po.productId})</div>
          <div><strong>Danh mục:</strong> ${po.category} &gt; ${po.subcategory}</div>
          <div><strong>Hãng sản xuất:</strong> ${po.manufacturer}</div>
          <div><strong>Số lượng nhập:</strong> ${po.qty.toLocaleString()} sp</div>
          <div><strong>Đơn giá nhập:</strong> ${Comp.formatCurrency(po.unitCost)}</div>
          <div style="margin-top:8px; padding-top:8px; border-top:1px solid var(--border-color); font-size:14px; font-weight:800; color:var(--color-primary);">
            Tổng Giá Trị Đơn (Order Total): ${Comp.formatCurrency(po.totalCost)}
          </div>
        </div>
      </div>

      <button class="topbar-action-btn" style="width:100%; height:38px; background:var(--color-primary); color:white; font-weight:600;" onclick="KhangNghiComponents.showToast('Mở hợp đồng đầy đủ đơn PO ${po.id}')">Xem Hồ Sơ Đơn Hàng PO</button>
    `;
    drawer.classList.add("active");
  }

  function showPurchaseSearchSuggestions() {
    const inp = document.getElementById("purchase-search-input");
    const container = document.getElementById("purchase-search-suggestions");
    if (!inp || !container) return;

    const val = inp.value.trim();
    const suggestions = window.KhangNghiSearch ? window.KhangNghiSearch.getSuggestions(val) : [];

    if (!suggestions.length) {
      container.style.display = "none";
      return;
    }

    container.innerHTML = suggestions.map(item => `
      <div style="padding:8px 14px; cursor:pointer; font-size:12px; display:flex; align-items:center; justify-content:space-between; transition:background 0.15s; border-bottom:1px solid var(--border-color);" onmouseover="this.style.background='var(--bg-app)'" onmouseout="this.style.background='transparent'" onclick="KhangNghiApp.selectPurchaseSearchSuggestion('${item.value}', '${item.type}')">
        <div>
          <span style="font-weight:600; color:var(--text-main);">${item.title}</span>
          <span style="font-size:11px; color:var(--text-muted); margin-left:6px;">(${item.subtitle})</span>
        </div>
        <span class="badge badge-info" style="font-size:10px;">${item.type.toUpperCase()}</span>
      </div>
    `).join("");

    container.style.display = "block";
  }

  function onPurchaseSearchInput(val) {
    showPurchaseSearchSuggestions();
    Filters.setFilter("search", val);
  }

  function selectPurchaseSearchSuggestion(val, type) {
    const container = document.getElementById("purchase-search-suggestions");
    if (container) container.style.display = "none";

    if (type === "supplier") {
      Filters.setFilter("supplier", val);
    } else if (type === "category") {
      Filters.setFilter("category", val);
    } else if (type === "manufacturer") {
      Filters.setFilter("manufacturer", val);
    } else if (type === "po") {
      openPODetailDrawer(val);
    } else {
      Filters.setFilter("search", val);
    }
  }

  // --- GLOBAL SHORTCUT LISTENER (Ctrl + K) ---
  document.addEventListener("keydown", function (e) {
    if ((e.ctrlKey || e.metaKey) && (e.key === "k" || e.key === "K")) {
      e.preventDefault();
      const salesInp = document.getElementById("sales-search-input");
      const purInp = document.getElementById("purchase-search-input");
      const targetInp = purInp && purInp.offsetParent !== null ? purInp : salesInp;

      if (targetInp) {
        targetInp.focus();
        targetInp.select();
        if (targetInp === purInp) showPurchaseSearchSuggestions();
        else showSearchSuggestions();
      }
    }
  });

  // Close search suggestions & export menus when clicking outside
  document.addEventListener("click", function (e) {
    const sug = document.getElementById("sales-search-suggestions");
    const inp = document.getElementById("sales-search-input");
    if (sug && inp && !sug.contains(e.target) && !inp.contains(e.target)) {
      sug.style.display = "none";
    }

    const purSug = document.getElementById("purchase-search-suggestions");
    const purInp = document.getElementById("purchase-search-input");
    if (purSug && purInp && !purSug.contains(e.target) && !purInp.contains(e.target)) {
      purSug.style.display = "none";
    }

    const expDropdown = document.getElementById("sales-export-dropdown");
    if (expDropdown && !e.target.closest("#sales-export-dropdown") && !e.target.closest("button[onclick*='toggleExportMenu']")) {
      expDropdown.style.display = "none";
    }

    const purExpDropdown = document.getElementById("purchase-export-dropdown");
    if (purExpDropdown && !e.target.closest("#purchase-export-dropdown") && !e.target.closest("button[onclick*='togglePurchaseExportMenu']")) {
      purExpDropdown.style.display = "none";
    }
  });

  window.KhangNghiApp = {
    handleLogin,
    fillDemoAccount,
    togglePasswordVisibility,
    switchDemoRole,
    openDetailDrawer,
    openPODetailDrawer,
    askCopilot,
    switchSalesMetric,
    switchGranularity,
    switchRankingMode,
    toggleCompareMode,
    saveCurrentPreset,
    toggleExportMenu,
    exportSalesCSV,
    exportChartPNG,
    printSalesView,
    refreshSalesData,
    showSearchSuggestions,
    onSearchInput,
    selectSearchSuggestion,
    switchPurchaseMetric,
    switchPurchaseGranularity,
    switchSupplierRankingMode,
    togglePurchaseCompareMode,
    savePurchasePreset,
    togglePurchaseExportMenu,
    exportPurchaseCSV,
    exportPurchaseChartPNG,
    printPurchaseView,
    refreshPurchaseData,
    showPurchaseSearchSuggestions,
    onPurchaseSearchInput,
    selectPurchaseSearchSuggestion,
    refreshInventoryData,
    exportInventoryCSV,
    onInventorySearchInput,
    updateCrossAnalysis,
    saveWhatIfScenario,
    filterQuestionCatalog,
    filterQuestionCatalogByDomain
  };
});

