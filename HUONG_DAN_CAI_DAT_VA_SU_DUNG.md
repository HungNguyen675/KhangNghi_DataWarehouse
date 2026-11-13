Được, **chỉ chuẩn bị hướng dẫn, không thử phục hồi**.

Bạn tạo tệp **`HUONG_DAN_CAI_DAT_VA_SU_DUNG.md`** tại:

```text
E:\CNTT_KLCN235_Nguyễn Trường Hưng
```

Dán nội dung sau vào tệp:

---

# HƯỚNG DẪN CÀI ĐẶT VÀ SỬ DỤNG HỆ THỐNG KHANG NGHỊ

## 1. Thành phần bàn giao

| Thư mục | Nội dung |
|---|---|
| `CSDL_BanGiao` | Năm bản sao lưu SQL Server, bản sao lưu SSAS và script tạo lịch ETL |
| `KhangNghi_DataWarehouse\KhangNghi_ETL` | Mã nguồn các package SSIS |
| `KhangNghi_DataWarehouse\KhangNghi_SSAS` | Mã nguồn thiết kế các chiều và cube |
| `KhangNghi_DataWarehouse\KhangNghi_DesktopApp` | Mã nguồn Desktop và ETL Runner |
| `KhangNghi_DataWarehouse\BanChay_Desktop` | Desktop đã đóng gói |
| `KhangNghi_DataWarehouse\ModelDuBao` | Mô hình Prophet, dịch vụ dự báo và kết quả đánh giá |
| `KhangNghi_WebBI\WebProject` | Mã nguồn Web |
| `KhangNghi_WebBI\BanChay_Web` | Web đã đóng gói |

Hai tệp Power BI nằm trong `KhangNghi_DataWarehouse`:

- `BaoCao_KinhDoanh.pbix`
- `BaoCao_TonKho.pbix`

Giữ nguyên các tệp thư viện đi kèm trong thư mục chương trình đóng gói.

## 2. Phần mềm cần chuẩn bị

- Windows 64-bit.
- SQL Server Database Engine và SQL Server Management Studio.
- SQL Server Analysis Services ở chế độ Multidimensional.
- SQL Server Integration Services và chương trình DTExec để chạy ETL.
- Các provider kết nối được package sử dụng, trong đó bản hiện tại có `SQLNCLI11.1`; kết nối SSAS sử dụng MSOLAP.
- Python và các thư viện trong `ModelDuBao\requirements.txt`.
- Microsoft Edge WebView2 Runtime nếu chức năng nhúng của Desktop yêu cầu.
- Power BI Desktop để mở các báo cáo `.pbix`.

Visual Studio và các extension SSIS/SSAS cần thiết khi mở, chỉnh sửa hoặc triển khai lại từ project.

Bản Desktop và Web đã publish kèm .NET runtime cho Windows 64-bit. Các dịch vụ SQL Server, SSAS, SSIS, provider và Python được chuẩn bị riêng.

## 3. Phục hồi năm cơ sở dữ liệu SQL Server

Thực hiện trên máy nhận trong SSMS:

1. Kết nối đến Database Engine.
2. Nhấp phải `Databases` → `Restore Database…`.
3. Chọn `Source → Device`, thêm tệp `.bak`.
4. Giữ tên cơ sở dữ liệu đích đúng như bảng bên dưới.
5. Trong trang `Files`, điều chỉnh vị trí tệp dữ liệu và tệp log cho phù hợp máy nhận.
6. Kiểm tra thông tin rồi thực hiện phục hồi.

| Tệp sao lưu bắt đầu bằng | Tên CSDL đích |
|---|---|
| `KhangNghi_OLTP_VN_DRAFT_` | `KhangNghi_OLTP_VN_DRAFT` |
| `KhangNghi_Purchasing_OLTP_VN_DRAFT_` | `KhangNghi_Purchasing_OLTP_VN_DRAFT` |
| `KhangNghi_Staging_` | `KhangNghi_Staging` |
| `DWH_KhangNghi_` | `DWH_KhangNghi` |
| `KhangNghi_AppManagement_` | `KhangNghi_AppManagement` |

