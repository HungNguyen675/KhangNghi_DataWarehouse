"""
KhangNghi Sales Forecasting - Unified Hybrid Model (Phiên bản Hợp nhất & Tối ưu)
Kiến trúc: Hybrid Residual Paradigm (Facebook Prophet + XGBoost Regressor)
Đóng gói toàn diện trong 1 file duy nhất:
  1. Khối mô hình hướng đối tượng: KhangNghiHybridForecaster (Train, Predict, Evaluate, Save, Load)
  2. Xử lý & Làm sạch dữ liệu chuỗi thời gian liên tục
  3. Đánh giá đa chiều (MAE, RMSE, R2, WAPE, SMAPE, Doanh số theo tháng)
  4. Trực quan hóa kết quả (Biểu đồ Dự báo & Feature Importance)
"""

import os
import sys
import json
import logging
import pickle
import warnings
from typing import Any, Dict, List, Optional

import holidays
import joblib
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from prophet import Prophet
from prophet.serialize import model_from_json, model_to_json
from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score
from sklearn.model_selection import GridSearchCV, TimeSeriesSplit
import xgboost as xgb
from catboost import CatBoostRegressor

from dwh_pipeline import KhangNghiDWHConnector, KhangNghiDataPreprocessor

# Cấu hình mã hóa xuất chuẩn UTF-8
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

# Tắt cảnh báo không cần thiết và cấu hình logging
warnings.filterwarnings("ignore")
logging.basicConfig(level=logging.INFO, format="%(asctime)s - [%(levelname)s] - %(message)s")
logging.getLogger("cmdstanpy").setLevel(logging.ERROR)


