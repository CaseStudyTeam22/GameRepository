@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0\Assets\StreamingAssets\Server"

REM 通常は Unity 起動時に NetworkBootstrap が自動でサーバを起動するため、
REM このスクリプトを手動実行する必要はない。
REM サーバを単体で動かして外部から確認したい場合のみ使用する。

if not exist "node_modules\" (
    echo [Setup] node_modules が存在しないため npm install を実行します...
    call npm install
    if errorlevel 1 (
        echo [Error] npm install に失敗しました。Node.js のインストールを確認してください。
        pause
        exit /b 1
    )
)

echo [Check] ポート 3000 の使用状況を確認します...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :3000 ^| findstr LISTENING') do (
    echo [Clean] 旧サーバ PID %%a を停止します ...
    taskkill /f /pid %%a >nul 2>&1
)

REM 同梱の portable node.exe があればそれを使う。無ければシステム PATH の node を使う。
set "NODE_BIN=node.exe"
if not exist "%NODE_BIN%" (
    echo [Info] 同梱の node.exe が見つかりません。Tools\download-node.ps1 を一度実行することを推奨します。
    echo [Info] とりあえずシステムにインストール済みの node で起動を試みます。
    set "NODE_BIN=node"
)

echo [Start] node server.js を起動します（このウィンドウを閉じると停止します）
echo ----------------------------------------------------
"%NODE_BIN%" server.js
