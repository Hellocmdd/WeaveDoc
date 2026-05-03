#!/bin/bash

# 后端LLM服务启动脚本
# 启动Chat LLM和Reranker LLM两个llama-server实例

set -e

# 获取脚本所在目录，然后切换到项目根目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"

# llama-server可执行文件路径
LLAMA_SERVER="./llama.cpp/build/bin/llama-server"

# 检查llama-server是否存在
if [ ! -f "$LLAMA_SERVER" ]; then
    echo "错误: llama-server不存在，路径: $LLAMA_SERVER"
    echo "请先编译llama.cpp: cd llama.cpp && cmake -B build && cmake --build build"
    exit 1
fi

# 模型路径
CHAT_MODEL="./models/Qwen3.5-4B-Q4_K_M.gguf"
RERANKER_MODEL="./models/bge-reranker-v2-m3.gguf"

# 检查模型文件是否存在
if [ ! -f "$CHAT_MODEL" ]; then
    echo "错误: Chat模型不存在: $CHAT_MODEL"
    exit 1
fi

if [ ! -f "$RERANKER_MODEL" ]; then
    echo "错误: Reranker模型不存在: $RERANKER_MODEL"
    exit 1
fi

echo "================================"
echo "启动后端LLM服务"
echo "================================"

# 启动Chat LLM服务 (端口8080)
echo "[1/2] 启动Chat LLM服务..."
echo "命令: $LLAMA_SERVER -m $CHAT_MODEL --port 8080 -ngl 99 --flash-attn on -c 32438"
"$LLAMA_SERVER" -m "$CHAT_MODEL" --port 8080 -ngl 99 --flash-attn on -c 32438 &
CHAT_PID=$!
echo "Chat LLM已启动 (PID: $CHAT_PID) - 监听端口 8080"

# 等待Chat服务启动
sleep 3

# 启动Reranker LLM服务 (端口8081)
echo "[2/2] 启动Reranker LLM服务..."
echo "命令: $LLAMA_SERVER -m $RERANKER_MODEL --embedding --pooling rank --port 8081"
"$LLAMA_SERVER" -m "$RERANKER_MODEL" --embedding --pooling rank --port 8081 &
RERANKER_PID=$!
echo "Reranker LLM已启动 (PID: $RERANKER_PID) - 监听端口 8081"

echo "================================"
echo "两个服务已启动:"
echo "  - Chat LLM:     http://localhost:8080"
echo "  - Reranker LLM: http://localhost:8081"
echo "================================"
echo "按 Ctrl+C 停止两个服务"
echo ""

# 清理函数：关闭两个进程
cleanup() {
    echo ""
    echo "正在关闭服务..."
    kill $CHAT_PID 2>/dev/null || true
    kill $RERANKER_PID 2>/dev/null || true
    wait $CHAT_PID 2>/dev/null || true
    wait $RERANKER_PID 2>/dev/null || true
    echo "所有服务已关闭"
    exit 0
}

# 捕获Ctrl+C和退出信号
trap cleanup SIGINT SIGTERM

# 等待进程
wait
