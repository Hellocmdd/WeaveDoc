# WeaveDoc

English | [简体中文](README.zh-CN.md)

WeaveDoc is a local RAG desktop application built around an Avalonia front end, local embedding + retrieval, and `llama.cpp` for chat generation.

The current repository combines three layers:

- `llama.cpp` as a Git submodule for local model serving
- `csharp-rag-avalonia` as the desktop app and RAG pipeline
- helper scripts for update, build, run, and offline evaluation

## Repository Layout

- `csharp-rag-avalonia/`: Avalonia desktop app, RAG pipeline, prompt assembly, and local document management
- `llama.cpp/`: upstream `ggml-org/llama.cpp` linked as a Git submodule
- `scripts/`: build, run, debug, and update helper scripts
- `docs/`: project documentation such as the RAG architecture summary
- `doc/`: local source documents indexed by the app
- `models/`: local GGUF model files used by the app and `llama-server`

## First Clone

Clone the repository together with submodules:

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
```

If you already cloned the repository without submodules:

```bash
git submodule update --init --recursive
```

## Models

Prepare GGUF models in the workspace-level `models/` directory.

Required embedding model:

- `models/bge-m3.gguf`

Required reranker model:

- `models/bge-reranker-v2-m3.gguf`

Default chat model:

- `models/Qwen3.5-4B-Q4_K_M.gguf`

## One-Click Update

The repository includes `llama.cpp` as a submodule and provides a helper script to refresh both the main repo and the submodule, then rebuild the local runtime pieces:

```bash
./scripts/update_all.sh
```

By default, the script:

- pulls the current branch of this repository
- syncs and initializes submodules
- updates `llama.cpp` to the latest commit on the tracked branch from `.gitmodules`
- rebuilds `llama.cpp`
- rebuilds the Avalonia app

If you only want the submodule version pinned by the current main repo commit, use:

```bash
./scripts/update_all.sh --pinned
```

## Run

After models are prepared in `models/`, start the full local stack with:

```bash
./scripts/run_weavedoc.sh
```

This launcher will build and start the chat `llama-server` and the BGE reranker server when needed, then launch the Avalonia desktop app.

Useful overrides:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_RERANKER_PORT=8083 ./scripts/run_weavedoc.sh
```

## What The App Supports

- local indexing of `.md`, `.txt`, `.json`, and `.pdf` documents from `doc/`
- JSON ingestion into structure-aware chunks with stable section-path labels for deep array items
- duplicate import detection so the app does not keep copying identical files into the knowledge base
- model-driven retrieval: pure vector cosine Top-N candidate retrieval followed by BGE-Reranker cross-encoder as sole ranking authority
- document-scope hard filter for title-scoped questions (for example `《...》这篇论文...`), restricting candidates to the requested file before reranking
- intent-specific context window assembly (metadata slot prioritization, procedure structure preference, definition section-title matching, compare subject coverage, summary lead/support split)
- stable citations in answers, using file path + section + chunk id instead of temporary `[1] [2]` numbering
- multi-intent support: `compare`, `explain`, `procedure`, `usage`, `summary`, `definition`, `module_list`, `module_implementation`, `composition`, `metadata`, `general`
- strong system prompt enforcing grounding, terminology preservation, and citation discipline
- repair retry and intent-specific fallback answer builders for weak or off-topic generations
- offline baseline evaluation through a CLI entry point and helper script, with keyword checks, structural answer checks, retrieval signal coverage, citation precision/recall, and scope accuracy
- optional cloud chat provider (DeepSeek API) as an alternative to local `llama-server`

## SQLite Store Sync

The legacy `rag_store.db` can be refreshed from the converted markdown and JSON corpus:

```bash
./scripts/sync_markdown_json_sqlite.sh
```

## Offline Evaluation

Run the baseline evaluation after `llama-server` is reachable:

```bash
./scripts/eval_rag.sh
```

Notes:

- exit code `0`: all eval cases passed
- exit code `2`: at least one case failed
- the evaluator measures keyword coverage, top-chunk signal coverage, context signal coverage, citation precision/recall, scope accuracy, and evidence kind accuracy
- the baseline schema expresses answer, retrieval, and citation expectations separately so regressions can be localized

Or run the app in evaluation mode directly:

```bash
dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

### Latest benchmark (2026-05-06)

| Metric | Value |
|--------|-------|
| Case pass count | 15 / 20 (75%) |
| Keyword coverage | 89.8% |
| Context signal coverage | 98.4% |
| Avg citation recall | 95.0% |

## Documentation

- RAG architecture summary: [docs/rag-architecture.md](docs/rag-architecture.md)
- 中文 RAG 架构说明: [docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- Baseline evaluation cases: [docs/eval-baseline.json](docs/eval-baseline.json)
- App-specific usage notes: [csharp-rag-avalonia/README.md](csharp-rag-avalonia/README.md)
- 中文应用说明: [csharp-rag-avalonia/README.zh-CN.md](csharp-rag-avalonia/README.zh-CN.md)
