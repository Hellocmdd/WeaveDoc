# WeaveDoc

[English](README.md) | 简体中文

WeaveDoc 现在已经收敛为一个统一桌面工作区：用 `WeaveDoc.App` 作为唯一 Avalonia 图形入口，把文档转换与模板管理、RAG 问答和离线评测统一到同一套 `src/` 结构下。

## 仓库结构

- `src/WeaveDoc.App/`：唯一桌面入口，包含 `文档转换`、`模板管理`、`RAG 问答` 三个页签
- `src/WeaveDoc.Converter/`：文档转换核心、Pandoc 管道、AFD 模板和 PDF 导出
- `src/WeaveDoc.Rag/`：RAG 服务、检索链路、本地/云端聊天接入和离线评测运行器
- `tests/WeaveDoc.App.Tests/`：统一桌面壳的 Headless UI 测试
- `tests/WeaveDoc.Converter.Tests/`：转换器单元与集成测试
- `tests/WeaveDoc.Rag.Tests/`：RAG 单元测试
- `WeaveDoc.slnx`：主解决方案入口
- `scripts/`：运行、评测、更新、`llama.cpp` 辅助脚本
- `docs/`：架构说明和评测基线
- `doc/`、`models/`、`.rag/`、`.eval/`：继续保留在仓库根目录的工作区数据目录
- `llama.cpp/`：上游 `ggml-org/llama.cpp` 子模块

## 首次克隆

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
```

如果之前没有带 submodule：

```bash
git submodule update --init --recursive
```

## 构建

统一构建入口：

```bash
dotnet build WeaveDoc.slnx
```

Pandoc 自动准备同时保留原有 Windows 路径，并补齐 Linux/macOS 路径：

- Windows：`tools/DownloadExternalTools.targets` 继续分派到 `tools/setup-tools.ps1`
- Linux/macOS：同一个 target 分派到 `tools/setup-tools.sh`
- 如果要跳过自动下载，可设置 `SkipExternalToolsDownload=true`

例如：

```bash
dotnet build WeaveDoc.slnx -p:SkipExternalToolsDownload=true
```

## 模型准备

请把 GGUF 模型放到工作区级的 `models/` 目录中。

必须准备：

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`

默认聊天模型：

- `models/Qwen3.5-4B-Q4_K_M.gguf`

## 运行

完整本地栈启动命令：

```bash
./scripts/run_weavedoc.sh
```

脚本会继续负责聊天 `llama-server`，随后启动 `WeaveDoc.App`。Reranker 不再由脚本单独管理，而是由 `WeaveDoc.Rag` 在应用内部自动管理。

常用覆盖参数：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_RERANKER_PORT=8083 ./scripts/run_weavedoc.sh
```

也可以直接运行统一桌面程序：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

这条命令在 Windows 上同样适用；Windows 的外部工具下载路径没有被改坏，仍然保留。

## 离线评测

在聊天 `llama-server` 可访问后运行：

```bash
./scripts/eval_rag.sh
```

或者直接走统一入口的评测模式：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

## 当前统一桌面能力

- 在同一套 Avalonia 桌面壳里完成模板管理、文档转换和 RAG 问答
- 从 `doc/` 索引 `.md`、`.txt`、`.json`、`.pdf`
- 本地 embedding、检索、rerank 和带引用回答
- 支持本地 `llama-server` 或任意 OpenAI 兼容云端 API
- 通过同一个 `WeaveDoc.App` 程序执行离线 RAG 基线评测

## 测试

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## 文档索引

- 统一桌面说明：[src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- Converter 说明：[src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)
- RAG 说明：[src/WeaveDoc.Rag/README.zh-CN.md](src/WeaveDoc.Rag/README.zh-CN.md)
- RAG 架构说明（英文）：[docs/rag-architecture.md](docs/rag-architecture.md)
- RAG 架构说明（中文）：[docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- 评测基线：[docs/eval-baseline.json](docs/eval-baseline.json)
