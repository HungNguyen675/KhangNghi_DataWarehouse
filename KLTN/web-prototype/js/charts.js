/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - APEXCHARTS MANAGER
   Enterprise Chart Rendering, Hover Tooltips & Responsive Lifecycle
   ========================================================================== */

window.KhangNghiCharts = (function () {
  const chartInstances = {};

  function destroyChart(containerId) {
    if (chartInstances[containerId]) {
      chartInstances[containerId].destroy();
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

  function renderSparkline(containerId, dataSeries, color = "#0f62fe") {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

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

  function renderRevenueTrend(containerId, seriesData, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const metric = options.metric || 'revenue';
    const gran = options.granularity || 'monthly';

    let categories = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'];
    let mainSeries = [340, 310, 390, 370, 410, 440, 420, 460, 480, 510, 530, 590];
    let subSeries = [42, 38, 48, 45, 51, 55, 52, 58, 60, 64, 66, 74];
    let mainName = 'Doanh Thu ($K)';
    let subName = 'Lợi Nhuận ($K)';

    if (metric === 'profit') {
      mainSeries = subSeries;
      mainName = 'Lợi Nhuận ($K)';
      subSeries = [12.4, 12.2, 12.3, 12.1, 12.4, 12.5, 12.3, 12.6, 12.5, 12.5, 12.4, 12.5];
      subName = 'Margin (%)';
    } else if (metric === 'quantity') {
      mainSeries = [2400, 2100, 2900, 2700, 3100, 3400, 3200, 3600, 3800, 4100, 4300, 4900];
      mainName = 'Số Lượng Sp';
      subSeries = [120, 110, 145, 135, 155, 170, 160, 180, 190, 205, 215, 245];
      subName = 'Đơn Hàng';
    } else if (metric === 'margin') {
      mainSeries = [12.4, 12.2, 12.3, 12.1, 12.4, 12.5, 12.3, 12.6, 12.5, 12.5, 12.4, 12.5];
      mainName = 'Margin (%)';
      subSeries = [42, 38, 48, 45, 51, 55, 52, 58, 60, 64, 66, 74];
      subName = 'Lợi Nhuận ($K)';
    }

    if (gran === 'quarterly') {
      categories = ['Q1 (T1-T3)', 'Q2 (T4-T6)', 'Q3 (T7-T9)', 'Q4 (T10-T12)'];
      mainSeries = [
        mainSeries[0] + mainSeries[1] + mainSeries[2],
        mainSeries[3] + mainSeries[4] + mainSeries[5],
        mainSeries[6] + mainSeries[7] + mainSeries[8],
        mainSeries[9] + mainSeries[10] + mainSeries[11]
      ];
      subSeries = [
        subSeries[0] + subSeries[1] + subSeries[2],
        subSeries[3] + subSeries[4] + subSeries[5],
        subSeries[6] + subSeries[7] + subSeries[8],
        subSeries[9] + subSeries[10] + subSeries[11]
      ];
    } else if (gran === 'yearly') {
      categories = ['2012', '2013', '2014', '2015'];
      mainSeries = [2800, 3400, 4200, 5501];
      subSeries = [310, 390, 480, 588];
    }

    const chartOptions = {
      chart: {
        type: 'area',
        height: 270,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const catLabel = categories[config.dataPointIndex];
            if (options.onPointClick && catLabel) {
              options.onPointClick(catLabel);
            }
          }
        }
      },
      colors: ['#0f62fe', '#198038'],
      stroke: { curve: 'smooth', width: 2 },
      fill: { type: 'gradient', gradient: { opacityFrom: 0.25, opacityTo: 0.05 } },
      series: [
        { name: mainName, data: mainSeries },
        { name: subName, data: subSeries }
      ],
      xaxis: {
        categories: categories,
        labels: { style: { colors: tc.text } }
      },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid },
      theme: { mode: tc.mode }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderSalesByCategory(containerId, categories, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    let catList = [...categories];

    if (options.rankingMode === 'bottom10') {
      catList.sort((a, b) => a.revenue - b.revenue);
    } else {
      catList.sort((a, b) => b.revenue - a.revenue);
    }

    const chartOptions = {
      chart: {
        type: 'bar',
        height: 270,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedCat = catList[config.dataPointIndex];
            if (options.onBarClick && selectedCat) {
              options.onBarClick(selectedCat.category);
            }
          }
        }
      },
      colors: ['#0f62fe', '#8a3ffc', '#b25900'],
      plotOptions: { bar: { borderRadius: 4, columnWidth: '40%', distributed: true } },
      series: [{ name: 'Doanh Thu ($)', data: catList.map(c => c.revenue) }],
      xaxis: { categories: catList.map(c => c.category), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid },
      legend: { show: false }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderRegionHorizontalBar(containerId, regionData) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const options = {
      chart: { type: 'bar', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#0f62fe'],
      plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '50%' } },
      series: [{ name: 'Doanh Thu ($)', data: regionData.map(r => r.revenue) }],
      xaxis: { categories: regionData.map(r => r.region), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderCustomerSegmentDonut(containerId, segments) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const options = {
      chart: { type: 'donut', height: 260, background: 'transparent' },
      colors: ['#0f62fe', '#198038', '#b25900'],
      labels: segments.map(s => s.segment),
      series: segments.map(s => s.value),
      legend: { position: 'bottom' }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderForecastChart(containerId, forecastData) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const labels = forecastData.map(d => d.month);
    const options = {
      chart: { type: 'line', height: 320, toolbar: { show: true }, background: 'transparent' },
      colors: ['#0f62fe', '#b25900', '#198038', '#da1e28'],
      stroke: { curve: 'smooth', width: [2.5, 2.5, 1.5, 1.5], dashArray: [0, 4, 2, 2] },
      series: [
        { name: 'Thực Tế (Actual)', data: forecastData.map(d => d.revenue) },
        { name: 'Dự Báo (ML.NET SSA)', data: forecastData.map(d => d.forecast) },
        { name: 'Upper 95%', data: forecastData.map(d => d.upper) },
        { name: 'Lower 95%', data: forecastData.map(d => d.lower) }
      ],
      xaxis: { categories: labels }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderProfitScatterMatrix(containerId, products) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const options = {
      chart: { type: 'bubble', height: 300, background: 'transparent' },
      colors: ['#198038', '#0f62fe', '#b25900', '#da1e28'],
      series: products.map(p => ({
        name: p.name,
        data: [[p.price / 10000, p.margin, p.stock]]
      })),
      xaxis: { title: { text: 'Doanh Thu ($K)' } },
      yaxis: { title: { text: 'Tỷ Suất Lợi Nhuận Margin (%)' } }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderWhatIfCompareBar(containerId, currentProfit, projectedProfit) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const options = {
      chart: { type: 'bar', height: 240, toolbar: { show: false }, background: 'transparent' },
      colors: ['#8d8d8d', projectedProfit >= currentProfit ? '#198038' : '#da1e28'],
      plotOptions: { bar: { columnWidth: '35%', distributed: true, borderRadius: 6 } },
      series: [{ name: 'Lợi Nhuận', data: [currentProfit, projectedProfit] }],
      xaxis: { categories: ['Hiện Tại', 'Mô Phỏng'] },
      legend: { show: false }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderSupplierBarChart(containerId, suppliers, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    let supList = [...suppliers];
    const metric = options.metric || 'cost';
    const rank = options.rankingMode || 'top10';

    if (rank === 'top5') supList = supList.slice(0, 5);
    else if (rank === 'top10') supList = supList.slice(0, 10);

    const dataSeries = supList.map(s => metric === 'qty' ? s.qty : metric === 'orders' ? s.orders : s.cost);
    const metricLabel = metric === 'qty' ? 'Số Lượng Import' : metric === 'orders' ? 'Số Đơn PO' : 'Chi Phí Mua ($)';

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
              options.onBarClick(selectedSup.name);
            }
          }
        }
      },
      colors: ['#0f62fe'],
      plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '50%' } },
      series: [{ name: metricLabel, data: dataSeries }],
      xaxis: { categories: supList.map(s => s.name), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderSupplierDonutChart(containerId, suppliers) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const options = {
      chart: { type: 'donut', height: 260, background: 'transparent' },
      colors: ['#0f62fe', '#8a3ffc', '#198038', '#b25900', '#da1e28', '#0072c6'],
      labels: suppliers.map(s => s.name),
      series: suppliers.map(s => s.cost),
      legend: { position: 'bottom' }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderUnitCostLineChart(containerId, trendData) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const options = {
      chart: { type: 'line', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#8a3ffc'],
      stroke: { curve: 'smooth', width: 2.5 },
      series: [{ name: 'Weighted Avg Unit Cost ($)', data: trendData.map(t => t.unitCost || t.cost / (t.qty || 1)) }],
      xaxis: { categories: trendData.map(t => t.label), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderManufacturerBarChart(containerId, manufacturers, options = {}) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const chartOptions = {
      chart: {
        type: 'bar',
        height: 240,
        toolbar: { show: false },
        background: 'transparent',
        events: {
          dataPointSelection: function (event, chartContext, config) {
            const selectedMfg = manufacturers[config.dataPointIndex];
            if (options.onBarClick && selectedMfg) {
              options.onBarClick(selectedMfg.name);
            }
          }
        }
      },
      colors: ['#198038'],
      plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '50%' } },
      series: [{ name: 'Chi Phí Mua ($)', data: manufacturers.map(m => m.cost) }],
      xaxis: { categories: manufacturers.map(m => m.name), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, chartOptions);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderStockMovementChart(containerId, movements = []) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const options = {
      chart: { type: 'line', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#0f62fe', '#198038', '#da1e28', '#8a3ffc'],
      stroke: { curve: 'smooth', width: 2 },
      series: [
        { name: 'Opening Stock', data: movements.map(m => m.opening) },
        { name: 'Inbound (+)', data: movements.map(m => m.inbound) },
        { name: 'Outbound (-)', data: movements.map(m => m.outbound) },
        { name: 'Closing Stock', data: movements.map(m => m.closing) }
      ],
      xaxis: { categories: movements.map(m => m.month), labels: { style: { colors: tc.text } } },
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid },
      legend: { labels: { colors: tc.text } }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderSlaDonut(containerId, slaData = []) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const options = {
      chart: { type: 'donut', height: 240, background: 'transparent' },
      labels: (slaData.length ? slaData : [{status:'On Time'}, {status:'Late'}]).map(s => s.status),
      series: (slaData.length ? slaData : [{pct:95.8}, {pct:4.2}]).map(s => s.pct || s.count),
      colors: ['#198038', '#f1c21b', '#da1e28'],
      legend: { position: 'bottom', labels: { colors: tc.text } }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderProductMatrixScatter(containerId, matrixData = []) {
    destroyChart(containerId);
    const el = document.getElementById(containerId);
    if (!el) return;

    const tc = getThemeColors();
    const series = [
      {
        name: 'Sản Phẩm',
        data: (matrixData.length ? matrixData : [
          { x: 380, y: 24.5, z: 20 }, { x: 420, y: 29.0, z: 30 }, { x: 650, y: 32.4, z: 40 }, { x: 1450, y: 18.2, z: 15 }
        ]).map(p => [p.x, p.y, p.z])
      }
    ];

    const options = {
      chart: { type: 'bubble', height: 260, toolbar: { show: false }, background: 'transparent' },
      colors: ['#0f62fe'],
      series: series,
      xaxis: { title: { text: 'Doanh Thu ($)', style: { color: tc.text } }, labels: { style: { colors: tc.text } } },
      yaxis: { title: { text: 'Profit Margin (%)', style: { color: tc.text } }, labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  function renderCrossMatrix(containerId, matrixData = []) {
    renderProductMatrixScatter(containerId, matrixData);
  }

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
      yaxis: { labels: { style: { colors: tc.text } } },
      grid: { borderColor: tc.grid }
    };
    const chart = new ApexCharts(el, options);
    chart.render();
    chartInstances[containerId] = chart;
  }

  return {
    renderSparkline,
    renderRevenueTrend,
    renderSalesByCategory,
    renderRegionHorizontalBar,
    renderCustomerSegmentDonut,
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
    renderWhatIfComparison
  };
})();

