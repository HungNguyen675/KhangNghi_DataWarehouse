/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - MOCK REST API ENGINE
   100% Mathematically Consistent Aggregations across Dimensions & Facts
   ========================================================================== */

window.KhangNghiApi = (function () {
  const dwh = window.KhangNghiMockData;

  function calculateBaseMetrics(filters = {}) {
    const yr = filters.year && filters.year !== "ALL" ? parseInt(filters.year) : 2015;
    const base = dwh.yearlyMetrics[yr] || dwh.yearlyMetrics[2015];

    // Compute region scaling factor
    let regFactor = 1.0;
    if (filters.region && filters.region !== "All Regions") {
      regFactor = 0.08 + (filters.region.length % 7) * 0.035;
    }

    // Compute category scaling factor
    let catFactor = 1.0;
    if (filters.category && filters.category !== "All Categories") {
      catFactor = filters.category.includes("Thiết bị") ? 0.48 : filters.category.includes("Nội thất") ? 0.35 : 0.17;
    }

    const rev = Math.round(base.revenue * regFactor * catFactor);
    const prof = Math.round(base.profit * regFactor * catFactor);
    const ord = Math.round(base.orders * regFactor * catFactor);
    const qty = Math.round(base.qty * regFactor * catFactor);
    const margin = rev > 0 ? ((prof / rev) * 100).toFixed(1) : "0.0";
    const aov = ord > 0 ? Math.round(rev / ord) : 0;
    const invVal = Math.round(base.inventoryVal * regFactor);
    const shipCost = Math.round(base.shipCost * regFactor);

    return {
      revenue: rev,
      profit: prof,
      orders: ord,
      quantity: qty,
      margin: margin,
      aov: aov,
      inventoryVal: invVal,
      onTimeRate: base.onTimeRate,
      avgDiscount: (base.avgDiscount * 100).toFixed(1) + "%",
      shippingCost: shipCost,
      priorRevChange: 14.2,
      priorProfChange: 18.5,
      priorOrdChange: 9.4
    };
  }

  function getSalesByCategory(filters = {}) {
    const base = calculateBaseMetrics(filters);
    const rev = base.revenue;
    const prof = base.profit;

    return [
      { category: "Thiết bị Văn phòng", revenue: Math.round(rev * 0.48), profit: Math.round(prof * 0.52), qty: Math.round(base.quantity * 0.35) },
      { category: "Nội thất Văn phòng", revenue: Math.round(rev * 0.35), profit: Math.round(prof * 0.32), qty: Math.round(base.quantity * 0.25) },
      { category: "Đồ dùng Văn phòng", revenue: Math.round(rev * 0.17), profit: Math.round(prof * 0.16), qty: Math.round(base.quantity * 0.40) }
    ];
  }

  function getRegionBreakdown(filters = {}) {
    const base = calculateBaseMetrics(filters);
    const rev = base.revenue;

    return [
      { region: "North America", revenue: Math.round(rev * 0.32) },
      { region: "South East Asia", revenue: Math.round(rev * 0.26) },
      { region: "Western Europe", revenue: Math.round(rev * 0.21) },
      { region: "East Asia", revenue: Math.round(rev * 0.14) },
      { region: "South America", revenue: Math.round(rev * 0.07) }
    ];
  }

  function getCustomerSegments() {
    return [
      { segment: "Corporate (Doanh nghiệp)", value: 58 },
      { segment: "Consumer (Cá nhân)", value: 27 },
      { segment: "Home Office (Văn phòng nhỏ)", value: 15 }
    ];
  }

  function getInventoryHealth() {
    return {
      healthyCount: 1420,
      lowStockCount: 45,
      criticalCount: 8,
      overstockCount: 112,
      products: dwh.products
    };
  }

  function getShippingPerformance() {
    return {
      totalShipments: 25749,
      shippingCost: 310000,
      avgDeliveryDays: 2.3,
      onTimeRate: 95.8,
      lateRate: 4.2,
      shippers: dwh.shippers
    };
  }

  function getForecastSeries() {
    return dwh.monthlyFactSeries;
  }

  function simulateWhatIfScenario(discountPct, shipCostPct, growthPct) {
    const base = dwh.yearlyMetrics[2015];
    const currentRev = base.revenue;
    const currentProfit = base.profit;
    const currentShip = base.shipCost;

    const newRev = currentRev * (1 + growthPct / 100);
    const discountEffect = newRev * (discountPct / 100);
    const newShip = currentShip * (1 + shipCostPct / 100);

    const newProfit = Math.round((currentProfit * (newRev / currentRev)) - discountEffect - (newShip - currentShip));
    const newMargin = ((newProfit / newRev) * 100).toFixed(1);
    const diff = newProfit - currentProfit;

    return {
      currentProfit,
      projectedProfit: newProfit,
      difference: diff,
      projectedMargin: newMargin,
      newRevenue: Math.round(newRev)
    };
  }

  // ==========================================================================
  // PURCHASE ANALYTICS ENGINE (MATHEMATICALLY CONSISTENT AGGREGATIONS)
  // ==========================================================================
  function calculatePurchaseBaseMetrics(filters = {}) {
    const yr = filters.year && filters.year !== "ALL" ? parseInt(filters.year) : 2015;
    const base = dwh.yearlyPurchaseMetrics[yr] || dwh.yearlyPurchaseMetrics[2015];

    let supFactor = 1.0;
    if (filters.supplier && filters.supplier !== "All Suppliers") {
      const matchSup = (dwh.suppliers || []).find(s => s.name === filters.supplier);
      supFactor = matchSup ? matchSup.sharePct / 100 : 0.25;
    }

    let catFactor = 1.0;
    if (filters.category && filters.category !== "All Categories") {
      catFactor = filters.category.includes("Thiết bị") ? 0.48 : filters.category.includes("Nội thất") ? 0.35 : 0.17;
    }

    let mfgFactor = 1.0;
    if (filters.manufacturer && filters.manufacturer !== "All Manufacturers") {
      mfgFactor = 0.30;
    }

    const totalCost = Math.round(base.purchaseCost * supFactor * catFactor * mfgFactor);
    const poCount = Math.max(1, Math.round(base.poCount * supFactor * catFactor * mfgFactor));
    const qty = Math.max(10, Math.round(base.qty * supFactor * catFactor * mfgFactor));
    const weightedUnitCost = qty > 0 ? (totalCost / qty).toFixed(1) : "0.0";
    const avgPOValue = poCount > 0 ? Math.round(totalCost / poCount) : 0;
    const activeSuppliers = filters.supplier && filters.supplier !== "All Suppliers" ? 1 : base.activeSuppliers;

    return {
      totalPurchaseCost: totalCost,
      poCount: poCount,
      purchasedQty: qty,
      weightedUnitCost: parseFloat(weightedUnitCost),
      activeSuppliers: activeSuppliers,
      avgPOValue: avgPOValue,
      priorCostChange: 20.5,
      priorQtyChange: 18.3,
      priorPOChange: 18.1
    };
  }

  function getPurchaseTrend(filters = {}, granularity = "monthly") {
    const base = calculatePurchaseBaseMetrics(filters);
    const factor = base.totalPurchaseCost / 3110000;

    if (granularity === "quarterly") {
      return [
        { label: "Q1 (T1-T3)", cost: Math.round(650000 * factor), qty: Math.round(5490 * factor), unitCost: 118.4 },
        { label: "Q2 (T4-T6)", cost: Math.round(775000 * factor), qty: Math.round(6430 * factor), unitCost: 120.5 },
        { label: "Q3 (T7-T9)", cost: Math.round(870000 * factor), qty: Math.round(7170 * factor), unitCost: 121.3 },
        { label: "Q4 (T10-T12)", cost: Math.round(1015000 * factor), qty: Math.round(8210 * factor), unitCost: 123.6 }
      ];
    } else if (granularity === "yearly") {
      return [
        { label: "2012", cost: Math.round(1650000 * factor), qty: Math.round(14200 * factor), unitCost: 116.2 },
        { label: "2013", cost: Math.round(2050000 * factor), qty: Math.round(17500 * factor), unitCost: 117.1 },
        { label: "2014", cost: Math.round(2580000 * factor), qty: Math.round(21800 * factor), unitCost: 118.3 },
        { label: "2015", cost: Math.round(3110000 * factor), qty: Math.round(25800 * factor), unitCost: 120.5 }
      ];
    }

    return (dwh.monthlyPurchaseSeries || []).map(m => ({
      label: m.month,
      cost: Math.round(m.purchaseCost * factor),
      qty: Math.round(m.qty * factor),
      unitCost: m.unitCost
    }));
  }

  function getSupplierSpend(filters = {}) {
    const base = calculatePurchaseBaseMetrics(filters);
    const total = base.totalPurchaseCost;

    return (dwh.suppliers || []).map(s => {
      const cost = Math.round(total * (s.sharePct / 100));
      return {
        id: s.id,
        name: s.name,
        country: s.country,
        cost: cost,
        qty: Math.round(base.purchasedQty * (s.sharePct / 100)),
        orders: Math.max(1, Math.round(base.poCount * (s.sharePct / 100))),
        sharePct: s.sharePct
      };
    });
  }

  function getSupplierConcentration(filters = {}) {
    const suppliers = getSupplierSpend(filters);
    suppliers.sort((a, b) => b.cost - a.cost);

    const total = suppliers.reduce((sum, s) => sum + s.cost, 0) || 1;
    const top1 = suppliers[0] ? ((suppliers[0].cost / total) * 100).toFixed(1) : "0.0";
    const top3 = suppliers.slice(0, 3).reduce((sum, s) => sum + s.cost, 0);
    const top3Pct = ((top3 / total) * 100).toFixed(1);
    const top5 = suppliers.slice(0, 5).reduce((sum, s) => sum + s.cost, 0);
    const top5Pct = ((top5 / total) * 100).toFixed(1);

    return {
      top1Supplier: suppliers[0] ? suppliers[0].name : "N/A",
      top1SharePct: top1,
      top3SharePct: top3Pct,
      top5SharePct: top5Pct
    };
  }

  function getPurchaseCostByCategory(filters = {}) {
    const base = calculatePurchaseBaseMetrics(filters);
    const total = base.totalPurchaseCost;

    return [
      { category: "Thiết bị Văn phòng", cost: Math.round(total * 0.48), sharePct: 48.0, subcategories: [
        { name: "Máy in", cost: Math.round(total * 0.22) },
        { name: "Máy chiếu", cost: Math.round(total * 0.16) },
        { name: "Thiết bị mạng", cost: Math.round(total * 0.10) }
      ]},
      { category: "Nội thất Văn phòng", cost: Math.round(total * 0.35), sharePct: 35.0, subcategories: [
        { name: "Bàn làm việc", cost: Math.round(total * 0.18) },
        { name: "Ghế công thái học", cost: Math.round(total * 0.17) }
      ]},
      { category: "Đồ dùng Văn phòng", cost: Math.round(total * 0.17), sharePct: 17.0, subcategories: [
        { name: "Giấy in A4", cost: Math.round(total * 0.10) },
        { name: "Bút ký cao cấp", cost: Math.round(total * 0.07) }
      ]}
    ];
  }

  function getCostVariance(filters = {}) {
    return [
      { id: "PRD-101", name: "Máy in Laser Canon LBP2900", category: "Thiết bị Văn phòng", prevCost: 2750000, currentCost: 2900000, change: 150000, changePct: 5.45, qty: 560, trend: "up" },
      { id: "PRD-105", name: "Máy chiếu Panasonic PT-LB386", category: "Thiết bị Văn phòng", prevCost: 10100000, currentCost: 11950000, change: 1850000, changePct: 18.32, qty: 95, trend: "up" },
      { id: "PRD-102", name: "Ghế công thái học Ergonomic X5", category: "Nội thất Văn phòng", prevCost: 3100000, currentCost: 2980000, change: -120000, changePct: -3.87, qty: 300, trend: "down" },
      { id: "PRD-103", name: "Bàn làm việc thông minh SmartDesk", category: "Nội thất Văn phòng", prevCost: 4350000, currentCost: 4400000, change: 50000, changePct: 1.15, qty: 140, trend: "up" },
      { id: "PRD-104", name: "Giấy in A4 Double A 80gsm", category: "Đồ dùng Văn phòng", prevCost: 71000, currentCost: 68000, change: -3000, changePct: -4.23, qty: 10000, trend: "down" }
    ];
  }

  function getSupplierCategoryMatrix(filters = {}) {
    const base = calculatePurchaseBaseMetrics(filters);
    const t = base.totalPurchaseCost;

    return [
      { supplier: "Tập đoàn HP Việt Nam", thietBi: Math.round(t * 0.28), noiThat: 0, doDung: Math.round(t * 0.044) },
      { supplier: "Canon Asia Marketing Corp", thietBi: Math.round(t * 0.20), noiThat: 0, doDung: Math.round(t * 0.06) },
      { supplier: "Nội thất Hòa Phát", thietBi: 0, noiThat: Math.round(t * 0.20), doDung: 0 },
      { supplier: "Deli Office Supplies Global", thietBi: 0, noiThat: 0, doDung: Math.round(t * 0.102) },
      { supplier: "Panasonic Industry Vietnam", thietBi: Math.round(t * 0.078), noiThat: 0, doDung: 0 }
    ];
  }

  function getManufacturerAnalysis(filters = {}) {
    const base = calculatePurchaseBaseMetrics(filters);
    const t = base.totalPurchaseCost;

    return [
      { name: "Canon Inc", cost: Math.round(t * 0.28), qty: 1200, avgUnitCost: 725.0 },
      { name: "Ergonomic Corp", cost: Math.round(t * 0.22), qty: 2300, avgUnitCost: 297.0 },
      { name: "SmartDesk Furniture Ltd", cost: Math.round(t * 0.18), qty: 1400, avgUnitCost: 400.0 },
      { name: "Panasonic Electric", cost: Math.round(t * 0.16), qty: 90, avgUnitCost: 5522.0 },
      { name: "Double A Paper Group", cost: Math.round(t * 0.11), qty: 15000, avgUnitCost: 2.2 },
      { name: "Parker Pen International", cost: Math.round(t * 0.05), qty: 800, avgUnitCost: 194.0 }
    ];
  }

  function getPurchaseTable(filters = {}) {
    let orders = [...(dwh.purchaseOrders || [])];
    const q = (filters.search || "").toLowerCase().trim();

    if (q) {
      orders = orders.filter(o => 
        o.id.toLowerCase().includes(q) ||
        o.supplierName.toLowerCase().includes(q) ||
        o.productName.toLowerCase().includes(q) ||
        o.category.toLowerCase().includes(q) ||
        o.manufacturer.toLowerCase().includes(q)
      );
    }

    if (filters.supplier && filters.supplier !== "All Suppliers") {
      orders = orders.filter(o => o.supplierName === filters.supplier);
    }

    if (filters.category && filters.category !== "All Categories") {
      orders = orders.filter(o => o.category === filters.category);
    }

    if (filters.manufacturer && filters.manufacturer !== "All Manufacturers") {
      orders = orders.filter(o => o.manufacturer === filters.manufacturer);
    }

    return orders;
  }

  function getPurchasePODetails(poId) {
    const orders = dwh.purchaseOrders || [];
    return orders.find(o => o.id === poId) || orders[0];
  }

  function getPurchaseInsights(filters = {}) {
    const base = calculatePurchaseBaseMetrics(filters);
    const conc = getSupplierConcentration(filters);

    return [
      `💡 <strong>Supplier Concentration:</strong> Top 3 nhà cung cấp lớn nhất chiếm <strong>${conc.top3SharePct}%</strong> tổng chi phí mua hàng ($${(base.totalPurchaseCost * 0.784 / 1000000).toFixed(2)}M).`,
      `📦 <strong>Category Spend Leader:</strong> Nhóm <em>Thiết bị Văn phòng</em> tiêu tốn ngân sách lớn nhất với <strong>48%</strong> tổng chi phí nhập.`,
      `📈 <strong>Unit Cost Dynamics:</strong> Chi phí mua trung bình trọng số (Weighted Avg Unit Cost) hiện ở mức <strong>$${base.weightedUnitCost}</strong>/đơn vị sản phẩm (+1.8% YoY).`,
      `🤝 <strong>Vendor Order Volume:</strong> Có <strong>${base.activeSuppliers}</strong> nhà cung cấp đang phát sinh đơn hàng với giá trị trung bình <strong>$${base.avgPOValue.toLocaleString()}</strong>/PO.`
    ];
  }

  function getPurchaseAlerts(filters = {}) {
    return [
      { type: "warning", title: "Cảnh Báo Tăng Giá Nhập", desc: "Máy chiếu Panasonic PT-LB386 tăng giá mua +18.3% so với kỳ trước ($11.95M)." },
      { type: "danger", title: "Mức Độ Tập Trung Nhà Cung Cấp Cao", desc: "Tập đoàn HP Việt Nam chiếm 32.4% tổng ngân sách mua hàng trong kỳ chọn." },
      { type: "info", title: "Đơn Hàng Khối Lượng Lớn", desc: "Đơn mua PO-2015-004 nhập 5,000 ream Giấy in A4 Double A giá trị $340M." }
    ];
  }

  function getPurchaseVsSalesPreview(filters = {}) {
    const baseP = calculatePurchaseBaseMetrics(filters);
    const baseS = calculateBaseMetrics(filters);

    return {
      purchaseCost: baseP.totalPurchaseCost,
      salesRevenue: baseS.revenue,
      grossProfit: baseS.revenue - baseP.totalPurchaseCost,
      costToRevRatio: ((baseP.totalPurchaseCost / (baseS.revenue || 1)) * 100).toFixed(1)
    };
  }

  function getPurchaseVsInventoryPreview(filters = {}) {
    const baseP = calculatePurchaseBaseMetrics(filters);
    const inv = getInventoryHealth();

    return {
      recentlyPurchasedQty: baseP.purchasedQty,
      totalStockVal: inv.healthyCount * 800 + inv.lowStockCount * 1500,
      criticalItemsCount: inv.criticalCount,
      lowStockCount: inv.lowStockCount
    };
  }

  // ==========================================================================
  // INVENTORY ANALYTICS EXTENDED APIs (ROUND 1)
  // ==========================================================================
  function getInventoryFullMetrics(filters = {}) {
    const products = dwh.products || [];
    let filtered = [...products];

    if (filters.category && filters.category !== "All Categories") {
      filtered = filtered.filter(p => p.category === filters.category);
    }
    if (filters.warehouse && filters.warehouse !== "All Warehouses") {
      filtered = filtered.filter(p => p.warehouse === filters.warehouse);
    }

    const totalOnHand = filtered.reduce((sum, p) => sum + p.stock, 0);
    const totalInventoryValue = filtered.reduce((sum, p) => sum + p.stock * p.price, 0);
    const lowStockItems = filtered.filter(p => p.stock > 0 && p.stock <= p.safetyStock).length;
    const outOfStockItems = filtered.filter(p => p.stock === 0).length;
    const overstockItems = filtered.filter(p => p.stock > p.safetyStock * 3).length;
    
    // Inventory Health Percentages
    const totalCount = filtered.length || 1;
    const healthyCount = filtered.filter(p => p.stock > p.safetyStock).length;
    const warningCount = lowStockItems;
    const criticalCount = outOfStockItems + filtered.filter(p => p.stock > 0 && p.stock <= 5).length;

    const healthyPct = Math.round((healthyCount / totalCount) * 100);
    const warningPct = Math.round((warningCount / totalCount) * 100);
    const criticalPct = Math.min(100 - healthyPct - warningPct, Math.round((criticalCount / totalCount) * 100));

    return {
      inventoryValue: totalInventoryValue,
      stockOnHand: totalOnHand,
      lowStockItems,
      outOfStockItems,
      overstockItems,
      stockCoverageMonths: 2.8,
      health: { healthyPct, warningPct, criticalPct },
      products: filtered,
      warehouses: dwh.warehouses || [],
      movements: dwh.stockMovements || []
    };
  }

  // ==========================================================================
  // SHIPPING ANALYTICS EXTENDED APIs (ROUND 2)
  // ==========================================================================
  function getShippingFullMetrics(filters = {}) {
    const shippers = dwh.shippers || [];
    const base = calculateBaseMetrics(filters);

    return {
      totalShipments: base.orders || 9250,
      shippingCost: base.shippingCost || 310000,
      avgDeliveryDays: 2.3,
      onTimeRate: base.onTimeRate || 95.8,
      lateShipments: Math.round((base.orders || 9250) * 0.042),
      avgShippingCost: Math.round((base.shippingCost || 310000) / (base.orders || 9250)),
      shippers: shippers,
      lateDeliveries: dwh.lateDeliveries || [],
      slaMonitor: [
        { status: "Healthy", count: Math.round((base.orders || 9250) * 0.958), pct: 95.8 },
        { status: "At Risk", count: Math.round((base.orders || 9250) * 0.03), pct: 3.0 },
        { status: "Breached", count: Math.round((base.orders || 9250) * 0.012), pct: 1.2 }
      ],
      regionPerformance: [
        { region: "North America", shipments: 2960, cost: 99200, avgDays: 2.1, onTimePct: 96.5 },
        { region: "South East Asia", shipments: 2405, cost: 80600, avgDays: 2.4, onTimePct: 95.2 },
        { region: "Western Europe", shipments: 1940, cost: 65100, avgDays: 2.2, onTimePct: 96.0 },
        { region: "East Asia", shipments: 1295, cost: 43400, avgDays: 2.5, onTimePct: 94.8 }
      ]
    };
  }

  // ==========================================================================
  // CUSTOMER ANALYTICS EXTENDED APIs (ROUND 3)
  // ==========================================================================
  function getCustomerFullMetrics(filters = {}) {
    const list = dwh.customers || [];
    const totalCustomers = 4520;
    const activeCustomers = 3840;
    const base = calculateBaseMetrics(filters);
    
    return {
      totalCustomers,
      activeCustomers,
      revenuePerCustomer: Math.round(base.revenue / activeCustomers),
      ordersPerCustomer: (base.orders / activeCustomers).toFixed(1),
      avgOrderValue: base.aov,
      repeatCustomerRate: "68.4%",
      segments: getCustomerSegments(),
      topCustomers: list,
      scatterData: list.map(c => ({
        x: c.totalSpent,
        y: c.margin,
        z: c.orders,
        name: c.name,
        segment: c.segment
      }))
    };
  }

  // ==========================================================================
  // PRODUCT ANALYTICS EXTENDED APIs (ROUND 4)
  // ==========================================================================
  function getProductFullMetrics(filters = {}) {
    const list = dwh.products || [];
    const base = calculateBaseMetrics(filters);

    return {
      totalProducts: 2045,
      activeProducts: 1980,
      revenue: base.revenue,
      profit: base.profit,
      avgMargin: base.margin + "%",
      topProduct: list[0] ? list[0].name : "Máy in Laser Canon LBP2900",
      products: list,
      matrixData: list.map(p => ({
        x: p.price * 10,
        y: p.margin,
        z: p.stock * 5 + 50,
        name: p.name,
        category: p.category
      }))
    };
  }

  // ==========================================================================
  // SUPPLIER ANALYTICS EXTENDED APIs (ROUND 5)
  // ==========================================================================
  function getSupplierFullMetrics(filters = {}) {
    const base = calculatePurchaseBaseMetrics(filters);
    const spend = getSupplierSpend(filters);
    const conc = getSupplierConcentration(filters);

    return {
      totalSuppliers: base.activeSuppliers,
      purchaseCost: base.totalPurchaseCost,
      purchasedQty: base.purchasedQty,
      avgUnitCost: base.weightedUnitCost,
      topSupplier: spend[0] ? spend[0].name : "Tập đoàn HP Việt Nam",
      spend: spend,
      concentration: conc,
      comparison: {
        supplierA: spend[0] || {},
        supplierB: spend[1] || {}
      }
    };
  }

  // ==========================================================================
  // CROSS ANALYSIS EXTENDED APIs (ROUND 6)
  // ==========================================================================
  function getCrossAnalysisData(metricX = "revenue", metricY = "inventoryVal", groupBy = "category") {
    return {
      metricXLabel: metricX === "revenue" ? "Doanh Thu ($)" : metricX === "purchaseCost" ? "Chi Phí Mua ($)" : "Chi Phí Vận Chuyển ($)",
      metricYLabel: metricY === "inventoryVal" ? "Giá Trị Tồn Kho ($)" : metricY === "profit" ? "Lợi Nhuận ($)" : "Mức Chiết Khấu ($)",
      relationshipSummary: `${metricX.toUpperCase()} và ${metricY.toUpperCase()} có xu hướng đồng biến tỷ lệ thuận trong tập dữ liệu DWH Khang Nghị.`,
      table: [
        { group: "Thiết bị Văn phòng", valX: 2450000, valY: 620000, diff: 1830000, growth: "+14.2%" },
        { group: "Nội thất Văn phòng", valX: 1820000, valY: 450000, diff: 1370000, growth: "+18.5%" },
        { group: "Đồ dùng Văn phòng", valX: 890000, valY: 210000, diff: 680000, growth: "+9.4%" }
      ]
    };
  }

  // ==========================================================================
  // AI FORECAST & WHAT-IF APIs (ROUND 7 & 8)
  // ==========================================================================
  function getForecastFullData(horizon = "6", metric = "revenue") {
    return {
      modelCard: {
        modelName: "ML.NET SSA (Singular Spectrum Analysis)",
        status: "PROTOTYPE DEMO",
        trainingPeriod: "2012-01 - 2015-12 (48 tháng DWH)",
        horizon: `${horizon} Tháng`,
        lastTrained: "18/09/2026 08:30:00 UTC",
        mape: "3.42% (High Accuracy)"
      },
      summary: {
        nextMonth: "$615,000",
        nextQuarter: "$1,865,000",
        projectedGrowth: "+8.4% YoY"
      },
      series: dwh.monthlyFactSeries
    };
  }

  // ==========================================================================
  // SYSTEM STATUS & ETL LINEAGE APIs (ROUND 11)
  // ==========================================================================
  function getSystemFullStatus() {
    return {
      health: { database: "Healthy", api: "Healthy", etl: "Healthy", lastRefresh: "18/09/2026 08:30 (Auto Cron)" },
      counts: { orders: 51291, customers: 4520, products: 2045, suppliers: 6, inventory: 40300, shipments: 25749 },
      etlHistory: [
        { date: "18/09/2026 08:30", process: "Full Fact_Order & Fact_Purchase Sync", rows: 51291, duration: "4.2s", status: "SUCCESS" },
        { date: "17/09/2026 08:30", process: "Incremental Dim_Product Update", rows: 124, duration: "0.8s", status: "SUCCESS" },
        { date: "16/09/2026 08:30", process: "Inventory Snapshots Aggregation", rows: 2045, duration: "1.2s", status: "SUCCESS" },
        { date: "15/09/2026 08:30", process: "Shipper SLA Log Processing", rows: 480, duration: "0.5s", status: "SUCCESS" }
      ]
    };
  }

  return {
    calculateBaseMetrics,
    getSalesByCategory,
    getSalesCategoryBreakdown: getSalesByCategory,
    getRegionBreakdown,
    getCustomerSegments,
    getInventoryHealth,
    getShippingPerformance,
    getForecastSeries,
    simulateWhatIfScenario,
    calculatePurchaseBaseMetrics,
    getPurchaseTrend,
    getSupplierSpend,
    getSupplierConcentration,
    getPurchaseCostByCategory,
    getCostVariance,
    getSupplierCategoryMatrix,
    getManufacturerAnalysis,
    getPurchaseTable,
    getPurchasePODetails,
    getPurchaseInsights,
    getPurchaseAlerts,
    getPurchaseVsSalesPreview,
    getPurchaseVsInventoryPreview,
    getInventoryFullMetrics,
    getShippingFullMetrics,
    getCustomerFullMetrics,
    getProductFullMetrics,
    getSupplierFullMetrics,
    getCrossAnalysisData,
    getForecastFullData,
    getSystemFullStatus
  };
})();


