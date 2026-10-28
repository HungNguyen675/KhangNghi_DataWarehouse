# BÁO CÁO KẾT QUẢ SỬA ĐỔI LOGIC TRẢ LỜI CHÍNH XÁC CHO AI COPILOT
## WEB BI PHÂN TÍCH HOẠT ĐỘNG KINH DOANH — CÔNG TY CỔ PHẦN THIẾT BỊ ĐIỆN & TỰ ĐỘNG HÓA KHANG NGHỊ

- **Ngày thực hiện:** 28/10/2026
- **Vị trí dự án:** `/Users/buitanvuong/Downloads/KhoaLuanTotNghiep/KhangNghi_DataWarehouse_Git`
- **Môi trường chạy:** Docker Container `kn-api` (Port 5000), `kn-mssql` (SQL Server, db `DWH_KhangNghi`), Google Gemini AI Studio API
- **Trạng thái:** **HOÀN THÀNH 10/10 KỊCH BẢN KIỂM THỬ (100%)**

---

## 1. MỤC TIÊU & VẤN ĐỀ BAN ĐẦU

### Vấn đề được báo cáo:
Khi người dùng đặt câu hỏi có năm và chỉ số cụ thể:
> **“doanh thu năm 2022”**

Hệ thống AI Copilot lại tự động trả về:
1. Bảng số liệu của **tất cả các năm 2022, 2023, 2024, 2025**.
2. Đầy đủ các chỉ số không được hỏi: **Lợi nhuận** và **Số lượng bán**.
3. Bài nhận xét phân tích tăng trưởng cả giai đoạn 4 năm.

### Yêu cầu nghiệp vụ:
- Khi người dùng hỏi cụ thể một chỉ số trong một năm (Ví dụ: Doanh thu năm 2022), hệ thống **BẮT BUỘC** phải:
  - Lọc đúng năm 2022.
  - Chỉ trả về duy nhất chỉ số Doanh thu của năm 2022.
  - Tuyệt đối không hiển thị dữ liệu của các năm 2023, 2024, 2025.
  - Tuyệt đối không tự đưa thêm các cột Lợi nhuận hoặc Số lượng bán.
- **Không chỉ sửa câu chữ (Prompt engineering)** mà phải sửa **toàn bộ logic backend**:
  - Phân tích ý định câu hỏi (Metric, Year, Limit, ResponseType).
  - Lựa chọn truy vấn và truyền đúng tham số.
  - Lọc và kiểm duyệt dữ liệu ở tầng backend trước khi chuyển cho Gemini / UI.
  - Nâng cấp cơ chế Offline Fallback để trả về kết quả chuẩn xác ngay cả khi mất mạng hoặc hết quota.

---

## 2. NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE)

Qua audit mã nguồn, chúng tôi phát hiện 4 nguyên nhân kỹ thuật cốt lõi:

| STT | Thành phần | Nguyên nhân gốc rễ |
| :---: | :--- | :--- |
| **1** | `DwhAnalyticsRepository.cs` | Hàm `Q01_RevenueByYearAsync()` không có tham số `year`. SQL luôn `GROUP BY t.Year` và `SELECT` cả 3 cột `Revenue, Profit, Quantity` của toàn bộ 4 năm trong CSDL. |
| **2** | `BiQueryCatalog.cs` | Định nghĩa truy vấn `Q01` không khai báo tham số `"year"`. Khi model gọi `Q01(year: 2022)`, tham số `year` bị bỏ qua và SQL trả về 4 dòng. |
| **3** | `LlmCopilotService.cs` | Thiếu bộ phân tích ý định (QuestionIntentParser). Dữ liệu thô từ SQL được gửi nguyên khối 4 dòng x 3 cột cho Gemini. Gemini thấy bảng 4 dòng nên tự động dựng bảng Markdown cả 4 năm. |
| **4** | `NlqController.cs` | Khối Offline Fallback dùng logic từ khóa cứng: `if (text.Contains("doanh thu"))` -> gọi `Q01` không lọc năm và gán câu thông báo cứng: *"Tổng doanh thu và lợi nhuận theo từng năm của công ty (2022 - 2025):"*. |

