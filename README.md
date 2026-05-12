# WeaveDoc

English | [简体中文](README.zh-CN.md)

WeaveDoc is a unified desktop workspace for template-driven document conversion, template management, and optional local RAG workflows. The repository now centers on one Avalonia desktop entry with focused converter and RAG modules underneath.

## Modules

- `src/WeaveDoc.App/`: unified Avalonia shell with `文档转换`, `模板管理`, and `RAG 问答` tabs
- `src/WeaveDoc.Converter/`: Markdown + AFD template to DOCX/PDF conversion pipeline
- `src/WeaveDoc.Rag/`: document indexing, retrieval, reranking, chat integration, and offline evaluation helpers

## Repository Layout

- `tests/`: app, converter, and RAG test projects
- `scripts/`: launch, evaluation, and `llama.cpp` helper scripts
- `docs/`: architecture notes and evaluation baseline files
- `doc/`, `models/`, `.rag/`, `.eval/`: workspace-level data directories
- `llama.cpp/`: upstream submodule used by the local RAG stack
- `WeaveDoc.slnx`: main solution entry

## Quick Start

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Pandoc bootstrap is handled during build:

- Windows uses `tools/setup-tools.ps1`
- Linux/macOS uses `tools/setup-tools.sh`
- set `SkipExternalToolsDownload=true` if you want to skip auto-download

For converter and template-management work, the commands above are enough. If you want to use the local RAG tab or run offline evaluation, continue with the module guide in `src/WeaveDoc.Rag/README.md`.

## README Scope

- Repository-level setup, build, test, and navigation stay in this README.
- RAG-specific model preparation, `llama-server` startup, environment variables, and evaluation instructions now live in `src/WeaveDoc.Rag/README.md`.
- Converter implementation details and template-format notes live in `src/WeaveDoc.Converter/README.md`.

This keeps the root document focused on the whole repo instead of one subsystem.

## Tests

```bash
dotnet test WeaveDoc.slnx -nologo
```

## Documentation

- App shell notes: [src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- Converter module notes (Chinese): [src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)
- RAG module notes: [src/WeaveDoc.Rag/README.md](src/WeaveDoc.Rag/README.md)
- RAG architecture summary: [docs/rag-architecture.md](docs/rag-architecture.md)
- Baseline evaluation cases: [docs/eval-baseline.json](docs/eval-baseline.json)