Không chọn ghi đè một CSDL đã tồn tại trên máy nhận nếu chưa xác định rõ mục đích.

Tài khoản dùng để chạy ứng dụng, ETL và xử lý SSAS cần được cấp quyền phù hợp sau khi phục hồi. Các login cấp máy chủ và SQL Server Agent job không tự được khôi phục chỉ bằng năm tệp này.

## 4. Phục hồi hoặc triển khai SSAS

### Cách 1: Dùng bản sao lưu

1. Trong SSMS, kết nối `Analysis Services`.
2. Nhấp phải `Databases` → `Restore…`.
3. Chọn tệp `CSDL_BanGiao\KhangNghi_SSAS.abf`.
4. Đặt tên cơ sở dữ liệu là `KhangNghi_SSAS`.
5. Kiểm tra các tùy chọn bảo mật và vị trí lưu trữ phù hợp máy nhận, rồi phục hồi.
6. Kiểm tra nguồn dữ liệu của SSAS đang trỏ đến `DWH_KhangNghi` trên đúng Database Engine.
7. Cấu hình quyền truy cập nguồn và quyền truy vấn SSAS cho tài khoản sử dụng.

### Cách 2: Triển khai từ project

1. Mở solution trong `KhangNghi_DataWarehouse\KhangNghi_SSAS`.
2. Cập nhật kết nối nguồn dữ liệu `DWH_KhangNghi`.
3. Cập nhật máy chủ triển khai trong cấu hình project.
4. Deploy project.
5. Process các chiều và cube.

Hệ thống có ba cube:

- `KhangNghi_Cube`
- `Cube_Sales`
- `Cube_Inventory`

Sau khi ETL thay đổi dữ liệu trong DWH, cần xử lý lại SSAS để cập nhật dữ liệu phân tích.

## 5. Cấu hình ứng dụng trên máy nhận

Các cấu hình bàn giao hiện sử dụng tên máy và đường dẫn của máy phát triển. Nếu máy nhận khác, cần cập nhật trước khi chạy.

### Desktop

Mở `BanChay_Desktop\appsettings.json`, kiểm tra:

- `DwhConnection`: kết nối `DWH_KhangNghi`.
- `AppManagementConnection`: kết nối `KhangNghi_AppManagement`.
- `SsasConnection`: kết nối `KhangNghi_SSAS`.
- `SqlAgentConnection`: Database Engine chứa SQL Server Agent job.
- `MasterPackagePath`: đường dẫn `Master_ETL.dtsx`.
- `DtExecPath`: đường dẫn DTExec trên máy nhận.
- `RunnerPath`, `RunnerConfigPath`: đường dẫn ETL Runner và cấu hình.
- `BackupDirectory`: thư mục lưu bản sao lưu sau nạp.
- Các đường dẫn báo cáo Power BI.

Nếu tiếp tục phát triển mã nguồn, cập nhật cấu hình tương ứng trong project Desktop.

### ETL Runner

Mở:

```text
KhangNghi_DataWarehouse\KhangNghi_DesktopApp\deploy\runner.agent.json
```

Cập nhật kết nối CSDL, đường dẫn DTExec, Master package, máy chủ SSAS và thư mục sao lưu.

### SSIS

Mở project `KhangNghi_ETL`, kiểm tra các Connection Manager đến:

- Hai cơ sở dữ liệu nguồn.
- `KhangNghi_Staging`.
- `DWH_KhangNghi`.

Điều chỉnh tên máy chủ, phương thức xác thực và thông tin kết nối theo máy nhận.

### Web

Mở `KhangNghi_WebBI\BanChay_Web\appsettings.json`, cấu hình các kết nối CSDL và SSAS phù hợp.

Dịch vụ dự báo Python được Web API gọi tại:

```text
http://127.0.0.1:8001/
```

Do đó, với cấu hình hiện tại, Python cần chạy trên cùng máy với Web API.

## 6. Chuẩn bị dịch vụ dự báo

Môi trường Python đã chạy trên máy phát triển là Python 3.14.4.

Mở PowerShell tại thư mục `ModelDuBao` và chạy:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

