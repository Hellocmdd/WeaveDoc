# WeaveDoc.App

[English](README.md) | 简体中文

`WeaveDoc.App` 是 WeaveDoc 的统一 Avalonia 桌面入口。它把转换器、Markdown 编辑器、模板管理和 RAG 服务模块整合成一个多页签工作区。

## 页签组成

| 页签 | 对应代码 | 职责 |
| --- | --- | --- |
| `RAG 问答` | `MainWindow.*`、`ViewModels/RagTabViewModel.cs`、`src/WeaveDoc.Rag/` | 文档管理、本地/云端聊天设置、检索和答案展示 |
| `文档转换` | `Views/ConvertTab.*`、`src/WeaveDoc.Converter/` | Markdown 输入、AFD 模板选择、DOCX/PDF 输出和 PDF 版式选择 |
| `Markdown 编辑` | `src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.*` | 嵌入式 Markdown 编辑、HTML 预览和 PDF 阅读界面 |
| `模板管理` | `Views/TemplateTab.*`、`src/WeaveDoc.Converter/Config/` | 种子模板加载、模板列表、导入、刷新和删除 |

## 启动流程

`Program.cs` 负责：

- 识别 `--eval <baseline.json>`，并直接交给 `EvalRunner`，不启动 Avalonia UI
- 注册 PDF 兜底转换使用的 Syncfusion 许可证
- 创建应用输出目录下的 `data/weavedoc.db`
- 通过 `ConfigManager` 导入内置 AFD 模板
- 构造 `PandocPipeline`、`CompositePdfConverter` 和 `DocumentConversionEngine`
- 带着配置好的服务启动 Avalonia

`MainWindow` 使用 `RagTabViewModel` 管理 RAG 状态，把转换服务注入 `ConvertTab` 和 `TemplateTab`，并且只在选中 Markdown 页签时激活 Markdown 编辑器。

## 项目引用

```text
WeaveDoc.App
├── WeaveDoc.Converter
├── WeaveDoc.MarkdownEditor
└── WeaveDoc.Rag
```

MarkdownEditor 的静态资源会通过项目文件复制到 App 输出目录，因此统一桌面程序可以直接加载 Monaco、KaTeX 和 PDF.js 资源。

## 构建与运行

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

需要本地 RAG 且希望脚本管理 `llama-server` 时：

```bash
./scripts/run_weavedoc.sh
```

## 评测模式

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

评测模式会运行 `WeaveDoc.Rag.Services.EvalRunner`，结束后直接退出，不打开桌面 UI。

## 测试

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

headless Avalonia 测试范围见 [../../tests/WeaveDoc.App.Tests/README.md](../../tests/WeaveDoc.App.Tests/README.md)。
