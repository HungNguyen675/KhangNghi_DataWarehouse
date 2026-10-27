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
  // Khai báo trước renderCurrentView() bên dưới: tải lại trang ngay trên route sẽ gọi renderer trước khi chạy tới phần thân
  let crossCache = { items: [], eff: [] };   // Phân tích chéo: dữ liệu lần vẽ gần nhất cho cửa sổ diễn giải
  let currentMultiDimData = null;            // Phân tích đa chiều: bảng đang hiển thị

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
      "multi-dim": renderMultiDim,
      "olap-explorer": renderMultiDim,
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
    ], { format: CUR, onClick: filters.year === "ALL" ? openYearDetail : null }));
    draw("chart-ov-category", d.byCategory, () => Charts.donut("chart-ov-category", d.byCategory.items, { value: "revenue", format: CUR }));
    draw("chart-ov-region", d.byRegion, () => Charts.bar("chart-ov-region", [...d.byRegion.items].sort((a, b) => b.revenue - a.revenue), { value: "revenue", format: CUR, horizontal: true, max: 12, seriesName: "Doanh thu", height: 360 }));
  }

  // --------------------------------------------------------------------------
  // 2. BÁN HÀNG
  // --------------------------------------------------------------------------
  // --------------------------------------------------------------------------
  // CHI TIẾT NĂM - bấm vào mốc năm trên biểu đồ để xem doanh thu, lợi nhuận đến từ đâu
  // --------------------------------------------------------------------------
  const pctOf = (part, total) => (total ? (part / total) * 100 : null);
  const marginOf = (profit, revenue) => (revenue ? (profit / revenue) * 100 : null);
  const fmtOrDash = (v, format) => (v === null || v === undefined || Number.isNaN(v) ? null : Comp.formatValue(v, format));

  // Thêm tỷ trọng doanh thu và biên lợi nhuận; giữ nguyên thứ tự API trả về
  function withShares(items) {
    const total = items.reduce((acc, i) => acc + i.revenue, 0);
    return items.map(i => ({ ...i, share: pctOf(i.revenue, total), margin: marginOf(i.profit, i.revenue) }));
  }

  function breakdownTable(rows, title) {
    return Comp.renderTable([
      { label: title, render: r => Comp.escapeHtml(r.label) },
      { label: "Doanh thu", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.revenue, CUR) || "—") },
      { label: "% doanh thu", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.share, "percent") || "—") },
      { label: "Lợi nhuận", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.profit, CUR) || "—") },
      { label: "Biên LN", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.margin, "percent") || "—") },
      { label: "SL bán", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.quantity, "number") || "—") }
    ], rows, { empty: "Không có dữ liệu." });
  }

  function productTable(rows) {
    return Comp.renderTable([
      { label: "#", align: "right", render: r => r.rank },
      { label: "Sản phẩm", render: r => Comp.escapeHtml(r.label) },
      { label: "Danh mục", render: r => Comp.escapeHtml(r.category) },
      { label: "Doanh thu", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.revenue, CUR) || "—") },
      { label: "Lợi nhuận", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.profit, CUR) || "—") },
      { label: "Biên LN", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.margin, "percent") || "—") },
      { label: "SL bán", align: "right", render: r => Comp.escapeHtml(fmtOrDash(r.quantity, "number") || "—") }
    ], rows, { empty: "Không có sản phẩm." });
  }

  // Câu tóm tắt: doanh thu, lợi nhuận cả năm; danh mục chiếm tỷ trọng lớn nhất; quý cao nhất
  function yearSummary(year, s, categories, quarters) {
    const parts = [`Năm ${year} đạt doanh thu ${fmtOrDash(s.revenue, CUR)}, lợi nhuận ${fmtOrDash(s.profit, CUR)}, biên lợi nhuận ${fmtOrDash(s.margin, "percent")}.`];
    const topCat = categories[0];
    if (topCat) parts.push(`Danh mục đóng góp nhiều nhất là ${topCat.label} với ${fmtOrDash(topCat.share, "percent")} doanh thu.`);
    const bestQ = quarters.reduce((a, q) => (!a || q.revenue > a.revenue ? q : a), null);
    if (bestQ) parts.push(`Quý có doanh thu cao nhất là ${bestQ.label} (${fmtOrDash(bestQ.revenue, CUR)}).`);
    return parts.join(" ");
  }

  function renderYearDetail(d) {
    if (d.summary.state !== "ready") {
      return Comp.renderState(d.summary.state, { message: d.summary.message || `Không có dữ liệu năm ${d.year}.`, compact: true });
    }
    const s = d.summary.data;
    const categories = withShares(d.categories.items);
    const regions = withShares(d.regions.items);
    const quarters = withShares(d.quarters.items);
    const products = d.products.items.map((p, i) => ({ ...p, rank: i + 1, margin: marginOf(p.profit, p.revenue) }));
    const metrics = [
      ["Doanh thu", s.revenue, CUR],
      ["Lợi nhuận", s.profit, CUR],
      ["Biên lợi nhuận", s.margin, "percent"],
      ["Số đơn hàng", s.orders, "number"],
      ["Số lượng bán", s.quantity, "number"],
      ["Khách hàng", s.customers, "number"]
    ];
    return `
      <p class="detail-summary">${Comp.escapeHtml(yearSummary(d.year, s, categories, quarters))}</p>
      <div class="detail-grid">${metrics.map(([label, v, format]) => Comp.field(label, fmtOrDash(v, format))).join("")}</div>
      <h3 class="detail-section-title">Doanh thu theo quý</h3>
      ${breakdownTable(quarters, "Quý")}
      <h3 class="detail-section-title">Đóng góp theo danh mục</h3>
      ${breakdownTable(categories, "Danh mục")}
      <h3 class="detail-section-title">Đóng góp theo khu vực</h3>
      ${breakdownTable(regions, "Khu vực")}
      <h3 class="detail-section-title">Sản phẩm bán chạy nhất</h3>
      ${productTable(products)}`;
  }

  async function openYearDetail(label) {
    const year = parseInt(label, 10);
    if (!year) return;
    const body = Comp.openModal("year-detail-modal", `Chi tiết năm ${year}`, "Doanh thu, lợi nhuận và nguồn đóng góp trong năm");
    const d = await Svc.getYearDetail(year);
    body.innerHTML = renderYearDetail(d);
  }

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
    ], { format: CUR, onClick: filters.year === "ALL" ? openYearDetail : null }), `Không có doanh thu${yearText(filters)}.`);
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
  // Ngưỡng khớp với StockCoverSql (Q38/Q39): < 3 ngày sắp hết, > 7 ngày tồn thừa
  const STOCK_TONE = { "Sắp hết": "danger", "An toàn": "success", "Tồn thừa": "warning" };
  const STOCK_RULE = { "Sắp hết": "đủ bán dưới 3 ngày", "An toàn": "đủ bán từ 3 đến 7 ngày", "Tồn thừa": "đủ bán trên 7 ngày, hoặc không bán được trong 3 tháng" };
  const toneColor = tone => getComputedStyle(document.body).getPropertyValue(`--color-${tone}`).trim();
  const statusColor = status => toneColor(STOCK_TONE[status] || "info");
  const stockBadge = status => Comp.statusBadge(STOCK_TONE[status] || "neutral", status || "—");
  const fmtCover = r => (r.cover === null ? "Không bán" : `${r.cover.toFixed(1)} ngày`);
  const nf = (n, digits = 0) => Number(n || 0).toLocaleString("vi-VN", { maximumFractionDigits: digits });
  const RULE_LINE = `<p class="cross-rule"><span class="cross-dot tone-danger"></span>Sắp hết &lt; 3 ngày <span class="cross-dot tone-success"></span>An toàn 3–7 ngày <span class="cross-dot tone-warning"></span>Tồn thừa &gt; 7 ngày (chu kỳ nhập hàng theo tuần)</p>`;

  async function renderCrossAnalysis(filters, isCurrent) {
    setKpis("cross-kpis", Comp.renderSkeletonCards(4));
    setLoading(["chart-cross-status", "chart-cross-stockout", "chart-cross-overstock", "chart-cross-eff", "cross-cover-table"]);
    const d = await Svc.getCrossAnalysis();
    if (!isCurrent()) return;

    const allSame = d.cover.state !== "ready" && d.cover.state === d.efficiency.state;
    gate("cross-analysis", allSame ? d.cover : { state: "ready" }, { title: "Chưa đủ dữ liệu để phân tích chéo", emptyMessage: "Cần dữ liệu trong Fact_Sales, Fact_Inventory và Fact_Shipping." });
    if (allSame) return;

    const items = d.cover.items || [];
    const ready = d.cover.state === "ready";
    const byStatus = s => items.filter(r => r.status === s);
    const short = byStatus("Sắp hết");
    // API trả theo số ngày tăng dần (không bán xếp cuối) -> đảo lại để tồn nhiều nhất lên đầu
    const over = byStatus("Tồn thừa").reverse();
    const overCap = Math.max(14, ...over.filter(r => r.cover !== null).map(r => r.cover));
    const eff = [...(d.efficiency.items || [])].sort((a, b) => (b.ratio || 0) - (a.ratio || 0));
    crossCache = { items, eff };

    setKpis("cross-kpis",
      kpi({ label: "Sắp hết hàng", value: ready ? short.length : null, icon: "fa-box-open", note: "Đủ bán dưới 3 ngày" }) +
      kpi({ label: "An toàn", value: ready ? byStatus("An toàn").length : null, icon: "fa-circle-check", note: "Đủ bán 3–7 ngày" }) +
      kpi({ label: "Tồn thừa", value: ready ? over.length : null, icon: "fa-boxes-stacked", note: "Tồn trên 7 ngày bán" }) +
      kpi({ label: "Phí giao cao nhất", value: eff.length ? `${eff[0].label} · ${(eff[0].ratio || 0).toFixed(1)}%` : null, format: "text", icon: "fa-truck-fast", note: "% doanh thu" })
    );
    setScope("cross-eff", "% chi phí vận chuyển / doanh thu", d.efficiency);

    draw("chart-cross-status", d.cover, () => Charts.donut("chart-cross-status",
      ["Sắp hết", "An toàn", "Tồn thừa"].map(s => ({ label: s, value: byStatus(s).length })),
      { colorOf: x => statusColor(x.label), onClick: x => openStockStatus(x.label) }));
    draw("chart-cross-stockout", d.cover, () => Charts.bar("chart-cross-stockout", short,
      { value: "cover", horizontal: true, max: 10, seriesName: "Số ngày còn đủ bán", colorOf: () => toneColor("danger"), onClick: openStockCover }),
      "Không có mặt hàng nào sắp hết hàng.");
    // Không bán được trong 3 tháng (cover null) là tồn thừa nặng nhất -> vẽ ở mức trần của trục
    draw("chart-cross-overstock", d.cover, () => Charts.bar("chart-cross-overstock",
      over.map(r => ({ ...r, plot: r.cover === null ? overCap : r.cover, label: r.cover === null ? `${r.label} (không bán)` : r.label })),
      { value: "plot", horizontal: true, max: 10, seriesName: "Số ngày tồn kho", colorOf: () => toneColor("warning"), onClick: openStockCover }),
      "Không có mặt hàng nào tồn quá 7 ngày bán.");
    draw("chart-cross-eff", d.efficiency, () => Charts.bar("chart-cross-eff", eff,
      { value: "ratio", format: "percent", horizontal: true, max: 12, seriesName: "Chi phí VC / doanh thu", onClick: openShippingRegion }));

    if (Comp.applySectionState("cross-cover-table", d.cover, "Chưa có mặt hàng nào có tồn kho hoặc doanh số.", "Không có dữ liệu")) {
      mountTable("cross-cover", "cross-cover-table", [
        { key: "label", label: "Sản phẩm" },
        { key: "category", label: "Danh mục" },
        { key: "avgMonthly", label: "Bán / tháng", format: "number" },
        { key: "onHand", label: "Tồn", format: "number" },
        { key: "cover", label: "Số ngày tồn kho", align: "right", render: fmtCover },
        { key: "status", label: "Tình trạng", render: r => stockBadge(r.status) }
      ], items, { limit: 15, onRowClick: "KhangNghiApp.openStockCover", rowKey: "productKey" });
    }
  }

  // Diễn giải một mặt hàng: 3 bước tính từ số liệu gốc trong Fact_Inventory và Fact_Sales
  async function openStockCover(key) {
    const item = typeof key === "object" ? key : crossCache.items.find(r => String(r.productKey) === String(key));
    if (!item || item.productKey === null) return;
    const body = Comp.openModal("stock-cover-modal", item.label.replace(/ \(không bán\)$/, ""), "Vì sao có con số này?");
    const d = await Svc.getStockCoverDetail(item.productKey);
    if (!d.success) {
      body.innerHTML = Comp.renderState("error", { message: d.message, compact: true });
      return;
    }
    const onHand = d.stock.reduce((sum, r) => sum + r.onHand, 0);
    const sold = d.sales.reduce((sum, r) => sum + r.quantity, 0);
    const perDay = sold / 90;
    const days = perDay > 0 ? onHand / perDay : null;
    const b = t => `<b>${Comp.escapeHtml(t)}</b>`;
    const stockRows = d.stock.map(r => ({ ...r, date: r.date ? new Date(r.date).toLocaleDateString("vi-VN") : "—", place: [r.warehouse, r.city].filter(Boolean).join(" · ") }));
    const salesRows = d.sales.map(r => ({ ...r, period: `Tháng ${r.month}/${r.year}` }));
    body.innerHTML = `
      <p class="detail-summary">Còn đủ bán ${b(days === null ? "—" : nf(days, 1) + " ngày")} ${stockBadge(item.status)} vì ${b(STOCK_RULE[item.status] || "")}.</p>
      <div class="cross-step">
        <span class="cross-step-no">1</span>
        <div class="cross-step-body">
          <h4>Tồn hiện tại <small>Fact_Inventory · lần chốt tồn mới nhất của mỗi kho</small></h4>
          ${Comp.renderTable([{ key: "place", label: "Kho" }, { key: "date", label: "Ngày chốt" }, { key: "onHand", label: "Tồn", format: "number" }, { key: "reorder", label: "Mức đặt lại", format: "number" }], stockRows, { empty: "Mặt hàng chưa có bản ghi tồn kho." })}
          <p class="cross-calc">${stockRows.length ? stockRows.map(r => nf(r.onHand)).join(" + ") : "0"} = ${b(nf(onHand))} sản phẩm</p>
        </div>
      </div>
      <div class="cross-step">
        <span class="cross-step-no">2</span>
        <div class="cross-step-body">
          <h4>Tốc độ bán <small>Fact_Sales · 3 tháng gần nhất có dữ liệu</small></h4>
          ${Comp.renderTable([{ key: "period", label: "Tháng" }, { key: "quantity", label: "Số lượng bán", format: "number" }], salesRows)}
          <p class="cross-calc">(${salesRows.map(r => nf(r.quantity)).join(" + ")}) ÷ 90 ngày = ${b(nf(perDay, 1))} sản phẩm/ngày</p>
        </div>
      </div>
      <div class="cross-step">
        <span class="cross-step-no">3</span>
        <div class="cross-step-body">
          <h4>Số ngày tồn kho <small>Tồn hiện tại ÷ Bán mỗi ngày</small></h4>
          <p class="cross-calc">${nf(onHand)} ÷ ${nf(perDay, 1)} = ${b(days === null ? "không bán được" : nf(days, 1) + " ngày")}</p>
        </div>
      </div>
      ${RULE_LINE}`;
  }

  // Bấm một phần của biểu đồ tròn: quy tắc phân loại và các mặt hàng thuộc nhóm
  function openStockStatus(status) {
    const rows = crossCache.items.filter(r => r.status === status);
    const body = Comp.openModal("stock-status-modal", `${status}: ${nf(rows.length)} mặt hàng`, `Quy tắc: ${STOCK_RULE[status] || ""}`);
    body.innerHTML = `
      <p class="detail-summary">Số ngày tồn kho = Tồn hiện tại (Fact_Inventory) ÷ Bán trung bình mỗi ngày trong 3 tháng gần nhất (Fact_Sales). Bấm một dòng để xem cách tính.</p>
      ${Comp.renderTable([
        { key: "label", label: "Sản phẩm" },
        { key: "onHand", label: "Tồn", format: "number" },
        { key: "avgMonthly", label: "Bán / tháng", format: "number" },
        { key: "cover", label: "Số ngày tồn kho", align: "right", render: fmtCover }
      ], rows, { onRowClick: "KhangNghiApp.openStockCover", rowKey: "productKey" })}
      ${RULE_LINE}`;
  }

  // Bấm một khu vực ở Q40: phép chia phí vận chuyển cho doanh thu
  function openShippingRegion(key) {
    const r = typeof key === "object" ? key : crossCache.eff.find(x => x.label === key);
    if (!r) return;
    const rank = crossCache.eff.indexOf(r) + 1;
    const fv = v => Comp.formatValue(v, CUR);
    const body = Comp.openModal("shipping-region-modal", r.label, "Vì sao có con số này?");
    body.innerHTML = `
      <p class="detail-summary">Cứ 100 đồng doanh thu thì tốn <b>${nf(r.ratio, 1)} đồng</b> phí giao hàng, cao thứ ${rank}/${crossCache.eff.length} khu vực.</p>
      <div class="detail-grid">
        ${Comp.field("Doanh thu (Fact_Sales)", fv(r.revenue))}
        ${Comp.field("Phí vận chuyển (Fact_Shipping)", fv(r.shippingCost))}
        ${Comp.field("Lợi nhuận", fv(r.profit))}
        ${Comp.field("Lợi nhuận sau phí giao", fv(r.netProfit))}
      </div>
      <p class="cross-calc">${Comp.escapeHtml(fv(r.shippingCost))} ÷ ${Comp.escapeHtml(fv(r.revenue))} × 100 = <b>${nf(r.ratio, 1)}%</b></p>`;
  }

  // --------------------------------------------------------------------------
  // 11. KHÁM PHÁ OLAP
  // --------------------------------------------------------------------------
  // 11. PHÂN TÍCH KINH DOANH ĐA CHIỀU (MULTI-DIMENSIONAL BUSINESS ANALYTICS)
  // --------------------------------------------------------------------------
  async function renderMultiDim() {
    // Tự động tải phân tích mặc định ngay khi vào trang, không để màn hình trống
    if (!currentMultiDimData) {
      await loadMultiDimAnalysis();
    }
  }

  // Tải và hiển thị kết quả phân tích đa chiều tự động ngay khi chọn
  async function loadMultiDimAnalysis() {
    const measureEl = document.getElementById("multi-dim-measure");
    const rowsEl = document.getElementById("multi-dim-rows");
    const colsEl = document.getElementById("multi-dim-cols");
    const btn = document.getElementById("multi-dim-run-btn");

    const measure = measureEl ? measureEl.value : "revenue";
    const row = rowsEl ? rowsEl.value : "category";
    const col = colsEl ? colsEl.value : "year";

    const tableWrap = document.getElementById("multi-dim-table-wrap");
    const chartTitle = document.getElementById("multi-dim-chart-title");
    const chartDesc = document.getElementById("multi-dim-chart-desc");
    const tableTitle = document.getElementById("multi-dim-table-title");

    if (btn) {
      btn.disabled = true;
      btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Đang tải...`;
    }

    if (tableWrap && !currentMultiDimData) {
      tableWrap.innerHTML = Comp.renderState("loading", { message: "Đang truy vấn số liệu đa chiều từ kho DWH...", compact: true });
    }

    const res = await Svc.getMultiDimAnalysis(measure, row, col);

    if (btn) {
      btn.disabled = false;
      btn.innerHTML = `<i class="fa-solid fa-arrows-rotate"></i> Xem phân tích`;
    }

    if (res.state !== "ready" || !res.data) {
      if (tableWrap) {
        tableWrap.innerHTML = Comp.renderState("error", {
          title: "Không thể tải số liệu phân tích",
          message: res.message || "Đã xảy ra lỗi khi truy vấn số liệu.",
          compact: true
        });
      }
      return;
    }

    const d = res.data;
    currentMultiDimData = d;

    // 1. Thẻ KPI tổng hợp
    const s = d.summary || {};
    const fmt = d.measureFormat === "number" ? "number" : CUR;

    const totalEl = document.getElementById("multi-dim-kpi-total");
    const topEl = document.getElementById("multi-dim-kpi-top");
    const scaleEl = document.getElementById("multi-dim-kpi-scale");
    const kpi1Label = document.getElementById("multi-dim-kpi1-label");
    const kpi2Label = document.getElementById("multi-dim-kpi2-label");

    if (kpi1Label) kpi1Label.textContent = `Tổng ${d.measure}`;
    if (totalEl) totalEl.textContent = Comp.formatValue(s.total, fmt);
    if (kpi2Label) kpi2Label.textContent = `${d.rowLabel} cao nhất`;
    if (topEl) {
      topEl.innerHTML = s.topRow ? `<strong>${esc(s.topRow)}</strong> <span style="font-size:13px; font-weight:normal; color:var(--text-secondary);">(${Comp.formatValue(s.topRowValue, fmt)})</span>` : "—";
    }
    if (scaleEl) {
      scaleEl.textContent = d.isMultiDim ? `${s.rowCount} ${d.rowLabel} × ${s.colCount} ${d.colLabel}` : `${s.rowCount} phân nhóm`;
    }

    // 2. Tiêu đề biểu đồ và bảng
    if (chartTitle) chartTitle.textContent = `Biểu đồ ${d.measure} theo ${d.rowLabel} ${d.isMultiDim ? "và " + d.colLabel : ""}`;
    if (chartDesc) chartDesc.textContent = d.isMultiDim ? `So sánh đối chiếu ${d.measure} giữa các ${d.rowLabel} qua từng ${d.colLabel}` : `Xếp hạng mức độ đóng góp ${d.measure} theo ${d.rowLabel}`;
    if (tableTitle) tableTitle.textContent = `Bảng số liệu ma trận ${d.measure}`;

    // 3. Render biểu đồ ApexCharts
    renderMultiDimChart(d);

    // 4. Render Bảng ma trận số liệu
    if (tableWrap && d.table) {
      const cols = d.table.columns.map(c => ({
        key: c.key,
        label: c.label,
        format: c.format === "number" ? "number" : c.format === "percent" ? "percent" : c.key === "RowName" || c.key === "label" ? "text" : CUR,
        render: c.key === "RowName" || c.key === "label"
          ? r => `<strong>${esc(r[c.key])}</strong>`
          : c.key === "Total"
            ? r => `<strong>${Comp.formatValue(r[c.key], fmt)}</strong>`
            : null
      }));
      tableWrap.innerHTML = Comp.renderTable(cols, d.table.rows);
    }
  }

  function renderMultiDimChart(d) {
    if (typeof Charts === "undefined") return;
    Charts.destroy("multi-dim-chart-canvas");

    const canvas = document.getElementById("multi-dim-chart-canvas");
    if (!canvas) return;

    const fmt = d.measureFormat === "number" ? "number" : CUR;

    if (d.isMultiDim && d.chart && d.chart.type === "groupedBar") {
      Charts.groupedBar("multi-dim-chart-canvas", d.chart.categories, d.chart.series, {
        format: fmt,
        height: 340
      });
    } else if (d.chart && d.chart.items) {
      Charts.bar("multi-dim-chart-canvas", d.chart.items, {
        horizontal: true,
        format: fmt,
        seriesName: d.measure,
        height: Math.max(280, d.chart.items.length * 34)
      });
    }
  }

  function swapMultiDimAxes() {
    const rowsEl = document.getElementById("multi-dim-rows");
    const colsEl = document.getElementById("multi-dim-cols");
    if (!rowsEl || !colsEl) return;
    if (colsEl.value === "none") {
      Comp.showToast("Vui lòng chọn một chiều cho cột trước khi đổi trục.", "info");
      return;
    }
    const temp = rowsEl.value;
    rowsEl.value = colsEl.value;
    colsEl.value = temp;
    loadMultiDimAnalysis();
  }

  function resetMultiDim() {
    const measureEl = document.getElementById("multi-dim-measure");
    const rowsEl = document.getElementById("multi-dim-rows");
    const colsEl = document.getElementById("multi-dim-cols");
    if (measureEl) measureEl.value = "revenue";
    if (rowsEl) rowsEl.value = "category";
    if (colsEl) colsEl.value = "year";
    loadMultiDimAnalysis();
  }

  function exportMultiDimToCSV() {
    if (!currentMultiDimData || !currentMultiDimData.table) {
      Comp.showToast("Chưa có dữ liệu để xuất.", "warning");
      return;
    }
    const t = currentMultiDimData.table;
    const cols = t.columns.map(c => ({ key: c.key, label: c.label }));
    Svc.exportToCSV(t.rows, `BaoCao_PhanTichDaChieu_${Date.now()}.csv`, cols);
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

  // Kịch bản mẫu: mỗi kịch bản là một quyết định nghiệp vụ thường gặp
  const WHATIF_PRESETS = {
    promo: { growth: 10, discount: 2, ship: 0 },
    slow: { growth: -10, discount: 0, ship: 0 },
    fuel: { growth: 0, discount: 0, ship: 20 },
    carrier: { growth: 0, discount: 0, ship: -15 }
  };

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

  function applyWhatIfPreset(key) {
    const p = WHATIF_PRESETS[key];
    if (!p) return;
    document.getElementById("slider-growth").value = p.growth;
    document.getElementById("slider-discount").value = p.discount;
    document.getElementById("slider-ship").value = p.ship;
    updateWhatIf();
  }

  function updateWhatIf() {
    const p = whatIfInputs();
    const sign = v => (v > 0 ? "+" : "");
    const signed = v => `${v >= 0 ? "+" : "−"}${fv(Math.abs(v), CUR)}`;
    const setText = (id, text) => { const el = document.getElementById(id); if (el) el.textContent = text; };
    setText("val-growth", `${sign(p.growth)}${p.growth}%`);
    setText("val-discount", `${p.discount}%`);
    setText("val-ship", `${sign(p.ship)}${p.ship}%`);
    document.querySelectorAll("#whatif-presets .whatif-preset").forEach(btn => {
      const x = WHATIF_PRESETS[btn.dataset.preset];
      btn.classList.toggle("is-active", !!x && x.growth === p.growth && x.discount === p.discount && x.ship === p.ship);
    });
    if (!whatIfBase) return;

    const b = whatIfBase;
    const r = computeWhatIf(b, p);
    const ship = b.shippingCost || 0;
    setText("hint-growth", `Hiện tại ${fv(b.revenue, CUR)}${p.growth ? ` → ${fv(r.revenue, CUR)}` : ""}`);
    setText("hint-discount", p.discount ? `Giảm giá cho khách ${fv(r.revenue * p.discount / 100, CUR)}` : "Chưa giảm giá thêm");
    setText("hint-ship", ship ? `Hiện tại ${fv(ship, CUR)}${p.ship ? ` → ${fv(ship * (1 + p.ship / 100), CUR)}` : ""}` : "Chưa có chi phí vận chuyển thực tế");

    // Cầu nối lợi nhuận: hiện tại ± từng yếu tố = giả định (cộng lại khớp computeWhatIf)
    const parts = [
      { label: p.growth >= 0 ? `Lời thêm khi doanh thu ${sign(p.growth)}${p.growth}%` : `Lời mất đi khi doanh thu ${p.growth}%`, value: b.profit * p.growth / 100 },
      { label: `Chiết khấu thêm ${p.discount}% cho khách`, value: -r.revenue * p.discount / 100 },
      { label: `Phí vận chuyển ${sign(p.ship)}${p.ship}%`, value: -ship * p.ship / 100 }
    ].filter(x => Math.abs(x.value) > 0.5);
    const maxAbs = Math.max(...parts.map(x => Math.abs(x.value)), 1);
    const pct = b.profit ? (r.diff / Math.abs(b.profit)) * 100 : 0;
    const flat = Math.abs(pct) < 0.5;
    const tone = flat ? "flat" : r.diff > 0 ? "up" : "down";

    let headline = "Chưa thay đổi gì";
    let reason = "Chọn một kịch bản mẫu ở trên hoặc kéo thanh trượt bên trái để bắt đầu.";
    if (parts.length) {
      headline = flat ? "Lợi nhuận gần như không đổi" : r.diff > 0 ? `Lợi nhuận tăng ${fv(r.diff, CUR)}` : `Lợi nhuận giảm ${fv(-r.diff, CUR)}`;
      const pos = parts.filter(x => x.value > 0).sort((a, c) => c.value - a.value)[0];
      const neg = parts.filter(x => x.value < 0).sort((a, c) => a.value - c.value)[0];
      if (pos && neg) reason = `${pos.label} (${signed(pos.value)}) ${pos.value + neg.value >= 0 ? "lớn hơn" : "không bù được"} ${neg.label.toLowerCase()} (${signed(neg.value)}).`;
      else reason = `Do ${(pos || neg).label.toLowerCase()} (${signed((pos || neg).value)}).`;
    }
    // Mức chiết khấu tối đa để lợi nhuận không thấp hơn hiện tại, với mức doanh thu đang chọn
    const breakEven = p.growth > 0 ? ((b.profit * p.growth / 100) - ship * p.ship / 100) / r.revenue * 100 : null;

    const row = (label, value, cls, bar) => `
      <div class="whatif-bridge-row ${cls}">
        <span class="whatif-bridge-label">${esc(label)}</span>
        <span class="whatif-bridge-bar">${bar || ""}</span>
        <strong>${esc(value)}</strong>
      </div>`;
    document.getElementById("whatif-results").innerHTML = `
      <div class="whatif-verdict is-${tone}">
        <div class="whatif-verdict-main">${esc(headline)}${parts.length ? `<small>${flat ? "≈ 0%" : `${pct > 0 ? "+" : ""}${pct.toFixed(1)}%`}</small>` : ""}</div>
        <p>${esc(reason)}</p>
      </div>
      <div class="whatif-bridge">
        ${row("Lợi nhuận hiện tại", fv(b.profit, CUR), "is-base")}
        ${parts.map(x => row(x.label, signed(x.value), x.value >= 0 ? "is-up" : "is-down",
          `<i style="width:${Math.max(4, Math.abs(x.value) / maxAbs * 100)}%"></i>`)).join("")}
        ${row("Lợi nhuận giả định", fv(r.profit, CUR), "is-total")}
      </div>
      <div class="whatif-compare">
        ${Comp.field("Doanh thu", p.growth ? `${fv(b.revenue, CUR)} → ${fv(r.revenue, CUR)}` : fv(b.revenue, CUR))}
        ${Comp.field("Biên lợi nhuận", parts.length ? `${fv(b.margin, "percent")} → ${fv(r.margin, "percent")}` : fv(b.margin, "percent"))}
      </div>
      ${breakEven !== null ? `<p class="whatif-tip"><i class="fa-solid fa-lightbulb"></i><span>Với doanh thu ${sign(p.growth)}${p.growth}%, chiết khấu tối đa khoảng <b>${breakEven > 0 ? breakEven.toFixed(1) : 0}%</b> thì lợi nhuận mới không thấp hơn hiện tại.</span></p>` : ""}`;
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

  const QUICK_CHART_IDS = new Set(["MDX-19", "MDX-20", "MDX-21", "MDX-22", "MDX-23", "MDX-24"]);

  function renderQuestions() {
    const el = document.getElementById("question-table");
    if (!el || !mdxCatalog) return;
    if (mdxCatalog.state !== "ready") { el.innerHTML = Comp.renderState(mdxCatalog.state, { message: mdxCatalog.message, compact: true }); return; }
    const q = (document.getElementById("question-search")?.value || "").toLowerCase();
    const sec = document.getElementById("question-section")?.value || "";
    const rows = mdxCatalog.items.filter(x =>
      (!q || `${x.id} ${x.title} ${x.purpose}`.toLowerCase().includes(q)) &&
      (!sec || x.section === sec));
    el.innerHTML = Comp.renderTable([
      { key: "id", label: "Mã", render: r => `<code>${esc(r.id)}</code>` },
      { key: "title", label: "Câu hỏi & Bài toán nghiệp vụ", render: r => `<strong>${esc(r.title)}</strong><div class="cell-sub">${esc(r.purpose)}</div>` },
      { key: "section", label: "Phân hệ" },
      { key: "", label: "Thao tác", render: r => {
        if (QUICK_CHART_IDS.has(r.id)) {
          return `<button type="button" class="btn btn-sm btn-primary" onclick="KhangNghiApp.openQuickChart('${esc(r.id)}')"><i class="fa-solid fa-chart-pie"></i> Xem biểu đồ</button>`;
        }
        const route = SECTION_ROUTE[r.section] || "sales";
        return `<a class="btn btn-sm" href="#/${route}"><i class="fa-solid fa-chart-simple"></i> Mở báo cáo</a>`;
      } }
    ], rows, { empty: "Không có câu hỏi khớp bộ lọc." });
  }

  function openQuestionMdx(id) {
    if (!mdxCatalog || !mdxCatalog.items) return;
    const q = mdxCatalog.items.find(x => x.id === id);
    if (!q) return;
    const modal = document.getElementById("mdx-viewer-modal");
    const titleEl = document.getElementById("mdx-modal-title");
    const purpEl = document.getElementById("mdx-modal-purpose");
    const codeEl = document.getElementById("mdx-modal-code");
    if (titleEl) titleEl.innerHTML = '<i class="fa-solid fa-code"></i> ' + esc(q.id) + ' · ' + esc(q.title);
    if (purpEl) purpEl.textContent = q.purpose || "Cú pháp truy vấn khối dữ liệu SSAS";
    if (codeEl) codeEl.textContent = q.mdx || "/* Chưa có mã MDX */";
    if (modal) modal.classList.add("active");
  }

  function closeMdxModal() {
    const modal = document.getElementById("mdx-viewer-modal");
    if (modal) modal.classList.remove("active");
  }

  function copyMdxQuery() {
    const codeEl = document.getElementById("mdx-modal-code");
    if (codeEl && navigator.clipboard) {
      navigator.clipboard.writeText(codeEl.textContent).then(() => {
        const btn = document.getElementById("mdx-modal-copy-btn");
        if (btn) {
          const orig = btn.innerHTML;
          btn.innerHTML = '<i class="fa-solid fa-check"></i> Đã sao chép';
          setTimeout(() => { btn.innerHTML = orig; }, 1800);
        }
      });
    }
  }


  // --------------------------------------------------------------------------
  // 15b. QUICK CHART MODAL CHO MDX-19 ĐẾN MDX-24
  // --------------------------------------------------------------------------
  function closeQuickChart() {
    const modal = document.getElementById("quick-chart-modal");
    if (modal) modal.classList.remove("active");
    document.body.style.overflow = "";
    if (typeof Charts !== "undefined") {
      Charts.destroy("quick-chart-canvas");
    }
    document.removeEventListener("keydown", handleQuickChartKeyDown);
  }

  function handleQuickChartKeyDown(e) {
    if (e.key === "Escape") {
      closeQuickChart();
    }
  }

  async function openQuickChart(mdxId) {
    const modal = document.getElementById("quick-chart-modal");
    if (!modal) return;

    modal.classList.add("active");
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", handleQuickChartKeyDown);

    const badgeEl = document.getElementById("quick-chart-badge");
    const titleEl = document.getElementById("quick-chart-title");
    const purpEl = document.getElementById("quick-chart-purpose");
    const chipsEl = document.getElementById("quick-chart-meta-chips");
    const stateEl = document.getElementById("quick-chart-state");
    const contentEl = document.getElementById("quick-chart-content");
    const tableWrap = document.getElementById("quick-chart-table-wrap");
    const fallbackBtn = document.getElementById("quick-chart-fallback-btn");

    if (typeof Charts !== "undefined") {
      Charts.destroy("quick-chart-canvas");
    }

    if (badgeEl) badgeEl.textContent = mdxId;
    if (titleEl) titleEl.textContent = "Đang tải bài toán phân tích...";
    if (purpEl) purpEl.textContent = "Đang kết nối kho dữ liệu DWH...";
    if (chipsEl) chipsEl.innerHTML = "";
    if (tableWrap) tableWrap.innerHTML = "";
    if (contentEl) contentEl.style.display = "none";
    if (stateEl) {
      stateEl.style.display = "block";
      stateEl.innerHTML = Comp.renderState("loading", { message: "Đang truy vấn số liệu thực tế từ DWH...", compact: true });
    }

    const res = await Svc.getQuickChart(mdxId);

    if (res.state !== "ready" || !res.data) {
      if (stateEl) {
        stateEl.innerHTML = Comp.renderState("error", {
          title: "Không thể tải số liệu",
          message: res.message || "Đã xảy ra lỗi khi truy vấn kho dữ liệu DWH.",
          compact: true
        });
      }
      return;
    }

    const d = res.data;
    if (titleEl) titleEl.textContent = d.title || mdxId;
    if (purpEl) purpEl.textContent = d.purpose || "Phân tích số liệu DWH";
    if (fallbackBtn) {
      fallbackBtn.href = `#/${d.relatedRoute || "sales"}`;
      const routeName = d.relatedRoute === "product" ? "Sản phẩm" : d.relatedRoute === "cross-analysis" ? "Phân tích chéo" : "Bán hàng";
      fallbackBtn.innerHTML = `<i class="fa-solid fa-arrow-up-right-from-square"></i> Mở phân hệ ${routeName}`;
    }

    if (chipsEl) {
      chipsEl.innerHTML = `
        <span class="smart-prompt-chip"><i class="fa-solid fa-layer-group"></i> ${esc(d.section || "Nhân sự & Khuyến mãi")}</span>
        <span class="smart-prompt-chip"><i class="fa-solid fa-cube"></i> ${esc(d.dimension || "Dimension")}</span>
        ${(d.measures || []).map(m => `<span class="smart-prompt-chip"><i class="fa-solid fa-calculator"></i> ${esc(m)}</span>`).join("")}
        <span class="smart-prompt-chip" style="color:var(--tone-success);"><i class="fa-solid fa-database"></i> DWH Live Data</span>
      `;
    }

    const rows = Array.isArray(d.data) ? d.data : [];

    if (rows.length === 0) {
      if (stateEl) {
        stateEl.innerHTML = `
          <div class="empty-state-card" style="padding: 32px 16px; text-align: center;">
            <div style="font-size: 36px; margin-bottom: 12px; color: var(--text-muted);"><i class="fa-solid fa-box-open"></i></div>
            <h4 style="margin: 0 0 8px;">Chưa có dữ liệu phát sinh</h4>
            <p style="color: var(--text-secondary); max-width: 520px; margin: 0 auto 16px; font-size: 13px;">
              ${mdxId === "MDX-23" 
                ? "Hệ thống ghi nhận trường <code>ServiceKey</code> trong bảng Fact_Sales hiện đang mang giá trị NULL (doanh nghiệp chưa hạch toán doanh thu dịch vụ riêng biệt trong kỳ dữ liệu này)." 
                : "Không tìm thấy bản ghi nào phù hợp với điều kiện phân tích trong kho dữ liệu."}
            </p>
            <div style="font-size: 12px; color: var(--text-muted);">
              Căn cứ nguồn: Bảng <code>Fact_Sales</code> liên kết <code>Dim_Service</code>.
            </div>
          </div>
        `;
      }
      return;
    }

    if (stateEl) stateEl.style.display = "none";
    if (contentEl) contentEl.style.display = "block";

    // Cho DOM hoàn tất tính toán kích thước chiều rộng trước khi ApexCharts render
    setTimeout(() => {
      renderQuickChartVisuals(mdxId, d, rows);
    }, 60);
  }

  function renderQuickChartVisuals(mdxId, meta, rows) {
    const tableWrap = document.getElementById("quick-chart-table-wrap");

    if (mdxId === "MDX-19") {
      // 1. Chart: Horizontal Bar xếp hạng Nhân viên theo Doanh thu
      Charts.bar("quick-chart-canvas", rows.map(r => ({ label: r.Label, value: r.Revenue })), {
        horizontal: true,
        format: CUR,
        seriesName: "Doanh thu",
        height: 280
      });

      // 2. Table
      if (tableWrap) {
        tableWrap.innerHTML = Comp.renderTable([
          { key: "Label", label: "Nhân viên kinh doanh", render: r => `<strong>${esc(r.Label)}</strong>` },
          { key: "SubLabel", label: "Chức vụ", render: r => esc(r.SubLabel || "Nhân viên kinh doanh") },
          { key: "Revenue", label: "Doanh thu", format: CUR },
          { key: "Profit", label: "Lợi nhuận", format: CUR },
          { key: "margin", label: "Biên LN", render: r => r.Revenue ? ((r.Profit / r.Revenue) * 100).toFixed(1) + "%" : "—" },
          { key: "OrderCount", label: "Số đơn hàng", format: "number" }
        ], rows);
      }
    } else if (mdxId === "MDX-20") {
      // 1. Chart: Donut tỷ trọng Doanh thu theo Khuyến mãi
      Charts.donut("quick-chart-canvas", rows.map(r => ({ label: r.Label, value: r.Revenue })), {
        format: CUR,
        height: 280
      });

      // 2. Table
      if (tableWrap) {
        tableWrap.innerHTML = Comp.renderTable([
          { key: "Label", label: "Chương trình khuyến mãi", render: r => `<strong>${esc(r.Label)}</strong>` },
          { key: "Revenue", label: "Doanh thu mang lại", format: CUR },
          { key: "DiscountAmount", label: "Chiết khấu bỏ ra", format: CUR },
          { key: "Profit", label: "Lợi nhuận ròng", format: CUR },
          { key: "OrderCount", label: "Số đơn áp dụng", format: "number" }
        ], rows);
      }
    } else if (mdxId === "MDX-21") {
      // 1. Chart: Bar chart Hãng sản xuất
      Charts.bar("quick-chart-canvas", rows.map(r => ({ label: r.Label, value: r.Revenue })), {
        horizontal: false,
        format: CUR,
        seriesName: "Doanh thu",
        height: 280
      });

      // 2. Table
      if (tableWrap) {
        tableWrap.innerHTML = Comp.renderTable([
          { key: "Label", label: "Hãng sản xuất", render: r => `<strong>${esc(r.Label)}</strong>` },
          { key: "Country", label: "Xuất xứ", render: r => esc(r.Country || "Quốc tế") },
          { key: "Revenue", label: "Doanh thu", format: CUR },
          { key: "Profit", label: "Lợi nhuận", format: CUR },
          { key: "Quantity", label: "Số lượng bán", format: "number" },
          { key: "OrderCount", label: "Số đơn", format: "number" }
        ], rows);
      }
    } else if (mdxId === "MDX-22") {
      // 1. Chart: Bar chart Tỷ suất lợi nhuận (%)
      Charts.bar("quick-chart-canvas", rows.map(r => ({ label: r.Label, value: r.ProfitMarginPct })), {
        horizontal: true,
        format: "percent",
        seriesName: "Tỷ suất LN %",
        height: 280
      });

      // 2. Table
      if (tableWrap) {
        tableWrap.innerHTML = Comp.renderTable([
          { key: "Label", label: "Danh mục sản phẩm", render: r => `<strong>${esc(r.Label)}</strong>` },
          { key: "Revenue", label: "Doanh thu", format: CUR },
          { key: "Profit", label: "Lợi nhuận", format: CUR },
          { key: "ProfitMarginPct", label: "Tỷ suất LN (%)", render: r => `<strong>${Number(r.ProfitMarginPct).toFixed(2)}%</strong>` },
          { key: "OrderCount", label: "Số đơn hàng", format: "number" }
        ], rows);
      }
    } else if (mdxId === "MDX-24") {
      // CROSSJOIN Ma trận Kênh bán x Quý
      const quarters = [1, 2, 3, 4];
      const channels = [...new Set(rows.map(r => r.Channel))];

      // 1. Chart: Grouped Column Chart (ApexCharts)
      const series = channels.map((ch, i) => ({
        name: ch,
        data: quarters.map(q => {
          const match = rows.find(r => r.Channel === ch && r.Quarter === q);
          return match ? match.Revenue : 0;
        })
      }));

      Charts.groupedBar("quick-chart-canvas", ["Quý 1", "Quý 2", "Quý 3", "Quý 4"], series, {
        format: CUR,
        height: 280
      });

      // 2. Table: Ma trận CROSSJOIN (Channel x Quarters)
      if (tableWrap) {
        const matrixRows = channels.map(ch => {
          const q1 = rows.find(r => r.Channel === ch && r.Quarter === 1)?.Revenue || 0;
          const q2 = rows.find(r => r.Channel === ch && r.Quarter === 2)?.Revenue || 0;
          const q3 = rows.find(r => r.Channel === ch && r.Quarter === 3)?.Revenue || 0;
          const q4 = rows.find(r => r.Channel === ch && r.Quarter === 4)?.Revenue || 0;
          const total = q1 + q2 + q3 + q4;
          return { Channel: ch, Q1: q1, Q2: q2, Q3: q3, Q4: q4, Total: total };
        });

        tableWrap.innerHTML = Comp.renderTable([
          { key: "Channel", label: "Kênh bán hàng", render: r => `<strong>${esc(r.Channel)}</strong>` },
          { key: "Q1", label: "Quý 1", format: CUR },
          { key: "Q2", label: "Quý 2", format: CUR },
          { key: "Q3", label: "Quý 3", format: CUR },
          { key: "Q4", label: "Quý 4", format: CUR },
          { key: "Total", label: "Tổng cả năm", format: CUR, render: r => `<strong>${Comp.formatCurrency(r.Total)}</strong>` }
        ], matrixRows);
      }
    }
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

    // Trạng thái đang tải dùng loader chuẩn của CopilotResponseRules
    const loadingHtml = window.CopilotResponseRules
      ? window.CopilotResponseRules.renderLoading("Đang truy vấn dữ liệu…")
      : `<i class="fa-solid fa-spinner fa-spin"></i> Đang truy vấn dữ liệu…`;
    const loading = chatAppend("bot", loadingHtml);

    const res = await window.KhangNghiApiClient.get("/nlq/query", { q: text }, { timeout: 60000 });
    loading.remove();

    if (!res.success) {
      const errText = res.status === 403
        ? (window.CopilotResponseRules ? window.CopilotResponseRules.MESSAGES.UNAUTHORIZED : "Vai trò hiện tại chưa được cấp quyền dùng trợ lý.")
        : ((res.data && (res.data.answer || res.data.Answer)) || res.error || (window.CopilotResponseRules ? window.CopilotResponseRules.MESSAGES.CONNECTION_ERROR : "Không thể kết nối nguồn dữ liệu."));
      const errHtml = window.CopilotResponseRules ? window.CopilotResponseRules.renderError(errText) : `<p>${esc(errText)}</p>`;
      chatAppend("bot", errHtml);
      return;
    }

    const answer = (res.data && (res.data.answer ?? res.data.Answer)) || "";
    const data = res.data ? (res.data.data ?? res.data.Data) : null;
    const isLlm = res.data && (res.data.source === "llm" || res.data.Source === "llm");
    const queriesUsed = res.data ? (res.data.queriesUsed || res.data.QueriesUsed || []) : [];

    // TRƯỜNG HỢP 1: Phản hồi từ Tầng 3 LLM (đã áp dụng trọn vẹn Model Response Rules: Markdown, Table, Code Block MDX, số liệu BI)
    if (isLlm && window.CopilotResponseRules) {
      const sourceNote = queriesUsed.length > 0
        ? `Nguồn: kho DWH_KhangNghi (truy vấn ${queriesUsed.join(", ")})`
        : "Nguồn: Khang Nghi AI Copilot";
      chatAppend("bot", window.CopilotResponseRules.createBotBubbleHtml(answer, sourceNote));
      return;
    }

    // TRƯỜNG HỢP 2: Dữ liệu fallback từ khóa hoặc khi chưa qua LLM
    const source = `<div class="chat-source"><i class="fa-solid fa-database"></i> Nguồn: kho DWH_KhangNghi</div>`;

    if (data === null || data === undefined) {
      if (window.CopilotResponseRules) {
        chatAppend("bot", window.CopilotResponseRules.createBotBubbleHtml(answer || window.CopilotResponseRules.MESSAGES.EMPTY_DATA, "Hệ thống BI Khang Nghị"));
      } else {
        chatAppend("bot", `<p>${esc(answer)}</p>`);
      }
    } else if (Array.isArray(data)) {
      if (!data.length) {
        const emptyHtml = window.CopilotResponseRules
          ? window.CopilotResponseRules.renderEmpty(answer || window.CopilotResponseRules.MESSAGES.EMPTY_DATA)
          : `<p>${esc(answer || "Kho dữ liệu chưa có bản ghi phù hợp.")}</p>`;
        chatAppend("bot", emptyHtml);
      } else {
        const intro = window.CopilotResponseRules ? window.CopilotResponseRules.renderMarkdown(answer) : `<p>${esc(answer)}</p>`;
        const content = `
<div class="chat-bubble-content">
  ${intro}
  ${chatTable(data)}
</div>
${source}
<div class="chat-actions">
  <button type="button" class="btn-chat-action" onclick="CopilotResponseRules.copyMessage(this)" title="Sao chép toàn bộ câu trả lời">
    <i class="fa-regular fa-copy"></i> Sao chép
  </button>
</div>`.trim();
        chatAppend("bot", content);
      }
    } else {
      // Dữ liệu tổng quan
      const g = k => data[k] ?? data[k.charAt(0).toLowerCase() + k.slice(1)];
      const totalOrders = Number(g("TotalOrders") || 0);
      if (totalOrders > 0) {
        const mdText = `### Tổng quan kinh doanh
- **Doanh thu:** ${fv(g("TotalRevenue"), CUR)}
- **Lợi nhuận:** ${fv(g("TotalProfit"), CUR)}
- **Biên lợi nhuận:** ${fv(g("ProfitMargin"), "percent")}
- **Số đơn hàng:** ${fv(g("TotalOrders"))}`;
        if (window.CopilotResponseRules) {
          chatAppend("bot", window.CopilotResponseRules.createBotBubbleHtml(mdText, "Kho DWH_KhangNghi (tổng quan)"));
        } else {
          chatAppend("bot", `<p>Tổng quan kinh doanh:</p><ul class="chat-list"><li>Doanh thu: <strong>${fv(g("TotalRevenue"), CUR)}</strong></li><li>Lợi nhuận: <strong>${fv(g("TotalProfit"), CUR)}</strong></li><li>Biên lợi nhuận: <strong>${fv(g("ProfitMargin"), "percent")}</strong></li><li>Số đơn hàng: <strong>${fv(g("TotalOrders"))}</strong></li></ul>${source}`);
        }
      } else {
        const emptyHtml = window.CopilotResponseRules
          ? window.CopilotResponseRules.renderEmpty("Chưa có giao dịch phát sinh để tổng hợp.")
          : `<p>Chưa có giao dịch phát sinh để tổng hợp.</p>`;
        chatAppend("bot", emptyHtml);
      }
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
    openStockCover,
    openStockStatus,
    openShippingRegion,
    switchMdxTab,
    loadMdxSample,
    clearMdx,
    formatMdx,
    runMdx,
    renderMdxLibrary,
    renderMultiDim,
    loadMultiDimAnalysis,
    swapMultiDimAxes,
    resetMultiDim,
    exportMultiDimToCSV,
    setForecastHorizon,
    updateWhatIf,
    resetWhatIf,
    applyWhatIfPreset,
    saveWhatIfScenario,
    deleteScenario,
    renderQuestions,
    openQuestionMdx,
    openQuickChart,
    closeQuickChart,
    closeMdxModal,
    copyMdxQuery,
    setTheme,
    changePassword,
    openCopilot,
    askCopilot
  };
});
