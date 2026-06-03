@echo off
echo ========================================
echo WebView2 Runtime 安装脚本
echo ========================================
echo.

echo 正在下载 WebView2 Runtime (Fixed Version)...
echo 注意：此下载可能需要几分钟时间

powershell -Command "Invoke-WebRequest -Uri 'https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/84b7b160-83db-42e9-b6e8-e8c8ee09cf78/MicrosoftEdgeWebView2Setup.exe' -OutFile 'WebView2Setup.exe'"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo 下载完成！正在安装...
    echo.
    start /wait WebView2Setup.exe /silent /install
    del WebView2Setup.exe
    echo.
    echo ========================================
    echo WebView2 Runtime 安装完成！
    echo 请重新运行应用程序。
    echo ========================================
) else (
    echo.
    echo 下载失败！
    echo 请手动下载 WebView2 Runtime：
    echo 1. 访问 https://developer.microsoft.com/en-us/microsoft-edge/webview2/
    echo 2. 点击 "Download"
    echo 3. 选择 "Fixed Version Runtime" 并下载 x64 版本
    echo 4. 运行安装程序
    echo.
    pause
)
