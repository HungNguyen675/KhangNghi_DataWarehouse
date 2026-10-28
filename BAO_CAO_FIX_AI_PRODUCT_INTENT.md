# BÁO CÁO AUDIT & KẾ HOẠCH KHẮC PHỤC LỖI AI COPILOT ÁNH XẠ SAI CÂU HỎI TOP SẢN PHẨM

**Dự án:** Web BI Phân tích hoạt động kinh doanh Công ty Khang Nghi  
**Mục tiêu:** Khắc phục lỗi khi hỏi *"sản phẩm có doanh thu thấp nhất"*, AI trả về *"Top 10 sản phẩm bán chạy mang lại doanh thu cao nhất"*.  
**Ngày thực hiện:** 28/10/2026  
**Trạng thái:** GIAI ĐOẠN 1 — AUDIT HOÀN TẤT & ĐỀ XUẤT GIẢI PHÁP (Chờ người dùng phê duyệt trước khi sửa code).

---

## 1. BẰNG CHỨNG THỰC TẾ & NGUYÊN NHÂN GỐC TỪ SOURCE CODE

Sau khi rà soát toàn bộ source code thực tế, nguyên nhân gốc đến từ 5 điểm nghẽn logic đồng thời:

### 1.1. Hàm phân tích câu hỏi người dùng (`QuestionIntentParser.cs`) bỏ quên chiều 'thấp nhất'
- **Vị trí code:** `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs` (Dòng 139 - 158)
- **Bằng chứng:**
```csharp
else if (lower.Contains("cao nhất") || lower.Contains("lớn nhất") || lower.Contains("nhiều nhất") || lower.Contains("đứng đầu") || lower.Contains("bán chạy nhất") || lower.Contains("hot nhất"))
{
    if (lower.Contains("khách hàng") && !lower.Contains("những") && !lower.Contains("các") && !lower.Contains("danh sách"))
    {
        limit = 1;
    }
    else if (lower.Contains("sản phẩm") && !lower.Contains("những") && !lower.Contains("các") && !lower.Contains("danh sách"))
    {
        limit = 1;
    }
}
```
- **Hậu quả:** 
  1. Chỉ nhận diện các từ khóa cực đại (`cao nhất`, `lớn nhất`, `bán chạy nhất`). 
  2. Hoàn toàn **KHÔNG** nhận diện các từ khóa cực tiểu: `thấp nhất`, `ít nhất`, `bé nhất`, `nhỏ nhất`, `ế nhất`, `bán chậm nhất`.
  3. Khi người dùng hỏi *"sản phẩm có doanh thu thấp nhất"*, `limit` không được gán bằng 1 mà bị bỏ qua, dẫn đến rơi xuống dòng 209: `if (!limit.HasValue) limit = 10;`.
  4. Hệ thống hoàn toàn thiếu thuộc tính xác định chiều sắp xếp (ví dụ `OrderDirection` hoặc `Operator`: `Highest` vs `Lowest`).

### 1.2. Logic gán cứng truy vấn `Q06` cho mọi câu hỏi về sản phẩm
- **Vị trí code:** `QuestionIntentParser.cs` (Dòng 204 - 209)
- **Bằng chứng:**
```csharp
else if (dimension == "product" && (hasRevenue || lower.Contains("bán chạy") || lower.Contains("hot")))
{
    responseType = QuestionResponseType.TopN;
    suggestedQueryId = "Q06";
    if (!limit.HasValue) limit = 10;
}
```
- **Hậu quả:** Bất kể câu hỏi là "cao nhất" hay "thấp nhất", hệ thống luôn gán `suggestedQueryId = "Q06"` và mặc định `limit = 10`.

