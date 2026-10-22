/**
 * ==============================================================================
 * KHANG NGHI BUSINESS INTELLIGENCE - COPILOT RESPONSE RULES & MARKDOWN ENGINE
 * ==============================================================================
 * Module dùng chung quản lý quy tắc định dạng câu trả lời cho Chatbox AI Copilot:
 * - Chuẩn hóa thông điệp trạng thái (loading, empty, error).
 * - Trình phân tích & render Markdown an toàn (XSS-safe), hỗ trợ:
 *   + Headings (h2, h3), In đậm, In nghiêng, Trích dẫn (blockquote).
 *   + Danh sách có thứ tự (ol) và không có thứ tự (ul).
 *   + Bảng dữ liệu Markdown (gfm table) có cuộn ngang và căn chỉnh cột.
 *   + Khối mã lệnh (code block) với badge ngôn ngữ (mdx, sql, json...) và nút sao chép riêng.
 *   + Syntax highlighting nhẹ cho MDX và SQL.
 * - Nút sao chép toàn bộ nội dung câu trả lời.
 * - Định dạng số liệu BI (tiền tệ VNĐ, tỷ lệ %, phân cách hàng nghìn).
 */

(function (window) {
  "use strict";

  // 1. CÁC THÔNG ĐIỆP TRẠNG THÁI TIÊU CHUẨN
  const MESSAGES = {
    EMPTY_DATA: "Chưa có dữ liệu phù hợp với điều kiện truy vấn hiện tại.",
    CONNECTION_ERROR: "Không thể kết nối nguồn dữ liệu. Vui lòng kiểm tra kết nối hoặc thử lại.",
    LOADING: "Đang truy vấn dữ liệu…",
    UNAUTHORIZED: "Vai trò hiện tại chưa được cấp quyền sử dụng Trợ lý AI.",
    SOURCE_VERIFIED: "Nguồn: kho DWH_KhangNghi (đã xác thực)"
  };

  // 2. PHÒNG CHỐNG XSS: Chuyển đổi ký tự đặc biệt trước khi xử lý Markdown
  function escapeHtml(str) {
    if (str === null || str === undefined) return "";
    return String(str)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  // 3. SYNTAX HIGHLIGHTING CƠ BẢN CHO MDX & SQL
  function highlightCode(escapedCode, lang) {
    const l = (lang || "").toLowerCase();
    if (l !== "mdx" && l !== "sql") return escapedCode;

    // Từ khóa MDX & SQL
    const keywords = [
      "SELECT", "FROM", "WHERE", "ON COLUMNS", "ON ROWS", "NON EMPTY",
      "WITH MEMBER", "SET", "CREATE", "AS", "AND", "OR", "NOT",
      "ORDER BY", "GROUP BY", "HAVING", "JOIN", "LEFT JOIN", "INNER JOIN",
      "INSERT", "UPDATE", "DELETE", "TOP", "DESC", "ASC", "DISTINCT",
      "MEMBERS", "CHILDREN", "ALLMEMBERS", "PROPERTIES", "DIMENSION"
    ];

    let highlighted = escapedCode;

    // Tô màu chuỗi ký tự '...'
    highlighted = highlighted.replace(/'([^'\\]*(\\.[^'\\]*)*)'/g, '<span class="token-string">\'$1\'</span>');

    // Tô màu số
    highlighted = highlighted.replace(/\b(\d+(\.\d+)?)\b/g, '<span class="token-number">$1</span>');

    // Tô màu chú thích -- hoặc //
    highlighted = highlighted.replace(/(--.*$|\/\/.*$)/gm, '<span class="token-comment">$1</span>');

    // Tô màu từ khóa (case-insensitive nhưng hiển thị đúng)
    keywords.forEach(kw => {
      const reg = new RegExp(`\\b(${kw})\\b`, "gi");
      highlighted = highlighted.replace(reg, '<span class="token-keyword">$1</span>');
    });

    // Tô màu các identifier trong ngoặc vuông [Dim Date], [Measures]
    highlighted = highlighted.replace(/(\[[^\]]+\])/g, '<span class="token-dimension">$1</span>');

    return highlighted;
  }

  // 4. PARSER BẢNG DỮ LIỆU MARKDOWN
  function parseMarkdownTable(tableLines) {
    if (tableLines.length < 2) return tableLines.join("\n");

    const headerLine = tableLines[0];
    const alignLine = tableLines[1];
    const rowLines = tableLines.slice(2);

    // Kiểm tra dòng phân cách có dạng |---|---|
    if (!/^\s*\|?([\s:]*-+[\s:]*\|?)+\s*$/.test(alignLine)) {
      return tableLines.join("\n");
    }

    const splitCells = line =>
      line.trim()
        .replace(/^\|/, "")
        .replace(/\|$/, "")
        .split("|")
        .map(c => c.trim());

    const headers = splitCells(headerLine);
    const aligns = splitCells(alignLine).map(a => {
      const left = a.startsWith(":");
      const right = a.endsWith(":");
      if (left && right) return "center";
      if (right) return "right";
      return "left";
    });

    let html = '<div class="chat-table-wrapper"><table class="chat-markdown-table"><thead><tr>';
    headers.forEach((h, i) => {
      const align = aligns[i] || "left";
      html += `<th style="text-align: ${align}">${parseInline(h)}</th>`;
    });
    html += "</tr></thead><tbody>";

    rowLines.forEach(r => {
      if (!r.trim()) return;
      const cells = splitCells(r);
      html += "<tr>";
      headers.forEach((_, i) => {
        const val = cells[i] !== undefined ? cells[i] : "";
        const align = aligns[i] || "left";
        html += `<td style="text-align: ${align}">${parseInline(val)}</td>`;
      });
      html += "</tr>";
    });

    html += "</tbody></table></div>";
    return html;
  }

  // 5. PARSER CÁC THẺ INLINE (BOLD, ITALIC, CODE, LINK)
  function parseInline(text) {
    return text
      // Bold + Italic: ***text***
      .replace(/\*\*\*(.*?)\*\*\*/g, "<strong><em>$1</em></strong>")
      // Bold: **text** hoặc __text__
      .replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
      .replace(/__(.*?)__/g, "<strong>$1</strong>")
      // Italic: *text* hoặc _text_
      .replace(/\*([^*]+)\*/g, "<em>$1</em>")
      .replace(/_([^_]+)_/g, "<em>$1</em>")
      // Inline code: `code`
      .replace(/`([^`]+)`/g, '<code class="chat-inline-code">$1</code>');
  }

  // 6. TRÌNH RENDER MARKDOWN AN TOÀN TOÀN DIỆN
  function renderMarkdown(rawMarkdown) {
    if (!rawMarkdown) return "";

    // Bước 1: Bảo vệ an toàn chống XSS bằng cách Escape toàn bộ HTML nguyên bản
    let safe = escapeHtml(rawMarkdown);

    // Bước 2: Tách và bảo lưu các Code Blocks trước khi xử lý các phần tử khác
    const codeBlocks = [];
    safe = safe.replace(/```([a-zA-Z0-9_-]*)\n([\s\S]*?)```/g, (match, lang, code) => {
      const token = `__CODE_BLOCK_${codeBlocks.length}__`;
      const language = (lang || "code").trim();
      const cleanCode = code.replace(/\r\n/g, "\n").trim();
      const highlighted = highlightCode(cleanCode, language);

      const blockHtml = `
<div class="chat-code-block">
  <div class="code-header">
    <span class="code-lang"><i class="fa-solid fa-code"></i> ${language.toUpperCase()}</span>
    <button type="button" class="btn-copy-code" onclick="CopilotResponseRules.copyCode(this)" title="Sao chép đoạn mã">
      <i class="fa-regular fa-copy"></i> Sao chép
    </button>
  </div>
  <pre><code class="language-${language}">${highlighted}</code></pre>
</div>`.trim();

      codeBlocks.push(blockHtml);
      return `\n\n${token}\n\n`;
    });

    // Bước 3: Phân tích theo từng khối dòng (Line Blocks)
    const lines = safe.split(/\r?\n/);
    const out = [];
    let inList = null; // 'ul' hoặc 'ol'
    let tableBuffer = [];

    function closeList() {
      if (inList) {
        out.push(`</${inList}>`);
        inList = null;
      }
    }

    function flushTable() {
      if (tableBuffer.length > 0) {
        out.push(parseMarkdownTable(tableBuffer));
        tableBuffer = [];
      }
    }

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const trimmed = line.trim();

      // Kiểm tra token code block
      if (/^__CODE_BLOCK_\d+__$/.test(trimmed)) {
        closeList();
        flushTable();
        out.push(trimmed);
        continue;
      }

      // Kiểm tra dòng bảng Markdown (| ... |)
      if (trimmed.startsWith("|") && trimmed.endsWith("|") && trimmed.length > 2) {
        closeList();
        tableBuffer.push(trimmed);
        continue;
      } else {
        flushTable();
      }

      // Dòng trống
      if (!trimmed) {
        closeList();
        continue;
      }

      // Tiêu đề: ### Heading 3, ## Heading 2, # Heading 1
      if (/^###\s+(.+)$/.test(trimmed)) {
        closeList();
        const content = trimmed.replace(/^###\s+/, "");
        out.push(`<h3 class="chat-heading">${parseInline(content)}</h3>`);
        continue;
      }
      if (/^##\s+(.+)$/.test(trimmed)) {
        closeList();
        const content = trimmed.replace(/^##\s+/, "");
        out.push(`<h2 class="chat-heading">${parseInline(content)}</h2>`);
        continue;
      }
      if (/^#\s+(.+)$/.test(trimmed)) {
        closeList();
        const content = trimmed.replace(/^#\s+/, "");
        out.push(`<h2 class="chat-heading chat-heading-lg">${parseInline(content)}</h2>`);
        continue;
      }

      // Blockquote: > Trích dẫn
      if (/^>\s+(.+)$/.test(trimmed)) {
        closeList();
        const quote = trimmed.replace(/^>\s+/, "");
        out.push(`<blockquote class="chat-blockquote">${parseInline(quote)}</blockquote>`);
        continue;
      }

      // Unordered list: - item hoặc * item
      if (/^[-*]\s+(.+)$/.test(trimmed)) {
        if (inList !== "ul") {
          closeList();
          out.push('<ul class="chat-list">');
          inList = "ul";
        }
        const item = trimmed.replace(/^[-*]\s+/, "");
        out.push(`<li>${parseInline(item)}</li>`);
        continue;
      }

      // Ordered list: 1. item, 2. item
      if (/^\d+\.\s+(.+)$/.test(trimmed)) {
        if (inList !== "ol") {
          closeList();
          out.push('<ol class="chat-list is-ordered">');
          inList = "ol";
        }
        const item = trimmed.replace(/^\d+\.\s+/, "");
        out.push(`<li>${parseInline(item)}</li>`);
        continue;
      }

      // Đoạn văn thông thường
      closeList();
      out.push(`<p class="chat-paragraph">${parseInline(trimmed)}</p>`);
    }

    closeList();
    flushTable();

    // Bước 4: Khôi phục lại các Code Blocks
    let finalHtml = out.join("\n");
    codeBlocks.forEach((block, idx) => {
      finalHtml = finalHtml.replace(`__CODE_BLOCK_${idx}__`, block);
    });

    return finalHtml;
  }

  // 7. TẠO TOÀN BỘ BUBBLE TIN NHẮN CHO AI (GỒM NỘI DUNG + THANH THAO TÁC COPY)
  function createBotBubbleHtml(markdownAnswer, sourceInfo) {
    const rendered = renderMarkdown(markdownAnswer);
    const sourceHtml = sourceInfo
      ? `<div class="chat-source"><i class="fa-solid fa-database"></i> ${escapeHtml(sourceInfo)}</div>`
      : `<div class="chat-source"><i class="fa-solid fa-robot"></i> Khang Nghi AI Copilot (DWH_KhangNghi)</div>`;

    return `
<div class="chat-bubble-content">${rendered}</div>
${sourceHtml}
<div class="chat-actions">
  <button type="button" class="btn-chat-action" onclick="CopilotResponseRules.copyMessage(this)" title="Sao chép toàn bộ câu trả lời">
    <i class="fa-regular fa-copy"></i> Sao chép
  </button>
</div>`.trim();
  }

  // 8. CÁC HÀM TIỆN ÍCH SAO CHÉP (CLIPBOARD)
  function copyToClipboard(text, btnElement, successText = "Đã sao chép!") {
    if (!navigator.clipboard) {
      // Fallback cho trình duyệt cũ
      const textarea = document.createElement("textarea");
      textarea.value = text;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.select();
      try {
        document.execCommand("copy");
        showCopyFeedback(btnElement, successText);
      } catch (err) {
        console.error("Không thể sao chép:", err);
      }
      document.body.removeChild(textarea);
      return;
    }

    navigator.clipboard.writeText(text).then(() => {
      showCopyFeedback(btnElement, successText);
    }).catch(err => {
      console.error("Lỗi clipboard:", err);
    });
  }

  function showCopyFeedback(btn, text) {
    if (!btn) return;
    const oldHtml = btn.innerHTML;
    btn.innerHTML = `<i class="fa-solid fa-check"></i> ${text}`;
    btn.classList.add("is-copied");
    btn.disabled = true;
    setTimeout(() => {
      btn.innerHTML = oldHtml;
      btn.classList.remove("is-copied");
      btn.disabled = false;
    }, 2000);
  }

  function copyCode(btn) {
    const codeBlock = btn.closest(".chat-code-block");
    if (!codeBlock) return;
    const codeEl = codeBlock.querySelector("code");
    if (codeEl) copyToClipboard(codeEl.innerText, btn);
  }

  function copyMessage(btn) {
    const bubble = btn.closest(".chat-bubble");
    if (!bubble) return;
    const contentEl = bubble.querySelector(".chat-bubble-content") || bubble;
    copyToClipboard(contentEl.innerText, btn);
  }

  // 9. CÁC HÀM HIỂN THỊ TRẠNG THÁI GIAO DIỆN
  function renderLoading(text) {
    const msg = text || MESSAGES.LOADING;
    return `
<div class="chat-loading-row">
  <span class="chat-typing-dots">
    <span></span><span></span><span></span>
  </span>
  <span class="chat-loading-text">${escapeHtml(msg)}</span>
</div>`.trim();
  }

  function renderError(errorMessage) {
    const msg = errorMessage || MESSAGES.CONNECTION_ERROR;
    return `
<div class="chat-alert is-danger">
  <i class="fa-solid fa-circle-exclamation"></i>
  <div class="chat-alert-content">
    <strong>Không thể thực hiện</strong>
    <p>${escapeHtml(msg)}</p>
  </div>
</div>`.trim();
  }

  function renderEmpty(customMessage) {
    const msg = customMessage || MESSAGES.EMPTY_DATA;
    return `
<div class="chat-alert is-warning">
  <i class="fa-solid fa-circle-info"></i>
  <div class="chat-alert-content">
    <p>${escapeHtml(msg)}</p>
  </div>
</div>`.trim();
  }

  // Xuất module ra phạm vi toàn cục
  window.CopilotResponseRules = {
    MESSAGES,
    escapeHtml,
    renderMarkdown,
    createBotBubbleHtml,
    copyCode,
    copyMessage,
    copyToClipboard,
    renderLoading,
    renderError,
    renderEmpty
  };

})(window);
