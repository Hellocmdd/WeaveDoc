# WeaveDoc

English | [简体中文](README.zh-CN.md)

WeaveDoc is a .NET/Avalonia desktop workspace for writing, previewing, converting, and querying academic documents. The current app centers on a three-column shell: document/navigation tools on the left, a native Markdown/PDF workspace in the middle, and an AI assistant panel on the right.

## What Is In This Repository

| Area | Project | Role |
| --- | --- | --- |
| Desktop app | `src/WeaveDoc.App/` | Avalonia shell, Markdown/PDF workspace, export/settings dialogs, and RAG assistant UI |
| Markdown editor | `src/WeaveDoc.MarkdownEditor/` | Native AvaloniaEdit Markdown editor, Markdig preview, WebView preview host, and PDF.js viewer |
| Converter | `src/WeaveDoc.Converter/` | AFD template system, Pandoc pipeline, DOCX post-processing, and DOCX-to-PDF renderer chain |
| RAG services | `src/WeaveDoc.Rag/` | Corpus indexing, retrieval/reranking, local `llama-server` chat, cloud chat settings, and evaluation |
| Tests | `tests/` | App, converter, Markdown editor, and RAG tests |

`llama.cpp/` is an upstream dependency used by helper scripts. Its README files are not part of the WeaveDoc documentation set.

## Repository Layout

| Path | Description |
| --- | --- |
| `WeaveDoc.slnx` | Main solution containing product and test projects |
| `WeaveDoc.csproj` | Legacy root project stub; normal development uses `WeaveDoc.slnx` or project-specific `.csproj` files |
| `src/` | Product projects |
| `tests/` | Automated tests and small scratch/harness projects |
| `scripts/` | Setup, launch, RAG, diagnostics, and test helper scripts |
| `tools/` | Downloaded external tools, including Pandoc |
| `doc/` | Project documents, task docs, design docs, and local corpus material |
| `models/` | Local GGUF models for RAG workflows |
| `.rag/` | Local RAG cache, logs, copied corpus files, and cloud settings |
| `.eval/` | Offline evaluation reports |

## Requirements

- .NET 10 SDK.
- Pandoc for conversion. The converter project imports `tools/DownloadExternalTools.targets`, which can provision Pandoc under `tools/pandoc/`.
- Avalonia desktop runtime dependencies for your OS.
- Windows WebView2 Runtime when using WebView-backed preview/PDF surfaces on Windows.
- Optional local RAG stack: initialized `llama.cpp`, GGUF embedding/reranker/chat models under `models/`, and `curl` for helper-script health checks.

## Quick Start

```bash
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

The unified app opens the desktop shell. Use the top command bar to create/open/save Markdown files, export through AFD templates, open settings, switch the editor preview mode, and show or hide the assistant panel.

## Conversion

Markdown export is handled by `WeaveDoc.Converter` through:

1. AFD template loading from `ConfigManager`.
2. Markdown preprocessing and Pandoc DOCX generation.
3. OpenXML style/page/header/footer correction.
4. Optional PDF layout selection and DOCX-to-PDF conversion.

Built-in templates live in `src/WeaveDoc.Converter/Config/TemplateSchemas/`. The desktop app seeds them into `data/weavedoc.db` on startup and exposes them from the export/settings dialogs.

## Local RAG

For the scripted local stack:

```bash
./scripts/run_weavedoc.sh
```

The script checks `models/`, builds or reuses `llama.cpp/build/bin/llama-server`, starts a chat server when needed, exports RAG environment variables, and launches the Avalonia app.

Use direct app startup when an endpoint is already running or when you are using cloud settings:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Detailed model names, environment variables, and evaluation commands are in [src/WeaveDoc.Rag/README.md](src/WeaveDoc.Rag/README.md).

## Tests

Run the whole solution:

```bash
dotnet test WeaveDoc.slnx -nologo
```

Run a focused project while iterating:

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## Documentation Map

- [src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md): desktop shell, app startup, workspace layout, and UI integration.
- [src/WeaveDoc.MarkdownEditor/README.md](src/WeaveDoc.MarkdownEditor/README.md): native editor, preview renderer, WebView host abstraction, PDF viewer, and diagnostics.
- [src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md): AFD templates, conversion pipeline, PDF renderers, and converter test scope.
- [src/WeaveDoc.Rag/README.md](src/WeaveDoc.Rag/README.md): local/cloud RAG setup, model files, environment variables, and evaluation.
- [tests/WeaveDoc.App.Tests/README.md](tests/WeaveDoc.App.Tests/README.md): app-shell test scope.
- [tests/WeaveDoc.Converter.Tests/README.md](tests/WeaveDoc.Converter.Tests/README.md): converter test scope.
- [tests/WeaveDoc.MarkdownEditor.Tests/README.md](tests/WeaveDoc.MarkdownEditor.Tests/README.md): Markdown editor test scope.
- [tests/WeaveDoc.Rag.Tests/README.md](tests/WeaveDoc.Rag.Tests/README.md): RAG test scope.
