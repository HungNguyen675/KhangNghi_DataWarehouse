# BỘ 50 CÂU TRUY VẤN MDX PHÂN TÍCH NGHIỆP VỤ – KHO DỮ LIỆU KHANG NGHỊ
## MA TRẬN AGENDA, THAO TÁC OLAP & TRẠNG THÁI KIỂM CHỨNG

> **Phạm vi:** Bộ truy vấn chạy trên khối `[KhangNghi_Cube]` (dự án `KhangNghi_SSAS`), gồm 13 chiều dùng chung và 4 nhóm độ đo (Fact Sales, Fact Purchase, Fact Inventory, Fact Shipping). Các câu cũng chạy được trên `[Cube_Sales]` hoặc `[Cube_Inventory]` nếu khối đó chứa các chiều và độ đo được dùng.
>
> **Cập nhật 29/10/2026:**
> 1. **Bổ sung 10 câu (41–50)** theo 4 thao tác OLAP mà đề cương yêu cầu (Roll-up, Drill-down, Slice, Dice), thêm Pivot và so sánh cùng kỳ. Mỗi câu gắn với vai trò người dùng của hệ thống.
> 2. **Sửa các câu sai:**
>    - Câu 02, 16 và 24 lọc năm 2026, nhưng năm này chưa có dữ liệu bán hàng (Dim_Time chỉ đến 07/01/2026).
>    - Câu 02, 05 và 14 dùng `.CHILDREN` trên attribute hierarchy phẳng, hoặc chỉ liệt kê cấp con mà không khoan sâu theo hierarchy.
>    - Câu 22 và 40 vừa nhân 100 vừa dùng định dạng `%`, nên tỷ lệ bị phóng đại 100 lần.
>    - Câu 32 và 33 so sánh `<`, nhưng tồn kho trong dữ liệu luôn bằng ngưỡng tái đặt hàng.
>    - Câu 30, 31 và 34 cộng dồn ảnh chụp tồn kho của mọi kỳ.
>    - Câu 37 cộng dồn số ngày giao.
>    - Câu 11 ghi giá trị phân khúc không có trong kho dữ liệu.
> 3. **Trạng thái mua hàng và tồn kho:** `Fact_Purchase` và `Fact_Inventory` đã có dữ liệu trong DWH (mỗi bảng 7.747 dòng), nên các câu 25–35 không còn ở trạng thái "chờ nạp dữ liệu".
> 4. **Số tham chiếu:** cột "Kết quả tham chiếu" được tính bằng SQL trực tiếp trên `DWH_KhangNghi`. Khi chạy MDX trên SSAS (sau khi Process khối), kết quả phải khớp với các số này. Dùng chúng để đối chiếu và chụp ảnh minh chứng cho báo cáo.

### Quy ước cú pháp theo cấu trúc khối thực tế
| Đối tượng | Tên trong khối | Ghi chú |
| :--- | :--- | :--- |
| Phân cấp thời gian | `[Dim Time].[PhanCap_ThoiGian]` | Year → Quarter → Month → Day; key Year là số (`&[2025]`) |
| Phân cấp địa lý | `[Dim Location].[PhanCap_Diali]` | Region → Country → State → City |
| Phân cấp sản phẩm | `[Dim Product].[Hierarchy]` | Category → Sub Category → Product Name |
| Khu vực | `&[Miền Bắc]`, `&[Miền Trung]`, `&[Miền Nam]` | Giá trị thật trong `Dim_Location.Region` |
| Kênh bán | `Đại lý`, `Telesale trực tiếp`, `Website Khang Nghị`, `Cửa hàng` | Giá trị thật trong `Dim_SalesChannel` |

> `.CHILDREN` và `DESCENDANTS` chỉ có ý nghĩa trên **user hierarchy** (ví dụ `PhanCap_ThoiGian`). Attribute hierarchy như `[Dim Time].[Year]` chỉ có cấp All và cấp thành viên, nên `[Dim Time].[Year].&[2025].CHILDREN` trả về tập rỗng.

---

## MA TRẬN 4 THAO TÁC OLAP THEO ĐỀ CƯƠNG

| Thao tác | Câu hỏi nghiệp vụ minh họa (đề cương) | Câu truy vấn đáp ứng | Người dùng chính |
| :--- | :--- | :--- | :--- |
| **1. Roll-up** | Tổng hợp doanh thu từ tháng/quý lên năm | **Câu 01** (năm), **Câu 41** (thành phố → khu vực), **Câu 42** (sản phẩm → danh mục) | Giám đốc |
| **2. Drill-down** | Xem chi tiết doanh thu từ năm xuống quý/tháng | **Câu 02** (năm 2025 → quý → tháng), **Câu 43** (quý 4/2025 → tháng), **Câu 05** (danh mục → phân nhóm), **Câu 14** (Miền Nam → thành phố) | Giám đốc, Trưởng phòng Kinh doanh |
| **3. Slice** | Doanh thu sản phẩm trong một năm cụ thể | **Câu 15** (năm 2025), **Câu 44** (Miền Nam), **Câu 45** (kênh Website) | Trưởng phòng Kinh doanh |
| **4. Dice** | Doanh thu sản phẩm đồng thời theo năm và khu vực | **Câu 16** (2025 × Miền Nam), **Câu 46** (2 năm × 2 khu vực × 2 kênh), **Câu 47** (mua hàng: 2025 × kho TP.HCM) | Trưởng phòng Kinh doanh, Trưởng phòng Mua hàng |
| Pivot (bổ sung) | Xoay trục so sánh chéo | **Câu 17**, **Câu 48** | Giám đốc |
| So sánh cùng kỳ (bổ sung) | Tăng trưởng quý so với cùng quý năm trước | **Câu 49** | Giám đốc |

---

## BẢNG AGENDA THEO DÕI CHI TIẾT

