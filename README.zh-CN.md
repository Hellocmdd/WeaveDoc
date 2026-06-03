# WeaveDoc

[English](README.md) | 简体中文

WeaveDoc 是一个面向学术文档工作的 Avalonia 桌面工作区。它把 Markdown 编辑、AFD 模板管理、Markdown 到 DOCX/PDF 转换，以及可选的本地或云端 RAG 问答集中在同一个应用里。

## 模块组成

| 模块 | 项目 | 职责 |
| --- | --- | --- |
| 桌面外壳 | `src/WeaveDoc.App/` | 统一 Avalonia 入口，包含 `RAG 问答`、`文档转换`、`Markdown 编辑`、`模板管理` 页签 |
| 转换器 | `src/WeaveDoc.Converter/` | AFD 模板解析、Pandoc 管线、DOCX 样式修正和 PDF 引擎选择 |
| Markdown 编辑器 | `src/WeaveDoc.MarkdownEditor/` | Monaco 编辑、HTML 预览、KaTeX 渲染和 PDF.js 阅读控件 |
| RAG 服务 | `src/WeaveDoc.Rag/` | 文档索引、检索、重排、答案拼装、聊天提供商和评测辅助 |

## 仓库结构

| 路径 | 说明 |
| --- | --- |
| `WeaveDoc.slnx` | 应用、模块和测试的主解决方案 |
| `src/` | 产品项目 |
| `tests/` | App、Converter、MarkdownEditor、RAG 测试项目 |
| `scripts/` | 安装、启动、评测、调试和 `llama.cpp` 辅助脚本 |
| `tools/` | Pandoc 等下载后的外部工具 |
| `doc/` | 项目文档和本地工作区文档 |
| `models/` | RAG 使用的本地 GGUF 模型 |
| `.rag/`、`.eval/` | 本地 RAG 索引、日志、缓存和评测输出 |
| `llama.cpp/` | 辅助脚本使用的上游本地 AI 依赖 |

`llama.cpp/` 下的 README 属于上游项目文档，不纳入 WeaveDoc 文档整理范围。

## 环境要求

- .NET 10 SDK
- Pandoc；构建目标可以自动下载到 `tools/pandoc/`
- Windows 上的 Markdown 编辑和 PDF 阅读界面需要 WebView2 Runtime
- 可选 RAG 依赖：`llama.cpp`、embedding/reranker/chat GGUF 模型，以及可访问的本地或 OpenAI 兼容聊天端点

## 快速开始

```bash
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

构建会导入 `tools/DownloadExternalTools.targets`，当 Pandoc 缺失时自动运行平台脚本：

- Windows：`scripts/setup-tools.ps1`
- Linux/macOS：`scripts/setup-tools.sh`

如需跳过自动下载，可设置 `SkipExternalToolsDownload=true`。

## 本地 RAG

使用本地 RAG 时，先把 GGUF 模型放到 `models/`，再通过脚本启动：

```bash
./scripts/run_weavedoc.sh
```

脚本会检查模型目录，构建或复用 `llama-server`，导出 RAG 环境变量，并启动桌面应用。模型、环境变量和评测说明见 [src/WeaveDoc.Rag/README.zh-CN.md](src/WeaveDoc.Rag/README.zh-CN.md)。

## 测试

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

- [src/WeaveDoc.App/README.zh-CN.md](src/WeaveDoc.App/README.zh-CN.md)：桌面外壳、启动流程和 UI 集成
- [src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)：AFD 模板、转换管线、PDF 引擎和转换器测试
- [src/WeaveDoc.MarkdownEditor/README.md](src/WeaveDoc.MarkdownEditor/README.md)：编辑器、预览、PDF 阅读、资源和测试
- [src/WeaveDoc.Rag/README.zh-CN.md](src/WeaveDoc.Rag/README.zh-CN.md)：模型准备、RAG 运行时、聊天提供商和评测
- [tests/WeaveDoc.App.Tests/README.md](tests/WeaveDoc.App.Tests/README.md)：Avalonia headless 应用测试范围
- [tests/WeaveDoc.Converter.Tests/README.md](tests/WeaveDoc.Converter.Tests/README.md)：转换器单元测试与集成测试范围
