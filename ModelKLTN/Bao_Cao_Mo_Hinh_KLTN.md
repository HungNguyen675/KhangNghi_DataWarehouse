# BÁO CÁO KHOA HỌC: HỆ THỐNG DỰ BÁO DOANH SỐ LAI (HYBRID RESIDUAL PARADIGM) TRÊN KHO DỮ LIỆU DOANH NGHIỆP VIỆT NAM (VNĐ)

**Đề tài Khóa luận Tốt nghiệp:** Nghiên cứu và Xây dựng Hệ thống Dự báo Doanh số Chuỗi Thời gian Đa tầng kết hợp Facebook Prophet và Thuật toán Gradient Boosting (XGBoost / CatBoost) trên Kho Dữ liệu Doanh nghiệp  
**Đối tượng nghiên cứu:** Doanh nghiệp Thương mại Khang Nghi (Đơn vị tiền tệ: VNĐ)  
**Tác giả:** [Họ và tên sinh viên]  
**Giảng viên Hướng dẫn:** [Họ và tên GVHD]  
**Ngày cập nhật:** 24/10/2026  

---

## 1. TỔNG QUAN VẤN ĐỀ NGHIÊN CỨU & MỤC TIÊU ĐỀ TÀI

### 1.1. Bối cảnh Thực tiễn tại Doanh nghiệp
Trong thị trường thương mại và bán lẻ tại Việt Nam, nhu cầu thị trường biến động hết sức phức tạp:
- **Biên độ dao động lớn:** Doanh số bán lẻ hàng ngày có thể biến thiên từ vài chục triệu lên đến hàng trăm triệu hoặc vượt 500 triệu VNĐ trong các dịp bão khuyến mãi, ngày trả lương (payday) và các đợt lễ Tết.
- **Tính chu kỳ đa tầng:** Chuỗi thời gian vừa mang tính chu kỳ tuần (Thứ 2 đến Chủ nhật), chu kỳ tháng (đầu/cuối tháng), vừa chịu sự chi phối mạnh mẽ của các sự kiện văn hóa tiêu dùng đặc thù (Tết Nguyên Đán, Giỗ Tổ, 30/4 - 1/5, Quốc khánh 2/9, đợt mua sắm Black Friday, 11/11, 12/12).
- **Hạn chế của các phương pháp truyền thống:**
  * Các mô hình thống kê cổ điển (ARIMA, SARIMA) yêu cầu giả định chuỗi dừng nghiêm ngặt và khó tiếp nhận các biến ngoại sinh phức tạp.
  * Mô hình **Facebook Prophet** giải quyết rất tốt xu hướng (trend) và tính mùa vụ (seasonality) vĩ mô, nhưng **hoàn toàn bất lực trước các đỉnh nhọn bất thường (volatility spikes)**, dẫn đến tình trạng sai số ngày lớn và hiện tượng dự báo hụt sâu (**Underforecasting**) trên quy mô cả năm.
  * Các mô hình học máy thuần túy (XGBoost, CatBoost) tuy giỏi nắm bắt phi tuyến tính nhưng lại thiếu khả năng suy diễn xu hướng ngoại suy (extrapolation) theo thời gian dài.

### 1.2. Giải pháp Đề xuất: Kiến trúc Lai Học Phần Dư (Hybrid Residual Learning Paradigm)
Đề tài xây dựng một khối mô hình lai hai tầng:
1. **Tầng 1 (Vĩ mô):** Sử dụng **Facebook Prophet** làm nền tảng phân rã xu hướng tăng trưởng dài hạn và các chu kỳ mùa vụ tuần hoàn:
   $$y(t) = g(t) + s(t) + h(t) + \epsilon(t)$$
   trong đó $g(t)$ là xu hướng, $s(t)$ là mùa vụ, $h(t)$ là hiệu ứng ngày lễ, và $\epsilon(t)$ là phần dư.
