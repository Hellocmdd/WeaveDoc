# WeaveDoc.MarkdownEditor

`WeaveDoc.MarkdownEditor` 是 WeaveDoc 的 Markdown 编辑、预览和 PDF 阅读模块。它可以作为独立 Avalonia 程序运行，也会被 `WeaveDoc.App` 作为 `Markdown 编辑` 页签集成。

## 功能范围

| 功能 | 对应代码 | 说明 |
| --- | --- | --- |
| Monaco 编辑器 | `Controls/MonacoEditorControl.*`、`ViewModels/MonacoEditorViewModel.cs` | 承载本地 Monaco 资源，维护编辑器内容状态 |
| Markdown 预览 | `Services/MarkdownService.cs`、`Controls/PreviewWebViewControl.*` | 将 Markdown 转成带行号定位信息的 HTML 并在 WebView2 中展示 |
| 数学公式渲染 | `Assets/katex/`、`Assets/preview-template.html` | 支持行内公式、块级公式和常见 LaTeX 环境 |
| PDF 阅读 | `Controls/PdfViewerControl.*`、`Assets/pdfjs-5.7.284-dist/` | 基于 PDF.js 加载 PDF，补齐新版 PDF.js 需要的浏览器兼容脚本 |
| App 集成 | `Views/MarkdownEditorTab.*`、`Views/IMarkdownEditorHost.cs` | 作为统一桌面应用的可嵌入页签 |

## 目录结构

```text
WeaveDoc.MarkdownEditor/
├── App.axaml / App.axaml.cs
├── Program.cs
├── WeaveDoc.MarkdownEditor.csproj
├── Controls/
│   ├── MonacoEditorControl.*
│   ├── PreviewWebViewControl.*
│   ├── PdfViewerControl.*
│   └── WebView2EnvironmentManager.cs
├── Views/
│   ├── MainWindow.*
│   ├── MarkdownEditorTab.*
│   └── IMarkdownEditorHost.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   └── MonacoEditorViewModel.cs
├── Services/
│   ├── MarkdownService.cs
│   └── Interop/WebViewInterop.cs
├── Helpers/
│   ├── Logger.cs
│   └── MarkdownHelper.cs
└── Assets/
    ├── monaco-editor/
    ├── katex/
    ├── pdfjs-5.7.284-dist/
    ├── preview-template.html
    └── pdf-viewer-template.html
```

## 技术栈

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| .NET / C# | `net10.0` | 运行时与语言 |
| Avalonia | 12.0.4 | 桌面 UI |
| Avalonia.Native | 12.0.4 | 原生平台支持 |
| Microsoft.Web.WebView2 | 1.0.2592.51 | Monaco、HTML 预览和 PDF.js 宿主 |
| Monaco Editor | 本地静态资源 | Markdown 编辑器前端 |
| KaTeX | 本地静态资源 | 数学公式渲染 |
| PDF.js | 5.7.284 | PDF 阅读与文本选择 |

## 构建与运行

独立运行 MarkdownEditor：

```bash
dotnet build src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
```

在统一桌面应用中运行：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

`WeaveDoc.App` 会通过项目引用加载 `MarkdownEditorTab`，并把 MarkdownEditor 的 `Assets/` 复制到 App 输出目录。

## 测试

```bash
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj -nologo
```

当前测试覆盖：

- `MarkdownServiceTests`：标题、段落、空输入和 null 输入行为
- `PdfViewerControlTests`：PDF.js URL、兼容脚本、worker 前缀、打开脚本和文本选择样式

## 维护说明

- WebView2 相关代码尽量集中在 `Controls/` 和 `Services/Interop/`，避免视图直接拼接互操作脚本。
- 前端静态资源统一放在 `Assets/`，由项目文件复制到输出目录。
- 嵌入 App 的行为优先放在 `MarkdownEditorTab`，独立窗口行为保留在 `MainWindow`。
