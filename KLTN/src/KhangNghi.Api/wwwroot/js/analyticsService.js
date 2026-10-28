/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - ANALYTICS DATA SERVICE
   Lớp dữ liệu duy nhất giữa REST API (/api/v1) và giao diện.

   Nguyên tắc trung thực dữ liệu:
   - Mỗi khối dữ liệu là một "section": { state, items, message, scope }
       state = "ready"     : API trả về dữ liệu hợp lệ
               "empty"     : API trả về thành công nhưng không có bản ghi
               "error"     : lỗi kết nối / lỗi truy vấn (status, message)
               "forbidden" : vai trò hiện tại không có quyền với API này
   - Không dùng số liệu mẫu, hằng số hay mock để lấp chỗ trống.
   - Chỉ số không tính được từ dữ liệu thật trả về null (giao diện hiển thị "—").
   - scope ghi rõ phạm vi thời gian mà API thực sự áp dụng
     (nhiều API backend không nhận tham số năm -> "Toàn bộ giai đoạn").
   ========================================================================== */

window.analyticsService = (function () {
  const client = window.KhangNghiApiClient;

  // --------------------------------------------------------------------------
  // HELPERS
  // --------------------------------------------------------------------------

  // Dapper dynamic trả PascalCase, DTO có kiểu trả camelCase -> đọc được cả hai
  function pick(obj, key) {
    if (!obj) return undefined;
    if (obj[key] !== undefined) return obj[key];
    const camel = key.charAt(0).toLowerCase() + key.slice(1);
    return obj[camel];
  }

  function num(obj, key) {
    const v = pick(obj, key);
    if (v === null || v === undefined || v === "") return null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }

  function str(obj, key) {
    const v = pick(obj, key);
    return v === null || v === undefined ? "" : String(v);
  }

  function sum(items, key) {
    return items.reduce((acc, it) => acc + (Number(it[key]) || 0), 0);
  }

  function ratio(a, b) {
    return b > 0 && a !== null && a !== undefined ? a / b : null;
  }

  function yearParam(filters) {
    const y = filters && filters.year;
    return y && y !== "ALL" ? parseInt(y, 10) : null;
  }

  function scopeLabel(year) {
    return year ? `Năm ${year}` : "Toàn bộ giai đoạn";
  }

  const ALL_TIME = "Toàn bộ giai đoạn";

  function failMessage(res) {
    if (res.status === 403) return "Vai trò hiện tại không có quyền xem dữ liệu này.";
    if (res.status === 0 || res.status === 408) return "Không kết nối được máy chủ API. Kiểm tra dịch vụ KhangNghi.Api.";
    if (res.status >= 500) return "Máy chủ gặp lỗi khi truy vấn kho dữ liệu (HTTP " + res.status + ").";
    return res.error || "Không truy vấn được dữ liệu.";
  }

  async function get(path, params) {
    if (!client) return { success: false, status: 0, error: "API client chưa sẵn sàng" };
    return client.get(path, params || {});
  }

  // Chuyển một phản hồi API dạng danh sách thành section đã chuẩn hóa
  function section(res, mapFn, scope) {
    if (!res || !res.success) {
      return { state: res && res.status === 403 ? "forbidden" : "error", items: [], message: failMessage(res || {}), scope };
    }
    const raw = Array.isArray(res.data) ? res.data : [];
    const items = mapFn ? raw.map(mapFn) : raw;
    return { state: items.length ? "ready" : "empty", items, message: "", scope };
  }

  function emptySection(scope, message) {
    return { state: "empty", items: [], message: message || "", scope };
  }

  // Tổng quan /dashboard/summary -> KPI. Không có đơn hàng nào = chưa có dữ liệu
  function summarySection(res, scope) {
    if (!res || !res.success) {
      return { state: res && res.status === 403 ? "forbidden" : "error", data: null, message: failMessage(res || {}), scope };
    }
    const d = res.data || {};
    const data = {
      revenue: num(d, "TotalRevenue"),
      profit: num(d, "TotalProfit"),
      margin: num(d, "ProfitMargin"),          // đã ở đơn vị %
      orders: num(d, "TotalOrders"),
      quantity: num(d, "TotalQuantitySold"),
      customers: num(d, "TotalCustomers"),
      shippingCost: num(d, "TotalShippingCost"), // backend không lọc theo năm
      lowStockAlerts: num(d, "LowStockAlertCount")
    };
    return { state: data.orders > 0 ? "ready" : "empty", data, message: "", scope };
  }

  // % thay đổi so với năm trước, chỉ khi có đủ dữ liệu cả 2 kỳ
  function yoyChange(yearItems, year, key) {
    if (!year) return null;
    const cur = yearItems.find(y => y.year === year);
    const prev = yearItems.find(y => y.year === year - 1);
    if (!cur || !prev || !prev[key]) return null;
    return { pct: ((cur[key] - prev[key]) / Math.abs(prev[key])) * 100, label: `so với ${year - 1}` };
  }

  const mapYear = r => ({ year: num(r, "Year"), revenue: num(r, "Revenue") || 0, profit: num(r, "Profit") || 0, quantity: num(r, "Quantity") || 0 });
  const mapCategory = r => ({ label: str(r, "Category") || "Chưa phân loại", revenue: num(r, "Revenue") || 0, profit: num(r, "Profit") || 0, quantity: num(r, "Quantity") || 0, margin: num(r, "ProfitMargin") });
  const mapRegion = r => ({ label: str(r, "Region") || "Chưa xác định", revenue: num(r, "Revenue") || 0, profit: num(r, "Profit") || 0, quantity: num(r, "Quantity") || 0 });

  // --------------------------------------------------------------------------
  // 1. TỔNG QUAN
  // --------------------------------------------------------------------------
  async function getOverview(filters = {}) {
    const year = yearParam(filters);
    const [sumRes, yearRes, catRes, regRes, monthRes] = await Promise.all([
      get("/dashboard/summary", year ? { year } : {}),
      get("/sales/by-year"),
      get("/sales/by-category"),
      get("/sales/by-region"),
      get("/sales/by-month")
    ]);

    const summary = summarySection(sumRes, scopeLabel(year));
    const byYear = section(yearRes, mapYear, ALL_TIME);
    byYear.items.sort((a, b) => a.year - b.year);

    return {
      year,
      summary,
      change: {
        revenue: yoyChange(byYear.items, year, "revenue"),
        profit: yoyChange(byYear.items, year, "profit"),
        quantity: yoyChange(byYear.items, year, "quantity")
      },
      byYear,
      byMonth: section(monthRes, r => ({ year: num(r, "Year"), month: num(r, "Month"), revenue: num(r, "Revenue") || 0, profit: num(r, "Profit") || 0 }), ALL_TIME),
      byCategory: section(catRes, mapCategory, ALL_TIME),
      byRegion: section(regRes, mapRegion, ALL_TIME)
    };
  }

  // --------------------------------------------------------------------------
  // 2. BÁN HÀNG
  // --------------------------------------------------------------------------
  async function getSales(filters = {}) {
    const year = yearParam(filters);
    const [sumRes, yearRes, monthRes, revProdRes, profProdRes, catRes] = await Promise.all([
      get("/dashboard/summary", year ? { year } : {}),
      get("/sales/by-year"),
      get("/sales/by-month"),
      get("/sales/top-products-revenue", { top: 15 }),
      get("/sales/top-products-profit", { top: 15 }),
      get("/sales/by-category")
    ]);

    const summary = summarySection(sumRes, scopeLabel(year));
    if (summary.data) {
      summary.data.aov = ratio(summary.data.revenue, summary.data.orders);
    }

    const byYear = section(yearRes, mapYear, ALL_TIME);
    byYear.items.sort((a, b) => a.year - b.year);

    // Xu hướng theo tháng; trang tự gộp theo năm hoặc lọc 12 tháng của một năm
    const trend = section(monthRes, r => ({ year: num(r, "Year"), month: num(r, "Month"), revenue: num(r, "Revenue") || 0, profit: num(r, "Profit") || 0 }), ALL_TIME);

    const mapProduct = r => ({
      key: str(r, "ProductKey"),
      label: str(r, "ProductName") || "Chưa đặt tên",
      category: str(r, "Category"),
      subCategory: str(r, "SubCategory"),
      revenue: num(r, "Revenue"),
      profit: num(r, "Profit"),
      quantity: num(r, "Quantity")
    });

    return {
      year,
      summary,
      change: {
        revenue: yoyChange(byYear.items, year, "revenue"),
        profit: yoyChange(byYear.items, year, "profit"),
        quantity: yoyChange(byYear.items, year, "quantity")
      },
      trend,
      topByRevenue: section(revProdRes, mapProduct, ALL_TIME),
      topByProfit: section(profProdRes, mapProduct, ALL_TIME),
      byCategory: section(catRes, mapCategory, ALL_TIME)
    };
  }

  // --------------------------------------------------------------------------
  // 3. MUA HÀNG
  // --------------------------------------------------------------------------
  async function getPurchase(filters = {}) {
    const year = yearParam(filters);
    const [ordersRes, catRes, supRes, prodRes] = await Promise.all([
      get("/purchase/orders-summary", year ? { year } : {}),
      get("/purchase/by-category", year ? { year } : {}),
      get("/purchase/top-suppliers-amount"),
      get("/purchase/top-purchased-products", { top: 10 })
    ]);

    const orders = section(ordersRes, r => ({
      id: str(r, "PurchaseOrderID"),
      date: str(r, "OrderDate"),
      supplier: str(r, "SupplierName"),
      warehouse: str(r, "WarehouseName"),
      lines: num(r, "LineItemCount"),
      quantity: num(r, "TotalQuantity") || 0,
      cost: num(r, "TotalCost") || 0,
      avgUnitCost: num(r, "AvgUnitCost")
    }), scopeLabel(year));

    let kpi = null;
    if (orders.state === "ready") {
      const totalCost = sum(orders.items, "cost");
      const totalQty = sum(orders.items, "quantity");
      const poCount = new Set(orders.items.map(o => o.id)).size;
      kpi = {
        totalCost,
        poCount,
        totalQty,
        avgUnitCost: ratio(totalCost, totalQty),
        avgPoValue: ratio(totalCost, poCount),
        suppliers: new Set(orders.items.map(o => o.supplier)).size
      };
    }

    // Xu hướng chi phí theo tháng, gộp từ danh sách PO (đúng phạm vi năm đang chọn)
    const monthMap = {};
    orders.items.forEach(o => {
      const m = (o.date || "").slice(0, 7);
      if (m) monthMap[m] = (monthMap[m] || 0) + o.cost;
    });
    const months = Object.keys(monthMap).sort();
    const trend = {
      state: orders.state === "ready" ? (months.length ? "ready" : "empty") : orders.state,
      message: orders.message,
      scope: orders.scope,
      items: months.map(m => ({ label: `${m.slice(5, 7)}/${m.slice(0, 4)}`, year: Number(m.slice(0, 4)), month: Number(m.slice(5, 7)), cost: monthMap[m] }))
    };

    return {
      year,
      kpi,
      orders,
      trend,
      byCategory: section(catRes, r => ({ label: str(r, "Category") || "Chưa phân loại", cost: num(r, "TotalCost") || 0, quantity: num(r, "TotalQuantity") || 0 }), scopeLabel(year)),
      suppliers: section(supRes, r => ({ label: str(r, "SupplierName"), cost: num(r, "TotalPurchaseAmount") || 0, quantity: num(r, "TotalQuantity") || 0 }), ALL_TIME),
      topProducts: section(prodRes, r => ({ label: str(r, "ProductName"), category: str(r, "Category"), cost: num(r, "TotalPurchaseAmount") || 0, quantity: num(r, "TotalQuantity") || 0 }), ALL_TIME)
    };
  }

  async function getPurchaseOrderDetail(poId) {
    const res = await get(`/purchase/order-detail/${encodeURIComponent(poId)}`, { top: 50 });
    return section(res, r => ({
      id: str(r, "PurchaseOrderID"),
      date: str(r, "OrderDate"),
      supplier: str(r, "SupplierName"),
      warehouse: str(r, "WarehouseName"),
      productId: str(r, "ProductID"),
      product: str(r, "ProductName"),
      category: str(r, "Category"),
      quantity: num(r, "PurchaseQuantity") || 0,
      unitCost: num(r, "UnitCost"),
      cost: num(r, "TotalCost") || 0
    }), ALL_TIME);
  }

  // --------------------------------------------------------------------------
  // 4. TỒN KHO
  // --------------------------------------------------------------------------
  async function getInventory() {
    const [regRes, moveRes, alertRes] = await Promise.all([
      get("/inventory/by-region"),
      get("/inventory/movement-over-time"),
      get("/inventory/stock-cover", { top: 500 })
    ]);

    const byRegion = section(regRes, r => ({
      label: str(r, "Region") || "Chưa xác định",
      onHand: num(r, "TotalOnHand") || 0,
      inbound: num(r, "TotalIn") || 0,
      outbound: num(r, "TotalOut") || 0
    }), ALL_TIME);

    const movement = section(moveRes, r => ({
      label: str(r, "Period") || `${num(r, "Month")}/${num(r, "Year")}`,
      year: num(r, "Year"),
      month: num(r, "Month"),
      inbound: num(r, "TotalQuantityIn") || 0,
      outbound: num(r, "TotalQuantityOut") || 0,
      onHand: num(r, "TotalQuantityOnHand") || 0
    }), ALL_TIME);

    // Cảnh báo theo số ngày tồn kho; "Cần nhập thêm" = lượng để đủ bán 3 ngày (chỉ mặt hàng Sắp hết)
    const alerts = section(alertRes, r => {
      const avgMonthly = num(r, "AvgMonthlySold") || 0;
      const onHand = num(r, "QuantityOnHand") || 0;
      const status = str(r, "StockStatus");
      return {
        key: str(r, "ProductKey"),
        product: str(r, "ProductName"),
        category: str(r, "Category"),
        onHand,
        avgMonthly,
        cover: num(r, "DaysOfCover"),
        status,
        need: status === "Sắp hết" ? Math.max(0, Math.ceil(avgMonthly / 30 * 3 - onHand)) : 0
      };
    }, "Tồn mới nhất so với bán 3 tháng gần nhất");

    let kpi = null;
    if (byRegion.state === "ready") {
      kpi = {
        onHand: sum(byRegion.items, "onHand"),
        inbound: sum(byRegion.items, "inbound"),
        outbound: sum(byRegion.items, "outbound"),
        lowStock: alerts.state === "ready" ? alerts.items.filter(x => x.status === "Sắp hết").length : null
      };
    }

    return { kpi, byRegion, movement, alerts };
  }

  // --------------------------------------------------------------------------
  // 5. VẬN CHUYỂN
  // --------------------------------------------------------------------------
  async function getShipping() {
    const [ordRes, costRes, lateRes, daysRes, regCostRes, regLateRes] = await Promise.all([
      get("/shipping/avg-cost-by-shipper"),
      get("/shipping/cost-by-shipper"),
      get("/shipping/highest-late-days"),
      get("/shipping/fastest-delivery"),
      get("/shipping/cost-by-region"),
      get("/shipping/late-delivery-by-region")
    ]);

    const orders = section(ordRes, r => ({ shipper: str(r, "ShipperName"), orders: num(r, "TotalOrders") || 0, avgCost: num(r, "AvgCostPerOrder") }), ALL_TIME);
    const costs = section(costRes, r => ({ shipper: str(r, "ShipperName"), cost: num(r, "TotalShippingCost") || 0 }), ALL_TIME);
    const late = section(lateRes, r => ({ shipper: str(r, "ShipperName"), lateOrders: num(r, "LateOrdersCount") || 0, totalOrders: num(r, "TotalOrders") || 0, avgLateDays: num(r, "AvgLateDays") }), ALL_TIME);
    const days = section(daysRes, r => ({ shipper: str(r, "ShipperName"), method: str(r, "ShippingMethod"), avgDays: num(r, "AvgDeliveryDays") }), ALL_TIME);

    // Bảng hiệu suất: ghép theo tên đơn vị vận chuyển; thiếu trường nào để null
    const shippers = {};
    const row = name => (shippers[name] = shippers[name] || { shipper: name, orders: null, cost: null, avgCost: null, lateRate: null, avgLateDays: null, avgDays: null });
    orders.items.forEach(o => { const r = row(o.shipper); r.orders = o.orders; r.avgCost = o.avgCost; });
    costs.items.forEach(c => { row(c.shipper).cost = c.cost; });
    late.items.forEach(l => { const r = row(l.shipper); r.lateRate = l.totalOrders > 0 ? (l.lateOrders / l.totalOrders) * 100 : null; r.avgLateDays = l.avgLateDays; });
    days.items.forEach(d => { const r = row(d.shipper); if (r.avgDays === null) r.avgDays = d.avgDays; });
    const performanceItems = Object.values(shippers).sort((a, b) => (b.orders || 0) - (a.orders || 0));

    let kpi = null;
    if (orders.state === "ready") {
      const totalOrders = sum(orders.items, "orders");
      const totalCost = costs.state === "ready" ? sum(costs.items, "cost") : null;
      const lateOrders = late.state === "ready" ? sum(late.items, "lateOrders") : null;
      const lateBase = late.state === "ready" ? sum(late.items, "totalOrders") : 0;
      kpi = {
        totalOrders,
        totalCost,
        avgCost: totalCost !== null ? ratio(totalCost, totalOrders) : null,
        lateOrders,
        // Tỷ lệ đúng hạn chỉ tính khi có trường LateDays (Q35)
        onTimeRate: lateBase > 0 ? (1 - lateOrders / lateBase) * 100 : null
      };
    }

    return {
      kpi,
      performance: { state: orders.state === "ready" || costs.state === "ready" ? (performanceItems.length ? "ready" : "empty") : orders.state, items: performanceItems, message: orders.message, scope: ALL_TIME },
      costByRegion: section(regCostRes, r => ({ label: str(r, "Region") || "Chưa xác định", cost: num(r, "TotalShippingCost") || 0, avgCost: num(r, "AvgCostPerOrder") }), ALL_TIME),
      lateByRegion: section(regLateRes, r => ({ label: str(r, "Region") || "Chưa xác định", rate: num(r, "LateRatePercent"), lateOrders: num(r, "LateOrderCount") || 0, totalOrders: num(r, "TotalOrders") || 0 }), ALL_TIME)
    };
  }

  // --------------------------------------------------------------------------
  // 6. KHÁCH HÀNG
  // --------------------------------------------------------------------------
  async function getCustomers() {
    const [sumRes, segRes, revRes, profRes] = await Promise.all([
      get("/dashboard/summary"),
      get("/sales/by-segment"),
      get("/sales/top-customers-revenue", { top: 15 }),
      get("/sales/top-customers-profit", { top: 50 })
    ]);

    const summary = summarySection(sumRes, ALL_TIME);
    const segments = section(segRes, r => ({ label: str(r, "Segment") || "Chưa phân khúc", revenue: num(r, "Revenue") || 0, profit: num(r, "Profit") || 0, orders: num(r, "OrderCount") || 0 }), ALL_TIME);
    const profitByKey = {};
    (profRes && profRes.success && Array.isArray(profRes.data) ? profRes.data : []).forEach(r => { profitByKey[str(r, "CustomerKey")] = num(r, "Profit"); });

    const top = section(revRes, r => ({
      key: str(r, "CustomerKey"),
      id: str(r, "CustomerID"),
      name: str(r, "CustomerName") || "Chưa đặt tên",
      segment: str(r, "Segment"),
      revenue: num(r, "Revenue") || 0,
      orders: num(r, "OrderCount"),
      profit: profitByKey[str(r, "CustomerKey")] ?? null
    }), ALL_TIME);

    let kpi = null;
    if (summary.state === "ready") {
      const s = summary.data;
      const segTotal = sum(segments.items, "revenue");
      const topSeg = [...segments.items].sort((a, b) => b.revenue - a.revenue)[0];
      const top10 = top.items.slice(0, 10);
      kpi = {
        customers: s.customers,
        revenuePerCustomer: ratio(s.revenue, s.customers),
        ordersPerCustomer: ratio(s.orders, s.customers),
        topSegment: topSeg ? topSeg.label : null,
        topSegmentShare: topSeg && segTotal > 0 ? (topSeg.revenue / segTotal) * 100 : null,
        top10Share: top10.length && s.revenue > 0 ? (sum(top10, "revenue") / s.revenue) * 100 : null
      };
    }

    return { summary, kpi, segments, top };
  }

  // --------------------------------------------------------------------------
  // 7. SẢN PHẨM
  // --------------------------------------------------------------------------
  async function getProducts() {
    const [catRes, subRes, revRes, profRes, discRes] = await Promise.all([
      get("/sales/by-category"),
      get("/sales/by-sub-category"),
      get("/sales/top-products-revenue", { top: 15 }),
      get("/sales/top-products-profit", { top: 50 }),
      get("/sales/high-discount-low-profit", { top: 10 })
    ]);

    const byCategory = section(catRes, mapCategory, ALL_TIME);
    const subCategories = section(subRes, r => ({ label: str(r, "SubCategory"), category: str(r, "Category"), quantity: num(r, "TotalQuantity") || 0, revenue: num(r, "Revenue") || 0 }), ALL_TIME);

    const profitByKey = {};
    (profRes && profRes.success && Array.isArray(profRes.data) ? profRes.data : []).forEach(r => { profitByKey[str(r, "ProductKey")] = num(r, "Profit"); });

    const top = section(revRes, r => {
      const profit = profitByKey[str(r, "ProductKey")] ?? null;
      const revenue = num(r, "Revenue") || 0;
      return {
        key: str(r, "ProductKey"),
        label: str(r, "ProductName") || "Chưa đặt tên",
        category: str(r, "Category"),
        subCategory: str(r, "SubCategory"),
        revenue,
        quantity: num(r, "Quantity"),
        profit,
        margin: profit !== null && revenue > 0 ? (profit / revenue) * 100 : null
      };
    }, ALL_TIME);

    let kpi = null;
    if (byCategory.state === "ready") {
      const revenue = sum(byCategory.items, "revenue");
      const profit = sum(byCategory.items, "profit");
      kpi = {
        categories: byCategory.items.length,
        subCategories: subCategories.state === "ready" ? subCategories.items.length : null,
        revenue,
        profit,
        margin: ratio(profit, revenue) !== null ? (profit / revenue) * 100 : null
      };
    }

    return {
      kpi,
      byCategory,
      subCategories,
      top,
      lowMargin: section(discRes, r => ({ label: str(r, "ProductName"), category: str(r, "Category"), discount: num(r, "AvgDiscountPercent"), profit: num(r, "TotalProfit"), revenue: num(r, "TotalRevenue") }), ALL_TIME)
    };
  }

  // --------------------------------------------------------------------------
  // 8. NHÀ CUNG CẤP
  // --------------------------------------------------------------------------
  async function getSuppliers() {
    const res = await get("/purchase/top-suppliers-amount");
    const suppliers = section(res, r => ({ label: str(r, "SupplierName"), cost: num(r, "TotalPurchaseAmount") || 0, quantity: num(r, "TotalQuantity") || 0 }), ALL_TIME);
    suppliers.items.sort((a, b) => b.cost - a.cost);

    let kpi = null;
    if (suppliers.state === "ready") {
      const total = sum(suppliers.items, "cost");
      suppliers.items.forEach(s => { s.share = total > 0 ? (s.cost / total) * 100 : null; s.unitCost = ratio(s.cost, s.quantity); });
      kpi = {
        count: suppliers.items.length,
        totalCost: total,
        top: suppliers.items[0].label,
        top1Share: suppliers.items[0].share,
        top3Share: total > 0 ? (sum(suppliers.items.slice(0, 3), "cost") / total) * 100 : null
      };
    }
    return { kpi, suppliers };
  }

  // --------------------------------------------------------------------------
  // 9. KHU VỰC
  // --------------------------------------------------------------------------
  async function getGeography() {
    const [regRes, countryRes, cityRes, trendRes] = await Promise.all([
      get("/sales/by-region"),
      get("/sales/profit-by-country"),
      get("/sales/top-cities-quantity", { top: 15 }),
      get("/sales/regional-revenue-trend")
    ]);
    const regions = section(regRes, mapRegion, ALL_TIME);
    regions.items.sort((a, b) => b.revenue - a.revenue);
    const countries = section(countryRes, r => ({ label: str(r, "Country") || "Chưa xác định", profit: num(r, "Profit") || 0, revenue: num(r, "Revenue") || 0 }), ALL_TIME);
    countries.items.sort((a, b) => b.profit - a.profit);
    return {
      regions,
      countries,
      cities: section(cityRes, r => ({ label: str(r, "City"), region: str(r, "Region"), quantity: num(r, "TotalQuantity") || 0, revenue: num(r, "Revenue") || 0 }), ALL_TIME),
      trend: section(trendRes, r => ({ region: str(r, "Region") || "Chưa xác định", year: num(r, "Year"), revenue: num(r, "Revenue") || 0 }), ALL_TIME)
    };
  }

  // --------------------------------------------------------------------------
  // 10. PHÂN TÍCH CHÉO (Bán - Tồn - Mua - Vận chuyển)
  // --------------------------------------------------------------------------
  async function getCrossAnalysis() {
    // Q38 trả toàn bộ mặt hàng theo số ngày tồn kho (đủ để đếm tình trạng); Q39 chỉ là cùng dữ liệu xếp ngược nên không gọi riêng
    const [coverRes, effRes] = await Promise.all([
      get("/cross-analysis/stockout-risk", { top: 5000 }),
      get("/cross-analysis/shipping-efficiency")
    ]);
    return {
      cover: section(coverRes, r => ({ productKey: num(r, "ProductKey"), label: str(r, "ProductName"), category: str(r, "Category"), avgMonthly: num(r, "AvgMonthlySold") || 0, onHand: num(r, "QuantityOnHand") || 0, cover: num(r, "DaysOfCover"), status: str(r, "StockStatus") }), ALL_TIME),
      efficiency: section(effRes, r => ({ label: str(r, "Region") || "Chưa xác định", revenue: num(r, "TotalRevenue") || 0, profit: num(r, "TotalProfit") || 0, shippingCost: num(r, "ShippingCost") || 0, ratio: num(r, "ShippingCostRatioPercent"), netProfit: num(r, "NetProfitAfterShipping") }), ALL_TIME)
    };
  }

  // Số liệu gốc để diễn giải số ngày tồn kho của một mặt hàng
  async function getStockCoverDetail(productKey) {
    const res = await get(`/cross-analysis/stock-cover/${encodeURIComponent(productKey)}`);
    if (!res.success) return { success: false, message: failMessage(res) };
    const d = res.data || {};
    const list = key => (Array.isArray(pick(d, key)) ? pick(d, key) : []);
    return {
      success: true,
      stock: list("Stock").map(r => ({ warehouse: str(r, "Warehouse"), city: str(r, "City"), date: str(r, "SnapshotDate"), onHand: num(r, "QuantityOnHand") || 0, reorder: num(r, "ReorderLevel") || 0 })),
      sales: list("Sales").map(r => ({ year: num(r, "Year"), month: num(r, "Month"), quantity: num(r, "Quantity") || 0 }))
    };
  }
  // --------------------------------------------------------------------------
  // 11. DỰ BÁO DOANH SỐ (ModelKLTN Hybrid Optimal)
  // --------------------------------------------------------------------------
  async function getForecast(opts = {}) {
    const params = typeof opts === "number"
      ? { granularity: "month", horizon: opts }
      : {
          granularity: opts.granularity || "month",
          horizon: opts.horizon || 6,
          ...(opts.startDate ? { startDate: opts.startDate } : {}),
          ...(opts.endDate ? { endDate: opts.endDate } : {})
        };
    const res = await get("/forecast/sales", params);
    if (!res.success) {
      return { state: res.status === 403 ? "forbidden" : "error", message: failMessage(res), points: [] };
    }
    const d = res.data || {};
    const points = (Array.isArray(pick(d, "Points")) ? pick(d, "Points") : []).map(p => ({
      label: str(p, "Period"),
      dateKey: str(p, "DateKey"),
      subLabel: str(p, "SubLabel"),
      isFuture: !!pick(p, "IsFuture"),
      actual: pick(p, "IsFuture") ? null : num(p, "ActualValue"),
      samePeriodActual: num(p, "SamePeriodActualValue"),
      forecast: num(p, "ForecastValue"),
      lower: pick(p, "IsFuture") ? num(p, "LowerBound") : null,
      upper: pick(p, "IsFuture") ? num(p, "UpperBound") : null
    }));
    const modelName = str(d, "ModelName");
    const futureCount = points.filter(p => p.isFuture).length;
    return {
      // Chỉ coi là có kết quả khi mô hình trả về điểm dự báo tương lai
      state: futureCount > 0 ? "ready" : "empty",
      message: "",
      modelName,
      isFallback: /fallback/i.test(modelName),
      targetMetric: str(d, "TargetMetric"),
      granularity: str(d, "Granularity") || params.granularity,
      forecastStart: str(d, "ForecastStart") || "2026-01-01",
      forecastEnd: str(d, "ForecastEnd") || "2027-12-31",
      selectedStart: str(d, "SelectedStart"),
      selectedEnd: str(d, "SelectedEnd"),
      totalActualRevenue: num(d, "TotalActualRevenue"),
      totalForecastRevenue: num(d, "TotalForecastRevenue"),
      averagePeriodRevenue: num(d, "AveragePeriodRevenue"),
      peakPeriodLabel: str(d, "PeakPeriodLabel"),
      peakPeriodRevenue: num(d, "PeakPeriodRevenue"),
      mae: num(d, "MeanAbsoluteError"),
      rmse: num(d, "RootMeanSquaredError"),
      historyCount: points.length - futureCount,
      futureCount,
      points
    };
  }

  async function getForecastMonthDetail(year, month) {
    const res = await get("/forecast/month-detail", { year, month });
    if (!res.success || !res.data) {
      return { state: res.status === 403 ? "forbidden" : "error", message: failMessage(res) };
    }
    const d = res.data;
    const mapPt = p => ({
      label: str(p, "Period"),
      dateKey: str(p, "DateKey"),
      subLabel: str(p, "SubLabel"),
      isFuture: !!pick(p, "IsFuture"),
      actual: pick(p, "IsFuture") ? null : num(p, "ActualValue"),
      samePeriodActual: num(p, "SamePeriodActualValue"),
      forecast: num(p, "ForecastValue"),
      lower: pick(p, "IsFuture") ? num(p, "LowerBound") : null,
      upper: pick(p, "IsFuture") ? num(p, "UpperBound") : null
    });
    return {
      state: "ready",
      year: num(d, "Year") || year,
      month: num(d, "Month") || month,
      monthLabel: str(d, "MonthLabel") || `Tháng ${month}/${year}`,
      isFuture: !!pick(d, "IsFuture"),
      totalActual: num(d, "TotalActual"),
      totalSamePeriodActual: num(d, "TotalSamePeriodActual"),
      totalForecast: num(d, "TotalForecast"),
      averageWeeklyForecast: num(d, "AverageWeeklyForecast"),
      averageDailyForecast: num(d, "AverageDailyForecast"),
      weeks: (Array.isArray(pick(d, "Weeks")) ? pick(d, "Weeks") : []).map(mapPt),
      days: (Array.isArray(pick(d, "Days")) ? pick(d, "Days") : []).map(mapPt)
    };
  }

  // --------------------------------------------------------------------------
  // 12. TRẠNG THÁI DỮ LIỆU, ETL, SSAS
  // --------------------------------------------------------------------------
  async function getSsasStatus() {
    const res = await get("/ssas/status");
    if (!res.success || !res.data) {
      return { state: res.status === 403 ? "forbidden" : "error", message: failMessage(res) };
    }
    const s = res.data.data || res.data;
    return {
      state: "ready",
      connected: !!pick(s, "IsServerConnected"),
      server: str(s, "ServerInstance"),
      catalog: str(s, "CatalogName"),
      catalogExists: !!pick(s, "CatalogExists"),
      cube: str(s, "CubeName"),
      cubeStatus: str(s, "CubeStatus"),
      error: str(s, "ErrorMessage"),
      action: str(s, "ActionRequired")
    };
  }

  async function getDataStatus() {
    const [tablesRes, ssas] = await Promise.all([get("/etl/tables-status"), getSsasStatus()]);
    const tables = section(tablesRes, r => ({
      name: str(r, "TableName"),
      type: str(r, "TableType").toLowerCase() === "fact" ? "Fact" : "Dimension",
      rows: num(r, "RowCount") || 0,
      lastUpdated: str(r, "LastUpdated")
    }), "");
    tables.items.sort((a, b) => (a.type === b.type ? a.name.localeCompare(b.name) : a.type === "Fact" ? -1 : 1));
    return { tables, ssas, checkedAt: new Date() };
  }

  // Số liệu gốc toàn giai đoạn cho phân tích giả định
  async function getSummary() {
    return summarySection(await get("/dashboard/summary"), ALL_TIME);
  }

  async function getEtlLogs(limit = 50) {
    const res = await get("/etl/logs", { limit });
    return section(res, r => ({
      id: num(r, "LogID"),
      start: str(r, "StartTime"),
      end: str(r, "EndTime"),
      status: str(r, "Status"),
      rows: num(r, "RowsInserted"),
      error: str(r, "ErrorMessage"),
      by: str(r, "TriggeredBy")
    }), "");
  }

  // Kiểm tra nhanh kết nối DWH cho nhãn trạng thái trên topbar (mọi vai trò đều gọi được)
  async function getConnectionStatus() {
    const res = await get("/dashboard/summary");
    if (!res.success) return { state: "error", message: failMessage(res) };
    return { state: num(res.data, "TotalOrders") > 0 ? "ready" : "empty" };
  }

  // --------------------------------------------------------------------------
  // 13. SSAS / MDX
  // --------------------------------------------------------------------------
  async function getMdxCatalog() {
    const res = await get("/ssas/catalog-40");
    if (!res.success) return { state: res.status === 403 ? "forbidden" : "error", items: [], message: failMessage(res) };
    const list = Array.isArray(res.data && res.data.data) ? res.data.data : [];
    const items = list.map(q => ({
      id: str(q, "Id"),
      order: num(q, "OrderNumber"),
      section: str(q, "Section"),
      title: str(q, "Title"),
      purpose: str(q, "BusinessPurpose"),
      operation: str(q, "OperationType"),
      mdx: str(q, "MdxQuery"),
      verified: str(q, "VerificationStatus") === "VERIFIED",
      factTable: str(q, "SourceFactTable")
    })).sort((a, b) => (a.order || 0) - (b.order || 0));
    return { state: items.length ? "ready" : "empty", items, message: "" };
  }

  // Thực thi MDX; phân biệt lỗi kết nối SSAS với truy vấn không có kết quả
  async function executeMdx(mdxQuery) {
    if (!client) return { state: "error", message: "API client chưa sẵn sàng" };
    const res = await client.post("/ssas/query", { mdxQuery });
    const body = res.data || {};
    if (res.success && body.success !== false) {
      const rows = Array.isArray(body.data) ? body.data : [];
      return {
        state: rows.length ? "ready" : "empty",
        rows,
        columns: Array.isArray(body.columns) && body.columns.length ? body.columns : (rows[0] ? Object.keys(rows[0]) : []),
        timeMs: body.executionTimeMs ?? null
      };
    }
    if (res.status === 403) return { state: "forbidden", message: failMessage(res) };
    const msg = body.message || res.error || "Lỗi thực thi MDX";
    const isConnection = /oledb|not supported|kết nối|connect|server|MSOLAP|instance|timeout/i.test(msg) || res.status === 0;
    return { state: isConnection ? "unavailable" : "error", message: msg, timeMs: body.executionTimeMs ?? null };
  }

  // --------------------------------------------------------------------------
  // 13c. QUICK CHART DÀNH CHO CÂU HỎI PHÂN TÍCH (MDX-19 ĐẾN MDX-24)
  // --------------------------------------------------------------------------
  // Câu trả lời trọng tâm của một câu hỏi phân tích; khóa cột chuẩn hóa về chữ thường
  async function getQuestionAnswer(mdxId) {
    const res = await get(`/question-answer/${encodeURIComponent(mdxId)}`);
    if (!res.success) return { state: res.status === 403 ? "forbidden" : "error", rows: [], message: failMessage(res) };
    const list = Array.isArray(res.data && res.data.data) ? res.data.data : [];
    const rows = list.map(r => {
      const o = {};
      Object.keys(r).forEach(k => { o[k.toLowerCase()] = r[k]; });
      return o;
    });
    return { state: rows.length ? "ready" : "empty", rows, message: "" };
  }

  async function getQuickChart(mdxId) {
    if (!client) return { state: "error", message: "API client chưa sẵn sàng" };
    const res = await client.get(`/ssas/quick-chart/${encodeURIComponent(mdxId)}`);
    if (!res.success) {
      return { state: res.status === 403 ? "forbidden" : "error", message: failMessage(res) };
    }
    const data = res.data && res.data.data ? res.data.data : res.data;
    return {
      state: "ready",
      data: data
    };
  }

  // --------------------------------------------------------------------------
  // 13d. PHÂN TÍCH KINH DOANH ĐA CHIỀU (MULTI-DIMENSIONAL ANALYSIS)
  // --------------------------------------------------------------------------
  async function getMultiDimAnalysis(measure, row, col) {
    if (!client) return { state: "error", message: "API client chưa sẵn sàng" };
    const res = await client.get("/ssas/multidim-analysis", { measure, row, col });
    if (!res.success) {
      return { state: res.status === 403 ? "forbidden" : "error", message: failMessage(res) };
    }
    const data = res.data && res.data.data ? res.data.data : res.data;
    return {
      state: "ready",
      data: data
    };
  }

  // Siêu dữ liệu khối KhangNghi_Cube dùng cho trình dựng truy vấn OLAP
  function getCubeMetadata() {
    return {
      cube: "KhangNghi_Cube",
      measures: [
        { id: "[Measures].[Sales Amount]", name: "Doanh thu" },
        { id: "[Measures].[Profit Amount]", name: "Lợi nhuận" },
        { id: "[Measures].[Quantity]", name: "Số lượng bán" },
        { id: "[Measures].[Discount Amount]", name: "Chiết khấu" },
        { id: "[Measures].[Shipping Cost]", name: "Chi phí vận chuyển" },
        { id: "[Measures].[Total Cost]", name: "Chi phí mua hàng" },
        { id: "[Measures].[Purchase Quantity]", name: "Số lượng nhập" },
        { id: "[Measures].[Quantity On Hand]", name: "Tồn kho" }
      ],
      levels: [
        { id: "[Dim Time].[Year]", name: "Năm" },
        { id: "[Dim Time].[Quarter]", name: "Quý" },
        { id: "[Dim Time].[Month]", name: "Tháng" },
        { id: "[Dim Product].[Category]", name: "Danh mục sản phẩm" },
        { id: "[Dim Product].[Sub Category]", name: "Nhóm sản phẩm" },
        { id: "[Dim Customer].[Segment]", name: "Phân khúc khách hàng" },
        { id: "[Dim Location].[Region]", name: "Khu vực" },
        { id: "[Dim Location].[City]", name: "Thành phố" },
        { id: "[Dim Sales Channel].[Channel Name]", name: "Kênh bán hàng" },
        { id: "[Dim Warehouse].[Warehouse Name]", name: "Kho hàng" },
        { id: "[Dim Supplier].[Supplier Name]", name: "Nhà cung cấp" },
        { id: "[Dim Shipper].[Shipper Name]", name: "Đơn vị vận chuyển" }
      ]
    };
  }

  // --------------------------------------------------------------------------
  // 13b. CHI TIẾT MỘT NĂM (bấm mốc năm trên biểu đồ)
  // --------------------------------------------------------------------------
  const mapBreakdown = key => r => ({
    label: str(r, key) || "Chưa xác định",
    revenue: num(r, "Revenue") || 0,
    profit: num(r, "Profit") || 0,
    quantity: num(r, "Quantity") || 0
  });

  const mapYearProduct = r => ({
    label: str(r, "ProductName") || "Chưa đặt tên",
    category: str(r, "Category") || "Chưa phân loại",
    revenue: num(r, "Revenue") || 0,
    profit: num(r, "Profit") || 0,
    quantity: num(r, "Quantity") || 0
  });

  const mapQuarter = r => ({
    label: str(r, "Period") || `Q${num(r, "Quarter")}/${num(r, "Year")}`,
    revenue: num(r, "Revenue") || 0,
    profit: num(r, "Profit") || 0
  });

  async function getYearDetail(year) {
    const [sumRes, breakRes, quarterRes] = await Promise.all([
      get("/dashboard/summary", { year }),
      get("/sales/year-breakdown", { year, top: 10 }),
      get("/sales/by-quarter", { year })
    ]);
    const pickList = key => ({ success: breakRes.success, status: breakRes.status, error: breakRes.error, data: breakRes.success && breakRes.data ? breakRes.data[key] : [] });
    return {
      year,
      summary: summarySection(sumRes, scopeLabel(year)),
      quarters: section(quarterRes, mapQuarter, scopeLabel(year)),
      categories: section(pickList("categories"), mapBreakdown("Category"), scopeLabel(year)),
      regions: section(pickList("regions"), mapBreakdown("Region"), scopeLabel(year)),
      products: section(pickList("products"), mapYearProduct, scopeLabel(year))
    };
  }

  // --------------------------------------------------------------------------
  // 14. TRA CỨU CHI TIẾT (khách hàng, đơn hàng)
  // --------------------------------------------------------------------------
  async function getCustomerDetail(customerId) {
    if (!customerId) return { success: false, message: "Thiếu mã khách hàng" };
    const res = await get(`/customers/${encodeURIComponent(customerId)}`);
    if (res.success && res.data) return { success: true, data: res.data };
    return { success: false, message: res.status === 404 ? "Không tìm thấy khách hàng trong DWH." : failMessage(res) };
  }

  async function getOrderDetail(orderId) {
    if (!orderId) return { success: false, message: "Thiếu mã đơn hàng" };
    const res = await get(`/orders/${encodeURIComponent(orderId)}`);
    if (res.success && res.data) return { success: true, data: res.data };
    return { success: false, message: res.status === 404 ? "Không tìm thấy đơn hàng trong DWH." : failMessage(res) };
  }

  // Xuất đúng các dòng đang hiển thị ra CSV (UTF-8 BOM để Excel đọc tiếng Việt)
  function exportToCSV(filename, rows, columns) {
    if (!rows || !rows.length) return false;
    const cols = columns || Object.keys(rows[0]).map(k => ({ key: k, label: k }));
    const esc = v => `"${String(v === null || v === undefined ? "" : v).replace(/"/g, '""')}"`;
    const lines = [cols.map(c => esc(c.label)).join(",")].concat(rows.map(r => cols.map(c => esc(r[c.key])).join(",")));
    const blob = new Blob(["﻿" + lines.join("\n")], { type: "text/csv;charset=utf-8" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);
    return true;
  }

  return {
    getOverview,
    getSales,
    getPurchase,
    getPurchaseOrderDetail,
    getInventory,
    getShipping,
    getCustomers,
    getProducts,
    getSuppliers,
    getGeography,
    getCrossAnalysis,
    getStockCoverDetail,
    getForecast,
    getForecastMonthDetail,
    getSummary,
    getSsasStatus,
    getDataStatus,
    getEtlLogs,
    getConnectionStatus,
    getMdxCatalog,
    executeMdx,
    getQuickChart,
    getQuestionAnswer,
    getMultiDimAnalysis,
    getCubeMetadata,
    getCustomerDetail,
    getOrderDetail,
    getYearDetail,
    exportToCSV
  };
})();
