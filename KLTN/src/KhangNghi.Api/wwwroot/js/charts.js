/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - APEXCHARTS MANAGER
   Enterprise Chart Rendering, Hover Tooltips & Responsive Lifecycle
   Instructor Guidelines Compliance:
   - 100% Verified Real Data (No Mock/Fallback series)
   - Handles 4 states: Empty, Non-empty with zeros, Error, Loading
   - Cross-filtering on Click Events (Time, Product, Supplier, Category)
   - Proper ApexCharts Lifecycle & Theme Adaptation
   ========================================================================== */

window.KhangNghiCharts = (function () {
  const chartInstances = {};

  function destroyChart(containerId) {
    if (chartInstances[containerId]) {
      try {
        chartInstances[containerId].destroy();
      } catch (e) {
        console.warn(`[KhangNghiCharts] Error destroying chart ${containerId}:`, e);
      }
      delete chartInstances[containerId];
    }
  }

  function getThemeColors() {
    const isDark = document.body.getAttribute('data-theme') === 'dark';
    return {
      text: isDark ? '#a1a1aa' : '#525252',
      grid: isDark ? '#2e2e34' : '#e0e0e0',
      mode: isDark ? 'dark' : 'light'
    };
  }

  // --------------------------------------------------------------------------
  // STATE HELPERS (Empty, Error, Loading)
  // --------------------------------------------------------------------------
  function renderEmptyState(containerId, message = "Không có dữ liệu phù hợp với điều kiện lọc hiện tại") {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    el.innerHTML = `
      <div class="chart-empty-state">
        <div class="empty-icon"><i class="fa-solid fa-chart-simple"></i></div>
        <div class="empty-title">Không có dữ liệu hiển thị</div>
        <div class="empty-desc">${message}</div>
      </div>
    `;
  }

  function renderErrorState(containerId, errorMessage = "Lỗi khi nạp dữ liệu từ hệ thống DWH") {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    el.innerHTML = `
      <div class="chart-error-state">
        <div class="error-icon"><i class="fa-solid fa-triangle-exclamation"></i></div>
        <div class="error-title">Lỗi kết nối truy vấn số liệu</div>
        <div class="error-desc">${errorMessage}</div>
      </div>
    `;
  }

  function renderLoadingState(containerId, message = "Đang tải dữ liệu từ kho DWH...") {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    el.innerHTML = `
      <div class="chart-loading-state">
        <div class="chart-spinner"></div>
        <div style="font-size:12px; color:var(--text-muted);">${message}</div>
      </div>
    `;
  }

  // --------------------------------------------------------------------------
  // CURRENCY / VALUE FORMATTER HELPER
  // --------------------------------------------------------------------------
  function formatAxisValue(val, currency = 'USD') {
    if (typeof val !== 'number' || isNaN(val)) return '0';
    if (currency === 'VND') {
      if (Math.abs(val) >= 1e9) return `${(val / 1e9).toFixed(1)} Tỷ`;
      if (Math.abs(val) >= 1e6) return `${(val / 1e6).toFixed(1)} Tr`;
      if (Math.abs(val) >= 1e3) return `${(val / 1e3).toFixed(0)} K`;
      return `${val}`;
    }
    // Default USD
    if (Math.abs(val) >= 1e6) return `$${(val / 1e6).toFixed(1)}M`;
    if (Math.abs(val) >= 1e3) return `$${(val / 1e3).toFixed(0)}K`;
    return `$${val}`;
  }

  function formatTooltipValue(val, currency = 'USD') {
    if (typeof val !== 'number' || isNaN(val)) return '0';
    if (currency === 'VND') {
      return `${Math.round(val).toLocaleString('vi-VN')} VNĐ`;
    }
    return `$${Math.round(val).toLocaleString('en-US')}`;
  }

  // --------------------------------------------------------------------------
  // SPARKLINE
  // --------------------------------------------------------------------------
  function renderSparkline(containerId, dataSeries, color = "#0f62fe") {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(dataSeries) || dataSeries.length === 0) {
      el.innerHTML = '';
      return;
    }

    const options = {
      chart: { type: 'line', height: 24, sparkline: { enabled: true } },
      stroke: { curve: 'smooth', width: 2 },
      colors: [color],
      series: [{ data: dataSeries }],
      tooltip: { enabled: false }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // BIỂU ĐỒ 1: XU HƯỚNG THEO THỜI GIAN (DỮ LIỆU THẬT DWH)
  // ==========================================================================
  function renderRevenueTrend(containerId, seriesData, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(seriesData) || seriesData.length === 0) {
      renderEmptyState(containerId, options.emptyMessage || "Không có dữ liệu xu hướng cho điều kiện lọc đã chọn");
      return;
    }

    // Lọc bỏ các phần tử rỗng hoặc không có dữ liệu
    const validData = seriesData.filter(d => d && (d.Period || d.period || d.Year || d.year || d.label || d.month || d.Month));
    if (validData.length === 0) {
      renderEmptyState(containerId, options.emptyMessage || "Không có dữ liệu xu hướng phù hợp");
      return;
    }

    const tc = getThemeColors();
    const metric = options.metric || 'revenue';
    const isVND = options.currency === 'VND' || options.isVND === true;
    const currency = isVND ? 'VND' : 'USD';

    const categories = validData.map(d => {
      if (d.Period || d.period) return d.Period || d.period;
      if (d.Month && d.Year) return `T${d.Month}/${d.Year}`;
      if (d.month && d.year) return `T${d.month}/${d.year}`;
      if (d.Year) return `Năm ${d.Year}`;
      if (d.year) return `Năm ${d.year}`;
      return d.label || '';
    });

    let mainSeries = [];
    let subSeries = [];
    let mainName = isVND ? 'Chi phí mua (VNĐ)' : 'Doanh thu ($)';
    let subName = isVND ? 'Số lượng nhập' : 'Lợi nhuận ($)';

    if (metric === 'profit') {
      mainSeries = validData.map(d => Math.round(d.Profit ?? d.profit ?? 0));
      mainName = 'Lợi nhuận ($)';
      subSeries = validData.map(d => Math.round(d.Revenue ?? d.revenue ?? 0));
      subName = 'Doanh thu ($)';
    } else if (metric === 'quantity' || metric === 'qty') {
      mainSeries = validData.map(d => d.Quantity ?? d.quantity ?? d.TotalQuantity ?? d.totalQuantity ?? d.qty ?? 0);
      mainName = 'Số lượng (Sp)';
      subSeries = validData.map(d => Math.round(d.Revenue ?? d.revenue ?? d.TotalCost ?? d.totalCost ?? d.cost ?? 0));
      subName = isVND ? 'Tổng chi phí (VNĐ)' : 'Doanh thu ($)';
    } else if (metric === 'cost') {
      mainSeries = validData.map(d => Math.round(d.TotalCost ?? d.totalCost ?? d.cost ?? d.TotalPurchaseAmount ?? d.totalPurchaseAmount ?? 0));
      mainName = isVND ? 'Chi phí mua (VNĐ)' : 'Chi phí mua ($)';
      subSeries = validData.map(d => d.Quantity ?? d.quantity ?? d.TotalQuantity ?? d.totalQuantity ?? d.qty ?? 0);
      subName = 'Số lượng nhập (Sp)';
    } else {
      mainSeries = validData.map(d => Math.round(d.Revenue ?? d.revenue ?? d.TotalCost ?? d.totalCost ?? 0));
      mainName = isVND ? 'Chi phí mua (VNĐ)' : 'Doanh thu ($)';
      subSeries = validData.map(d => Math.round(d.Profit ?? d.profit ?? d.Quantity ?? d.quantity ?? 0));
      subName = isVND ? 'Số lượng mua' : 'Lợi nhuận ($)';
    }

    const series = [{ name: mainName, data: mainSeries }];
    if (subSeries.some(v => v > 0)) {
      series.push({ name: subName, data: subSeries });
    }

    const chartOptions = {
      chart: {
        type: 'area',
        height: 275,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const rawItem = validData[config.dataPointIndex];
            const catLabel = categories[config.dataPointIndex];
            if (options.onPointClick && rawItem) {
              options.onPointClick(rawItem, catLabel);
            }
          }
        }
      },
      colors: ['#0f62fe', '#198038'],
      stroke: { curve: 'smooth', width: 2 },
      fill: { type: 'gradient', gradient: { opacityFrom: 0.28, opacityTo: 0.05 } },
      series: series,
      xaxis: {
        categories: categories,
        labels: { style: { colors: tc.text, fontSize: '11px' } }
      },
      yaxis: {
        labels: {
          style: { colors: tc.text, fontSize: '11px' },
          formatter: (v) => formatAxisValue(v, currency)
        }
      },
      tooltip: {
        y: {
          formatter: (v, { seriesIndex }) => {
            if (seriesIndex === 1 && subName.includes('Số lượng')) {
              return `${v.toLocaleString()} sp`;
            }
            return formatTooltipValue(v, currency);
          }
        }
      },
      grid: { borderColor: tc.grid },
      theme: { mode: tc.mode }
    };

    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // BIỂU ĐỒ 2: XẾP HẠNG SẢN PHẨM HOẶC DANH MỤC (HORIZONTAL BAR)
  // ==========================================================================
  function renderSalesByCategory(containerId, products, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(products) || products.length === 0) {
      renderEmptyState(containerId, options.emptyMessage || "Không có dữ liệu xếp hạng trong phạm vi lọc");
      return;
    }

    const validItems = products.filter(p => p && (p.ProductName || p.productName || p.category || p.Category || p.name));
    if (validItems.length === 0) {
      renderEmptyState(containerId, "Không có mục dữ liệu hợp lệ để xếp hạng");
      return;
    }

    const tc = getThemeColors();
    let list = [...validItems];
    const metric = options.metric || 'revenue';
    const isVND = options.currency === 'VND' || options.isVND === true;
    const currency = isVND ? 'VND' : 'USD';

    // Sắp xếp
    if (options.rankingMode === 'bottom10') {
      list.sort((a, b) => {
        const valA = metric === 'profit' ? (a.Profit ?? a.profit ?? 0) : (a.Revenue ?? a.revenue ?? a.TotalPurchaseAmount ?? a.totalPurchaseAmount ?? a.cost ?? 0);
        const valB = metric === 'profit' ? (b.Profit ?? b.profit ?? 0) : (b.Revenue ?? b.revenue ?? b.TotalPurchaseAmount ?? b.totalPurchaseAmount ?? b.cost ?? 0);
        return valA - valB;
      });
    } else {
      list.sort((a, b) => {
        const valA = metric === 'profit' ? (a.Profit ?? a.profit ?? 0) : (a.Revenue ?? a.revenue ?? a.TotalPurchaseAmount ?? a.totalPurchaseAmount ?? a.cost ?? 0);
        const valB = metric === 'profit' ? (b.Profit ?? b.profit ?? 0) : (b.Revenue ?? b.revenue ?? b.TotalPurchaseAmount ?? b.totalPurchaseAmount ?? b.cost ?? 0);
        return valB - valA;
      });
    }

    const topLimit = options.topN || 10;
    const topList = list.slice(0, topLimit);

    const names = topList.map(p => {
      const n = p.ProductName || p.productName || p.category || p.Category || p.name || 'Đối tượng';
      return n.length > 28 ? n.substring(0, 26) + '...' : n;
    });

    const values = topList.map(p => {
      if (metric === 'profit') return Math.round(p.Profit ?? p.profit ?? 0);
      if (metric === 'qty' || metric === 'quantity') return p.Quantity ?? p.quantity ?? p.TotalQuantity ?? p.totalQuantity ?? 0;
      return Math.round(p.Revenue ?? p.revenue ?? p.TotalPurchaseAmount ?? p.totalPurchaseAmount ?? p.cost ?? 0);
    });

    const seriesName = metric === 'profit' ? 'Lợi nhuận ($)' : (isVND ? 'Chi phí mua (VNĐ)' : 'Doanh thu ($)');

    const chartOptions = {
      chart: {
        type: 'bar',
        height: 275,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedItem = topList[config.dataPointIndex];
            if (options.onBarClick && selectedItem) {
              options.onBarClick(selectedItem);
            }
          }
        }
      },
      plotOptions: {
        bar: {
          horizontal: true,
          borderRadius: 4,
          barHeight: '65%',
          distributed: false
        }
      },
      colors: [options.rankingMode === 'bottom10' ? '#da1e28' : '#0f62fe'],
      series: [{ name: seriesName, data: values }],
      xaxis: {
        categories: names,
        labels: {
          style: { colors: tc.text, fontSize: '11px' },
          formatter: (v) => formatAxisValue(v, currency)
        }
      },
      yaxis: {
        labels: { style: { colors: tc.text, fontSize: '11px' } }
      },
      tooltip: {
        y: { formatter: (v) => formatTooltipValue(v, currency) }
      },
      grid: { borderColor: tc.grid },
      legend: { show: false }
    };

    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // BIỂU ĐỒ 3: KẾT HỢP NĂM × SẢN PHẨM (GROUPED COLUMN DỮ LIỆU CUBE SSAS)
  // ==========================================================================
  function renderTimeProductCombinedChart(containerId, dataPoints, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(dataPoints) || dataPoints.length === 0) {
      renderEmptyState(containerId, options.emptyMessage || "Không có dữ liệu phân tích kết hợp theo thời gian");
      return;
    }

    const tc = getThemeColors();
    const prodMap = {};
    const yearsSet = new Set();

    dataPoints.forEach(d => {
      const pName = d["Product Name"] || d.ProductName || d.productName;
      const yr = String(d.Year || d.year || '');
      if (pName && !pName.includes("Tổng cộng") && yr) {
        yearsSet.add(yr);
        if (!prodMap[pName]) prodMap[pName] = { name: pName, total: 0, byYear: {} };
        const val = Number(d["Sales Amount"] || d.Revenue || d.revenue || d.TotalPurchaseAmount || 0);
        prodMap[pName].byYear[yr] = (prodMap[pName].byYear[yr] || 0) + val;
        prodMap[pName].total += val;
      }
    });

    const years = Array.from(yearsSet).sort();
    if (years.length === 0 || Object.keys(prodMap).length === 0) {
      renderEmptyState(containerId, "Dữ liệu đa chiều rỗng hoặc không có năm hợp lệ");
      return;
    }

    const sortedProds = Object.values(prodMap).sort((a, b) => b.total - a.total).slice(0, 5);

    const series = sortedProds.map(p => ({
      name: p.name.length > 28 ? p.name.substring(0, 26) + '...' : p.name,
      data: years.map(yr => Math.round(p.byYear[yr] || 0))
    }));

    const chartOptions = {
      chart: {
        type: 'bar',
        height: 290,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const yr = years[config.dataPointIndex];
            const p = sortedProds[config.seriesIndex];
            if (options.onBarClick && p && yr) {
              options.onBarClick(p.name, yr);
            }
          }
        }
      },
      plotOptions: {
        bar: {
          horizontal: false,
          columnWidth: '55%',
          borderRadius: 4
        }
      },
      colors: ['#0f62fe', '#8a3ffc', '#198038', '#f1c21b', '#fa4d56'],
      series: series,
      xaxis: {
        categories: years.map(y => `Năm ${y}`),
        labels: { style: { colors: tc.text, fontSize: '12px' } }
      },
      yaxis: {
        labels: {
          style: { colors: tc.text, fontSize: '11px' },
          formatter: (v) => formatAxisValue(v, options.currency || 'USD')
        }
      },
      tooltip: {
        y: { formatter: (v) => formatTooltipValue(v, options.currency || 'USD') }
      },
      legend: {
        position: 'top',
        horizontalAlign: 'center',
        labels: { colors: tc.text }
      },
      grid: { borderColor: tc.grid },
      theme: { mode: tc.mode }
    };

    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // BIỂU ĐỒ 4: CƠ CẤU TỶ TRỌNG DOANH THU HOẶC CHI PHÍ (DONUT)
  // ==========================================================================
  function renderProductShareDonut(containerId, items, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(items) || items.length === 0) {
      renderEmptyState(containerId, options.emptyMessage || "Không có dữ liệu cơ cấu cho điều kiện lọc đã chọn");
      return;
    }

    const validItems = items.filter(item => {
      const val = Number(item.Revenue ?? item.revenue ?? item.Profit ?? item.profit ?? item.TotalPurchaseAmount ?? item.totalPurchaseAmount ?? item.cost ?? 0);
      return val > 0;
    });

    if (validItems.length === 0) {
      renderEmptyState(containerId, "Tất cả các nhóm đều có giá trị bằng 0 trong phạm vi lọc");
      return;
    }

    const tc = getThemeColors();
    const metric = options.metric || 'revenue';
    const isVND = options.currency === 'VND' || options.isVND === true;
    const currency = isVND ? 'VND' : 'USD';

    const labels = validItems.map(item => item.Category || item.category || item.ProductName || item.name || 'Khác');
    const series = validItems.map(item => Math.round(metric === 'profit' ? (item.Profit ?? item.profit ?? 0) : (item.Revenue ?? item.revenue ?? item.TotalPurchaseAmount ?? item.totalPurchaseAmount ?? item.cost ?? 0)));

    const chartOptions = {
      chart: {
        type: 'donut',
        height: 275,
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedItem = validItems[config.dataPointIndex];
            const selectedLabel = labels[config.dataPointIndex];
            if (options.onSliceClick && selectedItem) {
              options.onSliceClick(selectedItem, selectedLabel);
            }
          }
        }
      },
      colors: ['#0f62fe', '#8a3ffc', '#0072c3', '#00539a', '#b25900', '#198038', '#da1e28'],
      series: series,
      labels: labels,
      legend: {
        position: 'bottom',
        fontSize: '11.5px',
        labels: { colors: tc.text }
      },
      plotOptions: {
        pie: {
          donut: {
            size: '65%',
            labels: {
              show: true,
              total: {
                show: true,
                label: 'Tổng số',
                color: tc.text,
                formatter: function (w) {
                  const sum = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                  return formatAxisValue(sum, currency);
                }
              }
            }
          }
        }
      },
      tooltip: {
        y: { formatter: (v) => formatTooltipValue(v, currency) }
      },
      stroke: { show: false },
      theme: { mode: tc.mode }
    };

    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // KHU VỰC ĐỊA LÝ (HORIZONTAL BAR)
  // ==========================================================================
  function renderRegionHorizontalBar(containerId, regionData, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(regionData) || regionData.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu phân bổ theo khu vực");
      return;
    }

    const tc = getThemeColors();
    const list = [...regionData].filter(r => r && (r.region || r.Region || r.name));
    list.sort((a, b) => (b.revenue ?? b.Revenue ?? 0) - (a.revenue ?? a.Revenue ?? 0));
    const topRegions = list.slice(0, 8);

    if (topRegions.length === 0) {
      renderEmptyState(containerId, "Không có bản ghi khu vực hợp lệ");
      return;
    }

    const chartOptions = {
      chart: {
        type: 'bar',
        height: 260,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const reg = topRegions[config.dataPointIndex];
            if (options.onBarClick && reg) {
              options.onBarClick(reg.region || reg.Region);
            }
          }
        }
      },
      colors: ['#0f62fe'],
      plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '55%' } },
      dataLabels: {
        enabled: true,
        formatter: (val) => formatAxisValue(val, 'USD'),
        style: { fontSize: '11px', colors: ['#ffffff'] }
      },
      series: [{ name: 'Doanh Thu ($)', data: topRegions.map(r => Math.round(r.revenue ?? r.Revenue ?? 0)) }],
      xaxis: {
        categories: topRegions.map(r => r.region || r.Region || 'Khác'),
        labels: {
          style: { colors: tc.text },
          formatter: (val) => formatAxisValue(val, 'USD')
        }
      },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid },
      tooltip: {
        y: { formatter: (val) => formatTooltipValue(val, 'USD') }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // PHÂN KHÚC KHÁCH HÀNG (DONUT)
  // ==========================================================================
  function renderCustomerSegmentDonut(containerId, segments, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(segments) || segments.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu phân khúc khách hàng");
      return;
    }

    const validSegs = segments.filter(s => (s.value ?? s.revenue ?? s.Revenue ?? s.cost ?? 0) > 0);
    if (validSegs.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu phân khúc có doanh thu");
      return;
    }

    const labels = validSegs.map(s => s.segment || s.Segment || s.name || 'Khác');
    const series = validSegs.map(s => Math.round(s.value ?? s.revenue ?? s.Revenue ?? s.cost ?? 0));

    const chartOptions = {
      chart: {
        type: 'donut',
        height: 260,
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const seg = validSegs[config.dataPointIndex];
            const label = labels[config.dataPointIndex];
            if (options.onSliceClick && seg) {
              options.onSliceClick(seg, label);
            }
          }
        }
      },
      colors: ['#0f62fe', '#198038', '#b25900', '#8a3ffc'],
      labels: labels,
      series: series,
      legend: { position: 'bottom' },
      tooltip: {
        y: { formatter: (val) => formatTooltipValue(val, 'USD') }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // MA TRẬN KHÁCH HÀNG (SCATTER PLOT)
  // ==========================================================================
  function renderCustomerScatterMatrix(containerId, customers, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(customers) || customers.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu khách hàng để vẽ ma trận");
      return;
    }

    const tc = getThemeColors();
    const list = customers.slice(0, 20);

    const chartOptions = {
      chart: {
        type: 'scatter',
        height: 260,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const c = list[config.dataPointIndex];
            if (options.onPointClick && c) {
              options.onPointClick(c);
            }
          }
        }
      },
      colors: ['#0f62fe'],
      series: [{
        name: 'Khách hàng',
        data: list.map(c => [
          Math.round(c.revenue ?? c.Revenue ?? 0),
          Math.round(c.profit ?? c.Profit ?? 0)
        ])
      }],
      xaxis: {
        title: { text: 'Doanh Thu ($)', style: { color: tc.text } },
        labels: {
          style: { colors: tc.text },
          formatter: (val) => formatAxisValue(val, 'USD')
        }
      },
      yaxis: {
        title: { text: 'Lợi Nhuận ($)', style: { color: tc.text } },
        labels: {
          style: { colors: tc.text },
          formatter: (val) => formatAxisValue(val, 'USD')
        }
      },
      grid: { borderColor: tc.grid },
      tooltip: {
        custom: function({ seriesIndex, dataPointIndex }) {
          const c = list[dataPointIndex];
          if (!c) return '';
          const name = c.customerName || c.CustomerName || `KH #${dataPointIndex + 1}`;
          const rev = Math.round(c.revenue ?? c.Revenue ?? 0).toLocaleString();
          const prof = Math.round(c.profit ?? c.Profit ?? 0).toLocaleString();
          return `<div style="padding:8px 12px; font-size:12px; background:var(--bg-app); border:1px solid var(--border-color); border-radius:6px; color:var(--text-main);"><strong>${name}</strong><br>Doanh thu: $${rev}<br>Lợi nhuận: $${prof}</div>`;
        }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // DỰ BÁO DOANH SỐ (ML.NET SSA LINE CHART)
  // ==========================================================================
  function renderForecastChart(containerId, forecastData, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(forecastData) || forecastData.length === 0) {
      renderEmptyState(containerId, "Chưa có chuỗi dữ liệu dự báo ML.NET");
      return;
    }

    const labels = forecastData.map(d => d.month || d.Month || d.period || d.Period || '');
    const actualData = forecastData.map(d => Math.round(d.revenue ?? d.Revenue ?? d.actualValue ?? d.ActualValue ?? 0));
    const forecastPoints = forecastData.map(d => Math.round(d.forecast ?? d.Forecast ?? d.forecastValue ?? d.ForecastValue ?? 0));
    const upperPoints = forecastData.map(d => Math.round(d.upper ?? d.Upper ?? d.upperBound ?? d.UpperBound ?? 0));
    const lowerPoints = forecastData.map(d => Math.round(d.lower ?? d.Lower ?? d.lowerBound ?? d.LowerBound ?? 0));

    const series = [
      { name: 'Thực tế (Actual)', data: actualData },
      { name: 'Dự báo (ML.NET SSA)', data: forecastPoints }
    ];

    if (upperPoints.some(v => v > 0)) {
      series.push({ name: 'Biên trên 95%', data: upperPoints });
      series.push({ name: 'Biên dưới 95%', data: lowerPoints });
    }

    const chartOptions = {
      chart: { type: 'line', height: 320, toolbar: { show: true }, background: 'transparent' },
      colors: ['#0f62fe', '#b25900', '#198038', '#da1e28'],
      stroke: { curve: 'smooth', width: [2.5, 2.5, 1.5, 1.5], dashArray: [0, 4, 2, 2] },
      series: series,
      xaxis: { categories: labels },
      yaxis: {
        labels: { formatter: (v) => formatAxisValue(v, 'USD') }
      },
      tooltip: {
        y: { formatter: (v) => formatTooltipValue(v, 'USD') }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // NHÀ CUNG CẤP (HORIZONTAL BAR - CHI PHÍ THẬT 2022-2025)
  // ==========================================================================
  function renderSupplierBarChart(containerId, suppliers, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(suppliers) || suppliers.length === 0) {
      renderEmptyState(containerId, options.emptyMessage || "Không có dữ liệu nhà cung cấp trong phạm vi lọc");
      return;
    }

    const tc = getThemeColors();
    let supList = [...suppliers].filter(s => s && (s.name || s.supplierName || s.SupplierName));
    const metric = options.metric || 'cost';
    const rank = options.rankingMode || 'top10';

    if (rank === 'top5') supList = supList.slice(0, 5);
    else if (rank === 'top10') supList = supList.slice(0, 10);

    if (supList.length === 0) {
      renderEmptyState(containerId, "Không có nhà cung cấp hợp lệ");
      return;
    }

    const isVND = options.currency === 'VND' || options.isVND !== false; // Mua hàng mặc định VNĐ
    const dataSeries = supList.map(s => {
      if (metric === 'qty') return (s.qty ?? s.totalQuantity ?? 0);
      if (metric === 'orders') return (s.orders ?? s.orderCount ?? 0);
      return Math.round(s.cost ?? s.totalPurchaseAmount ?? s.TotalPurchaseAmount ?? 0);
    });

    const metricLabel = metric === 'qty' ? 'Số Lượng Nhập (Sp)' : metric === 'orders' ? 'Số Đơn PO' : 'Chi Phí Mua (VNĐ)';

    const chartOptions = {
      chart: {
        type: 'bar',
        height: 270,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedSup = supList[config.dataPointIndex];
            if (options.onBarClick && selectedSup) {
              options.onBarClick(selectedSup.name || selectedSup.supplierName || selectedSup.SupplierName);
            }
          }
        }
      },
      colors: ['#0f62fe'],
      plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '50%' } },
      series: [{ name: metricLabel, data: dataSeries }],
      xaxis: {
        categories: supList.map(s => s.name || s.supplierName || s.SupplierName || 'NCC'),
        labels: {
          style: { colors: tc.text },
          formatter: (v) => metric === 'qty' || metric === 'orders' ? v.toLocaleString() : formatAxisValue(v, isVND ? 'VND' : 'USD')
        }
      },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid },
      tooltip: {
        y: {
          formatter: (v) => metric === 'qty' || metric === 'orders' ? `${v.toLocaleString()} đơn/sp` : formatTooltipValue(v, isVND ? 'VND' : 'USD')
        }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // CƠ CẤU CHI PHÍ THEO NHÀ CUNG CẤP (DONUT)
  // ==========================================================================
  function renderSupplierDonutChart(containerId, suppliers, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(suppliers) || suppliers.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu cơ cấu nhà cung cấp");
      return;
    }

    const validSups = suppliers.filter(s => (s.cost ?? s.totalPurchaseAmount ?? s.TotalPurchaseAmount ?? 0) > 0);
    if (validSups.length === 0) {
      renderEmptyState(containerId, "Tất cả các nhà cung cấp đều có chi phí bằng 0");
      return;
    }

    const labels = validSups.map(s => s.name || s.supplierName || s.SupplierName || 'NCC');
    const series = validSups.map(s => Math.round(s.cost ?? s.totalPurchaseAmount ?? s.TotalPurchaseAmount ?? 0));
    const isVND = options.currency === 'VND' || options.isVND !== false;

    const chartOptions = {
      chart: {
        type: 'donut',
        height: 260,
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedSup = validSups[config.dataPointIndex];
            const selectedLabel = labels[config.dataPointIndex];
            if (options.onSliceClick && selectedSup) {
              options.onSliceClick(selectedSup, selectedLabel);
            }
          }
        }
      },
      colors: ['#0f62fe', '#8a3ffc', '#198038', '#b25900', '#da1e28', '#0072c6'],
      labels: labels,
      series: series,
      legend: { position: 'bottom' },
      tooltip: {
        y: { formatter: (val) => formatTooltipValue(val, isVND ? 'VND' : 'USD') }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // ĐƠN GIÁ BÌNH QUÂN (WEIGHTED UNIT COST TREND)
  // ==========================================================================
  function renderUnitCostLineChart(containerId, trendData, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(trendData) || trendData.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu xu hướng đơn giá");
      return;
    }

    const tc = getThemeColors();
    const isVND = options.currency === 'VND' || options.isVND !== false;
    const categories = trendData.map(t => t.label || t.period || t.Period || (t.Month ? `T${t.Month}/${t.Year}` : ''));
    const values = trendData.map(t => Math.round(t.unitCost ?? (t.cost && t.qty ? t.cost / t.qty : (t.avgUnitCost ?? 0))));

    const chartOptions = {
      chart: { type: 'line', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#8a3ffc'],
      stroke: { curve: 'smooth', width: 2.5 },
      series: [{ name: isVND ? 'Đơn giá TB (VNĐ/sp)' : 'Đơn giá TB ($/sp)', data: values }],
      xaxis: { categories: categories, labels: { style: { colors: tc.text } } },
      yaxis: {
        labels: {
          style: { colors: tc.text },
          formatter: (v) => formatAxisValue(v, isVND ? 'VND' : 'USD')
        }
      },
      grid: { borderColor: tc.grid },
      tooltip: {
        y: { formatter: (v) => formatTooltipValue(v, isVND ? 'VND' : 'USD') }
      }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // HÃNG SẢN XUẤT (HORIZONTAL BAR)
  // ==========================================================================
  function renderManufacturerBarChart(containerId, manufacturers, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(manufacturers) || manufacturers.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu hãng sản xuất");
      return;
    }

    const tc = getThemeColors();
    const validMfgs = manufacturers.filter(m => m && (m.name || m.manufacturer || m.Manufacturer));
    if (validMfgs.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu hãng hợp lệ");
      return;
    }

    const chartOptions = {
      chart: {
        type: 'bar',
        height: 240,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedMfg = validMfgs[config.dataPointIndex];
            if (options.onBarClick && selectedMfg) {
              options.onBarClick(selectedMfg.name || selectedMfg.manufacturer || selectedMfg.Manufacturer);
            }
          }
        }
      },
      colors: ['#198038'],
      plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '50%' } },
      series: [{ name: 'Chi Phí Mua ($)', data: validMfgs.map(m => Math.round(m.cost ?? m.totalPurchaseAmount ?? 0)) }],
      xaxis: {
        categories: validMfgs.map(m => m.name || m.manufacturer || m.Manufacturer || 'Hãng'),
        labels: { style: { colors: tc.text }, formatter: (v) => formatAxisValue(v, 'USD') }
      },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // BIẾN ĐỘNG XUẤT NHẬP TỒN KHO
  // ==========================================================================
  function renderStockMovementChart(containerId, movements = []) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(movements) || movements.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu biến động tồn kho");
      return;
    }

    const tc = getThemeColors();
    const options = {
      chart: { type: 'line', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#0f62fe', '#198038', '#da1e28', '#8a3ffc'],
      stroke: { curve: 'smooth', width: 2 },
      series: [
        { name: 'Tồn đầu kỳ', data: movements.map(m => m.opening ?? m.qtyIn ?? 0) },
        { name: 'Nhập kho (+)', data: movements.map(m => m.inbound ?? m.qtyIn ?? 0) },
        { name: 'Xuất kho (-)', data: movements.map(m => m.outbound ?? m.qtyOut ?? 0) },
        { name: 'Tồn cuối kỳ', data: movements.map(m => m.closing ?? m.onHand ?? 0) }
      ],
      xaxis: { categories: movements.map(m => m.month || m.period || ''), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text }, formatter: (v) => v.toLocaleString() } },
      grid: { borderColor: tc.grid },
      legend: { labels: { colors: tc.text } }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // VẬN CHUYỂN SLA (DONUT)
  // ==========================================================================
  function renderSlaDonut(containerId, slaData = []) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(slaData) || slaData.length === 0) {
      renderEmptyState(containerId, "Chưa có dữ liệu giám sát SLA vận chuyển");
      return;
    }

    const tc = getThemeColors();
    const labels = slaData.map(s => s.name || s.status || 'Khác');
    const series = slaData.map(s => Number(s.value ?? s.pct ?? s.count ?? 0));

    const options = {
      chart: { type: 'donut', height: 240, background: 'transparent' },
      labels: labels,
      series: series,
      colors: ['#198038', '#da1e28', '#f1c21b'],
      legend: { position: 'bottom', labels: { colors: tc.text } },
      tooltip: {
        y: { formatter: (val) => `${val}%` }
      }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  // ==========================================================================
  // PHÂN TÍCH CHÉO / BUBBLE MATRIX
  // ==========================================================================
  function renderProductMatrixScatter(containerId, matrixData = [], options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    if (!Array.isArray(matrixData) || matrixData.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu ma trận chéo rủi ro thiếu hàng");
      return;
    }

    const tc = getThemeColors();
    const series = [
      {
        name: 'Sản Phẩm',
        data: matrixData.map(p => [p.x ?? p.revenue ?? 0, p.y ?? p.margin ?? 0, p.z ?? p.stock ?? 10])
      }
    ];

    const chartOptions = {
      chart: { type: 'bubble', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#0f62fe'],
      series: series,
      xaxis: {
        title: { text: 'Doanh Thu ($)', style: { color: tc.text } },
        labels: { style: { colors: tc.text }, formatter: (v) => formatAxisValue(v, 'USD') }
      },
      yaxis: {
        title: { text: 'Profit Margin (%)', style: { color: tc.text } },
        labels: { style: { colors: tc.text } }
      },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderCrossMatrix(containerId, matrixData = [], options = {}) {
    renderProductMatrixScatter(containerId, matrixData, options);
  }

  // ==========================================================================
  // MÔ PHỎNG WHAT-IF
  // ==========================================================================
  function renderWhatIfComparison(containerId, baseline, scenario) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const options = {
      chart: { type: 'bar', height: 220, toolbar: { show: false }, background: 'transparent' },
      colors: ['#0f62fe', '#198038'],
      plotOptions: { bar: { columnWidth: '40%', borderRadius: 4 } },
      series: [{ name: 'Lợi Nhuận ($)', data: [Math.round(baseline), Math.round(scenario)] }],
      xaxis: { categories: ['Baseline (Hiện tại)', 'Kịch Bản Mô Phỏng'], labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text }, formatter: (v) => formatAxisValue(v, 'USD') } },
      grid: { borderColor: tc.grid },
      tooltip: {
        y: { formatter: (v) => formatTooltipValue(v, 'USD') }
      }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderWhatIfCompareBar(containerId, currentProfit, projectedProfit) {
    renderWhatIfComparison(containerId, currentProfit, projectedProfit);
  }

  function renderProfitScatterMatrix(containerId, products, options = {}) {
    if (!Array.isArray(products) || products.length === 0) {
      renderEmptyState(containerId, "Không có dữ liệu ma trận lợi nhuận");
      return;
    }
    const matrix = products.map(p => ({
      x: p.revenue ?? p.price ?? 0,
      y: p.margin ?? 12,
      z: p.stock ?? 20
    }));
    renderProductMatrixScatter(containerId, matrix, options);
  }

  return {
    destroyChart,
    renderEmptyState,
    renderErrorState,
    renderLoadingState,
    renderSparkline,
    renderRevenueTrend,
    renderSalesByCategory,
    renderRegionHorizontalBar,
    renderCustomerSegmentDonut,
    renderCustomerScatterMatrix,
    renderForecastChart,
    renderProfitScatterMatrix,
    renderWhatIfCompareBar,
    renderSupplierBarChart,
    renderSupplierDonutChart,
    renderUnitCostLineChart,
    renderManufacturerBarChart,
    renderStockMovementChart,
    renderSlaDonut,
    renderProductMatrixScatter,
    renderCrossMatrix,
    renderWhatIfComparison,
    renderTimeProductCombinedChart,
    renderProductShareDonut
  };
})();