| Mục | Phân hệ nghiệp vụ | Phạm vi | Số câu | Dữ liệu trong DWH | Trạng thái |
| :---: | :--- | :---: | :---: | :---: | :--- |
| **Mục 1** | Bán hàng & Doanh thu | Câu 01 – 10 | 10 | Fact_Sales 51.248 dòng | Câu 02, 04, 05 đã sửa; các câu còn lại giữ nguyên |
| **Mục 2** | Khách hàng & Thị trường | Câu 11 – 18 | 8 | Fact_Sales | Câu 11, 14, 15, 16 đã sửa |
| **Mục 3** | Nhân sự, Khuyến mãi & Tính toán | Câu 19 – 24 | 6 | Fact_Sales | Câu 22, 24 đã sửa; Câu 23 không có dữ liệu (xem ghi chú) |
| **Mục 4** | Mua hàng & Nhà cung cấp | Câu 25 – 29 | 5 | Fact_Purchase 7.747 dòng | Đã có dữ liệu, cần chạy lại trên SSAS |
| **Mục 5** | Quản lý kho & Tồn kho | Câu 30 – 35 | 6 | Fact_Inventory 7.747 dòng | Đã sửa logic tồn cuối kỳ, cần chạy lại trên SSAS |
| **Mục 6** | Vận chuyển & Giao hàng | Câu 36 – 40 | 5 | Fact_Shipping 25.728 dòng | Câu 37, 40 đã sửa |
| **Mục 7** | **Thao tác OLAP theo vai trò** | **Câu 41 – 50** | **10** | Fact_Sales, Fact_Purchase, Fact_Inventory | **Mới bổ sung** |
| **TỔNG** | | **Câu 01 – 50** | **50** | | |

---

## CHI TIẾT TỪNG MỤC

---

### MỤC 1: PHÂN HỆ BÁN HÀNG & DOANH THU (CÂU 01 - 10)
> **Căn cứ nguồn:** `Fact_Sales` liên kết `Dim_Time`, `Dim_Product`, `Dim_SalesChannel`, `Dim_PaymentMethod`.

#### Câu 01: Tổng Doanh thu, Lợi nhuận và Số lượng bán theo từng Năm — **ROLL-UP** (ngày → năm)
* **Ý nghĩa nghiệp vụ:** Báo cáo tăng trưởng dài hạn cho Ban Giám đốc. Dữ liệu cấp dòng đơn hàng theo ngày được cộng gộp lên cấp năm.
* **Kết quả tham chiếu (SQL):**
  * 2022: 38,08 tỷ (lợi nhuận 8,52 tỷ).
  * 2023: 45,88 tỷ (10,02 tỷ).
  * 2024: 55,45 tỷ (12,06 tỷ).
  * 2025: 74,27 tỷ (16,34 tỷ).
  * Tổng 213,68 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Quantity] } ON COLUMNS,
    NON EMPTY [Dim Time].[PhanCap_ThoiGian].[Year].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 02: Doanh thu năm 2025 khoan sâu xuống Quý và Tháng — **DRILL-DOWN** *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Ban Giám đốc theo dõi tiến độ doanh thu năm gần nhất có đủ dữ liệu, từ tổng năm xuống từng quý rồi từng tháng.