### 1.3. Lỗi hardcode trong Fallback Rule-based (`NlqController.cs`)
- **Vị trí code:** `KLTN/src/KhangNghi.Api/Controllers/NlqController.cs` (Dòng 202 - 215)
- **Bằng chứng:**
```csharp
if (intent.Dimension == "product")
{
    int top = intent.Limit ?? 10;
    if (intent.Metrics.Contains(BiMetric.Profit))
    {
        var data = await _repo.Q07_TopProductsByProfitAsync(top);
        string ans = top == 1
            ? "Sản phẩm mang lại lợi nhuận cao nhất cho công ty:"
            : $"Top {top} sản phẩm mang lại lợi nhuận cao nhất cho công ty:";
        return FallbackOk(ans, data, new[] { "Q07" });
    }
    else
    {
        var data = await _repo.Q06_TopProductsByRevenueAsync(top);
        string ans = top == 1
            ? "Sản phẩm bán chạy nhất mang lại doanh thu cao nhất:"
            : $"Top {top} sản phẩm bán chạy mang lại doanh thu cao nhất:";
        return FallbackOk(ans, data, new[] { "Q06" });
    }
}
```
- **Hậu quả:** Đây chính là dòng chữ chính xác xuất hiện trong câu trả lời bị lỗi! Vì `intent.Limit` bị null nên `top = 10`, và nhánh `else` tự động in ra chuỗi *"Top 10 sản phẩm bán chạy mang lại doanh thu cao nhất:"*.

### 1.4. Kho Repository và BiQueryCatalog thiếu hỗ trợ sắp xếp và lọc theo Năm
- **Vị trí code:** `KLTN/src/KhangNghi.Infrastructure/Repositories/DwhAnalyticsRepository.cs` (Dòng 197 - 220)
- **Bằng chứng:**
  - `Q06_TopProductsByRevenueAsync(int top = 10)` hardcode `ORDER BY Revenue DESC`, không có tham số `ascending` hay `year`.
  - `Q07_TopProductsByProfitAsync(int top = 10)` hardcode `ORDER BY Profit DESC`, không có tham số `ascending` hay `year`.
  - Hoàn toàn **chưa có hàm** truy vấn sản phẩm theo số lượng bán (`ORDER BY Quantity DESC/ASC`), dẫn đến việc nhập nhằng giữa "Bán chạy nhất" (sản lượng bán) và "Doanh thu cao nhất" (tiền bán).

### 1.5. Prompt và Function Calling trong `LlmCopilotService.cs` & `CopilotResponseRules.cs`
- `BuildTools` chỉ cung cấp tham số `query_id`, `top`, `year`, `region`, `product_name`, `id`. Không có tham số `ascending` hoặc `order_by`.
- Prompt trong `CopilotResponseRules.cs` (Dòng 83) ép buộc: `BẠN BẮT BUỘC CHỈ HIỂN THỊ ĐÚNG {intent.Limit.Value} kết quả theo thứ tự giảm dần`.

---

## 2. KẾT QUẢ XÁC MINH ĐỊNH NGHĨA NGHIỆP VỤ & SỐ LIỆU THỰC TẾ TRONG DWH

Đã truy vấn trực tiếp cơ sở dữ liệu `DWH_KhangNghi` trên SQL Server:

### 2.1. Measure áp dụng trong DWH & SSAS Cube
- **Doanh thu sản phẩm (Revenue):** `SUM(f.SalesAmount)` trong `Fact_Sales` (Tương ứng `[Measures].[Sales Amount]` trong SSAS Cube).
- **Lợi nhuận sản phẩm (Profit):** `SUM(f.ProfitAmount)` trong `Fact_Sales` (Tương ứng `[Measures].[Profit Amount]` trong SSAS Cube).
- **Sản lượng bán (Quantity):** `SUM(f.Quantity)` trong `Fact_Sales` (Tương ứng `[Measures].[Quantity]` trong SSAS Cube). Trong SSAS, câu MDX-08 xác nhận: Bottom-N dùng `BOTTOMCOUNT(..., [Measures].[Quantity])`.

### 2.2. Sự khác biệt rõ rệt giữa "Bán chạy nhất" và "Doanh thu cao nhất"
- **Sản phẩm bán chạy nhất (Sản lượng nhiều nhất - Quantity DESC):**
  1. `Ống ruột gà 20 mm [SIM-ACC-003]`: **41.530** cái (Doanh thu: 461.532.000 ₫)
  2. `Ống co nhiệt 6 mm [SIM-ACC-002]`: **40.230** cái (Doanh thu: 295.560.000 ₫)
- **Sản phẩm doanh thu cao nhất (SalesAmount DESC):**
  1. `Biến tần LS H100 3 Pha 380V 11kW [LSLV0110H100-4COFD]`: **41.036.237.500 ₫** (Số lượng chỉ 2.470 cái)
  2. `Biến tần 3 pha 380V 5,5 kW [SIM-VFD-001]`: **21.257.325.000 ₫** (Số lượng chỉ 2.632 cái)
