# 后端LLM服务启动脚本 (Windows PowerShell版本)
# 启动Chat LLM和Reranker LLM两个llama-server实例
# 使用方法: .\scripts\start_backend_llm.ps1

$ErrorActionPreference = "Stop"

# 获取脚本所在目录，然后切换到项目根目录
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
Set-Location $projectRoot

# llama-server可执行文件路径
$llamaServer = ".\llama.cpp\build\bin\llama-server.exe"

# 检查llama-server是否存在
if (-not (Test-Path $llamaServer)) {
    Write-Host "错误: llama-server不存在，路径: $llamaServer" -ForegroundColor Red
    Write-Host "请先编译llama.cpp: cd llama.cpp && cmake -B build && cmake --build build" -ForegroundColor Yellow
    exit 1
}

# 模型路径
$chatModel = ".\models\Qwen3.5-4B-Q4_K_M.gguf"
$rerankerModel = ".\models\bge-reranker-v2-m3.gguf"

# 检查模型文件是否存在
if (-not (Test-Path $chatModel)) {
    Write-Host "错误: Chat模型不存在: $chatModel" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $rerankerModel)) {
    Write-Host "错误: Reranker模型不存在: $rerankerModel" -ForegroundColor Red
    exit 1
}

Write-Host "================================" -ForegroundColor Green
Write-Host "启动后端LLM服务" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

# 启动Chat LLM服务 (端口8080)
Write-Host "[1/2] 启动Chat LLM服务..." -ForegroundColor Cyan
Write-Host "命令: $llamaServer -m $chatModel -port 8080 -ngl 99 --flash-attn on -c 32438" -ForegroundColor Gray

$chatProcess = Start-Process -FilePath $llamaServer `
    -ArgumentList @("-m", $chatModel, "-port", "8080", "-ngl", "99", "--flash-attn", "on", "-c", "32438") `
    -PassThru

Write-Host "Chat LLM已启动 (PID: $($chatProcess.Id)) - 监听端口 8080" -ForegroundColor Green

# 等待Chat服务启动
Start-Sleep -Seconds 3

# 启动Reranker LLM服务 (端口8081)
Write-Host "[2/2] 启动Reranker LLM服务..." -ForegroundColor Cyan
Write-Host "命令: $llamaServer -m $rerankerModel --embedding --pooling rank -port 8081" -ForegroundColor Gray

$rerankerProcess = Start-Process -FilePath $llamaServer `
    -ArgumentList @("-m", $rerankerModel, "--embedding", "--pooling", "rank", "-port", "8081") `
    -PassThru

Write-Host "Reranker LLM已启动 (PID: $($rerankerProcess.Id)) - 监听端口 8081" -ForegroundColor Green

Write-Host "================================" -ForegroundColor Green
Write-Host "两个服务已启动:" -ForegroundColor Green
Write-Host "  - Chat LLM:     http://localhost:8080" -ForegroundColor Cyan
Write-Host "  - Reranker LLM: http://localhost:8081" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Green
Write-Host "按 Ctrl+C 停止两个服务" -ForegroundColor Yellow

# 定义清理函数
$null = Register-EngineEvent -SourceIdentifier "PowerShell.Exiting" -Action {
    Write-Host ""
    Write-Host "正在关闭服务..." -ForegroundColor Yellow
    $chatProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    $rerankerProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "所有服务已关闭" -ForegroundColor Green
}

# 等待进程退出
$chatProcess.WaitForExit()
$rerankerProcess.WaitForExit()
