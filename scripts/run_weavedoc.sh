#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
LLAMA_DIR="$ROOT_DIR/llama.cpp"
LLAMA_SERVER_BIN="$LLAMA_DIR/build/bin/llama-server"
APP_PROJECT="$ROOT_DIR/csharp-rag-avalonia/RagAvalonia.csproj"
MODELS_DIR="$ROOT_DIR/models"
DEFAULT_EMBED_MODEL="$MODELS_DIR/sentence-transformers--all-MiniLM-L6-v2.gguf"

LLAMA_SERVER_HOST="${LLAMA_SERVER_HOST:-127.0.0.1}"
LLAMA_SERVER_PORT="${LLAMA_SERVER_PORT:-8080}"
LLAMA_SERVER_BASE_URL="${LLAMA_SERVER_BASE_URL:-http://$LLAMA_SERVER_HOST:$LLAMA_SERVER_PORT}"
LLAMA_SERVER_MODEL="${LLAMA_SERVER_MODEL:-}"
LLAMA_SERVER_LOG="${LLAMA_SERVER_LOG:-$ROOT_DIR/.rag/llama-server.log}"

LLAMA_SERVER_PID=""
SERVER_STARTED_BY_SCRIPT=0

cleanup() {
    if [[ "$SERVER_STARTED_BY_SCRIPT" == "1" && -n "$LLAMA_SERVER_PID" ]] && kill -0 "$LLAMA_SERVER_PID" 2>/dev/null; then
        echo "[run_weavedoc] stopping llama-server (pid=$LLAMA_SERVER_PID)"
        kill "$LLAMA_SERVER_PID" 2>/dev/null || true
        wait "$LLAMA_SERVER_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

find_chat_model() {
    if [[ -n "$LLAMA_SERVER_MODEL" ]]; then
        echo "$LLAMA_SERVER_MODEL"
        return 0
    fi

    if [[ -f "$MODELS_DIR/gemma-4-E2B-it.gguf" ]]; then
        echo "$MODELS_DIR/gemma-4-E2B-it.gguf"
        return 0
    fi

    local first_model=""
    while IFS= read -r candidate; do
        if [[ "$candidate" != "$DEFAULT_EMBED_MODEL" ]]; then
            first_model="$candidate"
            break
        fi
    done < <(find "$MODELS_DIR" -maxdepth 1 -type f -name '*.gguf' | sort)

    if [[ -n "$first_model" ]]; then
        echo "$first_model"
        return 0
    fi

    return 1
}

ensure_prereqs() {
    if [[ ! -d "$LLAMA_DIR" ]]; then
        echo "[run_weavedoc] missing llama.cpp directory: $LLAMA_DIR" >&2
        echo "[run_weavedoc] initialize the submodule first: git submodule update --init --recursive" >&2
        exit 1
    fi

    if [[ ! -d "$MODELS_DIR" ]]; then
        echo "[run_weavedoc] missing models directory: $MODELS_DIR" >&2
        exit 1
    fi

    if [[ ! -f "$DEFAULT_EMBED_MODEL" ]]; then
        echo "[run_weavedoc] missing embedding model: $DEFAULT_EMBED_MODEL" >&2
        exit 1
    fi

    if ! command -v dotnet >/dev/null 2>&1; then
        echo "[run_weavedoc] dotnet is not installed or not on PATH" >&2
        exit 1
    fi

    if ! command -v curl >/dev/null 2>&1; then
        echo "[run_weavedoc] curl is required for llama-server health checks" >&2
        exit 1
    fi
}

ensure_llama_server_binary() {
    if [[ -x "$LLAMA_SERVER_BIN" ]]; then
        return 0
    fi

    echo "[run_weavedoc] llama-server not found, building llama.cpp first"
    bash "$SCRIPT_DIR/cmake_llama_cpp.sh"

    if [[ ! -x "$LLAMA_SERVER_BIN" ]]; then
        echo "[run_weavedoc] build finished but llama-server is still missing: $LLAMA_SERVER_BIN" >&2
        exit 1
    fi
}

wait_for_server() {
    local retries=30
    for ((i = 1; i <= retries; i++)); do
        if curl -fsS "$LLAMA_SERVER_BASE_URL/health" >/dev/null 2>&1; then
            return 0
        fi

        sleep 1
    done

    echo "[run_weavedoc] llama-server did not become healthy in time" >&2
    echo "[run_weavedoc] check log: $LLAMA_SERVER_LOG" >&2
    return 1
}

start_server_if_needed() {
    if curl -fsS "$LLAMA_SERVER_BASE_URL/health" >/dev/null 2>&1; then
        echo "[run_weavedoc] using existing llama-server at $LLAMA_SERVER_BASE_URL"
        return 0
    fi

    ensure_llama_server_binary

    local chat_model
    if ! chat_model="$(find_chat_model)"; then
        echo "[run_weavedoc] no chat GGUF model found in $MODELS_DIR" >&2
        echo "[run_weavedoc] set LLAMA_SERVER_MODEL=/abs/path/to/your-chat-model.gguf if needed" >&2
        exit 1
    fi

    mkdir -p "$(dirname "$LLAMA_SERVER_LOG")"

    echo "[run_weavedoc] starting llama-server"
    echo "[run_weavedoc] model: $chat_model"
    echo "[run_weavedoc] endpoint: $LLAMA_SERVER_BASE_URL"

    "$LLAMA_SERVER_BIN" \
        -m "$chat_model" \
        --host "$LLAMA_SERVER_HOST" \
        --port "$LLAMA_SERVER_PORT" \
        >"$LLAMA_SERVER_LOG" 2>&1 &

    LLAMA_SERVER_PID="$!"
    SERVER_STARTED_BY_SCRIPT=1

    wait_for_server
}

run_app() {
    echo "[run_weavedoc] launching Avalonia app"
    (
        cd "$ROOT_DIR"
        export LLAMA_SERVER_BASE_URL
        dotnet run --project "$APP_PROJECT"
    )
}

ensure_prereqs
start_server_if_needed
run_app
