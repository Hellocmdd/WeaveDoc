#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

MODE="remote"
if [[ "${1:-}" == "--pinned" ]]; then
    MODE="pinned"
fi

cd "$ROOT_DIR"

echo "[update_all] pulling main repository"
git pull --recurse-submodules

echo "[update_all] syncing submodule config"
git submodule sync --recursive

echo "[update_all] initializing submodules"
git submodule update --init --recursive

if [[ "$MODE" == "remote" ]]; then
    echo "[update_all] updating submodules to latest tracked branch"
    git submodule update --init --recursive --remote
else
    echo "[update_all] keeping submodules at versions pinned by the current main repo commit"
fi

echo "[update_all] rebuilding llama.cpp"
bash "$SCRIPT_DIR/cmake_llama_cpp.sh"

echo "[update_all] rebuilding Avalonia app"
dotnet build "$ROOT_DIR/csharp-rag-avalonia/RagAvalonia.csproj"
