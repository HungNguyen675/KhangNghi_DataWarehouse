/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - CHART ENGINE (ApexCharts)
   Bốn dạng biểu đồ dùng chung, nhận dữ liệu đã chuẩn hóa từ analyticsService.
   - Màu danh mục theo thứ tự cố định (bảng màu đã kiểm định CVD, có bộ cho nền tối)
   - Một trục y duy nhất; không tạo dữ liệu khi mảng rỗng
   ========================================================================== */

window.KhangNghiCharts = (function () {
  const instances = {};
  const fmt = (v, f) => window.KhangNghiComponents.formatValue(v, f);

  const PALETTE = {
    light: ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948"],
    dark: ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"]
  };
  const OTHER_COLOR = { light: "#a3a9b5", dark: "#5b6170" };

  function theme() {
    const dark = document.body.getAttribute("data-theme") === "dark";
    const css = getComputedStyle(document.body);
    return {
      mode: dark ? "dark" : "light",
      colors: dark ? PALETTE.dark : PALETTE.light,
      other: dark ? OTHER_COLOR.dark : OTHER_COLOR.light,
      text: css.getPropertyValue("--text-secondary").trim() || "#4b5563",
      muted: css.getPropertyValue("--text-muted").trim() || "#7b8494",
      grid: css.getPropertyValue("--border-color").trim() || "#e3e7ee",
      surface: css.getPropertyValue("--bg-card").trim() || "#ffffff"
    };
  }

  function destroy(id) {
    if (instances[id]) {
      try { instances[id].destroy(); } catch (e) { /* đã bị gỡ khỏi DOM */ }
      delete instances[id];
    }
  }

  function mount(id, options) {
    destroy(id);
    const el = document.getElementById(id);
    if (!el || typeof ApexCharts === "undefined") return null;
    el.innerHTML = "";
    const chart = new ApexCharts(el, options);
    chart.render();
    instances[id] = chart;
    return chart;
  }

  function base(t, type, height) {
    return {
      chart: {
        type,
        height,
        fontFamily: "Inter, -apple-system, Segoe UI, sans-serif",
        foreColor: t.text,
        background: "transparent",
        toolbar: { show: false },
        zoom: { enabled: false },
        animations: { enabled: true, speed: 350 }
      },
      theme: { mode: t.mode },
      grid: { borderColor: t.grid, strokeDashArray: 3, padding: { left: 8, right: 8 } },
      dataLabels: { enabled: false },
      legend: { position: "top", horizontalAlign: "left", fontSize: "12px", markers: { size: 6 }, labels: { colors: t.text } },
      tooltip: { theme: t.mode }
    };
  }

  function shorten(s, n = 26) {
    s = String(s || "");
    return s.length > n ? s.slice(0, n - 1) + "…" : s;
  }

  // --------------------------------------------------------------------------
  // CỘT (dọc hoặc ngang) - một chuỗi số liệu
  // --------------------------------------------------------------------------
  function bar(id, items, opts = {}) {
    const { value = "value", label = "label", format = "number", horizontal = false, max = 12, seriesName = "", height = 300, colorIndex = 0, colorOf = null, onClick = null } = opts;
    const data = (items || []).slice(0, max);
    if (!data.length) return null;
    const t = theme();
    const o = base(t, "bar", height);
    o.series = [{ name: seriesName, data: data.map(d => d[value] ?? 0) }];
    o.colors = [t.colors[colorIndex % t.colors.length]];
    // Ít thanh thì thu hẹp độ dày để không thành khối lớn
    const thick = data.length <= 2 ? "28%" : data.length <= 4 ? "45%" : "62%";
    o.plotOptions = { bar: { horizontal, borderRadius: 4, borderRadiusApplication: "end", barHeight: thick, columnWidth: data.length <= 2 ? "30%" : "55%" } };
    // colorOf(item) -> màu riêng từng thanh (vd. theo tình trạng)
    if (colorOf) {
      o.colors = data.map(colorOf);
      o.plotOptions.bar.distributed = true;
    }
    o.xaxis = {
      categories: data.map(d => (horizontal ? shorten(d[label], 24) : shorten(d[label], 16))),
      labels: { formatter: v => (horizontal ? fmt(v, format) : v), style: { fontSize: "11px" } },
      axisBorder: { show: false },
      axisTicks: { show: false }
    };
    o.yaxis = { labels: { formatter: v => (horizontal ? v : fmt(v, format)), maxWidth: 260, style: { fontSize: "11px" } } };
    // Chừa lề trái để nhãn danh mục dài không bị cắt chữ đầu
    if (horizontal) o.grid.padding = { left: 18, right: 12 };
    o.grid.xaxis = { lines: { show: horizontal } };
    o.grid.yaxis = { lines: { show: !horizontal } };
    o.legend.show = false;
    o.tooltip.x = { formatter: (v, ctx) => (ctx && data[ctx.dataPointIndex] ? data[ctx.dataPointIndex][label] : v) };
    o.tooltip.y = { formatter: v => fmt(v, format), title: { formatter: () => (seriesName ? seriesName + ":" : "") } };
    if (onClick) {
      o.chart.events = { dataPointSelection: (e, ctx, cfg) => { const item = data[cfg.dataPointIndex]; if (item) onClick(item); } };
    }
    return mount(id, o);
  }

  // --------------------------------------------------------------------------
  // ĐƯỜNG / VÙNG - nhiều chuỗi cùng đơn vị (một trục y)
  //   series: [{ name, data: [number|null] }]
  // --------------------------------------------------------------------------
  function line(id, labels, series, opts = {}) {
    const { format = "number", height = 300, area = false, colorOffset = 0, onClick = null } = opts;
    if (!labels || !labels.length || !series || !series.length) return null;
    const t = theme();
    const o = base(t, area ? "area" : "line", height);
    o.series = series;
    o.colors = series.map((s, i) => s.color || t.colors[(i + colorOffset) % t.colors.length]);
    o.stroke = { width: 2, curve: "smooth" };
    o.markers = { size: labels.length <= 16 ? 4 : 0, strokeWidth: 2, strokeColors: t.surface, hover: { size: 6 } };
    if (onClick) {
      // Mốc lớn hơn để dễ bấm; bấm vào mốc trả về nhãn và vị trí của mốc
      o.markers.size = 6;
      o.chart.events = { click: (e, ctx, cfg) => { if (cfg.dataPointIndex >= 0) onClick(labels[cfg.dataPointIndex], cfg.dataPointIndex); } };
    }
    o.fill = area ? { type: "gradient", gradient: { opacityFrom: 0.28, opacityTo: 0.02 } } : { type: "solid" };
    o.xaxis = { categories: labels, labels: { rotate: -30, hideOverlappingLabels: true, style: { fontSize: "11px" } }, axisBorder: { show: false }, axisTicks: { show: false }, tooltip: { enabled: false } };
    o.yaxis = { labels: { formatter: v => fmt(v, format), style: { fontSize: "11px" } } };
    o.legend.show = series.length > 1;
    o.tooltip.shared = true;
    o.tooltip.intersect = false;
    o.tooltip.y = { formatter: v => (v === null || v === undefined ? "—" : fmt(v, format)) };
    return mount(id, o);
  }

  // --------------------------------------------------------------------------
  // TRÒN (donut) - tối đa maxSlices phần, phần còn lại gộp "Khác"
  // --------------------------------------------------------------------------
  function donut(id, items, opts = {}) {
    const { value = "value", label = "label", format = "number", maxSlices = 6, height = 300, colorOf = null, onClick = null } = opts;
    const rows = (items || []).filter(d => Number(d[value]) > 0).sort((a, b) => b[value] - a[value]);
    if (!rows.length) return null;
    const t = theme();
    let slices = rows;
    if (rows.length > maxSlices) {
      const head = rows.slice(0, maxSlices - 1);
      const rest = rows.slice(maxSlices - 1).reduce((acc, d) => acc + Number(d[value]), 0);
      slices = head.concat([{ [label]: "Khác", [value]: rest, isOther: true }]);
    }
    const total = slices.reduce((acc, d) => acc + Number(d[value]), 0);
    const o = base(t, "donut", height);
    o.series = slices.map(d => Number(d[value]));
    o.labels = slices.map(d => d[label]);
    o.colors = slices.map((d, i) => (d.isOther ? t.other : colorOf ? colorOf(d) : t.colors[i % t.colors.length]));
    o.stroke = { width: 2, colors: [t.surface] };
    o.legend = { position: "bottom", fontSize: "12px", labels: { colors: t.text }, markers: { size: 6 } };
    o.plotOptions = {
      pie: {
        donut: {
          size: "66%",
          labels: {
            show: true,
            value: { fontSize: "18px", fontWeight: 700, formatter: v => fmt(Number(v), format) },
            total: { show: true, label: "Tổng", color: t.muted, formatter: () => fmt(total, format) }
          }
        }
      }
    };
    o.tooltip.y = { formatter: v => `${fmt(v, format)} (${total > 0 ? ((v / total) * 100).toFixed(1) : 0}%)` };
    if (onClick) {
      o.chart.events = { dataPointSelection: (e, ctx, cfg) => { const item = slices[cfg.dataPointIndex]; if (item && !item.isOther) onClick(item); } };
    }
    return mount(id, o);
  }

  // --------------------------------------------------------------------------
  // DỰ BÁO - lịch sử (đường liền) + dự báo (đường đứt) + khoảng tin cậy
  //   points: [{ label, actual, forecast, lower, upper, isFuture }]
  // --------------------------------------------------------------------------
function forecast(id, points, opts = {}) {
  const { format = "usd", height = 340 } = opts;
  if (!points || !points.length) return null;

  const t = theme();
  const o = base(t, "line", height);

  o.series = [
    {
      name: "Doanh thu thực tế",
      data: points.map(point => ({
        x: point.label,
        y: point.actual ?? null
      }))
    },
    {
      name: "Kiểm định và dự báo Prophet",
      data: points.map(point => ({
        x: point.label,
        y: point.isFuture
          ? point.forecast
          : (point.backtest ?? null)
      }))
    }
  ];

  o.colors = [t.colors[0], "#EA580C"];
  o.stroke = {
    width: [2, 2],
    curve: "straight",
    dashArray: [0, 6]
  };
  o.markers = {
    size: 3,
    strokeWidth: 2,
    strokeColors: t.surface
  };
  o.xaxis = {
    type: "category",
    labels: { rotate: -30, hideOverlappingLabels: true },
    tooltip: { enabled: false }
  };
  o.yaxis = {
    labels: { formatter: value => fmt(value, format) }
  };

  const boundaries = [
    { month: "2025-07", text: "Cập nhật model" },
    { month: "2026-01", text: "Dự báo 2026" }
  ];

  o.annotations = {
    xaxis: boundaries
      .filter(boundary => points.some(point => point.label === boundary.month))
      .map(boundary => ({
        x: boundary.month,
        borderColor: "#94A3B8",
        strokeDashArray: 4,
        label: {
          text: boundary.text,
          style: { background: "#E2E8F0", color: "#334155" }
        }
      }))
  };

  o.legend.show = true;
  o.legend.showForNullSeries = false;
  o.tooltip.shared = true;
  o.tooltip.intersect = false;
  o.tooltip.x = {
    formatter: (value, context) => {
      const point = points[context.dataPointIndex];
      if (!point) return value;

      const cutoff = point.isFuture
        ? "2025-12-31"
        : point.backtestCutoff;

      return cutoff
        ? `${point.label} · Mốc chốt ${cutoff}`
        : point.label;
    }
  };
  o.tooltip.y = {
    formatter: value =>
      value === null || value === undefined
        ? "—"
        : fmt(value, format)
  };

  return mount(id, o);
}

  // --------------------------------------------------------------------------
  // CỘT NHÓM (Grouped Bar) - nhiều chuỗi theo danh mục
  //   categories: ["Quý 1", "Quý 2", "Quý 3", "Quý 4"]
  //   series: [{ name: "Kênh A", data: [10, 20, 30, 40] }]
  // --------------------------------------------------------------------------
  function groupedBar(id, categories, series, opts = {}) {
    const { format = "number", height = 280, horizontal = false } = opts;
    if (!categories || !categories.length || !series || !series.length) return null;
    const t = theme();
    const o = base(t, "bar", height);
    o.series = series;
    o.colors = series.map((s, i) => s.color || t.colors[i % t.colors.length]);
    o.plotOptions = {
      bar: {
        horizontal,
        borderRadius: 4,
        borderRadiusApplication: "end",
        columnWidth: "55%"
      }
    };
    o.xaxis = {
      categories,
      labels: { style: { fontSize: "11px", colors: t.text } },
      axisBorder: { show: false },
      axisTicks: { show: false }
    };
    o.yaxis = {
      labels: {
        formatter: v => fmt(v, format),
        style: { fontSize: "11px", colors: t.text }
      }
    };
    o.tooltip.shared = true;
    o.tooltip.intersect = false;
    o.tooltip.y = { formatter: v => (v === null || v === undefined ? "—" : fmt(v, format)) };
    o.legend = {
      show: true,
      position: "bottom",
      fontSize: "12px",
      labels: { colors: t.text },
      markers: { size: 6 }
    };
    return mount(id, o);
  }

  return { bar, line, donut, forecast, groupedBar, destroy, theme, mount };
})();
