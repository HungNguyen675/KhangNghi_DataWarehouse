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
    if (activeFilters.search && activeFilters.search.trim()) count++;
    if (activeFilters.year && activeFilters.year !== DEFAULT_FILTERS.year) count++;
    if (activeFilters.quarter && activeFilters.quarter !== DEFAULT_FILTERS.quarter) count++;
    if (activeFilters.region && activeFilters.region !== DEFAULT_FILTERS.region) count++;
    if (activeFilters.category && activeFilters.category !== DEFAULT_FILTERS.category) count++;
    if (activeFilters.supplier && activeFilters.supplier !== DEFAULT_FILTERS.supplier) count++;
    if (activeFilters.manufacturer && activeFilters.manufacturer !== DEFAULT_FILTERS.manufacturer) count++;
    if (activeFilters.segment && activeFilters.segment !== DEFAULT_FILTERS.segment) count++;
    if (activeFilters.compareMode) count++;
    return count;
  }

  function countAdvancedFilters() {
    let count = 0;
    if (activeFilters.segment && activeFilters.segment !== DEFAULT_FILTERS.segment) count++;
    if (activeFilters.quarter && activeFilters.quarter !== DEFAULT_FILTERS.quarter) count++;
    if (activeFilters.supplier && activeFilters.supplier !== DEFAULT_FILTERS.supplier) count++;
    if (activeFilters.manufacturer && activeFilters.manufacturer !== DEFAULT_FILTERS.manufacturer) count++;
    return count;
  }

  function renderActiveChips() {
    renderChipsContainer("sales-active-chips");
    renderChipsContainer("purchase-active-chips");
    renderChipsContainer("active-filter-chips");
  }

  function renderChipsContainer(targetId) {
    const el = document.getElementById(targetId);
    if (el) {
      el.innerHTML = buildChipsHTML();
    }

    const totalCount = countActiveFilters();
    const advCount = countAdvancedFilters();

    const badgeEls = document.querySelectorAll(".filter-count-badge");
    badgeEls.forEach(badge => {
      if (totalCount > 0) {
        badge.textContent = `(${totalCount})`;
        badge.style.display = "inline";
      } else {
        badge.style.display = "none";
      }
    });

    const advBadgeEls = document.querySelectorAll(".filter-adv-count-badge");
    advBadgeEls.forEach(badge => {
      if (advCount > 0) {
        badge.textContent = `(${advCount})`;
        badge.style.display = "inline-flex";
      } else {
        badge.style.display = "none";
      }
    });
  }

  function buildChipsHTML() {
    let html = "";
    if (activeFilters.search && activeFilters.search.trim()) {
      html += `<div class="filter-chip">🔍 Tìm: "${activeFilters.search}" <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('search', '')" title="Xóa tìm kiếm">&times;</span></div>`;
    }
    if (activeFilters.year && activeFilters.year !== DEFAULT_FILTERS.year) {
      const yrText = activeFilters.year === "ALL" ? "Tất cả năm" : activeFilters.year;
      html += `<div class="filter-chip">Năm: ${yrText} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('year', '${DEFAULT_FILTERS.year}')" title="Đặt lại năm mặc định">&times;</span></div>`;
    }
    if (activeFilters.region && activeFilters.region !== DEFAULT_FILTERS.region) {
      html += `<div class="filter-chip">Khu vực: ${activeFilters.region} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('region', '${DEFAULT_FILTERS.region}')" title="Xóa khu vực">&times;</span></div>`;
    }
    if (activeFilters.category && activeFilters.category !== DEFAULT_FILTERS.category) {
      html += `<div class="filter-chip">Danh mục: ${activeFilters.category} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('category', '${DEFAULT_FILTERS.category}')" title="Xóa danh mục">&times;</span></div>`;
    }
    if (activeFilters.segment && activeFilters.segment !== DEFAULT_FILTERS.segment) {
      html += `<div class="filter-chip">Phân khúc: ${activeFilters.segment} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('segment', '${DEFAULT_FILTERS.segment}')" title="Xóa phân khúc">&times;</span></div>`;
    }
    if (activeFilters.quarter && activeFilters.quarter !== DEFAULT_FILTERS.quarter) {
      html += `<div class="filter-chip">Quý: ${activeFilters.quarter} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('quarter', '${DEFAULT_FILTERS.quarter}')" title="Xóa quý">&times;</span></div>`;
    }
    if (activeFilters.supplier && activeFilters.supplier !== DEFAULT_FILTERS.supplier) {
      html += `<div class="filter-chip">NCC: ${activeFilters.supplier} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('supplier', '${DEFAULT_FILTERS.supplier}')" title="Xóa NCC">&times;</span></div>`;
    }
    if (activeFilters.manufacturer && activeFilters.manufacturer !== DEFAULT_FILTERS.manufacturer) {
      html += `<div class="filter-chip">Hãng: ${activeFilters.manufacturer} <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('manufacturer', '${DEFAULT_FILTERS.manufacturer}')" title="Xóa hãng">&times;</span></div>`;
    }
    if (activeFilters.compareMode) {
      html += `<div class="filter-chip" style="background:var(--color-primary-light); color:var(--color-primary);">So sánh vs 2014 <span class="filter-chip-remove" onclick="KhangNghiFilters.setFilter('compareMode', false)" title="Tắt so sánh">&times;</span></div>`;
    }

    if (html) {
      html = `<div style="display:flex; align-items:center; gap:8px; flex-wrap:wrap;"><span style="font-size:11.5px; font-weight:700; color:var(--text-muted); text-transform:uppercase; letter-spacing:0.5px;">BỘ LỌC ĐANG ÁP DỤNG:</span> ${html} <button class="reset-filters-btn" onclick="KhangNghiFilters.resetAllFilters()"><i class="fa-solid fa-arrows-rotate"></i> Xóa tất cả</button></div>`;
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
    countAdvancedFilters,
    renderChipsContainer,
    saveCurrentView,
    loadPreset,
    getPresets,
    onChange,
    getState
  };
})();
