# BÁO CÁO NGHIÊN CỨU & TÌM HIỂU VỀ NGÔN NGỮ TRUY VẤN ĐA CHIỀU MDX (MULTIDIMENSIONAL EXPRESSIONS)

**Đề tài Khóa luận:** Xây dựng Hệ thống Data Warehouse & Business Intelligence cho Công ty Khang Nghị  
**Học phần:** Khóa Luận Tốt Nghiệp / Chuyên Đề Business Intelligence  
**Môi trường thử nghiệm & triển khai:** SQL Server Analysis Services (SSAS), .NET Core Backend API, Power BI  
**Khối phân tích thực nghiệm:** `[KhangNghi_Cube]`  

---

## LỜI MỞ ĐẦU & TÍNH CẤP THIẾT

Trong kỷ nguyên chuyển đổi số và dữ liệu lớn, các doanh nghiệp thương mại - dịch vụ như Công ty Khang Nghị phải đối mặt với khối lượng giao dịch kinh doanh khổng lồ mỗi ngày. Các hệ thống cơ sở dữ liệu giao dịch truyền thống (OLTP - Relational Database) vốn được tối ưu cho việc ghi dữ liệu tức thời và duy trì tính toàn vẹn (ACID), nhưng bộc lộ hạn chế nghiêm trọng khi thực hiện các tác vụ phân tích quản trị:
* Các câu truy vấn báo cáo yêu cầu thực hiện nhiều phép nối (`JOIN`) phức tạp giữa hàng chục triệu bản ghi, gây nghẽn cổ chai hệ thống và suy giảm nghiêm trọng hiệu năng tác nghiệp.
* Ngôn ngữ SQL quan hệ truyền thống chỉ xử lý dữ liệu dưới dạng các bảng 2 chiều phẳng (Flat Tables), thiếu khả năng biểu diễn tự nhiên các góc nhìn đa chiều (Multi-dimensional View) như Thời gian theo cây phân cấp (Năm -> Quý -> Tháng -> Ngày), Sản phẩm theo nhóm hàng, hay Khu vực địa lý.

Để giải quyết bài toán này, kiến trúc **Business Intelligence (BI)** và **Kho dữ liệu (Data Warehouse - DWH)** đã ra đời, trong đó công nghệ **Khối đa chiều OLAP (Online Analytical Processing)** cùng ngôn ngữ truy vấn **MDX (Multidimensional Expressions)** đóng vai trò hạt nhân trung tâm. Báo cáo này đi sâu vào việc tìm hiểu lý thuyết, cơ chế hoạt động, cú pháp chuyên sâu của MDX và cách ứng dụng thực tiễn để khai phá dữ liệu trong đồ án Khóa luận tốt nghiệp của Công ty Khang Nghị.

---

## CHƯƠNG 1: TỔNG QUAN VỀ NGÔN NGỮ TRUY VẤN MDX

### 1.1. MDX là gì?
**MDX (Multidimensional Expressions)** là ngôn ngữ truy vấn và tính toán mạnh mẽ được phát triển bởi Microsoft vào năm 1997 (xuất hiện lần đầu trong chuẩn *OLE DB for OLAP* và sau đó được tích hợp sâu vào *Microsoft SQL Server Analysis Services - SSAS*). Về sau, MDX đã trở thành chuẩn công nghiệp thực tế (*de-facto standard*) được hầu hết các nhà cung cấp nền tảng OLAP hàng đầu hỗ trợ (SAP BW, Oracle Essbase, Mondrian/Pentaho, SAS,...).

Khác với SQL được thiết kế cho đại số quan hệ (Relational Algebra), MDX được thiết kế chuyên biệt để truy vấn các khối lập phương đa chiều (**OLAP Cubes**). MDX cho phép người dùng trích xuất dữ liệu, tổng hợp chỉ số KPI và thực hiện các phép xoay lật góc nhìn dữ liệu phức tạp chỉ bằng các cú pháp ngắn gọn, trực quan.

### 1.2. Vị trí của MDX trong Kiến trúc Business Intelligence
Trong luồng kiến trúc tổng thể của đề tài Khang Nghị:
1. **Tầng Nguồn (Data Sources):** CSDL giao dịch (SQL Server / ERP / Excel).
2. **Tầng Tích hợp (ETL - SSIS):** Trích xuất, làm sạch, chuẩn hóa và nạp vào Kho dữ liệu `DWH_KhangNghi` theo mô hình chuẩn Constellation (Star Schema mở rộng).
3. **Tầng Phân tích Đa chiều (SSAS OLAP):** Dữ liệu được nạp vào khối `KhangNghi_Cube`, nơi các phép tính toán tổng hợp (Aggregations) được xử lý trước (Pre-calculation) theo cấu trúc lưu trữ MOLAP.
4. **Tầng Truy vấn (MDX Engine):** Tiếp nhận câu lệnh MDX từ các ứng dụng client, tính toán các độ đo động và trả về ma trận kết quả đa chiều trong vài mili-giây.
5. **Tầng Trực quan hóa & Khai thác (Presentation):** Hệ thống Web Dashboard (.NET Core Backend gọi SSAS qua OleDb/ADOMD.NET) và Power BI báo cáo cho Ban giám đốc.

