/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - UI COMPONENTS
   Định dạng số, thẻ KPI, khối trạng thái dữ liệu, bảng, toast, ngăn chi tiết.
   Style: css/components.css (.kpi-card, .state-panel, .data-table, .detail-*)
   ========================================================================== */

// Đơn vị tiền của Fact_Sales / Fact_Shipping. Nguồn KhangNghi_OLTP_VN 2022–2025 dùng VNĐ (mặc định);
// nếu Fact_Sales nạp từ nguồn Superstore 2012–2015 (USD) thì đặt salesCurrency: "usd". Fact_Purchase luôn là VNĐ.
window.KN_CONFIG = Object.assign({ salesCurrency: "vnd" }, window.KN_CONFIG || {});

window.KhangNghiComponents = (function () {
  const SALES_CURRENCY = window.KN_CONFIG.salesCurrency === "vnd" ? "vnd" : "usd";
  const SALES_CURRENCY_LABEL = SALES_CURRENCY === "vnd" ? "VNĐ" : "USD";

  // --------------------------------------------------------------------------
  // ĐỊNH DẠNG
  // --------------------------------------------------------------------------
  function isNum(v) {
    return v !== null && v !== undefined && v !== "" && Number.isFinite(Number(v));
  }

  function compact(n) {
    const a = Math.abs(n);
    if (a >= 1e9) return (n / 1e9).toFixed(2).replace(/\.?0+$/, "") + " tỷ";
    if (a >= 1e6) return (n / 1e6).toFixed(2).replace(/\.?0+$/, "") + " tr";
    if (a >= 1e3) return (n / 1e3).toFixed(1).replace(/\.0$/, "") + "K";
    return n.toLocaleString("vi-VN", { maximumFractionDigits: 2 });
  }

  // format: "usd" | "vnd" | "number" | "percent" | "days" | "text"
  function formatValue(v, format = "number") {
    if (format === "text") return v === null || v === undefined || v === "" ? "—" : String(v);
    if (!isNum(v)) return "—";
    const n = Number(v);
    switch (format) {
      case "usd": {
        const a = Math.abs(n);
        const sign = n < 0 ? "-" : "";
        if (a >= 1e9) return `${sign}$${(a / 1e9).toFixed(2)}B`;
        if (a >= 1e6) return `${sign}$${(a / 1e6).toFixed(2)}M`;
        if (a >= 1e3) return `${sign}$${(a / 1e3).toFixed(1)}K`;
        return `${sign}$${a.toLocaleString("en-US", { maximumFractionDigits: 2 })}`;
      }
      case "vnd": return compact(n) + " ₫";
      case "percent": return n.toLocaleString("vi-VN", { maximumFractionDigits: 1 }) + "%";
      case "days": return n.toLocaleString("vi-VN", { maximumFractionDigits: 1 }) + " ngày";
      default: return n.toLocaleString("vi-VN", { maximumFractionDigits: 2 });
    }
  }

  // Giữ tương thích với mã cũ
  function formatCurrency(v) { return formatValue(v, "usd"); }
  function formatNumber(v) { return formatValue(v, "number"); }

  function escapeHtml(s) {
    return String(s === null || s === undefined ? "" : s)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
  }

  // --------------------------------------------------------------------------
  // THẺ KPI
  //   value null -> "—" + ghi chú "Chưa có dữ liệu"
  //   change: { pct, label } chỉ hiển thị khi được tính từ dữ liệu thật
  // --------------------------------------------------------------------------
  function renderKpiCard({ label, value, format = "number", icon = "fa-chart-simple", change = null, note = "", hint = "" }) {
    const hasValue = format === "text" ? !!value : isNum(value);
    let changeHtml = "";
    if (change && isNum(change.pct)) {
      const up = change.pct >= 0;
      changeHtml = `<span class="kpi-change ${up ? "is-up" : "is-down"}"><i class="fa-solid fa-arrow-${up ? "up" : "down"}"></i> ${Math.abs(change.pct).toFixed(1)}% <span class="kpi-change-label">${escapeHtml(change.label || "")}</span></span>`;
    }
    const foot = !hasValue ? "Chưa có dữ liệu" : note;
    return `
      <div class="kpi-card glass-card${hasValue ? "" : " is-empty"}${format === "text" ? " is-text" : ""}"${hint ? ` title="${escapeHtml(hint)}"` : ""}>
        <div class="kpi-top-row">
          <span class="kpi-label">${escapeHtml(label)}</span>
          <span class="kpi-icon"><i class="fa-solid ${icon}"></i></span>
        </div>
        <div class="kpi-main-value">${escapeHtml(formatValue(value, format))}</div>
        <div class="kpi-foot">${changeHtml}${foot ? `<span class="kpi-note">${escapeHtml(foot)}</span>` : ""}</div>
      </div>`;
  }

  function renderSkeletonCards(n = 4) {
    return Array.from({ length: n }, () => `
      <div class="kpi-card glass-card">
        <div class="skeleton skeleton-text" style="width:45%;"></div>
        <div class="skeleton skeleton-title"></div>
        <div class="skeleton skeleton-text" style="width:30%;"></div>
      </div>`).join("");
  }

  // --------------------------------------------------------------------------
  // KHỐI TRẠNG THÁI DỮ LIỆU (phân biệt rõ từng trường hợp)
  // --------------------------------------------------------------------------
  const STATE_META = {
    loading:     { icon: "fa-spinner fa-spin", tone: "info",    title: "Đang tải dữ liệu" },
    empty:       { icon: "fa-database",        tone: "neutral", title: "Chưa có dữ liệu" },
    error:       { icon: "fa-plug-circle-xmark", tone: "danger", title: "Không truy vấn được dữ liệu" },
    forbidden:   { icon: "fa-lock",            tone: "warning", title: "Không có quyền truy cập" },
    unavailable: { icon: "fa-server",          tone: "warning", title: "Nguồn dữ liệu chưa kết nối" }
  };

  function renderState(state, { title = "", message = "", compact = false, actions = "" } = {}) {
    const meta = STATE_META[state] || STATE_META.empty;
    return `
      <div class="state-panel tone-${meta.tone}${compact ? " is-compact" : ""}" role="${state === "error" ? "alert" : "status"}">
        <span class="state-icon"><i class="fa-solid ${meta.icon}"></i></span>
        <div class="state-text">
          <strong>${escapeHtml(title || meta.title)}</strong>
          ${message ? `<span>${escapeHtml(message)}</span>` : ""}
        </div>
        ${actions ? `<div class="state-actions">${actions}</div>` : ""}
      </div>`;
  }

  // Thông điệp mặc định cho một section rỗng/lỗi
  function sectionMessage(sec, emptyMessage) {
    if (!sec) return "";
    if (sec.state === "empty") return emptyMessage || "Kho dữ liệu chưa có bản ghi phù hợp.";
    return sec.message || "";
  }

  // Ghi nội dung trạng thái vào một vùng; trả về true nếu section sẵn sàng để vẽ.
  // emptyTitle: dùng khi truy vấn thành công nhưng không có dòng (vd. "Không có cảnh báo")
  function applySectionState(containerId, sec, emptyMessage, emptyTitle) {
    const el = document.getElementById(containerId);
    if (!el) return false;
    if (sec && sec.state === "ready") return true;
    const state = sec ? sec.state : "empty";
    el.innerHTML = renderState(state, { title: state === "empty" ? (emptyTitle || "") : "", message: sectionMessage(sec, emptyMessage), compact: true });
    return false;
  }

  // --------------------------------------------------------------------------
  // BẢNG DỮ LIỆU
  //   columns: [{ key, label, format, align, render(row) }]
  // --------------------------------------------------------------------------
  function renderTable(columns, rows, { onRowClick = "", rowKey = null, empty = "Không có dòng dữ liệu.", emptyTitle = "Không có dòng nào" } = {}) {
    if (!rows || !rows.length) {
      return renderState("empty", { title: emptyTitle, message: empty, compact: true });
    }
    const head = columns.map(c => `<th class="${c.align === "right" || ["usd", "vnd", "number", "percent", "days"].includes(c.format) ? "num" : ""}">${escapeHtml(c.label)}</th>`).join("");
    const body = rows.map(r => {
      const click = onRowClick && rowKey ? ` class="is-clickable" tabindex="0" onclick="${onRowClick}('${escapeHtml(String(r[rowKey]).replace(/'/g, "\\'"))}')"` : "";
      const cells = columns.map(c => {
        const isNumCol = c.align === "right" || ["usd", "vnd", "number", "percent", "days"].includes(c.format);
        const content = c.render ? c.render(r) : escapeHtml(formatValue(r[c.key], c.format || "text"));
        return `<td class="${isNumCol ? "num" : ""}">${content}</td>`;
      }).join("");
      return `<tr${click}>${cells}</tr>`;
    }).join("");
    return `<div class="table-scroll"><table class="data-table"><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table></div>`;
  }

  function statusBadge(tone, text) {
    return `<span class="status-badge tone-${tone}">${escapeHtml(text)}</span>`;
  }

  // --------------------------------------------------------------------------
  // TOAST
  // --------------------------------------------------------------------------
  function showToast(message, tone = "success") {
    let container = document.getElementById("toast-container");
    if (!container) {
      container = document.createElement("div");
      container.id = "toast-container";
      document.body.appendChild(container);
    }
    const icons = { success: "fa-circle-check", info: "fa-circle-info", warning: "fa-triangle-exclamation", danger: "fa-circle-xmark" };
    const toast = document.createElement("div");
    toast.className = `toast tone-${tone}`;
    toast.innerHTML = `<i class="fa-solid ${icons[tone] || icons.info}"></i> <span>${escapeHtml(message)}</span>`;
    container.appendChild(toast);
    setTimeout(() => {
      toast.style.opacity = "0";
      setTimeout(() => toast.remove(), 200);
    }, 2800);
  }

  // --------------------------------------------------------------------------
  // NGĂN CHI TIẾT (khách hàng, đơn hàng) - dữ liệu thật từ API tra cứu
  // --------------------------------------------------------------------------
  function openModal(id, title, subtitle) {
    let modal = document.getElementById(id);
    if (!modal) {
      modal = document.createElement("div");
      modal.id = id;
      modal.className = "modal-overlay";
      modal.addEventListener("click", e => { if (e.target === modal) modal.classList.remove("active"); });
      document.body.appendChild(modal);
    }
    modal.innerHTML = `
      <div class="modal-box detail-modal">
        <div class="detail-head">
          <div>
            <h2>${escapeHtml(title)}</h2>
            <span>${escapeHtml(subtitle)}</span>
          </div>
          <button type="button" class="panel-close-btn" aria-label="Đóng" onclick="document.getElementById('${id}').classList.remove('active')">&times;</button>
        </div>
        <div class="detail-body">${renderState("loading", { compact: true })}</div>
      </div>`;
    modal.classList.add("active");
    return modal.querySelector(".detail-body");
  }

  function field(label, value) {
    return `<div class="detail-field"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value === null || value === undefined || value === "" ? "—" : value)}</strong></div>`;
  }

  async function openCustomerDetailDrawer(customerId) {
    if (!customerId || customerId === "N/A" || customerId === "undefined") {
      showToast("Dòng này không có mã khách hàng để tra cứu.", "warning");
      return;
    }
    const body = openModal("customer-detail-drawer", "Chi tiết khách hàng", `Mã khách hàng: ${customerId}`);
    const res = await window.analyticsService.getCustomerDetail(customerId);
    if (!res.success) {
      body.innerHTML = renderState("error", { message: res.message });
      return;
    }
    const d = res.data || {};
    const cust = d.customer || {};
    const sum = d.summary || {};
    const orders = Array.isArray(d.orders) ? d.orders : [];
    body.innerHTML = `
      <div class="detail-grid">
        ${field("Tên khách hàng", cust.CustomerName)}
        ${field("Phân khúc", cust.Segment)}
        ${field("Tổng doanh thu", isNum(sum.TotalRevenue) ? formatValue(sum.TotalRevenue, SALES_CURRENCY) : null)}
        ${field("Số đơn hàng", isNum(sum.TotalOrders) ? formatValue(sum.TotalOrders) : null)}
        ${field("Nguồn dữ liệu", cust.SourceSystem)}
        ${field("Khóa thay thế (CustomerKey)", cust.CustomerKey)}
      </div>
      <h3 class="detail-section-title">Lịch sử đơn hàng (${orders.length})</h3>
      ${renderTable([
        { key: "OrderID", label: "Mã đơn", render: o => `<a href="javascript:void(0)" class="link" onclick="KhangNghiComponents.openOrderDetailDrawer('${escapeHtml(o.OrderID)}')">${escapeHtml(o.OrderID)}</a>` },
        { key: "OrderDate", label: "Ngày đặt", render: o => escapeHtml((o.OrderDate || "").substring(0, 10) || "—") },
        { key: "ProductName", label: "Sản phẩm" },
        { key: "Quantity", label: "SL", format: "number" },
        { key: "SalesAmount", label: "Doanh thu", format: SALES_CURRENCY },
        { key: "ProfitAmount", label: "Lợi nhuận", format: SALES_CURRENCY }
      ], orders, { empty: "Khách hàng chưa có đơn trong Fact_Sales." })}`;
  }

  async function openOrderDetailDrawer(orderId) {
    if (!orderId || orderId === "N/A" || orderId === "undefined") {
      showToast("Dòng này không có mã đơn hàng để tra cứu.", "warning");
      return;
    }
    const body = openModal("order-detail-drawer", "Chi tiết đơn hàng", `Mã đơn hàng: ${orderId}`);
    const res = await window.analyticsService.getOrderDetail(orderId);
    if (!res.success) {
      body.innerHTML = renderState("error", { message: res.message });
      return;
    }
    const d = res.data || {};
    const head = d.orderHeader || {};
    const items = Array.isArray(d.items) ? d.items : [];
    const total = items.reduce((acc, x) => acc + (Number(x.SalesAmount) || 0), 0);
    body.innerHTML = `
      <div class="detail-grid">
        ${field("Ngày đặt", (head.orderDate || "").substring(0, 10))}
        ${field("Khách hàng", head.customerName)}
        ${field("Mã khách hàng", head.customerID)}
        ${field("Tổng giá trị", items.length ? formatValue(isNum(head.totalSales) ? head.totalSales : total, SALES_CURRENCY) : null)}
      </div>
      <h3 class="detail-section-title">Mặt hàng trong đơn (${items.length})</h3>
      ${renderTable([
        { key: "ProductID", label: "Mã SP" },
        { key: "ProductName", label: "Sản phẩm" },
        { key: "Category", label: "Danh mục" },
        { key: "Quantity", label: "SL", format: "number" },
        { key: "DiscountAmount", label: "Chiết khấu", format: SALES_CURRENCY },
        { key: "SalesAmount", label: "Doanh thu", format: SALES_CURRENCY },
        { key: "ProfitAmount", label: "Lợi nhuận", format: SALES_CURRENCY }
      ], items, { empty: "Đơn hàng không có dòng chi tiết." })}`;
  }

  return {
    SALES_CURRENCY,
    SALES_CURRENCY_LABEL,
    isNum,
    formatValue,
    formatCurrency,
    formatNumber,
    escapeHtml,
    renderKpiCard,
    renderSkeletonCards,
    renderState,
    sectionMessage,
    applySectionState,
    renderTable,
    statusBadge,
    showToast,
    openModal,
    field,
    openCustomerDetailDrawer,
    openOrderDetailDrawer
  };
})();