* **Lý do sửa:** bản cũ dùng `[Dim Time].[Year].&[2026].CHILDREN`. Năm 2026 không có doanh thu, và `.CHILDREN` trên attribute hierarchy trả về tập rỗng.
* **Kết quả tham chiếu (SQL):** năm 2025 là 74,27 tỷ, gồm Q1 11,73 tỷ; Q2 17,38 tỷ; Q3 19,28 tỷ; Q4 25,88 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    DESCENDANTS(
        [Dim Time].[PhanCap_ThoiGian].[Year].&[2025],
        [Dim Time].[PhanCap_ThoiGian].[Month],
        SELF_AND_BEFORE
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 03: Doanh số bán hàng chi tiết theo từng Tháng trong năm (Xu hướng chu kỳ mùa vụ)
* **Ý nghĩa nghiệp vụ:** Đánh giá biến động doanh số qua các tháng (gộp mọi năm) để lập kế hoạch nhập hàng theo mùa vụ.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Time].[Month].[Month].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 04: Doanh thu và Lợi nhuận theo từng Danh mục sản phẩm (Category)
* **Ý nghĩa nghiệp vụ:** Xác định nhóm hàng mũi nhọn trong 7 danh mục thiết bị điện – tự động hóa (Biến tần, Thiết bị tự động hóa, Thiết bị điều khiển, …).
* **Kết quả tham chiếu (SQL):** Biến tần 120,51 tỷ; Thiết bị tự động hóa 43,16 tỷ; Thiết bị điều khiển 23,42 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 05: Khoan sâu từ Danh mục "Biến tần" xuống Phân nhóm sản phẩm — **DRILL-DOWN** *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Danh mục Biến tần chiếm hơn một nửa doanh thu. Trưởng phòng Kinh doanh cần biết phân nhóm nào tạo ra doanh thu đó.
* **Lý do sửa:** bản cũ liệt kê toàn bộ Sub Category, không đi từ cấp cha xuống cấp con.
* **Kết quả tham chiếu (SQL):** Biến tần (phân nhóm chung) 99,25 tỷ; Biến tần hạ thế 21,26 tỷ; Biến tần trung thế chưa phát sinh doanh thu.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Product].[Hierarchy].[Category].&[Biến tần].CHILDREN ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 06: Top 10 Sản phẩm mang lại Doanh thu cao nhất (Top-N Analysis)
* **Ý nghĩa nghiệp vụ:** Lọc ra 10 mặt hàng đóng góp doanh thu lớn nhất toàn công ty.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,
    TOPCOUNT([Dim Product].[Product Name].[Product Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 07: Top 10 Sản phẩm có Lợi nhuận gộp cao nhất
* **Ý nghĩa nghiệp vụ:** Phát hiện 10 sản phẩm sinh lời tốt nhất, độc lập với doanh số.
```mdx
SELECT 
    { [Measures].[Profit Amount], [Measures].[Sales Amount] } ON COLUMNS,
    TOPCOUNT([Dim Product].[Product Name].[Product Name].MEMBERS, 10, [Measures].[Profit Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 08: Top 5 Sản phẩm bán chậm nhất (Bottom-N Analysis)
* **Ý nghĩa nghiệp vụ:** Nhận diện mặt hàng bán chậm để có giải pháp xả hàng hoặc ngừng nhập.
```mdx
SELECT 
    { [Measures].[Quantity], [Measures].[Sales Amount] } ON COLUMNS,
    BOTTOMCOUNT(
        NONEMPTY([Dim Product].[Product Name].[Product Name].MEMBERS, [Measures].[Quantity]),
        5, [Measures].[Quantity]
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 09: Cơ cấu Doanh thu và Chiết khấu theo Kênh bán hàng (Sales Channel)
* **Ý nghĩa nghiệp vụ:** So sánh hiệu quả 4 kênh: Đại lý, Telesale trực tiếp, Website Khang Nghị và Cửa hàng.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,
    NON EMPTY [Dim Sales Channel].[Channel Name].[Channel Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 10: Phân bổ Doanh thu theo Hình thức thanh toán (Payment Method)
* **Ý nghĩa nghiệp vụ:** Đánh giá cơ cấu thanh toán của khách hàng để quản lý công nợ.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Payment Method].[Method Type].[Method Type].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 2: PHÂN HỆ KHÁCH HÀNG & THỊ TRƯỜNG ĐỊA LÝ (CÂU 11 - 18)
> **Căn cứ nguồn:** `Fact_Sales` liên kết `Dim_Customer`, `Dim_Location`, `Dim_Time`.

#### Câu 11: Doanh thu theo Phân khúc khách hàng (Segment) *(đã sửa mô tả)*
* **Ý nghĩa nghiệp vụ:** Đo tỷ trọng doanh số của 6 phân khúc: Hộ kinh doanh nhỏ lẻ, Xưởng sản xuất, Nhà thầu xây dựng, Thợ điện dân dụng, Nhà máy và Chủ nhà.
* **Kết quả tham chiếu (SQL):**
  * Hộ kinh doanh nhỏ lẻ: 42,92 tỷ.
  * Xưởng sản xuất: 42,90 tỷ.
  * Nhà thầu xây dựng: 42,03 tỷ.
  * Thợ điện dân dụng: 40,26 tỷ.
  * Nhà máy: 40,18 tỷ.
  * Chủ nhà: 5,39 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Customer].[Segment].[Segment].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 12: Top 10 Khách hàng có giá trị mua hàng lớn nhất
* **Ý nghĩa nghiệp vụ:** Chăm sóc khách hàng trọng điểm và xây dựng chính sách chiết khấu riêng.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,
    TOPCOUNT([Dim Customer].[Customer Name].[Customer Name].MEMBERS, 10, [Measures].[Sales Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 13: Doanh thu và Lợi nhuận theo Khu vực địa lý (Miền Bắc, Miền Trung, Miền Nam)
* **Ý nghĩa nghiệp vụ:** Đánh giá hiệu quả kinh doanh theo vùng miền.
* **Kết quả tham chiếu (SQL):** Miền Nam 140,85 tỷ; Miền Trung 37,24 tỷ; Miền Bắc 35,58 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 14: Khoan sâu Doanh số từ "Miền Nam" xuống Tỉnh/Thành phố — **DRILL-DOWN** *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Miền Nam chiếm khoảng 66% doanh thu. Cần biết tỉnh/thành nào trong vùng tiêu thụ mạnh nhất.
* **Lý do sửa:** bản cũ chỉ liệt kê mọi City, không khoan sâu theo phân cấp địa lý.
* **Kết quả tham chiếu (SQL):** TP. Hồ Chí Minh 36,29 tỷ; Biên Hòa 35,58 tỷ; Thủ Dầu Một 34,70 tỷ; Cần Thơ 34,29 tỷ.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY DESCENDANTS(
        [Dim Location].[PhanCap_Diali].[Region].&[Miền Nam],
        [Dim Location].[PhanCap_Diali].[City]
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 15: Doanh thu sản phẩm (theo danh mục) trong năm 2025 — **SLICE** *(đã sửa theo đề cương)*
* **Ý nghĩa nghiệp vụ:** Cố định một lát cắt trên chiều Thời gian (năm 2025) bằng mệnh đề `WHERE` để xem cơ cấu doanh thu sản phẩm trong năm.
* **Lý do sửa:** đề cương mô tả Slice là "doanh thu sản phẩm trong một năm cụ thể". Lát cắt theo khu vực của bản cũ được chuyển sang **Câu 44**.
* **Kết quả tham chiếu (SQL):**
  * Biến tần: 42,52 tỷ.
  * Thiết bị tự động hóa: 14,97 tỷ.
  * Thiết bị điều khiển: 7,78 tỷ.
  * Thiết bị truyền tải điện: 2,74 tỷ.
  * Thiết bị đóng cắt: 2,29 tỷ.
  * Vật tư phụ kiện: 2,06 tỷ.
  * Thiết bị đo lường hiển thị: 1,91 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Dim Time].[Year].&[2025] );
```

#### Câu 16: Doanh thu sản phẩm đồng thời theo Năm 2025 và Khu vực "Miền Nam" — **DICE** *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Thu hẹp khối dữ liệu theo đồng thời hai chiều (Thời gian và Địa lý) để đánh giá thị trường trọng điểm trong năm gần nhất.
* **Lý do sửa:** bản cũ lọc năm 2026, nên kết quả rỗng.
* **Kết quả tham chiếu (SQL):**
  * Biến tần: 27,81 tỷ.
  * Thiết bị tự động hóa: 9,73 tỷ.
  * Thiết bị điều khiển: 5,23 tỷ.
  * Thiết bị truyền tải điện: 1,83 tỷ.
  * Thiết bị đóng cắt: 1,44 tỷ.
  * Vật tư phụ kiện: 1,33 tỷ.
  * Thiết bị đo lường hiển thị: 1,26 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE (
    [Dim Time].[Year].&[2025],
    [Dim Location].[Region].&[Miền Nam]
);
```

#### Câu 17: Thao tác PIVOT (Xoay chiều) – Ma trận chéo giữa Danh mục sản phẩm và Vùng miền
* **Ý nghĩa nghiệp vụ:** Đưa Vùng miền lên cột và Danh mục xuống dòng để so sánh tỷ trọng chéo.
```mdx
SELECT 
    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Measures].[Sales Amount] );
```

#### Câu 18: Top 5 Thành phố đóng góp Lợi nhuận cao nhất cả nước
* **Ý nghĩa nghiệp vụ:** Xác định các đô thị sinh lời cao nhất để ưu tiên nguồn lực bán hàng.
```mdx
SELECT 
    [Measures].[Profit Amount] ON COLUMNS,
    TOPCOUNT([Dim Location].[City].[City].MEMBERS, 5, [Measures].[Profit Amount]) ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 3: NHÂN SỰ, KHUYẾN MÃI & TÍNH TOÁN NÂNG CAO (CÂU 19 - 24)
> **Căn cứ nguồn:** `Fact_Sales` liên kết `Dim_Employee`, `Dim_Promotion`, `Dim_Manufacturer`, `Dim_Service`.

#### Câu 19: Đánh giá KPI Doanh số bán hàng theo từng Nhân viên (Dim Employee)
* **Ý nghĩa nghiệp vụ:** Đánh giá năng suất đội ngũ bán hàng làm căn cứ tính hoa hồng.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    ORDER(
        NONEMPTY([Dim Employee].[Employee Name].[Employee Name].MEMBERS, [Measures].[Sales Amount]),
        [Measures].[Sales Amount], BDESC
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 20: Đánh giá hiệu quả các chương trình Khuyến mãi (Promotion Name)
* **Ý nghĩa nghiệp vụ:** So sánh tiền chiết khấu đã chi với doanh thu mang lại của từng chương trình.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Discount Amount] } ON COLUMNS,
    NON EMPTY [Dim Promotion].[Promotion Name].[Promotion Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 21: Doanh số sản phẩm bán ra theo Hãng sản xuất (Dim Manufacturer)
* **Ý nghĩa nghiệp vụ:** Xác định thương hiệu được thị trường ưa chuộng để ưu tiên nhập hàng.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Quantity] } ON COLUMNS,
    NON EMPTY [Dim Manufacturer].[Manufacturer Name].[Manufacturer Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 22: Calculated Member – Tỷ suất lợi nhuận (Profit Margin %) theo Danh mục *(đã sửa công thức)*
* **Ý nghĩa nghiệp vụ:** Dùng `WITH MEMBER` tính `Lợi nhuận / Doanh thu` tức thời mà không sửa CSDL.
* **Lý do sửa:** bản cũ nhân 100 rồi định dạng `0.00%`, nên hiển thị bị phóng đại 100 lần (ví dụ 2.197%).
* **Kết quả tham chiếu (SQL):**
  * Toàn công ty: 21,97%.
  * Vật tư phụ kiện: 24,04%.
  * Thiết bị đóng cắt: 23,45%.
  * Biến tần: 21,87%.
```mdx
WITH MEMBER [Measures].[Profit Margin Pct] AS
    IIF([Measures].[Sales Amount] = 0, NULL, [Measures].[Profit Amount] / [Measures].[Sales Amount]),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 23: Phân tích Doanh thu theo Gói Dịch vụ đính kèm (Dim Service)
* **Ghi chú:** hiện `Dim_Service` có 0 dòng và `Fact_Sales.ServiceKey` đều NULL, nên câu này trả về rỗng. Câu được giữ lại như **câu thiết kế** để dùng khi nguồn bổ sung dữ liệu dịch vụ (bảo hành, lắp đặt).
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Service].[Service Name].[Service Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 24: Ma trận CROSSJOIN giữa Kênh bán hàng và Quý của năm 2025 *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Đối chiếu diễn biến doanh thu của từng kênh qua các quý năm 2025.
* **Lý do sửa:** mô tả cũ nói năm 2026 nhưng câu lệnh không lọc năm, nên cộng gộp mọi năm.
* **Kết quả tham chiếu (SQL), Q1 → Q4/2025, tỷ đồng:**
  * Đại lý: 4,56 / 5,96 / 6,57 / 9,28.
  * Telesale trực tiếp: 4,41 / 6,29 / 6,62 / 8,82.
  * Website Khang Nghị: 2,50 / 4,71 / 5,60 / 7,17.
  * Cửa hàng: 0,26 / 0,42 / 0,48 / 0,60.
```mdx
SELECT 
    NON EMPTY [Dim Time].[Quarter].[Quarter].MEMBERS ON COLUMNS,
    NON EMPTY CROSSJOIN(
        [Dim Sales Channel].[Channel Name].[Channel Name].MEMBERS,
        { [Measures].[Sales Amount] }
    ) ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Dim Time].[Year].&[2025] );
```

---

### MỤC 4: MUA HÀNG & NHÀ CUNG CẤP (CÂU 25 - 29)
> **Căn cứ nguồn:** `Fact_Purchase` (7.747 dòng) liên kết `Dim_Supplier`, `Dim_Product`, `Dim_Warehouse`, `Dim_Time`.  
> **Trạng thái:** đã có dữ liệu trong DWH, cần chạy lại trên SSAS để đối soát. Tổng chi phí nhập là 167,76 tỷ cho 388.170 đơn vị.  
> **Lưu ý:** độ đo `Unit Cost` đang dùng hàm tổng hợp mặc định là Sum. Không đọc trực tiếp độ đo này ở cấp tổng hợp; dùng đơn giá bình quân tính như Câu 28.

#### Câu 25: Tổng Chi phí mua hàng và Số lượng nhập theo từng Năm
* **Ý nghĩa nghiệp vụ:** Kiểm soát dòng tiền chi mua hàng qua các năm tài chính.
```mdx
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,
    NON EMPTY [Dim Time].[Year].[Year].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 26: Top 5 Nhà cung cấp có giá trị mua vào lớn nhất
* **Ý nghĩa nghiệp vụ:** Đánh giá mức độ phụ thuộc vào các nhà cung ứng trọng yếu.
```mdx
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,
    TOPCOUNT([Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS, 5, [Measures].[Total Cost]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 27: Chi phí nhập hàng phân bổ theo từng Danh mục sản phẩm
* **Ý nghĩa nghiệp vụ:** Đối chiếu tỷ trọng vốn nhập hàng giữa các nhóm sản phẩm.
```mdx
SELECT 
    [Measures].[Total Cost] ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 28: Đơn giá bình quân nhập hàng theo Nhà cung cấp (Calculated Measure)
* **Ý nghĩa nghiệp vụ:** So sánh giá nhập bình quân giữa các nhà cung ứng để đàm phán giá.
```mdx
WITH MEMBER [Measures].[Avg Purchase Price] AS
    IIF([Measures].[Purchase Quantity] = 0, NULL, [Measures].[Total Cost] / [Measures].[Purchase Quantity]),
    FORMAT_STRING = "#,##0"
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity], [Measures].[Avg Purchase Price] } ON COLUMNS,
    NON EMPTY [Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 29: Số lượt nhập hàng theo từng Kho tiếp nhận (Dim Warehouse)
* **Ý nghĩa nghiệp vụ:** Đánh giá tần suất nhập hàng tại từng kho.
```mdx
SELECT 
    { [Measures].[Fact Purchase Count], [Measures].[Purchase Quantity] } ON COLUMNS,
    NON EMPTY [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 5: QUẢN LÝ KHO & TỒN KHO (CÂU 30 - 35)
> **Căn cứ nguồn:** `Fact_Inventory` (7.747 dòng) liên kết `Dim_Warehouse`, `Dim_Product`, `Dim_Location`, `Dim_Time`.  
> **Đặc điểm dữ liệu:**
> * `Fact_Inventory` là bảng **ảnh chụp tồn kho định kỳ** (48 kỳ, kỳ cuối ngày 01/12/2025).
> * `Quantity On Hand` và `Reorder Level` là độ đo **bán cộng dồn** (semi-additive). Cộng qua thời gian là sai, nên các câu dưới đây lấy **kỳ cuối cùng có dữ liệu**.
> * Trong dữ liệu hiện tại, mọi dòng có tồn kho **bằng đúng** ngưỡng tái đặt hàng.
> * **Hướng xử lý về lâu dài:** đặt hàm tổng hợp `LastNonEmpty` cho hai độ đo này trong khối (cần SSAS Enterprise hoặc Developer).

#### Câu 30: Tổng Nhập, Xuất và Tồn kho cuối kỳ theo từng Kho hàng *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Thủ kho kiểm soát dòng luân chuyển và tồn hiện hành của từng kho.
* **Lý do sửa:** bản cũ cộng `Quantity On Hand` của tất cả 48 kỳ.
* **Kết quả tham chiếu (SQL):**

  | Kho | Nhập | Xuất | Tồn kỳ 01/12/2025 |
  | :--- | ---: | ---: | ---: |
  | Kho TP. Hồ Chí Minh | 260.055 | 258.595 | 1.460 |
  | Kho Biên Hòa | 64.218 | 62.758 | 1.460 |
  | Kho Thủ Dầu Một | 63.897 | 62.437 | 1.460 |
```mdx
WITH MEMBER [Measures].[Ton Cuoi Ky] AS
    ( [Measures].[Quantity On Hand],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )
SELECT 
    { [Measures].[Quantity In], [Measures].[Quantity Out], [Measures].[Ton Cuoi Ky] } ON COLUMNS,
    NON EMPTY [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 31: Tồn kho cuối kỳ phân bổ theo Danh mục sản phẩm *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Tránh tồn đọng vốn quá mức ở một danh mục.
```mdx
WITH MEMBER [Measures].[Ton Cuoi Ky] AS
    ( [Measures].[Quantity On Hand],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )
SELECT 
    [Measures].[Ton Cuoi Ky] ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 32: Cảnh báo Tồn kho chạm ngưỡng tái đặt hàng (FILTER + WITH MEMBER) *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Lọc các sản phẩm có tồn kỳ cuối **nhỏ hơn hoặc bằng** ngưỡng tái đặt hàng để Trưởng phòng Mua hàng lên kế hoạch nhập.
* **Lý do sửa:** điều kiện `<` của bản cũ không bao giờ đúng, vì dữ liệu có tồn kho bằng đúng ngưỡng. "Chạm ngưỡng" nghĩa là `<=`.
* **Kết quả tham chiếu (SQL):** cả 57 sản phẩm tồn kho đều chạm ngưỡng ở kỳ cuối. Kết quả này phản ánh đặc điểm dữ liệu mô phỏng (nhập bằng xuất), không phải lỗi của truy vấn.
```mdx
WITH 
MEMBER [Measures].[Ton Cuoi Ky] AS
    ( [Measures].[Quantity On Hand],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )
MEMBER [Measures].[Nguong Cuoi Ky] AS
    ( [Measures].[Reorder Level],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Reorder Level]), 1).ITEM(0) )
SELECT 
    { [Measures].[Ton Cuoi Ky], [Measures].[Nguong Cuoi Ky] } ON COLUMNS,
    FILTER(
        NONEMPTY([Dim Product].[Product Name].[Product Name].MEMBERS, [Measures].[Quantity On Hand]),
        [Measures].[Ton Cuoi Ky] <= [Measures].[Nguong Cuoi Ky]
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 33: Mức thiếu hụt so với tồn kho an toàn (Stock Deficit) cho từng mặt hàng *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Tính số lượng cần bổ sung để đạt **gấp đôi** ngưỡng tái đặt hàng (mức an toàn đề xuất). Hệ số 2 là tham số nghiệp vụ, có thể điều chỉnh.
```mdx
WITH 
MEMBER [Measures].[Ton Cuoi Ky] AS
    ( [Measures].[Quantity On Hand],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )
MEMBER [Measures].[Nguong Cuoi Ky] AS
    ( [Measures].[Reorder Level],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Reorder Level]), 1).ITEM(0) )
MEMBER [Measures].[Stock Deficit] AS
    2 * [Measures].[Nguong Cuoi Ky] - [Measures].[Ton Cuoi Ky]
SELECT 
    { [Measures].[Ton Cuoi Ky], [Measures].[Nguong Cuoi Ky], [Measures].[Stock Deficit] } ON COLUMNS,
    FILTER(
        NONEMPTY([Dim Product].[Product Name].[Product Name].MEMBERS, [Measures].[Quantity On Hand]),
        [Measures].[Stock Deficit] > 0
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 34: Top 10 Sản phẩm có Tồn kho cuối kỳ cao nhất (Nguy cơ ứ đọng vốn) *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** Giúp quản lý kho lên phương án giải phóng hàng tồn.
* **Kết quả tham chiếu (SQL):** nhóm dây cáp, máng cáp và ống co nhiệt có tồn kỳ cuối 600 đơn vị (cộng 3 kho), cao nhất.
```mdx
WITH MEMBER [Measures].[Ton Cuoi Ky] AS
    ( [Measures].[Quantity On Hand],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )
SELECT 
    [Measures].[Ton Cuoi Ky] ON COLUMNS,
    TOPCOUNT([Dim Product].[Product Name].[Product Name].MEMBERS, 10, [Measures].[Ton Cuoi Ky]) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 35: Biến động lượng hàng Xuất kho theo từng Tháng của năm 2025
* **Ý nghĩa nghiệp vụ:** Phân tích nhịp độ xuất kho để bố trí nhân lực đóng gói và giao hàng. `Quantity Out` là độ đo cộng dồn được, nên được phép Sum theo thời gian.
```mdx
SELECT 
    { [Measures].[Quantity Out] } ON COLUMNS,
    NON EMPTY [Dim Time].[Month].[Month].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Dim Time].[Year].&[2025] );
```

---

### MỤC 6: PHÂN HỆ VẬN CHUYỂN & GIAO NHẬN (CÂU 36 - 40)
> **Căn cứ nguồn:** `Fact_Shipping` (25.728 dòng, mỗi dòng 1 đơn hàng) liên kết `Dim_Shipper`, `Dim_Location`, `Dim_Time`.  
> **Phương thức vận chuyển thật:** "Giao hàng tiêu chuẩn" và "Nhận tại kho".

#### Câu 36: Tổng Chi phí vận chuyển theo từng Đơn vị/Phương thức vận chuyển
* **Ý nghĩa nghiệp vụ:** Theo dõi ngân sách cước vận chuyển.
```mdx
SELECT 
    { [Measures].[Shipping Cost], [Measures].[Fact Shipping Count] } ON COLUMNS,
    NON EMPTY [Dim Shipper].[Shipper Name].[Shipper Name].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 37: Hiệu suất giao hàng theo Phương thức vận chuyển *(đã sửa)*
* **Ý nghĩa nghiệp vụ:** So sánh cước phí và **số ngày giao trung bình** giữa các phương thức.
* **Lý do sửa:** `Delivery Days` dùng hàm Sum, nên bản cũ hiển thị tổng số ngày thay vì trung bình.
* **Kết quả tham chiếu (SQL):**
  * Giao hàng tiêu chuẩn: 24.436 đơn, cước 1.343,19 triệu, trung bình 4,18 ngày.
  * Nhận tại kho: 1.292 đơn, cước 0, 0 ngày.
```mdx
WITH MEMBER [Measures].[Avg Delivery Days] AS
    IIF([Measures].[Fact Shipping Count] = 0, NULL,
        [Measures].[Delivery Days] / [Measures].[Fact Shipping Count]),
    FORMAT_STRING = "0.00"
SELECT 
    { [Measures].[Shipping Cost], [Measures].[Fact Shipping Count], [Measures].[Avg Delivery Days] } ON COLUMNS,
    NON EMPTY [Dim Shipper].[Shipping Method].[Shipping Method].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 38: Tổng số ngày trễ hẹn giao hàng (Late Days) theo Đơn vị vận chuyển
* **Ý nghĩa nghiệp vụ:** Đo cam kết giao hàng đúng hẹn (SLA).
```mdx
SELECT 
    { [Measures].[Late Days], [Measures].[Fact Shipping Count] } ON COLUMNS,
    ORDER(
        NONEMPTY([Dim Shipper].[Shipper Name].[Shipper Name].MEMBERS, [Measures].[Fact Shipping Count]),
        [Measures].[Late Days], BDESC
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 39: Chi phí vận chuyển phân bổ theo Khu vực giao nhận (Region)
* **Ý nghĩa nghiệp vụ:** Phân tích chi phí logistics theo vùng miền.
```mdx
SELECT 
    [Measures].[Shipping Cost] ON COLUMNS,
    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 40: Tỷ lệ Chi phí vận chuyển trên Doanh thu (Logistics Cost Ratio) *(đã sửa công thức)*
* **Ý nghĩa nghiệp vụ:** Kiểm soát để cước vận chuyển không bào mòn lợi nhuận.
* **Lý do sửa:** cùng lỗi nhân 100 hai lần như Câu 22.
* **Kết quả tham chiếu (SQL):** toàn công ty 0,63%.
```mdx
WITH MEMBER [Measures].[Shipping Ratio Pct] AS
    IIF([Measures].[Sales Amount] = 0, NULL, [Measures].[Shipping Cost] / [Measures].[Sales Amount]),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Shipping Cost], [Measures].[Shipping Ratio Pct] } ON COLUMNS,
    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

### MỤC 7: THAO TÁC OLAP THEO VAI TRÒ NGƯỜI DÙNG (CÂU 41 - 50) — *MỚI BỔ SUNG*
> **Mục tiêu:** đáp ứng đủ 4 thao tác Roll-up, Drill-down, Slice và Dice của đề cương. Mỗi câu gắn với nhu cầu của một vai trò trong hệ thống: Giám đốc (Director), Trưởng phòng Kinh doanh (SalesManager), Trưởng phòng Mua hàng (PurchaseManager) và Quản lý kho (InventoryManager).

#### Câu 41: Gộp doanh thu từ Thành phố lên Khu vực — **ROLL-UP** (chiều Địa lý)
* **Người dùng:** Giám đốc.
* **Câu hỏi nghiệp vụ:** "Mỗi miền đóng góp bao nhiêu doanh thu và lợi nhuận?"
* **Kết quả tham chiếu (SQL):**
  * Miền Nam: 140,85 tỷ (lợi nhuận 30,94 tỷ).
  * Miền Trung: 37,24 tỷ (8,16 tỷ).
  * Miền Bắc: 35,58 tỷ (7,84 tỷ).
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Location].[PhanCap_Diali].[Region].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 42: Gộp doanh thu từ Sản phẩm lên Danh mục, kèm tỷ suất lợi nhuận — **ROLL-UP** (chiều Sản phẩm)
* **Người dùng:** Giám đốc, Trưởng phòng Kinh doanh.
* **Câu hỏi nghiệp vụ:** "Ngành hàng nào vừa lớn về doanh thu vừa có biên lợi nhuận tốt?"
* **Kết quả tham chiếu (SQL):**

  | Danh mục | Doanh thu | Lợi nhuận | Tỷ suất |
  | :--- | ---: | ---: | ---: |
  | Biến tần | 120,51 tỷ | 26,36 tỷ | 21,87% |
  | Thiết bị tự động hóa | 43,16 tỷ | 9,36 tỷ | 21,68% |
  | Thiết bị điều khiển | 23,42 tỷ | 5,05 tỷ | 21,55% |
  | Thiết bị truyền tải điện | 8,19 tỷ | 1,92 tỷ | 23,41% |
  | Thiết bị đóng cắt | 6,81 tỷ | 1,60 tỷ | 23,45% |
  | Vật tư phụ kiện | 6,11 tỷ | 1,47 tỷ | 24,04% |
  | Thiết bị đo lường hiển thị | 5,49 tỷ | 1,20 tỷ | 21,84% |
```mdx
WITH MEMBER [Measures].[Profit Margin Pct] AS
    IIF([Measures].[Sales Amount] = 0, NULL, [Measures].[Profit Amount] / [Measures].[Sales Amount]),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,
    ORDER(
        NONEMPTY([Dim Product].[Hierarchy].[Category].MEMBERS, [Measures].[Sales Amount]),
        [Measures].[Sales Amount], BDESC
    ) ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 43: Khoan sâu Quý 4/2025 xuống từng Tháng — **DRILL-DOWN** (2 cấp)
* **Người dùng:** Giám đốc.
* **Câu hỏi nghiệp vụ:** "Quý 4/2025 tăng mạnh nhất năm, tháng nào đóng góp nhiều nhất?"
* **Kết quả tham chiếu (SQL):** Tháng 10: 7,59 tỷ; Tháng 11: 9,37 tỷ; Tháng 12: 8,91 tỷ. Tổng Q4 là 25,88 tỷ.
* **Ghi chú:** đường dẫn `&[2025].&[4]` đi theo key của từng cấp trong `PhanCap_ThoiGian`. Nếu SSAS báo không tìm thấy thành viên, dùng dạng tương đương `[Dim Time].[Month].[Month].MEMBERS` với `WHERE ([Dim Time].[Year].&[2025], [Dim Time].[Quarter].&[4])`.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Time].[PhanCap_ThoiGian].[Year].&[2025].&[4].CHILDREN ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 44: Doanh thu sản phẩm tại riêng "Miền Nam" (mọi năm) — **SLICE** (chiều Địa lý)
* **Người dùng:** Trưởng phòng Kinh doanh.
* **Câu hỏi nghiệp vụ:** "Ở thị trường lớn nhất, nhóm hàng nào bán chạy?"
* **Kết quả tham chiếu (SQL):**
  * Biến tần: 79,39 tỷ.
  * Thiết bị tự động hóa: 28,46 tỷ.
  * Thiết bị điều khiển: 15,42 tỷ.
  * Thiết bị truyền tải điện: 5,39 tỷ.
  * Thiết bị đóng cắt: 4,59 tỷ.
  * Vật tư phụ kiện: 4,03 tỷ.
  * Thiết bị đo lường hiển thị: 3,57 tỷ.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Dim Location].[Region].&[Miền Nam] );
