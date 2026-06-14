# WeaveDoc.App

English | [简体中文](README.zh-CN.md)

`WeaveDoc.App` is the unified Avalonia desktop application for WeaveDoc. It composes the Markdown editor, PDF reader, converter, template settings, and RAG assistant into one three-column workspace.

## Shell Layout

| Surface | Main files | Responsibility |
| --- | --- | --- |
| Command bar | `Views/MainWindow.axaml`, `Views/MainWindow.axaml.cs` | New/open/save, export, settings, AI panel tab commands, theme toggle |
| Left workspace | `Views/WorkspaceSidebar.*`, `Views/PdfWorkspace.*` | Document/sidebar surface and PDF reading mode |
| Center editor | `Views/EditorWorkspace.*`, `ViewModels/DocumentWorkspaceViewModel.cs` | Native Markdown editing, edit/preview switch, formatting commands, preview refresh |
| Right assistant | `Views/AiAssistantPanel.*`, `Views/RagChatView.*`, `Views/RagCorpusView.*`, `Views/RagSnapshotView.*` | Chat, corpus/literature management, retrieval snapshot display |
| Dialogs | `Views/ExportDialog.*`, `Views/SettingsDialog.*` | AFD export, PDF layout selection, cloud/provider settings, template management |

The old standalone conversion and template-management pages have been folded into `ExportDialog` and `SettingsDialog`. The current app flow is document-first: open or create Markdown in the center workspace, then export or query from the surrounding tools.

## Startup Flow

`Program.cs` handles two modes:

- `--eval <baseline.json>` runs `WeaveDoc.Rag.Services.EvalRunner` and exits without opening Avalonia.
- Normal startup registers the Syncfusion license, creates `data/weavedoc.db`, seeds built-in AFD templates, constructs `PandocPipeline`, `CompositePdfConverter`, `DocumentConversionEngine`, and `LocalAiService`, then starts Avalonia.

`App.axaml.cs` passes those services into `MainWindow`. `MainWindow` creates an `AppShellViewModel`, a `DocumentWorkspaceViewModel`, and a `RagTabViewModel` when an AI service is available.

## Project References

```text
WeaveDoc.App
├── WeaveDoc.Converter
├── WeaveDoc.MarkdownEditor
└── WeaveDoc.Rag
```

MarkdownEditor assets are linked into the app output by `WeaveDoc.App.csproj`, so the embedded preview/PDF WebView resources are available when running the unified desktop app.

## Document Workflow

`DocumentWorkspaceViewModel` owns the open Markdown document state:

- current file path and display name
- text content and dirty state
- rendered preview HTML
- save/save-as status and error messages

`MarkdownDocumentService` performs file I/O for `.md`, `.markdown`, and `.txt` files and uses the Markdown editor module's `MarkdigMarkdownRenderService` to produce preview HTML. Before save/export, `EditorWorkspace` syncs the live native editor content back into the workspace view model.

PDF files are opened through `PdfWorkspace`, which wraps `PdfViewerControl` from `WeaveDoc.MarkdownEditor` and manages temporary files created by the Avalonia storage provider.

## Export And Settings

`ExportDialog` loads templates from `ConfigManager`, lets the user choose DOCX or PDF, supports `PdfLayoutMode.SingleColumn` / `TwoColumn`, calls `DocumentConversionEngine`, and can open a converted PDF back into the workspace.

`SettingsDialog` currently groups general settings, model/cloud-provider settings, Zotero placeholder UI, template library management, and snapshot policy UI. Template import validates JSON through `AfdParser` before saving through `ConfigManager`.

## RAG Assistant

The assistant panel is intentionally hosted by the shell while the retrieval/chat implementation lives in `WeaveDoc.Rag`.

- `RagChatView` streams answers, supports stop/cancel, and pre-warms the service on first prompt focus.
- `RagCorpusView` imports Markdown/text/JSON files into the corpus, refreshes the corpus, filters the file list, and deletes selected entries.
- `RagSnapshotView` displays ranked retrieval chunks and the context chunks used by the latest answer.
- Cloud provider settings are edited through `RagTabViewModel` and persisted by `CloudApiSettings`.

## Build And Run

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Local RAG startup with helper-managed `llama-server`:

```bash
./scripts/run_weavedoc.sh
```

Offline evaluation mode:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

## Tests

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

The app test README at [../../tests/WeaveDoc.App.Tests/README.md](../../tests/WeaveDoc.App.Tests/README.md) describes the current headless shell, document, theme, and RAG view-model coverage.