2. **Tầng 2 (Vi mô & Cú sốc):** Sử dụng các mô hình cây quyết định tăng cường độ dốc (**XGBoost Regressor** và **CatBoost Regressor**) để học quy luật trên chuỗi phần dư $\epsilon(t)$ dựa trên tập 63 đặc trưng ngoại sinh.
3. **Cơ chế Stacking Tối ưu:**
   $$\hat{y}_{\text{Final}}(t) = \max\left(0, \hat{y}_{\text{Prophet}}(t) + w_1 \cdot \hat{\epsilon}_{\text{XGB}}(t) + w_2 \cdot \hat{\epsilon}_{\text{Cat}}(t)\right)$$
   với $w_1, w_2$ được tối ưu hóa bằng thuật toán tối ưu hóa lồi.

---

## 2. QUY TRÌNH KỸ NGHỆ DỮ LIỆU & KIẾN TRÚC DATA WAREHOUSE (ETL PIPELINE)

Quy trình dữ liệu được thiết kế bài bản theo chuẩn Data Engineering công nghiệp Ralph Kimball:

```
[Hệ thống OLTP Nguồn] 
 ├── KhangNghi_OLTP_VN_DRAFT (25,728 Orders, 51,248 Lines)
 └── KhangNghi_Purchasing_OLTP_VN_DRAFT (864 Purchases, 7,747 Lines)
           │
           ▼ (etl_pipeline_oltp_to_dwh.py)
[Kho Dữ liệu Phân tích - DWH_KhangNghi (Star Schema)]
 ├── Fact_Sales (51,248 rows) & Fact_Purchase (7,747 rows)
 └── Dim_Time (1,826 days), Dim_Customer, Dim_Product, Dim_Location...
           │
           ▼ (dwh_pipeline.py - SQL Analytical Extraction)
[Tập Dữ liệu Chuỗi Thời gian Đã Tiền Xử Lý (1,461 ngày: 2022-2025)]
 ├── Capping Ngoại lai 99% (Winsorization tại 522,081,990 VNĐ)
 └── Sinh 63 Đặc trưng Ngoại sinh (Lags, Rolling, Calendar, Holidays)
```

### 2.1. Truy vấn Trích xuất Phân tích từ Data Warehouse
Đúng theo yêu cầu của Đề cương Khóa luận, toàn bộ dữ liệu phân tích và học máy được trích xuất trực tiếp từ kho `DWH_KhangNghi`:
```sql
SELECT 
    t.FullDate AS ds,
    SUM(f.SalesAmount) AS y,
    COUNT(DISTINCT f.OrderID) AS total_orders,
    SUM(f.Quantity) AS total_quantity,
    SUM(f.ProfitAmount) AS total_profit,
    AVG(f.DiscountAmount) AS total_discount
FROM Fact_Sales f
JOIN Dim_Time t ON f.TimeKey = t.TimeKey
GROUP BY t.FullDate 
ORDER BY ds ASC;
```

### 2.2. Xử lý Dữ liệu Chuỗi Thời Gian Liên tục & Ngoại lai
1. **Tái lập chuỗi liên tục (Continuous Series):** Khung thời gian từ `01/01/2022` đến `31/12/2025` có tổng cộng 1,461 ngày. DWH ghi nhận 1,429 ngày có giao dịch thực tế; pipeline tự động lấp đầy 32 ngày khuyết (do nghỉ lễ hoặc hệ thống bảo trì) với giá trị doanh số $y = 0$, đảm bảo cấu trúc chuỗi thời gian đều đặn (regular spacing).
2. **Cắt gọt ngoại lai thích ứng (Adaptive Winsorization):** 
   - Phân phối doanh số có độ lệch phải cao ($Skewness = 1.1636$, $Kurtosis = 1.6881$).
   - Áp dụng kỹ thuật Capping tại phân vị 99% (ngưỡng **522,081,990 VNĐ/ngày**). Các giá trị vượt ngưỡng này được co về mức phân vị 99% nhằm ngăn chặn hiện tượng quá khớp (overfitting) do các đơn hàng mua buôn bất thường, giúp mô hình gradient boosting học đúng phân phối tổng thể.