```
+------------------+       +-------------------+       +--------------------+
|  Data Warehouse  | ----> |  SSAS MOLAP Cube  | <---> | MDX Query Engine   |
| (Fact & Dim RDBMS| (ETL) | (KhangNghi_Cube)  |       |                    |
+------------------+       +-------------------+       +----------+---------+
                                                                  |
                                                                  v
                                                        +-------------------+
                                                        | Web API & Power BI|
                                                        | Visual Dashboard  |
                                                        +-------------------+
```

---

## CHƯƠNG 2: CÁC THỰC THỂ CỐT LÕI TRONG MÔ HÌNH ĐA CHIỀU CỦA MDX

Để hiểu và làm chủ ngôn ngữ MDX, cần nắm vững hệ thống phân cấp thực thể trong khối đa chiều:

```
Cube
  ├── Measure Groups & Measures (Độ đo định lượng)
  └── Dimensions (Chiều phân tích)
        └── Hierarchies (Cây phân cấp)
              └── Levels (Cấp độ)
                    └── Members (Phần tử)
```

### 2.1. Cube (Khối dữ liệu)
Là không gian dữ liệu n chiều lưu trữ toàn bộ các chỉ tiêu định lượng và các trục ngữ cảnh của doanh nghiệp. Trong đồ án, khối trung tâm là `[KhangNghi_Cube]`.

### 2.2. Measures (Độ đo / Chỉ số định lượng)
Là các dữ liệu số học có thể đo lường và tính toán tổng hợp (SUM, COUNT, AVERAGE, MIN, MAX). Ví dụ:
* `[Measures].[Sales Amount]` (Doanh thu bán hàng)
* `[Measures].[Profit Amount]` (Lợi nhuận gộp)
* `[Measures].[Quantity]` (Số lượng hàng hóa bán ra)
* `[Measures].[Total Cost]` (Chi phí mua hàng)
* `[Measures].[Quantity On Hand]` (Số lượng tồn kho)

### 2.3. Dimensions (Chiều phân tích)
Là các trục ngữ cảnh dùng để phân loại, lọc và phân nhóm các Measures. Đồ án xây dựng 13 Dimensions: `Dim Time`, `Dim Product`, `Dim Customer`, `Dim Location`, `Dim Employee`, `Dim Warehouse`, `Dim Shipper`, `Dim Supplier`,...

### 2.4. Hierarchies & Levels (Cây phân cấp & Cấp độ)
Một Dimension thường chứa một hoặc nhiều Hierarchy để mô hình hóa mối quan hệ cha - con (*Parent - Child*):
* `Dim Time Hierarchy:` `Year` $\rightarrow$ `Quarter` $\rightarrow$ `Month` $\rightarrow$ `Day` $\rightarrow$ `Full Date`.
* `Dim Product Hierarchy:` `Category` $\rightarrow$ `Sub Category` $\rightarrow$ `Product Name`.
* `Dim Location Hierarchy:` `Region` $\rightarrow$ `Country` $\rightarrow$ `State` $\rightarrow$ `City`.

### 2.5. Members, Tuples và Sets (Ba khái niệm nền tảng trong MDX)
Đây là 3 khái niệm toán học quyết định cách MDX định vị dữ liệu:
* **Member (Phần tử):** Một giá trị cụ thể trong một chiều phân cấp.  
  *Cú pháp:* `[Dim Time].[Year].&[2026]` hoặc `[Dim Product].[Category].&[Nội thất văn phòng]`
* **Tuple (Bộ tọa độ):** Giao điểm của các phần tử thuộc các chiều khác nhau, xác định một "tế bào" (Cell) duy nhất trong không gian đa chiều. Một Tuple được bao bọc trong cặp ngoặc đơn `(...)`.  
  *Cú pháp:* `([Dim Time].[Year].&[2026], [Dim Location].[Region].&[Miền Nam], [Measures].[Sales Amount])`
