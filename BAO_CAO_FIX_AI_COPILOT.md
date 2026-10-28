# BÁO CÁO KHẮC PHỤC AI COPILOT — GEMINI, TIMEOUT, ĐỘ CHÍNH XÁC OFFLINE VÀ THỨ TỰ HỘI THOẠI

- **Ngày:** 28/10/2026
- **Căn cứ:** [BAO_CAO_AUDIT_AI_COPILOT.md](BAO_CAO_AUDIT_AI_COPILOT.md)
- **Trạng thái:** đã sửa và **đã kiểm thử thực tế**: 14/14 bài đạt ở chế độ offline và với server giả lập lỗi; 3/3 câu đạt với Gemini thật.
- **Sao lưu trước khi sửa:** `/tmp/claude-501/kn/copilot_193825/files.tar`

---

## 1. Model Gemini đã xác minh

Gọi Google thật **khoảng 9 lượt** (mỗi câu hỏi Function Calling tốn 2 lượt):

| Model | Có trong ListModels của key | Gọi thử | Kết luận |
|---|---|---|---|
| `models/gemini-3.8-flash` | Có | **HTTP 429** "You exceeded your current quota" | Không dùng được với key hiện tại (hết hạn mức) |
| `models/gemini-3.5-flash-lite` | Có | **Thành công**: trả lời 5–6 s, Function Calling gọi đúng `Q06`, `Q06_LOW`, `Q08` | **Đang dùng** |
| `models/gemini-3.5-flash` | Có | Không thử (đã có model đạt) | Lúc 14:37 từng bị 503 quá tải |

**Cấu hình hiện tại** trong `.env`, file nằm trong `.gitignore` nên **không commit**:
- `GEMINI_MODEL=models/gemini-3.5-flash-lite`
- `GEMINI_TIMEOUT_SECONDS=20`
- `GEMINI_MAX_RETRIES` không đặt (mặc định 1)

**Bảo mật API key:**
- API key chỉ nằm trong `.env` phía backend.
- Không in ra log hay gửi xuống trình duyệt.
- Không xuất hiện trong `.env.example`, đã kiểm tra không có chuỗi `AIza…`.

---

## 2. Nguyên nhân và cách sửa

| # | Nguyên nhân | Cách sửa | File |
|---|---|---|---|
| 1 | **Lỗi có sẵn:** khi Gemini trả về lệnh gọi công cụ, code gắn nguyên node JSON đang thuộc cây gốc vào hội thoại, gây lỗi `The node already has a parent`. Vì vậy **chế độ Gemini có Function Calling chưa từng chạy được**: mọi câu hỏi cần truy vấn đều rơi về offline. | Tách bản sao (`DeepClone`) trước khi đưa vào hội thoại | `LlmCopilotService.cs` |
| 2 | Model mới `3.8-flash` hết quota với key hiện tại | Chuyển sang `3.5-flash-lite` sau khi gọi thử thành công | `.env` |
| 3 | Timeout 60 s **mỗi lượt**, tối đa 4 lượt, nên có thể chờ tới khoảng 240 s; trình duyệt chỉ chờ 60 s | Ngân sách **tổng 20 s** cho cả vòng (`CancellationTokenSource` liên kết với `RequestAborted`). Trình duyệt chờ **45 s**. | `LlmCopilotService.cs`, `Program.cs`, `app.js` |
| 4 | Mọi lỗi Gemini bị gộp làm một | Thêm `LlmException` gồm các loại `Auth / Quota / Overloaded / Timeout / Network / BadResponse`. Log ghi loại lỗi, mã HTTP và thời gian. Phản hồi có `FallbackReason`. | `LlmCopilotService.cs`, `NlqController.cs` |
| 5 | Không có chính sách retry | 503: retry **tối đa 1 lần**, chờ 1 s, chỉ khi còn ≥ 8 s. 429/401: **không retry**. Cấu hình bằng `GEMINI_MAX_RETRIES`. | `LlmCopilotService.cs`, `Program.cs` |
| 6 | Model mặc định viết cứng ở 2 nơi với 2 giá trị khác nhau | Chỉ còn hằng `LlmOptions.DefaultModel / DefaultBaseUrl / DefaultTimeoutSeconds`, Program.cs đọc từ đó | `LlmCopilotService.cs`, `Program.cs`, `.env.example` |
| 7 | Không có khái niệm "thấp nhất" | Thêm `QuestionIntent.Order` (Highest/Lowest), nhận các từ "thấp nhất / ít nhất / nhỏ nhất / kém nhất / chậm nhất…" | `QuestionIntentParser.cs` |
| 8 | "Nhóm sản phẩm" bị hiểu là "sản phẩm" | Thực thể `product_group` (nhóm sản phẩm / nhóm hàng / danh mục / ngành hàng / loại hàng) được nhận **trước** "sản phẩm". Dùng `Q08` có sẵn, xếp theo đúng chỉ số được hỏi. | `QuestionIntentParser.cs`, `NlqController.cs` |
| 9 | Chưa có truy vấn sản phẩm doanh thu/lợi nhuận thấp nhất | Thêm `GetLowestProductsAsync(top, byProfit)` (2 câu SQL cố định, `ORDER BY … ASC`, chỉ sản phẩm đã phát sinh bán, giống MDX-08). Đăng ký `Q06_LOW`, `Q07_LOW` vào `BiQueryCatalog`. | `DwhAnalyticsRepository.cs`, `BiQueryCatalog.cs` |
| 10 | Hỏi số ít không có "top N" lại trả 10 dòng | Số ít ("sản phẩm…", "nhóm sản phẩm…", "khách hàng…") thì Limit = 1 | `QuestionIntentParser.cs` |
| 11 | Prompt ép "giảm dần" cho mọi câu Top N | Thứ tự theo `Order`, nêu rõ nhóm là Category (Q08) | `CopilotResponseRules.cs` |
| 12 | Gemini có thể chọn nhầm truy vấn | Backend tự đổi truy vấn theo ý định đã phân tích (Q06 thành Q06_LOW khi hỏi thấp nhất, Q06/Q07 thành Q08 khi hỏi nhóm), xếp lại Q08 theo chỉ số trước khi cắt Top N | `LlmCopilotService.cs` |
| 13 | Hỏi "khách hàng doanh thu thấp nhất" sẽ trả danh sách cao nhất | Báo rõ chưa có truy vấn này, gợi ý câu khác, **không trả số liệu sai** | `NlqController.cs` |
| 14 | Câu trả lời được thêm xuống cuối khung chat nên có thể nằm dưới câu hỏi khác | Mỗi câu hỏi có `requestId`. Câu trả lời **thay đúng vào ô "đang tải" của câu hỏi đó**. Các câu vẫn chạy song song. | `app.js` |
| 15 | Không biết câu trả lời đến từ đâu | Nhãn dưới mỗi câu trả lời: "Trả lời bởi Gemini" hoặc "Chế độ offline (Gemini đã hết hạn mức / đang quá tải / không phản hồi kịp / API key không hợp lệ…)" | `app.js` |
| 16 | Số tiền nhỏ hiện "0.01 tỷ ₫" | Dưới 1 tỷ hiện "triệu ₫" | `NlqController.cs` |

