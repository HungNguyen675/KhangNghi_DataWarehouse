# BỘ 40 CÂU TRUY VẤN MDX MẪU DỰA TRÊN THIẾT KẾ ĐỒ ÁN KHANG NGHỊ
## MA TRẬN AGENDA & THEO DÕI TRẠNG THÁI KIỂM CHỨNG DỮ LIỆU THỰC TẾ

> **LƯU Ý QUAN TRỌNG VỀ DỮ LIỆU KIỂM CHỨNG TRONG ĐỒ ÁN:**  
> Bộ 40 câu truy vấn MDX dưới đây được xây dựng **chuẩn hóa dựa trên thiết kế mô hình đa chiều (Constellation Schema)** của khối `[KhangNghi_Cube]`. Trong quá trình thực hiện đồ án, do đặc thù nguồn dữ liệu thô ban đầu từ doanh nghiệp (`KhangNghiData.xlsx`), trạng thái thực thi được phân định rõ ràng như sau:
> 
> 1. **Nhóm ĐÃ THỰC THI TRUY VẤN THÀNH CÔNG (29 Câu):** Thuộc các phân hệ **Bán hàng (`Fact_Sales`)** và **Vận chuyển (`Fact_Shipping`)**. Do đã có sẵn dữ liệu thực tế đầy đủ từ nguồn nạp, các câu truy vấn này đã được chạy trực tiếp trên SSAS / Backend API và trả về kết quả số liệu chính xác, kiểm chứng được tính đúng đắn.
> 2. **Nhóm CÂU HỎI MẪU THIẾT KẾ – CHƯA KIỂM CHỨNG ĐƯỢC DO CHƯA NẠP ĐỦ DỮ LIỆU NGUỒN THỰC TẾ (11 Câu):** Thuộc các phân hệ **Mua hàng (`Fact_Purchase`)** và **Quản lý Kho (`Fact_Inventory`)**. Do file Excel thô của doanh nghiệp chưa cung cấp dữ liệu nghiệp vụ mua hàng và kiểm kê kho thực tế, các câu lệnh này được **thiết kế mẫu chuẩn cú pháp MDX theo Schema của Cube**; trạng thái hiện tại là *chờ nạp bổ sung đầy đủ dữ liệu nguồn để đối soát tính đúng/sai thực tế*.

---

## BẢNG AGENDA THEO DÕI CHI TIẾT CÁC MỤC TRUY VẤN MDX

| Mục Agenda | Tên phân hệ nghiệp vụ | Phạm vi câu hỏi | Số câu | Trạng thái dữ liệu nguồn | Trạng thái thực thi truy vấn MDX |
| :---: | :--- | :---: | :---: | :---: | :---: |
| **Mục 1** | **Bán hàng & Doanh thu** | Câu 01 – 10 | 10 | Có sẵn đầy đủ trong Excel | <span style="color: green; font-weight: bold;">[ĐÃ TRUY VẤN THÀNH CÔNG - DỮ LIỆU THỰC]</span> |
| **Mục 2** | **Khách hàng & Thị trường** | Câu 11 – 18 | 8 | Có sẵn đầy đủ trong Excel | <span style="color: green; font-weight: bold;">[ĐÃ TRUY VẤN THÀNH CÔNG - DỮ LIỆU THỰC]</span> |
| **Mục 3** | **Nhân sự, Khuyến mãi & Đối tác** | Câu 19 – 24 | 6 | Có sẵn đầy đủ trong Excel | <span style="color: green; font-weight: bold;">[ĐÃ TRUY VẤN THÀNH CÔNG - DỮ LIỆU THỰC]</span> |
| **Mục 4** | **Mua hàng & Nhà cung cấp** | Câu 25 – 29 | 5 | Chưa có trong Excel thô | <span style="color: #b45309; font-weight: bold;">[CÂU MẪU - CHƯA NẠP ĐỦ DỮ LIỆU NGUỒN]</span> |
| **Mục 5** | **Quản lý Kho & Tồn kho** | Câu 30 – 35 | 6 | Chưa có trong Excel thô | <span style="color: #b45309; font-weight: bold;">[CÂU MẪU - CHƯA NẠP ĐỦ DỮ LIỆU NGUỒN]</span> |
| **Mục 6** | **Vận chuyển & Giao hàng** | Câu 36 – 40 | 5 | Có sẵn đầy đủ trong Excel | <span style="color: green; font-weight: bold;">[ĐÃ TRUY VẤN THÀNH CÔNG - DỮ LIỆU THỰC]</span> |
| **TỔNG** | **Toàn bộ 6 phân hệ** | **Câu 01 – 40** | **40** | **29 Thực tế / 11 Mẫu** | **29 Đã kiểm chứng / 11 Chờ nạp dữ liệu** |

