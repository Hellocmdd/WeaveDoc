# WeaveDoc.MarkdownEditor

`WeaveDoc.MarkdownEditor` provides WeaveDoc's Markdown editing, preview, and PDF reading surfaces. It can run as a standalone Avalonia application and is also embedded by `WeaveDoc.App`.

## Current Shape

| Capability | Main files | Notes |
| --- | --- | --- |
| Native Markdown editing | `Controls/NativeMarkdownEditorControl.*` | AvaloniaEdit editor with TextMate Markdown grammar, selection helpers, formatting wrappers, and plain-text fallback mode |
| Markdown preview | `Services/MarkdigMarkdownRenderService.cs`, `Controls/PreviewWebViewControl.*`, `Assets/preview-template.html` | Markdig AST-to-HTML rendering with line metadata, KaTeX assets, and a WebView host |
| PDF reading | `Controls/PdfViewerControl.*`, `Assets/pdfjs-5.7.284-dist/`, `Assets/pdf-viewer-template.html` | PDF.js viewer with compatibility scripts and full-screen support |
| Web host abstraction | `Controls/Web/` | `IWebViewHost` abstraction over Avalonia `NativeWebView`, with fallback policy for unavailable hosts |
| Standalone app | `Program.cs`, `Views/MainWindow.*`, `Views/MarkdownEditorTab.*` | File menu, open/save, preview, and PDF tab |
| App integration | `Views/MarkdownEditorTab.*`, `Views/IMarkdownEditorHost.cs` | Reusable view surface for the unified desktop app |

`MonacoEditorControl` and `MonacoEditorViewModel` still exist in the source tree for compatibility/history, but the active editor path is the native AvaloniaEdit-based `NativeMarkdownEditorControl`.

## Directory Map

```text
WeaveDoc.MarkdownEditor/
├── Controls/
│   ├── NativeMarkdownEditorControl.*
│   ├── PreviewWebViewControl.*
│   ├── PdfViewerControl.*
│   ├── MonacoEditorControl.*
│   └── Web/
├── Services/
│   ├── IMarkdownRenderService.cs
│   ├── MarkdigMarkdownRenderService.cs
│   ├── MarkdownService.cs
│   ├── RelaxedMathInlineParser.cs
│   ├── StorageFileOpenService.cs
│   └── Interop/
├── Views/
│   ├── MainWindow.*
│   ├── MarkdownEditorTab.*
│   └── IMarkdownEditorHost.cs
├── ViewModels/
├── Helpers/
└── Assets/
    ├── katex/
    ├── monaco-editor/
    ├── pdfjs-5.7.284-dist/
    ├── preview-template.html
    └── pdf-viewer-template.html
```

## Native Editor

`NativeMarkdownEditorControl` wraps `AvaloniaEdit.TextEditor` and exposes a small control-level API used by the app shell:

- `SetContent` / `GetContent`
- `WrapSelection`
- `GetSelection` / `SetSelection`
- `ContentEdited`
- `HasUnsyncedContent`

TextMate grammar loading is attempted for Markdown syntax highlighting. When content patterns are known to trigger expensive AvaloniaEdit behavior, the control can switch to a plain `TextBox` fallback while preserving content, selection-style operations, and horizontal scrolling.

Diagnostic flags:

```bash
WEAVEDOC_DEBUG_AVEDIT_SELECTION=1 dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1 dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
```

The first flag logs selection/pointer/layout sampling. The second suppresses fallback so AvaloniaEdit behavior can be compared during diagnostics.

## Preview Rendering

`MarkdigMarkdownRenderService` parses Markdown through Markdig with advanced extensions, pipe tables, task lists, auto links, emphasis extras, generic attributes, and a custom math extension.

The renderer emits HTML manually from the Markdig AST so WeaveDoc can preserve `data-line` metadata and control preview structure. It handles headings, paragraphs, lists, block quotes, code blocks, tables, raw HTML blocks, inline formatting, and math inlines/blocks.

`PreviewWebViewControl` injects the rendered body into `Assets/preview-template.html`, which carries the CSS/KaTeX/browser-side behavior for the preview surface.

## PDF Viewer

`PdfViewerControl` hosts PDF.js from `Assets/pdfjs-5.7.284-dist/`. It can initialize/deactivate the WebView host, load a PDF path, apply compatibility scripts required by newer PDF.js builds, and toggle full-screen mode. The unified app wraps it in `PdfWorkspace`; the standalone editor exposes it as the PDF tab.

## WebView Host

Preview and PDF surfaces do not call platform-specific WebView APIs directly. They use:

- `IWebViewHost`
- `IWebViewHostFactory`
- `NativeWebViewHost`
- `WebViewBridge`
- `WebViewRenderPolicy`

`WebViewRenderPolicy` only falls back when the host reports unsupported or uninstalled WebView capability. A host that renders in a separate native dialog is still treated as usable.

## Build And Run

Standalone editor:

```bash
dotnet build src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
```

Open a sample file at startup:

```bash
dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj -- tests/test_doc/markdown/test-simple.md
```

Unified desktop app:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

## Tests

```bash
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj -nologo
```

The test README at [../../tests/WeaveDoc.MarkdownEditor.Tests/README.md](../../tests/WeaveDoc.MarkdownEditor.Tests/README.md) describes the current NUnit/Avalonia headless coverage for rendering, file-open flows, native editor behavior, WebView hosts, PDF viewer helpers, and regression probes.