```

#### Câu 45: Doanh thu theo năm của riêng kênh "Website Khang Nghị" — **SLICE** (chiều Kênh bán)
* **Người dùng:** Trưởng phòng Kinh doanh.
* **Câu hỏi nghiệp vụ:** "Kênh bán hàng trực tuyến tăng trưởng thế nào qua các năm?"
* **Kết quả tham chiếu (SQL):**
  * 2022: 10,73 tỷ.
  * 2023: 13,10 tỷ.
  * 2024: 16,29 tỷ.
  * 2025: 19,99 tỷ.
```mdx
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
    NON EMPTY [Dim Time].[Year].[Year].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Dim Sales Channel].[Channel Name].&[Website Khang Nghị] );
```

#### Câu 46: Doanh thu sản phẩm theo năm 2024–2025 × Miền Bắc, Miền Trung × kênh Đại lý, Cửa hàng — **DICE** (3 chiều, tập giá trị)
* **Người dùng:** Trưởng phòng Kinh doanh.
* **Câu hỏi nghiệp vụ:** "Trong 2 năm gần nhất, kênh trực tiếp (Đại lý, Cửa hàng) ở thị trường ngoài Miền Nam bán nhóm hàng nào?"
* **Điểm khác Slice:** mỗi chiều được lọc bằng một **tập** nhiều giá trị, nên kết quả là một khối con, không phải một lát cắt.
* **Kết quả tham chiếu (SQL):**
  * Biến tần: 8,66 tỷ.
  * Thiết bị tự động hóa: 3,29 tỷ.
  * Thiết bị điều khiển: 1,84 tỷ.
  * Thiết bị truyền tải điện: 0,89 tỷ.
  * Thiết bị đóng cắt: 0,71 tỷ.
  * Vật tư phụ kiện: 0,65 tỷ.
  * Thiết bị đo lường hiển thị: 0,54 tỷ.
```mdx
SELECT 
    [Measures].[Sales Amount] ON COLUMNS,
    NON EMPTY [Dim Product].[Category].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE (
    { [Dim Time].[Year].&[2024], [Dim Time].[Year].&[2025] },
    { [Dim Location].[Region].&[Miền Bắc], [Dim Location].[Region].&[Miền Trung] },
    { [Dim Sales Channel].[Channel Name].&[Đại lý], [Dim Sales Channel].[Channel Name].&[Cửa hàng] }
);
```

#### Câu 47: Giá trị nhập hàng theo Nhà cung cấp, năm 2025, tại "Kho TP. Hồ Chí Minh" — **DICE** (phân hệ Mua hàng)
* **Người dùng:** Trưởng phòng Mua hàng.
* **Câu hỏi nghiệp vụ:** "Năm 2025, kho chính nhập hàng chủ yếu từ nhà cung cấp nào?"
* **Kết quả tham chiếu (SQL):**

  | Nhà cung cấp | Số lượng | Giá trị |
  | :--- | ---: | ---: |
  | Công ty Thiết bị điện An Thịnh | 10.638 | 23,28 tỷ |
  | Công ty Tự động hóa Đông Thành | 6.293 | 8,11 tỷ |
  | Công ty Kỹ thuật Minh Phát | 7.141 | 4,05 tỷ |
  | Công ty Công nghệ Thiên Phúc | 27.262 | 1,45 tỷ |
  | Công ty Vật tư điện Nam Việt | 5.836 | 1,05 tỷ |
  | Công ty Thiết bị công nghiệp Hưng Gia | 30.904 | 1,04 tỷ |
```mdx
SELECT 
    { [Measures].[Total Cost], [Measures].[Purchase Quantity] } ON COLUMNS,
    ORDER(
        NONEMPTY([Dim Supplier].[Supplier Name].[Supplier Name].MEMBERS, [Measures].[Total Cost]),
        [Measures].[Total Cost], BDESC
    ) ON ROWS
