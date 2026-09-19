import pandas as pd
import numpy as np
import pyodbc
import matplotlib.pyplot as plt
from sklearn.linear_model import LinearRegression
from datetime import timedelta
import warnings
warnings.filterwarnings('ignore')

print("1. Đang kết nối đến cơ sở dữ liệu DWH_KhangNghi (13 Dims)...")
conn_str = (
    r'DRIVER={ODBC Driver 17 for SQL Server};'
    r'SERVER=localhost\SQLEXPRESS;'
    r'DATABASE=DWH_KhangNghi;'
    r'Trusted_Connection=yes;'
)

try:
    conn = pyodbc.connect(conn_str)
    
    # 1. Truy vấn dữ liệu doanh thu theo ngày từ Fact_Sales và Dim_Time
    query = """
    SELECT 
        t.FullDate as [Date], 
        SUM(f.SalesAmount) as TotalSales,
        COUNT(f.OrderID) as OrderCount
    FROM dbo.Fact_Sales f
    JOIN dbo.Dim_Time t ON f.TimeKey = t.TimeKey
    GROUP BY t.FullDate
    ORDER BY t.FullDate
    """
    
    df = pd.read_sql(query, conn)
    conn.close()
    
    print(f"2. Đã tải thành công {len(df)} ngày có dữ liệu doanh thu.")
    
    if len(df) == 0:
        print("Lỗi: Không có dữ liệu trong Fact_Sales. Hãy dùng SSIS để nạp dữ liệu (Staging -> sp_SSIS_ETL_Load) trước.")
        exit()

    df['Date'] = pd.to_datetime(df['Date'])
    df = df.set_index('Date').asfreq('D').fillna(0) # Đảm bảo liên tục theo ngày
    
    # 2. Chuẩn bị dữ liệu cho Linear Regression
    df['DayIndex'] = np.arange(len(df))
    X = df[['DayIndex']]
    y = df['TotalSales']
    
    # 3. Huấn luyện mô hình Machine Learning
    print("3. Đang huấn luyện mô hình Linear Regression...")
    model = LinearRegression()
    model.fit(X, y)
    
    # 4. Dự báo 30 ngày tiếp theo
    future_days = 30
    last_day_index = df['DayIndex'].max()
    future_X = pd.DataFrame({'DayIndex': np.arange(last_day_index + 1, last_day_index + 1 + future_days)})
    future_y = model.predict(future_X)
    
    future_dates = [df.index[-1] + timedelta(days=i) for i in range(1, future_days + 1)]
    future_df = pd.DataFrame({'Date': future_dates, 'PredictedSales': future_y})
    
    # Không để doanh thu dự báo bị âm
    future_df['PredictedSales'] = future_df['PredictedSales'].clip(lower=0)
    
    print("4. Hoàn tất dự báo. Đang xuất biểu đồ...")
    
    # 5. Trực quan hóa
    plt.figure(figsize=(12, 6))
    plt.plot(df.index, df['TotalSales'], label='Doanh thu thực tế', color='blue', alpha=0.7)
    plt.plot(df.index, model.predict(X), label='Đường xu hướng (Trend)', color='green', linestyle='--')
    plt.plot(future_df['Date'], future_df['PredictedSales'], label='Dự báo 30 ngày tới', color='red', marker='.')
    
    plt.title('DỰ BÁO DOANH SỐ (MACHINE LEARNING - LINEAR REGRESSION)', fontsize=14, fontweight='bold')
    plt.xlabel('Thời gian', fontsize=12)
    plt.ylabel('Doanh thu (USD)', fontsize=12)
    plt.legend()
    plt.grid(True, linestyle='--', alpha=0.5)
    plt.tight_layout()
    
    # Lưu file
    plt.savefig('BieuDo_DuBao_DoanhSo.png', dpi=300)
    print("✅ Đã lưu biểu đồ vào file BieuDo_DuBao_DoanhSo.png")
    
    future_df.to_excel('DuBao_Result.xlsx', index=False)
    print("✅ Đã xuất kết quả dự báo ra file DuBao_Result.xlsx")
    
except Exception as e:
    print(f"Lỗi: {e}")
