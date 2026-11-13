/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - ENTERPRISE MOCK DWH DATASET
   Modeled after Kimball Star Schema (DWH_KhangNghi)
   Years: 2012 - 2015 | Global Regions: 23 Regions | Fact Constellation Schema
   ========================================================================== */

window.KhangNghiMockData = (function () {
  const years = [2012, 2013, 2014, 2015];
  
  const regions = [
    "All Regions", "North America", "Central America", "South America",
    "Western Europe", "Northern Europe", "Southern Europe", "Eastern Europe",
    "South East Asia", "East Asia", "Central Asia", "South Asia",
    "Oceania", "Middle East", "North Africa", "Sub-Saharan Africa"
  ];

  const categories = [
    {
      id: "CAT-01",
      name: "Thiết bị Văn phòng",
      subcategories: ["Máy in", "Máy chiếu", "Máy hủy tài liệu", "Thiết bị mạng"]
    },
    {
      id: "CAT-02",
      name: "Nội thất Văn phòng",
      subcategories: ["Bàn làm việc", "Ghế công thái học", "Tủ tài liệu", "Kệ sách"]
    },
    {
      id: "CAT-03",
      name: "Đồ dùng Văn phòng",
      subcategories: ["Bìa hồ sơ", "Kẹp giấy", "Giấy in A4", "Bút ký cao cấp"]
    }
  ];

  const warehouses = [
    { id: "WH-NORTH", name: "Kho Miền Bắc (Hà Nội)", capacity: 25000, stockQty: 12400, stockValue: 5400000000, lowStockCount: 2 },
    { id: "WH-SOUTH", name: "Kho Miền Nam (TP.HCM)", capacity: 35000, stockQty: 18900, stockValue: 8200000000, lowStockCount: 1 },
    { id: "WH-CENTRAL", name: "Kho Miền Trung (Đà Nẵng)", capacity: 15000, stockQty: 6200, stockValue: 2700000000, lowStockCount: 1 }
  ];

  const products = [
    { id: "PRD-101", name: "Máy in Laser Canon LBP2900", category: "Thiết bị Văn phòng", subcategory: "Máy in", manufacturer: "Canon Inc", price: 3800000, margin: 24.5, stock: 4, safetyStock: 25, warehouse: "Kho Miền Bắc (Hà Nội)", status: "CRITICAL" },
    { id: "PRD-102", name: "Ghế công thái học Ergonomic X5", category: "Nội thất Văn phòng", subcategory: "Ghế công thái học", manufacturer: "Ergonomic Corp", price: 4200000, margin: 29.0, stock: 12, safetyStock: 20, warehouse: "Kho Miền Nam (TP.HCM)", status: "WARNING" },
    { id: "PRD-103", name: "Bàn làm việc thông minh SmartDesk", category: "Nội thất Văn phòng", subcategory: "Bàn làm việc", manufacturer: "SmartDesk Furniture Ltd", price: 6500000, margin: 32.4, stock: 18, safetyStock: 15, warehouse: "Kho Miền Nam (TP.HCM)", status: "HEALTHY" },
    { id: "PRD-104", name: "Giấy in A4 Double A 80gsm", category: "Đồ dùng Văn phòng", subcategory: "Giấy in A4", manufacturer: "Double A Paper Group", price: 78000, margin: 12.0, stock: 850, safetyStock: 200, warehouse: "Kho Miền Trung (Đà Nẵng)", status: "HEALTHY" },
    { id: "PRD-105", name: "Máy chiếu Panasonic PT-LB386", category: "Thiết bị Văn phòng", subcategory: "Máy chiếu", manufacturer: "Panasonic Electric", price: 14500000, margin: 18.2, stock: 2, safetyStock: 10, warehouse: "Kho Miền Bắc (Hà Nội)", status: "CRITICAL" },
    { id: "PRD-106", name: "Bút ký cao cấp Parker Sonnet", category: "Đồ dùng Văn phòng", subcategory: "Bút ký cao cấp", manufacturer: "Parker Pen International", price: 1850000, margin: 38.0, stock: 45, safetyStock: 20, warehouse: "Kho Miền Bắc (Hà Nội)", status: "HEALTHY" },
    { id: "PRD-107", name: "Máy hủy tài liệu Silicon PS-800C", category: "Thiết bị Văn phòng", subcategory: "Máy hủy tài liệu", manufacturer: "Silicon Tech", price: 5200000, margin: 26.5, stock: 0, safetyStock: 8, warehouse: "Kho Miền Nam (TP.HCM)", status: "CRITICAL" },
    { id: "PRD-108", name: "Tủ tài liệu sắt Hòa Phát TU09K3", category: "Nội thất Văn phòng", subcategory: "Tủ tài liệu", manufacturer: "SmartDesk Furniture Ltd", price: 3100000, margin: 22.0, stock: 35, safetyStock: 10, warehouse: "Kho Miền Bắc (Hà Nội)", status: "HEALTHY" }
  ];

  const stockMovements = [
    { month: "Jan 15", opening: 32000, inbound: 4500, outbound: 3800, closing: 32700 },
    { month: "Feb 15", opening: 32700, inbound: 3800, outbound: 3600, closing: 32900 },
    { month: "Mar 15", opening: 32900, inbound: 5200, outbound: 4200, closing: 33900 },
    { month: "Apr 15", opening: 33900, inbound: 4800, outbound: 4100, closing: 34600 },
    { month: "May 15", opening: 34600, inbound: 5100, outbound: 4500, closing: 35200 },
    { month: "Jun 15", opening: 35200, inbound: 5500, outbound: 4800, closing: 35900 },
    { month: "Jul 15", opening: 35900, inbound: 5300, outbound: 4700, closing: 36500 },
    { month: "Aug 15", opening: 36500, inbound: 5800, outbound: 5100, closing: 37200 },
    { month: "Sep 15", opening: 37200, inbound: 6100, outbound: 5300, closing: 38000 },
    { month: "Oct 15", opening: 38000, inbound: 6400, outbound: 5600, closing: 38800 },
    { month: "Nov 15", opening: 38800, inbound: 6700, outbound: 5900, closing: 39600 },
    { month: "Dec 15", opening: 39600, inbound: 7200, outbound: 6500, closing: 40300 }
  ];

  const lateDeliveries = [
    { orderId: "SO-2015-881", region: "South East Asia", shipMode: "Second Class", shipDate: "2015-11-04", expectedDelivery: "2015-11-08", actualDelivery: "2015-11-12", delayDays: 4, shipper: "Vietnam Post (Second Class)", status: "Breached" },
    { orderId: "SO-2015-912", region: "North America", shipMode: "Standard", shipDate: "2015-11-10", expectedDelivery: "2015-11-14", actualDelivery: "2015-11-17", delayDays: 3, shipper: "Viettel Post (Standard)", status: "At Risk" },
    { orderId: "SO-2015-945", region: "Western Europe", shipMode: "First Class", shipDate: "2015-11-18", expectedDelivery: "2015-11-20", actualDelivery: "2015-11-22", delayDays: 2, shipper: "Giao Hàng Nhanh (First Class)", status: "At Risk" },
    { orderId: "SO-2015-982", region: "East Asia", shipMode: "Second Class", shipDate: "2015-12-01", expectedDelivery: "2015-12-05", actualDelivery: "2015-12-10", delayDays: 5, shipper: "Vietnam Post (Second Class)", status: "Breached" }
  ];

  const customers = [
    { id: "CUS-101", name: "Ngân hàng TMCP VPBank", segment: "Corporate", region: "South East Asia", totalSpent: 685000, profit: 142000, orders: 42, margin: 20.7, topCategory: "Thiết bị Văn phòng" },
    { id: "CUS-102", name: "Tập đoàn Vingroup", segment: "Corporate", region: "South East Asia", totalSpent: 1250000, profit: 285000, orders: 88, margin: 22.8, topCategory: "Nội thất Văn phòng" },
    { id: "CUS-103", name: "Công ty FPT Software", segment: "Corporate", region: "South East Asia", totalSpent: 910000, profit: 198000, orders: 64, margin: 21.7, topCategory: "Thiết bị Văn phòng" },
    { id: "CUS-104", name: "Văn phòng Luật Minh Anh", segment: "Home Office", region: "North America", totalSpent: 145000, profit: 34000, orders: 12, margin: 23.4, topCategory: "Đồ dùng Văn phòng" },
    { id: "CUS-105", name: "Đại học Ngân hàng TP.HCM", segment: "Consumer", region: "South East Asia", totalSpent: 380000, profit: 79000, orders: 28, margin: 20.8, topCategory: "Thiết bị Văn phòng" },
    { id: "CUS-106", name: "Tập đoàn Viettel Corp", segment: "Corporate", region: "South East Asia", totalSpent: 1120000, profit: 245000, orders: 76, margin: 21.8, topCategory: "Thiết bị Văn phòng" },
    { id: "CUS-107", name: "Công ty THTrueMilk", segment: "Corporate", region: "South East Asia", totalSpent: 520000, profit: 115000, orders: 36, margin: 22.1, topCategory: "Đồ dùng Văn phòng" },
    { id: "CUS-108", name: "Văn phòng Kiến trúc A&B", segment: "Home Office", region: "Western Europe", totalSpent: 210000, profit: 48000, orders: 18, margin: 22.8, topCategory: "Nội thất Văn phòng" }
  ];

  const suppliers = [
    { id: "SUP-001", name: "Tập đoàn HP Việt Nam", country: "Mỹ", leadTimeDays: 4, rating: 4.8, totalPurchased: 1450000, sharePct: 32.4 },
    { id: "SUP-002", name: "Canon Asia Marketing Corp", country: "Nhật Bản", leadTimeDays: 7, rating: 4.9, totalPurchased: 1120000, sharePct: 26.0 },
    { id: "SUP-003", name: "Nội thất Hòa Phát", country: "Việt Nam", leadTimeDays: 2, rating: 4.7, totalPurchased: 890000, sharePct: 20.0 },
    { id: "SUP-004", name: "Deli Office Supplies Global", country: "Trung Quốc", leadTimeDays: 10, rating: 4.5, totalPurchased: 450000, sharePct: 10.2 },
    { id: "SUP-005", name: "Panasonic Industry Vietnam", country: "Nhật Bản", leadTimeDays: 5, rating: 4.8, totalPurchased: 380000, sharePct: 7.8 },
    { id: "SUP-006", name: "Parker Pen International", country: "Mỹ", leadTimeDays: 6, rating: 4.9, totalPurchased: 160000, sharePct: 3.6 }
  ];

  const manufacturers = [
    { id: "MAN-101", name: "Canon Inc", country: "Nhật Bản" },
    { id: "MAN-102", name: "Ergonomic Corp", country: "Mỹ" },
    { id: "MAN-103", name: "SmartDesk Furniture Ltd", country: "Việt Nam" },
    { id: "MAN-104", name: "Double A Paper Group", country: "Thái Lan" },
    { id: "MAN-105", name: "Panasonic Electric", country: "Nhật Bản" },
    { id: "MAN-106", name: "Parker Pen International", country: "Mỹ" }
  ];

  const shippers = [
    { id: "SHP-001", name: "Giao Hàng Nhanh (First Class)", avgDays: 1.5, lateRate: 0.03, status: "Healthy" },
    { id: "SHP-002", name: "Viettel Post (Standard)", avgDays: 3.2, lateRate: 0.06, status: "Healthy" },
    { id: "SHP-003", name: "DHL Express (Same Day)", avgDays: 0.8, lateRate: 0.01, status: "Healthy" },
    { id: "SHP-004", name: "Vietnam Post (Second Class)", avgDays: 4.5, lateRate: 0.09, status: "At Risk" }
  ];

  const purchaseOrders = [
    { id: "PO-2015-001", date: "2015-01-10", supplierId: "SUP-002", supplierName: "Canon Asia Marketing Corp", productId: "PRD-101", productName: "Máy in Laser Canon LBP2900", category: "Thiết bị Văn phòng", subcategory: "Máy in", manufacturer: "Canon Inc", qty: 250, unitCost: 2850000, totalCost: 712500000 },
    { id: "PO-2015-002", date: "2015-01-18", supplierId: "SUP-003", supplierName: "Nội thất Hòa Phát", productId: "PRD-102", productName: "Ghế công thái học Ergonomic X5", category: "Nội thất Văn phòng", subcategory: "Ghế công thái học", manufacturer: "Ergonomic Corp", qty: 180, unitCost: 2980000, totalCost: 536400000 },
    { id: "PO-2015-003", date: "2015-02-05", supplierId: "SUP-001", supplierName: "Tập đoàn HP Việt Nam", productId: "PRD-103", productName: "Bàn làm việc thông minh SmartDesk", category: "Nội thất Văn phòng", subcategory: "Bàn làm việc", manufacturer: "SmartDesk Furniture Ltd", qty: 120, unitCost: 4400000, totalCost: 528000000 },
    { id: "PO-2015-004", date: "2015-02-14", supplierId: "SUP-004", supplierName: "Deli Office Supplies Global", productId: "PRD-104", productName: "Giấy in A4 Double A 80gsm", category: "Đồ dùng Văn phòng", subcategory: "Giấy in A4", manufacturer: "Double A Paper Group", qty: 5000, unitCost: 68000, totalCost: 340000000 },
    { id: "PO-2015-005", date: "2015-03-02", supplierId: "SUP-005", supplierName: "Panasonic Industry Vietnam", productId: "PRD-105", productName: "Máy chiếu Panasonic PT-LB386", category: "Thiết bị Văn phòng", subcategory: "Máy chiếu", manufacturer: "Panasonic Electric", qty: 45, unitCost: 11800000, totalCost: 531000000 },
    { id: "PO-2015-006", date: "2015-03-20", supplierId: "SUP-006", supplierName: "Parker Pen International", productId: "PRD-106", productName: "Bút ký cao cấp Parker Sonnet", category: "Đồ dùng Văn phòng", subcategory: "Bút ký cao cấp", manufacturer: "Parker Pen International", qty: 300, unitCost: 1150000, totalCost: 345000000 },
    { id: "PO-2015-007", date: "2015-04-12", supplierId: "SUP-002", supplierName: "Canon Asia Marketing Corp", productId: "PRD-101", productName: "Máy in Laser Canon LBP2900", category: "Thiết bị Văn phòng", subcategory: "Máy in", manufacturer: "Canon Inc", qty: 310, unitCost: 2900000, totalCost: 899000000 },
    { id: "PO-2015-008", date: "2015-05-08", supplierId: "SUP-001", supplierName: "Tập đoàn HP Việt Nam", productId: "PRD-105", productName: "Máy chiếu Panasonic PT-LB386", category: "Thiết bị Văn phòng", subcategory: "Máy chiếu", manufacturer: "Panasonic Electric", qty: 50, unitCost: 11950000, totalCost: 597500000 }
  ];

  // Base Yearly DWH Aggregates (Strict Mathematical Consistency)
  const yearlyMetrics = {
    2012: { revenue: 2450000, profit: 285000, orders: 4820, qty: 18400, avgDiscount: 0.12, shipCost: 185000, inventoryVal: 820000, onTimeRate: 92.4 },
    2013: { revenue: 3120000, profit: 375000, orders: 6150, qty: 23600, avgDiscount: 0.11, shipCost: 220000, inventoryVal: 950000, onTimeRate: 93.8 },
    2014: { revenue: 3890000, profit: 462000, orders: 7480, qty: 29800, avgDiscount: 0.10, shipCost: 265000, inventoryVal: 1120000, onTimeRate: 94.5 },
    2015: { revenue: 4780000, profit: 588000, orders: 9250, qty: 36500, avgDiscount: 0.09, shipCost: 310000, inventoryVal: 1280000, onTimeRate: 95.8 }
  };

  const yearlyPurchaseMetrics = {
    2012: { purchaseCost: 1650000, poCount: 310, qty: 14200, unitCost: 116.2, activeSuppliers: 4, avgPOValue: 5322 },
    2013: { purchaseCost: 2050000, poCount: 385, qty: 17500, unitCost: 117.1, activeSuppliers: 5, avgPOValue: 5324 },
    2014: { purchaseCost: 2580000, poCount: 440, qty: 21800, unitCost: 118.3, activeSuppliers: 5, avgPOValue: 5863 },
    2015: { purchaseCost: 3110000, poCount: 520, qty: 25800, unitCost: 120.5, activeSuppliers: 6, avgPOValue: 5980 }
  };

  // Monthly Purchase DWH Time-Series 2015
  const monthlyPurchaseSeries = [
    { month: "Jan 15", purchaseCost: 210000, qty: 1780, unitCost: 118.0, poCount: 36 },
    { month: "Feb 15", purchaseCost: 195000, qty: 1660, unitCost: 117.5, poCount: 34 },
    { month: "Mar 15", purchaseCost: 245000, qty: 2050, unitCost: 119.5, poCount: 42 },
    { month: "Apr 15", purchaseCost: 235000, qty: 1970, unitCost: 119.3, poCount: 40 },
    { month: "May 15", purchaseCost: 260000, qty: 2150, unitCost: 120.9, poCount: 44 },
    { month: "Jun 15", purchaseCost: 280000, qty: 2310, unitCost: 121.2, poCount: 46 },
    { month: "Jul 15", purchaseCost: 270000, qty: 2240, unitCost: 120.5, poCount: 45 },
    { month: "Aug 15", purchaseCost: 295000, qty: 2430, unitCost: 121.4, poCount: 48 },
    { month: "Sep 15", purchaseCost: 305000, qty: 2500, unitCost: 122.0, poCount: 50 },
    { month: "Oct 15", purchaseCost: 325000, qty: 2650, unitCost: 122.6, poCount: 52 },
    { month: "Nov 15", purchaseCost: 340000, qty: 2760, unitCost: 123.2, poCount: 54 },
    { month: "Dec 15", purchaseCost: 350000, qty: 2800, unitCost: 125.0, poCount: 55 }
  ];

  // Monthly DWH Fact Order Time-Series (2015 Actuals + 2016 ML.NET SSA Forecast)
  const monthlyFactSeries = [
    { month: "Jan 15", revenue: 340000, profit: 42000, forecast: null, upper: null, lower: null },
    { month: "Feb 15", revenue: 310000, profit: 38000, forecast: null, upper: null, lower: null },
    { month: "Mar 15", revenue: 390000, profit: 48000, forecast: null, upper: null, lower: null },
    { month: "Apr 15", revenue: 370000, profit: 45000, forecast: null, upper: null, lower: null },
    { month: "May 15", revenue: 410000, profit: 51000, forecast: null, upper: null, lower: null },
    { month: "Jun 15", revenue: 440000, profit: 55000, forecast: null, upper: null, lower: null },
    { month: "Jul 15", revenue: 420000, profit: 52000, forecast: null, upper: null, lower: null },
    { month: "Aug 15", revenue: 460000, profit: 58000, forecast: null, upper: null, lower: null },
    { month: "Sep 15", revenue: 480000, profit: 60000, forecast: null, upper: null, lower: null },
    { month: "Oct 15", revenue: 510000, profit: 64000, forecast: null, upper: null, lower: null },
    { month: "Nov 15", revenue: 530000, profit: 66000, forecast: null, upper: null, lower: null },
    { month: "Dec 15", revenue: 590000, profit: 74000, forecast: null, upper: null, lower: null },
    // SSA Forecast 2016
    { month: "Jan 16", revenue: null, profit: null, forecast: 615000, upper: 658000, lower: 572000 },
    { month: "Feb 16", revenue: null, profit: null, forecast: 590000, upper: 635000, lower: 545000 },
    { month: "Mar 16", revenue: null, profit: null, forecast: 660000, upper: 710000, lower: 610000 },
    { month: "Apr 16", revenue: null, profit: null, forecast: 645000, upper: 695000, lower: 595000 },
    { month: "May 16", revenue: null, profit: null, forecast: 690000, upper: 745000, lower: 635000 },
    { month: "Jun 16", revenue: null, profit: null, forecast: 730000, upper: 790000, lower: 670000 }
  ];

  return {
    years,
    regions,
    categories,
    products,
    suppliers,
    manufacturers,
    shippers,
    customers,
    warehouses,
    stockMovements,
    lateDeliveries,
    purchaseOrders,
    yearlyMetrics,
    yearlyPurchaseMetrics,
    monthlyPurchaseSeries,
    monthlyFactSeries
  };
})();


