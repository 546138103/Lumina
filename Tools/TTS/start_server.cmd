
@echo off
setlocal
cd /d "%~dp0"
chcp 65001 >nul
set PYTHONIOENCODING=utf-8

if not exist ".venv\Scripts\python.exe" (
    echo [ERROR] Cannot find Python virtual environment.
    echo Please configure Tools\TTS\.venv first.
    pause
    exit /b 1
)

echo Starting Lumina TTS Server...

".venv\Scripts\python.exe" -B -u server_CustomVoice.py

echo.
echo TTS Server stopped.
pause