Sau khi cài đặt, chạy:

```text
run_forecast.cmd
```

Kiểm tra dịch vụ tại:

```text
http://127.0.0.1:8001/
```

Giữ cửa sổ dịch vụ mở khi sử dụng dự báo trên Web.

Các tệp cần giữ nguyên:

- `saved_model\prophet_web_model.pkl`
- `saved_model\prophet_model.json`
- `evaluation_20261112_110609_637382`
- `forecast_api.py`

API đang sử dụng mô hình đã lưu, không tự huấn luyện lại khi khởi động.

Phạm vi dự báo hiện tại là từ ngày 01/01/2026 đến 30/06/2026.

## 7. Khởi chạy chương trình

### Desktop

Mở:

```text
KhangNghi_DataWarehouse\BanChay_Desktop\KhangNghi.DesktopAdmin.exe
```

Đăng nhập bằng tài khoản quản trị đã được bàn giao.

Các chức năng chính:

- Xem trạng thái kho dữ liệu.
- Cập nhật kho dữ liệu qua SSIS.
- Cập nhật dữ liệu báo cáo bằng xử lý SSAS.
- Quản lý tài khoản, phân quyền và lịch chạy.

Chế độ nạp lần đầu có thao tác xóa dữ liệu đích để nạp lại; không sử dụng chỉ để xem thử chương trình.

### Web

Mở PowerShell tại thư mục `KhangNghi_WebBI\BanChay_Web`, chạy:

```powershell
.\KhangNghi.Api.exe --urls http://localhost:5000
```

Giữ cửa sổ mở, truy cập:

```text
http://localhost:5000
```

Đăng nhập bằng tài khoản Web đã được bàn giao.

Để dùng chức năng dự báo, đồng thời chạy `ModelDuBao\run_forecast.cmd`.

Cấu hình AI Copilot được bổ sung riêng nếu cần sử dụng chức năng này.

## 8. Thiết lập lịch ETL

Tệp tạo job:

```text
CSDL_BanGiao\06_TaoLich_ETL.sql
```

Trước khi chạy trên máy nhận:

1. Điều chỉnh đường dẫn Runner và tệp cấu hình.
2. Điều chỉnh tài khoản chủ sở hữu job.
3. Chuẩn bị credential và proxy `KhangNghi_EtlProxy`, cấp quyền chạy CmdExec và quyền truy cập cần thiết.
4. Đảm bảo SQL Server Agent hoạt động.
5. Kiểm tra job chưa tồn tại trước khi chạy script.

Script bàn giao đặt job và lịch ở trạng thái tắt. Chỉ bật lịch sau khi cấu hình hoàn tất và xác định thời điểm chạy phù hợp.

## 9. Trình tự vận hành dữ liệu

```text
Hai CSDL nguồn
    → SSIS nạp Staging
    → SSIS nạp Dimension
    → SSIS nạp Fact
    → DWH_KhangNghi
    → Process SSAS
    → Web và báo cáo phân tích
```

Desktop có hai thao tác riêng:

- **Cập nhật kho dữ liệu:** chạy luồng ETL.
- **Cập nhật dữ liệu báo cáo:** xử lý SSAS.

Dịch vụ dự báo sử dụng mô hình đã huấn luyện; dữ liệu mới nạp vào DWH không tự làm mô hình được huấn luyện lại.

## 10. Trạng thái kiểm tra khi bàn giao

- Desktop đã publish và chạy được trên máy phát triển.
- Web đã publish và kiểm tra hoạt động trên máy phát triển.
- Dịch vụ dự báo đã kết nối với Web.
- Mô hình `.pkl` đã được kiểm tra cho kết quả khớp mô hình JSON trên 181 ngày dự báo.
- Năm bản sao lưu SQL Server đã vượt qua `RESTORE VERIFYONLY`.
- Bản sao lưu SSAS đã được tạo.
- Chưa thử phục hồi hoặc cài đặt trên máy khác.

---

**Không cần thực hiện các bước phục hồi trên máy hiện tại.** Đây là tài liệu để người nhận sử dụng khi triển khai.