* **Set (Tập hợp các Tuple/Member):** Một tập gồm không hoặc nhiều Tuple có cùng cấu trúc chiều. Một Set được bao bọc trong cặp ngoặc nhọn `{...}`.  
  *Cú pháp:* `{[Dim Time].[Year].&[2025], [Dim Time].[Year].&[2026]}`

---

## CHƯƠNG 3: CÚ PHÁP TRUY VẤN MDX TOÀN DIỆN

### 3.1. Cấu trúc câu lệnh MDX cơ bản
Không giống như SQL chỉ trả về một bảng phẳng 2 chiều, MDX hỗ trợ trả về dữ liệu trên nhiều trục tọa độ độc lập:

```mdx
SELECT 
    [Axis_Specification_0] ON COLUMNS,
    [Axis_Specification_1] ON ROWS
FROM [Cube_Name]
WHERE ([Slicer_Specification]);
```

* `ON COLUMNS` (hoặc `ON 0`): Trục hoành (Axis 0).
* `ON ROWS` (hoặc `ON 1`): Trục tung (Axis 1).
* Có thể mở rộng lên `ON PAGES (Axis 2)`, `ON SECTIONS (Axis 3)`, `ON CHAPTERS (Axis 4)`.
* Mệnh đề `WHERE`: Được gọi là **Slicer Axis**, dùng để cắt lọc khối dữ liệu mà không làm xuất hiện chiều đó trên trục hiển thị.

### 3.2. Các hàm điều hướng phân cấp (Hierarchy Navigation Functions)
MDX sở hữu khả năng điều hướng cây phân cấp cực kỳ linh hoạt:
* `.MEMBERS`: Lấy toàn bộ các phần tử thuộc một Level hoặc Dimension.
* `.CHILDREN`: Lấy tất cả các phần tử con trực tiếp của phần tử hiện tại.
* `.PARENT`: Lấy phần tử cha trực tiếp.
* `DESCENDANTS(Member, Level/Distance)`: Lấy toàn bộ các phần tử hậu duệ ở một cấp độ chỉ định bên dưới.

### 3.3. Các hàm lọc, sắp xếp và chọn lọc Top/Bottom
* `TOPCOUNT(Set, Count, Numeric_Expression)`: Lọc ra $N$ phần tử có giá trị độ đo cao nhất.
* `BOTTOMCOUNT(Set, Count, Numeric_Expression)`: Lọc ra $N$ phần tử có giá trị độ đo thấp nhất.
* `ORDER(Set, Expression, ASC | DESC | BASC | BDESC)`: Sắp xếp tập hợp (BDESC = Break hierarchy descending, phá vỡ phân cấp để sắp xếp thuần túy).
* `FILTER(Set, Logical_Expression)`: Lọc tập hợp theo một điều kiện logic định lượng.
* `NON EMPTY`: Tự động loại bỏ các dòng/cột có giá trị rỗng (NULL), tối ưu kích thước bảng kết quả.

### 3.4. Tính toán động với `WITH MEMBER` và `WITH SET`
MDX cho phép tạo các chỉ số đo lường tùy biến (Calculated Measure) ngay tại thời điểm thực thi truy vấn mà không cần can thiệp tái cấu trúc Cube trên server:

```mdx
WITH MEMBER [Measures].[Profit Margin Pct] AS
    IIF([Measures].[Sales Amount] = 0, 0, 
        ([Measures].[Profit Amount] / [Measures].[Sales Amount]) * 100),
    FORMAT_STRING = "0.00%"
SELECT 
    { [Measures].[Sales Amount], [Measures].[Profit Amount], [Measures].[Profit Margin Pct] } ON COLUMNS,
    [Dim Product].[Category].MEMBERS ON ROWS
FROM [KhangNghi_Cube];
```

---

## CHƯƠNG 4: HIỆN THỰC HÓA 4 THAO TÁC OLAP KINH ĐIỂN BẰNG MDX

Trong báo cáo khóa luận, việc chứng minh hệ thống thực hiện được đầy đủ **4 thao tác OLAP cốt lõi** là yêu cầu mang tính bắt buộc:

```
              ┌───────────────┐
              │    ROLL-UP    │ (Gộp thô: Ngày -> Tháng -> Năm)
              └───────▲───────┘
                      │
  ┌───────────┐       │       ┌─────────────┐
  │   SLICE   │ ◄─────┼─────► │    DICE     │
  │ (Cắt 1D)  │       │       │  (Cắt đa D) │
  └───────────┘       │       └─────────────┘
                      ▼
              ┌───────────────┐
              │  DRILL-DOWN   │ (Khoan sâu: Năm -> Quý -> Tháng)
              └───────────────┘
                      ↺
              ┌───────────────┐
              │     PIVOT     │ (Xoay 90 độ các trục tọa độ)
              └───────────────┘
```

