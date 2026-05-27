@echo off
chcp 65001 > nul
setlocal

rem このバッチは「管理者として実行」してください。
rem ゲーム同梱の node.exe に Windows ファイアウォールの受信許可を与えます。
rem 初回起動時に表示されるダイアログを取りこぼした場合の手動セットアップ用です。

set "NODE_PATH=%~dp0node.exe"

if not exist "%NODE_PATH%" (
    echo [エラー] node.exe が見つかりません:
    echo   %NODE_PATH%
    echo このバッチは node.exe と同じフォルダに置いてください。
    pause
    exit /b 1
)

rem 管理者権限の確認。net session は管理者でないと失敗する。
net session >nul 2>&1
if errorlevel 1 (
    echo [エラー] 管理者権限が必要です。
    echo このバッチを右クリックして「管理者として実行」してください。
    pause
    exit /b 1
)

echo Windows ファイアウォールに受信許可を追加します:
echo   %NODE_PATH%
echo.

rem 既に同じ名前のルールがあれば一度削除して登録し直す。
netsh advfirewall firewall delete rule name="GamblingAction Server" > nul 2>&1

netsh advfirewall firewall add rule ^
    name="GamblingAction Server" ^
    dir=in ^
    action=allow ^
    program="%NODE_PATH%" ^
    enable=yes ^
    profile=any

if errorlevel 1 (
    echo.
    echo [エラー] 受信許可の追加に失敗しました。
    pause
    exit /b 1
)

echo.
echo 設定が完了しました。ゲームを起動してください。
pause
endlocal
