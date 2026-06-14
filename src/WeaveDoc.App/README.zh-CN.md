# WeaveDoc.App

[English](README.md) | 简体中文

`WeaveDoc.App` 是 WeaveDoc 的统一 Avalonia 桌面应用。它把 Markdown 编辑器、PDF 阅读器、转换器、模板设置和 RAG 辅助面板组合成一个三栏工作区。

## 外壳布局

| 界面 | 主要文件 | 职责 |
| --- | --- | --- |
| 顶部命令栏 | `Views/MainWindow.axaml`、`Views/MainWindow.axaml.cs` | 新建/打开/保存、导出、设置、AI 面板页签命令、主题切换 |
| 左侧工作区 | `Views/WorkspaceSidebar.*`、`Views/PdfWorkspace.*` | 文档/侧栏区域和 PDF 阅读模式 |
| 中间编辑区 | `Views/EditorWorkspace.*`、`ViewModels/DocumentWorkspaceViewModel.cs` | 原生 Markdown 编辑、编辑/预览切换、格式命令、预览刷新 |
| 右侧辅助栏 | `Views/AiAssistantPanel.*`、`Views/RagChatView.*`、`Views/RagCorpusView.*`、`Views/RagSnapshotView.*` | 问答、语料/文献管理、检索快照展示 |
| 对话框 | `Views/ExportDialog.*`、`Views/SettingsDialog.*` | AFD 导出、PDF 版式选择、云端/本地模型设置、模板管理 |

旧的独立转换页和模板管理页已经被折叠进 `ExportDialog` 和 `SettingsDialog`。当前应用流程以文档为中心：先在中间工作区打开或新建 Markdown，再用周边工具导出、设置或问答。

## 启动流程

`Program.cs` 处理两种模式：

- `--eval <baseline.json>` 直接运行 `WeaveDoc.Rag.Services.EvalRunner`，结束后退出，不打开 Avalonia。
- 正常启动时注册 Syncfusion 许可证，创建 `data/weavedoc.db`，导入内置 AFD 模板，构造 `PandocPipeline`、`CompositePdfConverter`、`DocumentConversionEngine` 和 `LocalAiService`，再启动 Avalonia。

`App.axaml.cs` 把这些服务传入 `MainWindow`。`MainWindow` 创建 `AppShellViewModel`、`DocumentWorkspaceViewModel`，并在有 AI 服务时创建 `RagTabViewModel`。

## 项目引用

```text
WeaveDoc.App
├── WeaveDoc.Converter
├── WeaveDoc.MarkdownEditor
└── WeaveDoc.Rag
```

`WeaveDoc.App.csproj` 会把 MarkdownEditor 的资源链接到 App 输出目录，因此统一桌面应用运行时可以直接使用预览和 PDF WebView 资源。

## 文档工作流

`DocumentWorkspaceViewModel` 维护当前 Markdown 文档状态：

- 当前文件路径和展示名
- 文本内容和未保存状态
- 渲染后的预览 HTML
- 保存/另存为状态和错误消息

`MarkdownDocumentService` 负责 `.md`、`.markdown`、`.txt` 文件读写，并使用 MarkdownEditor 模块的 `MarkdigMarkdownRenderService` 生成预览 HTML。保存或导出前，`EditorWorkspace` 会把原生编辑器中的实时文本同步回工作区 ViewModel。

PDF 文件通过 `PdfWorkspace` 打开。它封装 `WeaveDoc.MarkdownEditor` 的 `PdfViewerControl`，并管理 Avalonia 存储提供器产生的临时文件。

## 导出与设置

`ExportDialog` 从 `ConfigManager` 加载模板，支持 DOCX/PDF 输出、`PdfLayoutMode.SingleColumn` / `TwoColumn` 版式选择，调用 `DocumentConversionEngine` 完成转换，并可把转换后的 PDF 重新打开到工作区。

`SettingsDialog` 目前包含通用设置、模型/云端提供商设置、Zotero 占位界面、模板库管理和快照策略界面。模板导入会先通过 `AfdParser` 校验 JSON，再通过 `ConfigManager` 保存。

## RAG 辅助面板

辅助面板由桌面外壳承载，检索和聊天实现位于 `WeaveDoc.Rag`。

- `RagChatView` 流式显示答案，支持停止生成，并在输入框首次聚焦时预热服务。
- `RagCorpusView` 可导入 Markdown/text/JSON 文件、刷新语料、筛选文件列表并删除条目。
- `RagSnapshotView` 展示最近一次问答的候选检索块和实际上下文块。
- 云端提供商设置通过 `RagTabViewModel` 编辑，并由 `CloudApiSettings` 持久化。

## 构建与运行

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

需要脚本管理本地 `llama-server` 时：

```bash
./scripts/run_weavedoc.sh
```

离线评测模式：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

## 测试

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

当前 headless 外壳、文档、主题和 RAG ViewModel 覆盖范围见 [../../tests/WeaveDoc.App.Tests/README.md](../../tests/WeaveDoc.App.Tests/README.md)。
