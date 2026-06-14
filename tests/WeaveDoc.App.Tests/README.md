# WeaveDoc.App.Tests

`WeaveDoc.App.Tests` is the headless Avalonia/xUnit v3 test project for the unified WeaveDoc desktop shell.

## Scope

| Test class | Coverage |
| --- | --- |
| `MainWindowTests` | Three-column shell composition, command-bar buttons, splitters, AI panel visibility, PDF workspace wiring, and high-level UI state |
| `EditorChromeThemeTests` | Dark/light shell palette behavior and editor chrome legibility |
| `DocumentWorkspaceViewModelTests` | New/open/save/save-as state, dirty tracking, preview refresh, and error state |
| `MarkdownDocumentServiceTests` | Markdown file validation, read/save behavior, preview generation, and unsupported extension handling |
| `MarkdownDocumentContractsTests` | Result/contract objects used by the app document service |
| `RagTabViewModelTests` | Chat-provider selection, cloud setting state, send/stop button state, and panel state |

The project focuses on shell behavior and view-model contracts. It avoids starting local model loading or long-running RAG initialization.

## Test Stack

- .NET 10
- xUnit v3
- Avalonia Headless
- Avalonia.Headless.XUnit
- `WeaveDoc.App` project reference
- Fake WebView and file/confirmation services under `Fakes/`

## Files

```text
WeaveDoc.App.Tests/
├── Fakes/
├── DocumentWorkspaceViewModelTests.cs
├── EditorChromeThemeTests.cs
├── MainWindowTests.cs
├── MarkdownDocumentContractsTests.cs
├── MarkdownDocumentServiceTests.cs
├── RagTabViewModelTests.cs
├── TestAppBuilder.cs
└── WeaveDoc.App.Tests.csproj
```

## Run

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "MainWindowTests" -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "DocumentWorkspaceViewModelTests" -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "RagTabViewModelTests" -nologo
```

## Notes

- `TestAppBuilder` configures Avalonia headless and swaps WebView hosts with fakes where needed.
- Tests should validate shell wiring without requiring actual WebView rendering, PDF renderers, local GGUF models, or network endpoints.
- When adding UI tests, prefer stable control names and view-model state over fragile visual-position assertions.