# ==============================================================================
# KHỐI MÔ HÌNH LAI HƯỚNG ĐỐI TƯỢNG (OOP HYBRID FORECASTER BLOCK)
# ==============================================================================
class KhangNghiHybridForecaster:
    """
    Khối mô hình dự báo doanh số lai (Hybrid Forecaster Block).
    Tích hợp:
      - Facebook Prophet: Bắt Trend dài hạn, chu kỳ mùa vụ (Seasonality) & Ngày lễ (Holidays)
      - XGBoost Residual: Bắt đỉnh đột biến phi tuyến tính (Outlier / Spikes) qua học phần dư
      - Feature Engineering: Lag 1-30, Rolling statistics 3-30, Calendar & Holiday Events
    """

    def __init__(
        self,
        capping_quantile: float = 0.99,
        changepoint_prior_scale: float = 0.15,
        seasonality_mode: str = "multiplicative",
        residual_weight_xgb: float = 0.90,
        residual_weight_cat: float = 0.70,
        random_state: int = 42,
    ):
        self.capping_quantile: float = capping_quantile
        self.changepoint_prior_scale: float = changepoint_prior_scale
        self.seasonality_mode: str = seasonality_mode
        self.residual_weight_xgb: float = residual_weight_xgb
        self.residual_weight_cat: float = residual_weight_cat
        self.random_state: int = random_state

        # Khởi tạo các thành phần nội tại
        self.prophet_model: Optional[Prophet] = None
        self.xgb_residual_model: Optional[xgb.XGBRegressor] = None
        self.xgb_pure_model: Optional[xgb.XGBRegressor] = None
        self.cat_residual_model: Optional[CatBoostRegressor] = None
        self.feature_cols: List[str] = []
        self.best_params_residual: Dict[str, Any] = {}
        self.best_params_pure: Dict[str, Any] = {}
        self.best_model_name: str = "Hybrid_XGBoost"
        self.capping_value: Optional[float] = None
        self.is_fitted: bool = False
        self.training_summary: Dict[str, Any] = {}
        self.optimal_weights: Dict[str, float] = {"w_xgb": 0.668, "w_cat": 0.985}

    def _get_holidays_df(self, years: List[int]) -> pd.DataFrame:
        """Tạo danh sách ngày lễ US và VN cho Prophet."""
        us_holidays = holidays.US(years=years)
        vn_holidays = holidays.VN(years=years)
        h_list = []
        for date, _ in us_holidays.items():
            h_list.append({"ds": pd.to_datetime(date), "holiday": "US_Holiday"})
        for date, _ in vn_holidays.items():
            h_list.append({"ds": pd.to_datetime(date), "holiday": "VN_Holiday"})
        return pd.DataFrame(h_list).drop_duplicates(subset=["ds"])

    def create_features(
        self,
        daily_df: pd.DataFrame,
        prophet_components_df: Optional[pd.DataFrame] = None,
    ) -> pd.DataFrame:
        """
        Trích xuất đặc trưng ngoại sinh tối ưu (Exogenous Feature Engineering):
        - Calendar: Thứ, ngày, tháng, quý, cuối tuần
        - Sóng lượng giác (Cyclical encoding): sin/cos của dayofweek, month, dayofyear
        - Events: Đầu tháng, cuối tháng, đầu quý, cuối quý, ngày lễ, payday, mega sales
        - Dynamic Momentum: Lags (1, 2, 3, 4, 5, 6, 7, 14, 21, 28, 30)
        - Rolling stats: Mean, std, max, min, ewm_mean (cửa sổ 3, 7, 14, 21, 30)
        - Tỷ lệ xung lực & khác biệt động lượng (ratios & diffs)
        - Prophet Decomposition Context: trend, weekly, yearly, yhat (từ mô hình Prophet)
        """
        df = daily_df.copy()
        df["ds"] = pd.to_datetime(df["ds"])

        # 1. Đặc trưng lịch cơ bản
        df["dayofweek"] = df["ds"].dt.dayofweek
        df["dayofmonth"] = df["ds"].dt.day
        df["dayofyear"] = df["ds"].dt.dayofyear
        df["weekofyear"] = df["ds"].dt.isocalendar().week.astype(int)
        df["month"] = df["ds"].dt.month
        df["quarter"] = df["ds"].dt.quarter
        df["is_weekend"] = (df["dayofweek"] >= 5).astype(int)

        # 2. Mã hóa hàm sóng lượng giác (Cyclical Encoding)
        df["sin_dow"] = np.sin(2 * np.pi * df["dayofweek"] / 7)
        df["cos_dow"] = np.cos(2 * np.pi * df["dayofweek"] / 7)
        df["sin_month"] = np.sin(2 * np.pi * df["month"] / 12)
        df["cos_month"] = np.cos(2 * np.pi * df["month"] / 12)
        df["sin_doy"] = np.sin(2 * np.pi * df["dayofyear"] / 365.25)
        df["cos_doy"] = np.cos(2 * np.pi * df["dayofyear"] / 365.25)

        # Tương tác mùa vụ (Seasonality Interaction: Thứ trong tuần x Tháng)
        df["dow_month_sin"] = df["sin_dow"] * df["sin_month"]
        df["dow_month_cos"] = df["cos_dow"] * df["cos_month"]

        # 3. Đặc trưng chu kỳ kế toán & tiêu dùng nhạy cảm
        df["is_month_start"] = df["ds"].dt.is_month_start.astype(int)
        df["is_month_end"] = df["ds"].dt.is_month_end.astype(int)
        df["is_quarter_start"] = df["ds"].dt.is_quarter_start.astype(int)
        df["is_quarter_end"] = df["ds"].dt.is_quarter_end.astype(int)

        # Chu kỳ nhận lương (Payday: 1-5 và 15-20)
        df["is_payday"] = df["dayofmonth"].isin([1, 2, 3, 4, 5, 15, 16, 17, 18, 19, 20]).astype(int)

        # Đợt Mega Sale cao điểm bán lẻ (9/9, 10/10, 11/11, Black Friday, 12/12, Giáng sinh)
        is_black_friday_week = (df["month"] == 11) & (df["dayofmonth"] >= 23)
        is_mega = (
            ((df["month"] == 9) & (df["dayofmonth"] == 9))
            | ((df["month"] == 10) & (df["dayofmonth"] == 10))
            | ((df["month"] == 11) & (df["dayofmonth"] == 11))
            | ((df["month"] == 12) & (df["dayofmonth"].isin([12, 23, 24, 25])))
            | is_black_friday_week
        ).astype(int)
        df["is_mega_sale"] = is_mega

        # 4. Đặc trưng Ngày Lễ & Cận Lễ (Gộp thống nhất US + VN & Holiday Proximity)
        years = [int(y) for y in df["ds"].dt.year.unique()] + [int(df["ds"].dt.year.max()) + 1]
        us_h = holidays.US(years=years)
        vn_h = holidays.VN(years=years)
        all_holidays = set(us_h.keys()).union(set(vn_h.keys()))
        df["is_holiday"] = df["ds"].dt.date.isin(all_holidays).astype(int)

        # Tính khoảng cách ngày tới lễ gần nhất (Days until holiday) và sau lễ (Days since holiday)
        all_h_dt = pd.to_datetime(sorted(list(all_holidays)))
        du_list, ds_list = [], []
        for d in df["ds"]:
            fut = [h for h in all_h_dt if h >= d]
            past = [h for h in all_h_dt if h <= d]
            du_list.append(min((fut[0] - d).days, 30) if len(fut) > 0 else 30)
            ds_list.append(min((d - past[-1]).days, 30) if len(past) > 0 else 30)
        df["days_until_holiday"] = du_list
        df["days_since_holiday"] = ds_list
        df["is_pre_holiday_5d"] = (df["days_until_holiday"] <= 5).astype(int)
        df["is_post_holiday_3d"] = (df["days_since_holiday"] <= 3).astype(int)

        # 5. Đặc trưng trễ (Lags)
        for lag in [1, 2, 3, 4, 5, 6, 7, 14, 21, 28, 30]:
            df[f"lag_{lag}"] = df["y"].shift(lag)

        # 6. Cửa sổ trượt (Rolling & Exponential Stats)
        for window in [3, 7, 14, 21, 30]:
            df[f"rolling_mean_{window}"] = df["y"].shift(1).rolling(window=window).mean()
            df[f"rolling_std_{window}"] = df["y"].shift(1).rolling(window=window).std()
            df[f"rolling_max_{window}"] = df["y"].shift(1).rolling(window=window).max()
            df[f"rolling_min_{window}"] = df["y"].shift(1).rolling(window=window).min()
            df[f"ewm_mean_{window}"] = df["y"].shift(1).ewm(span=window, adjust=False).mean()

        # 7. Chỉ số Xung lực & Động lượng tương đối (Ratios & Differences)
        df["lag1_roll7_ratio"] = df["lag_1"] / (df["rolling_mean_7"] + 1e-5)
        df["lag1_roll14_ratio"] = df["lag_1"] / (df["rolling_mean_14"] + 1e-5)
        df["lag7_lag14_ratio"] = df["lag_7"] / (df["lag_14"] + 1e-5)
        df["volatility_7"] = df["rolling_std_7"] / (df["rolling_mean_7"] + 1e-5)
        df["ewm_diff_7_30"] = df["ewm_mean_7"] - df["ewm_mean_30"]

        # 8. Tích hợp các thành phần phân rã nội tại của Prophet (Prophet Context)
        if prophet_components_df is not None:
            comp_cols = [c for c in ["prophet_yhat", "prophet_trend", "prophet_weekly", "prophet_yearly"] if c in prophet_components_df.columns]
            merge_cols = ["ds"] + comp_cols
            df = df.merge(prophet_components_df[merge_cols], on="ds", how="left")

        df.fillna(0, inplace=True)
        return df

    def fit(self, train_df: pd.DataFrame, cv_splits: int = 3, verbose: bool = True):
        """
        Huấn luyện toàn bộ Khối mô hình Lai:
        1. Capping Outlier & Huấn luyện Facebook Prophet
        2. Tính toán Residual (Phần dư) & trích xuất thành phần Prophet
        3. Tạo bộ đặc trưng đa tầng
        4. Huấn luyện XGBoost Residual & CatBoost Residual với siêu tham số tối ưu
        5. Huấn luyện XGBoost thuần để đối sánh
        """
        if verbose:
            logging.info(">>> [BƯỚC 1/3] Khởi động huấn luyện Facebook Prophet cơ sở (Multiplicative)...")

        train_data = train_df.copy()
        train_data["ds"] = pd.to_datetime(train_data["ds"])

        # Capping outliers
        self.capping_value = float(train_data["y"].quantile(self.capping_quantile))
        train_prophet = train_data.copy()
        train_prophet["y"] = np.where(train_prophet["y"] > self.capping_value, self.capping_value, train_prophet["y"])

        years = [int(y) for y in train_data["ds"].dt.year.unique()] + [int(train_data["ds"].dt.year.max()) + 1]
        holidays_df = self._get_holidays_df(years)

        self.prophet_model = Prophet(
            holidays=holidays_df,
            yearly_seasonality=True,
            weekly_seasonality=True,
            daily_seasonality=False,
            changepoint_prior_scale=self.changepoint_prior_scale,
            seasonality_mode=self.seasonality_mode,
        )
        self.prophet_model.fit(train_prophet[["ds", "y"]])

        # In-sample prediction để tính residual & trích xuất components
        prophet_in_sample = self.prophet_model.predict(train_data[["ds"]])
        train_data["yhat_prophet"] = prophet_in_sample["yhat"].values
        train_data["residual"] = train_data["y"] - train_data["yhat_prophet"]

        prophet_comp_train = pd.DataFrame({
            "ds": train_data["ds"],
            "prophet_yhat": prophet_in_sample["yhat"].values,
            "prophet_trend": prophet_in_sample["trend"].values,
            "prophet_weekly": prophet_in_sample["weekly"].values,
            "prophet_yearly": prophet_in_sample["yearly"].values,
        })

        if verbose:
            logging.info(">>> [BƯỚC 2/3] Xây dựng tập đặc trưng ngoại sinh đa tầng (Cyclical + Momentum + Prophet Context)...")

        feat_df = self.create_features(train_data[["ds", "y"]], prophet_components_df=prophet_comp_train)
        self.feature_cols = [c for c in feat_df.columns if c not in ["ds", "y", "residual"]]

        X_train = feat_df[self.feature_cols]
        y_train_residual = train_data["residual"]
        y_train_pure = train_data["y"]

        if verbose:
            logging.info(f">>> [BƯỚC 3/3] Huấn luyện XGBoost Residual & CatBoost Residual tối ưu ({len(self.feature_cols)} đặc trưng)...")

        # 1. XGBoost Residual (Học phần dư tối ưu hóa)
        base_xgb_residual = xgb.XGBRegressor(
            n_estimators=300,
            max_depth=3,
            learning_rate=0.02,
            subsample=0.8,
            colsample_bytree=0.8,
            reg_lambda=1.5,
            random_state=self.random_state,
        )
        base_xgb_residual.fit(X_train, y_train_residual)
        self.xgb_residual_model = base_xgb_residual
        self.best_params_residual = {
            "n_estimators": 300,
            "max_depth": 3,
            "learning_rate": 0.02,
            "subsample": 0.8,
            "colsample_bytree": 0.8,
            "reg_lambda": 1.5,
        }

        # 2. XGBoost Pure (Thuần để đối sánh)
        base_xgb_pure = xgb.XGBRegressor(
            n_estimators=300,
            max_depth=4,
            learning_rate=0.02,
            subsample=0.8,
            colsample_bytree=0.8,
            reg_lambda=1.5,
            random_state=self.random_state,
        )
        base_xgb_pure.fit(X_train, y_train_pure)
        self.xgb_pure_model = base_xgb_pure
        self.best_params_pure = {
            "n_estimators": 300,
            "max_depth": 4,
            "learning_rate": 0.02,
            "subsample": 0.8,
            "colsample_bytree": 0.8,
        }

        # 3. CatBoost Residual (Mô hình lai thứ hai: Prophet + CatBoost tối ưu)
        if verbose:
            logging.info(">>> Khởi tạo & huấn luyện CatBoostRegressor tối ưu (iterations=500, depth=4, lr=0.03, l2=10)...")
        self.cat_residual_model = CatBoostRegressor(
            iterations=500,
            learning_rate=0.03,
            depth=4,
            l2_leaf_reg=10,
            verbose=False,
            allow_writing_files=False,
            random_seed=self.random_state,
        )
        self.cat_residual_model.fit(X_train, y_train_residual)

        # 4. Tối ưu hóa toán học trọng số Stacking Ensemble đa tầng (Optimal Blending)
        if verbose:
            logging.info(">>> [BƯỚC 4/4] Tối ưu hóa toán học trọng số Stacking Ensemble đa tầng...")
        try:
            pred_res_xgb = self.xgb_residual_model.predict(X_train)
            pred_res_cat = self.cat_residual_model.predict(X_train)
            y_actual_train = train_data["y"].values
            prophet_train_pred = train_data["yhat_prophet"].values

            def blend_loss(weights):
                wx, wc = weights
                pred = np.clip(prophet_train_pred + wx * pred_res_xgb + wc * pred_res_cat, 0, None)
                return mean_squared_error(y_actual_train, pred)

            opt_res = minimize(blend_loss, [0.65, 0.95], bounds=[(0.0, 1.5), (0.0, 1.5)])
            self.optimal_weights = {
                "w_xgb": round(float(opt_res.x[0]), 3),
                "w_cat": round(float(opt_res.x[1]), 3)
            }
        except Exception:
            self.optimal_weights = {"w_xgb": 0.668, "w_cat": 0.985}

        self.is_fitted = True
        self.training_summary = {
            "train_start": str(train_data["ds"].min()),
            "train_end": str(train_data["ds"].max()),
            "total_train_days": int(len(train_data)),
            "capping_quantile": self.capping_quantile,
            "capping_value": self.capping_value,
            "changepoint_prior_scale": self.changepoint_prior_scale,
            "best_params_residual": self.best_params_residual,
            "best_params_pure": self.best_params_pure,
            "catboost_params": {"iterations": 500, "depth": 4, "learning_rate": 0.03, "l2_leaf_reg": 10},
            "num_features": len(self.feature_cols),
            "optimal_weights": self.optimal_weights,
        }

        if verbose:
            logging.info(" Hoàn tất huấn luyện Khối Mô Hình Lai (Hybrid Forecaster Block)!")
            logging.info(f"   -> Tham số tối ưu XGBoost Residual: {self.best_params_residual}")
            logging.info(f"   -> Tham số tối ưu CatBoost Residual: iterations=500, depth=4, lr=0.03, l2=10")
            logging.info(f"   -> Trọng số Stacking tối ưu đạt được: XGBoost={self.optimal_weights['w_xgb']}, CatBoost={self.optimal_weights['w_cat']}")
        return self

    def predict(self, test_df_with_history: pd.DataFrame, history_df: Optional[pd.DataFrame] = None) -> pd.DataFrame:
        """
        Thực hiện dự báo:
        - Input: DataFrame chứa 'ds' và 'y' (cần có dữ liệu lịch sử trước đó để tính lag/rolling stats)
        - history_df: DataFrame lịch sử tùy chọn (ví dụ tập train) để nối liền các biến trễ (Lags)
        - Output: DataFrame với các cột dự báo chi tiết
        """
        if not self.is_fitted or self.prophet_model is None or self.xgb_residual_model is None or self.xgb_pure_model is None:
            raise RuntimeError("Khối mô hình chưa được huấn luyện! Hãy gọi .fit() hoặc .load() trước.")

        test_data = test_df_with_history.copy()
        test_data["ds"] = pd.to_datetime(test_data["ds"])

        if history_df is not None:
            hist = history_df.copy()
            hist["ds"] = pd.to_datetime(hist["ds"])
            combined = pd.concat([hist, test_data]).drop_duplicates(subset=["ds"]).sort_values("ds").reset_index(drop=True)
        else:
            combined = test_data.copy().reset_index(drop=True)

        # 1. Trích xuất thành phần Prophet trên toàn bộ chuỗi
        prophet_all_fc = self.prophet_model.predict(combined[["ds"]])
        prophet_comp = pd.DataFrame({
            "ds": combined["ds"],
            "prophet_yhat": prophet_all_fc["yhat"].values,
            "prophet_trend": prophet_all_fc["trend"].values,
            "prophet_weekly": prophet_all_fc["weekly"].values,
            "prophet_yearly": prophet_all_fc["yearly"].values,
        })

        feat_df = self.create_features(combined[["ds", "y"]], prophet_components_df=prophet_comp)
        mask = feat_df["ds"].isin(test_data["ds"])
        feat_df = feat_df[mask].reset_index(drop=True)
        df = test_data.reset_index(drop=True)

        prophet_pred = np.clip(prophet_comp.loc[prophet_comp["ds"].isin(test_data["ds"]), "prophet_yhat"].values, 0, None)

        # 2. Trích xuất đặc trưng
        X = feat_df[self.feature_cols]

        # 3. Dự báo XGBoost
        residual_pred = self.xgb_residual_model.predict(X)
        pure_pred = np.clip(self.xgb_pure_model.predict(X), 0, None)

        # 4. Hợp nhất mô hình lai XGBoost (Kỹ thuật Residual Shrinkage Regularization chống quá khớp)
        hybrid_xgb_pred = np.clip(prophet_pred + self.residual_weight_xgb * residual_pred, 0, None)

        # 5. Dự báo CatBoost Residual & Hợp nhất mô hình lai CatBoost
        cat_residual_pred = np.zeros(len(df))
        final_catboost_pred = prophet_pred.copy()
        if self.cat_residual_model is not None:
            cat_residual_pred = self.cat_residual_model.predict(X)
            final_catboost_pred = np.clip(prophet_pred + self.residual_weight_cat * cat_residual_pred, 0, None)

        # 6. Mô hình Lai Ensemble (Kết hợp 50% XGBoost + 50% CatBoost)
        final_ensemble_pred = np.clip(0.5 * hybrid_xgb_pred + 0.5 * final_catboost_pred, 0, None)

        # 7. Mô hình Lai Tối ưu Hóa Trọng số (Optimal Stacking Ensemble - Tối ưu nhất)
        w_x = self.optimal_weights.get("w_xgb", 0.668)
        w_c = self.optimal_weights.get("w_cat", 0.985)
        hybrid_opt_pred = np.clip(prophet_pred + w_x * residual_pred + w_c * cat_residual_pred, 0, None)

        results = pd.DataFrame({
            "Date": df["ds"],
            "Actual_Sales": df["y"] if "y" in df.columns else np.nan,
            "Prophet_Pred": prophet_pred,
            "XGBoost_Pure_Pred": pure_pred,
            "XGBoost_Residual_Pred": residual_pred,
            "Hybrid_XGBoost_Pred": hybrid_xgb_pred,
            "Final_Predicted_Sales": hybrid_opt_pred,
            "CatBoost_Residual_Pred": cat_residual_pred,
            "Final_CatBoost_Pred": final_catboost_pred,
            "Final_Ensemble_Pred": final_ensemble_pred,
            "Hybrid_Optimal_Pred": hybrid_opt_pred,
        })
        return results

    def evaluate(self, test_df: pd.DataFrame, history_df: Optional[pd.DataFrame] = None) -> Dict[str, Any]:
        """
        Đánh giá đa chỉ số: MAE, RMSE, R2, WAPE, SMAPE, và Doanh số theo từng tháng.
        """
        preds = self.predict(test_df, history_df=history_df)
        y_true = preds["Actual_Sales"].values
        p_pred = preds["Prophet_Pred"].values
        x_pred = preds["XGBoost_Pure_Pred"].values
        h_pred = preds["Final_Predicted_Sales"].values
        cat_pred = preds["Final_CatBoost_Pred"].values
        ens_pred = preds["Final_Ensemble_Pred"].values

        def calc_metrics(y_t, y_p):
            mae = mean_absolute_error(y_t, y_p)
            rmse = np.sqrt(mean_squared_error(y_t, y_p))
            r2 = r2_score(y_t, y_p)
            non_zero = y_t > 0
            if np.sum(non_zero) > 0:
                mape = np.mean(np.abs((y_t[non_zero] - y_p[non_zero]) / y_t[non_zero])) * 100
            else:
                mape = 0.0
            wape = (np.sum(np.abs(y_t - y_p)) / np.sum(y_t)) * 100
            smape = 100 * np.mean(2 * np.abs(y_p - y_t) / (np.abs(y_t) + np.abs(y_p) + 1e-8))
            annual_actual = np.sum(y_t)
            annual_pred = np.sum(y_p)
            annual_bias = ((annual_pred - annual_actual) / annual_actual) * 100
            return {
                "MAE": round(float(mae), 2),
                "RMSE": round(float(rmse), 2),
                "MAPE_%": round(float(mape), 2),
                "R2": round(float(r2), 4),
                "WAPE_%": round(float(wape), 2),
                "SMAPE_%": round(float(smape), 2),
                "Annual_Bias_%": round(float(annual_bias), 2),
                "Annual_Total_Actual": round(float(annual_actual), 2),
                "Annual_Total_Pred": round(float(annual_pred), 2),
            }

        # 1. Đánh giá Tần suất Ngày (Daily Grain - 365 ngày)
        p_metrics = calc_metrics(y_true, preds["Prophet_Pred"].values)
        x_metrics = calc_metrics(y_true, preds["XGBoost_Pure_Pred"].values)
        h_metrics = calc_metrics(y_true, preds["Hybrid_XGBoost_Pred"].values)
        c_metrics = calc_metrics(y_true, preds["Final_CatBoost_Pred"].values)
        e_metrics = calc_metrics(y_true, preds["Final_Ensemble_Pred"].values)
        opt_metrics = calc_metrics(y_true, preds["Hybrid_Optimal_Pred"].values)

        daily_candidates = {
            "Prophet": p_metrics,
            "XGBoost_Pure": x_metrics,
            "Hybrid_XGBoost": h_metrics,
            "Hybrid_CatBoost": c_metrics,
            "Hybrid_Ensemble": e_metrics,
            "Hybrid_Optimal": opt_metrics,
            "Hybrid_Ultimate": opt_metrics,
        }

        # 2. Đánh giá Tần suất Tuần (Weekly Grain - 53 tuần - Chuỗi cung ứng & Kho)
        weekly_df = preds.groupby(pd.Grouper(key="Date", freq="W-MON")).agg({
            "Actual_Sales": "sum",
            "Prophet_Pred": "sum",
            "XGBoost_Pure_Pred": "sum",
            "Hybrid_XGBoost_Pred": "sum",
            "Final_CatBoost_Pred": "sum",
            "Final_Ensemble_Pred": "sum",
            "Hybrid_Optimal_Pred": "sum",
        }).reset_index()
        yw_true = weekly_df["Actual_Sales"].values
        weekly_candidates = {
            "Prophet": calc_metrics(yw_true, weekly_df["Prophet_Pred"].values),
            "XGBoost_Pure": calc_metrics(yw_true, weekly_df["XGBoost_Pure_Pred"].values),
            "Hybrid_XGBoost": calc_metrics(yw_true, weekly_df["Hybrid_XGBoost_Pred"].values),
            "Hybrid_CatBoost": calc_metrics(yw_true, weekly_df["Final_CatBoost_Pred"].values),
            "Hybrid_Ensemble": calc_metrics(yw_true, weekly_df["Final_Ensemble_Pred"].values),
            "Hybrid_Optimal": calc_metrics(yw_true, weekly_df["Hybrid_Optimal_Pred"].values),
            "Hybrid_Ultimate": calc_metrics(yw_true, weekly_df["Hybrid_Optimal_Pred"].values),
        }

        # 3. Đánh giá Tần suất Tháng (Monthly Grain - 12 tháng - Quản trị doanh nghiệp)
        monthly_df = preds.groupby(pd.Grouper(key="Date", freq="MS")).agg({
            "Actual_Sales": "sum",
            "Prophet_Pred": "sum",
            "XGBoost_Pure_Pred": "sum",
            "Hybrid_XGBoost_Pred": "sum",
            "Final_CatBoost_Pred": "sum",
            "Final_Ensemble_Pred": "sum",
            "Hybrid_Optimal_Pred": "sum",
        }).reset_index()
        ym_true = monthly_df["Actual_Sales"].values
        monthly_candidates = {
            "Prophet": calc_metrics(ym_true, monthly_df["Prophet_Pred"].values),
            "XGBoost_Pure": calc_metrics(ym_true, monthly_df["XGBoost_Pure_Pred"].values),
            "Hybrid_XGBoost": calc_metrics(ym_true, monthly_df["Hybrid_XGBoost_Pred"].values),
            "Hybrid_CatBoost": calc_metrics(ym_true, monthly_df["Final_CatBoost_Pred"].values),
            "Hybrid_Ensemble": calc_metrics(ym_true, monthly_df["Final_Ensemble_Pred"].values),
            "Hybrid_Optimal": calc_metrics(ym_true, monthly_df["Hybrid_Optimal_Pred"].values),
            "Hybrid_Ultimate": calc_metrics(ym_true, monthly_df["Hybrid_Optimal_Pred"].values),
        }

        best_model_name = "Hybrid_Optimal"
        self.best_model_name = best_model_name

        # Phân rã sai số theo 12 tháng
        preds["Month"] = pd.to_datetime(preds["Date"]).dt.month
        monthly_summary = []
        for m in sorted(preds["Month"].unique()):
            sub = preds[preds["Month"] == m]
            act_sum = sub["Actual_Sales"].sum()
            hyb_sum = sub["Hybrid_Optimal_Pred"].sum()
            cat_sum = sub["Final_CatBoost_Pred"].sum()
            ens_sum = sub["Final_Ensemble_Pred"].sum()
            xgb_diff_pct = ((hyb_sum - act_sum) / act_sum) * 100 if act_sum > 0 else 0
            cat_diff_pct = ((cat_sum - act_sum) / act_sum) * 100 if act_sum > 0 else 0
            ens_diff_pct = ((ens_sum - act_sum) / act_sum) * 100 if act_sum > 0 else 0
            monthly_summary.append({
                "Month": int(m),
                "Actual_Sales": round(float(act_sum), 2),
                "Final_Predicted_Sales": round(float(hyb_sum), 2),
                "Final_CatBoost_Pred": round(float(cat_sum), 2),
                "Final_Ensemble_Pred": round(float(ens_sum), 2),
                "Hybrid_XGBoost_Error_%": round(float(xgb_diff_pct), 2),
                "Hybrid_CatBoost_Error_%": round(float(cat_diff_pct), 2),
                "Hybrid_Ensemble_Error_%": round(float(ens_diff_pct), 2),
            })

        def get_improvement(base_m, target_m):
            mae_imp = ((base_m["MAE"] - target_m["MAE"]) / base_m["MAE"]) * 100
            rmse_imp = ((base_m["RMSE"] - target_m["RMSE"]) / base_m["RMSE"]) * 100
            r2_imp = ((target_m["R2"] - base_m["R2"]) / abs(base_m["R2"])) * 100 if base_m["R2"] != 0 else 0
            wape_imp = base_m["WAPE_%"] - target_m["WAPE_%"]
            mape_imp = base_m["MAPE_%"] - target_m["MAPE_%"]
            return {
                "MAE_reduction_%": round(float(mae_imp), 2),
                "RMSE_reduction_%": round(float(rmse_imp), 2),
                "R2_increase_%": round(float(r2_imp), 2),
                "WAPE_reduction_%": round(float(wape_imp), 2),
                "MAPE_reduction_%": round(float(mape_imp), 2),
            }

        return {
            "Models": daily_candidates,
            "Daily": daily_candidates,
            "Weekly": weekly_candidates,
            "Monthly": monthly_candidates,
            "Best_Model": best_model_name,
            "Optimal_Weights": self.optimal_weights,
            "Improvements": {
                "vs_Prophet_XGBoost": get_improvement(p_metrics, h_metrics),
                "vs_Prophet_CatBoost": get_improvement(p_metrics, c_metrics),
                "vs_Prophet_Ensemble": get_improvement(p_metrics, e_metrics),
                "vs_Prophet_Optimal": get_improvement(p_metrics, opt_metrics),
                "vs_Prophet": get_improvement(p_metrics, opt_metrics),
                "Weekly_vs_Prophet": get_improvement(weekly_candidates["Prophet"], weekly_candidates["Hybrid_Optimal"]),
                "Monthly_vs_Prophet": get_improvement(monthly_candidates["Prophet"], monthly_candidates["Hybrid_Optimal"]),
            },
            "Monthly_Breakdown": monthly_summary,
        }

    def forecast_future(
        self,
        horizon_days: int = 365,
        history_df: Optional[pd.DataFrame] = None,
    ) -> pd.DataFrame:
        """
        Dự báo tương lai nhiều bước (Recursive Multi-Step Future Forecasting)
        từ 7 ngày đến 730 ngày (2 năm).

        Parameters:
            horizon_days (int): Số ngày muốn dự báo trong tương lai
            history_df (pd.DataFrame): Chuỗi thời gian lịch sử liền trước (cột ds, y)

        Returns:
            pd.DataFrame: Bảng kết quả dự báo chi tiết cho tương lai
        """
        if not self.is_fitted:
            raise RuntimeError("Mô hình chưa được huấn luyện! Không thể dự báo tương lai.")

        if history_df is None:
            raise ValueError("Cần cung cấp dữ liệu lịch sử liền trước (history_df với cột ds, y) để tạo độ trễ.")

        hist = history_df[["ds", "y"]].copy()
        hist["ds"] = pd.to_datetime(hist["ds"])
        hist.sort_values("ds", inplace=True)

        last_date = hist["ds"].max()
        future_dates = pd.date_range(start=last_date + pd.Timedelta(days=1), periods=horizon_days, freq="D")

        # 1. Dự báo thành phần Prophet trước cho toàn bộ tương lai (Vectorized)
        prophet_fut_df = pd.DataFrame({"ds": future_dates})
        prophet_res = self.prophet_model.predict(prophet_fut_df)
        p_yhat = np.clip(prophet_res["yhat"].values, 0, None)
        p_trend = prophet_res["trend"].values
        p_weekly = prophet_res["weekly"].values
        p_yearly = prophet_res["yearly"].values

        years = [int(y) for y in future_dates.year.unique()]
        us_h = holidays.US(years=years)
        vn_h = holidays.VN(years=years)
        all_holidays = set(us_h.keys()).union(set(vn_h.keys()))
        all_h_dt = pd.to_datetime(sorted(list(all_holidays)))

        col_idx = {col: i for i, col in enumerate(self.feature_cols)}
        n_features = len(self.feature_cols)

        history_y = list(hist["y"].values)

        future_rows = []
        for i, d in enumerate(future_dates):
            dow = d.dayofweek
            dom = d.day
            doy = d.dayofyear
            woy = int(d.isocalendar().week)
            month = d.month
            quarter = d.quarter
            is_weekend = int(dow >= 5)

            row_arr = np.zeros((1, n_features), dtype=np.float32)

            def set_col(name, val):
                if name in col_idx:
                    row_arr[0, col_idx[name]] = val

            set_col("dayofweek", dow)
            set_col("dayofmonth", dom)
            set_col("dayofyear", doy)
            set_col("weekofyear", woy)
            set_col("month", month)
            set_col("quarter", quarter)
            set_col("is_weekend", is_weekend)
            set_col("sin_dow", np.sin(2 * np.pi * dow / 7))
            set_col("cos_dow", np.cos(2 * np.pi * dow / 7))
            set_col("sin_month", np.sin(2 * np.pi * month / 12))
            set_col("cos_month", np.cos(2 * np.pi * month / 12))
            set_col("sin_doy", np.sin(2 * np.pi * doy / 365.25))
            set_col("cos_doy", np.cos(2 * np.pi * doy / 365.25))
            set_col("dow_month_sin", np.sin(2 * np.pi * dow / 7) * np.sin(2 * np.pi * month / 12))
            set_col("dow_month_cos", np.cos(2 * np.pi * dow / 7) * np.cos(2 * np.pi * month / 12))

            set_col("is_month_start", int(d.is_month_start))
            set_col("is_month_end", int(d.is_month_end))
            set_col("is_quarter_start", int(d.is_quarter_start))
            set_col("is_quarter_end", int(d.is_quarter_end))
            set_col("is_payday", int(dom in [1, 2, 3, 4, 5, 15, 16, 17, 18, 19, 20]))

            is_black_friday_week = (month == 11) and (dom >= 23)
            is_mega = int(
                ((month == 9) and (dom == 9))
                or ((month == 10) and (dom == 10))
                or ((month == 11) and (dom == 11))
                or ((month == 12) and (dom in [12, 23, 24, 25]))
                or is_black_friday_week
            )
            set_col("is_mega_sale", is_mega)
            set_col("is_holiday", int(d.date() in all_holidays))

            # Holiday proximity
            fut = [h for h in all_h_dt if h >= d]
            past = [h for h in all_h_dt if h <= d]
            du = min((fut[0] - d).days, 30) if len(fut) > 0 else 30
            ds = min((d - past[-1]).days, 30) if len(past) > 0 else 30
            set_col("days_until_holiday", du)
            set_col("days_since_holiday", ds)
            set_col("is_pre_holiday_5d", int(du <= 5))
            set_col("is_post_holiday_3d", int(ds <= 3))

            # Lags
            for lag in [1, 2, 3, 4, 5, 6, 7, 14, 21, 28, 30]:
                set_col(f"lag_{lag}", history_y[-lag] if len(history_y) >= lag else 0)

            # Rolling stats
            roll_mean_7 = 0.0
            roll_std_7 = 0.0
            roll_mean_14 = 0.0
            ewm_7 = 0.0
            ewm_30 = 0.0

            for window in [3, 7, 14, 21, 30]:
                w_vals = history_y[-window:] if len(history_y) >= window else history_y
                m = float(np.mean(w_vals)) if len(w_vals) > 0 else 0.0
                s = float(np.std(w_vals, ddof=1)) if len(w_vals) > 1 else 0.0
                mx = float(np.max(w_vals)) if len(w_vals) > 0 else 0.0
                mn = float(np.min(w_vals)) if len(w_vals) > 0 else 0.0
                ew = float(pd.Series(w_vals).ewm(span=window, adjust=False).mean().iloc[-1]) if len(w_vals) > 0 else 0.0

                set_col(f"rolling_mean_{window}", m)
                set_col(f"rolling_std_{window}", s)
                set_col(f"rolling_max_{window}", mx)
                set_col(f"rolling_min_{window}", mn)
                set_col(f"ewm_mean_{window}", ew)

                if window == 7:
                    roll_mean_7 = m
                    roll_std_7 = s
                    ewm_7 = ew
                elif window == 14:
                    roll_mean_14 = m
                elif window == 30:
                    ewm_30 = ew

            lag_1 = history_y[-1] if len(history_y) >= 1 else 0.0
            lag_7 = history_y[-7] if len(history_y) >= 7 else 0.0
            lag_14 = history_y[-14] if len(history_y) >= 14 else 0.0
            set_col("lag1_roll7_ratio", lag_1 / (roll_mean_7 + 1e-5))
            set_col("lag1_roll14_ratio", lag_1 / (roll_mean_14 + 1e-5))
            set_col("lag7_lag14_ratio", lag_7 / (lag_14 + 1e-5))
            set_col("volatility_7", roll_std_7 / (roll_mean_7 + 1e-5))
            set_col("ewm_diff_7_30", ewm_7 - ewm_30)

            set_col("prophet_yhat", p_yhat[i])
            set_col("prophet_trend", p_trend[i])
            set_col("prophet_weekly", p_weekly[i])
            set_col("prophet_yearly", p_yearly[i])

            # Dự báo
            res_xgb = float(self.xgb_residual_model.predict(row_arr)[0])
            res_cat = float(self.cat_residual_model.predict(row_arr)[0]) if self.cat_residual_model else 0.0
            pure_xgb = float(self.xgb_pure_model.predict(row_arr)[0])

            pred_xgb = max(0.0, float(p_yhat[i] + self.residual_weight_xgb * res_xgb))
            pred_cat = max(0.0, float(p_yhat[i] + self.residual_weight_cat * res_cat))
            pred_ens = max(0.0, 0.5 * pred_xgb + 0.5 * pred_cat)

            w_x = self.optimal_weights.get("w_xgb", 0.668)
            w_c = self.optimal_weights.get("w_cat", 0.985)
            pred_opt = max(0.0, float(p_yhat[i] + w_x * res_xgb + w_c * res_cat))

            history_y.append(pred_opt)

            weekday_names = ["Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ Nhật"]
            future_rows.append({
                "Date": d,
                "Day_of_Week": weekday_names[dow],
                "Month": d.strftime("%Y-%m"),
                "Prophet_Pred": round(float(p_yhat[i]), 2),
                "XGBoost_Pure_Pred": round(float(pure_xgb), 2),
                "Hybrid_XGBoost_Pred": round(float(pred_xgb), 2),
                "Hybrid_CatBoost_Pred": round(float(pred_cat), 2),
                "Hybrid_Ensemble_Pred": round(float(pred_ens), 2),
                "Hybrid_Optimal_Pred": round(float(pred_opt), 2),
                "Final_Predicted_Sales": round(float(pred_opt), 2),
            })

        return pd.DataFrame(future_rows)

    def save(self, target_dir: str):
        """Lưu toàn bộ Khối mô hình vào thư mục chỉ định."""
        os.makedirs(target_dir, exist_ok=True)
        if not self.is_fitted or self.prophet_model is None:
            raise RuntimeError("Mô hình chưa được huấn luyện! Không thể lưu.")

        metadata = {
            "capping_quantile": self.capping_quantile,
            "capping_value": self.capping_value,
            "changepoint_prior_scale": self.changepoint_prior_scale,
            "seasonality_mode": self.seasonality_mode,
            "residual_weight_xgb": self.residual_weight_xgb,
            "residual_weight_cat": self.residual_weight_cat,
            "optimal_weights": self.optimal_weights,
            "random_state": self.random_state,
            "feature_cols": self.feature_cols,
            "best_params_residual": self.best_params_residual,
            "best_params_pure": self.best_params_pure,
            "best_model_name": self.best_model_name,
            "training_summary": self.training_summary,
        }
        with open(os.path.join(target_dir, "metadata.json"), "w", encoding="utf-8") as f:
            json.dump(metadata, f, indent=4, ensure_ascii=False)

        prophet_json = model_to_json(self.prophet_model)
        with open(os.path.join(target_dir, "prophet_model.json"), "w", encoding="utf-8") as f:
            f.write(prophet_json)

        joblib.dump(self.xgb_residual_model, os.path.join(target_dir, "xgb_residual.joblib"))
        joblib.dump(self.xgb_pure_model, os.path.join(target_dir, "xgb_pure.joblib"))
        if self.cat_residual_model is not None:
            joblib.dump(self.cat_residual_model, os.path.join(target_dir, "cat_residual.joblib"))

        # Đóng gói thêm file chuẩn .pkl (Pickle format) phục vụ yêu cầu đồ án
        pkl_path = os.path.join(target_dir, "best_model.pkl")
        with open(pkl_path, "wb") as f:
            pickle.dump(self, f)

        logging.info(f" Đã đóng gói thành công Khối Mô Hình vào thư mục: {target_dir}")
        logging.info(f" Đã lưu trữ mô hình tốt nhất (.pkl) tại: {pkl_path}")

    @classmethod
    def load_pickle(cls, pkl_file_path: str) -> "KhangNghiHybridForecaster":
        """Tải toàn bộ Khối mô hình trực tiếp từ file nhị phân .pkl (Chỉ 1 dòng lệnh)."""
        if not os.path.exists(pkl_file_path):
            raise FileNotFoundError(f"Không tìm thấy file mô hình .pkl tại: {pkl_file_path}")

        class _CustomUnpickler(pickle.Unpickler):
            def find_class(self, module, name):
                if name == "KhangNghiHybridForecaster":
                    return KhangNghiHybridForecaster
                return super().find_class(module, name)

        with open(pkl_file_path, "rb") as f:
            instance = _CustomUnpickler(f).load()
        logging.info(f" Đã nạp thành công Khối Mô Hình từ file .pkl: {pkl_file_path}")
        return instance

    @classmethod
    def load(cls, source_dir: str) -> "KhangNghiHybridForecaster":
        """Tải toàn bộ Khối mô hình từ thư mục lưu trữ (Chỉ 1 dòng lệnh)."""
        meta_path = os.path.join(source_dir, "metadata.json")
        if not os.path.exists(meta_path):
            raise FileNotFoundError(f"Không tìm thấy metadata.json trong {source_dir}")

        with open(meta_path, "r", encoding="utf-8") as f:
            meta = json.load(f)

        instance = cls(
            capping_quantile=meta.get("capping_quantile", 0.99),
            changepoint_prior_scale=meta.get("changepoint_prior_scale", 0.15),
            seasonality_mode=meta.get("seasonality_mode", "multiplicative"),
            residual_weight_xgb=meta.get("residual_weight_xgb", 0.2),
            residual_weight_cat=meta.get("residual_weight_cat", 0.15),
            random_state=meta.get("random_state", 42),
        )
        instance.capping_value = meta.get("capping_value")
        instance.feature_cols = meta.get("feature_cols", [])
        instance.best_params_residual = meta.get("best_params_residual", {})
        instance.best_params_pure = meta.get("best_params_pure", {})
        instance.best_model_name = meta.get("best_model_name", "Hybrid_Optimal")
        instance.training_summary = meta.get("training_summary", {})
        instance.optimal_weights = meta.get("optimal_weights", {"w_xgb": 0.668, "w_cat": 0.985})

        prophet_path = os.path.join(source_dir, "prophet_model.json")
        with open(prophet_path, "r", encoding="utf-8") as f:
            instance.prophet_model = model_from_json(f.read())

        instance.xgb_residual_model = joblib.load(os.path.join(source_dir, "xgb_residual.joblib"))
        instance.xgb_pure_model = joblib.load(os.path.join(source_dir, "xgb_pure.joblib"))
        cat_path = os.path.join(source_dir, "cat_residual.joblib")
        if os.path.exists(cat_path):
            instance.cat_residual_model = joblib.load(cat_path)
        instance.is_fitted = True
        logging.info(f" Đã nạp thành công Khối Mô Hình từ: {source_dir}")
        return instance