FROM [KhangNghi_Cube]
WHERE (
    [Dim Time].[Year].&[2025],
    [Dim Warehouse].[Warehouse Name].&[Kho TP. Hồ Chí Minh]
);
```

#### Câu 48: Ma trận Năm × Khu vực — **PIVOT**
* **Người dùng:** Giám đốc.
* **Câu hỏi nghiệp vụ:** "Doanh thu từng miền thay đổi thế nào qua các năm?" (Năm trên dòng, Miền trên cột. Đổi trục để xoay góc nhìn.)
* **Kết quả tham chiếu (SQL), tỷ đồng:**

  | Năm | Miền Bắc | Miền Trung | Miền Nam |
  | :--- | ---: | ---: | ---: |
  | 2022 | 6,34 | 7,06 | 24,69 |
  | 2023 | 7,84 | 7,85 | 30,19 |
  | 2024 | 9,11 | 8,99 | 37,35 |
  | 2025 | 12,30 | 13,34 | 48,63 |
```mdx
SELECT 
    NON EMPTY [Dim Location].[Region].[Region].MEMBERS ON COLUMNS,
    NON EMPTY [Dim Time].[Year].[Year].MEMBERS ON ROWS
FROM [KhangNghi_Cube]
WHERE ( [Measures].[Sales Amount] );
```

#### Câu 49: Tăng trưởng doanh thu từng quý 2025 so với cùng kỳ 2024 — **PARALLELPERIOD** (kết hợp Drill-down)
* **Người dùng:** Giám đốc.
* **Câu hỏi nghiệp vụ:** "Quý nào năm 2025 tăng trưởng mạnh nhất so với cùng kỳ năm trước?"
* **Kết quả tham chiếu (SQL):**

  | Quý | 2024 | 2025 | Tăng trưởng |
  | :--- | ---: | ---: | ---: |
  | Q1 | 8,27 tỷ | 11,73 tỷ | +41,89% |
  | Q2 | 13,80 tỷ | 17,38 tỷ | +25,95% |
  | Q3 | 16,19 tỷ | 19,28 tỷ | +19,11% |
  | Q4 | 17,20 tỷ | 25,88 tỷ | +50,49% |
```mdx
WITH 
MEMBER [Measures].[Doanh Thu Cung Ky] AS
    ( [Measures].[Sales Amount],
      PARALLELPERIOD([Dim Time].[PhanCap_ThoiGian].[Year], 1,
                     [Dim Time].[PhanCap_ThoiGian].CURRENTMEMBER) ),
    FORMAT_STRING = "#,##0"
