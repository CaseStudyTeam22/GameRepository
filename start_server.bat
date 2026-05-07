@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0\Assets\StreamingAssets\Server"

if not exist "node_modules\" (
    echo [Setup] node_modules missing, running npm install...
    call npm install
    if errorlevel 1 (
        echo [Error] npm install failed. Make sure Node.js is installed.
        pause
        exit /b 1
    )
)

echo [Check] checking port 3000...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :3000 ^| findstr LISTENING') do (
    echo [Clean] killing old server PID %%a ...
    taskkill /f /pid %%a >nul 2>&1
)

echo [Start] running node server.js (close window to stop)
echo ----------------------------------------------------
node server.js
