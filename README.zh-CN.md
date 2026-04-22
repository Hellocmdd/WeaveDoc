# WeaveDoc

[English](README.md) | 简体中文

WeaveDoc 是一个本地优先的桌面 RAG 项目，使用 Avalonia 构建前端界面，使用本地 embedding 与检索管线完成知识检索，并通过 `llama.cpp` 提供本地聊天生成能力。

当前仓库由三层组成：

- `llama.cpp`：作为 Git submodule 接入，用于本地模型服务
- `csharp-rag-avalonia`：桌面应用与 RAG 主体实现
- `scripts/`：更新、构建、运行、评测等辅助脚本

## 仓库结构

- `csharp-rag-avalonia/`：Avalonia 桌面应用、RAG 检索与提示词拼接
- `llama.cpp/`：上游 `ggml-org/llama.cpp` 子模块
- `scripts/`：构建、运行、调试、更新、评测脚本
- `docs/`：架构说明与评测基线等文档
- `doc/`：本地知识库文档目录
- `models/`：本地 GGUF 模型目录

## 首次克隆

建议直接连同 submodule 一起克隆：

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
```

如果已经克隆过主仓库但还没有 submodule：

```bash
git submodule update --init --recursive
```

## 模型准备

请把 GGUF 模型放到仓库根目录的 `models/` 下。

必须准备的 embedding 模型：

- `models/sentence-transformers--all-MiniLM-L6-v2.gguf`

聊天模型示例：

- `models/gemma-4-E2B-it.gguf`

## 一键更新

仓库提供了统一更新脚本：

```bash
./scripts/update_all.sh
```

默认行为：

- 拉取主仓库当前分支
- 同步并初始化 submodule
- 将 `llama.cpp` 更新到 `.gitmodules` 跟踪分支的最新提交
- 重新构建 `llama.cpp`
- 重新构建 Avalonia 应用

如果你只想使用主仓库当前提交固定住的 submodule 版本：

```bash
./scripts/update_all.sh --pinned
```

## 运行

模型准备好之后，在仓库根目录运行：

```bash
./scripts/run_weavedoc.sh
```

这个脚本会在需要时自动构建并启动 `llama-server`，然后拉起桌面应用。

常用覆盖参数：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8081 ./scripts/run_weavedoc.sh
```

## 当前能力

- 支持索引 `doc/` 中的 `.md`、`.txt`、`.json`
- JSON 会先转成带结构路径的 chunk，并为数组项生成可检索的小节标题
- 导入阶段会避免把相同文件一遍遍复制进知识库
- 检索采用“稀疏预筛 + 局部语义打分 + 结构感知重排”
- 命中 JSON 时会补充同一结构分支上的上下文，避免父子节点信息脱节
- 回答引用使用稳定标签，而不是临时 `[1][2]`
- 对“论文/文档主要讲什么”这类问题提供 summary-aware 回退答案
- 提供离线基线评测入口

## 离线评测

在 `llama-server` 已经可用的前提下运行：

```bash
./scripts/eval_rag.sh
```

也可以直接通过 CLI 模式运行：

```bash
dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

## 文档

- RAG 架构说明（英文）：[docs/rag-architecture.md](docs/rag-architecture.md)
- RAG 架构说明（中文）：[docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- 评测基线： [docs/eval-baseline.json](docs/eval-baseline.json)
- 应用说明（英文）：[csharp-rag-avalonia/README.md](csharp-rag-avalonia/README.md)
- 应用说明（中文）：[csharp-rag-avalonia/README.zh-CN.md](csharp-rag-avalonia/README.zh-CN.md)
