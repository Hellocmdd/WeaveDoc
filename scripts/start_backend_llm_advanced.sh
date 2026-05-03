#!/bin/bash

# 后端LLM服务启动脚本 (高级版本 - 支持自定义参数)
# 用法: ./scripts/start_backend_llm_advanced.sh [选项]
# 或从项目根目录: bash scripts/start_backend_llm_advanced.sh [选项]
# 
# 选项:
#   --help              显示帮助信息
#   --chat-port PORT    指定Chat LLM端口 (默认: 8080)
#   --reranker-port PORT 指定Reranker LLM端口 (默认: 8081)
#   --gpu-layers N      指定GPU层数 (默认: 99, 全部使用GPU)
#   --context-size N    指定上下文大小 (默认: 32438)
#   --no-flash-attn     禁用Flash Attention优化
#   --cpu-only          仅使用CPU (相当于 --gpu-layers 0)
#   --verbose           显示详细日志

set -e

# 脚本所在目录，然后切换到项目根目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"

# 默认参数
CHAT_PORT=8080
RERANKER_PORT=8081
GPU_LAYERS=99
CONTEXT_SIZE=32438
FLASH_ATTN="on"
VERBOSE=false

# 帮助信息
show_help() {
    echo "后端LLM服务启动脚本 (高级版本)"
    echo ""
    echo "用法: $0 [选项]"
    echo ""
    echo "选项:"
    echo "  --help              显示此帮助信息"
    echo "  --chat-port PORT    指定Chat LLM端口 (默认: 8080)"
    echo "  --reranker-port PORT 指定Reranker LLM端口 (默认: 8081)"
    echo "  --gpu-layers N      指定GPU层数 (默认: 99, 0=仅CPU)"
    echo "  --context-size N    指定上下文大小 (默认: 32438)"
    echo "  --no-flash-attn     禁用Flash Attention优化"
    echo "  --cpu-only          仅使用CPU"
    echo "  --verbose           显示详细日志"
    echo ""
    echo "示例:"
    echo "  # 使用默认参数启动"
    echo "  bash scripts/start_backend_llm_advanced.sh"
    echo ""
    echo "  # 使用CPU模式启动"
    echo "  bash scripts/start_backend_llm_advanced.sh --cpu-only"
    echo ""
    echo "  # 自定义端口和上下文大小"
    echo "  bash scripts/start_backend_llm_advanced.sh --chat-port 9000 --context-size 16384"
}

# 解析命令行参数
while [[ $# -gt 0 ]]; do
    case $1 in
        --help)
            show_help
            exit 0
            ;;
        --chat-port)
            CHAT_PORT="$2"
            shift 2
            ;;
        --reranker-port)
            RERANKER_PORT="$2"
            shift 2
            ;;
        --gpu-layers)
            GPU_LAYERS="$2"
            shift 2
            ;;
        --context-size)
            CONTEXT_SIZE="$2"
            shift 2
            ;;
        --no-flash-attn)
            FLASH_ATTN="off"
            shift
            ;;
        --cpu-only)
            GPU_LAYERS=0
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        *)
            echo "未知选项: $1"
            show_help
            exit 1
            ;;
    esac
done

# llama-server可执行文件路径
LLAMA_SERVER="./llama.cpp/build/bin/llama-server"

# 检查llama-server是否存在
if [ ! -f "$LLAMA_SERVER" ]; then
    echo "❌ 错误: llama-server不存在，路径: $LLAMA_SERVER"
    echo "📝 请先编译llama.cpp:"
    echo "   cd llama.cpp && cmake -B build && cmake --build build"
    exit 1
fi

# 模型路径
CHAT_MODEL="./models/Qwen3.5-4B-Q4_K_M.gguf"
RERANKER_MODEL="./models/bge-reranker-v2-m3.gguf"

# 检查模型文件是否存在
if [ ! -f "$CHAT_MODEL" ]; then
    echo "❌ 错误: Chat模型不存在: $CHAT_MODEL"
    exit 1
fi

if [ ! -f "$RERANKER_MODEL" ]; then
    echo "❌ 错误: Reranker模型不存在: $RERANKER_MODEL"
    exit 1
fi

# 打印配置信息
echo "================================"
echo "🚀 启动后端LLM服务"
echo "================================"
echo ""
echo "📋 配置信息:"
echo "  Chat LLM 端口:      $CHAT_PORT"
echo "  Reranker LLM 端口:  $RERANKER_PORT"
echo "  GPU层数:            $GPU_LAYERS $([ "$GPU_LAYERS" -eq 0 ] && echo '(CPU模式)' || echo '')"
echo "  上下文大小:         $CONTEXT_SIZE tokens"
echo "  Flash Attention:    $FLASH_ATTN"
echo ""

# 启动Chat LLM服务
echo "[1/2] 启动Chat LLM服务..."
if [ "$VERBOSE" = true ]; then
    echo "    📌 命令: $LLAMA_SERVER -m $CHAT_MODEL -port $CHAT_PORT -ngl $GPU_LAYERS --flash-attn $FLASH_ATTN -c $CONTEXT_SIZE"
fi
"$LLAMA_SERVER" -m "$CHAT_MODEL" -port "$CHAT_PORT" -ngl "$GPU_LAYERS" --flash-attn "$FLASH_ATTN" -c "$CONTEXT_SIZE" &
CHAT_PID=$!
echo "    ✅ Chat LLM已启动 (PID: $CHAT_PID) - 监听 http://localhost:$CHAT_PORT"

# 等待Chat服务启动
sleep 3

# 启动Reranker LLM服务
echo "[2/2] 启动Reranker LLM服务..."
if [ "$VERBOSE" = true ]; then
    echo "    📌 命令: $LLAMA_SERVER -m $RERANKER_MODEL --embedding --pooling rank -port $RERANKER_PORT"
fi
"$LLAMA_SERVER" -m "$RERANKER_MODEL" --embedding --pooling rank -port "$RERANKER_PORT" &
RERANKER_PID=$!
echo "    ✅ Reranker LLM已启动 (PID: $RERANKER_PID) - 监听 http://localhost:$RERANKER_PORT"

echo ""
echo "================================"
echo "✅ 两个服务已启动"
echo "================================"
echo ""
echo "🌐 访问地址:"
echo "  Chat LLM:     http://localhost:$CHAT_PORT"
echo "  Reranker LLM: http://localhost:$RERANKER_PORT"
echo ""
echo "📝 健康检查:"
echo "  curl http://localhost:$CHAT_PORT/health"
echo "  curl http://localhost:$RERANKER_PORT/health"
echo ""
echo "❌ 停止服务: 按 Ctrl+C"
echo ""

# 清理函数
cleanup() {
    echo ""
    echo "⏹️  正在关闭服务..."
    kill $CHAT_PID 2>/dev/null || true
    kill $RERANKER_PID 2>/dev/null || true
    wait $CHAT_PID 2>/dev/null || true
    wait $RERANKER_PID 2>/dev/null || true
    echo "✅ 所有服务已关闭"
    exit 0
}

# 捕获信号
trap cleanup SIGINT SIGTERM

# 等待进程
wait
