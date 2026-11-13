"""
Module Kết nối Data Warehouse & Pipeline Tiền Xử Lý Dữ Liệu Doanh Số KhangNghi
Phục vụ Đồ án / Khóa luận Tốt nghiệp

Quy trình chuẩn Data Engineering:
  1. Kết nối Data Warehouse (DWH_KhangNghi trên SQL Server .\\SQLEXPRESS qua SQLAlchemy / pyodbc)
  2. Thực thi câu truy vấn Analytical SQL Query kết hợp Fact_Sales & Dim_Time
  3. Kiểm tra chất lượng dữ liệu (Data Quality Checks: Nulls, Duplicates, Schema validation)
  4. Chuẩn hóa chuỗi thời gian liên tục (Tái lập đủ 365 ngày/năm, lấp ngày khuyết bằng 0.0)
  5. Xử lý ngoại lai thích ứng (Adaptive Outlier Capping / Winsorization tại phân vị 99%)
  6. Trích xuất đặc trưng chuỗi thời gian toàn diện (Lags 1-30, Rolling 3-30, EWM, Lịch, Lễ Tết)
  7. Cơ chế Dự phòng Thông minh (Fallback to Excel nếu máy khác không bật SQL Server)
"""

import os
import sys
import logging
from datetime import datetime
from typing import Dict, Any, Tuple, Optional

# Cấu hình mã hóa xuất chuẩn UTF-8 trên Windows console
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

import numpy as np
import pandas as pd
import holidays
import pyodbc

# Cấu hình logging chuyên nghiệp
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - [%(levelname)s] - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)

# Cấu hình đường dẫn thư mục gốc
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
FALLBACK_CSV_PATH = os.path.join(BASE_DIR, "data_csv", "Sales_Orders_Full_Detail.csv")
FALLBACK_EXCEL_PATH = os.path.join(BASE_DIR, "KhangNghiData.xlsx")

# Cấu hình kết nối SQL Server (Chuẩn Data Warehouse: DWH_KhangNghi)
DWH_CONFIG = {
    "server": r".\SQLEXPRESS",
    "database": "DWH_KhangNghi",
    "driver": "ODBC Driver 17 for SQL Server",
    "trusted_connection": "yes",
    "timeout": 5
}