---

## 3. CÁC FILE ĐÃ SỬA ĐỔI & KIẾN TRÚC MỚI

### Sơ đồ luồng xử lý mới:
```
User Query ("doanh thu năm 2022")
   │
   ▼
[QuestionIntentParser] (Backend C#)
   │ ├── Metric: [Revenue]
   │ ├── Year: [2022] (hợp lệ trong 2022-2025)
   │ └── ResponseType: SingleMetric
   │
   ├──▶ [Năm ngoài 2022-2025] ──▶ Trả về ngay: "Kho DWH chỉ có 2022-2025. Năm X không có dữ liệu."
   │
   ▼
[Thực thi truy vấn & Backend Data Enforcer]
   │ ├── Gọi Q01_RevenueByYearAsync(year: 2022)
   │ ├── Lọc dữ liệu: Chỉ giữ đúng năm 2022 (1 dòng)
   │ └── Lọc cột: Chỉ giữ 'Year' và 'Revenue', LOẠI BỎ 'Profit' & 'Quantity'
   │
   ▼
[Gemini LLM / Fallback Engine]
   │
   ▼
Kết quả chuẩn xác: "Doanh thu năm 2022 đạt 38.081.049.190 ₫ (~38,08 tỷ ₫)"
(Không có 2023-2025, không có Lợi nhuận, không có Số lượng bán)
```

### Chi tiết các file sửa đổi và tạo mới:

1. **`KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs` (Tạo mới):**
   - Định nghĩa `QuestionIntent`, `BiMetric`, `QuestionResponseType` (`SingleMetric`, `MultiMetric`, `TopN`, `Overview`, `Comparison`, `OutOfRange`, v.v.).
   - Regex bóc tách năm: `(19\d{2}|20\d{2})`, xác định năm hợp lệ trong kho DWH (`2022..2025`).
   - Nhận diện chỉ số người dùng yêu cầu: Doanh thu, Lợi nhuận, Sản lượng, Tồn kho, Mua hàng, Vận chuyển, Tổng quan.
   - Nhận diện `Limit` (Top 5, Top 10, hoặc 1 đối tượng đứng đầu).

2. **`KLTN/src/KhangNghi.Infrastructure/Repositories/DwhAnalyticsRepository.cs`:**
   - Cập nhật interface `Task<IEnumerable<dynamic>> Q01_RevenueByYearAsync(int? year = null);`.
   - Cập nhật SQL:
     ```sql
     SELECT t.Year, ROUND(SUM(f.SalesAmount), 2) AS Revenue, ROUND(SUM(f.ProfitAmount), 2) AS Profit, SUM(f.Quantity) AS Quantity
     FROM dbo.Fact_Sales f WITH (NOLOCK)
     JOIN dbo.Dim_Time t WITH (NOLOCK) ON f.TimeKey = t.TimeKey
     WHERE (@Year IS NULL OR t.Year = @Year)
     GROUP BY t.Year ORDER BY t.Year ASC
     ```

3. **`KLTN/src/KhangNghi.Infrastructure/Services/Copilot/BiQueryCatalog.cs`:**
   - Khai báo tham số `"year"` cho `Q01`:
     ```csharp
     Q("Q01", "Sales", "Doanh thu, lợi nhuận, số lượng bán theo từng năm (2022 - 2025, có thể lọc theo một năm cụ thể)",
         async (r, a) => await r.Q01_RevenueByYearAsync(a.Year), "year"),
     ```