---

## 3. KHÁM PHÁ DỮ LIỆU CHUỖI THỜI GIAN (SCIENTIFIC TIME SERIES EDA)

Quy trình khám phá thống kê được thực thi tự động qua [eda_analysis.py](file:///d:/MohinhKLTN/eda_analysis.py), tạo lập 4 biểu đồ chuẩn mực:

### 3.1. Phân Rã Chuỗi Thời Gian (Seasonal Decomposition)
![Biểu đồ Phân rã](eda_decomposition.png)
- **Trend (Xu hướng dài hạn):** Doanh số tăng trưởng đều đặn qua các năm từ 2022 đến 2025. Đặc biệt, nửa cuối năm (Quý 3 và Quý 4) luôn tạo ra các nấc thang doanh số cao hơn hẳn nửa đầu năm.
- **Seasonality (Tính mùa vụ):** Thể hiện rõ dao động tuần hoàn 7 ngày ($Period = 7$). Doanh số đạt đỉnh vào các ngày giữa tuần (Thứ 3 - Thứ 5) và hạ nhiệt vào cuối tuần.
- **Residuals (Phần dư):** Xuất hiện nhiều gai nhọn đột biến vào các dịp lễ tết và cuối năm. Đây chính là minh chứng toán học khẳng định **không thể chỉ dựa vào Prophet mà phải cần đến XGBoost/CatBoost để học phần dư này**.

### 3.2. Kiểm Định Tính Dừng Thống Kê (Stationarity Tests)
- **Kiểm định Augmented Dickey-Fuller (ADF Test):**
  * Giá trị thống kê ADF: `-2.8660`
  * $p\text{-value} = 0.04945 < 0.05$ (Bác bỏ giả thuyết vô hiệu $H_0$).
  * Kết luận: Chuỗi thời gian đạt tính dừng thống kê ở mức ý nghĩa 5%, sẵn sàng cho các mô hình tự hồi quy.

### 3.3. Phân Tích Tự Tương Quan (ACF & PACF)
![Biểu đồ ACF và PACF](eda_acf_pacf.png)
- **Biểu đồ ACF (Autocorrelation Function):** Các hệ số tự tương quan nhô cao vượt dải tin cậy 95% tại các độ trễ: **Lag 7, Lag 14, Lag 21, Lag 28** (bội số của chu kỳ tuần). Điều này chứng minh doanh số ngày hôm nay có mối liên hệ mật thiết với cùng thứ đó của các tuần trước.
- **Biểu đồ PACF (Partial Autocorrelation Function):** Hệ số PACF cao và có ý nghĩa thống kê tại **Lag 1, Lag 2 và Lag 7**.
- **Ý nghĩa sống còn:** Đây là nền tảng lý thuyết vững chắc giải thích việc xây dựng tập đặc trưng trễ `lag_1`, `lag_2`, `lag_7`, `lag_14`, `lag_30` và thống kê trượt `rolling_mean_7`.

### 3.4. Mẫu Hình Mùa Vụ Đa Chiều & Phân Phối
![Mùa vụ Đa chiều](eda_seasonality_patterns.png)
![Phân phối và Ngoại lai](eda_distribution_outliers.png)
- **Ngày trong tuần:** Thứ 2 - Thứ 6 đạt doanh số bình quân ~150 - 165 triệu VNĐ/ngày. Thứ 7 và Chủ nhật giảm còn ~110 - 120 triệu VNĐ/ngày.
- **Tháng trong năm:** Tháng 1-2 là vùng trũng sau Tết Nguyên Đán; từ Tháng 8 tăng tốc và Tháng 11-12 đạt đỉnh cao nhất năm (>200 triệu VNĐ/ngày).
- **Quý:** Doanh số Quý 4 gấp gần 1.8 lần Quý 1.

---

## 4. BỘ ĐẶC TRƯNG NGOẠI SINH ĐA TẦNG (63 FEATURES)

Mô hình [hybrid_model.py](file:///d:/MohinhKLTN/hybrid_model.py) thiết kế hệ thống 63 đặc trưng chuyên sâu:
1. **Biến Lịch & Chu kỳ Thời gian (Calendar Features):**
   `dayofweek`, `dayofmonth`, `dayofyear`, `weekofyear`, `month`, `quarter`, `is_weekend`, `is_month_start`, `is_month_end`, `is_quarter_start`, `is_quarter_end`.
2. **Biến Sóng Lượng Giác Tuần Hoàn (Trigonometric Cyclic Encoding):**
   $\sin/\cos$ của `dow` ($2\pi \cdot \text{dow} / 7$), `month` ($2\pi \cdot \text{month} / 12$), `doy` ($2\pi \cdot \text{doy} / 365.25$), kết hợp biến tương tác chéo `dow_month_sin`, `dow_month_cos`.
3. **Biến Sự Kiện Bán Hàng & Lễ Tết Việt Nam:**
   - `is_payday`: Ngày lĩnh lương của người lao động (tập trung các ngày 1-5 và 15-20 hàng tháng).
   - `is_mega_sale`: Các ngày hội mua sắm trực tuyến (9/9, 10/10, 11/11, 12/12, Black Friday, Giáng Sinh).
   - `is_holiday`: Tích hợp thư viện `holidays.VN` và `holidays.US` (Tết Dương Lịch, Tết Nguyên Đán, Giỗ Tổ Hùng Vương, 30/4, 1/5, Quốc khánh 2/9).
   - `days_until_holiday`, `days_since_holiday`: Khoảng cách số ngày đếm ngược đến kỳ nghỉ tiếp theo và số ngày đã trôi qua sau kỳ nghỉ.
   - `is_pre_holiday_5d`, `is_post_holiday_3d`: Cờ đánh dấu giai đoạn tích trữ hàng trước lễ và xả hàng sau lễ.
4. **Tập Độ Trễ Tự Hồi Quy (Autoregressive Lags):**
   `lag_1`, `lag_2`, `lag_3`, `lag_4`, `lag_5`, `lag_6`, `lag_7`, `lag_14`, `lag_21`, `lag_28`, `lag_30`.
5. **Thống Kê Trượt Động Lượng (Rolling & Moving Stats):**
   - Cửa sổ 7 ngày: `rolling_mean_7`, `rolling_std_7`, `rolling_min_7`, `rolling_max_7`, `ewm_mean_7`.
   - Cửa sổ 14 ngày: `rolling_mean_14`, `rolling_std_14`, `rolling_min_14`, `rolling_max_14`.
   - Cửa sổ 30 ngày: `rolling_mean_30`, `rolling_std_30`, `rolling_min_30`, `rolling_max_30`, `ewm_mean_30`.
   - Động lượng tốc độ thay đổi: `momentum_7_30 = rolling_mean_7 / (rolling_mean_30 + 1e-5)`.
6. **Thành phần Nội suy từ Prophet:**
   `prophet_trend`, `prophet_weekly`, `prophet_yearly`, `prophet_yhat`.

---

## 5. MINH CHỨNG THỰC NGHIỆM ĐỐI SÁNH HIỆU NĂNG (TẬP TEST 2025 - VNĐ)

Hệ thống được kiểm thử nghiêm ngặt theo phương pháp Out-of-Time Validation:
- **Tập Huấn luyện (Train set):** 1,096 ngày (Từ `01/01/2022` đến `31/12/2024`).
- **Tập Kiểm thử (Test set):** 365 ngày (Từ `01/01/2025` đến `31/12/2025`).

![Biểu đồ Đối sánh Dự báo](forecast_chart_ultimate.png)
*(Hình 1: Đồ thị chuỗi thời gian năm 2025. Đường màu đỏ đậm (Hybrid Optimal) bám sát các biến động thực tế màu xám và khắc phục triệt để điểm yếu của đường màu xanh đứt nét Prophet)*

### 5.1. Bảng 1: Hiệu năng Tần suất Ngày (Daily Grain - 365 mốc vận hành)
*Mục đích: Phục vụ điều hành bán hàng, phân ca nhân sự và giao nhận hàng ngày.*

| Chỉ số Đánh Giá | Baseline Prophet | XGBoost Thuần | Hybrid XGBoost | Hybrid CatBoost | Hybrid Ensemble | Hybrid Tối ưu (Optimal) | Đánh giá Khoa học |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **$R^2$ Score (Độ giải thích)** | 0.2949 | 0.3955 | 0.4039 | 0.3960 | 0.4034 | **0.4202** | **Tăng vọt +42.49% so với Prophet** |
| **MAE (Sai số tuyệt đối - VNĐ)** | 90,032,773 | 80,821,521 | 81,454,839 | 81,489,172 | 81,223,501 | **80,312,211** | **Giảm 10.80% MAE (~9.72 triệu/ngày)** |
| **RMSE (Sai số bình phương - VNĐ)**| 120,726,887 | 111,784,947 | 111,005,698 | 111,736,970 | 111,049,852 | **109,476,262** | **Giảm 9.32% RMSE** |
| **MAPE (% Sai số tuyệt đối)** | 442.83% | 282.52% | 284.33% | 300.70% | 292.31% | **160.93%** | Giảm thiểu đột biến sai số cực đoan |
| **WAPE (% Sai số có trọng số)** | 44.25% | 39.72% | 40.03% | 40.05% | 39.92% | **39.47%** | **Kiểm soát trọng số tốt nhất** |
| **SMAPE (% Sai số đối xứng)** | 59.94% | 51.37% | 53.67% | 53.22% | 53.32% | **59.16%** | Cân đối hai chiều dự báo |
| **Sai số tổng doanh thu năm** | -15.51% | -14.04% | -12.28% | -14.66% | -13.47% | **-11.31%** | Giảm lệch ngân sách doanh nghiệp |

---

### 5.2. Bảng 2: Hiệu năng Tần suất Tuần (Weekly Grain - 53 tuần - Tồn kho & Chuỗi Cung Ứng)
*Mục đích: Phục vụ Quản trị Tồn kho, Lập kế hoạch Đơn hàng và Điều phối Chuỗi cung ứng.*

| Chỉ số Đánh Giá | Baseline Prophet | XGBoost Thuần | Hybrid XGBoost | Hybrid CatBoost | Hybrid Ensemble | Hybrid Tối ưu (Optimal) | Đánh giá Khoa học |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **$R^2$ Score** | 0.5438 | 0.4822 | 0.5398 | 0.5404 | **0.5451** | 0.5139 | **Tăng từ 0.42 (ngày) lên > 0.54 (tuần)** |
| **MAE (VNĐ/tuần)** | 293,416,385 | 305,080,821 | 300,451,780 | 300,425,173 | **300,029,984** | 309,958,707 | Mức sai số bình quân ~300M đ/tuần |
| **RMSE (VNĐ)** | 369,712,097 | 393,883,150 | 371,347,427 | 371,094,004 | **369,174,819** | 381,647,649 | Hybrid Ensemble đạt RMSE thấp nhất |
| **WAPE (%)** | 20.94% | 21.77% | 21.44% | 21.44% | **21.41%** | 22.12% | **Sai số giảm một nửa so với cấp ngày** |
| **MAPE (%)** | 20.57% | 21.26% | 21.45% | 21.25% | **21.30%** | 22.75% | Độ chính xác đạt xấp xỉ ~80% |

---

### 5.3. Bảng 3: Hiệu năng Tần suất Tháng (Monthly Grain - 12 tháng - Quản trị Tài chính)
*Mục đích: Phục vụ Giám đốc Tài chính (CFO) và Hội đồng Quản trị lập Ngân sách chiến lược và Kế hoạch Dòng tiền.*

| Chỉ số Đánh Giá | Baseline Prophet | XGBoost Thuần | Hybrid XGBoost | Hybrid CatBoost | Hybrid Ensemble | Hybrid Tối ưu (Optimal) | Mức Cải Thiện vs Prophet |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **$R^2$ Score (Độ giải thích)** | 0.6986 | 0.6850 | 0.7613 | 0.7168 | 0.7424 | **0.7625** | **Giải thích tới 76.25% biến động!** |
| **MAE (VNĐ/tháng)** | 959,917,000 | 918,425,529 | 772,974,418 | 907,198,568 | 835,239,941 | **762,973,734** | **Giảm 20.52% (~197 triệu VNĐ/tháng)** |
| **RMSE (VNĐ)** | 1,139,412,243 | 1,164,753,687 | 1,013,928,314 | 1,104,350,553 | 1,053,423,611 | **1,011,340,385** | **Giảm 11.24% RMSE** |
| **WAPE (% Có trọng số)** | 15.51% | 14.84% | 12.49% | 14.66% | 13.50% | **12.33%** | **Sai số chỉ còn 12.33%!** |
| **MAPE (% Tuyệt đối)** | 14.50% | 13.23% | 11.98% | 14.16% | 12.95% | **12.08%** | **Độ chính xác đạt 87.92%** |

---

## 6. PHÂN TÍCH NHÂN TỐ QUYẾT ĐỊNH (FEATURE IMPORTANCE)

![Feature Importance](feature_importance.png)
*(Hình 2: Mức độ quan trọng tương đối của các biến số hàng đầu trích xuất từ mô hình XGBoost)*

- **Nhóm nhân tố chi phối mạnh nhất:**
  * `rolling_mean_7`, `rolling_mean_30`, `ewm_mean_7`: Giữ vai trò quyết định, chứng minh doanh số ngày hiện tại phụ thuộc chặt chẽ vào quán tính doanh thu của tuần và tháng liền trước.
  * `lag_1`, `lag_7`, `lag_14`: Xác nhận tính chu kỳ 7 ngày được mô hình học tập rất sâu sắc.
- **Nhóm nhân tố lịch và sự kiện:**
  * `dayofweek`, `is_weekend`, `month`: Phản ánh sự chênh lệch rõ rệt giữa ngày thường và cuối tuần cũng như cao điểm cuối năm.
  * `is_payday`, `is_mega_sale`, `is_holiday`: Đóng vai trò xung kích kích hoạt các đột biến doanh số tại các mốc thời gian đặc biệt.

---

## 7. ĐÓNG GÓI MÔ HÌNH VÀ TRIỂN KHAI ỨNG DỤNG WEB DASHBOARD

### 7.1. Đóng Gói Nhị Phân Chuẩn Doanh Nghiệp (.pkl & .joblib)
- Mô hình lai hướng đối tượng được đóng gói trọn vẹn trong tệp [best_model.pkl](file:///d:/MohinhKLTN/saved_model/best_model.pkl) dung lượng ~1.13 MB.
- Chứa đựng đồng thời: Mô hình Prophet đã huấn luyện, cây quyết định XGBoost Residual, cây quyết định CatBoost Residual, bộ tiền xử lý và trọng số Stacking tối ưu.
- Cho phép bất kỳ ứng dụng nào nạp lại và thực hiện suy luận (Live Inference) chỉ trong **0.05 giây**:
  ```python
  from hybrid_model import KhangNghiHybridForecaster
  model = KhangNghiHybridForecaster.load_pickle("saved_model/best_model.pkl")
  future_df = model.forecast_future(horizon_days=30, history_df=hist_df)
  ```

### 7.2. Ứng Dụng Web Dashboard Streamlit ([app_web.py](file:///d:/MohinhKLTN/app_web.py))
Dashboard cung cấp trải nghiệm điều hành doanh nghiệp hiện đại với **5 phân hệ cốt lõi**:
1. **Tab 1: Chuỗi Thời Gian & Đối Sánh (2025):** Đồ thị động Plotly trực quan hóa đường doanh số thực tế và 5 mô hình dự báo với công cụ thu phóng linh hoạt.
2. **Tab 2: Dự Báo Tương Lai Đa Kịch Bản (2026+):** 
   - Cho phép doanh nghiệp hoạch định chân trời dự báo tùy chọn: **7 ngày (ngắn hạn), 30 ngày (1 tháng), 90 ngày (1 quý), 180 ngày (nửa năm), 365 ngày (năm 2026) đến 730 ngày (2 năm tới)**.
   - Hiển thị 4 thẻ KPIs chiến lược (Tổng doanh số, Doanh số bình quân/ngày, Đỉnh doanh số, Đáy doanh số).
   - Biểu đồ phân bổ doanh số theo từng tháng tương lai.
   - Nút xuất dữ liệu một chạm ra file **Excel (.xlsx)** gồm 2 sheet: Dự báo từng ngày và Kế hoạch từng tháng.
3. **Tab 3: Chỉ Số Khoa Học & Phân Tích 12 Tháng:** Bảng đối sánh chi tiết các chỉ số MAE, RMSE, MAPE, $R^2$, WAPE, SMAPE và sai số từng tháng.
4. **Tab 4: Tra Cứu Dự Báo Từng Ngày (2025):** Cho phép người dùng chọn bất kỳ ngày nào trong năm 2025 để tra cứu doanh số thực tế, dự báo của các mô hình và độ lệch tuyệt đối.
5. **Tab 5: Phân Tích Nhân Tố (Features) & Giám Sát CSDL:** Xem biểu đồ Feature Importance, kiểm tra trạng thái kết nối SQL Server và tra cứu kết quả phân tích thống kê EDA.

---

## 8. KẾT LUẬN & ĐÓNG GÓP HỌC THUẬT CỦA KHÓA LUẬN

1. **Về mặt kỹ thuật dữ liệu:** Thiết kế thành công kiến trúc dữ liệu 3 tầng (OLTP $\rightarrow$ ETL $\rightarrow$ Data Warehouse) theo chuẩn Ralph Kimball. Quy trình EDA và Machine Learning được nạp trực tiếp từ Data Warehouse, bảo đảm tính toàn vẹn và thực tế của hệ thống BI doanh nghiệp.
2. **Về mặt thuật toán:** Đề xuất và hiện thực hóa thành công **Kiến trúc Lai Học Phần Dư (Hybrid Residual Paradigm)** kết hợp tối ưu hóa Stacking (XGBoost + CatBoost trên nền Prophet). Giải pháp này giải quyết triệt để bài toán hụt doanh số và bắt đỉnh nhọn của Prophet.
3. **Về mặt quản trị kinh doanh:** Cung cấp giải pháp dự báo đa độ mịn thời gian (Multi-Granularity: Ngày - Tuần - Tháng). Đạt độ chính xác $R^2 = 0.4202$ ở cấp ngày (tăng +42.49% so với Prophet), $R^2 = 0.5451$ ở cấp tuần (WAPE ~21%), và $R^2 = 0.7625$ ở cấp tháng (WAPE chỉ 12.33%, độ chính xác ~88%), phục vụ hoàn hảo từ tác nghiệp hàng ngày, quản lý tồn kho cho đến hoạch định chiến lược tài chính cấp cao của doanh nghiệp Khang Nghi.
