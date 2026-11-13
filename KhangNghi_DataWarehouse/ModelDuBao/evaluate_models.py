from datetime import datetime
from pathlib import Path
import hashlib
import json

import numpy as np
import pandas as pd

from hybrid_model import KhangNghiHybridForecaster


BASE_DIR = Path(__file__).resolve().parent
DATA_FILE = BASE_DIR / "evaluation_data_2022_2025.csv"

CUTOFFS = [
    "2024-12-31",
    "2025-03-31",
    "2025-06-30",
]

MODEL_COLUMNS = {
    "Prophet": "Prophet_Pred",
    "Hybrid_Optimal": "Hybrid_Optimal_Pred",
}


def main():
    data = pd.read_csv(DATA_FILE, parse_dates=["ds"])
    data = data.sort_values("ds").reset_index(drop=True)
    data["y"] = pd.to_numeric(data["y"], errors="raise")

    expected_dates = pd.date_range(
        "2022-01-01", "2025-12-31", freq="D"
    )

    if not pd.DatetimeIndex(data["ds"]).equals(expected_dates):
        raise ValueError("The CSV must contain all 1461 dates in order.")

    if not np.isfinite(data["y"]).all():
        raise ValueError("Revenue contains missing or invalid values.")

    if (data["y"] < 0).any():
        raise ValueError("Negative daily revenue requires review.")

    output_dir = BASE_DIR / (
        "evaluation_" + datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    )
    output_dir.mkdir()

    metric_rows = []
    monthly_rows = []
    backtest_rows = []
    training_records = []

    for cutoff_text in CUTOFFS:
        cutoff = pd.Timestamp(cutoff_text)
        forecast_end = cutoff + pd.offsets.MonthEnd(6)
        horizon_days = (forecast_end - cutoff).days

        # Only dates up to the cutoff are used for training.
        train = data.loc[
            data["ds"] <= cutoff, ["ds", "y"]
        ].copy().reset_index(drop=True)

        actual = data.loc[
            (data["ds"] > cutoff)
            & (data["ds"] <= forecast_end),
            ["ds", "y"],
        ].copy()

        if len(actual) != horizon_days:
            raise ValueError("Incomplete actual data for " + cutoff_text)

        print(
            f"\nTRAINING: cutoff={cutoff_text}, "
            f"training_days={len(train)}, "
            f"forecast_days={horizon_days}",
            flush=True,
        )

        forecaster = KhangNghiHybridForecaster(
            capping_quantile=0.99,
            changepoint_prior_scale=0.15,
            seasonality_mode="multiplicative",
            random_state=42,
        )

        # The class calculates its capping threshold from this
        # training period only. No test dates are passed to fit.
        forecaster.fit(train, verbose=True)
        forecaster.prophet_model.uncertainty_samples = 0

        # Recursive forecast: future actual revenue is not supplied.
        predictions = forecaster.forecast_future(
            horizon_days=horizon_days,
            history_df=train,
        )

        predictions["Date"] = pd.to_datetime(predictions["Date"])

        combined = actual.merge(
            predictions[
                ["Date", *MODEL_COLUMNS.values()]
            ],
            left_on="ds",
            right_on="Date",
            how="left",
            validate="one_to_one",
        )

        if combined[list(MODEL_COLUMNS.values())].isna().any().any():
            raise ValueError("Forecast dates do not match actual dates.")

        if not np.isfinite(
            combined[list(MODEL_COLUMNS.values())].to_numpy()
        ).all():
            raise ValueError("Forecast contains invalid values.")

        combined["month"] = combined["ds"].dt.strftime("%Y-%m")

        monthly = combined.groupby("month", as_index=False)[
            ["y", *MODEL_COLUMNS.values()]
        ].sum()

        if len(monthly) != 6:
            raise ValueError("Each cutoff must have six full months.")

        combined.to_csv(
            output_dir / f"daily_{cutoff_text}.csv",
            index=False,
            encoding="utf-8-sig",
        )

        for row in monthly.to_dict("records"):
            monthly_rows.append({
                "cutoff": cutoff_text,
                "month": row["month"],
                "actual_revenue_vnd": float(row["y"]),
                "prophet_revenue_vnd": float(row["Prophet_Pred"]),
                "hybrid_revenue_vnd": float(
                    row["Hybrid_Optimal_Pred"]
                ),
            })

            # Use the December cutoff for Jan-Jun and the
            # June cutoff for Jul-Dec: each month appears once.
            if cutoff_text in ("2024-12-31", "2025-06-30"):
                backtest_rows.append({
                    "month": row["month"],
                    "cutoff": cutoff_text,
                    "predicted_revenue_vnd": float(
                        row["Prophet_Pred"]
                    ),
                    "actual_revenue_vnd": float(row["y"]),
                })

        for horizon in (3, 6):
            selected = monthly.iloc[:horizon]

            for model_name, column in MODEL_COLUMNS.items():
                errors = (
                    selected[column].to_numpy(dtype=float)
                    - selected["y"].to_numpy(dtype=float)
                )

                metric_rows.append({
                    "cutoff": cutoff_text,
                    "horizon_months": horizon,
                    "model": model_name,
                    "mae_vnd": float(np.mean(np.abs(errors))),
                    "rmse_vnd": float(
                        np.sqrt(np.mean(errors ** 2))
                    ),
                })

        training_records.append({
            "cutoff": cutoff_text,
            "training_days": len(train),
            "capping_value": forecaster.capping_value,
            "optimal_weights": forecaster.optimal_weights,
        })

        print("COMPLETED:", cutoff_text, flush=True)

    metrics = pd.DataFrame(metric_rows)

    # Summary = arithmetic mean of the three cutoff metrics.
    summary = (
        metrics.groupby(
            ["horizon_months", "model"], as_index=False
        )[["mae_vnd", "rmse_vnd"]]
        .mean()
    )

    comparison = {
        "evaluation_type": "recursive_multi_step",
        "metric_level": "monthly_revenue",
        "summary_method": "mean_of_three_cutoff_metrics",
        "cutoffs": CUTOFFS,
        "data_sha256": hashlib.sha256(
            DATA_FILE.read_bytes()
        ).hexdigest(),
        "summary": summary.to_dict("records"),
        "by_cutoff": metric_rows,
        "training_records": training_records,
    }

    with (output_dir / "model_comparison.json").open(
        "x", encoding="utf-8"
    ) as file:
        json.dump(
            comparison, file,
            ensure_ascii=False, indent=2, allow_nan=False,
        )

    with (output_dir / "backtest_monthly.json").open(
        "x", encoding="utf-8"
    ) as file:
        json.dump(
            sorted(backtest_rows, key=lambda row: row["month"]),
            file,
            ensure_ascii=False, indent=2, allow_nan=False,
        )

    pd.DataFrame(monthly_rows).to_csv(
        output_dir / "monthly_predictions.csv",
        index=False,
        encoding="utf-8-sig",
    )

    metrics.to_csv(
        output_dir / "metrics_by_cutoff.csv",
        index=False,
        encoding="utf-8-sig",
    )

    print("\nSUMMARY - MONTHLY MAE / RMSE")
    print(summary.to_string(
        index=False,
        float_format=lambda value: f"{value:,.2f}",
    ))

    print("\nDETAIL BY CUTOFF")
    print(metrics.to_string(
        index=False,
        float_format=lambda value: f"{value:,.2f}",
    ))

    print("\nOUTPUT DIRECTORY:", output_dir)
    print("Evaluation completed. Serving model was not overwritten.")


if __name__ == "__main__":
    main()