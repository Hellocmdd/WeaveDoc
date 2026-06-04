# WeaveDoc.App

English | [简体中文](README.zh-CN.md)

`WeaveDoc.App` is the unified Avalonia desktop entry for WeaveDoc. It wires the converter, Markdown editor, template manager, and RAG service modules into one tabbed workspace.

## Tabs

| Tab | Backing code | Purpose |
| --- | --- | --- |
| `RAG 问答` | `MainWindow.*`, `ViewModels/RagTabViewModel.cs`, `src/WeaveDoc.Rag/` | Document management, local/cloud chat settings, retrieval, and answer display |
| `文档转换` | `Views/ConvertTab.*`, `src/WeaveDoc.Converter/` | Markdown input selection, AFD template selection, DOCX/PDF output, and PDF layout selection |
| `Markdown 编辑` | `src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.*` | Embedded Markdown editor, HTML preview, and PDF reader surface |
| `模板管理` | `Views/TemplateTab.*`, `src/WeaveDoc.Converter/Config/` | Seed template loading, template listing, import, refresh, and delete operations |

## Startup Flow

`Program.cs` is responsible for:

- handling `--eval <baseline.json>` and routing it through `EvalRunner` without starting Avalonia
- registering the Syncfusion license used by PDF fallback conversion
- creating the app-local `data/weavedoc.db`
- seeding built-in AFD templates through `ConfigManager`
- constructing `PandocPipeline`, `CompositePdfConverter`, and `DocumentConversionEngine`
- starting Avalonia with the configured services

`MainWindow` keeps RAG state in `RagTabViewModel`, injects converter services into `ConvertTab` and `TemplateTab`, and activates the Markdown editor tab only when that tab is selected.

## Project References

```text
WeaveDoc.App
├── WeaveDoc.Converter
├── WeaveDoc.MarkdownEditor
└── WeaveDoc.Rag
```

Markdown editor assets are copied into the app output through the project file, so the embedded Monaco, KaTeX, and PDF.js resources are available when running the unified app.

## Build And Run

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

For local RAG startup with `llama-server` management:

```bash
./scripts/run_weavedoc.sh
```

## Evaluation Mode

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

Evaluation mode runs `WeaveDoc.Rag.Services.EvalRunner` and exits without opening the desktop UI.

## Tests

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

The test README at [../../tests/WeaveDoc.App.Tests/README.md](../../tests/WeaveDoc.App.Tests/README.md) documents the headless Avalonia coverage.
