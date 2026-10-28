# BÁO CÁO AUDIT AI COPILOT — GEMINI, TIMEOUT, FALLBACK OFFLINE VÀ THỨ TỰ HỘI THOẠI

- **Ngày:** 28/10/2026
- **Bản mã nguồn:** `KhoaLuanTotNghiep/KhangNghi_DataWarehouse_Git` (đang chạy trong container `kn-api`)
- **Phạm vi:** chỉ đọc mã nguồn và log, **chưa sửa code**, **chưa gọi Gemini** trong bước audit.
- **Trạng thái:** chờ duyệt kế hoạch trước khi sửa.

---

## 1. Luồng xử lý hiện tại

```
Trình duyệt (app.js askCopilot)
  └─ GET /api/v1/nlq/query?q=...     timeout trình duyệt 60 s (apiClient.js)
       └─ NlqController.Query
            1. QuestionIntentParser.Parse(q)        → năm ngoài 2022–2025: trả lời ngay
            2. Nếu có API key: LlmCopilotService.AskAsync
                 vòng tối đa 4 lượt (MaxToolRounds):
                   CompleteAsync → POST {GEMINI_BASE_URL}/chat/completions   (HttpClient.Timeout 60 s / lượt)
                   Gemini gọi tool query_bi_warehouse → BiQueryCatalog → DwhAnalyticsRepository (SQL DWH)
                 lỗi bất kỳ → ghi log, rơi xuống bước 3
            3. Fallback offline: chuỗi if/else theo intent và từ khóa → truy vấn repository → Source = "fallback"
```

**Cách xác định nguồn trả lời:** trường `Source` của phản hồi:
- `llm`: Gemini trả lời.
- `fallback`: chế độ offline.
- `intent_guard`: chặn năm ngoài phạm vi.

---

## 2. Kết quả kiểm tra thực tế (trước khi sửa)

Hỏi qua giao diện Web BI bằng tài khoản `giamdoc`. Model đang cấu hình: `models/gemini-3.8-flash`.

| Câu hỏi | Thời gian | Nguồn | Kết quả |
|---|---|---|---|
| doanh thu năm 2022 | 4 s | fallback | Đúng: 38.081.049.190 ₫ |
| top 5 sản phẩm doanh thu cao nhất | 60 s | — | Giao diện báo **"Request Timeout (408)"** |
| sản phẩm có doanh thu thấp nhất | — | fallback | **Sai**: "Top 10 sản phẩm bán chạy mang lại doanh thu cao nhất" |
| nhóm sản phẩm nào có lợi nhuận cao nhất | — | fallback | **Sai**: trả về 1 sản phẩm (Biến tần LS H100…), không phải nhóm |

**Log server trong cùng khoảng thời gian:**
- Gemini trả **HTTP 503** "This model is currently experiencing high demand".
- Gemini **timeout 60 s** (`HttpClient.Timeout of 60 seconds elapsing`).
- Gemini trả **HTTP 429** "You exceeded your current quota".

> Câu 2 hết thời gian chờ nên câu trả lời của nó về muộn. Vì vậy các câu trả lời hiển thị sau đó có thể bị lệch so với câu hỏi (xem mục 3.4). Lỗi sai ở câu 3 và 4 được xác nhận độc lập qua đọc mã nguồn (mục 3.3).

---

## 3. Nguyên nhân gốc

### 3.1. Gemini (Nhóm A)

