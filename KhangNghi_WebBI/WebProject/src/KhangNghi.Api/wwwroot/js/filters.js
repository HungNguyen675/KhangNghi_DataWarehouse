/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - FILTER STATE
   Chỉ giữ bộ lọc mà API backend thực sự áp dụng: Năm.
   (Các API khác của backend không nhận tham số lọc nên không hiển thị bộ lọc giả.)
   Chip bộ lọc hiển thị trong mọi phần tử có class .filter-chips
   ========================================================================== */

window.KhangNghiFilters = (function () {
  const DEFAULT_FILTERS = { year: "ALL" };

  let activeFilters = { ...DEFAULT_FILTERS };
  const listeners = [];

  function init() {
    renderChips();
  }

  function setFilter(key, val) {
    if (!(key in DEFAULT_FILTERS) || activeFilters[key] === val) return;
    activeFilters[key] = val;
    notifyChange();
  }

  function resetAllFilters() {
    activeFilters = { ...DEFAULT_FILTERS };
    notifyChange();
  }

  function countActiveFilters() {
    return Object.keys(DEFAULT_FILTERS).filter(k => activeFilters[k] !== DEFAULT_FILTERS[k]).length;
  }

  function renderChips() {
    const chips = [];
    if (activeFilters.year !== DEFAULT_FILTERS.year) {
      chips.push(`<span class="filter-chip">Năm ${activeFilters.year}<button type="button" class="filter-chip-remove" aria-label="Bỏ lọc năm" onclick="KhangNghiFilters.setFilter('year', 'ALL')">&times;</button></span>`);
    }
    // "Xóa tất cả" chỉ cần khi có từ 2 điều kiện trở lên
    const html = chips.join("") + (chips.length > 1 ? `<button type="button" class="reset-filters-btn" onclick="KhangNghiFilters.resetAllFilters()">Xóa tất cả</button>` : "");
    document.querySelectorAll(".filter-chips").forEach(el => { el.innerHTML = html; });
  }

  function onChange(callback) {
    listeners.push(callback);
  }

  function notifyChange() {
    renderChips();
    listeners.forEach(fn => fn({ ...activeFilters }));
  }

  function getState() {
    return { ...activeFilters };
  }

  return { init, setFilter, resetAllFilters, countActiveFilters, renderChips, onChange, getState };
})();