---

## CHI TIẾT TỪNG MỤC THEO AGENDA

---

### MỤC 1: PHÂN HỆ BÁN HÀNG & DOANH THU (CÂU 01 - 10)
> **Trạng thái:** `[ĐÃ THỰC THI TRUY VẤN THÀNH CÔNG - CÓ SẴN DỮ LIỆU THỰC TẾ]`  
> **Căn cứ nguồn:** Bảng `Fact_Sales` liên kết `Dim_Time`, `Dim_Product`, `Dim_SalesChannel`, `Dim_PaymentMethod`.

#### Câu 01: Tổng Doanh thu, Lợi nhuận và Số lượng bán theo từng Năm (Roll-up theo Năm)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Báo cáo tăng trưởng dài hạn cấp quản trị cao nhất, tổng hợp dữ liệu từ ngày/tháng lên mức năm.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Quantity] } ON COLUMNS,
    [Dim Time].[Year].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 02: Doanh thu theo từng Quý của một Năm cụ thể (Drill-down xuống Quý)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Ban giám đốc theo dõi tiến độ hoàn thành chỉ tiêu kinh doanh theo từng quý trong năm 2026.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    [Dim Time].[Year].&[2026].CHILDREN ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 03: Doanh số bán hàng chi tiết theo từng Tháng trong năm (Xu hướng chu kỳ mùa vụ)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đánh giá xu hướng biến động doanh số qua các tháng để chuẩn bị kế hoạch sản xuất và nhập hàng.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    [Dim Time].[Month].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 04: Doanh thu và Lợi nhuận theo từng Danh mục sản phẩm (Category)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Xác định nhóm ngành hàng mũi nhọn (Nội thất văn phòng, gia đình, phụ kiện...) đóng góp vào dòng tiền.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 05: Khoan sâu (Drill-down) từ Danh mục xuống Phân nhóm sản phẩm (Sub Category)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Khảo sát chi tiết cơ cấu các dòng sản phẩm con trong từng danh mục chính.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    [Dim Product].[Sub Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 06: Top 10 Sản phẩm mang lại Doanh thu cao nhất (Top-N Analysis)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Lọc ra 10 mặt hàng "gà đẻ trứng vàng" về mặt doanh thu của toàn công ty.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,
    TOPCOUNT([Dim Product].[Product Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 07: Top 10 Sản phẩm có Lợi nhuận gộp cao nhất
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Phát hiện 10 sản phẩm có biên lợi nhuận ròng tốt nhất, không chỉ phụ thuộc vào doanh số bán thuần.
```mdx
SELECT 
    { [Measures].[Profit Amount], [Measures].[Sales Amount] } ON COLUMNS,
    TOPCOUNT([Dim Product].[Product Name].MEMBERS, 10, [Measures].[Profit Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 08: Top 5 Sản phẩm bán chậm nhất (Bottom-N Analysis)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Nhận diện các mặt hàng ế ẩm để có giải pháp xả hàng tồn hoặc ngừng kinh doanh.
```mdx
SELECT 
    { [Measures].[Quantity], [Measures].[Sales Amount] } ON COLUMNS,
    BOTTOMCOUNT([Dim Product].[Product Name].MEMBERS, 5, [Measures].[Quantity]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 09: Cơ cấu Doanh thu và Chiết khấu theo Kênh bán hàng (Sales Channel)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** So sánh hiệu quả giữa các kênh bán hàng (Bán lẻ cửa hàng, Website TMĐT, Đại lý sỉ,...).
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,
    [Dim Sales Channel].[Channel Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 10: Phân bổ Doanh thu theo Hình thức thanh toán (Payment Method)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đánh giá xu hướng thanh toán của khách hàng (Tiền mặt, Chuyển khoản, Thẻ tín dụng, Trả góp...).
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    [Dim Payment Method].[Method Type].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 2: PHÂN HỆ KHÁCH HÀNG & THỊ TRƯỜNG ĐỊA LÝ (CÂU 11 - 18)
> **Trạng thái:** `[ĐÃ THỰC THI TRUY VẤN THÀNH CÔNG - CÓ SẴN DỮ LIỆU THỰC TẾ]`  
> **Căn cứ nguồn:** Bảng `Fact_Sales` liên kết `Dim_Customer`, `Dim_Location`, `Dim_Time`.

#### Câu 11: Doanh thu theo Phân khúc khách hàng (Segment: Consumer, Corporate, Home Office)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đo lường tỷ trọng doanh số giữa khách hàng cá nhân, doanh nghiệp và văn phòng gia đình.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    [Dim Customer].[Segment].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 12: Top 10 Khách hàng VIP có giá trị mua hàng lớn nhất
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Chăm sóc khách hàng trọng điểm (Key Accounts) và triển khai chính sách tri ân đặc biệt.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,
    TOPCOUNT([Dim Customer].[Customer Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 13: Doanh thu và Lợi nhuận theo Khu vực địa lý (Region: Bắc, Trung, Nam)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đánh giá hiệu quả kinh doanh của các chi nhánh vùng miền trên cả nước.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    [Dim Location].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 14: Khoan sâu (Drill-down) Doanh số từ Khu vực xuống Tỉnh / Thành phố (City)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Xác định các tỉnh/thành phố trọng điểm tiêu thụ hàng hóa mạnh nhất trong từng vùng.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    [Dim Location].[City].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 15: Thao tác SLICE (Cắt lát) – Doanh thu các Danh mục sản phẩm tại riêng "Miền Nam"
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Cố định 1 lát cắt phân tích chỉ riêng cho thị trường trọng điểm Miền Nam bằng mệnh đề WHERE.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ([Dim Location].[Region].&[Miền Nam]);
```

#### Câu 16: Thao tác DICE (Cắt khối đa chiều) – Phân tích Doanh thu Danh mục tại "Miền Nam" trong "Năm 2026"
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Thu hẹp phạm vi khối dữ liệu thỏa mãn đồng thời 2 điều kiện chiều (Không gian & Thời gian).
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE (
    [Dim Location].[Region].&[Miền Nam],
    [Dim Time].[Year].&[2026]
);
```

#### Câu 17: Thao tác PIVOT (Xoay chiều) – Ma trận chéo giữa Danh mục sản phẩm và Vùng miền
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Xoay ma trận tọa độ hiển thị Vùng miền trên Cột và Danh mục trên Hàng để so sánh tỷ trọng chéo trực quan.
```mdx
SELECT 
    [Dim Location].[Region].MEMBERS ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ([Measures].[Sales Amount]);
```

#### Câu 18: Top 5 Thành phố đóng góp Lợi nhuận cao nhất cả nước
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Tìm ra các đô thị mang lại tỷ suất lợi nhuận cao nhất để tối ưu ngân sách quảng bá.
```mdx
SELECT 
    [Measures].[Profit Amount] ON COLUMNS,
    TOPCOUNT([Dim Location].[City].MEMBERS, 5, [Measures].[Profit Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 3: NHÂN SỰ, KHUYẾN MÃI & TÍNH TOÁN NÂNG CAO (CÂU 19 - 24)
> **Trạng thái:** `[ĐÃ THỰC THI TRUY VẤN THÀNH CÔNG - CÓ SẴN DỮ LIỆU THỰC TẾ]`  
> **Căn cứ nguồn:** Bảng `Fact_Sales` liên kết `Dim_Employee`, `Dim_Promotion`, `Dim_Manufacturer`, `Dim_Service`.

#### Câu 19: Đánh giá KPI Doanh số bán hàng theo từng Nhân viên (Dim Employee)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đánh giá năng suất của đội ngũ bán hàng và tính hoa hồng kinh doanh.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    ORDER([Dim Employee].[Employee Name].MEMBERS, [Measures].[Sales Amount], BDESC) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 20: Đánh giá hiệu quả các chương trình Khuyến mãi (Promotion Name)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Phân tích lượng tiền chiết khấu (Discount) đã bỏ ra so với doanh thu mang lại của từng đợt khuyến mãi.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,
    [Dim Promotion].[Promotion Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 21: Doanh số sản phẩm bán ra theo Hãng sản xuất (Dim Manufacturer)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Xác định thương hiệu/hãng sản xuất nào đang được thị trường ưa chuộng nhất.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,
    [Dim Manufacturer].[Manufacturer Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 22: Tạo trường tính toán động (Calculated Member) – Tính Tỷ suất lợi nhuận (Profit Margin %)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Dùng `WITH MEMBER` tính toán tỷ lệ `Lợi nhuận / Doanh thu` tức thời mà không cần can thiệp CSDL.
```mdx
WITH MEMBER [Measures].[Profit Margin Pct] AS
    IIF([Measures].[Sales Amount] = 0, 0, ([Measures].[Profit Amount] / [Measures].[Sales Amount]) * 100),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 23: Phân tích Doanh thu theo Gói Dịch vụ đính kèm (Dim Service)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đo lường doanh thu từ các gói bảo hành, dịch vụ vận chuyển và lắp đặt hoàn thiện.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    [Dim Service].[Service Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 24: Ma trận tích chéo (CROSSJOIN) giữa Kênh bán hàng và Quý trong Năm
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đối chiếu diễn biến doanh thu của từng kênh bán lẻ/online qua các quý trong năm 2026.
```mdx
SELECT 
    [Dim Time].[Quarter].MEMBERS ON COLUMNS,
    CROSSJOIN(
        [Dim Sales Channel].[Channel Name].MEMBERS,
        { [Measures].[Sales Amount] }
    ) ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 4: MUA HÀNG & NHÀ CUNG CẤP (CÂU 25 - 29)
> **Trạng thái:** `[CÂU HỎI MẪU THIẾT KẾ - CHƯA NẠP ĐỦ DỮ LIỆU NGUỒN THỰC TẾ ĐỂ ĐỐI SOÁT ĐÚNG SAI]`  
> **Căn cứ nguồn:** Bảng `Fact_Purchase` liên kết `Dim_Supplier`, `Dim_Product`, `Dim_Warehouse`, `Dim_Time`.  
> *Lưu ý: File Excel ban đầu chưa có bảng mua hàng. Câu lệnh viết chuẩn theo Schema thiết kế Cube, chờ nạp đủ nguồn.*

#### Câu 25: Tổng Chi phí mua hàng và Số lượng nhập theo từng Năm
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Purchase (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Kiểm soát dòng tiền chi mua nguyên vật liệu/hàng hóa qua các năm tài chính.
```mdx
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,
    [Dim Time].[Year].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 26: Top 5 Nhà cung cấp (Supplier) có giá trị đơn hàng mua vào lớn nhất
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Purchase (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Đánh giá mức độ phụ thuộc công nợ vào các nhà cung ứng trọng yếu.
```mdx
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,
    TOPCOUNT([Dim Supplier].[Supplier Name].MEMBERS, 5, [Measures].[Total Cost]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 27: Chi phí nhập hàng phân bổ theo từng Danh mục sản phẩm (Category)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Purchase (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Đối chiếu tỷ trọng vốn đầu tư nhập hàng giữa các nhóm sản phẩm khác nhau.
```mdx
SELECT 
    [Measures].[Total Cost] ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 28: Tính Đơn giá bình quân nhập hàng theo Nhà cung cấp (Calculated Measure)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Purchase (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** So sánh mức giá nhập bình quân giữa các nhà cung ứng để đàm phán giá tốt nhất.
```mdx
WITH MEMBER [Measures].[Avg Purchase Price] AS
    IIF([Measures].[Purchase Quantity] = 0, 0, [Measures].[Total Cost] / [Measures].[Purchase Quantity]),
    FORMAT_STRING = "#,##0"
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity], [Measures].[Avg Purchase Price] } ON COLUMNS,
    [Dim Supplier].[Supplier Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 29: Thống kê số lượng lượt nhập hàng theo từng Kho tiếp nhận (Dim Warehouse)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Purchase (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Đánh giá tần suất hoạt động nhập nguyên liệu và hàng hóa tại từng kho bãi.
```mdx
SELECT 
    { [Measures].[Fact Purchase Count], [Measures].[Purchase Quantity] } ON COLUMNS,
    [Dim Warehouse].[Warehouse Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 5: QUẢN LÝ KHO & TỒN KHO (CÂU 30 - 35)
> **Trạng thái:** `[CÂU HỎI MẪU THIẾT KẾ - CHƯA NẠP ĐỦ DỮ LIỆU NGUỒN THỰC TẾ ĐỂ ĐỐI SOÁT ĐÚNG SAI]`  
> **Căn cứ nguồn:** Bảng `Fact_Inventory` liên kết `Dim_Warehouse`, `Dim_Product`, `Dim_Location`, `Dim_Time`.  
> *Lưu ý: File Excel ban đầu chưa có dữ liệu tồn kho định kỳ. Câu lệnh viết chuẩn theo Schema thiết kế Cube, chờ nạp đủ nguồn.*

#### Câu 30: Tổng lượng Nhập, Xuất và Tồn kho hiện hành theo từng Kho hàng (Warehouse)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Inventory (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Kiểm soát dung lượng lưu trữ và dòng luân chuyển hàng hóa của từng kho.
```mdx
SELECT 
    { [Measures].[Quantity In], [Measures].[Quantity Out], [Measures].[Quantity On Hand] } ON COLUMNS,
    [Dim Warehouse].[Warehouse Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 31: Mức Tồn kho hiện tại phân bổ theo Danh mục sản phẩm
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Inventory (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Tránh tình trạng tồn đọng vốn quá mức ở một danh mục nhất định.
```mdx
SELECT 
    [Measures].[Quantity On Hand] ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 32: Cảnh báo Tồn kho chạm ngưỡng tái đặt hàng (FILTER kết hợp WITH MEMBER)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Inventory (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Tự động lọc các sản phẩm có Tồn kho thực tế nhỏ hơn Ngưỡng đặt hàng (Reorder Level) để kích hoạt cảnh báo mua sắm.
```mdx
SELECT 
    { [Measures].[Quantity On Hand], [Measures].[Reorder Level] } ON COLUMNS,
    FILTER(
        [Dim Product].[Product Name].MEMBERS,
        [Measures].[Quantity On Hand] < [Measures].[Reorder Level]
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 33: Tính toán Mức thiếu hụt Tồn kho (Stock Deficit) cho từng mặt hàng
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Inventory (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Tính chính xác số lượng hàng cần bổ sung thêm để đạt mức tồn kho an toàn tối thiểu.
```mdx
WITH MEMBER [Measures].[Stock Deficit] AS
    [Measures].[Reorder Level] - [Measures].[Quantity On Hand]
SELECT 
    { [Measures].[Quantity On Hand], [Measures].[Reorder Level], [Measures].[Stock Deficit] } ON COLUMNS,
    FILTER(
        [Dim Product].[Product Name].MEMBERS,
        [Measures].[Stock Deficit] > 0
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 34: Top 10 Sản phẩm có số lượng Tồn kho cao nhất (Nguy cơ ứ đọng vốn)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Inventory (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Giúp bộ phận quản lý kho lên phương án giải phóng mặt bằng kho bãi và xả hàng.
```mdx
SELECT 
    [Measures].[Quantity On Hand] ON COLUMNS,
    TOPCOUNT([Dim Product].[Product Name].MEMBERS, 10, [Measures].[Quantity On Hand]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 35: Biến động lượng hàng Xuất kho theo từng Tháng (Monthly Outflow)
* **Trạng thái:** Câu mẫu thiết kế theo Schema Fact_Inventory (Chờ nạp đủ dữ liệu nguồn).
* **Ý nghĩa nghiệp vụ:** Phân tích nhịp độ xuất bán hàng hóa để chủ động bố trí nhân lực đóng gói, giao hàng.
```mdx
SELECT 
    { [Measures].[Quantity Out] } ON COLUMNS,
    NON EMPTY [Dim Time].[Month].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 6: PHÂN HỆ VẬN CHUYỂN & GIAO NHẬN (CÂU 36 - 40)
> **Trạng thái:** `[ĐÃ THỰC THI TRUY VẤN THÀNH CÔNG - CÓ SẴN DỮ LIỆU THỰC TẾ]`  
> **Căn cứ nguồn:** Bảng `Fact_Shipping` liên kết `Dim_Shipper`, `Dim_Location`, `Dim_Time`, `Fact_Sales`.

#### Câu 36: Tổng Chi phí vận chuyển theo từng Đơn vị vận chuyển (Dim Shipper)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Theo dõi ngân sách cước vận tải chi trả cho các hãng giao hàng (GHTK, Viettel Post, GHN...).
```mdx
SELECT 
    { [Measures].[Shipping Cost], [Measures].[Fact Shipping Count] } ON COLUMNS,
    [Dim Shipper].[Shipper Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 37: Hiệu suất giao hàng theo Phương thức vận chuyển (Hỏa tốc, Chuẩn, Tiết kiệm)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** So sánh cước phí và thời gian giao trung bình giữa các gói dịch vụ vận chuyển.
```mdx
SELECT 
    { [Measures].[Shipping Cost], [Measures].[Delivery Days] } ON COLUMNS,
    [Dim Shipper].[Shipping Method].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 38: Thống kê Tổng số ngày trễ hẹn giao hàng (Late Days) theo Đơn vị vận chuyển
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Đo lường chỉ số SLA và uy tín cam kết giao hàng đúng hẹn của các đối tác vận tải.
```mdx
SELECT 
    { [Measures].[Late Days], [Measures].[Fact Shipping Count] } ON COLUMNS,
    ORDER([Dim Shipper].[Shipper Name].MEMBERS, [Measures].[Late Days], BDESC) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 39: Chi phí vận chuyển phân bổ theo Khu vực giao nhận (Region)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Phân tích chi phí logistics phân bổ đến các vùng miền để tối ưu kho trung chuyển vệ tinh.
```mdx
SELECT 
    [Measures].[Shipping Cost] ON COLUMNS,
    [Dim Location].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 40: Tỷ lệ Chi phí vận chuyển trên Tổng Doanh thu bán hàng (Logistics Cost Ratio)
* **Trạng thái:** Đã kiểm chứng thành công trên SSAS.
* **Ý nghĩa nghiệp vụ:** Chỉ số kiểm soát chi phí chuỗi cung ứng, đảm bảo cước vận chuyển không ăn mòn lợi nhuận biên.
```mdx
WITH MEMBER [Measures].[Shipping Ratio Pct] AS
    IIF([Measures].[Sales Amount] = 0, 0, ([Measures].[Shipping Cost] / [Measures].[Sales Amount]) * 100),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Shipping Cost], [Measures].[Shipping Ratio Pct] } ON COLUMNS,
    [Dim Location].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```