| # | Vấn đề | Vị trí |
|---|---|---|
| A1 | Model `gemini-3.8-flash` đang bị Google từ chối: 503 (quá tải) và 429 (hết quota của key). Mới chỉ xác minh được model **có trong danh sách** `ListModels` của key; **chưa** xác minh gọi được và Function Calling hoạt động. | `.env` → `GEMINI_MODEL` |
| A2 | Model mặc định bị viết cứng ở **2 nơi khác nhau**: `"models/gemini-3.5-flash"` trong Program.cs và `"models/gemini-3.5-flash-lite"` trong LlmOptions. `.env.example` còn ghi `gemini-1.5-flash` đã cũ. | [Program.cs:39](KLTN/src/KhangNghi.Api/Program.cs#L39), [LlmCopilotService.cs:25](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/LlmCopilotService.cs#L25), `.env.example:16` |
| A3 | Mọi lỗi đều bị gộp thành một `InvalidOperationException`, không phân biệt sai key (401/403), hết quota (429), quá tải (503), timeout hay lỗi mạng. Log và giao diện không cho biết lý do. | [LlmCopilotService.cs:339-343](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/LlmCopilotService.cs#L339-L343) |
| A4 | Không có retry. Đây là điều **đúng** với 429. Với 503 có thể cân nhắc 1 lần retry ngắn nếu còn thời gian. | — |

### 3.2. Timeout và fallback (Nhóm A + C)

| # | Vấn đề | Vị trí |
|---|---|---|
| B1 | Timeout Gemini **60 s cho mỗi lượt gọi**, và vòng Function Calling có thể chạy tới **4 lượt**. Như vậy server có thể chờ Gemini **tới khoảng 240 s**. Không có giới hạn thời gian tổng cho cả vòng. | [Program.cs:41,51](KLTN/src/KhangNghi.Api/Program.cs#L41-L51), [LlmCopilotService.cs:116](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/LlmCopilotService.cs#L116) |
| B2 | Trình duyệt chỉ chờ **60 s**, bằng đúng timeout một lượt Gemini. Khi Gemini hết 60 s, server mới bắt đầu chạy offline thì trình duyệt đã hủy yêu cầu và báo 408. Người dùng không nhận được câu trả lời offline. | [app.js askCopilot](KLTN/src/KhangNghi.Api/wwwroot/js/app.js), `apiClient.js:66` |
| B3 | Khi trình duyệt hủy, `RequestAborted` dừng server, nên server không chạy tiếp vô ích. Đây là điểm tốt cần giữ. | [NlqController.cs:65-68](KLTN/src/KhangNghi.Api/Controllers/NlqController.cs#L65-L68) |

### 3.3. Độ chính xác của chế độ offline (Nhóm B)

| # | Vấn đề | Vị trí |
|---|---|---|
| C1 | **Không có khái niệm "thấp nhất".** Parser chỉ nhận "cao nhất / lớn nhất / nhiều nhất…", không có thứ tự tăng/giảm (Operator). | [QuestionIntentParser.cs:139](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs#L139) |
| C2 | **Mọi câu hỏi có chữ "sản phẩm"** đều đi vào Q06 (doanh thu giảm dần) hoặc Q07 (lợi nhuận giảm dần), mặc định 10 dòng. | [NlqController.cs:193-212](KLTN/src/KhangNghi.Api/Controllers/NlqController.cs#L193-L212) |
| C3 | **Không có thực thể "nhóm sản phẩm".** "Nhóm sản phẩm…" chứa chữ "sản phẩm" nên bị coi là `product`. Truy vấn theo nhóm **đã có sẵn** là `Q08_RevenueByCategoryAsync`, gồm Revenue, Profit, Quantity, ProfitMargin theo `Dim_Product.Category`, nhưng chưa được dùng. | [QuestionIntentParser.cs:113,155](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs#L113) |
| C4 | **Không có truy vấn "sản phẩm doanh thu thấp nhất"** trong repository và `BiQueryCatalog`. Chế độ Gemini cũng không có truy vấn đúng để gọi. | `DwhAnalyticsRepository.cs`, [BiQueryCatalog.cs](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/BiQueryCatalog.cs) |
| C5 | Prompt gửi Gemini ép "theo thứ tự giảm dần" cho mọi câu Top N, nên Gemini sẽ trả sai câu hỏi "thấp nhất". | [CopilotResponseRules.cs:43,99](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/CopilotResponseRules.cs#L43) |
| C6 | Chữ "lãi" bắt cả từ ghép khác (ví dụ "lãi suất"), có thể nhận nhầm là lợi nhuận. Mức độ thấp. | [QuestionIntentParser.cs:105](KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs#L105) |

**Số liệu đối chiếu (DWH, đã xác minh qua trang Câu hỏi phân tích MDX-08 và báo cáo audit trước):**
- `Dim_Product` có 60 sản phẩm, trong đó **57 sản phẩm có phát sinh bán** và 3 sản phẩm chưa bán lần nào.
- Sản phẩm doanh thu thấp nhất trong số đã bán: **Đầu cos đồng 6 mm2 [SIM-ACC-001]**, 9.900.900 ₫.
- Nhóm doanh thu cao nhất: **Biến tần** (120,51 tỷ ₫). Nhóm *lợi nhuận* cao nhất sẽ được đối chiếu bằng `Q08` khi kiểm thử.

### 3.4. Thứ tự hội thoại (Nhóm C)

| # | Vấn đề | Vị trí |
|---|---|---|
| D1 | Mỗi câu hỏi tạo một ô "đang tải", nhưng khi có kết quả thì ô đó bị **xóa** và câu trả lời được **thêm xuống cuối khung chat** (`loading.remove()` rồi `chatAppend`). Câu hỏi nào xong trước sẽ hiện trước, nên có thể nằm dưới câu hỏi khác. | [app.js askCopilot](KLTN/src/KhangNghi.Api/wwwroot/js/app.js) |
| D2 | Không có mã định danh cho từng yêu cầu, và giao diện không cho biết câu trả lời đến từ Gemini hay offline (trường `Source` có trả về nhưng chỉ dùng để chọn cách hiển thị). | như trên |

---

## 4. Danh sách file theo nhóm

| Nhóm | File | Ghi chú |
|---|---|---|
| **A — Gemini và timeout** | `Program.cs` (đăng ký HttpClient, đọc cấu hình) | |
| | `Services/Copilot/LlmCopilotService.cs` (gọi Gemini, vòng tool, phân loại lỗi) | |
| | `.env.example` (tài liệu biến cấu hình, **không chứa key**) | |
| | `Controllers/NlqController.cs`: chỉ khối `try/catch` gọi LLM (dòng 59-72) | **dùng chung với B** |
| **B — Logic offline** | `Services/Copilot/QuestionIntentParser.cs` | Antigravity đã lên kế hoạch sửa (đợt 2) |
| | `Controllers/NlqController.cs`: các nhánh fallback (dòng 104-268) | **dùng chung với A** |
| | `Services/Copilot/BiQueryCatalog.cs` (thêm truy vấn "thấp nhất", nhóm) | |
| | `Services/Copilot/CopilotResponseRules.cs` (bỏ ép "giảm dần") | |
| | `Repositories/DwhAnalyticsRepository.cs` (thêm 1 truy vấn sản phẩm doanh thu tăng dần) | |
| **C — Frontend** | `wwwroot/js/app.js` (hàm `askCopilot`) | |
| | `wwwroot/js/apiClient.js` (timeout yêu cầu) | |

**Rủi ro xung đột:**
- `NlqController.cs` và `QuestionIntentParser.cs` nằm trong kế hoạch "đợt 2" của Antigravity (`BAO_CAO_FIX_AI_PRODUCT_INTENT.md`, trạng thái *chờ phê duyệt*).
- Chỉ **một** bên được sửa Nhóm B. Bên còn lại không đụng các file này cho tới khi xong.
- Mọi chỉnh sửa dùng thay thế từng đoạn có kiểm tra, **không ghi đè cả file**.

---

## 5. Kế hoạch sửa

### Giai đoạn A — Gemini

1. **Xác minh model bằng một phép thử nhỏ có kiểm soát** (cần bạn cho phép).
   - Tối đa **2 lượt gọi**:
     - Một câu ngắn để kiểm tra model trả lời được.
     - Một câu có Function Calling (tool `query_bi_warehouse`) để kiểm tra model gọi được công cụ.
   - Thử lần lượt `gemini-3.8-flash` → `gemini-3.5-flash-lite` → `gemini-3.5-flash`, **dừng ở model đầu tiên đạt**.
   - Chỉ thử các model có trong `ListModels` của key.
   - Key được đọc trong container và **không in ra màn hình hay log**.
2. **Model ID chỉ cấu hình một nơi**: biến `GEMINI_MODEL`. Giá trị mặc định chỉ khai báo trong `LlmOptions`, Program.cs đọc từ đó. Cập nhật `.env.example`.
3. **Phân loại lỗi** bằng một exception riêng có loại lỗi: `Auth` (401/403), `Quota` (429), `Overloaded` (503), `Timeout`, `Network`, `BadResponse`. Log ghi loại lỗi và mã HTTP, **không ghi key**.
4. **Retry:**
   - 429 và lỗi xác thực: **không retry**.
   - 503: tối đa **1** lần, chờ 1 giây, chỉ khi còn ≥ 8 giây trong ngân sách thời gian.
   - Cấu hình bằng `GEMINI_MAX_RETRIES` (mặc định 1).

### Giai đoạn B — Timeout và fallback

1. `GEMINI_TIMEOUT_SECONDS` mặc định **20**, là **tổng thời gian cho cả vòng Function Calling**, không phải cho từng lượt.
   - Dùng `CancellationTokenSource` riêng có hạn 20 s, liên kết với `RequestAborted`.
   - Hết hạn thì hủy yêu cầu đang chờ Gemini.
2. Khi Gemini lỗi hoặc hết thời gian:
   - Chạy offline ngay.
   - Phản hồi thêm `Source = "fallback"` và `FallbackReason` (ví dụ `quota`, `overloaded`, `timeout`) để giao diện hiện lý do.
3. Thời gian chờ của trình duyệt cho `/nlq/query` đặt **45 s**, lớn hơn 20 s chờ Gemini cộng thời gian truy vấn DWH. Truy vấn offline hiện chạy dưới 1 giây.
4. Offline không hiểu câu hỏi thì trả thông báo rõ ràng kèm câu hỏi gợi ý, **không tạo số liệu giả**. Giữ nguyên thông báo hiện có.

### Giai đoạn C — Độ chính xác offline (chỉ làm khi đã thống nhất với Antigravity)

1. **Bổ sung vào `QuestionIntent`:**
   - `Entity`: Product | ProductGroup | Customer | Supplier | Region…
   - `Order`: Highest | Lowest
   - Giữ nguyên Metrics, Limit, Years.
   - Từ khóa cho `Lowest`: "thấp nhất", "ít nhất", "nhỏ nhất", "kém nhất", "bán chậm nhất".
   - Từ khóa cho `ProductGroup`: "nhóm sản phẩm", "nhóm hàng", "danh mục", "ngành hàng". Kiểm tra trước "sản phẩm".
   - Số ít không có số lượng (ví dụ "sản phẩm có doanh thu thấp nhất") thì Limit = 1.
2. **Truy vấn:**
   - **Mới:** `GetProductsByRevenueAscAsync(top)`, cùng SQL với Q06 nhưng `ORDER BY Revenue ASC`. Định nghĩa thống nhất với MDX-08 trên trang Câu hỏi phân tích: chỉ xét sản phẩm **có phát sinh bán**. Đăng ký vào `BiQueryCatalog` với mã `Q06_LOW` (domain Sales) để Gemini cũng gọi được.
   - **Nhóm sản phẩm:** dùng `Q08` có sẵn, xếp theo đúng chỉ số được hỏi (Revenue / Profit / ProfitMargin), lấy Limit dòng đầu.
3. **Ánh xạ:**

   | Quy tắc | Câu hỏi | Truy vấn | Thứ tự | Số dòng |
   |---|---|---|---|---|
   | A | Sản phẩm doanh thu thấp nhất | `Q06_LOW` | tăng | 1 |
   | B | Top 5 sản phẩm doanh thu cao nhất | `Q06` | giảm | 5 |
   | C | Nhóm sản phẩm lợi nhuận cao nhất | `Q08` | giảm theo Profit | 1 |
   | D | Sản phẩm lợi nhuận cao nhất | `Q07` | giảm | 1 |
   | E | So sánh / doanh thu các năm | `Q01` (không lọc năm) | | |
   | F | Doanh thu năm 2022 | `Q01(year=2022)` | | chỉ cột Revenue (đã đúng) |

4. Sửa prompt ở `CopilotResponseRules.cs`: thứ tự theo `Order` của intent, không ép "giảm dần".

### Giai đoạn D — Thứ tự hội thoại

1. Mỗi lần gửi tạo `requestId`. Ô "đang tải" của câu hỏi được **thay bằng câu trả lời ngay tại chỗ** (`replaceWith`), không thêm xuống cuối. Câu trả lời luôn nằm ngay dưới câu hỏi của nó, dù về sớm hay muộn.
2. Gửi nhiều câu liên tiếp vẫn chạy song song, không bắt chờ tuần tự.
3. Dưới câu trả lời hiện nhãn nguồn:
   - "Gemini · truy vấn Q06".
   - "Chế độ offline · Gemini đang quá tải / hết quota / quá thời gian".
4. Lỗi 408/5xx hiện thông báo kèm gợi ý thử lại.

---

## 6. Kế hoạch kiểm thử

Mỗi bài ghi lại:
- câu hỏi
- intent nhận diện
- query ID
- tham số
- kết quả
- thời gian
- nguồn (Gemini/offline)
- PASS/FAIL, có **đối chiếu số liệu bằng SQL trực tiếp trên `DWH_KhangNghi`**

| # | Bài | Cách tạo tình huống |
|---|---|---|
| 1–6 | 6 câu nghiệp vụ (mục 5.C.3) | Chế độ offline: đặt tạm `GEMINI_API_KEY` rỗng trong môi trường test, không gọi Google. Chế độ Gemini: chỉ chạy nếu model được xác minh ở Giai đoạn A, tối đa 6 lượt. |
| 7 | Gemini 429 | Trỏ `GEMINI_BASE_URL` tạm tới một server giả lập cục bộ trả 429 (**không gọi Google**) |
| 8 | Gemini 503 | Server giả lập trả 503, kiểm tra retry đúng 1 lần |
| 9 | Gemini timeout | Server giả lập treo 30 s, kiểm tra fallback trong khoảng 20 s |
| 10 | Key sai | Server giả lập trả 401 |
| 11 | Truy vấn không có dữ liệu | Câu hỏi năm 2026 và câu hỏi ngoài dữ liệu |
| 12 | 2 câu hỏi về ngược thứ tự | Trình duyệt ảo: câu 1 chậm (server giả lập trễ), câu 2 nhanh; kiểm tra vị trí câu trả lời |
| 13 | Không có quyền | Tài khoản `kho` hỏi doanh thu, phải bị từ chối |
| 14 | Ngoài phạm vi | "thời tiết hôm nay", phải có thông báo và gợi ý, không có số liệu |

Kết quả ghi vào `BAO_CAO_FIX_AI_COPILOT.md`.

---

## 7. Cần quyết định trước khi sửa

1. **Cho phép phép thử Gemini có kiểm soát** ở Giai đoạn A, tối đa khoảng 8 lượt gọi tính cả kiểm thử 1–6? Nếu không, Giai đoạn A chỉ dừng ở cấu hình và phân loại lỗi, model vẫn ở trạng thái "chưa xác minh".
2. **Ai sửa Nhóm B** (`QuestionIntentParser.cs`, nhánh fallback của `NlqController.cs`): Claude hay Antigravity? Bên còn lại tạm dừng các file này.
3. **Định nghĩa "sản phẩm doanh thu thấp nhất":** mặc định đề xuất là *trong số sản phẩm có phát sinh bán*, giống MDX-08. Nếu muốn tính cả 3 sản phẩm chưa bán (doanh thu 0) thì câu trả lời sẽ khác.
