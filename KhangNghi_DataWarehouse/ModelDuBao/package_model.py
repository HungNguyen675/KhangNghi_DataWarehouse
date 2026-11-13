from pathlib import Path
from importlib.metadata import version
import pickle

import numpy as np
import pandas as pd
from prophet.serialize import model_from_json


BASE_DIR = Path(__file__).resolve().parent
SOURCE = BASE_DIR / "saved_model" / "prophet_model.json"
TARGET = BASE_DIR / "saved_model" / "prophet_web_model.pkl"

if TARGET.exists():
    raise SystemExit("The PKL already exists. Stop to avoid overwriting.")

model_json = SOURCE.read_text(encoding="utf-8")
original = model_from_json(model_json)
original.uncertainty_samples = 0

package = {
    "format": "khangnghi_prophet_json_v1",
    "model": "Prophet",
    "model_json": model_json,
    "training_start": original.history["ds"].min().date().isoformat(),
    "training_end": original.history["ds"].max().date().isoformat(),
    "versions": {
        name: version(name)
        for name in ["prophet", "pandas", "numpy"]
    },
}

# Test serialization in memory before creating the output file.
serialized = pickle.dumps(package, protocol=pickle.HIGHEST_PROTOCOL)
restored_package = pickle.loads(serialized)

restored = model_from_json(restored_package["model_json"])
restored.uncertainty_samples = 0

future = pd.DataFrame({
    "ds": pd.date_range("2026-01-01", "2026-06-30", freq="D")
})

expected = original.predict(future)["yhat"].clip(lower=0).to_numpy()
actual = restored.predict(future)["yhat"].clip(lower=0).to_numpy()

if not np.isfinite(actual).all():
    raise RuntimeError("Restored forecast contains invalid values.")

difference = float(np.max(np.abs(expected - actual)))

if not np.allclose(expected, actual, rtol=0, atol=0.01):
    raise RuntimeError(
        f"Forecast changed after serialization: {difference}"
    )

# Create a new file without overwriting an existing one.
with TARGET.open("xb") as file:
    file.write(serialized)

# Verify the file actually saved on disk.
with TARGET.open("rb") as file:
    saved_package = pickle.load(file)

if saved_package["model_json"] != model_json:
    raise RuntimeError("Saved model content does not match the source.")

print("Packaging: OK")
print("Training:", package["training_start"],
      "to", package["training_end"])
print("Forecast days checked:", len(actual))
print("Maximum difference:", difference)
print("Total 3 months:", f"{actual[:90].sum():,.2f}")
print("Total 6 months:", f"{actual.sum():,.2f}")
print("Saved:", TARGET)