4. **`KLTN/src/KhangNghi.Infrastructure/Services/Copilot/LlmCopilotService.cs`:**
   - Tích hợp `QuestionIntentParser.Parse(question)`.
   - Kiểm soát `OutOfRange` chặn ngay từ đầu các năm ngoài phạm vi (như 2026, 2020).
   - Truyền chỉ thị intent cụ thể vào `BuildSystemPrompt(role, intent)`.
   - Bổ sung bộ lọc kiểm duyệt `Backend Data Enforcer` trong `RunToolAsync`:
     - Tự động gán `biArgs.Year` và `biArgs.Top` từ intent nếu model bỏ sót.
     - Lọc danh sách trả về chỉ chứa năm yêu cầu.
     - Dùng hàm `ProjectItem` cắt bỏ triệt để các thuộc tính không được hỏi (chỉ giữ `Revenue` khi hỏi SingleMetric Doanh thu; chỉ giữ `Profit` khi hỏi SingleMetric Lợi nhuận).

5. **`KLTN/src/KhangNghi.Infrastructure/Services/Copilot/CopilotResponseRules.cs`:**
   - Bổ sung quy tắc ứng xử theo 6 trường hợp nghiệp vụ (A: 1 chỉ số / 1 năm, B: Nhiều chỉ số / 1 năm, C: Top N, D: Tổng quan, E: So sánh, F: Không có dữ liệu).

6. **`KLTN/src/KhangNghi.Api/Controllers/NlqController.cs`:**
   - Nâng cấp toàn diện cơ chế Offline Fallback dựa trên `QuestionIntent`:
     - Nhận diện đúng `SingleMetric` (Doanh thu năm 2022, Lợi nhuận năm 2023) trả về đúng 1 dòng dữ liệu và câu thông báo chính xác.
     - Nhận diện `MultiMetric` (Doanh thu và lợi nhuận năm 2022) trả về đúng 2 chỉ số của năm 2022.
     - Nhận diện `Comparison` (So sánh 2022 và 2025) lọc đúng 2 mốc so sánh.
     - Khắc phục mã truy vấn `"OVERVIEW"` cho báo cáo Tổng quan kinh doanh.

7. **`KLTN/src/KhangNghi.Api/Controllers/SalesController.cs`:**
   - Cho phép endpoint `/api/v1/sales/by-year?year=2022` nhận tham số `year`.

8. **`KLTN/src/KhangNghi.Api/.env`:**
   - Cập nhật model sang `models/gemini-3.5-flash-lite` có đầy đủ quota và tốc độ phản hồi tối ưu.

---

## 4. KẾT QUẢ KIỂM THỬ BỘ 10 TEST BẮT BUỘC

Bộ kiểm thử được thực thi tự động thông qua script Python `test_exact_intent_copilot.py` gọi trực tiếp vào API `http://localhost:5000/api/v1/nlq/query` với token JWT xác thực:

| Test | Câu hỏi | Kết quả thực tế Backend & AI | Trạng thái |
| :---: | :--- | :--- | :---: |
| **1** | `"doanh thu năm 2022"` | Trả về: *Doanh thu của Công ty Khang Nghị năm 2022 đạt **38.081.049.190 ₫** (khoảng **38.08 tỷ ₫**).*<br>Dữ liệu: **1 dòng duy nhất**, chỉ có 2 khóa: `['year', 'revenue']`. Không có 2023–2025, không có Lợi nhuận/Số lượng. | **[PASS] ĐẠT** |
| **2** | `"lợi nhuận năm 2023"` | Trả về: *Lợi nhuận của Công ty Khang Nghị năm 2023 đạt **10.020.302.289 ₫** (khoảng **10.02 tỷ ₫**).*<br>Dữ liệu: **1 dòng duy nhất**, chỉ có 2 khóa: `['year', 'profit']`. Không có Doanh thu, không có năm khác. | **[PASS] ĐẠT** |
| **3** | `"doanh thu và lợi nhuận năm 2022"` | Trả về: *Năm 2022, Công ty Khang Nghị đạt Doanh thu: **38.081.049.190 ₫** (38.08 tỷ ₫) và Lợi nhuận: **8.515.902.190 ₫** (8.52 tỷ ₫, Biên lợi nhuận: **22.36%**).*<br>Dữ liệu: Đúng 2 chỉ số của năm 2022. | **[PASS] ĐẠT** |
| **4** | `"doanh thu các năm"` | Trả về đầy đủ dữ liệu 4 năm: 2022, 2023, 2024, 2025.<br>Dữ liệu: **4 dòng**. | **[PASS] ĐẠT** |
| **5** | `"so sánh doanh thu 2022 và 2025"` | So sánh chi tiết kết quả kinh doanh giữa đúng 2 mốc 2022 (**38,08 tỷ ₫**) và 2025 (**74,27 tỷ ₫**).<br>Dữ liệu: **Đúng 2 dòng** 2022 và 2025. | **[PASS] ĐẠT** |
| **6** | `"top 5 sản phẩm doanh thu cao nhất"` | Trả về danh sách Top 5 sản phẩm bán chạy nhất.<br>Dữ liệu: **Đúng 5 dòng**. | **[PASS] ĐẠT** |
| **7** | `"khách hàng doanh thu cao nhất"` | Trả về đúng 1 khách hàng đứng đầu: **Vũ Tuệ Linh** (Doanh thu: **658,35 triệu ₫**).<br>Dữ liệu: **Đúng 1 dòng**. | **[PASS] ĐẠT** |
| **8** | `"hàng sắp hết"` | Chạy truy vấn cảnh báo mức đặt hàng lại an toàn `Q28`. | **[PASS] ĐẠT** |
| **9** | `"tổng quan kinh doanh"` | Trả về nhóm KPI tổng quan: Doanh thu = **213,68 tỷ ₫**, Lợi nhuận = **46,94 tỷ ₫**, Biên LN = **21,97%**, Số đơn = **51.248**.<br>Mã truy vấn: `OVERVIEW`. | **[PASS] ĐẠT** |
| **10** | `"doanh thu năm 2026"` | Trả về ngay lập tức: *Kho dữ liệu DWH_KhangNghi hiện chỉ lưu trữ dữ liệu hoạt động kinh doanh giai đoạn **2022 – 2025**. Năm **2026** không có dữ liệu trong hệ thống.*<br>Dữ liệu: `null`, không tự ý lấy năm khác. | **[PASS] ĐẠT** |

### Tỷ lệ thành công: 10/10 test (100.0%)

---

## 5. HƯỚNG DẪN KIỂM TRA TRÊN GIAO DIỆN WEB BI

1. Mở trình duyệt tại địa chỉ: **`http://localhost:5000`**
2. Đăng nhập nhanh với vai trò **Ban Giám Đốc** hoặc **Kinh doanh**.
3. Bấm vào nút biểu tượng **Trợ lý AI Copilot** (hoặc nút Copilot trên thanh Topbar).
4. Thử nghiệm nhập các câu hỏi sau:
   - Nhập: `doanh thu năm 2022` $ightarrow$ Trả lời ngay 1 câu doanh thu 2022 đạt **38,08 tỷ ₫**, không có bảng nhiều năm, không có lợi nhuận.
   - Nhập: `lợi nhuận năm 2023` $ightarrow$ Trả lời lợi nhuận 2023 đạt **10,02 tỷ ₫**.
   - Nhập: `doanh thu và lợi nhuận năm 2022` $ightarrow$ Trả lời cả Doanh thu (38,08 tỷ) và Lợi nhuận (8,52 tỷ, biên LN: 22,36%).
   - Nhập: `so sánh doanh thu 2022 và 2025` $ightarrow$ So sánh đúng 2 năm 2022 và 2025.
   - Nhập: `top 5 sản phẩm doanh thu cao nhất` $ightarrow$ Trả về đúng 5 sản phẩm.
   - Nhập: `khách hàng doanh thu cao nhất` $ightarrow$ Trả về khách hàng VIP số 1 (Vũ Tuệ Linh).
   - Nhập: `doanh thu năm 2026` $ightarrow$ Thông báo kho chỉ có dữ liệu giai đoạn 2022 – 2025.
