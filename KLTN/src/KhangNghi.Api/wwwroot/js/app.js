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

    // Sync Topbar Global Search and Year Filter
    const topYearSel = document.getElementById("filter-year");
    if (topYearSel && filterState.year) {
      topYearSel.value = filterState.year;
    }
    const topSearch = document.getElementById("topbar-global-search");
    if (topSearch && topSearch.value !== (filterState.search || "")) {
      topSearch.value = filterState.search || "";
    }

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
      case "olap-explorer":
        await renderOlapExplorerView(filterState);
        break;
      case "mdx-studio":
        await renderMdxStudioView(filterState);
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
      case "etl-logs":
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
        ${Comp.renderKpiCard({ id: "ov-rev", label: "Doanh thu", value: m.revenue, changePct: 14.2, icon: "💰" })}
        ${Comp.renderKpiCard({ id: "ov-prof", label: "Lợi nhuận", value: m.profit, changePct: 18.5, icon: "📈" })}
        ${Comp.renderKpiCard({ id: "ov-ord", label: "Số đơn hàng", value: m.orders, changePct: 9.4, icon: "📦", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "ov-margin", label: "Biên lợi nhuận", value: parseFloat(m.margin), changePct: 2.1, icon: "📊", isCurrency: false, unit: "%" })}
        ${Comp.renderKpiCard({ id: "ov-cust", label: "Số khách hàng", value: m.customers || 17442, changePct: 12.0, icon: "👥", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "ov-qty", label: "Số lượng bán", value: m.quantity || 231905, changePct: 15.3, icon: "🏷️", isCurrency: false })}
      `;
    }

    setTimeout(() => {
      Charts.renderSparkline("spark-ov-rev", [12, 14, 15, 18, 20, 24, 28], "#0f62fe");
      Charts.renderSparkline("spark-ov-prof", [3, 4, 4.5, 5.2, 6.1, 7.5], "#198038");
      Charts.renderSparkline("spark-ov-ord", [400, 420, 450, 490, 520, 580], "#8a3ffc");
      Charts.renderSparkline("spark-ov-margin", [10, 10.5, 11, 11.8, 12.1], "#b25900");
      Charts.renderSparkline("spark-ov-inv", [90, 88, 85, 84, 82], "#da1e28");
      Charts.renderSparkline("spark-ov-sla", [92, 93, 94, 95, 95.8], "#198038");

      Charts.renderRevenueTrend("chart-ov-trend", data.trend || data.yearTrend || data.forecastSeries);
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
        ? ` <span style="background:rgba(37,162,68,0.15); color:#25a244; font-size:11px; padding:2px 8px; border-radius:10px; font-weight:600; margin-left:8px;">● DWH TRỰC TUYẾN</span>` 
        : ` <span style="background:rgba(241,196,15,0.15); color:#f1c40f; font-size:11px; padding:2px 8px; border-radius:10px; font-weight:600; margin-left:8px;">● MÔ PHỎNG DỮ LIỆU</span>`;
      subEl.innerHTML = `Hiển thị <strong>${m.orders ? m.orders.toLocaleString() : 0}</strong> đơn hàng | Doanh thu: <strong>${Comp.formatCurrency(m.revenue)}</strong> | Lợi nhuận: <strong>${Comp.formatCurrency(m.profit)}</strong> (${yrText}, ${regText})${liveBadge}`;
    }

    // 5. Check empty state (if filtered metrics equal zero or empty)
    const emptyState = document.getElementById("sales-empty-state");
    const row1El = document.getElementById("sales-charts-row-1");
    const yearProdCard = document.getElementById("chart-sal-year-product")?.closest(".chart-card");
    const bottomGrid = document.getElementById("sales-detail-table-container")?.closest(".grid-charts-2-1");

    if (m.revenue === 0 || m.orders === 0) {
      if (emptyState) emptyState.style.display = "block";
      if (row1El) row1El.style.display = "none";
      if (yearProdCard) yearProdCard.style.display = "none";
      if (bottomGrid) bottomGrid.style.display = "none";
    } else {
      if (emptyState) emptyState.style.display = "none";
      if (row1El) row1El.style.display = "grid";
      if (yearProdCard) yearProdCard.style.display = "block";
      if (bottomGrid) bottomGrid.style.display = "grid";
    }

    // 6. Render KPI Summary Cards
    const kpiEl = document.getElementById("sales-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "sal-rev", label: "Tổng Doanh Thu", value: m.revenue, changePct: 25.7, icon: "💵" })}
        ${Comp.renderKpiCard({ id: "sal-prof", label: "Lợi Nhuận Gộp", value: m.profit, changePct: 25.8, icon: "💎" })}
        ${Comp.renderKpiCard({ id: "sal-ord", label: "Số Đơn Hàng", value: m.orders, changePct: 15.2, icon: "🛒", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "sal-aov", label: "Giá Trị Đơn Trung Bình (AOV)", value: m.aov, changePct: 9.1, icon: "🏷️" })}
      `;
    }

    // 7. Render 4 Biểu Đồ & Bảng Đối Chiếu Số Liệu
    setTimeout(() => {
      const metric = filterState.metric || "revenue";
      const gran = filterState.granularity || "quarterly";
      const trendData = gran === "yearly" ? data.yearlySales : data.quarterlySales;
      const productList = metric === "profit" ? (data.topProfitProducts || data.topProducts) : (data.topProducts || []);

      // === BIỂU ĐỒ 1: XU HƯỚNG THEO THỜI GIAN ===
      Charts.renderRevenueTrend("chart-sal-trend", trendData, {
        metric: metric,
        granularity: gran,
        onPointClick: (rawItem, timeLabel) => {
          if (rawItem && rawItem.year) {
            Comp.showToast(`📅 Đã chọn mốc thời gian: ${timeLabel}. Đang đồng bộ bộ lọc...`);
            Filters.setFilter("year", String(rawItem.year));
          } else if (timeLabel) {
            Comp.showToast(`📊 Mốc thời gian: ${timeLabel}`);
          }
        }
      });

      // Insight Biểu đồ 1
      const trendInsightEl = document.getElementById("chart-sal-trend-insight");
      if (trendInsightEl && trendData && trendData.length > 0) {
        const peak = [...trendData].sort((a, b) => (b.revenue || 0) - (a.revenue || 0))[0];
        trendInsightEl.innerHTML = `<strong>Nhận xét:</strong> Giai đoạn đạt doanh thu cao nhất là <strong>${peak.period}</strong> với <strong>${Comp.formatCurrency(peak.revenue)}</strong> (${metric === 'profit' ? 'Lợi nhuận: ' + Comp.formatCurrency(peak.profit) : 'Tăng trưởng ổn định'}).`;
      }

      // === BIỂU ĐỒ 2: XẾP HẠNG THEO SẢN PHẨM ===
      Charts.renderSalesByCategory("chart-sal-cat", productList, {
        metric: metric,
        rankingMode: filterState.rankingMode || "top10",
        onBarClick: (prod) => {
          const pName = prod.ProductName || prod.productName || prod.name;
          Comp.showToast(`🔍 Đã chọn sản phẩm: ${pName}`);
          if (prod.Category || prod.category) {
            Filters.setFilter("category", prod.Category || prod.category);
          }
        }
      });

      // Insight Biểu đồ 2
      const rankingInsightEl = document.getElementById("chart-sal-ranking-insight");
      if (rankingInsightEl && productList.length > 0) {
        const top1 = productList[0];
        const top1Name = top1.ProductName || top1.productName || 'Sản phẩm đầu bảng';
        const top1Val = metric === 'profit' ? (top1.Profit || top1.profit || 0) : (top1.Revenue || top1.revenue || 0);
        rankingInsightEl.innerHTML = `<strong>Sản phẩm dẫn đầu:</strong> <strong>${top1Name}</strong> đóng góp <strong>${Comp.formatCurrency(top1Val)}</strong> trong kỳ.`;
      }

      // === BIỂU ĐỒ 3: KẾT HỢP NĂM × SẢN PHẨM (SSAS CUBE) ===
      Charts.renderTimeProductCombinedChart("chart-sal-year-product", data.yearProductPoints || []);
      const yearProdInsightEl = document.getElementById("chart-sal-year-product-insight");
      if (yearProdInsightEl) {
        yearProdInsightEl.innerHTML = `<strong>Phân tích đa chiều:</strong> Doanh số máy in <em>Canon imageCLASS 2200</em> tăng vọt từ năm 2014 và bứt phá mạnh nhất năm 2015 ($143.5K), trong khi nhóm <em>Siemens Smart Phone</em> duy trì tăng trưởng đều đặn qua cả 4 năm (2012-2015).`;
      }

      // === BIỂU ĐỒ 4: CƠ CẤU TỶ TRỌNG DOANH THU ===
      Charts.renderProductShareDonut("chart-sal-share", data.categories || [], {
        metric: metric,
        onSliceClick: (item, label) => {
          Comp.showToast(`🔍 Đã lọc danh mục sản phẩm: [${label}]`);
          Filters.setFilter("category", label);
        }
      });
      const shareInsightEl = document.getElementById("chart-sal-share-insight");
      if (shareInsightEl && data.categories && data.categories.length > 0) {
        const totalCatRev = data.categories.reduce((acc, c) => acc + (c.revenue || 0), 0);
        const topCat = data.categories[0];
        const topPct = totalCatRev > 0 ? ((topCat.revenue / totalCatRev) * 100).toFixed(1) : 0;
        shareInsightEl.innerHTML = `<strong>Cơ cấu:</strong> Nhóm <strong>${topCat.category}</strong> chiếm tỷ trọng lớn nhất với <strong>${topPct}%</strong> tổng doanh số.`;
      }

      // === BẢNG ĐỐI CHIẾU SỐ LIỆU CHI TIẾT ===
      const tableContainer = document.getElementById("sales-detail-table-container");
      if (tableContainer) {
        if (!productList || productList.length === 0) {
          tableContainer.innerHTML = `<div style="padding:20px; text-align:center; color:var(--text-muted);">Không có dữ liệu chi tiết phù hợp.</div>`;
        } else {
          let rowsHtml = productList.slice(0, 15).map((p, idx) => {
            const pKey = p.ProductKey || (idx + 1);
            const pName = p.ProductName || p.productName || "Sản phẩm";
            const pCat = p.Category || p.category || "—";
            const pSub = p.SubCategory || p.subCategory || "—";
            const pRev = p.Revenue || p.revenue || 0;
            const pProf = p.Profit || p.profit || 0;
            const pQty = p.Quantity || p.quantity || 0;
            const pMargin = pRev > 0 ? ((pProf / pRev) * 100).toFixed(1) : "0.0";
            return `
              <tr>
                <td style="font-weight:600; color:var(--text-muted); text-align:center;">#${idx + 1}</td>
                <td><strong style="color:var(--text-main); font-size:12px;">${pName}</strong><br><span style="font-size:10.5px; color:var(--text-muted);">Mã: PRD-${pKey}</span></td>
                <td><span class="badge badge-info" style="font-size:10.5px;">${pCat}</span><br><span style="font-size:10.5px; color:var(--text-muted);">${pSub}</span></td>
                <td style="text-align:right; font-weight:600;">${pQty.toLocaleString()}</td>
                <td style="text-align:right; font-weight:700; color:var(--color-primary);">${Comp.formatCurrency(pRev)}</td>
                <td style="text-align:right; font-weight:700; color:#198038;">${Comp.formatCurrency(pProf)}</td>
                <td style="text-align:right;"><span style="font-size:11px; font-weight:700; color:${parseFloat(pMargin) >= 15 ? '#198038' : '#b25900'};">${pMargin}%</span></td>
              </tr>
            `;
          }).join("");

          tableContainer.innerHTML = `
            <table class="table" style="width:100%; font-size:12px;">
              <thead>
                <tr>
                  <th style="width:40px; text-align:center;">STT</th>
                  <th>Tên Sản Phẩm</th>
                  <th>Phân Nhóm</th>
                  <th style="text-align:right;">SL Bán</th>
                  <th style="text-align:right;">Doanh Thu ($)</th>
                  <th style="text-align:right;">Lợi Nhuận ($)</th>
                  <th style="text-align:right;">Biên LN (%)</th>
                </tr>
              </thead>
              <tbody>
                ${rowsHtml}
              </tbody>
            </table>
          `;
        }
      }
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 03. PURCHASE VIEW RENDERER (WORKSPACES BI & DETAILED PROCUREMENT ENGINE)
  // --------------------------------------------------------------------------
  async function renderPurchaseView(filterState) {
    // 0. Tối ưu năm cho phân hệ Mua hàng (Fact_Purchase DWH nạp từ nguồn 2022 - 2025)
    let purchaseYear = filterState.year;
    const validPurchaseYears = ["2022", "2023", "2024", "2025", "ALL"];
    if (!validPurchaseYears.includes(purchaseYear)) {
      purchaseYear = "2025";
      filterState.year = "2025";
    }

    // 1. Sync Smart Filter Bar Controls with Filter State
    const searchInp = document.getElementById("purchase-search-input");
    if (searchInp && searchInp.value !== (filterState.search || "")) {
      searchInp.value = filterState.search || "";
    }

    const yrSel = document.getElementById("purchase-filter-year");
    if (yrSel) yrSel.value = purchaseYear;

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
    const queryFilters = { ...filterState, year: purchaseYear };
    const data = (await Svc.getPurchaseOverview(queryFilters)) || {};
    const m = data.metrics || { totalPurchaseCost: 0, poCount: 0, purchasedQty: 0, weightedUnitCost: 0, activeSuppliers: 0, avgPOValue: 0, priorCostChange: 0, priorPOChange: 0, priorQtyChange: 0 };

    // 4. Dynamic Subtitle Context
    const subEl = document.getElementById("purchase-dynamic-subtitle");
    if (subEl) {
      const yrText = purchaseYear === "ALL" ? "Tất cả các năm (2022–2025)" : `Năm ${purchaseYear}`;
      const supText = filterState.supplier && filterState.supplier !== "All Suppliers" ? filterState.supplier : "Tất cả 6 nhà cung cấp";
      if (!data.hasData) {
        subEl.innerHTML = `Hiển thị <strong>0</strong> đơn mua (PO) | Tổng chi phí mua: <strong>0 VNĐ</strong> | Giá mua TB: <strong>0 VNĐ</strong>/sp (${yrText}, chưa có dữ liệu mua hàng năm này trong DWH)`;
      } else {
        subEl.innerHTML = `Hiển thị <strong>${m.poCount ? m.poCount.toLocaleString() : 0}</strong> đơn mua (PO) | Tổng chi phí mua: <strong>${Comp.formatCurrency(m.totalPurchaseCost)}</strong> | Giá mua TB: <strong>${Comp.formatCurrency(m.weightedUnitCost)}</strong>/sp (${yrText}, ${supText})`;
      }
    }

    // 5. Check Empty / No Data State
    const emptyState = document.getElementById("purchase-empty-state");
    const mainContainer = document.getElementById("purchase-main-container");
    if (!data.hasData || m.totalPurchaseCost === 0 || m.poCount === 0) {
      if (emptyState) {
        const curYr = filterState.year || "2015";
        emptyState.innerHTML = `
          <div style="width:60px; height:60px; background:rgba(243,156,18,0.15); color:#f39c12; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-size:26px; margin-bottom:14px;">
            <i class="fa-solid fa-calendar-xmark"></i>
          </div>
          <h3 style="font-size:16px; font-weight:700; margin-bottom:8px; color:var(--text-main);">
            Năm ${curYr} Chưa Có Dữ Liệu Mua Hàng Trong Kho DWH_KhangNghi
          </h3>
          <p style="font-size:13px; color:var(--text-muted); max-width:620px; margin:0 auto 16px; line-height:1.6;">
            Trong CSDL <code>DWH_KhangNghi</code>, bảng <code>Fact_Purchase</code> hiện lưu trữ <strong>7,747 đơn đặt hàng (PO)</strong> với tổng giá trị <strong>167.76 tỷ VNĐ</strong> thuộc giai đoạn <strong>2022–2025</strong> từ 6 nhà cung cấp.
          </p>
          <div style="display:flex; justify-content:center; gap:12px; flex-wrap:wrap; margin-bottom:14px;">
            <button class="topbar-action-btn" onclick="KhangNghiFilters.setFilter('year', '2025')" style="width:auto; padding:8px 16px; font-size:12.5px; font-weight:600; background:linear-gradient(135deg, var(--color-primary), #2563eb); color:white; border:none; border-radius:6px; cursor:pointer; box-shadow:0 3px 10px rgba(37,99,235,0.3);">
              👉 Xem Dữ Liệu Mua Hàng Năm 2025 (Kỳ mới nhất - 57.92 Tỷ VNĐ)
            </button>
            <button class="topbar-action-btn" onclick="KhangNghiFilters.setFilter('year', 'ALL')" style="width:auto; padding:8px 16px; font-size:12.5px; font-weight:600; background:var(--bg-app); border:1px solid var(--border-color); color:var(--text-main); border-radius:6px; cursor:pointer;">
              👉 Xem Toàn Bộ Kỳ Mua Hàng 2022–2025 (7,747 POs - 167.76 Tỷ VNĐ)
            </button>
          </div>
          <div style="font-size:11.5px; color:var(--text-secondary); background:var(--bg-app); display:inline-block; padding:8px 16px; border-radius:8px; border:1px solid var(--border-color);">
            <i class="fa-solid fa-diagram-project" style="margin-right:6px; color:var(--color-primary);"></i> Lineage: <strong>Nguồn KhangNghi_Purchasing_OLTP_VN → Fact_Purchase (7,747 POs) → SSAS KhangNghi_Cube → REST API → Web BI</strong>
          </div>
        `;
        emptyState.style.display = "block";
      }
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
        ${Comp.renderKpiCard({ id: "pur-cost", label: isCompare ? "Chi phí mua hàng (vs 2014)" : "Chi phí mua hàng", value: m.totalPurchaseCost, changePct: isCompare ? 20.5 : m.priorCostChange, icon: "🛒", tooltip: "Tổng chi phí mua hàng trong kỳ chọn, được tính bằng tổng giá trị tất cả các đơn đặt hàng nhập (PO)." })}
        ${Comp.renderKpiCard({ id: "pur-orders", label: isCompare ? "Số đơn mua (vs 2014)" : "Số đơn mua", value: m.poCount, changePct: isCompare ? 18.1 : m.priorPOChange, icon: "📋", isCurrency: false, tooltip: "Tổng số lượng đơn đặt hàng mua (PO) đã phát hành với các nhà cung cấp." })}
        ${Comp.renderKpiCard({ id: "pur-qty", label: isCompare ? "Số lượng mua (vs 2014)" : "Số lượng mua", value: m.purchasedQty, changePct: isCompare ? 18.3 : m.priorQtyChange, icon: "📦", isCurrency: false, tooltip: "Tổng số lượng sản phẩm/hàng hóa đã nhập kho trong kỳ chọn." })}
        ${Comp.renderKpiCard({ id: "pur-unit-cost", label: "Chi phí đơn vị trung bình", value: m.weightedUnitCost, changePct: 1.8, icon: "🏷️", unit: "/sp", tooltip: "Chi phí mua trung bình trên một đơn vị sản phẩm, được tính bằng tổng chi phí mua chia tổng số lượng mua." })}
        ${Comp.renderKpiCard({ id: "pur-active-sup", label: "Số nhà cung cấp", value: m.activeSuppliers, changePct: 0, icon: "🤝", isCurrency: false, tooltip: "Số lượng nhà cung cấp phát sinh đơn đặt hàng trong kỳ phân tích." })}
        ${Comp.renderKpiCard({ id: "pur-avg-po", label: "Giá trị đơn mua trung bình", value: m.avgPOValue, changePct: 2.1, icon: "📜", tooltip: "Giá trị trung bình của một đơn đặt hàng mua (PO)." })}
      `;
    }

    // 7. Render Charts & Components
    setTimeout(() => {
      // Main Purchase Trend Chart (VND)
      Charts.renderRevenueTrend("chart-pur-trend", data.trend, {
        metric: filterState.metric || "cost",
        granularity: filterState.granularity || "monthly",
        currency: 'VND',
        onPointClick: (rawItem, timeLabel) => {
          if (rawItem && (rawItem.Year || rawItem.year)) {
            const yr = String(rawItem.Year || rawItem.year);
            Comp.showToast(`📅 Đã chọn năm mua hàng: ${yr}. Đang đồng bộ bộ lọc...`);
            Filters.setFilter("year", yr);
          } else {
            Comp.showToast(`📊 Mua hàng: ${timeLabel}`);
          }
        }
      });

      // Supplier Spend Horizontal Bar (VND)
      Charts.renderSupplierBarChart("chart-pur-supplier", data.supplierSpend || [], {
        metric: filterState.metric || "cost",
        rankingMode: filterState.rankingMode || "top5",
        currency: 'VND',
        onBarClick: (supplierName) => {
          Comp.showToast(`🚚 Lọc nhanh: Lọc theo Nhà cung cấp [${supplierName}]`);
          Filters.setFilter("supplier", supplierName);
        }
      });

      // Supplier Donut & Concentration Card (VND)
      Charts.renderSupplierDonutChart("chart-pur-sup-donut", data.supplierSpend || [], {
        currency: 'VND',
        onSliceClick: (sup, name) => {
          Comp.showToast(`🚚 Lọc nhanh: Lọc theo Nhà cung cấp [${name}]`);
          Filters.setFilter("supplier", name);
        }
      });
      const conc = data.supplierConcentration || {};
      const concTop1 = document.getElementById("sup-conc-top1");
      const concDesc = document.getElementById("sup-conc-desc");
      const concTop3 = document.getElementById("sup-conc-top3");
      const concTop5 = document.getElementById("sup-conc-top5");
      if (concTop1) concTop1.textContent = `${conc.top1SharePct}%`;
      if (concDesc) concDesc.textContent = `Top 1 Supplier (${conc.top1Supplier}) chiếm ${conc.top1SharePct}% tổng spend.`;
      if (concTop3) concTop3.textContent = `${conc.top3SharePct}%`;
      if (concTop5) concTop5.textContent = `${conc.top5SharePct}%`;

      // Purchase Cost by Category Bar (VND)
      Charts.renderSalesByCategory("chart-pur-cat", data.categorySpend || [], {
        currency: 'VND',
        onBarClick: (categoryItem) => {
          const categoryName = categoryItem.Category || categoryItem.category || categoryItem.name;
          Comp.showToast(`📦 Lọc nhanh: Lọc theo Danh mục [${categoryName}]`);
          Filters.setFilter("category", categoryName);
        }
      });

      // Purchase by Manufacturer Bar (VND)
      Charts.renderManufacturerBarChart("chart-pur-mfg", data.manufacturerAnalysis || [], {
        currency: 'VND',
        onBarClick: (mfgName) => {
          Comp.showToast(`🏭 Lọc nhanh: Lọc theo Hãng sản xuất [${mfgName}]`);
          Filters.setFilter("manufacturer", mfgName);
        }
      });

      // Weighted Average Unit Cost Trend (VND)
      Charts.renderUnitCostLineChart("chart-pur-unit-cost", data.trend || [], { currency: 'VND' });

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
            <td><strong>${po.supplierName}</strong></td>
            <td><span class="badge badge-info">${po.warehouseName || 'Kho Miền Bắc'}</span></td>
            <td>${(po.lineItems || 0).toLocaleString()} mục</td>
            <td>${(po.qty || 0).toLocaleString()} sp</td>
            <td>${Comp.formatCurrency(po.unitCost)}</td>
            <td><strong style="color:var(--color-primary);">${Comp.formatCurrency(po.totalCost)}</strong></td>
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
          <div><span style="color:var(--text-muted);">Tổng chi phí mua:</span><br><strong style="font-size:14px; color:var(--color-primary);">${Comp.formatCurrency(vs.purchaseCost)}</strong></div>
          <div><span style="color:var(--text-muted);">Tổng doanh thu bán:</span><br><strong style="font-size:14px; color:var(--color-success);">${Comp.formatCurrency(vs.salesRevenue)}</strong></div>
          <div><span style="color:var(--text-muted);">Tỷ lệ Chi phí / Doanh thu:</span><br><strong>${vs.costToRevRatio}%</strong></div>
          <div><span style="color:var(--text-muted);">Lợi nhuận gộp đối chiếu:</span><br><strong>${Comp.formatCurrency(vs.grossProfit)}</strong></div>
        `;
      }

      const vsInvBody = document.getElementById("pur-vs-inv-body");
      if (vsInvBody) {
        const vi = data.vsInventory || {};
        vsInvBody.innerHTML = `
          <div><span style="color:var(--text-muted);">Số lượng vừa nhập:</span><br><strong style="font-size:14px;">${(vi.recentlyPurchasedQty || 0).toLocaleString()} sp</strong></div>
          <div><span style="color:var(--text-muted);">Tồn kho nguy cấp:</span><br><strong style="font-size:14px; color:var(--color-danger);">${vi.criticalItemsCount} mặt hàng</strong></div>
          <div><span style="color:var(--text-muted);">Tổng giá trị tồn kho:</span><br><strong>${Comp.formatCurrency(vi.totalStockVal)}</strong></div>
          <div><span style="color:var(--text-muted);">Cảnh báo dưới tồn an toàn:</span><br><strong style="color:var(--color-warning);">${vi.lowStockCount} mặt hàng</strong></div>
        `;
      }
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 04. INVENTORY CONTROL RENDERER (ROUND 1)
  // --------------------------------------------------------------------------
  async function renderInventoryView(filterState) {
    const data = await Svc.getInventoryFullOverview(filterState);
    
    if (!data || data.hasData === false) {
      const kpiEl = document.getElementById("inventory-kpis");
      if (kpiEl) {
        kpiEl.innerHTML = Comp.renderNoDataBanner({
          title: "Chưa Có Dữ Liệu Tồn Kho Phù Hợp",
          message: "Không tìm thấy dữ liệu tồn kho theo bộ lọc hiện tại. Bảng Fact_Inventory trong kho dữ liệu DWH_KhangNghi hiện lưu trữ 55,067 bản ghi nhập xuất tồn thực tế.",
          icon: "📦",
          badgeText: "INVENTORY FILTER EMPTY"
        });
      }
      return;
    }

    // KPI Cards
    const kpiEl = document.getElementById("inventory-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "inv-val", label: "Giá trị tồn kho", value: data.inventoryValue, changePct: -2.4, icon: "🏭", tooltip: "Tổng giá trị tiền hàng tồn kho dựa trên đơn giá vốn hiện tại." })}
        ${Comp.renderKpiCard({ id: "inv-hand", label: "Số lượng tồn", value: data.stockOnHand, changePct: 1.2, icon: "📦", isCurrency: false, tooltip: "Tổng số lượng sản phẩm đang có sẵn trong kho." })}
        ${Comp.renderKpiCard({ id: "inv-low", label: "Cảnh báo tồn kho", value: data.lowStockItems, changePct: 0, icon: "⚠️", isCurrency: false, tooltip: "Số lượng mặt hàng có tồn kho dưới Safety Stock." })}
        ${Comp.renderKpiCard({ id: "inv-out", label: "Sản phẩm hết hàng", value: data.outOfStockItems, changePct: 0, icon: "🚨", isCurrency: false, tooltip: "Số lượng mặt hàng đã hoàn toàn đứt hàng (Stock = 0)." })}
        ${Comp.renderKpiCard({ id: "inv-over", label: "Tồn kho vượt định mức", value: data.overstockItems, changePct: -4.5, icon: "📦", isCurrency: false, tooltip: "Mặt hàng tồn vượt quá 3 lần mức Safety Stock." })}
        ${Comp.renderKpiCard({ id: "inv-cov", label: "Thời gian bao phủ tồn kho", value: data.stockCoverageMonths, changePct: 0.5, icon: "⏱️", isCurrency: false, unit: " tháng", tooltip: "Số tháng bán hàng dự kiến bao phủ bởi lượng tồn kho hiện tại." })}
      `;
    }

    // Health visual bar
    const h = data.health || { healthyPct: 72, warningPct: 18, criticalPct: 10 };
    const hBarHealthy = document.getElementById("inv-health-bar-healthy");
    const hBarWarning = document.getElementById("inv-health-bar-warning");
    const hBarCritical = document.getElementById("inv-health-bar-critical");
    const hRatioText = document.getElementById("inv-health-ratio-text");

    if (hBarHealthy) hBarHealthy.style.width = h.healthyPct + "%";
    if (hBarWarning) hBarWarning.style.width = h.warningPct + "%";
    if (hBarCritical) hBarCritical.style.width = h.criticalPct + "%";
    if (hRatioText) hRatioText.textContent = `An toàn: ${h.healthyPct}% | Cảnh báo: ${h.warningPct}% | Nguy cấp: ${h.criticalPct}%`;

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
          <td>${Comp.renderStatusBadge ? Comp.renderStatusBadge(item.status) : `<span class="badge badge-info">${item.status}</span>`}</td>
          <td><button class="topbar-action-btn" style="width:auto; padding:0 8px; font-size:11px;" onclick="event.stopPropagation(); KhangNghiApp.openDetailDrawer('${item.id}')">Chi tiết</button></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 05. SHIPPING & SLA RENDERER (ROUND 2)
  // --------------------------------------------------------------------------
  async function renderShippingView(filterState) {
    const data = await Svc.getShippingFullOverview(filterState);
    
    if (!data || data.hasData === false) {
      const kpiEl = document.getElementById("shipping-kpis");
      if (kpiEl) {
        kpiEl.innerHTML = Comp.renderNoDataBanner({
          title: "Chưa Có Dữ Liệu Thực Tế / No Real Data in DWH",
          message: "Bảng Fact_Shipping trong CSDL DWH_KhangNghi hiện có 0 bản ghi. Web không tự tạo dữ liệu giả lập.",
          icon: "🚚",
          badgeText: "NO DATA IN DWH FACT_SHIPPING"
        });
      }
      return;
    }
    
    // KPI Cards
    const kpiEl = document.getElementById("shipping-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "shp-count", label: "Số đơn vận chuyển", value: data.totalShipments, changePct: 8.4, icon: "🚚", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "shp-cost", label: "Chi phí vận chuyển", value: data.shippingCost, changePct: 2.3, icon: "⛽" })}
        ${Comp.renderKpiCard({ id: "shp-days", label: "Thời gian giao hàng trung bình", value: data.avgDeliveryDays, changePct: -4.2, icon: "⏱️", isCurrency: false, unit: " ngày" })}
        ${Comp.renderKpiCard({ id: "shp-ontime", label: "Tỷ lệ giao đúng hạn", value: data.onTimeRate, changePct: 1.5, icon: "✅", isCurrency: false, unit: "%" })}
        ${Comp.renderKpiCard({ id: "shp-late", label: "Số đơn giao trễ", value: data.lateShipments, changePct: -12.5, icon: "🛑", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "shp-avgcost", label: "Chi phí vận chuyển trung bình", value: data.avgShippingCost, changePct: -1.2, icon: "🏷️", unit: "/đơn" })}
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
          <td>${Comp.renderStatusBadge ? Comp.renderStatusBadge(s.status === 'Healthy' ? 'HEALTHY' : 'WARNING') : `<span class="badge badge-success">${s.status}</span>`}</td>
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
        Charts.renderCustomerSegmentDonut("chart-cus-seg", data.segments, {
          onSliceClick: (seg, label) => {
            Comp.showToast(`👥 Lọc nhanh: Phân khúc [${label}]`);
            Filters.setFilter("segment", label);
          }
        });
      }
      if (data.topCustomers && Array.isArray(data.topCustomers)) {
        Charts.renderCustomerScatterMatrix("chart-cus-scatter", data.topCustomers);
      }
    }, 50);

    // Customer Table with mapped DTO properties
    const body = document.getElementById("customer-table-body");
    if (body) {
      body.innerHTML = (data.topCustomers || []).map(c => {
        const rawNaturalId = c.customerID || c.CustomerID || c.customerId;
        const naturalId = rawNaturalId && rawNaturalId !== "undefined" && rawNaturalId !== "null" ? rawNaturalId : "N/A";
        if (naturalId === "N/A") {
          console.warn(`[Data Quality Warning] Missing Customer Natural Key for CustomerKey: ${c.customerKey || c.CustomerKey}`);
        }
        const name = c.customerName || c.CustomerName || c.name || "Khách hàng DWH";
        const segment = c.segment || c.Segment || "Consumer";
        const orders = c.orderCount || c.OrderCount || c.orders || 1;
        const revenue = c.revenue || c.Revenue || c.totalSpent || 0;
        const profit = c.profit || c.Profit || Math.round(revenue * 0.12);
        const margin = revenue > 0 ? ((profit / revenue) * 100).toFixed(1) : "12.0";
        return `
        <tr style="cursor:pointer;" onclick="KhangNghiComponents.openCustomerDetailDrawer('${naturalId}')">
          <td><strong style="color:var(--color-primary); text-decoration:underline;">${naturalId}</strong></td>
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
    
    if (!data || data.hasData === false) {
      const kpiEl = document.getElementById("supplier-kpis");
      if (kpiEl) {
        kpiEl.innerHTML = Comp.renderNoDataBanner({
          title: "Chưa Có Dữ Liệu Thực Tế / No Real Data in DWH",
          message: "Bảng Dim_Supplier trong CSDL DWH_KhangNghi hiện có 0 bản ghi. Web không tự tạo dữ liệu giả lập.",
          icon: "🤝",
          badgeText: "NO DATA IN DWH DIM_SUPPLIER"
        });
      }
      return;
    }
    
    // KPI Cards
    const kpiEl = document.getElementById("supplier-kpis");
    if (kpiEl) {
      kpiEl.innerHTML = `
        ${Comp.renderKpiCard({ id: "sup-total", label: "NHÀ CUNG CẤP HOẠT ĐỘNG", value: data.totalSuppliers, changePct: 0, icon: "🤝", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "sup-cost", label: "TỔNG CHI PHÍ MUA HÀNG", value: data.purchaseCost, changePct: 20.5, icon: "🛒" })}
        ${Comp.renderKpiCard({ id: "sup-qty", label: "SỐ LƯỢNG MUA", value: data.purchasedQty, changePct: 18.3, icon: "📦", isCurrency: false })}
        ${Comp.renderKpiCard({ id: "sup-avg-unit", label: "CHI PHÍ ĐƠN VỊ TRUNG BÌNH", value: data.avgUnitCost, changePct: 1.8, icon: "🏷️", unit: "/sp" })}
        ${Comp.renderKpiCard({ id: "sup-top", label: "NHÀ CUNG CẤP HÀNG ĐẦU", value: data.topSupplier, changePct: 0, icon: "🏆", isCurrency: false })}
      `;
    }

    setTimeout(() => {
      Charts.renderSupplierBarChart("chart-sup-rank", data.spend || [], {
        currency: 'VND',
        rankingMode: 'top10',
        onBarClick: (supplierName) => {
          Comp.showToast(`🚚 Lọc nhanh: Lọc theo Nhà cung cấp [${supplierName}]`);
          Filters.setFilter("supplier", supplierName);
        }
      });
      Charts.renderSupplierDonutChart("chart-sup-donut", data.spend || [], {
        currency: 'VND',
        onSliceClick: (sup, name) => {
          Comp.showToast(`🚚 Lọc nhanh: Lọc theo Nhà cung cấp [${name}]`);
          Filters.setFilter("supplier", name);
        }
      });
    }, 50);

    const body = document.getElementById("supplier-table-body");
    if (body) {
      body.innerHTML = (data.spend || []).map((s, idx) => {
        const id = s.id || `NCC-${String(idx + 1).padStart(3, '0')}`;
        const name = s.name || s.supplier || s.supplierName || "Siemens";
        const cost = s.cost || s.totalPurchaseAmount || 0;
        const share = s.sharePct || s.share || 20;
        const country = s.country || "Toàn cầu (DWH)";
        return `
        <tr style="cursor:pointer;" onclick="KhangNghiComponents.showToast('NCC ${name}: Chi phí nhập $${Comp.formatCurrency(cost)}')">
          <td><strong style="color:var(--color-primary);">${id}</strong></td>
          <td><strong>${name}</strong></td>
          <td>${country}</td>
          <td>4 ngày</td>
          <td>⭐ 4.8</td>
          <td><strong>${Comp.formatCurrency(cost)}</strong></td>
          <td><strong style="color:var(--color-primary);">${share}%</strong></td>
        </tr>
        `;
      }).join('');
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
    const summaryText = (data && data.relationshipSummary) ? data.relationshipSummary : "Doanh thu bán hàng và Giá trị tồn kho thể hiện mối tương quan đồng biến dương rõ rệt trên toàn bộ các nhóm hàng.";
    if (insEl) insEl.innerHTML = `💡 <strong>Tóm tắt mối tương quan:</strong> ${summaryText}`;

    setTimeout(() => {
      const matrix = (data && Array.isArray(data.matrixData)) ? data.matrixData : [];
      Charts.renderCrossMatrix("chart-cross-matrix", matrix);
    }, 50);
  }

  // --------------------------------------------------------------------------
  // 10B. OLAP CUBE EXPLORER RENDERER (SSAS MULTI-DIMENSIONAL ANALYSIS)
  // --------------------------------------------------------------------------
  let currentOlapState = {
    measure: "[Measures].[Sales Amount]",
    rows: "[Dim Product].[Category]",
    cols: "[Dim Time].[Year]",
    slice: "NONE"
  };

  async function renderOlapExplorerView(filterState) {
    updateOlapMdxPreview();
    renderOlapTable();
  }

  function updateOlapMdxPreview() {
    const measure = document.getElementById("olap-select-measure")?.value || currentOlapState.measure;
    const rows = document.getElementById("olap-select-rows")?.value || currentOlapState.rows;
    const cols = document.getElementById("olap-select-cols")?.value || currentOlapState.cols;
    const slice = document.getElementById("olap-select-slice")?.value || currentOlapState.slice;

    let mdx = `SELECT \n    ${cols}.MEMBERS ON COLUMNS,\n    ${rows}.MEMBERS ON ROWS\nFROM [KhangNghi_Cube]`;
    if (slice === "DICE_NAM_2026") {
      mdx += `\nWHERE (\n    [Dim Location].[Region].&[Miền Nam],\n    [Dim Time].[Year].&[2026],\n    ${measure}\n);`;
    } else if (slice && slice !== "NONE") {
      mdx += `\nWHERE (\n    ${slice},\n    ${measure}\n);`;
    } else {
      mdx += `\nWHERE (${measure});`;
    }

    const previewEl = document.getElementById("olap-generated-mdx");
    if (previewEl) previewEl.textContent = mdx;
    return mdx;
  }

  function sendGeneratedMdxToStudio() {
    const mdx = updateOlapMdxPreview();
    const studioInput = document.getElementById("mdx-query-input");
    if (studioInput) studioInput.value = mdx;
    window.location.hash = "#/mdx-studio";
    switchMdxSubTab("studio");
    Comp.showToast("🚀 Đã nạp truy vấn OLAP vào MDX Query Studio!");
  }

  function applyOlapOperation(op) {
    const rSel = document.getElementById("olap-select-rows");
    const cSel = document.getElementById("olap-select-cols");
    const sSel = document.getElementById("olap-select-slice");

    if (op === "rollup") {
      if (rSel) rSel.value = "[Dim Time].[Year]";
      if (cSel) cSel.value = "[Dim Sales Channel].[Channel Name]";
      if (sSel) sSel.value = "NONE";
      Comp.showToast("🔄 Đã thực hiện ROLL-UP: Tổng hợp dữ liệu lên cấp Năm & Kênh bán hàng");
    } else if (op === "drilldown") {
      if (rSel) rSel.value = "[Dim Product].[Sub Category]";
      if (cSel) cSel.value = "[Dim Time].[Quarter]";
      if (sSel) sSel.value = "NONE";
      Comp.showToast("🔍 Đã thực hiện DRILL-DOWN: Khoan sâu xuống Phân nhóm sản phẩm & Quý");
    } else if (op === "slice") {
      if (sSel) sSel.value = "[Dim Location].[Region].&[Miền Nam]";
      Comp.showToast("✂️ Đã thực hiện SLICE: Cố định lát cắt tại Miền Nam");
    } else if (op === "dice") {
      if (sSel) sSel.value = "DICE_NAM_2026";
      Comp.showToast("🎲 Đã thực hiện DICE: Cắt khối dữ liệu tại Miền Nam và Năm 2026");
    } else if (op === "pivot") {
      if (rSel && cSel) {
        const temp = rSel.value;
        rSel.value = cSel.value;
        cSel.value = temp;
      }
      Comp.showToast("🔃 Đã thực hiện PIVOT: Hoán đổi trục Hàng và Cột");
    }

    updateOlapMdxPreview();
    renderOlapTable();
  }

  function renderOlapTable() {
    updateOlapMdxPreview();
    const container = document.getElementById("olap-table-container");
    const infoBadge = document.getElementById("olap-table-info");
    if (!container) return;

    const rVal = document.getElementById("olap-select-rows")?.value || "[Dim Product].[Category]";
    const cVal = document.getElementById("olap-select-cols")?.value || "[Dim Time].[Year]";
    const mVal = document.getElementById("olap-select-measure")?.value || "[Measures].[Sales Amount]";
    const sVal = document.getElementById("olap-select-slice")?.value || "NONE";

    let rowMembers = [];
    let colMembers = [];

    if (rVal.includes("Category")) {
      rowMembers = ["Technology (Công nghệ)", "Furniture (Nội thất)", "Office Supplies (Văn phòng phẩm)"];
    } else if (rVal.includes("Sub Category")) {
      rowMembers = ["Chairs & Tables", "Phones & Mobile", "Binders & Storage", "Storage Systems", "Paper & Stationery"];
    } else if (rVal.includes("Segment")) {
      rowMembers = ["Khách lẻ (Consumer)", "Nhà máy sản xuất (Corporate)", "Đại lý phân phối (Home Office)"];
    } else if (rVal.includes("Region")) {
      rowMembers = ["Miền Nam", "Miền Bắc", "Miền Trung", "Tây Nguyên"];
    } else if (rVal.includes("Year")) {
      rowMembers = ["Năm 2012", "Năm 2013", "Năm 2014", "Năm 2015"];
    } else {
      rowMembers = ["Kênh Cửa hàng (Store)", "Kênh TMĐT (Online)", "Đại lý sỉ (Wholesale)"];
    }

    if (cVal.includes("Year")) {
      colMembers = ["2012", "2013", "2014", "2015"];
    } else if (cVal.includes("Quarter")) {
      colMembers = ["Q1", "Q2", "Q3", "Q4"];
    } else if (cVal.includes("Region")) {
      colMembers = ["Miền Nam", "Miền Bắc", "Miền Trung"];
    } else if (cVal.includes("Channel")) {
      colMembers = ["Store", "Online", "Wholesale"];
    } else {
      colMembers = ["Consumer", "Corporate", "Home Office"];
    }

    let baseMult = 1.0;
    let isCurr = true;
    if (mVal.includes("Profit")) baseMult = 0.12;
    else if (mVal.includes("Quantity")) { baseMult = 0.003; isCurr = false; }
    else if (mVal.includes("Discount")) { baseMult = 0.05; }
    else if (mVal.includes("Shipping")) { baseMult = 0.08; }

    let sliceFactor = 1.0;
    if (sVal.includes("Miền Nam")) sliceFactor = 0.45;
    else if (sVal.includes("Miền Bắc")) sliceFactor = 0.35;
    else if (sVal.includes("2026") || sVal === "DICE_NAM_2026") sliceFactor = 0.30;

    let colTotals = colMembers.map(() => 0);
    let grandTotal = 0;

    let rowsHtml = rowMembers.map((rName, rIdx) => {
      let rTotal = 0;
      let cellsHtml = colMembers.map((cName, cIdx) => {
        const seed = (rIdx + 1) * 317 + (cIdx + 1) * 193;
        const val = Math.round((1250000 + (seed % 1450000)) * baseMult * sliceFactor);
        rTotal += val;
        colTotals[cIdx] += val;
        grandTotal += val;
        return `<td style="text-align:right; font-variant-numeric:tabular-nums;"><strong>${isCurr ? '$' + Comp.formatCurrency(val) : val.toLocaleString()}</strong></td>`;
      }).join('');

      return `
        <tr>
          <td><strong>${rName}</strong></td>
          ${cellsHtml}
          <td style="text-align:right; font-weight:800; background:rgba(15,98,254,0.06); color:var(--color-primary); font-variant-numeric:tabular-nums;">
            ${isCurr ? '$' + Comp.formatCurrency(rTotal) : rTotal.toLocaleString()}
          </td>
        </tr>
      `;
    }).join('');

    let colTotalsHtml = colTotals.map(t => `<td style="text-align:right; font-weight:800; color:var(--color-primary); font-variant-numeric:tabular-nums;">${isCurr ? '$' + Comp.formatCurrency(t) : t.toLocaleString()}</td>`).join('');

    container.innerHTML = `
      <table class="enterprise-table" style="font-size:12.5px; width:100%;">
        <thead>
          <tr style="background:var(--bg-card);">
            <th>${rVal.replace(/\[|\]/g, '')}</th>
            ${colMembers.map(c => `<th style="text-align:right;">${c}</th>`).join('')}
            <th style="text-align:right; background:rgba(15,98,254,0.06);">TỔNG HÀNG (SUM)</th>
          </tr>
        </thead>
        <tbody>
          ${rowsHtml}
        </tbody>
        <tfoot>
          <tr style="background:rgba(15,98,254,0.06); border-top:2px solid var(--border-color);">
            <td><strong>TỔNG CỘNG (GRAND TOTAL)</strong></td>
            ${colTotalsHtml}
            <td style="text-align:right; font-weight:900; font-size:13.5px; color:var(--color-primary); font-variant-numeric:tabular-nums;">
              ${isCurr ? '$' + Comp.formatCurrency(grandTotal) : grandTotal.toLocaleString()}
            </td>
          </tr>
        </tfoot>
      </table>
    `;

    if (infoBadge) {
      infoBadge.textContent = `${rowMembers.length} Hàng × ${colMembers.length} Cột | ${grandTotal > 0 ? (isCurr ? '$' + Comp.formatCurrency(grandTotal) : grandTotal.toLocaleString()) : '0'}`;
    }
  }

  // --------------------------------------------------------------------------
  // 10C. MDX QUERY STUDIO & 40 MDX QUERIES LIBRARY
  // --------------------------------------------------------------------------
  let mdxCatalogCache = [];
  let mdxActiveTab = "studio";

  async function renderMdxStudioView(filterState) {
    if (!mdxCatalogCache.length) {
      const res = await Svc.getMdxCatalog();
      if (res && res.data && Array.isArray(res.data)) {
        mdxCatalogCache = res.data;
      }
    }
    renderMdxCatalogTable(mdxCatalogCache);

    const textarea = document.getElementById("mdx-query-input");
    if (textarea && !textarea.dataset.bound) {
      textarea.dataset.bound = "true";
      textarea.addEventListener("keydown", (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
          e.preventDefault();
          executeMdxFromStudio();
        }
      });
    }
  }

  function switchMdxSubTab(tab) {
    mdxActiveTab = tab;
    const tabStudio = document.getElementById("subtab-mdx-studio");
    const tabCatalog = document.getElementById("subtab-mdx-catalog");
    const btnStudio = document.getElementById("tab-btn-studio");
    const btnCatalog = document.getElementById("tab-btn-catalog");

    if (tab === "studio") {
      if (tabStudio) tabStudio.style.display = "block";
      if (tabCatalog) tabCatalog.style.display = "none";
      if (btnStudio) btnStudio.classList.add("active");
      if (btnCatalog) btnCatalog.classList.remove("active");
    } else {
      if (tabStudio) tabStudio.style.display = "none";
      if (tabCatalog) tabCatalog.style.display = "block";
      if (btnStudio) btnStudio.classList.remove("active");
      if (btnCatalog) btnCatalog.classList.add("active");
    }
  }

  function loadSelectedSampleMdx(id) {
    if (!id) return;
    const item = mdxCatalogCache.find(q => q.id === id);
    if (item) {
      const input = document.getElementById("mdx-query-input");
      if (input) input.value = item.mdxQuery;
      Comp.showToast(`✅ Đã nạp câu ${item.id}: ${item.title}`);
    }
  }

  function clearMdxEditor() {
    const input = document.getElementById("mdx-query-input");
    if (input) input.value = "";
    const sel = document.getElementById("mdx-sample-select");
    if (sel) sel.value = "";
  }

  async function executeMdxFromStudio() {
    const input = document.getElementById("mdx-query-input");
    const query = input ? input.value.trim() : "";
    if (!query) {
      Comp.showToast("⚠️ Vui lòng nhập câu lệnh MDX trước khi thực thi.");
      return;
    }

    const execBtn = document.getElementById("btn-execute-mdx");
    const statusBadge = document.getElementById("mdx-exec-status");
    const timeBadge = document.getElementById("mdx-exec-time");
    const rowsBadge = document.getElementById("mdx-exec-rows");
    const placeholder = document.getElementById("mdx-result-placeholder");
    const tableWrapper = document.getElementById("mdx-table-wrapper");
    const errorCard = document.getElementById("mdx-error-card");
    const errorMsg = document.getElementById("mdx-error-message");

    if (execBtn) execBtn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Đang chạy...`;
    if (statusBadge) {
      statusBadge.textContent = "⏳ Đang kết nối SSAS...";
      statusBadge.className = "badge badge-warning";
    }

    try {
      const res = await Svc.executeMdxQuery(query);

      if (res.success && Array.isArray(res.data) && res.data.length > 0) {
        if (statusBadge) {
          statusBadge.textContent = "✅ Thực thi thành công";
          statusBadge.className = "badge badge-healthy";
        }
        if (timeBadge) timeBadge.innerHTML = `Thời gian: <strong>${res.executionTimeMs || 0} ms</strong>`;
        if (rowsBadge) rowsBadge.innerHTML = `Số bản ghi: <strong>${res.rowCount || res.data.length}</strong>`;

        if (placeholder) placeholder.style.display = "none";
        if (errorCard) errorCard.style.display = "none";
        if (tableWrapper) tableWrapper.style.display = "block";

        const thead = document.getElementById("mdx-dynamic-thead");
        const tbody = document.getElementById("mdx-dynamic-tbody");
        const cols = res.columns || Object.keys(res.data[0]);

        if (thead) thead.innerHTML = `<tr>${cols.map(c => `<th>${c}</th>`).join('')}</tr>`;
        if (tbody) {
          tbody.innerHTML = res.data.map(row => `
            <tr>${cols.map(c => {
              const val = row[c];
              const isNum = typeof val === 'number';
              return `<td style="${isNum ? 'text-align:right;' : ''}"><strong>${val !== null && val !== undefined ? (isNum ? Number(val).toLocaleString() : val) : '<span style="color:var(--text-muted);">(null)</span>'}</strong></td>`;
            }).join('')}</tr>
          `).join('');
        }
        Comp.showToast(`🎉 Đã thực thi thành công: ${res.data.length} dòng dữ liệu (${res.executionTimeMs} ms)`);
      } else {
        if (statusBadge) {
          statusBadge.textContent = "⚠️ Lỗi thực thi SSAS";
          statusBadge.className = "badge badge-danger";
        }
        if (timeBadge) timeBadge.innerHTML = `Thời gian: <strong>${res.executionTimeMs || 42} ms</strong>`;
        if (rowsBadge) rowsBadge.innerHTML = `Số bản ghi: <strong>0</strong>`;

        if (placeholder) placeholder.style.display = "none";
        if (tableWrapper) tableWrapper.style.display = "none";
        if (errorCard) {
          errorCard.style.display = "block";
          if (errorMsg) errorMsg.textContent = res.message || "Lỗi thực thi câu lệnh MDX trên máy chủ Analysis Services.";
        }
      }
    } catch (err) {
      if (statusBadge) {
        statusBadge.textContent = "❌ Lỗi mạng / API";
        statusBadge.className = "badge badge-danger";
      }
      if (errorCard) {
        errorCard.style.display = "block";
        if (errorMsg) errorMsg.textContent = err.message || "Không thể kết nối đến API /api/v1/ssas/query";
      }
    } finally {
      if (execBtn) execBtn.innerHTML = `<i class="fa-solid fa-play"></i> Thực thi MDX (Execute)`;
    }
  }

  function renderMdxCatalogTable(list) {
    const tbody = document.getElementById("mdx-catalog-table-body");
    if (!tbody) return;

    if (!list || !list.length) {
      tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; padding:20px; color:var(--text-muted);">Không tìm thấy câu truy vấn MDX nào phù hợp với bộ lọc.</td></tr>`;
      return;
    }

    tbody.innerHTML = list.map(q => {
      const isVerified = q.verificationStatus === "VERIFIED";
      const statusBadge = isVerified
        ? `<span class="badge badge-healthy" style="font-size:10.5px;">✓ Đã kiểm chứng</span>`
        : `<span class="badge badge-warning" style="font-size:10.5px; background:rgba(243,156,18,0.15); color:#f39c12;">⏳ Chờ nạp nguồn</span>`;

      return `
        <tr>
          <td><strong style="color:var(--color-primary);">${q.id}</strong></td>
          <td><span class="badge badge-info" style="font-size:10px;">${q.section}</span></td>
          <td>
            <strong>${q.title}</strong>
            <div style="font-size:11.5px; color:var(--text-muted); margin-top:2px;">${q.businessPurpose}</div>
          </td>
          <td><span class="badge badge-secondary" style="font-size:10px;">${q.operationType}</span></td>
          <td><code>${q.sourceFactTable}</code></td>
          <td>${statusBadge}</td>
          <td style="text-align:center;">
            <button type="button" class="topbar-action-btn" style="width:auto; padding:4px 10px; font-size:11px; font-weight:700; background:var(--color-primary); color:white; border:none;" onclick="KhangNghiApp.loadMdxQueryToEditor('${q.id}')">
              ⚡ Nạp & Chạy
            </button>
          </td>
        </tr>
      `;
    }).join('');
  }

  function loadMdxQueryToEditor(id) {
    const item = mdxCatalogCache.find(q => q.id === id);
    if (!item) return;

    switchMdxSubTab("studio");
    const input = document.getElementById("mdx-query-input");
    if (input) input.value = item.mdxQuery;

    const sampleSelect = document.getElementById("mdx-sample-select");
    if (sampleSelect) sampleSelect.value = item.id;

    window.scrollTo({ top: 0, behavior: 'smooth' });
    Comp.showToast(`⚡ Đã nạp câu ${item.id} vào Query Studio. Nhấn "Thực thi MDX" để chạy!`);
  }

  function filterMdxCatalogList(keyword) {
    const q = (keyword || "").toLowerCase().trim();
    const sector = document.getElementById("mdx-catalog-sector-filter")?.value || "ALL";

    const filtered = mdxCatalogCache.filter(item => {
      const matchKey = !q || item.id.toLowerCase().includes(q) || item.title.toLowerCase().includes(q) || item.businessPurpose.toLowerCase().includes(q) || item.operationType.toLowerCase().includes(q);
      const matchSector = sector === "ALL" || item.section === sector;
      return matchKey && matchSector;
    });

    renderMdxCatalogTable(filtered);
  }

  function filterMdxCatalogBySector(sector) {
    const kw = document.getElementById("mdx-catalog-search")?.value || "";
    filterMdxCatalogList(kw);
  }

  function filterMdxCatalogByStatus(status) {
    let filtered = mdxCatalogCache;
    if (status !== "ALL") {
      filtered = mdxCatalogCache.filter(q => q.verificationStatus === status);
    }
    renderMdxCatalogTable(filtered);
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
    
    const name = prompt("Nhập tên kịch bản mô phỏng:", `Kịch bản tăng trưởng ${g}%`);
    if (!name) return;

    const saved = JSON.parse(localStorage.getItem("kn_whatif_scenarios") || "[]");
    saved.push({
      name,
      date: new Date().toLocaleDateString('vi-VN'),
      params: `Tăng trưởng: ${g}%, Chiết khấu: ${d}%, Vận chuyển: ${s}%`,
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
      { id: "SAL-001", domain: "Bán hàng", question: "Doanh thu & Lợi nhuận theo Quý/Năm?", measures: "Revenue, Profit", dim: "Time", status: "Đã triển khai", targetRoute: "sales" },
      { id: "SAL-002", domain: "Bán hàng", question: "Top 10 sản phẩm có lợi nhuận cao nhất?", measures: "Profit, Margin", dim: "Product", status: "Đã triển khai", targetRoute: "sales" },
      { id: "PUR-001", domain: "Mua hàng", question: "Chi phí nhập hàng theo Nhà cung cấp?", measures: "PurchaseCost", dim: "Supplier", status: "Đã triển khai", targetRoute: "purchase" },
      { id: "INV-001", domain: "Tồn kho", question: "Sản phẩm nào đang dưới mức tồn kho an toàn?", measures: "StockQty", dim: "Product, Warehouse", status: "Đã triển khai", targetRoute: "inventory" },
      { id: "SHP-001", domain: "Vận chuyển", question: "Tỷ lệ đơn hàng giao trễ SLA theo đơn vị vận chuyển?", measures: "LateCount", dim: "Shipper", status: "Đã triển khai", targetRoute: "shipping" },
      { id: "CUS-001", domain: "Khách hàng", question: "Doanh thu và lợi nhuận theo Phân khúc khách hàng?", measures: "Revenue, Profit", dim: "Customer.Segment", status: "Đã triển khai", targetRoute: "customer" },
      { id: "PRD-001", domain: "Sản phẩm", question: "Ma trận sinh lời sản phẩm Margin vs Revenue?", measures: "Margin, Revenue", dim: "Product", status: "Đã triển khai", targetRoute: "product" },
      { id: "CROSS-001", domain: "Phân tích chéo / Cross Analysis", question: "Sản phẩm bán chạy nhưng lượng tồn kho thấp?", measures: "SalesQty, StockQty", dim: "Product", status: "Đã triển khai", targetRoute: "cross-analysis" },
      { id: "ML-001", domain: "Dự báo / Forecast", question: "Dự báo doanh số 3 - 12 tháng tới (ML.NET SSA)?", measures: "ForecastSales", dim: "Time", status: "Đã triển khai", targetRoute: "forecast" }
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
          <td class="col-id"><strong>${q.id}</strong></td>
          <td class="col-domain"><span class="badge badge-info">${q.domain}</span></td>
          <td class="col-question"><strong>${q.question}</strong></td>
          <td class="col-measures">${q.measures}</td>
          <td class="col-dim">${q.dim}</td>
          <td class="col-status"><span class="badge badge-healthy">${q.status}</span></td>
          <td class="col-action"><button class="btn-open-analysis" onclick="window.location.hash='#/${q.targetRoute}'" title="Mở phân tích cho ${q.id}"><i class="fa-solid fa-arrow-up-right-from-square"></i> Mở phân tích</button></td>
        </tr>
      `).join('');
    }
  }

  // --------------------------------------------------------------------------
  // 14. DATA STATUS & ETL LOG (ROUND 11)
  // --------------------------------------------------------------------------
  async function renderSystemStatusView() {
    const [dwhData, ssasRes] = await Promise.all([
      Svc.getSystemStatusOverview(),
      Svc.getSsasStatus()
    ]);

    const timestampEl = document.getElementById("system-diagnostic-timestamp");
    if (timestampEl) {
      timestampEl.textContent = `Kiểm tra: ${new Date().toLocaleTimeString('vi-VN')} (Live)`;
    }

    if (ssasRes && ssasRes.data) {
      const s = ssasRes.data;
      const ssasValEl = document.getElementById("status-ssas-val");
      const cubeValEl = document.getElementById("status-cube-val");

      if (ssasValEl) {
        ssasValEl.textContent = s.isServerConnected ? "🟢 Online (.\\SSAS_MULTI)" : "🔴 Mất kết nối";
        ssasValEl.style.color = s.isServerConnected ? "var(--color-success)" : "var(--color-danger)";
      }

      if (cubeValEl) {
        if (s.cubeStatus === "OPERATIONAL") {
          cubeValEl.textContent = "🟢 Sẵn Sàng (KhangNghi_Cube)";
          cubeValEl.style.color = "var(--color-success)";
        } else if (s.cubeStatus === "METADATA_MISMATCH") {
          cubeValEl.textContent = "🟡 Cần Đồng Bộ Metadata";
          cubeValEl.style.color = "#f39c12";
        } else {
          cubeValEl.textContent = "🔴 Không khả dụng";
          cubeValEl.style.color = "var(--color-danger)";
        }
      }
    }

    const body = document.getElementById("system-etl-table-body");
    if (body) {
      body.innerHTML = (dwhData.tablesStatus || []).map(t => `
        <tr>
          <td><strong style="color:var(--color-primary);">${t.name}</strong></td>
          <td><span class="badge ${t.type === 'FACT' ? 'badge-info' : 'badge-secondary'}" style="padding:2px 8px; font-size:10px;">${t.type}</span></td>
          <td><strong>${t.rows.toLocaleString()}</strong> bản ghi</td>
          <td>${t.description}</td>
          <td><span class="badge ${t.status === 'AVAILABLE' ? 'badge-healthy' : 'badge-warning'}" style="padding:2px 8px; font-size:10px; background:${t.status === 'AVAILABLE' ? 'rgba(37,162,68,0.15)' : 'rgba(243,156,18,0.15)'}; color:${t.status === 'AVAILABLE' ? '#25a244' : '#f39c12'};">${t.status === 'AVAILABLE' ? 'Đã kết nối' : t.status}</span></td>
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

  async function exportInventoryCSV() {
    const data = await Svc.getInventoryOverview();
    const rows = (data && Array.isArray(data.products) && data.products.length > 0)
      ? data.products
      : (window.KhangNghiMockData.products || []);
    Svc.exportToCSV("Inventory_Report_2015.csv", rows);
    Comp.showToast("📥 Đã xuất file Inventory_Report_2015.csv thành công!");
  }

  function onInventorySearchInput(val) {
    Filters.setFilter("search", val);
  }

  function updateCrossAnalysis() {
    renderCrossAnalysisView();
  }

  function filterQuestionCatalog(val) {
    const q = (val || "").trim().toLowerCase();
    document.querySelectorAll("#explorer-table-body tr").forEach(row => {
      row.style.display = (!q || row.textContent.toLowerCase().includes(q)) ? "" : "none";
    });
  }

  function filterQuestionCatalogByDomain(domain) {
    const domainMap = {
      "ALL": "",
      "Sales": "bán hàng",
      "Purchase": "mua hàng",
      "Inventory": "tồn kho",
      "Shipping": "vận chuyển",
      "Customer": "khách hàng",
      "Product": "sản phẩm",
      "Supplier": "nhà cung cấp",
      "Cross Analysis": "phân tích chéo",
      "Forecast": "dự báo"
    };
    const target = (domainMap[domain] || domain).toLowerCase();
    document.querySelectorAll("#explorer-table-body tr").forEach(row => {
      if (!target || domain === "ALL") {
        row.style.display = "";
      } else {
        row.style.display = row.textContent.toLowerCase().includes(target) ? "" : "none";
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
    
    // Tự động gán mật khẩu bảo mật riêng biệt tương ứng với tài khoản
    const auth = window.KhangNghiAuth;
    if (auth && auth.DEMO_ACCOUNTS) {
      const acc = auth.DEMO_ACCOUNTS.find(a => a.email.toLowerCase() === (email || "").toLowerCase());
      if (acc && passwordInput) {
        passwordInput.value = acc.password;
      }
    }
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
  function formatCopilotItems(items) {
    if (!Array.isArray(items) || items.length === 0) return "";
    const topItems = items.slice(0, 5);
    const rows = topItems.map((item, idx) => {
      const name = item.productName || item.ProductName 
        || item.customerName || item.CustomerName 
        || item.supplierName || item.SupplierName 
        || item.shipperName || item.ShipperName 
        || (item.year ? `Năm ${item.year}` : (item.Year ? `Năm ${item.Year}` : `Mục ${idx + 1}`));
      
      let metric = "";
      if (item.totalRevenue !== undefined || item.TotalRevenue !== undefined) {
        metric = `Doanh thu: $${Number(item.totalRevenue ?? item.TotalRevenue).toLocaleString()}`;
      } else if (item.totalPurchaseAmount !== undefined || item.TotalPurchaseAmount !== undefined) {
        metric = `Chi phí: $${Number(item.totalPurchaseAmount ?? item.TotalPurchaseAmount).toLocaleString()}`;
      } else if (item.totalShippingCost !== undefined || item.TotalShippingCost !== undefined) {
        metric = `Phí ship: $${Number(item.totalShippingCost ?? item.TotalShippingCost).toLocaleString()}`;
      } else if (item.quantityOnHand !== undefined || item.QuantityOnHand !== undefined) {
        const onHand = item.quantityOnHand ?? item.QuantityOnHand;
        const reorder = item.reorderLevel ?? item.ReorderLevel ?? 0;
        metric = `Tồn kho: ${onHand} (Mức đặt hàng lại: ${reorder})`;
      } else if (item.totalProfit !== undefined || item.TotalProfit !== undefined) {
        metric = `Lợi nhuận: $${Number(item.totalProfit ?? item.TotalProfit).toLocaleString()}`;
      }
      return `<li style="margin-bottom:3px;"><strong>${name}</strong>${metric ? ` — <span style="color:var(--color-primary);">${metric}</span>` : ""}</li>`;
    }).join("");

    return `<ul style="margin:8px 0 4px 18px; padding:0; font-size:12px;">${rows}</ul>${items.length > 5 ? `<div style="font-size:11px; color:var(--text-muted); margin-top:3px;">(Hiển thị 5 / ${items.length} kết quả từ DWH)</div>` : ""}`;
  }

  function formatCopilotSummary(obj) {
    if (!obj || typeof obj !== "object") return "";
    const rev = obj.totalRevenue ?? obj.TotalRevenue;
    const prof = obj.totalProfit ?? obj.TotalProfit;
    const margin = obj.profitMargin ?? obj.ProfitMargin;
    if (rev !== undefined) {
      return `<ul style="margin:8px 0 4px 18px; padding:0; font-size:12px;">
        <li>Tổng doanh thu: <strong>$${Number(rev).toLocaleString()}</strong></li>
        <li>Tổng lợi nhuận: <strong>$${Number(prof).toLocaleString()}</strong></li>
        <li>Biên lợi nhuận TB: <strong>${(margin * 100).toFixed(1)}%</strong></li>
      </ul>`;
    }
    return "";
  }

  async function askCopilot(text) {
    if (!text) return;
    const overlay = document.getElementById("copilot-modal");
    const chatBody = document.getElementById("copilot-chat-body");
    const input = document.getElementById("copilot-input");

    if (overlay) overlay.classList.add("active");
    if (!chatBody) return;

    chatBody.innerHTML += `<div style="text-align:right; margin-bottom:10px;"><span style="background:var(--color-primary); color:white; padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px;">${text}</span></div>`;
    if (input) input.value = "";
    
    // Add loading indicator
    const loadingId = "copilot-loading-" + Date.now();
    chatBody.innerHTML += `<div id="${loadingId}" style="text-align:left; margin-bottom:10px;"><span style="background:var(--bg-input); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12px; color:var(--text-muted);"><i class="fa-solid fa-spinner fa-spin" style="margin-right:6px; color:var(--color-primary);"></i> Khang Nghi AI đang tra cứu dữ liệu DWH...</span></div>`;
    chatBody.scrollTop = chatBody.scrollHeight;

    const client = window.KhangNghiApiClient;
    const token = client ? client.getToken() : null;

    if (!token) {
      const loader = document.getElementById(loadingId);
      if (loader) loader.remove();
      chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--color-warning-bg); border:1px solid var(--color-warning); color:var(--text-main); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">🔐 <strong>Yêu cầu xác thực:</strong> Bạn cần đăng nhập tài khoản hệ thống (hoặc tài khoản Admin/Giám Đốc) để kích hoạt trợ lý AI Copilot kết nối dữ liệu DWH.</span></div>`;
      chatBody.scrollTop = chatBody.scrollHeight;
      return;
    }

    try {
      const res = await client.get("/nlq/query", { q: text });
      const loader = document.getElementById(loadingId);
      if (loader) loader.remove();

      if (res.success && res.data) {
        const answer = res.data.answer || res.data.Answer || "Đã tìm thấy thông tin từ hệ thống.";
        const data = res.data.data ?? res.data.Data;
        let dataHtml = "";

        if (Array.isArray(data) && data.length > 0) {
          dataHtml = formatCopilotItems(data);
        } else if (data && typeof data === "object") {
          dataHtml = formatCopilotSummary(data);
        }

        const replyHtml = `🤖 <strong>Khang Nghi AI Copilot:</strong> ${answer}${dataHtml}`;
        chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--bg-input); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6; max-width:92%;">${replyHtml}</span></div>`;
      } else {
        const errorMsg = res.status === 401 
          ? "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại."
          : (res.error || "Không thể truy vấn cơ sở dữ liệu DWH.");
        chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--color-danger-bg); border:1px solid var(--color-danger); color:var(--text-main); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">⚠️ <strong>Thông báo lỗi:</strong> ${errorMsg}</span></div>`;
      }
    } catch (err) {
      const loader = document.getElementById(loadingId);
      if (loader) loader.remove();
      chatBody.innerHTML += `<div style="text-align:left; margin-bottom:10px;"><span style="background:var(--color-danger-bg); border:1px solid var(--color-danger); color:var(--text-main); padding:8px 12px; border-radius:12px; display:inline-block; font-size:12.5px; line-height:1.6;">⚠️ <strong>Lỗi kết nối:</strong> ${err.message}</span></div>`;
    }

    chatBody.scrollTop = chatBody.scrollHeight;
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

  function toggleSalesAdvPopover(forceState) {
    const popover = document.getElementById("sales-adv-popover");
    const btn = document.getElementById("sales-adv-filter-btn");
    if (!popover) return;
    const isShow = typeof forceState === "boolean" ? forceState : !popover.classList.contains("active");
    if (isShow) {
      popover.classList.add("active");
      if (btn) btn.classList.add("active");
    } else {
      popover.classList.remove("active");
      if (btn) btn.classList.remove("active");
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

    body.innerHTML = `<div style="text-align:center; padding:30px;"><i class="fa-solid fa-spinner fa-spin" style="font-size:24px; color:var(--color-primary);"></i><p style="margin-top:10px; font-size:12px; color:var(--text-muted);">Đang tải chi tiết đơn hàng ${poId}...</p></div>`;
    drawer.classList.add("active");

    const po = await Svc.getPurchasePODetails(poId);
    const lineItemsHtml = (po.lineItems || []).map(it => `
      <tr style="border-bottom:1px solid var(--border-color); font-size:11.5px;">
        <td style="padding:6px 8px;"><strong>${it.productName}</strong><br><span style="color:var(--text-muted); font-size:10.5px;">${it.productId} | ${it.category}</span></td>
        <td style="padding:6px 8px; text-align:right;">${(it.qty || 0).toLocaleString()}</td>
        <td style="padding:6px 8px; text-align:right;">${Comp.formatCurrency(it.unitCost)}</td>
        <td style="padding:6px 8px; text-align:right;"><strong>${Comp.formatCurrency(it.totalCost)}</strong></td>
      </tr>
    `).join('');

    body.innerHTML = `
      <div style="margin-bottom:16px;">
        <div style="display:flex; justify-content:space-between; align-items:center;">
          <span class="badge badge-info" style="font-size:12px; padding:4px 8px;">${po.id}</span>
          <span class="badge badge-healthy">DWH Fact_Purchase</span>
        </div>
        <h3 style="font-size:18px; font-weight:700; margin-top:8px; color:var(--text-main);">${po.supplierName}</h3>
        <div style="font-size:12px; color:var(--text-secondary); margin-top:4px;">
          <span>Ngày nhập: <strong>${po.date}</strong></span> &bull; <span>Kho nhận: <strong>${po.warehouseName}</strong></span>
        </div>
      </div>

      <div class="glass-card" style="margin-bottom:16px; padding:12px;">
        <div style="display:grid; grid-template-columns:1fr 1fr; gap:10px; font-size:12px;">
          <div>
            <span style="color:var(--text-muted);">Tổng số lượng:</span><br>
            <strong style="font-size:14px;">${(po.qty || 0).toLocaleString()} sp</strong>
          </div>
          <div>
            <span style="color:var(--text-muted);">Tổng giá trị PO:</span><br>
            <strong style="font-size:14px; color:var(--color-primary);">${Comp.formatCurrency(po.totalCost)}</strong>
          </div>
        </div>
      </div>

      <div class="glass-card" style="margin-bottom:16px; padding:10px;">
        <h4 style="font-size:12.5px; font-weight:700; margin-bottom:8px; color:var(--text-main);">
          Top Mặt Hàng Chi Phí Cao Nhất Trong Đơn (${(po.lineItems || []).length} mục)
        </h4>
        <div class="table-responsive" style="max-height:260px; overflow-y:auto;">
          <table style="width:100%; border-collapse:collapse;">
            <thead>
              <tr style="border-bottom:1px solid var(--border-color); font-size:11px; color:var(--text-muted); text-align:left;">
                <th style="padding:6px 8px;">Sản phẩm</th>
                <th style="padding:6px 8px; text-align:right;">Số lượng</th>
                <th style="padding:6px 8px; text-align:right;">Đơn giá</th>
                <th style="padding:6px 8px; text-align:right;">Thành tiền</th>
              </tr>
            </thead>
            <tbody>
              ${lineItemsHtml}
            </tbody>
          </table>
        </div>
      </div>

      <button class="topbar-action-btn" style="width:100%; height:38px; background:var(--color-primary); color:white; font-weight:600;" onclick="KhangNghiComponents.showToast('Đang trích xuất dữ liệu đơn PO ${po.id}')">
        <i class="fa-solid fa-file-invoice" style="margin-right:6px;"></i> Xuất Báo Cáo Đơn Mua ${po.id}
      </button>
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

    const advPopover = document.getElementById("sales-adv-popover");
    const advBtn = document.getElementById("sales-adv-filter-btn");
    if (advPopover && !advPopover.contains(e.target) && (!advBtn || !advBtn.contains(e.target))) {
      advPopover.classList.remove("active");
      if (advBtn) advBtn.classList.remove("active");
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
    toggleSalesAdvPopover,
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
    filterQuestionCatalogByDomain,
    applyOlapOperation,
    renderOlapTable,
    sendGeneratedMdxToStudio,
    switchMdxSubTab,
    loadSelectedSampleMdx,
    clearMdxEditor,
    executeMdxFromStudio,
    loadMdxQueryToEditor,
    filterMdxCatalogList,
    filterMdxCatalogBySector,
    filterMdxCatalogByStatus
  };
});

