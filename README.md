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

- `models/sentence-transformers--all-MiniLM-L6-v2.gguf`

Example chat model:

- `models/gemma-4-E2B-it.gguf`

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

This launcher will build and start `llama-server` when needed, then launch the Avalonia desktop app.

Useful overrides:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8081 ./scripts/run_weavedoc.sh
```

## What The App Supports

- local indexing of `.md`, `.txt`, and `.json` documents from `doc/`
- JSON ingestion into structure-aware chunks with searchable array-item section labels
- duplicate import detection so the app does not keep copying identical files into the knowledge base
- hybrid retrieval with sparse prefilter + semantic scoring + structure-aware reranking
- JSON branch-aware context expansion so related parent/child chunks can travel with the main hit
- stable citations in answers, using file path + section + chunk id instead of temporary `[1] [2]` numbering
- summary-aware fallback answers for paper and document overview questions
- offline baseline evaluation through a CLI entry point and helper script

## Offline Evaluation

Run the baseline evaluation after `llama-server` is reachable:

```bash
./scripts/eval_rag.sh
```

Or run the app in evaluation mode directly:

```bash
dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

## Documentation

- RAG architecture summary: [docs/rag-architecture.md](docs/rag-architecture.md)
- 中文 RAG 架构说明: [docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- Baseline evaluation cases: [docs/eval-baseline.json](docs/eval-baseline.json)
- App-specific usage notes: [csharp-rag-avalonia/README.md](csharp-rag-avalonia/README.md)
- 中文应用说明: [csharp-rag-avalonia/README.zh-CN.md](csharp-rag-avalonia/README.zh-CN.md)
