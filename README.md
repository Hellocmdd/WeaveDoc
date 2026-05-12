# WeaveDoc

English | [简体中文](README.zh-CN.md)

WeaveDoc is a local-first Avalonia RAG desktop app backed by `llama.cpp`, local embeddings, reranking, and grounded answer generation.

## Repository Layout

- `src/WeaveDoc.App/`: Avalonia entry point, window, and UI-facing RAG view model
- `src/WeaveDoc.Rag/`: RAG models, services, retrieval pipeline, and offline evaluation runner
- `tests/WeaveDoc.Rag.Tests/`: RAG unit tests
- `WeaveDoc.slnx`: solution entry
- `scripts/`: run, eval, update, and `llama.cpp` helper scripts
- `docs/`: architecture notes and evaluation baselines
- `doc/`: indexed local corpus
- `models/`: local GGUF model files
- `llama.cpp/`: upstream `ggml-org/llama.cpp` submodule

## First Clone

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
```

If you cloned without submodules:

```bash
git submodule update --init --recursive
```

## Build

```bash
dotnet build WeaveDoc.slnx
```

## Models

Prepare these files under `models/`:

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`
- a chat model such as `models/Qwen3.5-4B-Q4_K_M.gguf`

## Run

```bash
./scripts/run_weavedoc.sh
```

Or launch the desktop app directly:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

## Offline Evaluation

```bash
./scripts/eval_rag.sh
```

Or:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

## Tests

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## Documentation

- App notes: [src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- 中文应用说明: [src/WeaveDoc.App/README.zh-CN.md](src/WeaveDoc.App/README.zh-CN.md)
- RAG architecture summary: [docs/rag-architecture.md](docs/rag-architecture.md)
- 中文 RAG 架构说明: [docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- Baseline evaluation cases: [docs/eval-baseline.json](docs/eval-baseline.json)
