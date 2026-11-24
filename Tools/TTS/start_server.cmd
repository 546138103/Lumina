
@echo off
cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
    echo [ERROR] Cannot find Python virtual environment.
    echo Please configure Tools\TTS\.venv first.
    pause
    exit /b 1
)

echo Starting Lumina TTS Server...

".venv\Scripts\python.exe" -m uvicorn server:app --host 127.0.0.1 --port 8001

echo.
echo TTS Server stopped.
pause
