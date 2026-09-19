# Hướng dẫn Nhúng (Embed) Báo cáo Power BI vào Web Dashboard

## 1. Kết nối Power BI với SSAS
1. Mở **Power BI Desktop**.
2. Chọn `Get Data` -> `Analysis Services`.
3. Nhập tên server (ví dụ `localhost`) và chọn CSDL `KhangNghi_SSAS_DB`.
4. Chọn chế độ **Connect live** (để query trực tiếp khối OLAP qua MDX, giữ dữ liệu luôn là mới nhất).
5. Thiết kế các báo cáo, biểu đồ kéo thả (Kéo Dimension: Thời gian, Sản phẩm. Kéo Measure: Doanh thu, Lợi nhuận...).
6. Sau khi làm xong, nhấn **Publish** lên **Power BI Service** (yêu cầu tài khoản Pro hoặc Premium).

## 2. Lấy mã nhúng (Embed Code)
Trong portal của Power BI (app.powerbi.com):
1. Mở báo cáo bạn vừa đưa lên.
2. Đi tới `File` -> `Embed report` -> `Website or portal` (hoặc `Publish to web` nếu dữ liệu công khai).
3. Copy đoạn iframe được cung cấp. URL thường có dạng:
   `https://app.powerbi.com/reportEmbed?reportId=xxxx&autoAuth=true&ctid=yyyy`

## 3. Tích hợp vào Web Dashboard
Mở file `D:\VS_source\KLTN\src\KhangNghi.Api\wwwroot\index.html`.
Tìm thẻ `iframe` trong `#tab-powerbi` và dán URL lấy được từ Power BI Service vào thuộc tính `src`:

```html
<iframe title="KhangNghi_SSAS_Report" width="100%" height="100%" 
        src="https://app.powerbi.com/reportEmbed?reportId=YOUR_REPORT_ID" 
        frameborder="0" allowFullScreen="true">
</iframe>
```

Giám đốc chỉ cần đăng nhập vào hệ thống của Khang Nghị, chọn Tab **Báo Cáo Power BI** là có thể thao tác, kéo thả và lọc (Slicer) trực tiếp trên các đồ thị của Power BI được nhúng trên web mà không cần cài phần mềm Power BI.
