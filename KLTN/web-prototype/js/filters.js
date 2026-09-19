/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - MULTI-DIMENSIONAL FILTER ENGINE
   Smart Filter Bar, Metric Switcher, Active Chips, Presets & Cross-Filter Engine
   ========================================================================== */

window.KhangNghiFilters = (function () {
  const DEFAULT_FILTERS = {
    search: "",
    year: "2015",
    quarter: "ALL",
    region: "All Regions",
    category: "All Categories",
    supplier: "All Suppliers",
    manufacturer: "All Manufacturers",
    segment: "All Segments",
    metric: "revenue",
    granularity: "monthly",
    rankingMode: "top10",
    compareMode: false,
    preset: "All Sales"
  };

  let activeFilters = { ...DEFAULT_FILTERS };
  const listeners = [];

  const PRESETS_STORAGE_KEY = "kn_bi_filter_presets";
  let defaultPresets = [
    { name: "All Sales", filters: { ...DEFAULT_FILTERS } },
    { name: "Truyền Động High Profit", filters: { ...DEFAULT_FILTERS, category: "Truyền động", metric: "profit" } },
    { name: "Tự Động Hóa 2015", filters: { ...DEFAULT_FILTERS, year: "2015", region: "Southeastern Asia", category: "Tự động hóa" } },
    { name: "Thiết Bị Đóng Cắt Alert", filters: { ...DEFAULT_FILTERS, category: "Thiết bị đóng cắt" } },
    { name: "APAC Sales 2015", filters: { ...DEFAULT_FILTERS, year: "2015", region: "Southeastern Asia" } }
  ];

  function init() {
    loadPresetsFromStorage();
    renderActiveChips();
  }

  function loadPresetsFromStorage() {
    try {
      const stored = localStorage.getItem(PRESETS_STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed) && parsed.length > 0) {
          defaultPresets = parsed;
        }
      }
    } catch (e) {
      console.error("Failed to parse filter presets:", e);
    }
  }

  function setFilter(key, val) {
    activeFilters[key] = val;
    renderActiveChips();
    notifyChange();
  }

  function setMultipleFilters(obj) {
    activeFilters = { ...activeFilters, ...obj };
    renderActiveChips();
    notifyChange();
  }

  function resetAllFilters() {
    activeFilters = { ...DEFAULT_FILTERS };
    renderActiveChips();
    notifyChange();
  }

  function countActiveFilters() {
    let count = 0;
    if (activeFilters.search) count++;
    if (activeFilters.year !== "ALL") count++;
    if (activeFilters.quarter !== "ALL") count++;
    if (activeFilters.region !== "All Regions") count++;
    if (activeFilters.category !== "All Categories") count++;
    if (activeFilters.supplier && activeFilters.supplier !== "All Suppliers") count++;
    if (activeFilters.manufacturer && activeFilters.manufacturer !== "All Manufacturers") count++;
    if (activeFilters.segment !== "All Segments") count++;
    if (activeFilters.compareMode) count++;
    return count;
  }

  function renderActiveChips() {
    renderChipsContainer("sales-active-chips");
    renderChipsContainer("purchase-active-chips");
    renderChipsContainer("active-filter-chips");
  }

  function renderChipsContainer(targetId) {
    const el = document.getElementById(targetId);
    if (!el) return;
    el.innerHTML = buildChipsHTML();

    const count = countActiveFilters();
    const badgeEls = document.querySelectorAll(".filter-count-badge");
    badgeEls.forEach(badge => {
      if (count > 0) {
        badge.textContent = `(${count})`;
        badge.style.display = "inline";
      } else {
        badge.style.display = "none";
      }
    });
  }

  function buildChipsHTML() {
    let html = "";
    if (activeFilters.search) {
      html += `<div class="filter-chip">🔍 Tìm: "${activeFilters.search}" <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('search', '')">&times;</span></div>`;
    }
    if (activeFilters.year !== "ALL") {
      html += `<div class="filter-chip">Năm: ${activeFilters.year} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('year', 'ALL')">&times;</span></div>`;
    }
    if (activeFilters.region !== "All Regions") {
      html += `<div class="filter-chip">Vùng: ${activeFilters.region} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('region', 'All Regions')">&times;</span></div>`;
    }
    if (activeFilters.category !== "All Categories") {
      html += `<div class="filter-chip">Danh mục: ${activeFilters.category} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('category', 'All Categories')">&times;</span></div>`;
    }
    if (activeFilters.supplier && activeFilters.supplier !== "All Suppliers") {
      html += `<div class="filter-chip">NCC: ${activeFilters.supplier} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('supplier', 'All Suppliers')">&times;</span></div>`;
    }
    if (activeFilters.manufacturer && activeFilters.manufacturer !== "All Manufacturers") {
      html += `<div class="filter-chip">Hãng: ${activeFilters.manufacturer} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('manufacturer', 'All Manufacturers')">&times;</span></div>`;
    }
    if (activeFilters.segment !== "All Segments") {
      html += `<div class="filter-chip">Phân khúc: ${activeFilters.segment} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('segment', 'All Segments')">&times;</span></div>`;
    }
    if (activeFilters.compareMode) {
      html += `<div class="filter-chip" style="background:var(--color-primary-light); color:var(--color-primary);">So sánh vs 2014 <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('compareMode', false)">&times;</span></div>`;
    }

    if (html) {
      html = `<div style="display:flex; align-items:center; gap:8px; flex-wrap:wrap;"><span style="font-size:11.5px; font-weight:700; color:var(--text-muted);">ACTIVE FILTERS:</span> ${html} <button class="reset-filters-btn" onclick="KhangNghiFilters.resetAllFilters()"><i class="fa-solid fa-arrows-rotate"></i> Clear All</button></div>`;
    }

    return html;
  }

  function saveCurrentView(presetName) {
    if (!presetName) return;
    const cleanName = presetName.trim();
    if (!cleanName) return;

    const existingIndex = defaultPresets.findIndex(p => p.name.toLowerCase() === cleanName.toLowerCase());
    const newPreset = { name: cleanName, filters: { ...activeFilters } };

    if (existingIndex >= 0) {
      defaultPresets[existingIndex] = newPreset;
    } else {
      defaultPresets.push(newPreset);
    }

    localStorage.setItem(PRESETS_STORAGE_KEY, JSON.stringify(defaultPresets));
    activeFilters.preset = cleanName;
    notifyChange();
  }

  function loadPreset(presetName) {
    const matched = defaultPresets.find(p => p.name === presetName);
    if (matched) {
      activeFilters = { ...matched.filters, preset: presetName };
      renderActiveChips();
      notifyChange();
    }
  }

  function getPresets() {
    return defaultPresets;
  }

  function onChange(callback) {
    listeners.push(callback);
  }

  function notifyChange() {
    renderActiveChips();
    listeners.forEach(fn => fn(activeFilters));
  }

  function getState() {
    return { ...activeFilters };
  }

  return {
    init,
    setFilter,
    setMultipleFilters,
    resetAllFilters,
    countActiveFilters,
    renderChipsContainer,
    saveCurrentView,
    loadPreset,
    getPresets,
    onChange,
    getState
  };
})();
