/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - SMART SEARCH SERVICE
   Global Search, Auto-Suggestions, Keyboard Shortcuts (Ctrl+K) & Matcher
   ========================================================================== */

window.KhangNghiSearch = (function () {
  let recentSearches = ["Tập đoàn HP", "Canon LBP2900", "Thiết bị Văn phòng", "PO-2015-001"];

  function getSuggestions(query) {
    const q = (query || "").trim().toLowerCase();
    const dwh = window.KhangNghiMockData || {};
    const list = [];

    // 1. Suppliers Match
    const matchedSuppliers = (dwh.suppliers || []).filter(s =>
      s.name.toLowerCase().includes(q) || s.id.toLowerCase().includes(q) || s.country.toLowerCase().includes(q)
    );
    matchedSuppliers.forEach(s => {
      list.push({ type: "supplier", title: s.name, subtitle: `ID: ${s.id} | Quốc gia: ${s.country}`, value: s.name });
    });

    // 2. Products Match
    const matchedProducts = (dwh.products || []).filter(p =>
      p.name.toLowerCase().includes(q) || p.id.toLowerCase().includes(q) || p.category.toLowerCase().includes(q)
    );
    matchedProducts.forEach(p => {
      list.push({ type: "product", title: p.name, subtitle: `ID: ${p.id} | Category: ${p.category}`, value: p.name });
    });

    // 3. Purchase Orders Match
    const matchedPOs = (dwh.purchaseOrders || []).filter(po =>
      po.id.toLowerCase().includes(q) || po.supplierName.toLowerCase().includes(q) || po.productName.toLowerCase().includes(q)
    );
    matchedPOs.forEach(po => {
      list.push({ type: "po", title: po.id, subtitle: `${po.supplierName} - ${po.productName}`, value: po.id });
    });

    // 4. Categories & Subcategories Match
    const categoriesList = ["Thiết bị Văn phòng", "Nội thất Văn phòng", "Đồ dùng Văn phòng", "Máy in", "Ghế công thái học", "Giấy in A4"];
    categoriesList.filter(c => c.toLowerCase().includes(q)).forEach(c => {
      list.push({ type: "category", title: c, subtitle: "Danh mục / Nhóm sản phẩm", value: c });
    });

    // 5. Manufacturers Match
    const matchedMfg = (dwh.manufacturers || []).filter(m => m.name.toLowerCase().includes(q));
    matchedMfg.forEach(m => {
      list.push({ type: "manufacturer", title: m.name, subtitle: `Hãng sản xuất (${m.country})`, value: m.name });
    });

    return list.slice(0, 8);
  }

  function search(query) {
    const q = (query || "").trim().toLowerCase();
    const dwh = window.KhangNghiMockData || {};
    return {
      suppliers: (dwh.suppliers || []).filter(s => s.name.toLowerCase().includes(q)),
      products: (dwh.products || []).filter(p => p.name.toLowerCase().includes(q)),
      purchaseOrders: (dwh.purchaseOrders || []).filter(po => po.id.toLowerCase().includes(q)),
      categories: ["Thiết bị Văn phòng", "Nội thất Văn phòng", "Đồ dùng Văn phòng"].filter(c => c.toLowerCase().includes(q)),
      recent: recentSearches.filter(s => s.toLowerCase().includes(q))
    };
  }

  function addRecentSearch(term) {
    if (!term || typeof term !== "string") return;
    const clean = term.trim();
    if (!clean) return;
    recentSearches = [clean, ...recentSearches.filter(s => s !== clean)].slice(0, 5);
  }

  function getRecentSearches() {
    return [...recentSearches];
  }

  return {
    getSuggestions,
    search,
    addRecentSearch,
    getRecentSearches
  };
})();
