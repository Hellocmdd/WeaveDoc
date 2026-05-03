# RagAvalonia

[English](README.md) | 简体中文

本项目是一个本地 Avalonia 桌面 RAG 应用。

## 概览

- Embedding 与切块索引在本地通过 LLamaSharp 完成
- 聊天生成通过本地 `llama-server` 提供的 OpenAI 兼容接口完成
- 检索采用“稀疏预筛 + 语义打分 + BGE 学习式重排 + 规则补充排序 + 邻接上下文窗口”
- 回答中的引用使用稳定来源标签，例如 `[doc/json/a.json | 某章节 | c3]`

## 模型

应用会从工作区根目录的 `models/` 中寻找模型文件。

必须的 embedding 与 reranker 模型：

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`

默认聊天模型：

- `models/Qwen3.5-4B-Q4_K_M.gguf`

## 功能

- 从 `doc/` 加载 `.md`、`.txt`、`.json`
- 对文档做本地 embedding 与语义分块
- 复用 `.rag/embedding-cache.json` 中的 embedding 缓存
- 检索链路包括：
  稀疏预筛 = BM25 + keyword + title + direct-hit bonus + noise penalty
  语义阶段 = 只对候选池做向量相似度计算，稀疏信号过弱时可回退全量语义
  重排阶段 = coverage + neighbor support + JSON branch support + intent boost，之后在 reranker 服务可用时交给 BGE-Reranker 精排
- 会把相邻 chunk 和同一 JSON 结构分支上的相关 chunk 一并补进上下文
- 通过 `llama-server` 生成带引用的回答
- 首答不可靠时会走更严格的 repair prompt；总结类问题还有专门的 summary 回退
- UI 支持添加、删除、刷新 `.md` / `.txt` / `.json`
- JSON 会先展平为带标题层级的文本再切块
- 导入时会避免重复复制相同内容的文件

## 运行

在仓库根目录运行：

```bash
./scripts/run_weavedoc.sh
```

如果 `llama.cpp/` 子模块还没有初始化：

```bash
git submodule update --init --recursive
```

启动脚本会：

- 在缺少 `llama-server` 时先构建 `llama.cpp`
- 如果本地服务不可用，就启动 8080 端口的聊天 `llama-server` 和 8081 端口的 reranker `llama-server`
- 若已有健康服务则直接复用
- 健康检查通过后启动 Avalonia 桌面应用

常用覆盖参数：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_RERANKER_PORT=8083 ./scripts/run_weavedoc.sh
```

如果你想手动运行两个进程，也可以：

```bash
./llama.cpp/build/bin/llama-server \
  -m ./models/Qwen3.5-4B-Q4_K_M.gguf \
  --host 127.0.0.1 --port 8080 \
  --gpu-layers auto

./llama.cpp/build/bin/llama-server \
  -m ./models/bge-reranker-v2-m3.gguf \
  --host 127.0.0.1 --port 8081 \
  --embedding --pooling rank --reranking \
  --gpu-layers auto

dotnet restore csharp-rag-avalonia/RagAvalonia.csproj
dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj
```

## 离线评测

运行离线基线评测：

```bash
./scripts/eval_rag.sh
```

评测脚本要求本地已有可访问的 `llama-server`。如果服务没启动，脚本会先给出明确提示。

也可以直接使用 CLI 模式：

```bash
dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

评测输出包括：

- 每个问题
- 生成回答
- 检索调试信息
- 基于 `expectedKeywords` 的简单覆盖率统计

检索调试还会额外输出：

- 稀疏预筛候选数
- 进入语义阶段的候选数
- 当前问题是否使用了稀疏预筛，还是回退到全量语义

## 环境变量

- `LLAMA_SERVER_BASE_URL` 默认 `http://127.0.0.1:8080`
- `LLAMA_SERVER_CHAT_MODEL` 默认 `local-model`
- `LLAMA_SERVER_TEMPERATURE` 默认 `0.2`
- `LLAMA_SERVER_MAX_TOKENS` 默认 `1536`
- `RAG_EMBEDDING_MODEL_FILE` 默认 `bge-m3.gguf`
- `RAG_EMBEDDING_GPU_LAYERS` 默认 `999`，需要 GPU 版 LLamaSharp backend 才会真正走 GPU
- `RAG_RERANKER_ENABLED` 默认 `true`
- `RAG_RERANKER_BASE_URL` 默认 `http://127.0.0.1:8081`
- `RAG_RERANKER_MODEL` 默认 `bge-reranker-v2-m3`
- `RAG_PIPELINE_MODE` 默认 `legacy`，可选 `legacy`、`simple`、`refactored`
- `RAG_TOP_K` 默认 `4`
- `RAG_CANDIDATE_POOL_SIZE` 默认 `12`
- `RAG_SPARSE_CANDIDATE_POOL_SIZE` 默认 `48`
- `RAG_CONTEXT_WINDOW_RADIUS` 默认 `1`
- `RAG_VECTOR_WEIGHT` / `RAG_BM25_WEIGHT` / `RAG_KEYWORD_WEIGHT`
- `RAG_TITLE_WEIGHT` / `RAG_JSON_STRUCTURE_WEIGHT`
- `RAG_COVERAGE_WEIGHT` / `RAG_NEIGHBOR_WEIGHT` / `RAG_JSON_BRANCH_WEIGHT`
- `RAG_CHUNK_SIZE` 默认 `520`
- `RAG_CHUNK_OVERLAP` 默认 `96`
- `RAG_MIN_COMBINED_THRESHOLD` 默认 `0.18`
- `RAG_DIRECT_KEYWORD_BONUS` 默认 `0.08`
- `RAG_FALLBACK_SENTENCE_COUNT` 默认 `2`
- `LLAMA_SERVER_TIMEOUT_SECONDS` 默认 `300`

## 备注

- 程序会从输出目录向上查找包含 `models/` 的工作区根目录
- 如果 `doc/` 不存在，会自动创建空知识库
- embedding 缓存按模型名、切块参数、文件路径、chunk 序号与文本内容联合建键
- JSON 会先展平为带标题的纯文本，因此嵌套对象和数组也能参与检索
- 回答提示词使用稳定来源标签而不是临时上下文编号，使引用在不同运行之间更稳定
- 总结类问题会走专门的提示词结构和回退逻辑，避免“主要讲什么”只返回碎片化短句