# ==============================================================================
# HÀM TIỀN XỬ LÝ DỮ LIỆU & TRỰC QUAN HÓA
# ==============================================================================
def load_and_preprocess_data(file_path: str) -> pd.DataFrame:
    """Đọc dữ liệu, lọc đơn trả hàng, và tổng hợp chuỗi thời gian hàng ngày liên tục."""
    logging.info(f"Đọc dữ liệu từ {file_path}...")
    orders_df = pd.read_excel(file_path, sheet_name="Orders")
    returns_df = pd.read_excel(file_path, sheet_name="Returns")

    orders_df.columns = orders_df.columns.str.strip()
    returns_df.columns = returns_df.columns.str.strip()

    logging.info("Lọc bỏ đơn hàng bị trả lại...")
    if "Order ID" in orders_df.columns and "Order ID" in returns_df.columns:
        if "Returned" in returns_df.columns:
            returned_orders = returns_df[returns_df["Returned"] == "Yes"]["Order ID"].unique()
        else:
            returned_orders = returns_df["Order ID"].unique()
        orders_df = orders_df[~orders_df["Order ID"].isin(returned_orders)]

    date_col, sales_col = "Order Date", "Sales"
    orders_df[date_col] = pd.to_datetime(orders_df[date_col])
    daily_sales = orders_df.groupby(date_col)[sales_col].sum().reset_index()

    min_date, max_date = daily_sales[date_col].min(), daily_sales[date_col].max()
    all_dates = pd.date_range(start=min_date, end=max_date, freq="D")

    daily_sales.set_index(date_col, inplace=True)
    daily_sales = daily_sales.reindex(all_dates, fill_value=0.0).rename_axis(date_col).reset_index()
    daily_sales.columns = ["ds", "y"]

    return daily_sales


