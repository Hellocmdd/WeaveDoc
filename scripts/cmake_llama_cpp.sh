#!/usr/bin/env bash
set -euo pipefail

ORIG_PWD="$(pwd)"

# 无论脚本成功或失败，都回到脚本开始执行时的目录
cleanup() {
	cd "$ORIG_PWD"
}
trap cleanup EXIT

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LLAMA_DIR="$(cd "$SCRIPT_DIR/../llama.cpp" && pwd)"

cd "$LLAMA_DIR"

CUDA_FLAG="OFF"
if command -v nvidia-smi >/dev/null 2>&1 && nvidia-smi -L >/dev/null 2>&1; then
	CUDA_FLAG="ON"
fi

echo "[cmake_llama_cpp] GGML_CUDA=$CUDA_FLAG"
cmake -B build -DGGML_CUDA="$CUDA_FLAG"
cmake --build build --config Release --parallel 13