### 4.1. Thao tác Roll-up (Cuộn gộp / Tổng quát hóa)
* **Bản chất:** Chuyển mức độ quan sát từ hạt mịn (Fine Grain) lên hạt thô (Coarse Grain) dọc theo cây phân cấp.
* **Ý nghĩa:** Ban giám đốc xem bức tranh tổng thể theo từng Năm thay vì theo dõi từng Ngày lẻ tẻ.
* **Câu lệnh MDX:**
  ```mdx
  SELECT 
      [Measures].[Sales Amount] ON COLUMNS,
      [Dim Time].[Year].MEMBERS ON ROWS
  FROM [KhangNghi_Cube];
  ```

### 4.2. Thao tác Drill-down (Khoan sâu / Chi tiết hóa)
* **Bản chất:** Ngược lại với Roll-up, đi từ cấp tổng quan xuống cấp chi tiết bên dưới để truy vết nguyên nhân (*Root Cause Analysis*).
* **Ý nghĩa:** Khoan từ một danh mục hàng hóa xuống chi tiết từng sản phẩm cụ thể nhằm nhận diện mặt hàng bán chạy nhất.
* **Câu lệnh MDX:**
  ```mdx
  SELECT 
      { [Measures].[Sales Amount], [Measures].[Profit Amount] } ON COLUMNS,
      DESCENDANTS([Dim Product].[Category].&[Nội thất văn phòng], [Dim Product].[Product Name]) ON ROWS
  FROM [KhangNghi_Cube];
  ```

### 4.3. Thao tác Slice (Cắt lát)
* **Bản chất:** Cố định **duy nhất 1 chiều** tại một giá trị xác định (chiếu khối 3D thành mặt cắt 2D).
* **Ý nghĩa:** Chỉ xem doanh số của các danh mục sản phẩm tại riêng thị trường "Miền Nam".
* **Câu lệnh MDX:**
  ```mdx
  SELECT 
      [Measures].[Sales Amount] ON COLUMNS,
      [Dim Product].[Category].MEMBERS ON ROWS
  FROM [KhangNghi_Cube]
  WHERE ([Dim Location].[Region].&[Miền Nam]);
  ```

### 4.4. Thao tác Dice (Chia khối đa chiều / Cắt hạt lựu)
* **Bản chất:** Cắt khối dữ liệu con (Subcube) bằng cách lọc đồng thời trên **nhiều chiều khác nhau**.
* **Ý nghĩa:** Phân tích doanh số tại thị trường "Miền Nam" VÀ trong mốc thời gian "Năm 2026".
* **Câu lệnh MDX:**
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

### 4.5. Thao tác Pivot (Xoay chiều / Hoán đổi góc nhìn)
* **Bản chất:** Giữ nguyên dữ liệu tổng hợp nhưng hoán đổi vị trí các trục tọa độ (chuyển một Dimension từ Rows sang Columns hoặc ngược lại).
* **Ý nghĩa:** Đổi góc nhìn từ so sánh vùng miền trên từng loại sản phẩm sang so sánh tỷ trọng sản phẩm tại mỗi vùng miền.
* **Câu lệnh MDX:**
  ```mdx
  -- Góc nhìn: Cột là Vùng miền, Hàng là Danh mục sản phẩm
  SELECT 
      [Dim Location].[Region].MEMBERS ON COLUMNS,
      [Dim Product].[Category].MEMBERS ON ROWS
  FROM [KhangNghi_Cube]
  WHERE ([Measures].[Sales Amount]);
  ```

---

## CHƯƠNG 5: SO SÁNH CHUYÊN SÂU GIỮA MDX VÀ SQL