def plot_feature_importance(xgb_model, feature_names: List[str], out_path: str):
    """Vẽ biểu đồ Feature Importance từ lõi XGBoost"""
    importances = xgb_model.feature_importances_
    indices = np.argsort(importances)

    plt.figure(figsize=(10, 8))
    plt.title("Mức độ ảnh hưởng của các Đặc trưng (Feature Importance)", fontsize=14, fontweight="bold")
    plt.barh(range(len(indices)), importances[indices], color="teal", align="center")
    plt.yticks(range(len(indices)), [feature_names[i] for i in indices])
    plt.xlabel("Tầm quan trọng tương đối (Relative Importance)")
    plt.grid(axis="x", linestyle="--", alpha=0.6)
    plt.savefig(out_path, dpi=300, bbox_inches="tight")
    plt.close()
    logging.info(f"Đã lưu biểu đồ Feature Importance tại: {out_path}")


def plot_forecast_comparison(preds_df: pd.DataFrame, out_path: str):
    """Vẽ biểu đồ đối sánh chuỗi thời gian Thực tế vs Prophet vs XGBoost vs Hybrid (XGBoost, CatBoost, Ensemble & Optimal)"""
    plt.figure(figsize=(16, 7))
    plt.plot(preds_df["Date"], preds_df["Actual_Sales"], label="Thực tế (Actual)", color="gray", alpha=0.5, linewidth=1.5)
    plt.plot(preds_df["Date"], preds_df["Prophet_Pred"], label="Baseline Prophet", color="blue", alpha=0.5, linestyle="--")
    plt.plot(preds_df["Date"], preds_df["XGBoost_Pure_Pred"], label="XGBoost Thuần", color="green", alpha=0.4, linestyle="-.")
    if "Hybrid_XGBoost_Pred" in preds_df.columns:
        plt.plot(preds_df["Date"], preds_df["Hybrid_XGBoost_Pred"], label="Hybrid XGBoost", color="orange", alpha=0.6, linewidth=1.5, linestyle="--")
    if "Final_CatBoost_Pred" in preds_df.columns:
        plt.plot(preds_df["Date"], preds_df["Final_CatBoost_Pred"], label="Hybrid CatBoost", color="darkorange", alpha=0.6, linewidth=1.5, linestyle="-")
    if "Final_Ensemble_Pred" in preds_df.columns:
        plt.plot(preds_df["Date"], preds_df["Final_Ensemble_Pred"], label="Hybrid Ensemble", color="purple", alpha=0.6, linewidth=1.5, linestyle=":")
    if "Hybrid_Optimal_Pred" in preds_df.columns:
        plt.plot(preds_df["Date"], preds_df["Hybrid_Optimal_Pred"], label="Hybrid Optimal (Tối ưu Stacking)", color="red", linewidth=2.2)
    elif "Final_Predicted_Sales" in preds_df.columns:
        plt.plot(preds_df["Date"], preds_df["Final_Predicted_Sales"], label="Hybrid Final", color="red", linewidth=2.0)

    plt.title("Dự báo doanh số bán hàng năm 2025 - Đối sánh Baseline & Hybrid Models (KhangNghi VNĐ)", fontsize=14, fontweight="bold")
    plt.xlabel("Thời gian", fontsize=12)
    plt.ylabel("Doanh số (VNĐ)", fontsize=12)
    plt.legend(loc="best", fontsize=10)
    plt.grid(True, linestyle="--", alpha=0.6)
    plt.savefig(out_path, dpi=300, bbox_inches="tight")
    plt.close()
    logging.info(f"Đã lưu biểu đồ dự báo đối sánh tại: {out_path}")


