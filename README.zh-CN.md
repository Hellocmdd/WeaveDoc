# WeaveDoc

[English](README.md) | 简体中文

WeaveDoc 是一个基于 .NET/Avalonia 的学术文档桌面工作区，用来完成 Markdown 写作、预览、模板化导出和本地/云端知识库问答。当前应用以三栏工作区为核心：左侧是文档与导航工具，中间是原生 Markdown/PDF 工作区，右侧是 AI 辅助面板。

## 仓库模块

| 模块 | 项目 | 职责 |
| --- | --- | --- |
| 桌面应用 | `src/WeaveDoc.App/` | Avalonia 外壳、Markdown/PDF 工作区、导出/设置对话框和 RAG 辅助 UI |
| Markdown 编辑器 | `src/WeaveDoc.MarkdownEditor/` | 原生 AvaloniaEdit 编辑器、Markdig 预览、WebView 预览宿主和 PDF.js 阅读器 |
| 文档转换器 | `src/WeaveDoc.Converter/` | AFD 模板系统、Pandoc 管线、DOCX 后处理和 DOCX 到 PDF 引擎链 |
| RAG 服务 | `src/WeaveDoc.Rag/` | 语料索引、检索/重排、本地 `llama-server` 聊天、云端聊天配置和离线评测 |
| 测试 | `tests/` | App、Converter、MarkdownEditor、RAG 自动化测试 |

`llama.cpp/` 是辅助脚本使用的上游依赖，其中的 README 不属于 WeaveDoc 自有文档集合。

## 仓库结构

| 路径 | 说明 |
| --- | --- |
| `WeaveDoc.slnx` | 包含产品项目和测试项目的主解决方案 |
| `WeaveDoc.csproj` | 旧的根项目占位；日常开发使用 `WeaveDoc.slnx` 或具体项目 `.csproj` |
| `src/` | 产品项目 |
| `tests/` | 自动化测试和少量 scratch/harness 项目 |
| `scripts/` | 安装、启动、RAG、诊断和测试辅助脚本 |
| `tools/` | Pandoc 等下载后的外部工具 |
| `doc/` | 项目文档、任务清单、设计文档和本地语料材料 |
| `models/` | RAG 工作流使用的本地 GGUF 模型 |
| `.rag/` | 本地 RAG 缓存、日志、导入语料和云端设置 |
| `.eval/` | 离线评测报告 |

## 环境要求

- .NET 10 SDK。
- Pandoc。转换器项目会导入 `tools/DownloadExternalTools.targets`，可将 Pandoc 准备到 `tools/pandoc/`。
- 当前操作系统需要的 Avalonia 桌面运行依赖。
- Windows 上使用 WebView 预览/PDF 界面时需要 WebView2 Runtime。
- 可选本地 RAG 栈：已初始化的 `llama.cpp`、`models/` 下的 GGUF embedding/reranker/chat 模型，以及用于脚本健康检查的 `curl`。

## 快速开始

```bash
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

统一应用会打开桌面外壳。顶部命令栏提供新建/打开/保存 Markdown、按 AFD 模板导出、打开设置、切换编辑/预览，以及展开或收起辅助面板等入口。

## 文档转换

Markdown 导出由 `WeaveDoc.Converter` 完成，核心链路是：

1. 通过 `ConfigManager` 加载 AFD 模板。
2. 预处理 Markdown，并通过 Pandoc 生成 DOCX。
3. 使用 OpenXML 修正样式、页面、页眉和页脚。
4. 按需选择 PDF 版式并执行 DOCX 到 PDF 转换。

内置模板位于 `src/WeaveDoc.Converter/Config/TemplateSchemas/`。桌面应用启动时会把模板种子写入 `data/weavedoc.db`，并在导出/设置对话框中使用。

## 本地 RAG

脚本化启动本地栈：

```bash
./scripts/run_weavedoc.sh
```

脚本会检查 `models/`，构建或复用 `llama.cpp/build/bin/llama-server`，在需要时启动聊天服务，导出 RAG 环境变量，并启动 Avalonia 应用。

当聊天端点已在外部运行，或使用云端设置时，可以直接启动应用：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

模型文件、环境变量和评测命令详见 [src/WeaveDoc.Rag/README.zh-CN.md](src/WeaveDoc.Rag/README.zh-CN.md)。

## 测试

运行整个解决方案：

```bash
dotnet test WeaveDoc.slnx -nologo
```

开发迭代时也可以单独运行：

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## 文档索引

- [src/WeaveDoc.App/README.zh-CN.md](src/WeaveDoc.App/README.zh-CN.md)：桌面外壳、应用启动、工作区布局和 UI 集成。
- [src/WeaveDoc.MarkdownEditor/README.md](src/WeaveDoc.MarkdownEditor/README.md)：原生编辑器、预览渲染、WebView 宿主抽象、PDF 阅读器和诊断。
- [src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)：AFD 模板、转换管线、PDF 引擎和转换器测试范围。
- [src/WeaveDoc.Rag/README.zh-CN.md](src/WeaveDoc.Rag/README.zh-CN.md)：本地/云端 RAG 配置、模型文件、环境变量和评测。
- [tests/WeaveDoc.App.Tests/README.md](tests/WeaveDoc.App.Tests/README.md)：桌面外壳测试范围。
- [tests/WeaveDoc.Converter.Tests/README.md](tests/WeaveDoc.Converter.Tests/README.md)：转换器测试范围。
- [tests/WeaveDoc.MarkdownEditor.Tests/README.md](tests/WeaveDoc.MarkdownEditor.Tests/README.md)：Markdown 编辑器测试范围。
- [tests/WeaveDoc.Rag.Tests/README.md](tests/WeaveDoc.Rag.Tests/README.md)：RAG 测试范围。
