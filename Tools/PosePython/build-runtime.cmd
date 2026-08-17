@echo off
setlocal
cd /d "%~dp0"

set "PYTHON=.venv\Scripts\python.exe"
if not exist "%PYTHON%" (
    echo PosePython virtual environment was not found.
    echo Run setup-python.cmd first, then try again.
    exit /b 1
)

"%PYTHON%" --version >nul 2>&1
if errorlevel 1 (
    echo PosePython virtual environment is not usable.
    echo Delete Tools\PosePython\.venv and run setup-python.cmd again.
    exit /b 1
)

echo Installing build dependencies...
"%PYTHON%" -m pip install -r requirements-build.txt
if errorlevel 1 exit /b 1

echo Preparing the MediaPipe heavy pose model...
"%PYTHON%" -c "from mediapipe.python.solutions.pose import Pose; pose = Pose(model_complexity=2); pose.close()"
if errorlevel 1 exit /b 1

echo Building the self-contained Windows pose runtime...
"%PYTHON%" -m PyInstaller --clean --noconfirm LuminaPoseTracker.spec
if errorlevel 1 exit /b 1

if not exist "dist\LuminaPoseTracker\LuminaPoseTracker.exe" (
    echo Build finished without the expected executable.
    exit /b 1
)

echo.
echo Pose runtime is ready:
echo %CD%\dist\LuminaPoseTracker
exit /b 0
