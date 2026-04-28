#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BASELINE_PATH="${1:-$ROOT_DIR/docs/eval-baseline.json}"
REPORT_DIR="${2:-${RAG_EVAL_REPORT_DIR:-$ROOT_DIR/.eval}}"
LLAMA_SERVER_HOST="${LLAMA_SERVER_HOST:-127.0.0.1}"
LLAMA_SERVER_PORT="${LLAMA_SERVER_PORT:-8080}"
LLAMA_SERVER_BASE_URL="${LLAMA_SERVER_BASE_URL:-http://$LLAMA_SERVER_HOST:$LLAMA_SERVER_PORT}"
RAG_RERANKER_ENABLED="${RAG_RERANKER_ENABLED:-true}"
RAG_RERANKER_BASE_URL="${RAG_RERANKER_BASE_URL:-http://127.0.0.1:8081}"
export RAG_EVAL_REPORT_DIR="$REPORT_DIR"
export LLAMA_SERVER_HOST
export LLAMA_SERVER_PORT
export LLAMA_SERVER_BASE_URL
export RAG_RERANKER_ENABLED
export RAG_RERANKER_BASE_URL

cd "$ROOT_DIR"

if ! command -v curl >/dev/null 2>&1; then
    echo "[eval_rag] curl is required for llama-server health checks" >&2
    exit 1
fi

if ! curl -fsS "$LLAMA_SERVER_BASE_URL/health" >/dev/null 2>&1; then
    cat >&2 <<EOF
[eval_rag] llama-server is not reachable at $LLAMA_SERVER_BASE_URL
[eval_rag] start it first, for example:
  ./scripts/run_weavedoc.sh
or:
  ./llama.cpp/build/bin/llama-server -m ./models/Qwen3.5-4B-Q4_K_M.gguf --host $LLAMA_SERVER_HOST --port $LLAMA_SERVER_PORT --gpu-layers auto
EOF
    exit 1
fi

if [[ "${RAG_RERANKER_ENABLED,,}" != "false" ]] && ! curl -fsS "$RAG_RERANKER_BASE_URL/health" >/dev/null 2>&1; then
    cat >&2 <<EOF
[eval_rag] reranker is enabled but not reachable at $RAG_RERANKER_BASE_URL
[eval_rag] start the full stack with:
  ./scripts/run_weavedoc.sh
or disable learned reranking for this run:
  RAG_RERANKER_ENABLED=false ./scripts/eval_rag.sh
EOF
    exit 1
fi

mkdir -p "$REPORT_DIR"
echo "[eval_rag] baseline: $BASELINE_PATH"
echo "[eval_rag] report dir: $REPORT_DIR"

dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval "$BASELINE_PATH"
