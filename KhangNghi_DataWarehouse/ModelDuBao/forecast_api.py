from datetime import date
from pathlib import Path

import pandas as pd
import json
import pickle
from fastapi import FastAPI, HTTPException
from prophet.serialize import model_from_json


BASE_DIR = Path(__file__).resolve().parent
MODEL_PATH = BASE_DIR / "saved_model" / "prophet_web_model.pkl"
EVALUATION_DIR = BASE_DIR / "evaluation_20261112_110609_637382"

comparison_data = json.loads(
    (EVALUATION_DIR / "model_comparison.json").read_text(
        encoding="utf-8"
    )
)

backtest_data = json.loads(
    (EVALUATION_DIR / "backtest_monthly.json").read_text(
        encoding="utf-8"
    )
)

# Load the saved model once when the service starts.
with MODEL_PATH.open("rb") as file:
    model_package = pickle.load(file)

if model_package.get("format") != "khangnghi_prophet_json_v1":
    raise RuntimeError("Unsupported model package format.")

model = model_from_json(model_package["model_json"])
model.uncertainty_samples = 0

training_start = model.history["ds"].min().date()
training_end = model.history["ds"].max().date()

FORECAST_START = date(2026, 1, 1)
FORECAST_END = date(2026, 6, 30)

if training_end != date(2025, 12, 31):
    raise RuntimeError("Expected a model trained through 2025-12-31.")

future = pd.DataFrame({
    "ds": pd.date_range(FORECAST_START, FORECAST_END, freq="D")
})

prediction = model.predict(future)

daily = pd.DataFrame({
    "ds": prediction["ds"],
    "revenue": prediction["yhat"].clip(lower=0)
})

app = FastAPI(title="Khang Nghi Forecast API")


def metadata():
    return {
        "model": "Prophet",
        "training_start": training_start.isoformat(),
        "training_end": training_end.isoformat(),
        "forecast_start": FORECAST_START.isoformat(),
        "forecast_end": FORECAST_END.isoformat(),
    }


@app.get("/")
def health():
    return {
        "status": "ready",
        **metadata(),
    }


@app.get("/forecast")
def forecast(months: int = 6):
    if months not in (3, 6):
        raise HTTPException(
            status_code=422,
            detail="months must be 3 or 6."
        )

    selected = daily[daily["ds"].dt.month <= months].copy()
    selected["month"] = selected["ds"].dt.strftime("%Y-%m")

    monthly = (
        selected.groupby("month", as_index=False)["revenue"]
        .sum()
    )

    return {
        **metadata(),
        "months": months,
        "backtest_monthly": backtest_data,
        "total_revenue_vnd": float(selected["revenue"].sum()),
        "monthly": [
            {
                "month": row.month,
                "predicted_revenue_vnd": float(row.revenue),
            }
            for row in monthly.itertuples(index=False)
        ],
    }


@app.get("/forecast/detail")
def forecast_detail(
    granularity: str = "day",
    start_date: date = FORECAST_START,
    end_date: date = FORECAST_END,
):
    if granularity not in ("day", "week", "month"):
        raise HTTPException(
            status_code=422,
            detail="granularity must be day, week or month."
        )

    if not (
        FORECAST_START <= start_date <= end_date <= FORECAST_END
    ):
        raise HTTPException(
            status_code=422,
            detail="Choose dates within 2026-01-01 to 2026-06-30."
        )

    selected = daily[
        (daily["ds"] >= pd.Timestamp(start_date))
        & (daily["ds"] <= pd.Timestamp(end_date))
    ].copy()

    if granularity == "day":
        selected["period"] = selected["ds"].dt.strftime("%Y-%m-%d")
    elif granularity == "month":
        selected["period"] = selected["ds"].dt.strftime("%Y-%m")
    else:
        # Calendar weeks from Monday through Sunday.
        selected["period"] = (
            selected["ds"].dt.to_period("W-SUN").astype(str)
        )

    points = []

    for period, group in selected.groupby("period", sort=True):
        period_start = group["ds"].min().date().isoformat()
        period_end = group["ds"].max().date().isoformat()

        points.append({
            "label": str(period),
            "period_start": period_start,
            "period_end": period_end,
            "day_count": int(len(group)),
            "predicted_revenue_vnd": float(group["revenue"].sum()),
        })

    return {
        **metadata(),
        "granularity": granularity,
        "start_date": start_date.isoformat(),
        "end_date": end_date.isoformat(),
        "total_revenue_vnd": float(selected["revenue"].sum()),
        "points": points,
    }


@app.get("/models/comparison")
def model_comparison():
    return comparison_data