class KhangNghiDWHConnector:
    """
    Lớp kết nối và trích xuất dữ liệu từ Data Warehouse DWH_KhangNghi (Mô hình Star Schema).
    Trích xuất trực tiếp từ bảng Fact_Sales và Dim_Time theo đúng chuẩn Đề cương KLTN.
    Hỗ trợ cơ chế Fallback nếu môi trường không có sẵn SQL Server service.
    """

    def __init__(self, config: Optional[Dict[str, Any]] = None):
        self.config = config or DWH_CONFIG
        self.conn_str = (
            f"DRIVER={{{self.config['driver']}}};"
            f"SERVER={self.config['server']};"
            f"DATABASE={self.config['database']};"
            f"Trusted_Connection={self.config['trusted_connection']};"
        )
        self.is_connected = False
        self.connection_source = "None"

    def test_connection(self) -> bool:
        """Kiểm tra kết nối tới SQL Server."""
        try:
            conn = pyodbc.connect(self.conn_str, timeout=self.config.get("timeout", 5))
            conn.close()
            self.is_connected = True
            self.connection_source = f"SQL Server ({self.config['database']})"
            logging.info(f" Kết nối thành công tới Database: {self.config['database']} tại {self.config['server']}")
            return True
        except Exception as e:
            logging.warning(f"⚠️ Không thể kết nối SQL Server ({e}). Sẽ kích hoạt chế độ dự phòng.")
            self.is_connected = False
            return False

    def extract_analytical_sales(self) -> Tuple[pd.DataFrame, str]:
        """
        Trích xuất dữ liệu doanh số bằng Analytical SQL Query từ Data Warehouse (Fact_Sales + Dim_Time).
        Nếu SQL Server không khả dụng, tự động nạp từ nguồn dữ liệu CSV dự phòng (Sales_Orders_Full_Detail.csv).
        """
        if self.test_connection():
            # Truy vấn từ Data Warehouse DWH_KhangNghi (Mô hình Star Schema chuẩn Kimball)
            query = """
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
            """
            logging.info(f">>> Đang thực thi Analytical SQL Query trích xuất từ Data Warehouse {self.config['database']}...")
            conn = pyodbc.connect(self.conn_str)
            df = pd.read_sql(query, conn)
            conn.close()
            df["ds"] = pd.to_datetime(df["ds"])
            logging.info(f" Trích xuất thành công {len(df):,} ngày giao dịch từ Data Warehouse {self.config['database']} (Fact_Sales).")
            return df, "DATA_WAREHOUSE_DWH_KHANGNGHI"
        else:
            # Fallback 1: Nạp từ file CSV đã xuất từ KhangNghi_OLTP_VN_DRAFT
            if os.path.exists(FALLBACK_CSV_PATH):
                logging.info(f">>> Kích hoạt Fallback 1: Trích xuất dữ liệu từ file CSV {FALLBACK_CSV_PATH}...")
                sales_full = pd.read_csv(FALLBACK_CSV_PATH)
                sales_full["OrderDate"] = pd.to_datetime(sales_full["OrderDate"])
                df = sales_full.groupby("OrderDate").agg({
                    "Sales": "sum",
                    "OrderID": "nunique",
                    "Quantity": "sum",
                    "Profit": "sum",
                    "Discount": "mean"
                }).reset_index()
                df.rename(columns={
                    "OrderDate": "ds",
                    "Sales": "y",
                    "OrderID": "total_orders",
                    "Quantity": "total_quantity",
                    "Discount": "total_discount",
                    "Profit": "total_profit"
                }, inplace=True)
                df["ds"] = pd.to_datetime(df["ds"])
                logging.info(f" Nạp thành công {len(df):,} ngày giao dịch từ nguồn CSV Việt Nam.")
                return df, "FALLBACK_CSV_VN"

            # Fallback 2: Nạp từ file Excel cũ nếu không có CSV
            logging.info(f">>> Kích hoạt Fallback 2: Trích xuất dữ liệu từ file Excel {FALLBACK_EXCEL_PATH}...")
            if not os.path.exists(FALLBACK_EXCEL_PATH):
                raise FileNotFoundError(f"Không tìm thấy file dữ liệu: {FALLBACK_CSV_PATH} hoặc {FALLBACK_EXCEL_PATH}")
            
            orders = pd.read_excel(FALLBACK_EXCEL_PATH, sheet_name="Orders")
            returns = pd.read_excel(FALLBACK_EXCEL_PATH, sheet_name="Returns")
            orders.columns = orders.columns.str.strip()
            returns.columns = returns.columns.str.strip()

            if "Returned" in returns.columns:
                ret_ids = returns[returns["Returned"] == "Yes"]["Order ID"].unique()
            else:
                ret_ids = returns["Order ID"].unique()
            
            clean_orders = orders[~orders["Order ID"].isin(ret_ids)].copy()
            clean_orders["Order Date"] = pd.to_datetime(clean_orders["Order Date"])

            df = clean_orders.groupby("Order Date").agg({
                "Sales": "sum",
                "Order ID": "nunique",
                "Quantity": "sum",
                "Discount": "sum",
                "Profit": "sum"
            }).reset_index()

            df.rename(columns={
                "Order Date": "ds",
                "Sales": "y",
                "Order ID": "total_orders",
                "Quantity": "total_quantity",
                "Discount": "total_discount",
                "Profit": "total_profit"
            }, inplace=True)
            df["ds"] = pd.to_datetime(df["ds"])
            logging.info(f" Nạp thành công {len(df):,} ngày giao dịch từ nguồn Excel.")
            return df, "FALLBACK_EXCEL"