| Tiêu chí so sánh | SQL (Structured Query Language) | MDX (Multidimensional Expressions) |
| :--- | :--- | :--- |
| **Mô hình dữ liệu cơ sở** | CSDL quan hệ RDBMS (Bảng 2 chiều gồm Dòng và Cột phẳng). | Khối dữ liệu đa chiều (Hypercube gồm Measures, Dimensions, Hierarchies). |
| **Hệ trục trả về** | Chỉ trả về duy nhất 1 tập kết quả dạng bảng phẳng 2 chiều. | Hỗ trợ nhiều trục độc lập (`COLUMNS`, `ROWS`, `PAGES`, `SECTIONS`,...). |
| **Cơ chế tính toán tổng hợp** | Thực thi quét dòng, `SUM`, `GROUP BY`, `JOIN` động tại thời điểm chạy. | Dữ liệu tổng hợp đã được tiền tính toán trước trong khối MOLAP (*Pre-aggregated*). |
| **Hiệu năng với CSDL lớn** | Tốc độ chậm rõ rệt khi bảng Fact có hàng triệu dòng và nối nhiều bảng Dim. | Phản hồi gần như tức thời (< 1 giây) nhờ bộ đệm phân tích chuyên dụng của SSAS. |
| **Khả năng điều hướng phân cấp** | Rất khó khăn, đòi hỏi dùng đệ quy CTE (`WITH RECURSIVE`) phức tạp. | Hỗ trợ tự nhiên qua các hàm tích hợp: `.CHILDREN`, `.PARENT`, `DESCENDANTS()`. |
| **Tính toán kỳ phân tích (Time Series)** | Phức tạp, phải dùng nhiều kỹ thuật Window Functions (`LAG`, `LEAD`, `OVER`). | Rất mạnh mẽ nhờ hàm phân tích thời gian chuyên sâu: `PARALLELPERIOD()`, `YTD()`. |
| **Mục đích chính** | Phục vụ hệ thống giao dịch tác nghiệp (OLTP) và các luồng biến đổi ETL. | Phục vụ phân tích kinh doanh đa chiều (OLAP), Dashboard và hỗ trợ ra quyết định. |

---

## CHƯƠNG 6: KẾT NỐI VÀ THỰC THI TRUY VẤN MDX TRONG ỨNG DỤNG THỰC TẾ

### 6.1. Kiến trúc kết nối trong Backend C# (.NET Core Web API)
Trong đồ án, để hiển thị dữ liệu trực quan lên Dashboard cho người dùng cuối mà không phụ thuộc vào công cụ SSMS, hệ thống đã cài đặt lớp kết nối `SsasRepository.cs` sử dụng OleDb Provider kết nối tới máy chủ SSAS:

```csharp
// Chuỗi kết nối dịch vụ SSAS
string connString = "Provider=MSOLAP;Data Source=localhost;Initial Catalog=KhangNghi_SSAS;Integrated Security=SSPI;";

public async Task<List<Dictionary<string, object?>>> ExecuteMdxQueryAsync(string mdxQuery)
{
    var result = new List<Dictionary<string, object?>>();
    using var conn = new OleDbConnection(connString);
    await conn.OpenAsync();

    using var cmd = new OleDbCommand(mdxQuery, conn);
    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
        result.Add(row);
    }
    return result;
}
```

### 6.2. Cung cấp API tương tác MDX
Hệ thống mở endpoint tại `POST /api/v1/ssas/query` cho phép phía Client Frontend hoặc các dịch vụ phân tích gửi câu truy vấn MDX tùy ý và nhận về phản hồi định dạng JSON tốc độ cao.

---

## CHƯƠNG 7: ĐÁNH GIÁ ƯU NHƯỢC ĐIỂM VÀ KẾT LUẬN

### 7.1. Ưu điểm nổi bật của MDX
1. **Tốc độ vượt trội:** Giảm thời gian xử lý các báo cáo tài chính, doanh thu từ hàng phút (với SQL) xuống dưới 1 giây.
2. **Khả năng mô hình hóa nghiệp vụ tự nhiên:** Thể hiện trực quan cấu trúc tổ chức, danh mục ngành hàng và niên độ kế toán của công ty.
3. **Linh hoạt trong mở rộng chỉ số:** Khả năng định nghĩa độ đo động (`WITH MEMBER`) giúp doanh nghiệp tùy biến công thức KPI mà không cần nạp lại CSDL.

### 7.2. Thách thức và Nhược điểm
1. **Đường cong học tập (Learning Curve):** Tư duy truy vấn trên không gian tọa độ đa chiều phức tạp hơn đáng kể so với tư duy bảng phẳng của SQL.
2. **Chi phí tài nguyên:** Khối MOLAP đòi hỏi tài nguyên RAM và dung lượng đĩa lớn để lưu trữ các bảng chỉ mục và dữ liệu tiền tổng hợp.

### 7.3. Kết luận
Ngôn ngữ MDX là mảnh ghép công nghệ không thể thiếu để hoàn thiện đồ án Kho dữ liệu & Business Intelligence cho Công ty Khang Nghị. Việc nắm vững MDX không chỉ giúp sinh viên đáp ứng đầy đủ yêu cầu học thuật của Hội đồng chấm khóa luận (thực hiện trọn vẹn 4 thao tác OLAP, xử lý các bài toán Top-N, cảnh báo tồn kho, tính toán tỷ suất), mà còn trang bị năng lực thực tiễn vững chắc trong thiết kế và phát triển các hệ thống phân tích dữ liệu doanh nghiệp quy mô lớn.
