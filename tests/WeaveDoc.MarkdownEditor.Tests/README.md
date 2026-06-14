# WeaveDoc.MarkdownEditor.Tests

`WeaveDoc.MarkdownEditor.Tests` is the NUnit/Avalonia Headless test project for the standalone and embeddable Markdown editor module.

## Scope

| Test class | Coverage |
| --- | --- |
| `MarkdownRenderServiceTests` | Markdig renderer behavior for headings, paragraphs, lists, tables, raw HTML, math, escaping, and line metadata |
| `NativeMarkdownEditorControlTests` | Native editor content, selection/wrapping helpers, fallback state, and editor API contracts |
| `MainWindowViewModelTests` | Standalone editor view-model file state, preview state, save behavior, and status text |
| `MainWindowOpenWorkflowTests` | Storage-provider open flows for Markdown/PDF paths |
| `StorageFileOpenServiceTests` | Markdown and PDF file preparation helpers |
| `PdfViewerControlTests` | PDF.js URL/script generation, compatibility helpers, worker setup, and text-selection styling |
| `PreviewTemplateCompatibilityTests` | Preview template compatibility with generated preview HTML |
| `WebViewHostControlTests` | WebView host abstraction and fallback behavior |
| `AppInitTests`, `AppCrashChallengeTests`, `PermissionTests`, `EdgeCaseTests` | Startup and regression probes |
| `StandaloneLatexPerformanceProbeTests`, `NativeWebViewStressTest` | Performance/stress probes for known Markdown/WebView paths |

## Test Stack

- .NET 10
- NUnit
- Avalonia Headless
- Avalonia.Headless.NUnit
- Project reference to `src/WeaveDoc.MarkdownEditor`
- Fake WebView host under `Fakes/`

## Files

```text
WeaveDoc.MarkdownEditor.Tests/
├── Fakes/
├── AppCrashChallengeTests.cs
├── AppInitTests.cs
├── EdgeCaseTests.cs
├── MainWindowOpenWorkflowTests.cs
├── MainWindowViewModelTests.cs
├── MarkdownServiceTests.cs
├── NativeMarkdownEditorControlTests.cs
├── NativeWebViewStressTest.cs
├── PdfViewerControlTests.cs
├── PermissionTests.cs
├── PreviewTemplateCompatibilityTests.cs
├── StandaloneLatexPerformanceProbeTests.cs
├── StorageFileOpenServiceTests.cs
├── TestAppBuilder.cs
├── WebViewHostControlTests.cs
└── WeaveDoc.MarkdownEditor.Tests.csproj
```

## Run

```bash
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --filter "MarkdownRenderServiceTests" -nologo
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --filter "NativeMarkdownEditorControlTests" -nologo
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --filter "PdfViewerControlTests" -nologo
```

## Notes

- `TestAppBuilder` configures Avalonia headless and the fake WebView host.
- Tests should cover native editor behavior without depending on a visible desktop window.
- WebView/PDF tests should prefer generated URLs, scripts, and host abstraction behavior over machine-specific browser rendering.
