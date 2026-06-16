# WeaveDoc.App.Tests

`WeaveDoc.App.Tests` is the headless Avalonia/xUnit v3 test project for the unified WeaveDoc desktop shell.

## Scope

| Test class | Coverage |
| --- | --- |
| `MainWindowTests` | Three-column shell composition, command-bar buttons, splitters, AI panel visibility, PDF workspace wiring, and high-level UI state |
| `EditorChromeThemeTests` | Dark/light shell palette behavior and editor chrome legibility |
| `DocumentWorkspaceViewModelTests` | New/open/save/save-as state, dirty tracking, preview refresh, and error state |
| `DocumentSnapshotServiceTests` | Snapshot root injection, restore-before-overwrite protection, and stable snapshot ordering |
| `MarkdownDocumentServiceTests` | Markdown file validation, read/save behavior, preview generation, and unsupported extension handling |
| `MarkdownDocumentContractsTests` | Result/contract objects used by the app document service |
| `RagTabViewModelTests` | Chat-provider selection, cloud setting state, send/stop button state, and panel state |
| `LiteratureViewModelTests` | BibTeX library VM: import `.bib`, list/search/delete entries, missing-field flagging (GB/T 7714), IsBusy/status text, via `FakeLiteratureRepository` |

The project focuses on shell behavior and view-model contracts. It avoids starting local model loading or long-running RAG initialization.

## Test Stack

- .NET 10
- xUnit v3
- Avalonia Headless
- Avalonia.Headless.XUnit
- `WeaveDoc.App` project reference
- Fake WebView and file/confirmation services under `Fakes/`
- Fake snapshot service and user-data path provider under `Fakes/`

## Snapshot / Autosave Risk Checklist

| Risk | Coverage |
| --- | --- |
| 自动保存前同步编辑器最新内容 | `MainWindowTests.EditorWorkspace_AutoSaveSyncsUnsyncedEditorContentBeforeSaving` invokes the zero-delay autosave body and verifies saved content comes from the editor buffer, not the stale ViewModel snapshot |
| 快照恢复前保护当前状态 | `DocumentSnapshotServiceTests.RestoreSnapshotFileAsync_CreatesRestoreSnapshotBeforeOverwritingCurrentFile` verifies `RestoreBeforeOverwrite` captures current file content before restore writes the older snapshot |
| 保存失败 dirty 保留 | `DocumentWorkspaceViewModelTests.SaveAsync_WhenSaveFails_PreservesDirtyDocumentAndShowsError` and `AutoSaveAsync_WhenSaveFails_PreservesDirtyDocumentAndShowsError` keep dirty state and content after failed persistence |
| Linux CI 路径与时间排序稳定 | `DocumentSnapshotServiceTests.ListSnapshotsAsync_UsesProviderRootAndOrdersByCreatedAtDescendingStably` uses provider-injected roots, path segments with spaces, explicit UTC timestamps, and equal-time ordering |
| 不依赖真实定时器 | Autosave UI test calls the private zero-delay autosave body instead of waiting for debounce intervals |
| 不依赖真实用户目录 | Snapshot tests inject `FakeWeaveDocUserDataPathProvider`; ViewModel tests use `FakeDocumentSnapshotService` |

## Remaining Risks

- `EditorWorkspace` autosave scheduling debounce/max-interval timing is not fully covered without production-time abstraction; the covered path verifies the save body behavior once scheduled.
- Cross-OS path hashing case sensitivity is only indirectly covered through provider-root and path-shape tests; exact Linux vs Windows normalization still depends on runtime platform.
- `DateTime.Now` in autosave status text is not clock-injected, so tests assert format/prefix rather than a fixed timestamp.

## Files

```text
WeaveDoc.App.Tests/
├── Fakes/
├── DocumentWorkspaceViewModelTests.cs
├── DocumentSnapshotServiceTests.cs
├── EditorChromeThemeTests.cs
├── LiteratureViewModelTests.cs
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
