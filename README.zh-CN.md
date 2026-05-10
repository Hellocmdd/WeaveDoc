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

- `models/bge-m3.gguf`

必须准备的 reranker 模型：

- `models/bge-reranker-v2-m3.gguf`

默认聊天模型：

- `models/Qwen3.5-4B-Q4_K_M.gguf`

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

这个脚本会在需要时自动构建并启动聊天 `llama-server`（端口 `8080`）和 BGE Reranker 服务（端口 `8081`），然后拉起桌面应用。

常用覆盖参数：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_RERANKER_PORT=8083 ./scripts/run_weavedoc.sh
```

## 当前能力

- 支持索引 `doc/` 中的 `.md`、`.txt`、`.json`，以及通过 `pdftotext` 的 `.pdf`
- JSON 会先转成带结构路径的 chunk，并为数组项生成可检索的小节标题
- 导入阶段会避免把相同文件一遍遍复制进知识库
- 检索采用**模型驱动管线**（`refactored` 模式）：纯向量余弦 Top-N 候选召回 → BGE-Reranker 交叉编码器作为唯一排序权威 → 意图感知上下文窗口组装
- 对”《标题》这篇论文/文档...”这类问题支持文档定向 scope 硬过滤
- 回答引用使用稳定标签（`[文件 | 章节 | cN]`），而不是临时 `[1][2]`
- `procedure` / `compare` / `composition` / `explain` 等意图各有专用的本地回退答案生成器
- 对”论文/文档主要讲什么”这类问题提供 summary-aware 回退答案
- 支持可选的云端 Chat Provider（DeepSeek API），可替代本地 `llama-server`
- 提供离线基线评测入口（关键词覆盖 + 结构化校验 + 引用精确率/召回率 + scope 准确度 + evidence kind 准确度）

### 最新基线结果（2026-05-07）

| 指标 | 数值 |
|------|------|
| Case pass count | 18 / 20 (90.0%) |
| Keyword coverage | 75 / 97 (77.3%) |
| Top chunk signal coverage | 43 / 64 (67.2%) |
| Context signal coverage | 63 / 64 (98.4%) |
| 平均 citation precision | 72.7% |
| 平均 citation recall | 100.0% |
| Scope accuracy | 20/20 (100.0%) |
| Evidence kind accuracy | 20/20 (100.0%) |

## SQLite 存储同步

旧版 `rag_store.db` 可从已转换的 markdown 和 JSON 语料刷新：

```bash
./scripts/sync_markdown_json_sqlite.sh
```

## 离线评测

在 `llama-server` 已经可用的前提下运行：

```bash
./scripts/eval_rag.sh
```

说明：

- 退出码 `0`：全部用例通过
- 退出码 `2`：至少一个用例未通过
- 当前评测不仅统计关键词命中，还会对部分基线用例执行结构化检查

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
