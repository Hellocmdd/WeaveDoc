# WeaveDoc

English | [简体中文](README.zh-CN.md)

WeaveDoc is an Avalonia desktop workspace for academic document work. It combines Markdown editing, AFD template management, Markdown-to-DOCX/PDF conversion, and optional local or cloud RAG question answering in one app.

## Modules

| Area | Project | Purpose |
| --- | --- | --- |
| Desktop shell | `src/WeaveDoc.App/` | Unified Avalonia entry with `RAG 问答`, `文档转换`, `Markdown 编辑`, and `模板管理` tabs |
| Converter | `src/WeaveDoc.Converter/` | AFD template parsing, Pandoc pipeline, DOCX styling, and PDF renderer selection |
| Markdown editor | `src/WeaveDoc.MarkdownEditor/` | Monaco editor, HTML preview, KaTeX rendering, and PDF.js reader controls |
| RAG services | `src/WeaveDoc.Rag/` | Document indexing, retrieval, reranking, answer composition, chat providers, and evaluation helpers |

## Repository Layout

| Path | Description |
| --- | --- |
| `WeaveDoc.slnx` | Main solution for app, modules, and tests |
| `src/` | Product projects |
| `tests/` | App, converter, Markdown editor, and RAG test projects |
| `scripts/` | Setup, launch, evaluation, debug, and `llama.cpp` helper scripts |
| `tools/` | Downloaded external tools such as Pandoc |
| `doc/` | Project documents and local workspace documents |
| `models/` | Local GGUF model files for the RAG workflow |
| `.rag/`, `.eval/` | Local RAG indexes, logs, cache, and evaluation output |
| `llama.cpp/` | Upstream local AI dependency used by helper scripts |

README files under `llama.cpp/` are upstream documentation and are intentionally not part of the WeaveDoc documentation set.

## Requirements

- .NET 10 SDK
- Pandoc for conversion; the build target can download it into `tools/pandoc/`
- WebView2 Runtime on Windows for the Markdown editor and PDF viewer surfaces
- Optional RAG dependencies: `llama.cpp`, GGUF embedding/reranker/chat models, and a reachable local or OpenAI-compatible chat endpoint

## Quick Start

```bash
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

The build imports `tools/DownloadExternalTools.targets` and runs the platform setup script when Pandoc is missing:

- Windows: `scripts/setup-tools.ps1`
- Linux/macOS: `scripts/setup-tools.sh`

Set `SkipExternalToolsDownload=true` if you want to skip the automatic download.

## Local RAG

For the local RAG stack, prepare the GGUF models under `models/` and launch through:

```bash
./scripts/run_weavedoc.sh
```

That script checks the model directory, builds or reuses `llama-server`, exports RAG environment variables, and starts the desktop app. Detailed model, environment, and evaluation notes live in [src/WeaveDoc.Rag/README.md](src/WeaveDoc.Rag/README.md).

## Tests

```bash
dotnet test WeaveDoc.slnx -nologo
```

Run an individual project when iterating:

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## Documentation Map

- [src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md): desktop shell, startup flow, and UI integration
- [src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md): AFD templates, conversion pipeline, PDF engines, and converter tests
- [src/WeaveDoc.MarkdownEditor/README.md](src/WeaveDoc.MarkdownEditor/README.md): editor, preview, PDF reader, assets, and tests
- [src/WeaveDoc.Rag/README.md](src/WeaveDoc.Rag/README.md): model setup, RAG runtime, chat providers, and evaluation
- [tests/WeaveDoc.App.Tests/README.md](tests/WeaveDoc.App.Tests/README.md): headless Avalonia app test scope
- [tests/WeaveDoc.Converter.Tests/README.md](tests/WeaveDoc.Converter.Tests/README.md): converter unit and integration test scope
