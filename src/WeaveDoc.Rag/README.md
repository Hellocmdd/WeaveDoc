# WeaveDoc.Rag

English | [简体中文](README.zh-CN.md)

`WeaveDoc.Rag` is the RAG service module behind the `RAG 问答` tab in `WeaveDoc.App`. The root README now stays repository-focused; the local AI setup, model requirements, and evaluation workflow live here.

If you only use document conversion and template management, you can ignore this module and its model setup.

## Responsibilities

- workspace path discovery rooted at `models/`
- local embedding, retrieval, reranking, and answer composition
- local `llama-server` chat integration
- OpenAI-compatible cloud API settings and chat integration
- corpus refresh, document import/delete support, and evaluation helpers
- offline baseline execution through `EvalRunner`

## Workspace Requirements

Workspace data stays at the repository root:

- `doc/`: source documents for indexing
- `models/`: GGUF models used by embeddings, reranking, and chat
- `.rag/`: local indexes, logs, and runtime cache
- `.eval/`: evaluation outputs

## Required Models

- `models/bge-m3.gguf`: embedding model
- `models/bge-reranker-v2-m3.gguf`: reranker model when `RAG_RERANKER_ENABLED=true`
- one chat GGUF model; `scripts/run_weavedoc.sh` looks for `models/Qwen3.5-4B-Q4_K_M.gguf` first, then falls back to the first non-embedding `.gguf` file it finds

## Run The Local Stack

```bash
./scripts/run_weavedoc.sh
```

The helper script:

- verifies `models/` and the `llama.cpp` submodule
- builds `llama-server` when the binary is missing
- starts `llama-server` if no healthy endpoint is already running
- exports the RAG-related environment variables used by the desktop app
- launches `WeaveDoc.App`

Useful overrides:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
RAG_RERANKER_ENABLED=false ./scripts/run_weavedoc.sh
RAG_RERANKER_BASE_URL=http://127.0.0.1:8083 ./scripts/run_weavedoc.sh
```

## Launch The App Directly

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Use the direct app entry when your chat endpoint is already running or when you want to provide settings yourself. Common environment variables include:

- `LLAMA_SERVER_BASE_URL`
- `RAG_EMBEDDING_MODEL_FILE`
- `RAG_RERANKER_ENABLED`
- `RAG_RERANKER_BASE_URL`
- `RAG_RERANKER_MODEL`
- `RAG_CHAT_PROVIDER` with `llama_server`, `cloud`, or `deepseek`
- `CLOUD_API_KEY`, `CLOUD_MODEL`, `CLOUD_BASE_URL`
- `CLOUD_ENABLE_THINKING`, `CLOUD_REASONING_EFFORT`

## Offline Evaluation

Run the helper script after a chat `llama-server` is reachable:

```bash
./scripts/eval_rag.sh
```

Or reuse the unified app entry directly:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

`eval_rag.sh` expects a healthy chat endpoint. The `--eval` mode routes through `EvalRunner` and skips Avalonia startup.

## Main Surface Area

Public types intended for app or test usage include:

- `LocalAiService`
- `CloudApiSettings`
- `EvalRunner`
- `ChatTurn`
- `RagOptions`

Most retrieval internals remain implementation details under `Services/Rag/`.

## Notes

- this module no longer ships as its own desktop application
- chat `llama-server` can be managed by `scripts/run_weavedoc.sh`
- reranker lifecycle is managed inside `WeaveDoc.Rag`, not by a separate launcher script
