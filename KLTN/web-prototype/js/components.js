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

  async function openCustomerDetailDrawer(customerId) {
    if (!customerId || customerId === "N/A" || customerId === "undefined") {
      console.warn("[Data Quality Warning] Cannot open drawer for N/A or invalid CustomerID:", customerId);
      showToast("⚠️ Mã KH không tồn tại hoặc N/A trong Data Warehouse.");
      return;
    }

    let modal = document.getElementById("customer-detail-drawer");
    if (!modal) {
      modal = document.createElement("div");
      modal.id = "customer-detail-drawer";
      modal.className = "modal-overlay";
      document.body.appendChild(modal);
    }

    modal.innerHTML = `
      <div class="modal-box glass-card" style="max-width:850px; width:90%; max-height:90vh; overflow-y:auto; padding:24px;">
        <div style="display:flex; justify-content:space-between; align-items:center; border-bottom:1px solid rgba(255,255,255,0.1); padding-bottom:12px; margin-bottom:16px;">
          <div>
            <h2 style="font-size:18px; font-weight:700; color:var(--color-primary, #3b82f6); margin:0;">
              <i class="fa-solid fa-user-gear"></i> Chi Tiết Khách Hàng & Data Lineage Tracing
            </h2>
            <span style="font-size:12px; color:var(--color-text-secondary);">Natural Key: <strong>${customerId}</strong></span>
          </div>
          <button class="topbar-action-btn" onclick="document.getElementById('customer-detail-drawer').classList.remove('active')" style="background:transparent; border:none; color:white; font-size:18px; cursor:pointer;">&times;</button>
        </div>
        <div id="customer-drawer-content" style="padding:10px 0;">
          <div style="text-align:center; padding:30px;"><i class="fa-solid fa-spinner fa-spin" style="font-size:24px;"></i> Đang tải dữ liệu từ DWH...</div>
        </div>
      </div>
    `;
    modal.classList.add("active");

    const res = await window.analyticsService.getCustomerDetail(customerId);
    const contentEl = document.getElementById("customer-drawer-content");
    if (!contentEl) return;

    if (!res.success || !res.data) {
      contentEl.innerHTML = `
        <div style="text-align:center; padding:30px; color:var(--color-danger, #e74c3c);">
          <i class="fa-solid fa-triangle-exclamation" style="font-size:36px; margin-bottom:12px;"></i>
          <h3>Không tìm thấy dữ liệu cho Mã KH: ${customerId}</h3>
          <p style="font-size:13px; color:var(--color-text-muted);">Trạng thái Data Quality: N/A | Vui lòng kiểm tra lại dữ liệu nguồn Excel/DWH.</p>
        </div>
      `;
      return;
    }

    const d = res.data;
    const cust = d.customer || {};
    const sum = d.summary || {};
    const lin = d.lineage || {};
    const orders = d.orders || [];

    contentEl.innerHTML = `
      <!-- Customer Info Grid -->
      <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap:16px; margin-bottom:20px;">
        <div style="background:rgba(255,255,255,0.03); padding:12px 16px; border-radius:8px; border:1px solid rgba(255,255,255,0.08);">
          <div style="font-size:11px; color:var(--color-text-muted);">TÊN KHÁCH HÀNG</div>
          <div style="font-size:15px; font-weight:700; color:white;">${cust.CustomerName || "N/A"}</div>
          <div style="font-size:12px; color:var(--color-primary); margin-top:2px;">Mã KH (Natural Key): <strong>${cust.CustomerID || customerId}</strong></div>
        </div>
        <div style="background:rgba(255,255,255,0.03); padding:12px 16px; border-radius:8px; border:1px solid rgba(255,255,255,0.08);">
          <div style="font-size:11px; color:var(--color-text-muted);">PHÂN KHÚC (SEGMENT)</div>
          <div style="font-size:15px; font-weight:700; color:white;">${cust.Segment || "Khách lẻ"}</div>
          <div style="font-size:12px; color:var(--color-text-secondary); margin-top:2px;">Surrogate Key: <strong>CustomerKey = ${cust.CustomerKey || "N/A"}</strong></div>
        </div>
        <div style="background:rgba(255,255,255,0.03); padding:12px 16px; border-radius:8px; border:1px solid rgba(255,255,255,0.08);">
          <div style="font-size:11px; color:var(--color-text-muted);">TỔNG DOANH THU</div>
          <div style="font-size:15px; font-weight:700; color:var(--color-success, #2ecc71);">${formatCurrency(sum.TotalRevenue || 0)}</div>
          <div style="font-size:12px; color:var(--color-text-secondary); margin-top:2px;">Số đơn hàng: <strong>${sum.TotalOrders || 0} đơn</strong></div>
        </div>
      </div>

      <!-- DATA LINEAGE TRACING SECTION -->
      <div style="background:rgba(15, 23, 42, 0.8); border:1px solid rgba(59, 130, 246, 0.3); border-radius:10px; padding:16px; margin-bottom:20px;">
        <h4 style="font-size:13px; font-weight:700; color:var(--color-primary, #3b82f6); margin-top:0; margin-bottom:10px; display:flex; align-items:center; gap:8px;">
          <i class="fa-solid fa-diagram-project"></i> DATA LINEAGE TRACING (TRUY VẾT DỮ LIỆU NGUỒN)
        </h4>
        <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap:12px; font-size:12px;">
          <div><span style="color:var(--color-text-muted);">Nguồn Dữ Liệu:</span> <br><strong style="color:#f39c12;"><i class="fa-solid fa-file-excel"></i> ${lin.source || "Excel_Superstore"}</strong></div>
          <div><span style="color:var(--color-text-muted);">Natural Key Tra Cứu:</span> <br><strong style="color:#38bdf8;">${lin.naturalKey || "CustomerID = " + customerId}</strong></div>
          <div><span style="color:var(--color-text-muted);">Bảng Dimension:</span> <br><strong style="color:#a78bfa;">${lin.dimension || "Dim_Customer"}</strong></div>
          <div><span style="color:var(--color-text-muted);">Surrogate Key (Fact Join):</span> <br><strong style="color:#4ade80;">${lin.surrogateKey || "CustomerKey = " + cust.CustomerKey}</strong></div>
          <div><span style="color:var(--color-text-muted);">Bảng Fact Liên Kết:</span> <br><strong style="color:#f43f5e;">${lin.fact || "Fact_Sales"}</strong></div>
          <div><span style="color:var(--color-text-muted);">Bản Ghi Liên Quan:</span> <br><strong style="color:white;">${lin.relatedRecords || (orders.length + " orders")}</strong></div>
        </div>
      </div>

      <!-- Order History Table -->
      <h4 style="font-size:14px; font-weight:700; margin-bottom:10px; color:white;">Lịch Sử Đơn Hàng Của Khách Hàng (${orders.length} đơn gần nhất)</h4>
      <div class="table-responsive" style="max-height:300px; overflow-y:auto;">
        <table class="enterprise-table" style="font-size:12px; width:100%;">
          <thead>
            <tr>
              <th>Order ID (Natural Key)</th>
              <th>Ngày Đặt</th>
              <th>Mã Sản Phẩm</th>
              <th>Tên Sản Phẩm</th>
              <th>Số Lượng</th>
              <th>Doanh Số</th>
              <th>Lợi Nhuận</th>
            </tr>
          </thead>
          <tbody>
            ${orders.length ? orders.map(o => `
              <tr>
                <td>
                  <a href="javascript:void(0)" onclick="KhangNghiComponents.openOrderDetailDrawer('${o.OrderID}')" style="color:var(--color-primary, #38bdf8); font-weight:700; text-decoration:underline;">
                    ${o.OrderID || 'N/A'}
                  </a>
                </td>
                <td>${o.OrderDate ? o.OrderDate.substring(0,10) : 'N/A'}</td>
                <td><strong style="color:#a78bfa;">${o.ProductID || 'N/A'}</strong></td>
                <td>${o.ProductName || 'N/A'}</td>
                <td>${o.Quantity || 0}</td>
                <td>${formatCurrency(o.SalesAmount || 0)}</td>
                <td><strong style="color:${(o.ProfitAmount || 0) >= 0 ? 'var(--color-success,#2ecc71)' : 'var(--color-danger,#e74c3c)'}">${formatCurrency(o.ProfitAmount || 0)}</strong></td>
              </tr>
            `).join('') : '<tr><td colspan="7" style="text-align:center; padding:15px; color:var(--color-text-muted);">Chưa có đơn hàng phát sinh trong Fact_Sales.</td></tr>'}
          </tbody>
        </table>
      </div>
    `;
  }

  async function openOrderDetailDrawer(orderId) {
    if (!orderId || orderId === "N/A" || orderId === "undefined") {
      console.warn("[Data Quality Warning] Cannot open drawer for N/A or invalid OrderID:", orderId);
      showToast("⚠️ Mã Đơn Hàng không tồn tại hoặc N/A trong Data Warehouse.");
      return;
    }

    let modal = document.getElementById("order-detail-drawer");
    if (!modal) {
      modal = document.createElement("div");
      modal.id = "order-detail-drawer";
      modal.className = "modal-overlay";
      document.body.appendChild(modal);
    }

    modal.innerHTML = `
      <div class="modal-box glass-card" style="max-width:850px; width:90%; max-height:90vh; overflow-y:auto; padding:24px;">
        <div style="display:flex; justify-content:space-between; align-items:center; border-bottom:1px solid rgba(255,255,255,0.1); padding-bottom:12px; margin-bottom:16px;">
          <div>
            <h2 style="font-size:18px; font-weight:700; color:var(--color-accent, #38bdf8); margin:0;">
              <i class="fa-solid fa-receipt"></i> Chi Tiết Đơn Hàng & Lineage (Order ID: ${orderId})
            </h2>
            <span style="font-size:12px; color:var(--color-text-secondary);">Natural Key: <strong>${orderId}</strong></span>
          </div>
          <button class="topbar-action-btn" onclick="document.getElementById('order-detail-drawer').classList.remove('active')" style="background:transparent; border:none; color:white; font-size:18px; cursor:pointer;">&times;</button>
        </div>
        <div id="order-drawer-content" style="padding:10px 0;">
          <div style="text-align:center; padding:30px;"><i class="fa-solid fa-spinner fa-spin" style="font-size:24px;"></i> Đang tải dữ liệu từ DWH...</div>
        </div>
      </div>
    `;
    modal.classList.add("active");

    const res = await window.analyticsService.getOrderDetail(orderId);
    const contentEl = document.getElementById("order-drawer-content");
    if (!contentEl) return;

    if (!res.success || !res.data) {
      contentEl.innerHTML = `
        <div style="text-align:center; padding:30px; color:var(--color-danger, #e74c3c);">
          <i class="fa-solid fa-triangle-exclamation" style="font-size:36px; margin-bottom:12px;"></i>
          <h3>Không tìm thấy chi tiết cho Mã Đơn Hàng: ${orderId}</h3>
          <p style="font-size:13px; color:var(--color-text-muted);">Trạng thái Data Quality: N/A | Đơn hàng không tồn tại trong Fact_Sales.</p>
        </div>
      `;
      return;
    }

    const d = res.data;
    const head = d.orderHeader || {};
    const lin = d.lineage || {};
    const items = d.items || [];

    contentEl.innerHTML = `
      <!-- Order Header Summary -->
      <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap:16px; margin-bottom:20px;">
        <div style="background:rgba(255,255,255,0.03); padding:12px 16px; border-radius:8px; border:1px solid rgba(255,255,255,0.08);">
          <div style="font-size:11px; color:var(--color-text-muted);">MÃ ĐƠN HÀNG (NATURAL KEY)</div>
          <div style="font-size:15px; font-weight:700; color:var(--color-primary,#38bdf8);">${head.orderID || orderId}</div>
          <div style="font-size:12px; color:var(--color-text-secondary); margin-top:2px;">Ngày đặt: <strong>${head.orderDate ? head.orderDate.substring(0,10) : 'N/A'}</strong></div>
        </div>
        <div style="background:rgba(255,255,255,0.03); padding:12px 16px; border-radius:8px; border:1px solid rgba(255,255,255,0.08);">
          <div style="font-size:11px; color:var(--color-text-muted);">KHÁCH HÀNG MUA HÀNG</div>
          <div style="font-size:15px; font-weight:700; color:white;">${head.customerName || 'N/A'}</div>
          <div style="font-size:12px; color:var(--color-primary); margin-top:2px;">
            Mã KH: <a href="javascript:void(0)" onclick="document.getElementById('order-detail-drawer').classList.remove('active'); KhangNghiComponents.openCustomerDetailDrawer('${head.customerID}')" style="color:var(--color-primary); text-decoration:underline;"><strong>${head.customerID || 'N/A'}</strong></a>
          </div>
        </div>
        <div style="background:rgba(255,255,255,0.03); padding:12px 16px; border-radius:8px; border:1px solid rgba(255,255,255,0.08);">
          <div style="font-size:11px; color:var(--color-text-muted);">TỔNG GIÁ TRỊ ĐƠN HÀNG</div>
          <div style="font-size:15px; font-weight:700; color:var(--color-success, #2ecc71);">${formatCurrency(head.totalSales || items.reduce((acc, x)=>acc+(x.SalesAmount||0),0))}</div>
          <div style="font-size:12px; color:var(--color-text-secondary); margin-top:2px;">Số mặt hàng: <strong>${head.itemCount || items.length} món</strong></div>
        </div>
      </div>

      <!-- LINEAGE TRACING SECTION -->
      <div style="background:rgba(15, 23, 42, 0.8); border:1px solid rgba(56, 189, 248, 0.3); border-radius:10px; padding:16px; margin-bottom:20px;">
        <h4 style="font-size:13px; font-weight:700; color:var(--color-accent, #38bdf8); margin-top:0; margin-bottom:10px; display:flex; align-items:center; gap:8px;">
          <i class="fa-solid fa-diagram-project"></i> ORDER LINEAGE TRACING (FACT_SALES RECONCILIATION)
        </h4>
        <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap:12px; font-size:12px;">
          <div><span style="color:var(--color-text-muted);">Nguồn Dữ Liệu:</span> <br><strong style="color:#f39c12;"><i class="fa-solid fa-file-excel"></i> ${lin.source || "Excel_Superstore"}</strong></div>
          <div><span style="color:var(--color-text-muted);">Order Natural Key:</span> <br><strong style="color:#38bdf8;">${lin.naturalKey || "OrderID = " + orderId}</strong></div>
          <div><span style="color:var(--color-text-muted);">Bảng Fact Tra Cứu:</span> <br><strong style="color:#f43f5e;">Fact_Sales</strong></div>
          <div><span style="color:var(--color-text-muted);">Surrogate Keys Join:</span> <br><strong style="color:#4ade80;">${lin.surrogateKey || "CustomerKey & ProductKey"}</strong></div>
        </div>
      </div>

      <!-- Order Items Table -->
      <h4 style="font-size:14px; font-weight:700; margin-bottom:10px; color:white;">Chi Tiết Danh Mục Mặt Hàng Trong Đơn</h4>
      <div class="table-responsive">
        <table class="enterprise-table" style="font-size:12px; width:100%;">
          <thead>
            <tr>
              <th>Mã SP (Natural Key)</th>
              <th>Tên Sản Phẩm</th>
              <th>Danh Mục</th>
              <th>Số Lượng</th>
              <th>Chiết Khấu</th>
              <th>Doanh Số</th>
              <th>Lợi Nhuận</th>
            </tr>
          </thead>
          <tbody>
            ${items.map(i => `
              <tr>
                <td><strong style="color:#a78bfa;">${i.ProductID || 'N/A'}</strong></td>
                <td><strong>${i.ProductName || 'N/A'}</strong></td>
                <td><span class="badge badge-info">${i.Category || 'Thiết bị'}</span></td>
                <td>${i.Quantity || 0}</td>
                <td>${i.DiscountAmount ? formatCurrency(i.DiscountAmount) : '$0'}</td>
                <td>${formatCurrency(i.SalesAmount || 0)}</td>
                <td><strong style="color:${(i.ProfitAmount || 0) >= 0 ? 'var(--color-success,#2ecc71)' : 'var(--color-danger,#e74c3c)'}">${formatCurrency(i.ProfitAmount || 0)}</strong></td>
              </tr>
            `).join('')}
          </tbody>
        </table>
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
    renderSkeletonChart,
    openCustomerDetailDrawer,
    openOrderDetailDrawer
  };
})();
