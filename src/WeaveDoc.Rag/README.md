# WeaveDoc.Rag

English | [简体中文](README.zh-CN.md)

`WeaveDoc.Rag` is the retrieval and chat service module behind the `RAG 问答` tab in `WeaveDoc.App`. It does not ship as a separate desktop application; the unified app owns the UI and calls this library.

If you only use Markdown editing, template management, or document conversion, you can skip this module and its model setup.

## Responsibilities

- workspace path discovery for `doc/`, `models/`, `.rag/`, and `.eval/`
- local document indexing and corpus refresh
- hybrid retrieval across vector, BM25, keyword, title, and JSON-structure signals
- optional reranking through an OpenAI-compatible reranker endpoint
- local `llama-server` chat integration
- OpenAI-compatible cloud chat settings
- fallback answer composition and citation normalization
- offline evaluation through `EvalRunner`

## Workspace Directories

All paths are rooted at the repository workspace:

| Path | Purpose |
| --- | --- |
| `doc/` | Source documents for indexing |
| `models/` | GGUF embedding, reranker, and chat models |
| `.rag/` | Index files, logs, and runtime cache |
| `.eval/` | Evaluation reports |

## Model Files

The local stack expects these files by default:

| File | Used for |
| --- | --- |
| `models/bge-m3.gguf` | Embedding |
| `models/bge-reranker-v2-m3.gguf` | Reranker when `RAG_RERANKER_ENABLED=true` |
| `models/Qwen3.5-4B-Q4_K_M.gguf` or another non-embedding `.gguf` | Chat model for `llama-server` |

You can override the chat model:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
```

## Run The Local Stack

```bash
./scripts/run_weavedoc.sh
```

The script checks `models/`, builds or reuses `llama.cpp/build/bin/llama-server`, starts `llama-server` when no healthy endpoint is already running, exports the RAG environment variables, and launches `WeaveDoc.App`.

Useful overrides:

```bash
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
RAG_RERANKER_ENABLED=false ./scripts/run_weavedoc.sh
RAG_RERANKER_BASE_URL=http://127.0.0.1:8083 ./scripts/run_weavedoc.sh
```

## Run The App Directly

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Use the direct app entry when your chat endpoint is already running, when you use a cloud provider, or when you want to provide settings manually.

Common environment variables:

| Variable | Default | Meaning |
| --- | --- | --- |
| `LLAMA_SERVER_BASE_URL` | `http://127.0.0.1:8080` | Local chat endpoint |
| `LLAMA_SERVER_CHAT_MODEL` | `local-model` | Chat model name sent to the endpoint |
| `LLAMA_SERVER_TEMPERATURE` | `0.2` | Chat generation temperature |
| `LLAMA_SERVER_MAX_TOKENS` | `1536` | Chat max token budget |
| `RAG_EMBEDDING_MODEL_FILE` | `bge-m3.gguf` | Embedding model filename |
| `RAG_RERANKER_ENABLED` | `true` | Enables reranking |
| `RAG_RERANKER_BASE_URL` | `http://127.0.0.1:8081` | Reranker endpoint |
| `RAG_RERANKER_MODEL` | `bge-reranker-v2-m3` | Reranker model name |
| `RAG_CHAT_PROVIDER` | `llama_server` | `llama_server`, `cloud`, or `deepseek` |
| `CLOUD_API_KEY` | empty | Cloud provider API key |
| `CLOUD_MODEL` | `deepseek-v4-pro` | Cloud model name |
| `CLOUD_BASE_URL` | `https://api.deepseek.com` | Cloud base URL |
| `CLOUD_ENABLE_THINKING` | `false` | Enables provider reasoning/thinking mode when supported |
| `CLOUD_REASONING_EFFORT` | `medium` | Reasoning effort when supported |

`DEEPSEEK_API_KEY`, `DEEPSEEK_MODEL`, `DEEPSEEK_BASE_URL`, `DEEPSEEK_ENABLE_THINKING`, and `DEEPSEEK_REASONING_EFFORT` are also accepted as fallbacks for the matching cloud variables.

## Offline Evaluation

Start or reuse a healthy chat endpoint first, then run:

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json
```

Or call the app entry directly:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

The script writes reports to `.eval/` by default. Override with `RAG_EVAL_REPORT_DIR` or pass a second script argument:

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json /path/to/report-dir
```

## Public Surface

The main public types used by the app and tests are:

- `LocalAiService`
- `CloudApiSettings`
- `EvalRunner`
- `ChatTurn`
- `DocumentChunk`
- `RagOptions`

Retrieval internals live under `Services/Rag/` and are treated as implementation details.

## Tests

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

Current tests focus on query understanding, follow-up detection, fallback answer composition, citation normalization, and retrieval heuristic behavior.