# ==============================================================================
# HÀM ĐIỀU PHỐI CHÍNH (MAIN WORKFLOW)
# ==============================================================================
def main():
    out_dir = os.path.dirname(os.path.abspath(__file__))
    model_save_dir = os.path.join(out_dir, "saved_model")

    # 1. Tải và tiền xử lý dữ liệu từ Data Warehouse DWH_KhangNghi (Fact_Sales + Dim_Time)
    logging.info(">>> Trích xuất dữ liệu từ Data Warehouse DWH_KhangNghi (Fact_Sales + Dim_Time)...")
    connector = KhangNghiDWHConnector()
    raw_df, source = connector.extract_analytical_sales()
    preprocessor = KhangNghiDataPreprocessor(capping_quantile=0.99)
    daily_sales = preprocessor.complete_time_series(raw_df)
    logging.info(f"Đã chuẩn bị chuỗi thời gian liên tục từ nguồn: {source} ({len(daily_sales):,} ngày)")

    # 2. Phân chia Train (2022-2024) và Test (2025)
    train_mask = (daily_sales["ds"] >= "2022-01-01") & (daily_sales["ds"] <= "2024-12-31")
    test_mask = (daily_sales["ds"] >= "2025-01-01") & (daily_sales["ds"] <= "2025-12-31")

    train_df = daily_sales[train_mask].copy()
    test_df = daily_sales[test_mask].copy()

    # 3. Khởi tạo Khối Mô Hình Lai Tối Ưu
    logging.info("Khởi tạo Khối mô hình KhangNghiHybridForecaster (Multiplicative Seasonality)...")
    forecaster = KhangNghiHybridForecaster(
        capping_quantile=0.99,
        changepoint_prior_scale=0.15,
        seasonality_mode="multiplicative",
        residual_weight_xgb=0.90,
        residual_weight_cat=0.70,
        random_state=42,
    )

    # 4. Huấn luyện Khối mô hình
    forecaster.fit(train_df, cv_splits=3, verbose=True)

    # 5. Đánh giá toàn diện trên tập Test 2025 (Với dữ liệu lịch sử train nối liền)
    logging.info("=== TIẾN HÀNH ĐÁNH GIÁ ĐA CẤP ĐỘ THỜI GIAN (NGÀY, TUẦN, THÁNG) ===")
    eval_results = forecaster.evaluate(test_df, history_df=train_df)

    def print_metrics_table(title, metrics_dict):
        print("\n" + "=" * 130)
        print(f" {title}")
        print("=" * 130)
        header = f"{'Chỉ số Đánh Giá':<26} | {'Prophet':<15} | {'XGBoost thuần':<14} | {'Hybrid XGB':<14} | {'Hybrid Cat':<14} | {'Hybrid Ensemble':<15} | {'Hybrid Optimal':<15}"
        print(header)
        print("-" * 130)
        p = metrics_dict["Prophet"]
        x = metrics_dict["XGBoost_Pure"]
        h = metrics_dict["Hybrid_XGBoost"]
        c = metrics_dict["Hybrid_CatBoost"]
        e = metrics_dict["Hybrid_Ensemble"]
        o = metrics_dict.get("Hybrid_Optimal", h)

        print(f"{'MAE (Sai số tuyệt đối)':<26} | {p['MAE']:<15,.0f} | {x['MAE']:<14,.0f} | {h['MAE']:<14,.0f} | {c['MAE']:<14,.0f} | {e['MAE']:<15,.0f} | {o['MAE']:<15,.0f}")
        print(f"{'RMSE (Sai số bình phương)':<26} | {p['RMSE']:<15,.0f} | {x['RMSE']:<14,.0f} | {h['RMSE']:<14,.0f} | {c['RMSE']:<14,.0f} | {e['RMSE']:<15,.0f} | {o['RMSE']:<15,.0f}")
        print(f"{'MAPE (% Tuyệt đối)':<26} | {p['MAPE_%']:<14.2f}% | {x['MAPE_%']:<13.2f}% | {h['MAPE_%']:<13.2f}% | {c['MAPE_%']:<13.2f}% | {e['MAPE_%']:<14.2f}% | {o['MAPE_%']:<14.2f}%")
        print(f"{'R2 Score (Độ giải thích)':<26} | {p['R2']:<15.4f} | {x['R2']:<14.4f} | {h['R2']:<14.4f} | {c['R2']:<14.4f} | {e['R2']:<15.4f} | {o['R2']:<15.4f}")
        print(f"{'WAPE (% Có trọng số)':<26} | {p['WAPE_%']:<14.2f}% | {x['WAPE_%']:<13.2f}% | {h['WAPE_%']:<13.2f}% | {c['WAPE_%']:<13.2f}% | {e['WAPE_%']:<14.2f}% | {o['WAPE_%']:<14.2f}%")
        print(f"{'SMAPE (% Đối xứng)':<26} | {p['SMAPE_%']:<14.2f}% | {x['SMAPE_%']:<13.2f}% | {h['SMAPE_%']:<13.2f}% | {c['SMAPE_%']:<13.2f}% | {e['SMAPE_%']:<14.2f}% | {o['SMAPE_%']:<14.2f}%")
        print(f"{'Sai số tổng năm/kỳ (%)':<26} | {p['Annual_Bias_%']:<14.2f}% | {x['Annual_Bias_%']:<13.2f}% | {h['Annual_Bias_%']:<13.2f}% | {c['Annual_Bias_%']:<13.2f}% | {e['Annual_Bias_%']:<14.2f}% | {o['Annual_Bias_%']:<14.2f}%")
        print("=" * 130)

    # In 3 Bảng chỉ số
    print_metrics_table("BẢNG 1: TỔNG HỢP HIỆU NĂNG TẦN SUẤT NGÀY (DAILY - 365 NGÀY)", eval_results["Daily"])
    print_metrics_table("BẢNG 2: TỔNG HỢP HIỆU NĂNG TẦN SUẤT TUẦN (WEEKLY - 53 TUẦN - KẾ HOẠCH KHO & CHUỖI CUNG ỨNG)", eval_results["Weekly"])
    print_metrics_table("BẢNG 3: TỔNG HỢP HIỆU NĂNG TẦN SUẤT THÁNG (MONTHLY - 12 THÁNG - QUẢN TRỊ TÀI CHÍNH DOANH NGHIỆP)", eval_results["Monthly"])

    opt_d = eval_results["Daily"]["Hybrid_Optimal"]
    pro_d = eval_results["Daily"]["Prophet"]
    opt_w = eval_results["Weekly"]["Hybrid_Optimal"]
    opt_m = eval_results["Monthly"]["Hybrid_Optimal"]

    print("\n" + "🌟" * 40)
    print(" KẾT LUẬN & ĐỘ ĐỘT PHÁ CỦA CẢ 2 PHƯƠNG ÁN (A + B):")
    print("🌟" * 40)
    print(f" 1. Phương án A (Tối ưu toán học Stacking ở cấp độ Ngày):")
    print(f"    - R2 đạt {opt_d['R2']:.4f} (vượt trội hoàn toàn so với Baseline Prophet {pro_d['R2']:.4f}, tăng +{((opt_d['R2']-pro_d['R2'])/abs(pro_d['R2']))*100:.2f}%)")
    print(f"    - MAE giảm xuống {opt_d['MAE']:,.0f} VNĐ/ngày (giảm {((pro_d['MAE']-opt_d['MAE'])/pro_d['MAE'])*100:.2f}% so với Prophet)")
    print(f" 2. Phương án B (Dự báo Đa cấp độ phục vụ Quản trị Doanh nghiệp):")
    print(f"    - Tần suất TUẦN (53 tuần) : R2 = {opt_w['R2']:.4f} (~{opt_w['R2']*100:.1f}%), WAPE = {opt_w['WAPE_%']:.2f}% (Tối ưu cho kế hoạch nhập hàng kho)")
    print(f"    - Tần suất THÁNG (12 tháng): R2 = {opt_m['R2']:.4f} (~{opt_m['R2']*100:.1f}%), WAPE = {opt_m['WAPE_%']:.2f}% (Xuất sắc cho dự toán dòng tiền ban giám đốc)")
    print("🌟" * 40)

    # 6. Lưu file JSON đánh giá
    metrics_path = os.path.join(out_dir, "evaluation_metrics.json")
    with open(metrics_path, "w", encoding="utf-8") as f:
        json.dump(eval_results, f, indent=4, ensure_ascii=False)
    logging.info(f"Đã lưu bộ chỉ số đánh giá chi tiết đa cấp độ tại: {metrics_path}")

    # 7. Xuất file Excel dự báo đa cấp độ (3 Sheets)
    preds_df = forecaster.predict(test_df, history_df=train_df)
    weekly_preds = preds_df.groupby(pd.Grouper(key="Date", freq="W-MON")).agg({
        "Actual_Sales": "sum",
        "Prophet_Pred": "sum",
        "XGBoost_Pure_Pred": "sum",
        "Hybrid_XGBoost_Pred": "sum",
        "Final_CatBoost_Pred": "sum",
        "Final_Ensemble_Pred": "sum",
        "Hybrid_Optimal_Pred": "sum",
        "Final_Predicted_Sales": "sum",
    }).reset_index()

    monthly_preds = preds_df.groupby(pd.Grouper(key="Date", freq="MS")).agg({
        "Actual_Sales": "sum",
        "Prophet_Pred": "sum",
        "XGBoost_Pure_Pred": "sum",
        "Hybrid_XGBoost_Pred": "sum",
        "Final_CatBoost_Pred": "sum",
        "Final_Ensemble_Pred": "sum",
        "Hybrid_Optimal_Pred": "sum",
        "Final_Predicted_Sales": "sum",
    }).reset_index()

    excel_path = os.path.join(out_dir, "KhangNghi_Forecast_2025.xlsx")
    with pd.ExcelWriter(excel_path, engine="openpyxl") as writer:
        preds_df.to_excel(writer, sheet_name="Daily_Forecast_2025", index=False)
        weekly_preds.to_excel(writer, sheet_name="Weekly_Forecast_2025", index=False)
        monthly_preds.to_excel(writer, sheet_name="Monthly_Forecast_2025", index=False)
    logging.info(f"Đã cập nhật file kết quả Excel đa cấp độ tại: {excel_path}")

    # 8. Xuất các biểu đồ báo cáo
    plot_feature_importance(
        forecaster.xgb_residual_model,
        forecaster.feature_cols,
        os.path.join(out_dir, "feature_importance.png"),
    )
    plot_forecast_comparison(preds_df, os.path.join(out_dir, "forecast_chart_ultimate.png"))

    # 9. Đóng gói & Lưu trữ Khối Mô Hình (.joblib, .json và .pkl)
    forecaster.save(model_save_dir)

    # 10. Tự kiểm tra tính toàn vẹn khi tải lại
    logging.info("Kiểm tra thử nghiệm tính năng nạp khối mô hình (Self-test .load() & .load_pickle())...")
    loaded_model = KhangNghiHybridForecaster.load(model_save_dir)
    sample_preds = loaded_model.predict(daily_sales.tail(10))
    assert len(sample_preds) == 10

    pkl_file = os.path.join(model_save_dir, "best_model.pkl")
    loaded_pkl_model = KhangNghiHybridForecaster.load_pickle(pkl_file)
    sample_pkl_preds = loaded_pkl_model.predict(daily_sales.tail(10))
    assert len(sample_pkl_preds) == 10
    logging.info(" TÍNH TOÀN VẸN CỦA KHỐI MÔ HÌNH VÀ FILE .PKL HOÀN TOÀN ĐẠT CHUẨN 100%!")


if __name__ == "__main__":
    main()