Ngoài ra, log server ghi ý định của từng câu hỏi gồm dimension, order, metrics, limit, năm, loại câu hỏi và query dự kiến, để kiểm tra.

---

## 3. Kết quả kiểm thử

**Cách thực hiện:**
- Lỗi Gemini được tạo bằng **server giả lập cục bộ** (`host.docker.internal:5099`, trả 401/429/503/treo), **không gọi Google**.
- Số liệu đối chiếu với **các API báo cáo độc lập** của Web BI (SQL riêng, không qua Copilot):
  - `/sales/by-year`
  - `/sales/top-products-revenue?top=5`
  - `/sales/top-products-profit`
  - `/sales/by-category`
  - `/question-answer/8` (MDX-08)

### 3.1. Chế độ offline và lỗi Gemini (server giả lập)

| # | Câu hỏi | Ý định nhận diện | Query | Thời gian | Nguồn / lý do | Kết quả | Đối chiếu | |
|---|---|---|---|---|---|---|---|---|
| 1 | doanh thu năm 2022 | SingleMetric, Revenue, năm 2022 | Q01(year=2022) | 0,1 s | offline / quota | 38.081.049.190 ₫, 1 dòng, không có lợi nhuận | `/sales/by-year` | PASS |
| 2 | top 5 sản phẩm doanh thu cao nhất | product, Highest, Revenue, limit 5 | Q06(top=5) | 0 s | offline / quota | 5 dòng, đúng thứ tự và giá trị | `/sales/top-products-revenue` | PASS |
| 3 | sản phẩm có doanh thu thấp nhất | product, **Lowest**, Revenue, limit 1 | **Q06_LOW**(top=1) | 0 s | offline / quota | Đầu cos đồng 6 mm2, 9.900.900 ₫ | MDX-08 | PASS |
| 4 | nhóm sản phẩm nào có lợi nhuận cao nhất | **product_group**, Highest, Profit, limit 1 | **Q08**, xếp theo Profit | 0 s | offline / quota | **Biến tần**, 26.355.355.000 ₫ | `/sales/by-category` | PASS |
| 5 | sản phẩm có lợi nhuận cao nhất | product, Highest, Profit, limit 1 | Q07(top=1) | 0 s | offline / quota | Biến tần LS H100 11kW, 9.049.737.500 ₫ | `/sales/top-products-profit` | PASS |
| 6 | so sánh doanh thu các năm | Comparison, tất cả các năm | Q01 | 0 s | offline / quota | 4 năm 2022–2025 | `/sales/by-year` | PASS |
| 7 | [Gemini 429] top 5 sản phẩm… | như #2 | Q06 | 0 s | offline / **quota** | Gemini được gọi **1 lần**, không retry | log server giả lập | PASS |
| 8 | [Gemini 503] top 5 sản phẩm… | như #2 | Q06 | 1,1 s | offline / **overloaded** | Gemini được gọi **2 lần** (retry 1), rồi offline | log server giả lập | PASS |
| 9 | [Gemini treo 60 s] top 5 sản phẩm… | như #2 | Q06 | **20,1 s** | offline / **timeout** | Có câu trả lời trước hạn 45 s của trình duyệt | — | PASS |
| 10 | [API key sai, 401] doanh thu năm 2022 | như #1 | Q01 | 0 s | offline / **auth** | Không retry, vẫn có số liệu | log server giả lập | PASS |
| 11a | doanh thu năm 2026 | OutOfRange | — | 0 s | intent_guard | Báo kho chỉ có 2022–2025, không có số liệu | — | PASS |
| 11b | khách hàng có doanh thu thấp nhất | customer, Lowest | — | 0 s | offline | Báo **chưa hỗ trợ**, không trả danh sách cao nhất | — | PASS |
| 12 | 2 câu liên tiếp, câu 1 chậm 12 s | — | Q06_LOW, Q01 | 12,1 s | offline | Câu 2 về trước nhưng mỗi câu trả lời nằm **ngay dưới câu hỏi của nó** (#1/#1, #2/#2) | trình duyệt ảo | PASS |
| 13 | [tài khoản `kho`] doanh thu năm 2022 | như #1 | — | 0 s | offline | "Bạn không có quyền truy cập…", không có số liệu | — | PASS |
| 14 | thời tiết hôm nay thế nào | General | — | 0 s | offline | Thông báo không có dữ liệu kèm gợi ý câu hỏi | — | PASS |

### 3.2. Gemini thật (`gemini-3.5-flash-lite`)

| Câu hỏi | Thời gian | Nguồn | Query | Kết quả | Đối chiếu | |
|---|---|---|---|---|---|---|
| top 5 sản phẩm doanh thu cao nhất | 6 s | llm | Q06 | 5 sản phẩm, đứng đầu Biến tần LS H100 41,04 tỷ ₫ | khớp #2 | PASS |
| sản phẩm có doanh thu thấp nhất | 5 s | llm | **Q06_LOW** | Đầu cos đồng 6 mm2, 9.900.900 ₫ | khớp #3 | PASS |
| nhóm sản phẩm nào có lợi nhuận cao nhất | 5 s | llm | **Q08** | Biến tần, 26,36 tỷ ₫, biên LN 21,87% (26.355 / 120.508) | khớp #4 | PASS |

Câu 1, 5, 6 **chưa chạy ở chế độ Gemini** để giữ giới hạn số lượt gọi Google. Logic chọn truy vấn của các câu này dùng chung với các câu đã đạt.

---

## 4. Vấn đề còn tồn tại

1. **Hạn mức Gemini:** key hiện tại (có thể là gói miễn phí) có hạn mức thấp, và `3.8-flash` đã hết quota. Hôm bảo vệ nếu `3.5-flash-lite` cũng hết quota hoặc quá tải, Copilot **vẫn trả lời bằng chế độ offline** và hiện nhãn lý do. Muốn ổn định hơn nên bật thanh toán cho key trong Google AI Studio.
2. **"Bán chậm nhất"** hiện được hiểu theo **doanh thu** thấp nhất. Nếu muốn theo **số lượng bán** (như MDX-08 trên SSAS dùng `BOTTOMCOUNT … [Quantity]`) cần thêm chỉ số Quantity cho truy vấn sản phẩm.
3. **Năm trong câu hỏi về sản phẩm/nhóm** (ví dụ "top 5 sản phẩm năm 2024") chưa được lọc, vì `Q06/Q07/Q08` chưa có tham số năm. Hiện trả theo toàn giai đoạn.
4. **Khách hàng / nhà cung cấp "thấp nhất"** chưa có truy vấn. Copilot báo rõ là chưa hỗ trợ.
5. Câu trả lời offline vẫn kèm dòng "*(Đang hiển thị dữ liệu tra cứu trực tiếp…)*" cả với câu từ chối hoặc ngoài phạm vi. Đây là hành vi cũ, chưa đổi.
6. Phần sửa logic offline **trùng phạm vi "đợt 2"** trong `BAO_CAO_FIX_AI_PRODUCT_INTENT.md` của Antigravity. **Không nên duyệt đợt 2 nữa**, nếu không sẽ ghi đè các thay đổi trên.

---

## 5. Danh sách file đã sửa

- `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/LlmCopilotService.cs`
- `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs`
- `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/BiQueryCatalog.cs`
- `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/CopilotResponseRules.cs`
- `KLTN/src/KhangNghi.Infrastructure/Repositories/DwhAnalyticsRepository.cs`: thêm `GetLowestProductsAsync`
- `KLTN/src/KhangNghi.Api/Controllers/NlqController.cs`
- `KLTN/src/KhangNghi.Api/Program.cs`
- `KLTN/src/KhangNghi.Api/.env.example`: không chứa key
- `KLTN/src/KhangNghi.Api/wwwroot/js/app.js`: hàm `askCopilot`, `copilotModeBadge`
- `KLTN/src/KhangNghi.Api/wwwroot/index.html`: phiên bản cache `?v=20261028_19`
- `KLTN/src/KhangNghi.Api/.env`: model, timeout; **không commit**
