/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - MAIN APPLICATION CONTROLLER
   Điều phối khung ứng dụng, hiển thị từng trang và các tương tác.
   Dữ liệu: window.analyticsService (mọi khối có state ready/empty/error/forbidden)
   ========================================================================== */

document.addEventListener("DOMContentLoaded", function () {
  const Svc = window.analyticsService;
  const Comp = window.KhangNghiComponents;
  const Charts = window.KhangNghiCharts;
  const Filters = window.KhangNghiFilters;
  const Router = window.KhangNghiRouter;
  const esc = Comp.escapeHtml;
  const CUR = Comp.SALES_CURRENCY;          // tiền bán hàng, vận chuyển (cấu hình KN_CONFIG)
  const CUR_LABEL = Comp.SALES_CURRENCY_LABEL;
  const fv = Comp.formatValue;

  // Các trang có dữ liệu áp dụng bộ lọc năm của API
  const YEAR_ROUTES = ["overview", "sales", "purchase"];
  const ALL_TIME = "Toàn bộ giai đoạn";

  let renderSeq = 0;
  const tables = {};          // dữ liệu bảng đang hiển thị: phục vụ tìm kiếm và xuất CSV
  let mdxCatalog = null;      // dùng chung cho MDX Studio và Câu hỏi phân tích
  let forecastHorizon = 6;
  let whatIfBase = null;

  Filters.init();
  Router.init();
  initShell();
  renderCurrentView();
  checkConnection();

  Filters.onChange(() => renderCurrentView());
  Router.onRouteChanged(() => renderCurrentView());
  if (window.KhangNghiAuth) window.KhangNghiAuth.onAuthStateChanged(user => { if (user) checkConnection(); });

  // --------------------------------------------------------------------------
  // ĐIỀU PHỐI
  // --------------------------------------------------------------------------
  async function renderCurrentView() {
    const route = Router.getCurrentRoute();
    const auth = window.KhangNghiAuth;
    if (!auth || !auth.isAuthenticated() || route === "login") return;

    const seq = ++renderSeq;
    const filters = Filters.getState();
    const isCurrent = () => seq === renderSeq;

    const yearBox = document.getElementById("topbar-year");
    if (yearBox) yearBox.hidden = !YEAR_ROUTES.includes(route);
    const yearSel = document.getElementById("filter-year");
    if (yearSel) yearSel.value = filters.year;
    Filters.renderChips();

    const renderers = {
      "overview": renderOverview,
      "sales": renderSales,
      "purchase": renderPurchase,
      "inventory": renderInventory,
      "shipping": renderShipping,
      "customer": renderCustomers,
      "product": renderProducts,
      "supplier": renderSuppliers,
      "geography": renderGeography,
      "cross-analysis": renderCrossAnalysis,
      "olap-explorer": renderOlap,
      "mdx-studio": renderMdxStudio,
      "forecast": renderForecast,
      "what-if": renderWhatIf,
      "question-explorer": renderQuestionExplorer,
      "system-status": renderSystemStatus,
      "etl-logs": renderEtlLogs,
      "settings": renderSettings
    };
    const fn = renderers[route];
    if (!fn) return;
    try {
      await fn(filters, isCurrent);
    } catch (err) {
      console.error(`[render ${route}]`, err);
      if (isCurrent()) gate(route, { state: "error", message: "Lỗi hiển thị trang: " + err.message });
    }
  }

  function refreshView() {
    checkConnection();
    renderCurrentView();
  }

  // --------------------------------------------------------------------------
  // TIỆN ÍCH HIỂN THỊ
  // --------------------------------------------------------------------------
  function setScope(key, unit, sec, filters) {
    let text = unit ? `${unit} · ` : "";
    text += sec && sec.scope ? sec.scope : ALL_TIME;
    if (filters && filters.year !== "ALL" && sec && sec.scope === ALL_TIME) text += " (API chưa hỗ trợ lọc năm)";
    document.querySelectorAll(`[data-scope="${key}"]`).forEach(el => { el.textContent = text; });
  }

  function setLoading(ids) {
    ids.forEach(id => {
      Charts.destroy(id);
      const el = document.getElementById(id);
      if (el) el.innerHTML = Comp.renderState("loading", { compact: true });
    });
  }

  function setKpis(id, html) {
    const el = document.getElementById(id);
    if (el) el.innerHTML = html;
  }

  // Vẽ một khối nếu sẵn sàng; ngược lại hiển thị đúng trạng thái của khối
  function draw(id, sec, painter, emptyMessage) {
    Charts.destroy(id);
    if (Comp.applySectionState(id, sec, emptyMessage)) {
      const res = painter();
      if (res === null) {
        const el = document.getElementById(id);
        if (el) el.innerHTML = Comp.renderState("empty", { message: emptyMessage || "Không có giá trị để biểu diễn.", compact: true });
      }
    }
  }

  // Khối dữ liệu chính của trang không sẵn sàng -> một thông báo duy nhất cho cả trang
  function gate(route, sec, { title = "", emptyMessage = "", filters = null } = {}) {
    const view = document.getElementById(`view-${route}`);
    if (!view) return;
    let box = view.querySelector(".page-gate");
    if (!sec || sec.state === "ready") {
      view.classList.remove("is-gated");
      if (box) box.innerHTML = "";
      return;
    }
    if (!box) {
      box = document.createElement("div");
      box.className = "page-gate";
      const header = view.querySelector(".page-header");
      header ? header.after(box) : view.prepend(box);
    }
    const actions = [];
    if (sec.state === "empty" && filters && filters.year !== "ALL") {
      actions.push(`<button type="button" class="btn" onclick="KhangNghiFilters.setFilter('year','ALL')">Xem tất cả năm</button>`);
    }
    if (sec.state === "error") actions.push(`<button type="button" class="btn" onclick="KhangNghiApp.refreshView()"><i class="fa-solid fa-rotate"></i> Thử lại</button>`);
    if (sec.state !== "forbidden" && canOpen("system-status")) actions.push(`<a class="btn btn-ghost" href="#/system-status">Trạng thái dữ liệu</a>`);
    box.innerHTML = Comp.renderState(sec.state, {
      title: sec.state === "empty" ? title : "",
      message: sec.state === "empty" ? emptyMessage : sec.message,
      actions: actions.join("")
    });
    view.classList.add("is-gated");
  }

  function yearText(filters) {
    return filters.year === "ALL" ? "" : ` trong năm ${filters.year}`;
  }

  function canOpen(route) {
    const auth = window.KhangNghiAuth;
    const perm = Router.ROUTE_PERMISSIONS ? Router.ROUTE_PERMISSIONS[route] : null;
    return !!auth && (!perm || auth.hasPermission(perm));
  }

  // Bảng có lưu trạng thái để tìm kiếm và xuất CSV đúng các dòng đang xem
  function mountTable(name, containerId, columns, rows, opts = {}) {
    tables[name] = { containerId, columns, rows, opts, query: "" };
    paintTable(name);
  }

  function paintTable(name) {
    const t = tables[name];
    if (!t) return;
    const el = document.getElementById(t.containerId);
    if (!el) return;
    const q = t.query.trim().toLowerCase();
    const rows = q ? t.rows.filter(r => t.columns.some(c => String(r[c.key] ?? "").toLowerCase().includes(q))) : t.rows;
    const limit = t.opts.limit || 0;
    const shown = limit ? rows.slice(0, limit) : rows;
    let html = Comp.renderTable(t.columns, shown, { onRowClick: t.opts.onRowClick, rowKey: t.opts.rowKey, empty: q ? "Không có dòng khớp từ khóa." : t.opts.empty });
    if (limit && rows.length > limit) html += `<p class="table-note">Hiển thị ${limit.toLocaleString("vi-VN")} / ${rows.length.toLocaleString("vi-VN")} dòng. Dùng ô tìm kiếm để thu hẹp, hoặc xuất CSV để lấy toàn bộ.</p>`;
    el.innerHTML = html;
  }

  function filterTable(name, query) {
    if (!tables[name]) return;
    tables[name].query = query || "";
    paintTable(name);
  }

  function exportTable(name) {
    const t = tables[name];
    if (!t || !t.rows.length) {
      Comp.showToast("Không có dữ liệu để xuất.", "warning");
      return;
    }
    const q = t.query.trim().toLowerCase();
    const rows = q ? t.rows.filter(r => t.columns.some(c => String(r[c.key] ?? "").toLowerCase().includes(q))) : t.rows;
    const cols = t.columns.filter(c => c.key).map(c => ({ key: c.key, label: c.label }));
    Svc.exportToCSV(`KhangNghi_${name}_${new Date().toISOString().slice(0, 10)}.csv`, rows, cols);
    Comp.showToast(`Đã xuất ${rows.length.toLocaleString("vi-VN")} dòng ra CSV.`);
  }

  const kpi = Comp.renderKpiCard;

  // --------------------------------------------------------------------------
  // 1. TỔNG QUAN
  // --------------------------------------------------------------------------
  async function renderOverview(filters, isCurrent) {
    setKpis("overview-kpis", Comp.renderSkeletonCards(6));
    setLoading(["chart-ov-trend", "chart-ov-category", "chart-ov-region"]);
    const d = await Svc.getOverview(filters);
    if (!isCurrent()) return;

    gate("overview", d.summary, { title: "Chưa có dữ liệu bán hàng", emptyMessage: `Bảng Fact_Sales chưa có giao dịch${yearText(filters)}. Nạp dữ liệu bằng quy trình ETL rồi tải lại trang.`, filters });
    if (d.summary.state !== "ready") return;

    const s = d.summary.data;
    const note = d.summary.scope;
    setKpis("overview-kpis",
      kpi({ label: "Doanh thu", value: s.revenue, format: CUR, icon: "fa-sack-dollar", change: d.change.revenue, note }) +
      kpi({ label: "Lợi nhuận", value: s.profit, format: CUR, icon: "fa-arrow-trend-up", change: d.change.profit, note }) +
      kpi({ label: "Số đơn hàng", value: s.orders, icon: "fa-receipt", note }) +
      kpi({ label: "Biên lợi nhuận", value: s.margin, format: "percent", icon: "fa-percent", note, hint: "Lợi nhuận / Doanh thu" }) +
      kpi({ label: "Khách hàng", value: s.customers, icon: "fa-users", note: "Có phát sinh đơn hàng", hint: "Số khách hàng có đơn hàng trong kỳ (đếm gần đúng)" }) +
      kpi({ label: "Số lượng bán", value: s.quantity, icon: "fa-box", change: d.change.quantity, note })
    );

    setScope("ov-trend", CUR_LABEL, d.byYear, filters);
    setScope("ov-category", `${CUR_LABEL}, doanh thu`, d.byCategory, filters);
    setScope("ov-region", CUR_LABEL, d.byRegion, filters);
    draw("chart-ov-trend", d.byYear, () => Charts.line("chart-ov-trend", d.byYear.items.map(y => String(y.year)), [
      { name: "Doanh thu", data: d.byYear.items.map(y => y.revenue) },
      { name: "Lợi nhuận", data: d.byYear.items.map(y => y.profit) }
    ], { format: CUR }));
    draw("chart-ov-category", d.byCategory, () => Charts.donut("chart-ov-category", d.byCategory.items, { value: "revenue", format: CUR }));
    draw("chart-ov-region", d.byRegion, () => Charts.bar("chart-ov-region", [...d.byRegion.items].sort((a, b) => b.revenue - a.revenue), { value: "revenue", format: CUR, horizontal: true, max: 12, seriesName: "Doanh thu", height: 360 }));
  }

  // --------------------------------------------------------------------------
  // 2. BÁN HÀNG
  // --------------------------------------------------------------------------
  async function renderSales(filters, isCurrent) {
    setKpis("sales-kpis", Comp.renderSkeletonCards(5));
    setLoading(["chart-sal-trend", "chart-sal-category", "chart-sal-top-rev", "chart-sal-top-prof", "sales-table"]);
    const d = await Svc.getSales(filters);
    if (!isCurrent()) return;

    gate("sales", d.summary, { title: "Chưa có dữ liệu bán hàng", emptyMessage: `Bảng Fact_Sales chưa có giao dịch${yearText(filters)}.`, filters });
    if (d.summary.state !== "ready") return;

    const s = d.summary.data;
    const note = d.summary.scope;
    setKpis("sales-kpis",
      kpi({ label: "Doanh thu", value: s.revenue, format: CUR, icon: "fa-sack-dollar", change: d.change.revenue, note }) +
      kpi({ label: "Lợi nhuận", value: s.profit, format: CUR, icon: "fa-arrow-trend-up", change: d.change.profit, note }) +
      kpi({ label: "Số đơn hàng", value: s.orders, icon: "fa-receipt", note }) +
      kpi({ label: "Số lượng bán", value: s.quantity, icon: "fa-box", change: d.change.quantity, note }) +
      kpi({ label: "Giá trị đơn trung bình", value: s.aov, format: CUR, icon: "fa-tag", note, hint: "Doanh thu / Số đơn hàng" })
    );

    const title = document.getElementById("sales-trend-title");
    if (title) title.textContent = filters.year === "ALL" ? "Doanh thu và lợi nhuận theo năm" : `Doanh thu và lợi nhuận theo quý, năm ${filters.year}`;
    setScope("sal-trend", CUR_LABEL, d.trend, filters);
    setScope("sal-category", CUR_LABEL, d.byCategory, filters);
    setScope("sal-top-rev", CUR_LABEL, d.topByRevenue, filters);
    setScope("sal-top-prof", CUR_LABEL, d.topByProfit, filters);
    setScope("sal-table", CUR_LABEL, d.topByRevenue, filters);

    draw("chart-sal-trend", d.trend, () => Charts.line("chart-sal-trend", d.trend.items.map(t => t.label), [
      { name: "Doanh thu", data: d.trend.items.map(t => t.revenue) },
      { name: "Lợi nhuận", data: d.trend.items.map(t => t.profit) }
    ], { format: CUR }), `Không có doanh thu${yearText(filters)}.`);
    draw("chart-sal-category", d.byCategory, () => Charts.donut("chart-sal-category", d.byCategory.items, { value: "revenue", format: CUR }));
    draw("chart-sal-top-rev", d.topByRevenue, () => Charts.bar("chart-sal-top-rev", d.topByRevenue.items, { value: "revenue", format: CUR, horizontal: true, max: 10, seriesName: "Doanh thu" }));
    draw("chart-sal-top-prof", d.topByProfit, () => Charts.bar("chart-sal-top-prof", d.topByProfit.items, { value: "profit", format: CUR, horizontal: true, max: 10, seriesName: "Lợi nhuận", colorIndex: 1 }));

    if (Comp.applySectionState("sales-table", d.topByRevenue)) {
      const profitByKey = {};
      d.topByProfit.items.forEach(p => { profitByKey[p.key] = p.profit; });
      const rows = d.topByRevenue.items.map((p, i) => {
        const profit = profitByKey[p.key] ?? null;
        return { rank: i + 1, product: p.label, category: p.category, subCategory: p.subCategory, quantity: p.quantity, revenue: p.revenue, profit, margin: profit !== null && p.revenue > 0 ? (profit / p.revenue) * 100 : null };
      });
      mountTable("sales", "sales-table", [
        { key: "rank", label: "#", format: "number" },
        { key: "product", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "subCategory", label: "Nhóm" },
        { key: "quantity", label: "SL bán", format: "number" },
        { key: "revenue", label: "Doanh thu", format: CUR },
        { key: "profit", label: "Lợi nhuận", format: CUR },
        { key: "margin", label: "Biên LN", format: "percent" }
      ], rows, { empty: "Không có sản phẩm." });
    }
  }

  // --------------------------------------------------------------------------
  // 3. MUA HÀNG
  // --------------------------------------------------------------------------
  async function renderPurchase(filters, isCurrent) {
    setKpis("purchase-kpis", Comp.renderSkeletonCards(6));
    setLoading(["chart-pur-trend", "chart-pur-category", "chart-pur-suppliers", "chart-pur-products", "purchase-table"]);
    const d = await Svc.getPurchase(filters);
    if (!isCurrent()) return;

    gate("purchase", d.orders, { title: "Chưa có dữ liệu mua hàng", emptyMessage: `Bảng Fact_Purchase chưa có đơn mua${yearText(filters)}.`, filters });
    if (d.orders.state !== "ready") return;

    const k = d.kpi;
    const note = d.orders.scope;
    setKpis("purchase-kpis",
      kpi({ label: "Tổng chi phí mua", value: k.totalCost, format: "vnd", icon: "fa-money-bill-wave", note }) +
      kpi({ label: "Số đơn mua", value: k.poCount, icon: "fa-file-invoice", note }) +
      kpi({ label: "Số lượng nhập", value: k.totalQty, icon: "fa-boxes-stacked", note }) +
      kpi({ label: "Chi phí trung bình / sản phẩm", value: k.avgUnitCost, format: "vnd", icon: "fa-tag", note, hint: "Tổng chi phí / Tổng số lượng nhập" }) +
      kpi({ label: "Giá trị trung bình / đơn", value: k.avgPoValue, format: "vnd", icon: "fa-scale-balanced", note }) +
      kpi({ label: "Nhà cung cấp", value: k.suppliers, icon: "fa-handshake", note: "Có đơn mua trong kỳ" })
    );

    setScope("pur-trend", "VNĐ", d.trend, filters);
    setScope("pur-category", "VNĐ", d.byCategory, filters);
    setScope("pur-suppliers", "VNĐ", d.suppliers, filters);
    setScope("pur-products", "VNĐ", d.topProducts, filters);
    setScope("pur-orders", "VNĐ", d.orders, filters);

    draw("chart-pur-trend", d.trend, () => Charts.line("chart-pur-trend", d.trend.items.map(t => t.label), [{ name: "Chi phí mua", data: d.trend.items.map(t => t.cost) }], { format: "vnd", area: true }));
    draw("chart-pur-category", d.byCategory, () => Charts.donut("chart-pur-category", d.byCategory.items, { value: "cost", format: "vnd" }));
    draw("chart-pur-suppliers", d.suppliers, () => Charts.bar("chart-pur-suppliers", d.suppliers.items, { value: "cost", format: "vnd", horizontal: true, max: 10, seriesName: "Chi phí mua" }));
    draw("chart-pur-products", d.topProducts, () => Charts.bar("chart-pur-products", d.topProducts.items, { value: "cost", format: "vnd", horizontal: true, max: 10, seriesName: "Chi phí mua", colorIndex: 1 }));

    mountTable("purchase", "purchase-table", [
      { key: "id", label: "Mã đơn" },
      { key: "date", label: "Ngày" },
      { key: "supplier", label: "Nhà cung cấp" },
      { key: "warehouse", label: "Kho nhận" },
      { key: "lines", label: "Số dòng", format: "number" },
      { key: "quantity", label: "Số lượng", format: "number" },
      { key: "cost", label: "Giá trị", format: "vnd" }
    ], d.orders.items, { limit: 200, onRowClick: "KhangNghiApp.openPurchaseOrder", rowKey: "id", empty: "Không có đơn mua." });
  }

  async function openPurchaseOrder(poId) {
    const body = Comp.openModal("po-detail-modal", "Chi tiết đơn mua", `Mã đơn: ${poId}`);
    const sec = await Svc.getPurchaseOrderDetail(poId);
    if (sec.state !== "ready") {
      body.innerHTML = Comp.renderState(sec.state, { message: Comp.sectionMessage(sec, "Đơn mua không có dòng chi tiết.") });
      return;
    }
    const first = sec.items[0];
    const total = sec.items.reduce((a, x) => a + x.cost, 0);
    body.innerHTML = `
      <div class="detail-grid">
        ${Comp.field("Nhà cung cấp", first.supplier)}
        ${Comp.field("Kho nhận", first.warehouse)}
        ${Comp.field("Ngày", first.date)}
        ${Comp.field("Giá trị các dòng hiển thị", fv(total, "vnd"))}
      </div>
      <h3 class="detail-section-title">Mặt hàng (${sec.items.length}${sec.items.length >= 50 ? ", tối đa 50 dòng giá trị cao nhất" : ""})</h3>
      ${Comp.renderTable([
        { key: "productId", label: "Mã SP" },
        { key: "product", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "quantity", label: "SL", format: "number" },
        { key: "unitCost", label: "Đơn giá", format: "vnd" },
        { key: "cost", label: "Thành tiền", format: "vnd" }
      ], sec.items)}`;
  }

  // --------------------------------------------------------------------------
  // 4. TỒN KHO
  // --------------------------------------------------------------------------
  async function renderInventory(filters, isCurrent) {
    setKpis("inventory-kpis", Comp.renderSkeletonCards(4));
    setLoading(["chart-inv-movement", "chart-inv-region", "inventory-table"]);
    const d = await Svc.getInventory();
    if (!isCurrent()) return;

    gate("inventory", d.byRegion, { title: "Chưa có dữ liệu tồn kho", emptyMessage: "Bảng Fact_Inventory chưa có bản ghi." });
    if (d.byRegion.state !== "ready") return;

    const k = d.kpi;
    setKpis("inventory-kpis",
      kpi({ label: "Tồn kho hiện tại", value: k.onHand, icon: "fa-warehouse", note: "Tổng số lượng trên mọi khu vực" }) +
      kpi({ label: "Tổng nhập", value: k.inbound, icon: "fa-arrow-right-to-bracket", note: ALL_TIME }) +
      kpi({ label: "Tổng xuất", value: k.outbound, icon: "fa-arrow-right-from-bracket", note: ALL_TIME }) +
      kpi({ label: "Dòng tồn dưới mức đặt lại", value: k.belowReorder, icon: "fa-triangle-exclamation", note: "Cần bổ sung hàng", hint: "Số bản ghi tồn kho có số lượng dưới mức đặt hàng lại" })
    );

    setScope("inv-movement", "Số lượng", d.movement);
    setScope("inv-region", "Số lượng tồn", d.byRegion);
    setScope("inv-alerts", "", d.alerts);
    draw("chart-inv-movement", d.movement, () => Charts.line("chart-inv-movement", d.movement.items.map(m => m.label), [
      { name: "Nhập", data: d.movement.items.map(m => m.inbound) },
      { name: "Xuất", data: d.movement.items.map(m => m.outbound) }
    ], { format: "number" }));
    draw("chart-inv-region", d.byRegion, () => Charts.bar("chart-inv-region", [...d.byRegion.items].sort((a, b) => b.onHand - a.onHand), { value: "onHand", format: "number", horizontal: true, max: 12, seriesName: "Tồn kho" }));

    if (Comp.applySectionState("inventory-table", d.alerts, "Không có mặt hàng nào dưới mức đặt hàng lại.", "Không có cảnh báo")) {
      const tone = { Red: ["danger", "Nguy cấp"], Yellow: ["warning", "Cảnh báo"], Green: ["success", "Ổn định"] };
      mountTable("inventory", "inventory-table", [
        { key: "product", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "location", label: "Vị trí" },
        { key: "onHand", label: "Tồn", format: "number" },
        { key: "reorderLevel", label: "Mức đặt lại", format: "number" },
        { key: "deficit", label: "Thiếu hụt", format: "number" },
        { key: "status", label: "Mức độ", render: r => { const t = tone[r.status]; return t ? Comp.statusBadge(t[0], t[1]) : "—"; } },
        { key: "recommendation", label: "Khuyến nghị" }
      ], d.alerts.items, { empty: "Không có cảnh báo." });
    }
  }

  // --------------------------------------------------------------------------
  // 5. VẬN CHUYỂN
  // --------------------------------------------------------------------------
  async function renderShipping(filters, isCurrent) {
    setKpis("shipping-kpis", Comp.renderSkeletonCards(5));
    setLoading(["chart-shp-cost", "chart-shp-late", "shipping-table"]);
    const d = await Svc.getShipping();
    if (!isCurrent()) return;

    gate("shipping", d.performance, { title: "Chưa có dữ liệu vận chuyển", emptyMessage: "Bảng Fact_Shipping chưa có bản ghi." });
    if (d.performance.state !== "ready") return;

    const k = d.kpi || {};
    setKpis("shipping-kpis",
      kpi({ label: "Số đơn giao", value: k.totalOrders, icon: "fa-truck-fast", note: ALL_TIME }) +
      kpi({ label: "Chi phí vận chuyển", value: k.totalCost, format: CUR, icon: "fa-money-bill-wave", note: ALL_TIME }) +
      kpi({ label: "Chi phí trung bình / đơn", value: k.avgCost, format: CUR, icon: "fa-tag", note: ALL_TIME }) +
      kpi({ label: "Tỷ lệ giao đúng hạn", value: k.onTimeRate, format: "percent", icon: "fa-circle-check", note: "Theo số ngày trễ (LateDays)" }) +
      kpi({ label: "Đơn giao trễ", value: k.lateOrders, icon: "fa-clock", note: ALL_TIME })
    );

    setScope("shp-cost", CUR_LABEL, d.costByRegion);
    setScope("shp-late", "% đơn trễ", d.lateByRegion);
    setScope("shp-table", "", d.performance);
    draw("chart-shp-cost", d.costByRegion, () => Charts.bar("chart-shp-cost", d.costByRegion.items, { value: "cost", format: CUR, horizontal: true, max: 12, seriesName: "Chi phí" }));
    draw("chart-shp-late", d.lateByRegion, () => Charts.bar("chart-shp-late", [...d.lateByRegion.items].sort((a, b) => (b.rate || 0) - (a.rate || 0)), { value: "rate", format: "percent", horizontal: true, max: 12, seriesName: "Tỷ lệ trễ", colorIndex: 1 }));

    mountTable("shipping", "shipping-table", [
      { key: "shipper", label: "Đơn vị vận chuyển" },
      { key: "orders", label: "Số đơn", format: "number" },
      { key: "cost", label: "Chi phí", format: CUR },
      { key: "avgCost", label: "Chi phí / đơn", format: CUR },
      { key: "avgDays", label: "Thời gian giao TB", format: "days" },
      { key: "lateRate", label: "Tỷ lệ trễ", format: "percent" },
      { key: "avgLateDays", label: "Số ngày trễ TB", format: "days" }
    ], d.performance.items);
  }

  // --------------------------------------------------------------------------
  // 6. KHÁCH HÀNG
  // --------------------------------------------------------------------------
  async function renderCustomers(filters, isCurrent) {
    setKpis("customer-kpis", Comp.renderSkeletonCards(5));
    setLoading(["chart-cus-segment", "chart-cus-top", "customer-table"]);
    const d = await Svc.getCustomers();
    if (!isCurrent()) return;

    gate("customer", d.summary, { title: "Chưa có dữ liệu khách hàng", emptyMessage: "Chưa có khách hàng nào phát sinh đơn trong Fact_Sales." });
    if (d.summary.state !== "ready") return;

    const k = d.kpi;
    setKpis("customer-kpis",
      kpi({ label: "Khách hàng có giao dịch", value: k.customers, icon: "fa-users", note: ALL_TIME }) +
      kpi({ label: "Doanh thu / khách hàng", value: k.revenuePerCustomer, format: CUR, icon: "fa-sack-dollar", note: ALL_TIME }) +
      kpi({ label: "Số đơn / khách hàng", value: k.ordersPerCustomer, icon: "fa-receipt", note: ALL_TIME }) +
      kpi({ label: "Phân khúc dẫn đầu", value: k.topSegment, format: "text", icon: "fa-layer-group", note: k.topSegmentShare !== null ? `${fv(k.topSegmentShare, "percent")} doanh thu` : "" }) +
      kpi({ label: "Đóng góp của top 10 khách hàng", value: k.top10Share, format: "percent", icon: "fa-crown", note: "Trên tổng doanh thu" })
    );

    setScope("cus-segment", CUR_LABEL, d.segments);
    setScope("cus-top", CUR_LABEL, d.top);
    setScope("cus-table", CUR_LABEL, d.top);
    draw("chart-cus-segment", d.segments, () => Charts.donut("chart-cus-segment", d.segments.items, { value: "revenue", format: CUR }));
    draw("chart-cus-top", d.top, () => Charts.bar("chart-cus-top", d.top.items, { label: "name", value: "revenue", format: CUR, horizontal: true, max: 10, seriesName: "Doanh thu" }));

    if (Comp.applySectionState("customer-table", d.top)) {
      mountTable("customer", "customer-table", [
        { key: "id", label: "Mã KH", render: r => r.id ? `<span class="link">${esc(r.id)}</span>` : "—" },
        { key: "name", label: "Khách hàng" },
        { key: "segment", label: "Phân khúc" },
        { key: "orders", label: "Số đơn", format: "number" },
        { key: "revenue", label: "Doanh thu", format: CUR },
        { key: "profit", label: "Lợi nhuận", format: CUR }
      ], d.top.items, { onRowClick: "KhangNghiComponents.openCustomerDetailDrawer", rowKey: "id" });
    }
  }

  // --------------------------------------------------------------------------
  // 7. SẢN PHẨM
  // --------------------------------------------------------------------------
  async function renderProducts(filters, isCurrent) {
    setKpis("product-kpis", Comp.renderSkeletonCards(5));
    setLoading(["chart-prd-top", "chart-prd-category", "product-table", "product-discount-table"]);
    const d = await Svc.getProducts();
    if (!isCurrent()) return;

    gate("product", d.byCategory, { title: "Chưa có dữ liệu sản phẩm", emptyMessage: "Chưa có doanh thu theo sản phẩm trong Fact_Sales." });
    if (d.byCategory.state !== "ready") return;

    const k = d.kpi;
    setKpis("product-kpis",
      kpi({ label: "Danh mục", value: k.categories, icon: "fa-folder-tree", note: "Có doanh thu" }) +
      kpi({ label: "Nhóm hàng", value: k.subCategories, icon: "fa-layer-group", note: "Có doanh thu" }) +
      kpi({ label: "Doanh thu", value: k.revenue, format: CUR, icon: "fa-sack-dollar", note: ALL_TIME }) +
      kpi({ label: "Lợi nhuận", value: k.profit, format: CUR, icon: "fa-arrow-trend-up", note: ALL_TIME }) +
      kpi({ label: "Biên lợi nhuận", value: k.margin, format: "percent", icon: "fa-percent", note: ALL_TIME })
    );

    setScope("prd-top", CUR_LABEL, d.top);
    setScope("prd-category", CUR_LABEL, d.byCategory);
    setScope("prd-table", CUR_LABEL, d.top);
    setScope("prd-discount", "", d.lowMargin);
    draw("chart-prd-top", d.top, () => Charts.bar("chart-prd-top", d.top.items, { value: "revenue", format: CUR, horizontal: true, max: 10, seriesName: "Doanh thu" }));
    draw("chart-prd-category", d.byCategory, () => Charts.donut("chart-prd-category", d.byCategory.items, { value: "revenue", format: CUR }));

    if (Comp.applySectionState("product-table", d.top)) {
      mountTable("product", "product-table", [
        { key: "label", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "subCategory", label: "Nhóm" },
        { key: "quantity", label: "SL bán", format: "number" },
        { key: "revenue", label: "Doanh thu", format: CUR },
        { key: "profit", label: "Lợi nhuận", format: CUR },
        { key: "margin", label: "Biên LN", format: "percent" }
      ], d.top.items);
    }
    if (Comp.applySectionState("product-discount-table", d.lowMargin, "Không có sản phẩm thuộc nhóm này.", "Không có sản phẩm nào")) {
      mountTable("product-discount", "product-discount-table", [
        { key: "label", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "discount", label: "Chiết khấu TB", format: "percent" },
        { key: "revenue", label: "Doanh thu", format: CUR },
        { key: "profit", label: "Lợi nhuận", format: CUR }
      ], d.lowMargin.items);
    }
  }

  // --------------------------------------------------------------------------
  // 8. NHÀ CUNG CẤP
  // --------------------------------------------------------------------------
  async function renderSuppliers(filters, isCurrent) {
    setKpis("supplier-kpis", Comp.renderSkeletonCards(5));
    setLoading(["chart-sup-bar", "chart-sup-share", "supplier-table"]);
    const d = await Svc.getSuppliers();
    if (!isCurrent()) return;

    gate("supplier", d.suppliers, { title: "Chưa có dữ liệu nhà cung cấp", emptyMessage: "Chưa có đơn mua gắn với nhà cung cấp trong Fact_Purchase." });
    if (d.suppliers.state !== "ready") return;

    const k = d.kpi;
    setKpis("supplier-kpis",
      kpi({ label: "Nhà cung cấp", value: k.count, icon: "fa-handshake", note: "Có đơn mua" }) +
      kpi({ label: "Tổng chi phí mua", value: k.totalCost, format: "vnd", icon: "fa-money-bill-wave", note: ALL_TIME }) +
      kpi({ label: "Nhà cung cấp lớn nhất", value: k.top, format: "text", icon: "fa-trophy", note: k.top1Share !== null ? `${fv(k.top1Share, "percent")} chi phí` : "" }) +
      kpi({ label: "Tỷ trọng top 3", value: k.top3Share, format: "percent", icon: "fa-chart-pie", note: "Mức độ tập trung chi phí" })
    );

    setScope("sup-bar", "VNĐ", d.suppliers);
    setScope("sup-share", "VNĐ", d.suppliers);
    setScope("sup-table", "VNĐ", d.suppliers);
    draw("chart-sup-bar", d.suppliers, () => Charts.bar("chart-sup-bar", d.suppliers.items, { value: "cost", format: "vnd", horizontal: true, max: 10, seriesName: "Chi phí mua" }));
    draw("chart-sup-share", d.suppliers, () => Charts.donut("chart-sup-share", d.suppliers.items, { value: "cost", format: "vnd" }));
    mountTable("supplier", "supplier-table", [
      { key: "label", label: "Nhà cung cấp" },
      { key: "cost", label: "Chi phí mua", format: "vnd" },
      { key: "quantity", label: "Số lượng nhập", format: "number" },
      { key: "unitCost", label: "Đơn giá TB", format: "vnd" },
      { key: "share", label: "Tỷ trọng", format: "percent" }
    ], d.suppliers.items);
  }

  // --------------------------------------------------------------------------
  // 9. KHU VỰC
  // --------------------------------------------------------------------------
  async function renderGeography(filters, isCurrent) {
    setLoading(["chart-geo-region", "chart-geo-country", "chart-geo-trend", "geography-city-table"]);
    const d = await Svc.getGeography();
    if (!isCurrent()) return;

    gate("geography", d.regions, { title: "Chưa có dữ liệu theo khu vực", emptyMessage: "Fact_Sales chưa có giao dịch gắn với Dim_Location." });
    if (d.regions.state !== "ready") return;

    setScope("geo-region", CUR_LABEL, d.regions);
    setScope("geo-country", CUR_LABEL, d.countries);
    setScope("geo-trend", CUR_LABEL, d.trend);
    setScope("geo-city", "Số lượng bán", d.cities);
    draw("chart-geo-region", d.regions, () => Charts.bar("chart-geo-region", d.regions.items, { value: "revenue", format: CUR, horizontal: true, max: 12, seriesName: "Doanh thu" }));
    draw("chart-geo-country", d.countries, () => Charts.bar("chart-geo-country", d.countries.items, { value: "profit", format: CUR, horizontal: true, max: 10, seriesName: "Lợi nhuận", colorIndex: 1 }));
    draw("chart-geo-trend", d.trend, () => {
      // 5 khu vực doanh thu cao nhất; màu gắn theo khu vực, không theo thứ hạng năm
      const totals = {};
      d.trend.items.forEach(t => { totals[t.region] = (totals[t.region] || 0) + t.revenue; });
      const top = Object.keys(totals).sort((a, b) => totals[b] - totals[a]).slice(0, 5);
      const years = [...new Set(d.trend.items.map(t => t.year))].sort((a, b) => a - b);
      return Charts.line("chart-geo-trend", years.map(String), top.map(r => ({
        name: r,
        data: years.map(y => { const it = d.trend.items.find(t => t.region === r && t.year === y); return it ? it.revenue : null; })
      })), { format: CUR });
    });
    if (Comp.applySectionState("geography-city-table", d.cities)) {
      mountTable("geography-city", "geography-city-table", [
        { key: "label", label: "Thành phố" },
        { key: "region", label: "Khu vực" },
        { key: "quantity", label: "SL bán", format: "number" },
        { key: "revenue", label: "Doanh thu", format: CUR }
      ], d.cities.items);
    }
  }

  // --------------------------------------------------------------------------
  // 10. PHÂN TÍCH CHÉO
  // --------------------------------------------------------------------------
  async function renderCrossAnalysis(filters, isCurrent) {
    setLoading(["cross-stockout-table", "cross-overstock-table", "chart-cross-eff"]);
    const d = await Svc.getCrossAnalysis();
    if (!isCurrent()) return;

    const secs = [d.stockout, d.overstock, d.efficiency];
    const allSame = secs.every(s => s.state !== "ready" && s.state === secs[0].state);
    gate("cross-analysis", allSame ? secs[0] : { state: "ready" }, { title: "Chưa đủ dữ liệu để phân tích chéo", emptyMessage: "Cần dữ liệu đồng thời trong Fact_Sales, Fact_Inventory, Fact_Purchase và Fact_Shipping." });
    if (allSame) return;

    setScope("cross-stockout", "", d.stockout);
    setScope("cross-overstock", "", d.overstock);
    setScope("cross-eff", "% chi phí vận chuyển / doanh thu", d.efficiency);
    if (Comp.applySectionState("cross-stockout-table", d.stockout, "Không có mặt hàng có nguy cơ thiếu hàng.", "Không có rủi ro")) {
      mountTable("cross-stockout", "cross-stockout-table", [
        { key: "label", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "sold", label: "Đã bán", format: "number" },
        { key: "onHand", label: "Tồn", format: "number" },
        { key: "risk", label: "Mức rủi ro", render: r => Comp.statusBadge(/cao/i.test(r.risk) ? "danger" : "warning", r.risk || "—") }
      ], d.stockout.items);
    }
    if (Comp.applySectionState("cross-overstock-table", d.overstock, "Không có mặt hàng có nguy cơ tồn đọng.", "Không có rủi ro")) {
      mountTable("cross-overstock", "cross-overstock-table", [
        { key: "label", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "purchased", label: "Đã nhập", format: "number" },
        { key: "sold", label: "Đã bán", format: "number" },
        { key: "excess", label: "Dư", format: "number" }
      ], d.overstock.items);
    }
    draw("chart-cross-eff", d.efficiency, () => Charts.bar("chart-cross-eff", [...d.efficiency.items].sort((a, b) => (b.ratio || 0) - (a.ratio || 0)), { value: "ratio", format: "percent", horizontal: true, max: 12, seriesName: "Chi phí VC / doanh thu" }));
  }

  // --------------------------------------------------------------------------
  // 11. KHÁM PHÁ OLAP
  // --------------------------------------------------------------------------
  const OLAP_DEFAULT = { measure: "[Measures].[Sales Amount]", rows: "[Dim Product].[Category]", cols: "[Dim Time].[Year]" };

  function initOlapControls() {
    const m = document.getElementById("olap-measure");
    if (!m || m.options.length) return;
    const meta = Svc.getCubeMetadata();
    const opt = (id, name) => `<option value="${esc(id)}">${esc(name)}</option>`;
    m.innerHTML = meta.measures.map(x => opt(x.id, x.name)).join("");
    document.getElementById("olap-rows").innerHTML = meta.levels.map(x => opt(x.id, x.name)).join("");
    document.getElementById("olap-cols").innerHTML = opt("", "Không dùng") + meta.levels.map(x => opt(x.id, x.name)).join("");
    ["olap-measure", "olap-rows", "olap-cols"].forEach(id => document.getElementById(id).addEventListener("change", buildOlapMdx));
    resetOlap(true);
  }

  function buildOlapMdx() {
    const measure = document.getElementById("olap-measure").value;
    const rows = document.getElementById("olap-rows").value;
    const cols = document.getElementById("olap-cols").value;
    const mdx = cols && cols !== rows
      ? `SELECT\n  NON EMPTY ${cols}.CHILDREN ON COLUMNS,\n  NON EMPTY ${rows}.CHILDREN ON ROWS\nFROM [KhangNghi_Cube]\nWHERE (${measure})`
      : `SELECT\n  { ${measure} } ON COLUMNS,\n  NON EMPTY ${rows}.CHILDREN ON ROWS\nFROM [KhangNghi_Cube]`;
    const pre = document.getElementById("olap-mdx");
    if (pre) pre.textContent = mdx;
    return mdx;
  }

  function resetOlap(silent) {
    document.getElementById("olap-measure").value = OLAP_DEFAULT.measure;
    document.getElementById("olap-rows").value = OLAP_DEFAULT.rows;
    document.getElementById("olap-cols").value = OLAP_DEFAULT.cols;
    buildOlapMdx();
    if (!silent) {
      document.getElementById("olap-result").innerHTML = "";
      document.getElementById("olap-result-meta").textContent = "Chưa chạy truy vấn";
    }
  }

  function swapOlapAxes() {
    const r = document.getElementById("olap-rows");
    const c = document.getElementById("olap-cols");
    if (!c.value) return Comp.showToast("Chọn chiều cho cột trước khi đổi trục.", "info");
    [r.value, c.value] = [c.value, r.value];
    buildOlapMdx();
  }

  async function renderOlap() {
    initOlapControls();
    buildOlapMdx();
    const box = document.getElementById("olap-ssas-status");
    if (box) box.innerHTML = Comp.renderState("loading", { message: "Kiểm tra kết nối SSAS…", compact: true });
    const s = await Svc.getSsasStatus();
    if (!box) return;
    if (s.state !== "ready") box.innerHTML = Comp.renderState(s.state, { message: s.message, compact: true });
    else if (!s.connected) box.innerHTML = Comp.renderState("unavailable", { title: "SSAS chưa kết nối", message: `Không kết nối được ${s.server || "máy chủ SSAS"}. Truy vấn sẽ không có kết quả cho đến khi dịch vụ Analysis Services hoạt động.`, compact: true });
    else box.innerHTML = "";
    if (!document.getElementById("olap-result").innerHTML) {
      document.getElementById("olap-result").innerHTML = Comp.renderState("empty", { title: "Chưa chạy truy vấn", message: "Chọn độ đo và chiều, sau đó bấm Chạy truy vấn.", compact: true });
    }
  }

  async function runOlap() {
    await runMdxInto(buildOlapMdx(), "olap-result", "olap-result-meta", "olap-run-btn");
  }

  function openOlapInStudio() {
    const mdx = buildOlapMdx();
    window.location.hash = "#/mdx-studio";
    setTimeout(() => {
      switchMdxTab("editor");
      const input = document.getElementById("mdx-input");
      if (input) input.value = mdx;
    }, 50);
  }

  // Chạy MDX và hiển thị kết quả; phân biệt SSAS chưa kết nối / lỗi cú pháp / không có dòng
  async function runMdxInto(mdx, resultId, metaId, btnId) {
    const result = document.getElementById(resultId);
    const meta = document.getElementById(metaId);
    const btn = document.getElementById(btnId);
    if (!mdx.trim()) return Comp.showToast("Nhập câu lệnh MDX trước khi chạy.", "warning");
    const btnHtml = btn ? btn.innerHTML : "";
    if (btn) { btn.disabled = true; btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Đang chạy`; }
    result.innerHTML = Comp.renderState("loading", { message: "Đang gửi truy vấn tới SSAS…", compact: true });
    meta.textContent = "Đang chạy…";
    const r = await Svc.executeMdx(mdx);
    if (btn) { btn.disabled = false; btn.innerHTML = btnHtml; }
    const time = r.timeMs !== null && r.timeMs !== undefined ? ` · ${r.timeMs} ms` : "";
    if (r.state === "ready") {
      meta.textContent = `${r.rows.length.toLocaleString("vi-VN")} dòng${time}`;
      const cols = r.columns.map(c => ({ key: c, label: c, render: row => esc(typeof row[c] === "number" ? fv(row[c]) : (row[c] ?? "—")), align: r.rows.some(x => typeof x[c] === "number") ? "right" : "" }));
      result.innerHTML = Comp.renderTable(cols, r.rows);
    } else if (r.state === "empty") {
      meta.textContent = `0 dòng${time}`;
      result.innerHTML = Comp.renderState("empty", { title: "Truy vấn không trả về dòng nào", message: "SSAS thực thi thành công nhưng khối không có dữ liệu khớp với truy vấn.", compact: true });
    } else if (r.state === "unavailable") {
      meta.textContent = "Chưa thực thi được";
      result.innerHTML = Comp.renderState("unavailable", { title: "Không kết nối được SSAS", message: `Truy vấn chưa được thực thi. Chi tiết kỹ thuật: ${r.message}`, compact: true });
    } else {
      meta.textContent = "Lỗi truy vấn";
      result.innerHTML = Comp.renderState(r.state, { title: r.state === "error" ? "SSAS báo lỗi khi thực thi" : "", message: r.message, compact: true });
    }
  }

  // --------------------------------------------------------------------------
  // 12. MDX STUDIO
  // --------------------------------------------------------------------------
  async function loadCatalog() {
    if (!mdxCatalog || mdxCatalog.state !== "ready") mdxCatalog = await Svc.getMdxCatalog();
    return mdxCatalog;
  }

  async function renderMdxStudio() {
    const input = document.getElementById("mdx-input");
    if (input && !input.dataset.bound) {
      input.dataset.bound = "1";
      input.addEventListener("keydown", e => {
        if ((e.ctrlKey || e.metaKey) && e.key === "Enter") { e.preventDefault(); runMdx(); }
      });
    }
    if (!document.getElementById("mdx-result").innerHTML) {
      document.getElementById("mdx-result").innerHTML = Comp.renderState("empty", { title: "Chưa chạy truy vấn", message: "Soạn câu lệnh hoặc chọn truy vấn mẫu, sau đó bấm Chạy.", compact: true });
    }
    const cat = await loadCatalog();
    const sel = document.getElementById("mdx-sample");
    if (sel && cat.state === "ready" && sel.options.length <= 1) {
      const sections = [...new Set(cat.items.map(q => q.section))];
      sel.innerHTML = `<option value="">Chọn truy vấn mẫu…</option>` + sections.map(s => `<optgroup label="${esc(s)}">${cat.items.filter(q => q.section === s).map(q => `<option value="${esc(q.id)}">${esc(q.id)} · ${esc(q.title)}</option>`).join("")}</optgroup>`).join("");
      const secSel = document.getElementById("mdx-lib-section");
      if (secSel && secSel.options.length <= 1) secSel.innerHTML += sections.map(s => `<option value="${esc(s)}">${esc(s)}</option>`).join("");
    }
    renderMdxLibrary();
  }

  function switchMdxTab(tab) {
    document.getElementById("mdx-pane-editor").style.display = tab === "editor" ? "" : "none";
    document.getElementById("mdx-pane-library").style.display = tab === "library" ? "" : "none";
    document.getElementById("mdx-tab-editor").classList.toggle("active", tab === "editor");
    document.getElementById("mdx-tab-library").classList.toggle("active", tab === "library");
  }

  function loadMdxSample(id) {
    if (!id || !mdxCatalog) return;
    const q = mdxCatalog.items.find(x => x.id === id);
    if (!q) return;
    switchMdxTab("editor");
    document.getElementById("mdx-input").value = q.mdx;
    const sel = document.getElementById("mdx-sample");
    if (sel) sel.value = id;
    document.getElementById("mdx-result-meta").textContent = `${q.id} · ${q.verified ? "Đã kiểm chứng" : "Mẫu thiết kế, chưa kiểm chứng trên khối"}`;
  }

  function clearMdx() {
    document.getElementById("mdx-input").value = "";
    document.getElementById("mdx-sample").value = "";
  }

  function formatMdx() {
    const el = document.getElementById("mdx-input");
    el.value = el.value
      .replace(/\s+/g, " ")
      .replace(/\s*\b(SELECT|FROM|WHERE|WITH|MEMBER|SET)\b\s*/gi, (m, k) => `\n${k.toUpperCase()}\n  `)
      .replace(/\s+ON\s+(COLUMNS|ROWS|0|1)\s*,?/gi, (m, a) => ` ON ${a.toUpperCase()}${m.trim().endsWith(",") ? "," : ""}\n  `)
      .replace(/\n\s*\n/g, "\n")
      .trim();
  }

  function runMdx() {
    return runMdxInto(document.getElementById("mdx-input").value, "mdx-result", "mdx-result-meta", "mdx-run-btn");
  }

  function renderMdxLibrary() {
    const el = document.getElementById("mdx-library");
    if (!el) return;
    if (!mdxCatalog) { el.innerHTML = Comp.renderState("loading", { compact: true }); return; }
    if (mdxCatalog.state !== "ready") { el.innerHTML = Comp.renderState(mdxCatalog.state, { message: mdxCatalog.message, compact: true }); return; }
    const q = (document.getElementById("mdx-lib-search")?.value || "").toLowerCase();
    const sec = document.getElementById("mdx-lib-section")?.value || "";
    const st = document.getElementById("mdx-lib-status")?.value || "";
    const rows = mdxCatalog.items.filter(x =>
      (!q || `${x.id} ${x.title} ${x.purpose}`.toLowerCase().includes(q)) &&
      (!sec || x.section === sec) &&
      (!st || (st === "verified") === x.verified));
    el.innerHTML = Comp.renderTable([
      { key: "id", label: "Mã" },
      { key: "title", label: "Truy vấn", render: r => `<strong>${esc(r.title)}</strong><div class="cell-sub">${esc(r.operation)} · ${esc(r.factTable)}</div>` },
      { key: "section", label: "Nghiệp vụ" },
      { key: "verified", label: "Trạng thái", render: r => r.verified ? Comp.statusBadge("success", "Đã kiểm chứng") : Comp.statusBadge("neutral", "Mẫu thiết kế") },
      { key: "", label: "", render: r => `<button type="button" class="btn btn-sm" onclick="event.stopPropagation(); KhangNghiApp.loadMdxSample('${esc(r.id)}')">Nạp vào trình soạn</button>` }
    ], rows, { empty: "Không có truy vấn khớp bộ lọc." });
  }

  // --------------------------------------------------------------------------
  // 13. DỰ BÁO
  // --------------------------------------------------------------------------
  function setForecastHorizon(h) {
    forecastHorizon = h;
    document.querySelectorAll("#view-forecast .segmented-btn").forEach(b => b.classList.toggle("active", Number(b.dataset.horizon) === h));
    renderCurrentView();
  }

  async function renderForecast(filters, isCurrent) {
    setKpis("forecast-kpis", Comp.renderSkeletonCards(4));
    setLoading(["chart-forecast", "forecast-table"]);
    document.getElementById("forecast-notice").innerHTML = "";
    const d = await Svc.getForecast(forecastHorizon);
    if (!isCurrent()) return;

    gate("forecast", d, { title: "Chưa có kết quả dự báo", emptyMessage: "Mô hình chưa tạo được điểm dự báo vì Fact_Sales chưa có đủ doanh thu theo tháng để huấn luyện." });
    if (d.state !== "ready") return;

    if (d.isFallback) {
      document.getElementById("forecast-notice").innerHTML = Comp.renderState("unavailable", { title: "Đang dùng mô hình dự phòng", message: `${d.modelName}: ML.NET SSA không chạy được với dữ liệu hiện có, kết quả là ước lượng theo xu hướng và chỉ nên dùng để tham khảo.`, compact: true });
    }
    // Không hiển thị MAE/RMSE: backend hiện trả hằng số cố định, không phải sai số đo trên dữ liệu
    const firstFuture = d.points.find(p => p.isFuture);
    setKpis("forecast-kpis",
      kpi({ label: "Mô hình", value: d.modelName, format: "text", icon: "fa-brain", note: d.targetMetric }) +
      kpi({ label: "Kỳ dự báo", value: d.futureCount, icon: "fa-calendar-plus", note: "tháng" }) +
      kpi({ label: `Dự báo ${firstFuture ? firstFuture.label : "tháng tới"}`, value: firstFuture ? firstFuture.forecast : null, format: CUR, icon: "fa-wand-magic-sparkles", note: firstFuture && firstFuture.lower !== null ? `Khoảng ${fv(firstFuture.lower, CUR)} – ${fv(firstFuture.upper, CUR)}` : "" }) +
      kpi({ label: "Tháng lịch sử trên biểu đồ", value: d.historyCount, icon: "fa-clock-rotate-left", note: "Doanh thu thực tế gần nhất" })
    );
    draw("chart-forecast", d, () => Charts.forecast("chart-forecast", d.points, { format: CUR }));
    mountTable("forecast", "forecast-table", [
      { key: "label", label: "Kỳ" },
      { key: "forecast", label: "Dự báo", format: CUR },
      { key: "lower", label: "Cận dưới (95%)", format: CUR },
      { key: "upper", label: "Cận trên (95%)", format: CUR }
    ], d.points.filter(p => p.isFuture));
  }

  // --------------------------------------------------------------------------
  // 14. PHÂN TÍCH GIẢ ĐỊNH (tính từ số liệu thật, công thức hiển thị trên trang)
  // --------------------------------------------------------------------------
  async function renderWhatIf(filters, isCurrent) {
    document.getElementById("whatif-results").innerHTML = Comp.renderState("loading", { compact: true });
    const s = await Svc.getSummary();
    if (!isCurrent()) return;
    gate("what-if", s, { title: "Chưa có số liệu gốc", emptyMessage: "Cần doanh thu và lợi nhuận thực tế trong Fact_Sales để mô phỏng." });
    renderSavedScenarios();
    if (s.state !== "ready") { whatIfBase = null; return; }
    whatIfBase = s.data;
    document.getElementById("whatif-base-label").textContent = `Số liệu gốc: ${ALL_TIME}`;
    updateWhatIf();
  }

  function whatIfInputs() {
    return {
      growth: Number(document.getElementById("slider-growth").value),
      discount: Number(document.getElementById("slider-discount").value),
      ship: Number(document.getElementById("slider-ship").value)
    };
  }

  function computeWhatIf(b, p) {
    const revenue = b.revenue * (1 + p.growth / 100);
    const shipBase = b.shippingCost || 0;
    const profit = b.profit * (1 + p.growth / 100) - revenue * (p.discount / 100) - shipBase * (p.ship / 100);
    return { revenue, profit, margin: revenue > 0 ? (profit / revenue) * 100 : null, diff: profit - b.profit };
  }

  function updateWhatIf() {
    const p = whatIfInputs();
    document.getElementById("val-growth").textContent = `${p.growth > 0 ? "+" : ""}${p.growth}%`;
    document.getElementById("val-discount").textContent = `${p.discount}%`;
    document.getElementById("val-ship").textContent = `${p.ship > 0 ? "+" : ""}${p.ship}%`;
    if (!whatIfBase) return;
    const b = whatIfBase;
    const r = computeWhatIf(b, p);
    const diffPct = b.profit ? (r.diff / Math.abs(b.profit)) * 100 : null;
    document.getElementById("whatif-results").innerHTML = `
      <div class="whatif-row"><span>Doanh thu</span><span>${fv(b.revenue, CUR)}</span><i class="fa-solid fa-arrow-right"></i><strong>${fv(r.revenue, CUR)}</strong></div>
      <div class="whatif-row"><span>Lợi nhuận</span><span>${fv(b.profit, CUR)}</span><i class="fa-solid fa-arrow-right"></i><strong>${fv(r.profit, CUR)}</strong></div>
      <div class="whatif-row"><span>Biên lợi nhuận</span><span>${fv(b.margin, "percent")}</span><i class="fa-solid fa-arrow-right"></i><strong>${fv(r.margin, "percent")}</strong></div>
      <div class="whatif-row is-total"><span>Chênh lệch lợi nhuận</span><strong class="${r.diff >= 0 ? "is-up" : "is-down"}">${r.diff >= 0 ? "+" : ""}${fv(r.diff, CUR)}${diffPct !== null ? ` (${diffPct >= 0 ? "+" : ""}${diffPct.toFixed(1)}%)` : ""}</strong></div>
      ${!b.shippingCost ? `<p class="table-note">Chưa có chi phí vận chuyển thực tế nên tham số vận chuyển không ảnh hưởng kết quả.</p>` : ""}`;
    Charts.bar("chart-whatif", [{ label: "Hiện tại", value: b.profit }, { label: "Giả định", value: r.profit }], { format: CUR, seriesName: "Lợi nhuận", height: 220 });
  }

  function resetWhatIf() {
    document.getElementById("slider-growth").value = 0;
    document.getElementById("slider-discount").value = 0;
    document.getElementById("slider-ship").value = 0;
    updateWhatIf();
  }

  function readScenarios() {
    try { return JSON.parse(localStorage.getItem("kn_whatif_scenarios") || "[]"); } catch (e) { return []; }
  }

  function saveWhatIfScenario() {
    if (!whatIfBase) return Comp.showToast("Chưa có số liệu gốc để lưu kịch bản.", "warning");
    const p = whatIfInputs();
    const name = prompt("Tên kịch bản:", `Doanh thu ${p.growth >= 0 ? "+" : ""}${p.growth}%, chiết khấu ${p.discount}%, vận chuyển ${p.ship >= 0 ? "+" : ""}${p.ship}%`);
    if (!name) return;
    const r = computeWhatIf(whatIfBase, p);
    const list = readScenarios();
    list.push({ name, date: new Date().toLocaleDateString("vi-VN"), growth: p.growth, discount: p.discount, ship: p.ship, profit: r.profit, diff: r.diff });
    try { localStorage.setItem("kn_whatif_scenarios", JSON.stringify(list)); } catch (e) { /* trình duyệt chặn lưu trữ */ }
    renderSavedScenarios();
    Comp.showToast(`Đã lưu kịch bản "${name}".`);
  }

  function deleteScenario(idx) {
    const list = readScenarios();
    list.splice(Number(idx), 1);
    try { localStorage.setItem("kn_whatif_scenarios", JSON.stringify(list)); } catch (e) { /* bỏ qua */ }
    renderSavedScenarios();
  }

  function renderSavedScenarios() {
    const el = document.getElementById("whatif-saved");
    if (!el) return;
    const list = readScenarios().map((s, i) => ({ ...s, idx: i, params: `DT ${s.growth >= 0 ? "+" : ""}${s.growth}% · CK ${s.discount}% · VC ${s.ship >= 0 ? "+" : ""}${s.ship}%` }));
    el.innerHTML = Comp.renderTable([
      { key: "name", label: "Kịch bản" },
      { key: "date", label: "Ngày lưu" },
      { key: "params", label: "Tham số" },
      { key: "profit", label: "Lợi nhuận ước tính", format: CUR },
      { key: "diff", label: "Chênh lệch", format: CUR },
      { key: "", label: "", render: r => `<button type="button" class="btn btn-sm btn-ghost" onclick="KhangNghiApp.deleteScenario(${r.idx})" aria-label="Xóa kịch bản"><i class="fa-solid fa-trash"></i></button>` }
    ], list, { empty: "Điều chỉnh tham số rồi bấm Lưu kịch bản.", emptyTitle: "Chưa có kịch bản" });
  }

  // --------------------------------------------------------------------------
  // 15. CÂU HỎI PHÂN TÍCH (danh mục 40 truy vấn MDX từ API)
  // --------------------------------------------------------------------------
  const SECTION_ROUTE = {
    "Bán hàng & Doanh thu": "sales",
    "Khách hàng & Thị trường": "customer",
    "Quản lý Kho & Tồn kho": "inventory",
    "Mua hàng & Nhà cung cấp": "purchase",
    "Vận chuyển & Logistics": "shipping"
  };

  async function renderQuestionExplorer() {
    const el = document.getElementById("question-table");
    if (el && !mdxCatalog) el.innerHTML = Comp.renderState("loading", { compact: true });
    const cat = await loadCatalog();
    const secSel = document.getElementById("question-section");
    if (cat.state === "ready" && secSel && secSel.options.length <= 1) {
      secSel.innerHTML += [...new Set(cat.items.map(q => q.section))].map(s => `<option value="${esc(s)}">${esc(s)}</option>`).join("");
    }
    renderQuestions();
  }

  function renderQuestions() {
    const el = document.getElementById("question-table");
    if (!el || !mdxCatalog) return;
    if (mdxCatalog.state !== "ready") { el.innerHTML = Comp.renderState(mdxCatalog.state, { message: mdxCatalog.message, compact: true }); return; }
    const q = (document.getElementById("question-search")?.value || "").toLowerCase();
    const sec = document.getElementById("question-section")?.value || "";
    const st = document.getElementById("question-status")?.value || "";
    const rows = mdxCatalog.items.filter(x =>
      (!q || `${x.id} ${x.title} ${x.purpose}`.toLowerCase().includes(q)) &&
      (!sec || x.section === sec) &&
      (!st || (st === "verified") === x.verified));
    el.innerHTML = Comp.renderTable([
      { key: "id", label: "Mã" },
      { key: "title", label: "Câu hỏi", render: r => `<strong>${esc(r.title)}</strong><div class="cell-sub">${esc(r.purpose)}</div>` },
      { key: "section", label: "Phân hệ" },
      { key: "factTable", label: "Bảng nguồn", render: r => `<code>${esc(r.factTable)}</code>` },
      { key: "verified", label: "Trạng thái", render: r => r.verified ? Comp.statusBadge("success", "Đã kiểm chứng") : Comp.statusBadge("neutral", "Mẫu thiết kế") },
      { key: "", label: "", render: r => {
        const route = SECTION_ROUTE[r.section];
        const openPage = route && canOpen(route) ? `<a class="btn btn-sm" href="#/${route}">Mở trang</a>` : "";
        return `<div class="cell-actions">${openPage}<button type="button" class="btn btn-sm btn-ghost" onclick="KhangNghiApp.openQuestionMdx('${esc(r.id)}')">Xem MDX</button></div>`;
      } }
    ], rows, { empty: "Không có câu hỏi khớp bộ lọc." });
  }

  function openQuestionMdx(id) {
    window.location.hash = "#/mdx-studio";
    setTimeout(() => loadMdxSample(id), 60);
  }

  // --------------------------------------------------------------------------
  // 16. TRẠNG THÁI DỮ LIỆU & NHẬT KÝ ETL
  // --------------------------------------------------------------------------
  async function renderSystemStatus(filters, isCurrent) {
    setKpis("status-kpis", Comp.renderSkeletonCards(5));
    setLoading(["status-tables", "status-ssas"]);
    const d = await Svc.getDataStatus();
    if (!isCurrent()) return;

    const t = d.tables;
    const s = d.ssas;
    const tablesOk = t.state === "ready";
    const withData = tablesOk ? t.items.filter(x => x.rows > 0).length : null;
    const factRows = tablesOk ? t.items.filter(x => x.type === "Fact").reduce((a, x) => a + x.rows, 0) : null;
    const cubeText = { OPERATIONAL: "Sẵn sàng", METADATA_MISMATCH: "Cần đồng bộ metadata", UNAVAILABLE: "Không khả dụng" };
    setKpis("status-kpis",
      kpi({ label: "SQL Server DWH", value: tablesOk ? "Đã kết nối" : t.state === "forbidden" ? "Không có quyền" : "Mất kết nối", format: "text", icon: "fa-database", note: tablesOk ? "DWH_KhangNghi" : t.message }) +
      kpi({ label: "Bảng có dữ liệu", value: tablesOk ? `${withData} / ${t.items.length}` : null, format: "text", icon: "fa-table", note: tablesOk && withData === 0 ? "Kho dữ liệu đang trống" : "" }) +
      kpi({ label: "Bản ghi trong bảng Fact", value: factRows, icon: "fa-layer-group", note: "Tổng 4 bảng Fact" }) +
      kpi({ label: "Máy chủ SSAS", value: s.state !== "ready" ? null : s.connected ? "Đã kết nối" : "Chưa kết nối", format: "text", icon: "fa-cubes", note: s.state === "ready" ? s.server : s.message }) +
      kpi({ label: "Khối dữ liệu", value: s.state === "ready" ? (cubeText[s.cubeStatus] || s.cubeStatus || null) : null, format: "text", icon: "fa-cube", note: s.state === "ready" ? s.cube : "" })
    );

    document.getElementById("status-checked-at").textContent = `Kiểm tra lúc ${d.checkedAt.toLocaleTimeString("vi-VN")}`;
    if (Comp.applySectionState("status-tables", t)) {
      mountTable("status-tables", "status-tables", [
        { key: "name", label: "Bảng", render: r => `<code>${esc(r.name)}</code>` },
        { key: "type", label: "Loại" },
        { key: "rows", label: "Số bản ghi", format: "number" },
        { key: "lastUpdated", label: "Cập nhật lần cuối", render: r => esc(r.lastUpdated ? new Date(r.lastUpdated).toLocaleString("vi-VN") : "—") },
        { key: "status", label: "Trạng thái", render: r => r.rows > 0 ? Comp.statusBadge("success", "Có dữ liệu") : Comp.statusBadge("warning", "Trống") }
      ], t.items);
    }
    const ssasEl = document.getElementById("status-ssas");
    if (s.state !== "ready") {
      ssasEl.innerHTML = Comp.renderState(s.state, { message: s.message, compact: true });
    } else {
      ssasEl.innerHTML = `
        <div class="detail-grid is-single">
          ${Comp.field("Máy chủ", s.server)}
          ${Comp.field("Kết nối", s.connected ? "Đã kết nối" : "Chưa kết nối")}
          ${Comp.field("Catalog", s.catalog ? `${s.catalog}${s.connected ? (s.catalogExists ? "" : " (chưa có trên máy chủ)") : ""}` : "")}
          ${Comp.field("Khối", s.cube)}
          ${Comp.field("Trạng thái khối", cubeText[s.cubeStatus] || s.cubeStatus)}
        </div>
        ${s.error ? `<div class="tech-note"><strong>Chi tiết kỹ thuật</strong><code>${esc(s.error)}</code></div>` : ""}
        ${s.action ? `<p class="table-note">${esc(s.action)}</p>` : ""}`;
    }
  }

  async function renderEtlLogs(filters, isCurrent) {
    setLoading(["etl-log-table"]);
    const sec = await Svc.getEtlLogs(50);
    if (!isCurrent()) return;
    if (Comp.applySectionState("etl-log-table", sec, "Chưa có lần chạy ETL nào được ghi nhận trong Sys_EtlLog.")) {
      const tone = { Success: ["success", "Thành công"], Failed: ["danger", "Thất bại"], Running: ["info", "Đang chạy"] };
      mountTable("etl-logs", "etl-log-table", [
        { key: "start", label: "Bắt đầu", render: r => esc(r.start ? new Date(r.start).toLocaleString("vi-VN") : "—") },
        { key: "end", label: "Kết thúc", render: r => esc(r.end ? new Date(r.end).toLocaleString("vi-VN") : "—") },
        { key: "status", label: "Trạng thái", render: r => { const x = tone[r.status]; return x ? Comp.statusBadge(x[0], x[1]) : esc(r.status || "—"); } },
        { key: "rows", label: "Bản ghi nạp", format: "number" },
        { key: "by", label: "Người chạy" },
        { key: "error", label: "Lỗi" }
      ], sec.items);
    }
  }

  // --------------------------------------------------------------------------
  // 17. CÀI ĐẶT
  // --------------------------------------------------------------------------
  function renderSettings() {
    const theme = document.body.getAttribute("data-theme") || "light";
    document.querySelectorAll("[data-theme-choice]").forEach(b => b.classList.toggle("active", b.dataset.themeChoice === theme));
    if (Router.updateNavigationUI) Router.updateNavigationUI();
  }

  function setTheme(theme) {
    document.body.setAttribute("data-theme", theme);
    try { localStorage.setItem("kn_theme", theme); } catch (e) { /* bỏ qua */ }
    const btn = document.getElementById("btn-theme-toggle");
    if (btn) btn.innerHTML = theme === "dark" ? '<i class="fa-solid fa-sun"></i>' : '<i class="fa-regular fa-moon"></i>';
    renderCurrentView();
  }

  async function changePassword() {
    const oldPw = document.getElementById("pw-old").value;
    const newPw = document.getElementById("pw-new").value;
    const confirm = document.getElementById("pw-confirm").value;
    const msg = document.getElementById("pw-message");
    if (newPw !== confirm) {
      msg.innerHTML = Comp.renderState("error", { title: "Mật khẩu nhập lại không khớp", compact: true });
      return;
    }
    const btn = document.getElementById("pw-submit");
    btn.disabled = true;
    const res = await window.KhangNghiApiClient.post("/auth/change-password", { oldPassword: oldPw, newPassword: newPw });
    btn.disabled = false;
    const body = res.data || {};
    if (res.success) {
      msg.innerHTML = `<p class="form-success"><i class="fa-solid fa-circle-check"></i> ${esc(body.message || "Đã đổi mật khẩu.")}</p>`;
      ["pw-old", "pw-new", "pw-confirm"].forEach(id => { document.getElementById(id).value = ""; });
    } else {
      msg.innerHTML = Comp.renderState("error", { title: body.message || res.error || "Không đổi được mật khẩu", compact: true });
    }
  }

  // --------------------------------------------------------------------------
  // TRẠNG THÁI KẾT NỐI DWH (topbar + sidebar)
  // --------------------------------------------------------------------------
  async function checkConnection() {
    const auth = window.KhangNghiAuth;
    if (!auth || !auth.isAuthenticated()) return;
    const r = await Svc.getConnectionStatus();
    const badge = document.getElementById("lineage-badge");
    const text = document.getElementById("dwh-status-text");
    const nav = document.getElementById("nav-dwh-badge");
    const modalText = document.getElementById("lineage-status-text");
    const map = {
      ready: ["is-ok", "DWH đã kết nối", "Kết nối SQL Server thành công, kho dữ liệu đã có giao dịch bán hàng."],
      empty: ["is-warning", "DWH chưa có dữ liệu", "Kết nối SQL Server thành công nhưng Fact_Sales chưa có giao dịch."],
      error: ["is-error", "Mất kết nối DWH", r.message || "Không truy vấn được kho dữ liệu."]
    };
    const [cls, label, detail] = map[r.state] || map.error;
    if (badge) badge.className = `data-status-indicator ${cls}`;
    if (text) text.textContent = label;
    if (modalText) modalText.textContent = detail;
    if (nav) {
      nav.hidden = r.state === "ready";
      nav.textContent = r.state === "empty" ? "Trống" : "Lỗi";
      nav.className = `nav-badge ${r.state === "empty" ? "is-warning" : "is-error"}`;
    }
  }

  // --------------------------------------------------------------------------
  // TRỢ LÝ AI (NLQ)
  // --------------------------------------------------------------------------
  function openCopilot() {
    const overlay = document.getElementById("copilot-modal");
    if (overlay) overlay.classList.add("active");
    const input = document.getElementById("copilot-input");
    if (input) setTimeout(() => input.focus(), 50);
  }

  function chatAppend(role, html) {
    const body = document.getElementById("copilot-chat-body");
    const div = document.createElement("div");
    div.className = `chat-msg is-${role}`;
    div.innerHTML = `<div class="chat-bubble">${html}</div>`;
    body.appendChild(div);
    body.scrollTop = body.scrollHeight;
    return div;
  }

  // Tên cột và định dạng cho dữ liệu NLQ (khóa từ SQL backend)
  const NLQ_COLUMNS = {
    year: ["Năm", "text"], revenue: ["Doanh thu", "sales"], profit: ["Lợi nhuận", "sales"], quantity: ["Số lượng", "number"],
    totalquantity: ["Số lượng", "number"], productname: ["Sản phẩm", "text"], customername: ["Khách hàng", "text"],
    customerid: ["Mã KH", "text"], segment: ["Phân khúc", "text"], ordercount: ["Số đơn", "number"], category: ["Danh mục", "text"],
    subcategory: ["Nhóm", "text"], suppliername: ["Nhà cung cấp", "text"], totalpurchaseamount: ["Chi phí mua", "vnd"],
    shippername: ["Đơn vị vận chuyển", "text"], totalshippingcost: ["Chi phí vận chuyển", "sales"], quantityonhand: ["Tồn", "number"],
    reorderlevel: ["Mức đặt lại", "number"], deficit: ["Thiếu hụt", "number"], location: ["Vị trí", "text"]
  };

  // Bảng kết quả gọn từ dữ liệu NLQ trả về (tối đa 10 dòng, 4 cột)
  function chatTable(rows) {
    const keys = Object.keys(rows[0]).filter(k => !/key$|^statuscolor$|^recommendation$/i.test(k)).slice(0, 4);
    const cols = keys.map(k => {
      const [label, f] = NLQ_COLUMNS[k.toLowerCase()] || [k, typeof rows[0][k] === "number" ? "number" : "text"];
      const format = f === "sales" ? CUR : f;
      return { key: k, label, render: r => esc(fv(r[k], format)), align: format === "text" ? "" : "right" };
    });
    const more = rows.length > 10 ? `<div class="cell-sub">Hiển thị 10 / ${rows.length} dòng.</div>` : "";
    return Comp.renderTable(cols, rows.slice(0, 10)) + more;
  }

  async function askCopilot(text) {
    if (!text) return;
    openCopilot();
    chatAppend("user", esc(text));
    const input = document.getElementById("copilot-input");
    if (input) input.value = "";
    const loading = chatAppend("bot", `<i class="fa-solid fa-spinner fa-spin"></i> Đang truy vấn kho dữ liệu…`);

    const res = await window.KhangNghiApiClient.get("/nlq/query", { q: text });
    loading.remove();
    if (!res.success) {
      chatAppend("bot", `${Comp.statusBadge("danger", "Chưa truy vấn được dữ liệu")}<p>${esc(res.status === 403 ? "Vai trò hiện tại không có quyền dùng trợ lý." : (res.data && res.data.answer) || res.error || "Lỗi kết nối API.")}</p>`);
      return;
    }
    const answer = (res.data && (res.data.answer ?? res.data.Answer)) || "";
    const data = res.data ? (res.data.data ?? res.data.Data) : null;
    const source = `<div class="chat-source"><i class="fa-solid fa-database"></i> Nguồn: kho DWH_KhangNghi (truy vấn trực tiếp)</div>`;

    if (data === null || data === undefined) {
      // Backend không nhận diện được câu hỏi -> không có truy vấn dữ liệu nào được chạy
      chatAppend("bot", `${Comp.statusBadge("neutral", "Chưa truy vấn dữ liệu")}<p>${esc(answer)}</p>`);
    } else if (Array.isArray(data)) {
      chatAppend("bot", data.length
        ? `<p>${esc(answer)}</p>${chatTable(data)}${source}`
        : `<p>${esc(answer)}</p>${Comp.statusBadge("warning", "Kho dữ liệu chưa có bản ghi phù hợp")}${source}`);
    } else {
      // Tổng quan: tự định dạng từ số liệu (ProfitMargin đã ở đơn vị %)
      const g = k => data[k] ?? data[k.charAt(0).toLowerCase() + k.slice(1)];
      chatAppend("bot", Number(g("TotalOrders")) > 0
        ? `<p>Tổng quan kinh doanh:</p><ul class="chat-list"><li>Doanh thu: <strong>${fv(g("TotalRevenue"), CUR)}</strong></li><li>Lợi nhuận: <strong>${fv(g("TotalProfit"), CUR)}</strong></li><li>Biên lợi nhuận: <strong>${fv(g("ProfitMargin"), "percent")}</strong></li><li>Số đơn hàng: <strong>${fv(g("TotalOrders"))}</strong></li></ul>${source}`
        : `${Comp.statusBadge("warning", "Kho dữ liệu chưa có giao dịch")}<p>Chưa có doanh thu để tổng hợp.</p>${source}`);
    }
  }

  // --------------------------------------------------------------------------
  // KHUNG ỨNG DỤNG
  // --------------------------------------------------------------------------
  function initShell() {
    const main = document.getElementById("main-content");
    if (main) main.addEventListener("transitionend", e => {
      if (e.target === main && e.propertyName === "margin-left") window.dispatchEvent(new Event("resize"));
    });

    let saved = null;
    try { saved = localStorage.getItem("kn_theme"); } catch (e) { /* bỏ qua */ }
    if (saved === "dark" || saved === "light") document.body.setAttribute("data-theme", saved);
    const themeBtn = document.getElementById("btn-theme-toggle");
    if (themeBtn) {
      themeBtn.innerHTML = document.body.getAttribute("data-theme") === "dark" ? '<i class="fa-solid fa-sun"></i>' : '<i class="fa-regular fa-moon"></i>';
      themeBtn.addEventListener("click", () => setTheme(document.body.getAttribute("data-theme") === "dark" ? "light" : "dark"));
    }

    const toggleBtn = document.getElementById("sidebar-toggle");
    if (toggleBtn) toggleBtn.addEventListener("click", () => {
      document.body.classList.toggle("sidebar-collapsed");
      setTimeout(() => window.dispatchEvent(new Event("resize")), 260);
    });

    const profileBtn = document.getElementById("btn-profile");
    const profileMenu = document.getElementById("profile-dropdown");
    if (profileBtn && profileMenu) {
      profileBtn.addEventListener("click", e => { e.stopPropagation(); profileMenu.classList.toggle("active"); });
      profileMenu.addEventListener("click", e => e.stopPropagation());
      document.addEventListener("click", () => profileMenu.classList.remove("active"));
    }

    const overlay = document.getElementById("copilot-modal");
    ["copilot-trigger", "topbar-copilot-btn"].forEach(id => {
      const b = document.getElementById(id);
      if (b) b.addEventListener("click", openCopilot);
    });
    const close = document.getElementById("copilot-close");
    if (close && overlay) close.addEventListener("click", () => overlay.classList.remove("active"));

    const lineage = document.getElementById("lineage-modal");
    const lineageBadge = document.getElementById("lineage-badge");
    const lineageClose = document.getElementById("lineage-close");
    if (lineageBadge && lineage) lineageBadge.addEventListener("click", () => lineage.classList.add("active"));
    if (lineageClose && lineage) lineageClose.addEventListener("click", () => lineage.classList.remove("active"));

    document.querySelectorAll(".modal-overlay").forEach(m => m.addEventListener("click", e => { if (e.target === m) m.classList.remove("active"); }));
    document.addEventListener("keydown", e => {
      if (e.key === "Escape") document.querySelectorAll(".modal-overlay.active").forEach(m => m.classList.remove("active"));
    });
  }

  // --------------------------------------------------------------------------
  // ĐĂNG NHẬP
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

    if (submitBtn) submitBtn.disabled = true;
    if (btnText) btnText.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Đang xác thực...`;
    if (alertBox) alertBox.style.display = "none";

    const res = await window.KhangNghiAuth.login(emailInput.value.trim(), passwordInput.value, rememberInput ? rememberInput.checked : false);

    if (submitBtn) submitBtn.disabled = false;
    if (btnText) btnText.textContent = "Đăng Nhập";

    if (res.success) {
      Comp.showToast(`Xin chào ${res.user.fullName}`);
      window.location.hash = "#/overview";
    } else if (alertBox && alertMsg) {
      alertMsg.textContent = res.message;
      alertBox.style.display = "flex";
    }
  }

  function fillDemoAccount(email) {
    const emailInput = document.getElementById("login-email");
    const passwordInput = document.getElementById("login-password");
    if (emailInput) emailInput.value = email;
    const auth = window.KhangNghiAuth;
    if (auth && auth.DEMO_ACCOUNTS) {
      const acc = auth.DEMO_ACCOUNTS.find(a => a.email.toLowerCase() === (email || "").toLowerCase());
      if (acc && passwordInput) passwordInput.value = acc.password;
    }
    handleLogin();
  }

  function togglePasswordVisibility() {
    const input = document.getElementById("login-password");
    const icon = document.getElementById("password-toggle-icon");
    if (!input || !icon) return;
    const show = input.type === "password";
    input.type = show ? "text" : "password";
    icon.className = show ? "fa-solid fa-eye-slash" : "fa-solid fa-eye";
  }

  async function switchDemoRole(roleName) {
    if (window.KhangNghiAuth && await window.KhangNghiAuth.switchDemoRole(roleName)) {
      const user = window.KhangNghiAuth.getCurrentUser();
      Comp.showToast(`Đã chuyển sang vai trò ${user.roleTitle}`, "info");
      if (Router) Router.updateNavigationUI();
      const menu = document.getElementById("profile-dropdown");
      if (menu) menu.classList.remove("active");
      renderCurrentView();
    }
  }

  window.KhangNghiApp = {
    handleLogin,
    fillDemoAccount,
    togglePasswordVisibility,
    switchDemoRole,
    refreshView,
    filterTable,
    exportTable,
    openPurchaseOrder,
    runOlap,
    swapOlapAxes,
    resetOlap: () => resetOlap(false),
    openOlapInStudio,
    switchMdxTab,
    loadMdxSample,
    clearMdx,
    formatMdx,
    runMdx,
    renderMdxLibrary,
    setForecastHorizon,
    updateWhatIf,
    resetWhatIf,
    saveWhatIfScenario,
    deleteScenario,
    renderQuestions,
    openQuestionMdx,
    setTheme,
    changePassword,
    openCopilot,
    askCopilot
  };
});
