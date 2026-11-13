@echo off
setlocal
cd /d "%~dp0"

echo Starting Khang Nghi Forecast API...
echo Address: http://127.0.0.1:8001/
echo API documentation: http://127.0.0.1:8001/docs
echo Keep this window open. Press Ctrl+C to stop.
echo.

if exist ".venv\Scripts\python.exe" (
    ".venv\Scripts\python.exe" -m uvicorn forecast_api:app --host 127.0.0.1 --port 8001
) else (
    python -m uvicorn forecast_api:app --host 127.0.0.1 --port 8001
)

echo.
echo Forecast API has stopped.
pause
endlocal