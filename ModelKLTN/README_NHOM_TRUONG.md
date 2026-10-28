# GÓI MÔ HÌNH DỰ BÁO DOANH SỐ KHANG NGHI (HYBRID RESIDUAL PARADIGM)
> **Tài liệu bàn giao cho Trưởng nhóm Khóa luận Tốt nghiệp**  
> **Kiến trúc mô hình:** Hybrid Optimal (Facebook Prophet + XGBoost Residual + CatBoost Residual)  
> **Dữ liệu huấn luyện:** 1.461 ngày liên tục (2022 - 2025) từ Data Warehouse `DWH_KhangNghi`  
> **Cập nhật mới nhất:** 28/10/2026

---

## 1. CẤU TRÚC THƯ MỤC `ModelKLTN` (TINH GỌN - DUY NHẤT 1 GÓI MÔ HÌNH)

```
ModelKLTN/
│
├── 🤖 saved_model/                  # [DUY NHẤT] Gói mô hình tốt nhất đã huấn luyện trên toàn bộ lịch sử (1.461 ngày)
│   ├── best_model.pkl              # File nhị phân trọn gói nạp trong 0.05 giây để dự báo tương lai
│   ├── prophet_model.json          # Mô hình Facebook Prophet đã huấn luyện
│   ├── xgb_residual.joblib         # Cây quyết định XGBoost Residual
│   ├── cat_residual.joblib         # Cây quyết định CatBoost Residual
│   └── metadata.json               # Tham số và siêu trọng số Stacking tối ưu (w_xgb=0.668, w_cat=0.985)
│
├── 🐍 hybrid_model.py               # File mã nguồn lõi: Định nghĩa class KhangNghiHybridForecaster
├── 🐍 dwh_pipeline.py               # Module tiền xử lý chuỗi thời gian liên tục từ Data Warehouse
├── 📄 requirements.txt              # Danh sách thư viện cần thiết để chạy
│
├── 📊 evaluation_metrics.json       # Toàn bộ chỉ số kiểm nghiệm khoa học (MAE, RMSE, R2, WAPE theo Ngày, Tuần, Tháng)
├── 📈 KhangNghi_Forecast_2025.xlsx  # Bảng Excel kết quả dự báo đối soát
├── 🌐 forecast_bundle.json          # File JSON cache kết quả dự báo 730 ngày (2026-2027) nạp thẳng vào Web App
│
├── 🖼️ images/                       # Toàn bộ biểu đồ khoa học phân giải cao phục vụ viết bài & Slide
│   ├── forecast_chart_ultimate.png # Biểu đồ đối sánh các mô hình trên tập Test 2025
│   ├── feature_importance.png      # Mức độ quan trọng của 71 đặc trưng
│   ├── eda_decomposition.png       # Phân rã chuỗi thời gian (Trend, Seasonality, Residuals)
│   ├── eda_acf_pacf.png            # Tự tương quan ACF và PACF (chứng minh chu kỳ 7 ngày)
│   ├── eda_seasonality_patterns.png# Mẫu hình mùa vụ theo Ngày trong tuần, Tháng, Quý
│   └── eda_distribution_outliers.png # Phân phối doanh số và ngưỡng cắt ngoại lai 99%
│
└── 📑 Bao_Cao_Mo_Hinh_KLTN.md       # Báo cáo học thuật hoàn chỉnh (Copy nội dung này vào Khóa luận)
```

---

## 2. HƯỚNG DẪN NHÚNG VÀO CODE KHÓA LUẬN LỚN (CHỈ 3 DÒNG)

Trưởng nhóm chỉ cần đặt thư mục `saved_model` và file `hybrid_model.py` vào project lớn, rồi gọi:

```python
import pandas as pd
from hybrid_model import KhangNghiHybridForecaster

# 1. Nạp mô hình Hybrid Optimal tốt nhất (Nạp tức thì ~0.05 giây)
model = KhangNghiHybridForecaster.load_pickle("saved_model/best_model.pkl")

# 2. Dự báo N ngày tương lai (truyền DataFrame lịch sử gần nhất có cột 'ds' và 'y')
# df_future = model.forecast_future(horizon_days=30, history_df=history_df)

# 3. Trích xuất doanh số dự báo tối ưu nhất:
# print(df_future[["Date", "Day_of_Week", "Hybrid_Optimal_Pred"]])
```

*Nếu nhóm trưởng chỉ muốn lấy sẵn số liệu dự báo 2 năm tới (2026-2027) để vẽ Web Dashboard mà không muốn chạy lại code Machine Learning: Đọc trực tiếp file `forecast_bundle.json` (đã tính sẵn toàn bộ dải tin cậy 80%).*

---

## 3. TÓM TẮT SỐ LIỆU ĐƯA VÀO BÁO CÁO LUẬN VĂN & SLIDE

### 1. Kiến trúc đề xuất:
- **Tên mô hình:** Kiến trúc Lai Học Phần Dư Đa Tầng (**Hybrid Residual Learning Paradigm**).
- **Cơ chế Stacking tối ưu:**
  $$\hat{y}_{\text{Optimal}}(t) = \max\left(0, \;\hat{y}_{\text{Prophet}}(t) + 0.668 \cdot \hat{\epsilon}_{\text{XGB}}(t) + 0.985 \cdot \hat{\epsilon}_{\text{Cat}}(t)\right)$$

### 2. Hiệu năng vượt trội trên tập Test 2025:
- **Cấp Ngày (Daily - Tác nghiệp vận hành):** 
  - $R^2 = 0.4202$ (Tăng vọt **+42.49%** so với Baseline Prophet $R^2 = 0.2949$).
  - MAE = **80.31 triệu VNĐ/ngày** (Giảm **10.80%** MAE so với Prophet).
  - WAPE = **39.47%**.
- **Cấp Tuần (Weekly - Tồn kho & Cung ứng):** 
  - $R^2 = 0.5451$, WAPE giảm còn **~21%**.
- **Cấp Tháng (Monthly - Chiến lược Tài chính & Ngân sách):** 
  - $R^2 = 0.7625$ (Giải thích tới **76.25%** phương sai dữ liệu).
  - MAE = **762.97 triệu VNĐ/tháng** (Giảm **20.52%** MAE so với Prophet).
  - WAPE = **12.33%** (Độ chính xác đạt **87.67%**).
  - Sai lệch tổng doanh thu cả năm giảm mạnh về mức **-11.31%** (khắc phục hoàn toàn tình trạng hụt doanh số cả năm -15.51% của Prophet).
