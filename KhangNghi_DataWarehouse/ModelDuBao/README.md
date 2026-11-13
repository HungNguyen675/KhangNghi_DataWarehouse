\# Dịch vụ dự báo doanh thu Khang Nghị



\## Chức năng



Dịch vụ FastAPI cung cấp dự báo doanh thu toàn công ty bằng Prophet.

Ứng dụng Web gọi dịch vụ này thông qua ASP.NET Core.



Mô hình được huấn luyện trên dữ liệu từ 01/01/2022 đến 31/12/2025.

Khoảng dự báo hiện tại là 01/01/2026–30/06/2026.

Kết quả là dự báo điểm, chưa cung cấp khoảng dự báo.



\## Môi trường đã chạy



\- Windows.

\- Python 3.14.4.

\- Phiên bản thư viện chính được lưu trong requirements.txt.



\## Khởi chạy trên máy hiện tại



Nhấp đúp run\_forecast.cmd và giữ cửa sổ mở.



Địa chỉ kiểm tra:

http://127.0.0.1:8001/



Tài liệu API:

http://127.0.0.1:8001/docs



Ứng dụng Web chính phải được khởi chạy riêng.

Không mở thêm một dịch vụ khác cùng cổng 8001.



\## Chuẩn bị trên máy mới



Mở PowerShell tại thư mục ModelDuBao và chạy:



&#x20;   python -m venv .venv

&#x20;   .\\.venv\\Scripts\\python.exe -m pip install -r requirements.txt



Sau khi cài đặt, chạy run\_forecast.cmd.



Cần có kết nối Internet để tải thư viện.

Bộ cài này chưa được kiểm chứng trên một máy sạch.



\## Các đường dẫn API



\- GET /: kiểm tra dịch vụ.

\- GET /forecast?months=3: dự báo 3 tháng.

\- GET /forecast?months=6: dự báo 6 tháng.

\- GET /forecast/detail: dự báo theo ngày, tuần hoặc tháng.

\- GET /models/comparison: kết quả so sánh mô hình.



Ví dụ dự báo chi tiết:



http://127.0.0.1:8001/forecast/detail?granularity=week\&start\_date=2026-01-01\&end\_date=2026-03-31



\## Các tệp quan trọng



\- forecast\_api.py: mã nguồn dịch vụ.

\- saved\_model/prophet\_web\_model.pkl: gói mô hình API đang sử dụng.

\- saved\_model/prophet\_model.json: mô hình nguồn để tái đóng gói.

\- package\_model.py: đóng gói và kiểm tra tính nhất quán của mô hình.

\- evaluation\_data\_2022\_2025.csv: doanh thu ngày xuất từ kho dữ liệu.

\- evaluate\_models.py: chạy lại đánh giá mô hình.

\- hybrid\_model.py: mã huấn luyện và dự báo Hybrid.

\- dwh\_pipeline.py: mã pipeline gốc; còn cấu hình kết nối cũ,

&#x20; không chạy trực tiếp nếu chưa điều chỉnh.

\- evaluation\_20261112\_110609\_637382: kết quả đánh giá API đang đọc.



Tệp PKL chứa mô hình được tuần tự hóa bằng JSON của Prophet

cùng thông tin phiên bản. Chỉ nạp tệp PKL do nhóm tạo và tin cậy.



\## Đánh giá mô hình



Đánh giá tại ba mốc chốt:



\- 31/12/2024.

\- 31/03/2025.

\- 30/06/2025.



Hai kỳ hạn dự báo là 3 và 6 tháng.

MAE và RMSE được tính trên doanh thu tổng hợp theo tháng.

Bảng tổng hợp là trung bình các chỉ số tại ba mốc chốt.



Prophet có MAE và RMSE trung bình thấp hơn Hybrid ở cả hai kỳ hạn

trong lần đánh giá này và được tiếp tục sử dụng trên Web.



Đường kiểm định năm 2025 ghép:

\- Tháng 1–6: dự báo từ mốc 31/12/2024.

\- Tháng 7–12: dự báo từ mốc 30/06/2025.



\## Chạy lại đánh giá



Tại thư mục ModelDuBao, chạy:



&#x20;   python evaluate\_models.py



Chương trình đọc CSV đã lưu và tạo thư mục kết quả mới.

Không ghi đè mô hình đang phục vụ Web.

API không tự chuyển sang thư mục kết quả mới.



\## Kết quả dự báo tham chiếu



\- Tổng 3 tháng đầu năm 2026: 14.967.984.679,32 đồng.

\- Tổng 6 tháng đầu năm 2026: 37.035.028.510,67 đồng.



\## Lưu ý khi sao chép và bàn giao



Giữ nguyên cấu trúc thư mục, đặc biệt saved\_model và thư mục

kết quả đánh giá đang được khai báo trong forecast\_api.py.



API dự báo đọc mô hình và kết quả đã lưu, không cần kết nối SQL Server.

Việc hiển thị doanh thu thực tế trên Web vẫn cần hệ thống dữ liệu tương ứng.

