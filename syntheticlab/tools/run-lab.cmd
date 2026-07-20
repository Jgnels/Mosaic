@echo off
setlocal
cd /d "%~dp0\.."
where py >nul 2>&1
if %errorlevel%==0 (
    py -3 run_lab.py --self-test --seeds 64 --output artifacts
    exit /b %errorlevel%
)
where python >nul 2>&1
if %errorlevel%==0 (
    python run_lab.py --self-test --seeds 64 --output artifacts
    exit /b %errorlevel%
)
echo Python 3 was not found.
exit /b 1
