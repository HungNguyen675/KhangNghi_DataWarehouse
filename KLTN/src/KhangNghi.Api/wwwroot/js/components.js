/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - REUSABLE ENTERPRISE UI BUILDERS
   KPI Cards, Chart Cards, Tables, Context Menu, Skeleton Shimmers, Toasts
   ========================================================================== */

window.KhangNghiComponents = (function () {

  function formatCurrency(num) {
    if (num === null || num === undefined) return "$0";
    if (num >= 1000000) return "$" + (num / 1000000).toFixed(2) + "M";
    if (num >= 1000) return "$" + (num / 1000).toFixed(1) + "K";
    return "$" + num.toLocaleString();
  }

  function formatNumber(num) {
    if (num === null || num === undefined) return "0";
    return num.toLocaleString();
  }

  function renderKpiCard({ id, label, value, changePct, priorLabel = "vs kỳ trước", icon, isCurrency = true, unit = "" }) {
    const valText = isCurrency ? formatCurrency(value) : formatNumber(value) + unit;
    const isPositive = changePct >= 0;
    const tagClass = isPositive ? "positive" : "negative";
    const arrow = isPositive ? "↑" : "↓";

    return `
      <div class="glass-card kpi-card" id="card-${id}">
        <div class="kpi-top-row">
          <span class="kpi-label">${label} <i class="fa-solid fa-circle-info kpi-info-tooltip-icon" title="Chỉ số ${label} đo lường trên toàn DWH Khang Nghị"></i></span>
          <div style="font-size:16px;">${icon}</div>
        </div>
        <div class="kpi-value-row">
          <span class="kpi-main-value">${valText}</span>
          <span class="kpi-change-tag ${tagClass}">${arrow} ${Math.abs(changePct)}%</span>
        </div>
        <div class="kpi-bottom-row">
          <span>${priorLabel}</span>
          <div class="kpi-sparkline-box" id="spark-${id}"></div>
        </div>
      </div>
    `;
  }

  function renderChartCard({ id, title, description, bodyHtml = "" }) {
    return `
      <div class="glass-card chart-card" id="chart-card-${id}">
        <div class="chart-card-header">
          <div class="chart-title-group">
            <h3 class="chart-title">${title}</h3>
            <span class="chart-description">${description}</span>
          </div>
          <div style="position:relative;">
            <button class="chart-context-menu-trigger" onclick="KhangNghiComponents.toggleContextMenu('${id}')" title="Tùy chọn biểu đồ">
              <i class="fa-solid fa-ellipsis-vertical"></i>
            </button>
            <div class="dropdown-menu" id="ctx-menu-${id}">
              <div class="dropdown-item" onclick="KhangNghiComponents.handleChartCtxAction('${id}', 'view')"><i class="fa-solid fa-eye"></i> Xem Chi Tiết</div>
              <div class="dropdown-item" onclick="KhangNghiComponents.handleChartCtxAction('${id}', 'data')"><i class="fa-solid fa-table"></i> Hiển Thị Bảng</div>
              <div class="dropdown-item" onclick="KhangNghiComponents.handleChartCtxAction('${id}', 'csv')"><i class="fa-solid fa-file-csv"></i> Xuất CSV</div>
            </div>
          </div>
        </div>
        <div class="chart-canvas-wrapper" id="${id}">
          ${bodyHtml}
        </div>
      </div>
    `;
  }

  function toggleContextMenu(id) {
    const menu = document.getElementById(`ctx-menu-${id}`);
    if (menu) {
      document.querySelectorAll(".dropdown-menu").forEach(m => {
        if (m !== menu) m.classList.remove("active");
      });
      menu.classList.toggle("active");
    }
  }

  function handleChartCtxAction(id, action) {
    const menu = document.getElementById(`ctx-menu-${id}`);
    if (menu) menu.classList.remove("active");

    if (action === "csv") {
      showToast(`Đã xuất dữ liệu biểu đồ ${id} ra file CSV.`);
    } else {
      showToast(`Thao tác ${action} trên biểu đồ ${id}`);
    }
  }

  function showToast(message) {
    let container = document.getElementById("toast-container");
    if (!container) {
      container = document.createElement("div");
      container.id = "toast-container";
      document.body.appendChild(container);
    }

    const toast = document.createElement("div");
    toast.className = "toast";
    toast.innerHTML = `<i class="fa-solid fa-circle-check" style="color:var(--color-success);"></i> ${message}`;
    container.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = "0";
      setTimeout(() => toast.remove(), 200);
    }, 2500);
  }

  function renderSkeletonCard() {
    return `
      <div class="glass-card kpi-card">
        <div class="skeleton skeleton-text" style="width:40%;"></div>
        <div class="skeleton skeleton-title"></div>
        <div class="skeleton skeleton-text" style="width:60%;"></div>
      </div>
    `;
  }

  function renderSkeletonChart() {
    return `
      <div class="glass-card chart-card">
        <div class="skeleton skeleton-title"></div>
        <div class="skeleton skeleton-chart"></div>
      </div>
    `;
  }

  function renderNoDataBanner({ title = "Chưa Có Dữ Liệu Thực Tế", message = "Phân hệ này chưa được nạp dữ liệu vào Kho dữ liệu DWH_KhangNghi.", icon = "📂", badgeText = "NO DATA IN DWH" } = {}) {
    return `
      <div class="glass-card" style="padding: 40px; text-align: center; margin-bottom: 24px; border-left: 4px solid var(--color-accent-amber, #f39c12);">
        <div style="font-size: 48px; margin-bottom: 16px;">${icon}</div>
        <div style="display:inline-block; background:rgba(243,156,18,0.15); color:#f39c12; font-size:12px; font-weight:700; padding:4px 12px; border-radius:12px; margin-bottom:12px;">
          ● ${badgeText}
        </div>
        <h2 style="font-size: 20px; font-weight: 700; color: var(--color-text-primary); margin-bottom: 8px;">${title}</h2>
        <p style="font-size: 14px; color: var(--color-text-secondary); max-width: 600px; margin: 0 auto 20px auto; line-height: 1.6;">
          ${message}
        </p>
        <div style="font-size: 12px; color: var(--color-text-muted); background: rgba(255,255,255,0.03); display: inline-block; padding: 8px 16px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.08);">
          <i class="fa-solid fa-database" style="margin-right: 6px;"></i> Nguồn DWH: <strong>DWH_KhangNghi (SQL Server)</strong> | Trạng thái Bảng Fact: <strong>0 Records</strong>
        </div>
      </div>
    `;
  }

  return {
    formatCurrency,
    formatNumber,
    renderKpiCard,
    renderChartCard,
    renderNoDataBanner,
    toggleContextMenu,
    handleChartCtxAction,
    showToast,
    renderSkeletonCard,
    renderSkeletonChart
  };
})();
