#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BASELINE_PATH="${1:-$ROOT_DIR/docs/eval-baseline.json}"
REPORT_DIR="${2:-${RAG_EVAL_REPORT_DIR:-$ROOT_DIR/.eval}}"
LLAMA_SERVER_HOST="${LLAMA_SERVER_HOST:-127.0.0.1}"
LLAMA_SERVER_PORT="${LLAMA_SERVER_PORT:-8080}"
LLAMA_SERVER_BASE_URL="${LLAMA_SERVER_BASE_URL:-http://$LLAMA_SERVER_HOST:$LLAMA_SERVER_PORT}"
export RAG_EVAL_REPORT_DIR="$REPORT_DIR"

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
  ./llama.cpp/build/bin/llama-server -m ./models/gemma-4-E2B-it.gguf --host $LLAMA_SERVER_HOST --port $LLAMA_SERVER_PORT
EOF
    exit 1
fi

mkdir -p "$REPORT_DIR"
echo "[eval_rag] baseline: $BASELINE_PATH"
echo "[eval_rag] report dir: $REPORT_DIR"

dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval "$BASELINE_PATH"
