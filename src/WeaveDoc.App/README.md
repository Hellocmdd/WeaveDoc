# WeaveDoc.App

`WeaveDoc.App` is the unified Avalonia desktop shell for the repository. It replaces the old standalone converter UI and the old standalone RAG app with one window and three tabs.

## Tabs

- `文档转换`: document conversion workflow powered by `WeaveDoc.Converter`
- `模板管理`: template CRUD and seed-template discovery powered by `WeaveDoc.Converter`
- `RAG 问答`: local/cloud chat, corpus refresh, document management, and cloud settings powered by `WeaveDoc.Rag`

## Architecture

`WeaveDoc.App` references exactly two project libraries:

- `WeaveDoc.Converter`
- `WeaveDoc.Rag`

The app itself owns the Avalonia UI pieces:

- `Program.cs`: unified entry point; routes `--eval <file>` to `WeaveDoc.Rag.EvalRunner` before Avalonia startup
- `Views/MainWindow.*`: top-level window and tab layout
- `Views/ConvertTab.*`: converter UI
- `Views/TemplateTab.*`: template management UI
- `Views/RagTab.*`: extracted RAG UI
- `ViewModels/RagTabViewModel.cs`: RAG tab state and interactions

## Lifecycle

`MainWindow` explicitly manages RAG initialization:

- on `Opened`, it calls `RagTab.InitializeAsync()`
- on `Closed`, it calls `Dispose()`

This avoids model initialization during headless UI construction and keeps tests lightweight.

## Build And Run

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Offline evaluation uses the same executable:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

On Windows, external-tool bootstrap still follows the existing PowerShell path through `tools/setup-tools.ps1`. On Linux/macOS, the same MSBuild target dispatches to `tools/setup-tools.sh`.

## Tests

Headless UI coverage lives in `tests/WeaveDoc.App.Tests/` and includes:

- converter tab interactions
- template tab interactions
- a `RagTab` smoke test that validates the control tree without calling `InitializeAsync()`