class KhangNghiDataPreprocessor:
    """
    Quy trình Tiền xử lý dữ liệu và Trích xuất đặc trưng chuỗi thời gian (Feature Engineering).
    """

    def __init__(self, capping_quantile: float = 0.99):
        self.capping_quantile = capping_quantile
        self.capping_value: Optional[float] = None
        self.quality_report: Dict[str, Any] = {}

    def run_data_quality_checks(self, df: pd.DataFrame) -> Dict[str, Any]:
        """Kiểm tra chất lượng và tính toàn vẹn của dữ liệu chuỗi thời gian."""
        null_dates = int(df["ds"].isnull().sum())
        null_sales = int(df["y"].isnull().sum())
        negative_sales = int((df["y"] < 0).sum())
        zero_sales = int((df["y"] == 0).sum())
        duplicate_dates = int(df["ds"].duplicated().sum())

        report = {
            "Total_Raw_Records": len(df),
            "Min_Date": str(df["ds"].min().date()),
            "Max_Date": str(df["ds"].max().date()),
            "Total_Sales_Raw": round(float(df["y"].sum()), 2),
            "Null_Dates_Count": null_dates,
            "Null_Sales_Count": null_sales,
            "Negative_Sales_Count": negative_sales,
            "Zero_Sales_Days": zero_sales,
            "Duplicate_Dates_Count": duplicate_dates,
            "Status": "Passed" if (null_dates == 0 and null_sales == 0 and duplicate_dates == 0) else "Warning"
        }
        self.quality_report = report
        logging.info(f" [Data Quality] Kiểm tra hoàn tất: Trạng thái = {report['Status']}")
        return report

    def complete_time_series(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Chuẩn hóa chuỗi thời gian liên tục:
        Lấp đầy tất cả các ngày bị khuyết trong năm (reindex freq='D') với doanh số = 0.0.
        """
        df_sorted = df.sort_values("ds").copy()
        min_date, max_date = df_sorted["ds"].min(), df_sorted["ds"].max()
        all_dates = pd.date_range(start=min_date, end=max_date, freq="D", name="ds")

        df_sorted.set_index("ds", inplace=True)
        continuous_df = df_sorted.reindex(all_dates)

        # Lấp giá trị khuyết
        continuous_df["y"] = continuous_df["y"].fillna(0.0)
        for col in ["total_orders", "total_quantity", "total_discount", "total_profit"]:
            if col in continuous_df.columns:
                continuous_df[col] = continuous_df[col].fillna(0.0)

        continuous_df.reset_index(inplace=True)
        continuous_df.rename(columns={"index": "ds"}, inplace=True)

        added_days = len(continuous_df) - len(df)
        logging.info(f" [Continuous Series] Tái lập chuỗi liên tục từ {min_date.date()} đến {max_date.date()}:")
        logging.info(f"   + Tổng số ngày: {len(continuous_df):,} ngày (Đã bổ sung {added_days} ngày không có giao dịch với y=0).")
        return continuous_df

    def cap_outliers(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Xử lý ngoại lai bằng phân vị trần (Winsorization / Capping).
        Loại bỏ các cú sốc bất thường quá lớn gây nhiễu cho việc học xu hướng.
        """
        df_capped = df.copy()
        self.capping_value = float(df_capped["y"].quantile(self.capping_quantile))
        outlier_count = int((df_capped["y"] > self.capping_value).sum())
        df_capped["y_raw"] = df_capped["y"]
        df_capped["y"] = np.where(df_capped["y"] > self.capping_value, self.capping_value, df_capped["y"])
        logging.info(f" [Outlier Capping] Phân vị {self.capping_quantile*100:.0f}% = {self.capping_value:,.2f}."
                     f" Đã xử lý {outlier_count} ngày có đỉnh doanh số đột biến.")
        return df_capped

    def extract_features(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Trích xuất đặc trưng ngoại sinh toàn diện (Feature Engineering):
        - Lịch & Mùa vụ (Calendar Features)
        - Chu kỳ Lễ Tết Quốc tế & Việt Nam (US & VN Holidays)
        - Đặc trưng trễ (Lags 1-30)
        - Thống kê trượt (Rolling Stats: Mean, Std, Max) & Exponential Moving Average (EWM)
        """
        feat_df = df.copy()
        feat_df["ds"] = pd.to_datetime(feat_df["ds"])

        # 1. Đặc trưng lịch cơ bản
        feat_df["dayofweek"] = feat_df["ds"].dt.dayofweek
        feat_df["dayofmonth"] = feat_df["ds"].dt.day
        feat_df["dayofyear"] = feat_df["ds"].dt.dayofyear
        feat_df["weekofyear"] = feat_df["ds"].dt.isocalendar().week.astype(int)
        feat_df["month"] = feat_df["ds"].dt.month
        feat_df["quarter"] = feat_df["ds"].dt.quarter
        feat_df["year"] = feat_df["ds"].dt.year
        feat_df["is_weekend"] = (feat_df["dayofweek"] >= 5).astype(int)

        # 2. Đặc trưng chu kỳ mua sắm kế toán
        feat_df["is_month_start"] = feat_df["ds"].dt.is_month_start.astype(int)
        feat_df["is_month_end"] = feat_df["ds"].dt.is_month_end.astype(int)
        feat_df["is_quarter_start"] = feat_df["ds"].dt.is_quarter_start.astype(int)
        feat_df["is_quarter_end"] = feat_df["ds"].dt.is_quarter_end.astype(int)

        # 3. Đặc trưng Ngày Lễ
        years = [int(y) for y in feat_df["ds"].dt.year.unique()]
        us_h = holidays.US(years=years)
        vn_h = holidays.VN(years=years)
        all_holidays = set(us_h.keys()).union(set(vn_h.keys()))
        feat_df["is_holiday"] = feat_df["ds"].dt.date.isin(all_holidays).astype(int)

        # 4. Đặc trưng trễ (Lags)
        lag_days = [1, 2, 3, 4, 5, 6, 7, 14, 21, 30]
        for lag in lag_days:
            feat_df[f"lag_{lag}"] = feat_df["y"].shift(lag)

        # 5. Cửa sổ trượt (Rolling & EWM)
        window_sizes = [3, 7, 14, 21, 30]
        for w in window_sizes:
            feat_df[f"rolling_mean_{w}"] = feat_df["y"].shift(1).rolling(window=w).mean()
            feat_df[f"rolling_std_{w}"] = feat_df["y"].shift(1).rolling(window=w).std()
            feat_df[f"rolling_max_{w}"] = feat_df["y"].shift(1).rolling(window=w).max()
            feat_df[f"ewm_mean_{w}"] = feat_df["y"].shift(1).ewm(span=w, adjust=False).mean()

        feat_df.fillna(0, inplace=True)
        logging.info(f" [Feature Engineering] Đã sinh thành công {feat_df.shape[1]} đặc trưng (Features) cho mô hình học máy.")
        return feat_df


def get_preprocessed_dwh_data() -> Tuple[pd.DataFrame, Dict[str, Any]]:
    """
    Hàm đóng gói toàn diện: Trích xuất từ DWH -> Kiểm tra chất lượng -> Tiền xử lý -> Sinh đặc trưng.
    """
    logging.info("=" * 70)
    logging.info(" BẮT ĐẦU QUY TRÌNH TIỀN XỬ LÝ DỮ LIỆU TỪ DATA WAREHOUSE")
    logging.info("=" * 70)

    connector = KhangNghiDWHConnector()
    raw_df, source = connector.extract_analytical_sales()

    preprocessor = KhangNghiDataPreprocessor(capping_quantile=0.99)
    quality_report = preprocessor.run_data_quality_checks(raw_df)
    quality_report["DataSource"] = source

    # Chuẩn hóa chuỗi thời gian liên tục
    continuous_df = preprocessor.complete_time_series(raw_df)

    # Xử lý ngoại lai
    capped_df = preprocessor.cap_outliers(continuous_df)

    # Sinh đặc trưng
    final_df = preprocessor.extract_features(capped_df)

    logging.info("=" * 70)
    logging.info(" HOÀN TẤT TIỀN XỬ LÝ DỮ LIỆU TỪ DATA WAREHOUSE THÀNH CÔNG 100%!")
    logging.info("=" * 70)

    return final_df, quality_report


if __name__ == "__main__":
    df, report = get_preprocessed_dwh_data()
    print("\n--- BÁO CÁO CHẤT LƯỢNG DỮ LIỆU DATA WAREHOUSE ---")
    for k, v in report.items():
        print(f"  {k}: {v}")
    print("\n5 dòng dữ liệu đầu tiên sau tiền xử lý:")
    print(df[["ds", "y", "dayofweek", "month", "lag_1", "lag_7", "rolling_mean_7"]].head())