=> **Kết luận nghiệp vụ:** Không được dùng lẫn lộn `Revenue` cho câu hỏi "bán chạy nhất". Phải tách riêng:
- Bán chạy nhất / bán ít nhất -> Metric = **Quantity**.
- Doanh thu cao nhất / doanh thu thấp nhất -> Metric = **Revenue**.
- Lợi nhuận cao nhất / lợi nhuận thấp nhất -> Metric = **Profit**.

### 2.3. Quy định về "Doanh thu thấp nhất" và xử lý sản phẩm không phát sinh giao dịch
- Tổng số sản phẩm trong danh mục `Dim_Product`: **60 sản phẩm**.
- Số sản phẩm có phát sinh giao dịch bán hàng trong `Fact_Sales`: **57 sản phẩm**.
- Số sản phẩm trong danh mục nhưng **chưa từng bán được** (0 giao dịch trong Fact_Sales): **3 sản phẩm** gồm:
  1. `Tủ điện phân phối tiêu chuẩn [SIM-PWR-005]`
  2. `Máy biến áp điều khiển 1 kVA [SIM-PWR-006]`
  3. `Hệ biến tần trung thế theo đơn đặt hàng [SIM-VFD-002]`
- **Số liệu sản phẩm có doanh thu thấp nhất đã phát sinh giao dịch:**
  1. `Đầu cos đồng 6 mm2 [SIM-ACC-001]`: Doanh thu = **9.900.900 ₫**, Lợi nhuận = **2.696.900 ₫**, Số lượng = **3.602** cái.
  2. `Cầu đấu domino 12 cực [SIM-ACC-004]`: Doanh thu = **83.718.750 ₫**, Lợi nhuận = **18.270.750 ₫**, Số lượng = **3.636** cái.
  3. `Dây rút nhựa 200 mm gói 100 cái [SIM-ACC-005]`: Doanh thu = **100.606.500 ₫**, Lợi nhuận = **23.809.500 ₫**, Số lượng = **3.657** cái.
- **Nếu xét riêng năm 2024:**
  - Sản phẩm doanh thu thấp nhất năm 2024 vẫn là: `Đầu cos đồng 6 mm2 [SIM-ACC-001]` với Doanh thu = **2.701.425 ₫**, Lợi nhuận = **731.425 ₫**, Số lượng = **985** cái.
- **Đề xuất nghiệp vụ:** Trong phân tích kết quả kinh doanh bán hàng (Sales Performance), câu hỏi "sản phẩm có doanh thu thấp nhất" được định nghĩa chuẩn là sản phẩm có doanh thu nhỏ nhất trong số các sản phẩm đã phát sinh giao dịch (`SalesAmount > 0`). Đối với 3 sản phẩm chưa từng phát sinh doanh thu (0 ₫), hệ thống sẽ cung cấp ghi chú rõ ràng nếu người dùng cần tra cứu hàng chưa bán được.

---

## 3. THIẾT KẾ & KẾ HOẠCH TRIỂN KHAI SỬA LOGIC

Tách riêng 5 thuộc tính độc lập theo đúng Section 3 của yêu cầu:
1. **Entity:** `Product`, `Customer`, `Supplier`, `Overview`...
2. **Metric:** `Revenue` (Doanh thu), `Profit` (Lợi nhuận), `Quantity` (Số lượng/Sản lượng bán).
3. **Operator:** `Highest` (Tăng/Cao nhất/Nhiều nhất/Bán chạy) vs `Lowest` (Giảm/Thấp nhất/Ít nhất/Bán chậm).
4. **Limit:** Số lượng Top N (nếu hỏi số ít: limit = 1; nếu hỏi Top 10: limit = 10; Top 5: limit = 5).
5. **Filters:** Năm (`TargetYears`: ví dụ 2024), Nhóm hàng, Khu vực...

### Các file sẽ được cập nhật:
1. `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/QuestionIntentParser.cs`:
   - Thêm enum `BiOperator { Highest, Lowest, None }` và `BiOrderDirection { Descending, Ascending }`.
   - Nhận diện toàn diện từ khóa:
     - `Lowest`: `thấp nhất`, `ít nhất`, `bé nhất`, `nhỏ nhất`, `kém nhất`, `ế nhất`, `bán chậm nhất`, `chậm nhất`, `giảm`.
     - `Highest`: `cao nhất`, `lớn nhất`, `nhiều nhất`, `đứng đầu`, `bán chạy nhất`, `hot nhất`, `tăng`.
   - Tách bạch metric: `bán chạy`, `số lượng bán`, `sản lượng` -> `BiMetric.Quantity`.
   - Xử lý limit = 1 cho cả "thấp nhất" số ít và "cao nhất" số ít.