MEMBER [Measures].[Tang Truong YoY] AS
    IIF([Measures].[Doanh Thu Cung Ky] = 0, NULL,
        [Measures].[Sales Amount] / [Measures].[Doanh Thu Cung Ky] - 1),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Doanh Thu Cung Ky], [Measures].[Tang Truong YoY] } ON COLUMNS,
    [Dim Time].[PhanCap_ThoiGian].[Year].&[2025].CHILDREN ON ROWS
FROM [KhangNghi_Cube];
```

#### Câu 50: Tồn kho cuối kỳ của từng kho, khoan sâu theo Danh mục — **DRILL-DOWN + DICE** (phân hệ Kho)
* **Người dùng:** Quản lý kho.
* **Câu hỏi nghiệp vụ:** "Tại kỳ kiểm kê gần nhất, mỗi kho đang giữ bao nhiêu hàng của từng danh mục?"
* **Kết quả tham chiếu (SQL):** kỳ 01/12/2025, mỗi kho có tồn 1.460 đơn vị trên 57 sản phẩm.
```mdx
WITH MEMBER [Measures].[Ton Cuoi Ky] AS
    ( [Measures].[Quantity On Hand],
      TAIL(NONEMPTY([Dim Time].[Full Date].[Full Date].MEMBERS, [Measures].[Quantity On Hand]), 1).ITEM(0) )
SELECT 
    [Measures].[Ton Cuoi Ky] ON COLUMNS,
    NON EMPTY CROSSJOIN(
        [Dim Warehouse].[Warehouse Name].[Warehouse Name].MEMBERS,
        [Dim Product].[Category].[Category].MEMBERS
    ) ON ROWS
FROM [KhangNghi_Cube];
```

---

## HƯỚNG DẪN KIỂM CHỨNG TRÊN SSAS
1. Mở SQL Server Management Studio, kết nối **Analysis Services**, chọn cơ sở dữ liệu của dự án `KhangNghi_SSAS` và **Process Full** sau mỗi lần ETL.
2. Chọn **New Query → MDX**, dán từng câu và chạy (F5).
3. So sánh kết quả với dòng "Kết quả tham chiếu (SQL)". Doanh thu trên SSAS hiển thị bằng đồng, còn số tham chiếu làm tròn theo tỷ đồng.
4. Chụp màn hình các câu đại diện cho 4 thao tác (01, 02, 15, 16), cùng 41–50 nếu cần, để đưa vào Chương 4 của báo cáo.
