# WeaveDoc.Rag

English | [简体中文](README.zh-CN.md)

`WeaveDoc.Rag` is the retrieval and chat service library behind the AI assistant panel in `WeaveDoc.App`. It owns corpus indexing, retrieval, reranking, local/cloud chat calls, fallback answer composition, and offline evaluation. The desktop UI lives in `WeaveDoc.App`.

If you only need Markdown editing or document conversion, you can skip this module and its model setup.

## Responsibilities

- Locate the workspace root and standard RAG directories.
- Import or delete corpus files under `doc/`.
- Chunk Markdown/text/JSON documents and maintain an embedding cache.
- Retrieve candidates with vector, BM25, keyword, title, JSON-structure, coverage, neighbor, and branch signals.
- Optionally rerank candidates through an OpenAI-compatible reranker endpoint.
- Call a local OpenAI-compatible `llama-server` endpoint.
- Call a cloud OpenAI-compatible endpoint when selected in settings.
- Stream answers back to the app.
- Build fallback answers and normalize citations.
- Run offline evaluation through `EvalRunner`.

## Workspace Directories

All paths are resolved from the repository workspace:

| Path | Purpose |
| --- | --- |
| `doc/` | Source/corpus documents |
| `models/` | Local GGUF embedding, reranker, and chat models |
| `.rag/` | Embedding cache, copied corpus files, logs, and `cloud-settings.json` |
| `.eval/` | Evaluation reports |

`WorkspacePaths.FindWorkspaceRoot()` walks upward from `AppContext.BaseDirectory` and accepts a directory that contains `WeaveDoc.slnx`, `src/` + `tests/`, or `models/`.

## Model Files

The helper script expects these local files by default:

| File | Used for |
| --- | --- |
| `models/bge-m3.gguf` | Embedding |
| `models/bge-reranker-v2-m3.gguf` | Reranker when `RAG_RERANKER_ENABLED=true` |
| `models/Qwen3.5-4B-Q4_K_M.gguf` or another non-embedding `.gguf` | Chat model for `llama-server` |

Override the chat model:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
```

## Run The Local Stack

```bash
./scripts/run_weavedoc.sh
```

The script:

1. checks `llama.cpp/`, `models/`, `dotnet`, and `curl`;
2. builds `llama.cpp/build/bin/llama-server` if it is missing;
3. reuses a healthy `LLAMA_SERVER_BASE_URL` when one is already running;
4. starts `llama-server` otherwise;
5. exports RAG environment variables;
6. launches `src/WeaveDoc.App/WeaveDoc.App.csproj`.

Useful overrides:

```bash
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_SERVER_GPU_LAYERS=0 ./scripts/run_weavedoc.sh
RAG_RERANKER_ENABLED=false ./scripts/run_weavedoc.sh
RAG_RERANKER_BASE_URL=http://127.0.0.1:8083 ./scripts/run_weavedoc.sh
```

## Run The App Directly

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Use direct app startup when a chat endpoint is already running, when you are using cloud settings, or when you want to supply settings from the UI.

## Environment Variables

Core retrieval options:

| Variable | Default | Meaning |
| --- | --- | --- |
| `RAG_CHUNK_SIZE` | `520` | Chunk target size |
| `RAG_CHUNK_OVERLAP` | `96` | Overlap between chunks |
| `RAG_TOP_K` | `8` | Final context size target |
| `RAG_CANDIDATE_POOL_SIZE` | `12` | Candidate pool after vector retrieval |
| `RAG_SPARSE_CANDIDATE_POOL_SIZE` | `48` | Sparse prefilter pool |
| `RAG_CONTEXT_WINDOW_RADIUS` | `1` | Neighbor chunk window |
| `RAG_EMBEDDING_MODEL_FILE` | `bge-m3.gguf` | Embedding model filename under `models/` |
| `RAG_EMBEDDING_GPU_LAYERS` | `999` | Embedding GPU layer count |

Reranker options:

| Variable | Default | Meaning |
| --- | --- | --- |
| `RAG_RERANKER_ENABLED` | `true` | Enable reranking |
| `RAG_RERANKER_BASE_URL` | `http://127.0.0.1:8081` | Reranker endpoint |
| `RAG_RERANKER_MODEL` | `bge-reranker-v2-m3` | Reranker model name |
| `RAG_RERANKER_TOP_N` | `12` | Number of items reranked |
| `RAG_RERANKER_TIMEOUT_SECONDS` | `30` | Reranker timeout |
| `RAG_RERANKER_GPU_LAYERS` | `auto` | Script/provider GPU layer hint |

Local chat options:

| Variable | Default | Meaning |
| --- | --- | --- |
| `LLAMA_SERVER_BASE_URL` | `http://127.0.0.1:8080` | Local chat endpoint |
| `LLAMA_SERVER_CHAT_MODEL` | `local-model` | Model name sent to `/chat/completions` |
| `LLAMA_SERVER_TEMPERATURE` | `0.2` | Generation temperature |
| `LLAMA_SERVER_MAX_TOKENS` | `1536` | Max generated tokens |
| `LLAMA_SERVER_TIMEOUT_SECONDS` | `300` | HTTP timeout |

Cloud chat options:

| Variable | Default | Meaning |
| --- | --- | --- |
| `RAG_CHAT_PROVIDER` | `llama_server` | `llama_server`, `cloud`, or legacy `deepseek` |
| `CLOUD_API_KEY` | empty | Cloud API key |
| `CLOUD_MODEL` | `deepseek-v4-pro` | Cloud model |
| `CLOUD_BASE_URL` | `https://api.deepseek.com` | Cloud endpoint base URL |
| `CLOUD_ENABLE_THINKING` | `false` | Enable provider reasoning/thinking parameter |
| `CLOUD_REASONING_EFFORT` | `medium` | Reasoning effort when enabled |

`DEEPSEEK_API_KEY`, `DEEPSEEK_MODEL`, `DEEPSEEK_BASE_URL`, `DEEPSEEK_ENABLE_THINKING`, and `DEEPSEEK_REASONING_EFFORT` are accepted as fallbacks for the matching `CLOUD_*` values.

Cloud settings changed in the UI are persisted to `.rag/cloud-settings.json`.

## Public Surface

The app mainly depends on:

- `LocalAiService`
- `RagOptions`
- `CloudApiSettings`
- `LlamaServerProcess`
- `EvalRunner`
- `ChatTurn`
- `DocumentChunk`
- `RagStreamChunk`

Retrieval internals live under `Services/Rag/`. Local AI service implementation is split across `Services/LocalAi/*.cs`.

## Offline Evaluation

Start or reuse a healthy chat endpoint first, then run:

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json
```

The script writes reports to `.eval/` by default. Override the report directory with `RAG_EVAL_REPORT_DIR` or the second script argument:

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json /path/to/report-dir
```

Equivalent direct entry:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

## Tests

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

The RAG test README at [../../tests/WeaveDoc.Rag.Tests/README.md](../../tests/WeaveDoc.Rag.Tests/README.md) describes the current coverage for query understanding, corpus selection, retrieval heuristics, fallback answers, and streaming chat client behavior.