2. `KLTN/src/KhangNghi.Infrastructure/Repositories/IDwhAnalyticsRepository.cs` & `DwhAnalyticsRepository.cs`:
   - Cập nhật `Q06_TopProductsByRevenueAsync(int top = 10, bool ascending = false, int? year = null)`.
   - Cập nhật `Q07_TopProductsByProfitAsync(int top = 10, bool ascending = false, int? year = null)`.
   - Bổ sung hàm mới `GetProductsByQuantitySoldAsync(int top = 10, bool ascending = false, int? year = null)` cho câu hỏi "bán chạy nhất" / "bán ít nhất".
3. `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/BiQueryCatalog.cs`:
   - Cập nhật tham số hoặc đăng ký query ID rõ ràng (Q06, Q06_ASC, Q07, Q07_ASC, Q05_RANK...).
4. `KLTN/src/KhangNghi.Infrastructure/Services/Copilot/LlmCopilotService.cs` & `CopilotResponseRules.cs`:
   - Thêm tham số `order_direction` (`asc`/`desc`) hoặc `ascending` vào Function Call của Gemini.
   - Bổ sung quy tắc trong System Prompt cho trường hợp Top/Bottom N (Cả cao nhất và thấp nhất).
5. `KLTN/src/KhangNghi.Api/Controllers/NlqController.cs`:
   - Điều hướng Fallback thông minh: kiểm tra `intent.Metric` (Revenue vs Profit vs Quantity), `intent.Operator` (Highest vs Lowest), `intent.Limit`, và `intent.TargetYears`.
   - Câu trả lời linh hoạt, đúng ngữ cảnh:
     - Thấp nhất 1 sản phẩm: *"Sản phẩm có doanh thu thấp nhất là:"*
     - Top 5 thấp nhất: *"Top 5 sản phẩm có doanh thu thấp nhất là:"*
     - Bán chạy nhất: *"Sản phẩm bán chạy nhất theo sản lượng bán là:"*

---

## 4. BỘ KIỂM THỬ HỒI QUY (10 TEST CASES THEO MỤC 6)

Sẽ tạo script kiểm thử tự động `test_product_intent.py` kiểm tra 6 tiêu chí cho cả 10 câu hỏi:
1. *"Sản phẩm có doanh thu cao nhất"* -> Metric: Revenue, Operator: Highest, Limit: 1, Order: DESC.
2. *"Sản phẩm có doanh thu thấp nhất"* -> Metric: Revenue, Operator: Lowest, Limit: 1, Order: ASC. Kết quả: `Đầu cos đồng 6 mm2 [SIM-ACC-001]` (9.900.900 ₫).
3. *"Top 10 sản phẩm doanh thu cao nhất"* -> Metric: Revenue, Operator: Highest, Limit: 10, Order: DESC.
4. *"Top 5 sản phẩm doanh thu thấp nhất"* -> Metric: Revenue, Operator: Lowest, Limit: 5, Order: ASC.
5. *"Sản phẩm lợi nhuận cao nhất"* -> Metric: Profit, Operator: Highest, Limit: 1, Order: DESC.
6. *"Sản phẩm lợi nhuận thấp nhất"* -> Metric: Profit, Operator: Lowest, Limit: 1, Order: ASC.
7. *"Sản phẩm bán chạy nhất"* -> Metric: Quantity, Operator: Highest, Limit: 1, Order: DESC. Kết quả: `Ống ruột gà 20 mm` (41.530 cái).
8. *"Sản phẩm bán ít nhất"* -> Metric: Quantity, Operator: Lowest, Limit: 1, Order: ASC. Kết quả: `Bộ lập trình PLC 24 I/O` (2.383 cái).
9. *"Sản phẩm có doanh thu thấp nhất năm 2024"* -> Metric: Revenue, Operator: Lowest, Limit: 1, Year: 2024, Order: ASC. Kết quả: `Đầu cos đồng 6 mm2` (2.701.425 ₫).
10. *"Sản phẩm"* (Không rõ metric) -> AI yêu cầu làm rõ hoặc thông báo phù hợp, không tự tiện gán Q06 mặc định.
