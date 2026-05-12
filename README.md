# WeaveDoc

English | [简体中文](README.zh-CN.md)

WeaveDoc is a unified desktop workspace for document conversion, template management, and local RAG. The repository now centers on a single Avalonia shell, `WeaveDoc.App`, with the converter and RAG logic split into dedicated libraries under `src/`.

## Repository Layout

- `src/WeaveDoc.App/`: the only desktop entry, with three tabs: `文档转换`, `模板管理`, and `RAG 问答`
- `src/WeaveDoc.Converter/`: document conversion core, Pandoc pipeline, AFD templates, and PDF export
- `src/WeaveDoc.Rag/`: RAG services, retrieval pipeline, local/cloud chat integration, and offline evaluation runner
- `tests/WeaveDoc.App.Tests/`: headless UI smoke and interaction tests for the unified shell
- `tests/WeaveDoc.Converter.Tests/`: converter unit and integration tests
- `tests/WeaveDoc.Rag.Tests/`: RAG unit tests
- `WeaveDoc.slnx`: the main solution entry
- `scripts/`: run, eval, update, and `llama.cpp` helper scripts
- `docs/`: architecture notes and evaluation baselines
- `doc/`, `models/`, `.rag/`, `.eval/`: workspace-level runtime data directories kept at the repo root
- `llama.cpp/`: upstream `ggml-org/llama.cpp` submodule

## First Clone

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
```

If you already cloned without submodules:

```bash
git submodule update --init --recursive
```

## Build

Build the full solution with:

```bash
dotnet build WeaveDoc.slnx
```

Pandoc bootstrap remains cross-platform and keeps the existing Windows path intact:

- Windows: `tools/DownloadExternalTools.targets` dispatches to `tools/setup-tools.ps1`
- Linux/macOS: the same target dispatches to `tools/setup-tools.sh`
- To skip auto-download, set `SkipExternalToolsDownload=true`

Example:

```bash
dotnet build WeaveDoc.slnx -p:SkipExternalToolsDownload=true
```

## Models

Prepare GGUF models in the workspace-level `models/` directory.

Required embedding model:

- `models/bge-m3.gguf`

Required reranker model:

- `models/bge-reranker-v2-m3.gguf`

Default chat model:

- `models/Qwen3.5-4B-Q4_K_M.gguf`

## Run

Start the local stack with:

```bash
./scripts/run_weavedoc.sh
```

The launcher script keeps managing chat `llama-server`, then starts `WeaveDoc.App`. The reranker is no longer launched by the script and is auto-managed inside `WeaveDoc.Rag`.

Useful overrides:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_RERANKER_PORT=8083 ./scripts/run_weavedoc.sh
```

You can also launch the desktop app directly:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

This direct command works on Windows as well; the Windows external-tool bootstrap path remains unchanged.

## Offline Evaluation

Run the helper script after chat `llama-server` is reachable:

```bash
./scripts/eval_rag.sh
```

Or run evaluation mode directly through the unified app entry:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

## What The Unified App Supports

- visual document conversion and template management in one Avalonia shell
- local indexing of `.md`, `.txt`, `.json`, and `.pdf` documents from `doc/`
- local embedding, retrieval, reranking, and grounded chat answers
- local `llama-server` chat or any OpenAI-compatible cloud API
- offline RAG baseline evaluation through the same `WeaveDoc.App` executable

## Tests

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## Documentation

- Unified app notes: [src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- Converter library notes: [src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)
- RAG library notes: [src/WeaveDoc.Rag/README.md](src/WeaveDoc.Rag/README.md)
- RAG architecture summary: [docs/rag-architecture.md](docs/rag-architecture.md)
- 中文 RAG 架构说明: [docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- Baseline evaluation cases: [docs/eval-baseline.json](docs/eval-baseline.json)
