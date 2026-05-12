# WeaveDoc

[English](README.md) | 简体中文

WeaveDoc 是一个本地优先的 Avalonia 桌面 RAG 应用，依赖 `llama.cpp`、本地 embedding、rerank 和带引用回答生成。

## 仓库结构

- `src/WeaveDoc.App/`：Avalonia 入口、主窗口和面向 UI 的 RAG ViewModel
- `src/WeaveDoc.Rag/`：RAG 模型、服务、检索链路和离线评测运行器
- `tests/WeaveDoc.Rag.Tests/`：RAG 单元测试
- `WeaveDoc.slnx`：解决方案入口
- `scripts/`：运行、评测、更新和 `llama.cpp` 辅助脚本
- `docs/`：架构说明和评测基线
- `doc/`：本地索引语料
- `models/`：本地 GGUF 模型
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

```bash
dotnet build WeaveDoc.slnx
```

## 模型准备

请在 `models/` 下准备：

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`
- 一个聊天模型，例如 `models/Qwen3.5-4B-Q4_K_M.gguf`

## 运行

```bash
./scripts/run_weavedoc.sh
```

或者直接启动桌面程序：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

## 离线评测

```bash
./scripts/eval_rag.sh
```

或者：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

## 测试

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

## 文档

- 应用说明：[src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- 中文应用说明：[src/WeaveDoc.App/README.zh-CN.md](src/WeaveDoc.App/README.zh-CN.md)
- RAG 架构说明（英文）：[docs/rag-architecture.md](docs/rag-architecture.md)
- RAG 架构说明（中文）：[docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- 评测基线：[docs/eval-baseline.json](docs/eval-baseline.json)
