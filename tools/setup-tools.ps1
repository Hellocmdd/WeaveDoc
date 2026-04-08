# WeaveDoc 外部工具安装脚本
# 自动下载 Pandoc 和 Tectonic 到 tools/ 目录
# 用法: powershell -ExecutionPolicy Bypass -File tools/setup-tools.ps1

$ErrorActionPreference = "Stop"

# 版本配置
$PandocVersion = "3.9.0.2"
$TectonicVersion = "0.15.0"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ToolsDir = $ScriptDir  # tools/ 目录就是脚本所在目录

# ============================================================
# 下载 Pandoc
# ============================================================
$PandocDir = Join-Path $ToolsDir "pandoc"
$PandocExe = Join-Path $PandocDir "pandoc.exe"

if (Test-Path $PandocExe) {
    $ver = & $PandocExe --version 2>&1 | Select-Object -First 1
    Write-Host "[Pandoc] Already installed: $ver" -ForegroundColor Green
} else {
    Write-Host "[Pandoc] Downloading v$PandocVersion ..." -ForegroundColor Cyan
    $PandocUrl = "https://github.com/jgm/pandoc/releases/download/$PandocVersion/pandoc-$PandocVersion-windows-x86_64.zip"
    $PandocZip = Join-Path $ToolsDir "pandoc.zip"

    Invoke-WebRequest -Uri $PandocUrl -OutFile $PandocZip -UseBasicParsing

    Write-Host "[Pandoc] Extracting ..." -ForegroundColor Cyan
    $TempExtract = Join-Path $ToolsDir "pandoc-temp"
    Expand-Archive -Path $PandocZip -DestinationPath $TempExtract -Force
    Remove-Item $PandocZip

    # ZIP 内目录结构为 pandoc-3.9.0.2-windows-x86_64/pandoc.exe，取出放到 tools/pandoc/
    $InnerDir = Get-ChildItem -Path $TempExtract -Directory | Select-Object -First 1
    if (-not (Test-Path $PandocDir)) { New-Item -ItemType Directory -Path $PandocDir | Out-Null }
    Move-Item -Path "$($InnerDir.FullName)\pandoc.exe" -Destination $PandocExe -Force
    # 复制其他必要文件（如 Lua 相关）
    Get-ChildItem -Path $InnerDir.FullName -Exclude "pandoc.exe" | ForEach-Object {
        Move-Item -Path $_.FullName -Destination $PandocDir -Force
    }
    Remove-Item $TempExtract -Recurse -Force

    Write-Host "[Pandoc] Installed to $PandocDir" -ForegroundColor Green
}

# ============================================================
# 下载 Tectonic
# ============================================================
$TectonicDir = Join-Path $ToolsDir "tectonic"
$TectonicExe = Join-Path $TectonicDir "tectonic.exe"

if (Test-Path $TectonicExe) {
    Write-Host "[Tectonic] Already installed: $(& $TectonicExe --version 2>&1 | Select-Object -First 1)" -ForegroundColor Green
} else {
    Write-Host "[Tectonic] Downloading v$TectonicVersion ..." -ForegroundColor Cyan
    $TectonicUrl = "https://github.com/tectonic-typesetting/tectonic/releases/download/tectonic%40$TectonicVersion/tectonic-$TectonicVersion-x86_64-pc-windows-msvc.zip"
    $TectonicZip = Join-Path $TectonicDir "tectonic.zip"

    if (-not (Test-Path $TectonicDir)) { New-Item -ItemType Directory -Path $TectonicDir | Out-Null }

    Invoke-WebRequest -Uri $TectonicUrl -OutFile $TectonicZip -UseBasicParsing

    Write-Host "[Tectonic] Extracting ..." -ForegroundColor Cyan
    Expand-Archive -Path $TectonicZip -DestinationPath $TectonicDir -Force
    Remove-Item $TectonicZip

    Write-Host "[Tectonic] Installed to $TectonicDir" -ForegroundColor Green
}

# ============================================================
# 验证
# ============================================================
Write-Host ""
Write-Host "=== Installation Summary ===" -ForegroundColor Yellow

$allOk = $true
if (Test-Path $PandocExe) {
    Write-Host "  pandoc : $(& $PandocExe --version 2>&1 | Select-Object -First 1)" -ForegroundColor Green
} else {
    Write-Host "  pandoc : MISSING" -ForegroundColor Red
    $allOk = $false
}
if (Test-Path $TectonicExe) {
    Write-Host "  tectonic: $(& $TectonicExe --version 2>&1 | Select-Object -First 1)" -ForegroundColor Green
} else {
    Write-Host "  tectonic: MISSING" -ForegroundColor Red
    $allOk = $false
}

if ($allOk) {
    Write-Host ""
    Write-Host "All tools ready." -ForegroundColor Green
} else {
    Write-Error "Some tools failed to install."
}
