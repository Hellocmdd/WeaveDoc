# WeaveDoc.Rag

[English](README.md) | 简体中文

`WeaveDoc.Rag` 是 `WeaveDoc.App` AI 辅助面板背后的检索与聊天服务库。它负责语料索引、检索、重排、本地/云端聊天调用、兜底答案生成和离线评测。桌面 UI 位于 `WeaveDoc.App`。

如果只需要 Markdown 编辑或文档转换，可以先跳过本模块和模型配置。

## 主要职责

- 定位仓库工作区和标准 RAG 目录。
- 导入或删除 `doc/` 下的语料文件。
- 切分 Markdown/text/JSON 文档并维护 embedding 缓存。
- 综合向量、BM25、关键词、标题、JSON 结构、覆盖度、邻近块和分支信号检索候选块。
- 可选通过 OpenAI 兼容 reranker 端点重排候选块。
- 调用本地 OpenAI 兼容 `llama-server` 端点。
- 在设置选择云端时调用 OpenAI 兼容云端端点。
- 向应用流式返回答案。
- 生成兜底答案并规范化引用。
- 通过 `EvalRunner` 执行离线评测。

## 工作区目录

所有路径都从仓库工作区解析：

| 路径 | 用途 |
| --- | --- |
| `doc/` | 源文档/语料文档 |
| `models/` | 本地 GGUF embedding、reranker 和 chat 模型 |
| `.rag/` | embedding 缓存、导入语料、日志和 `cloud-settings.json` |
| `.eval/` | 评测报告 |

`WorkspacePaths.FindWorkspaceRoot()` 从 `AppContext.BaseDirectory` 向上查找，目录中存在 `WeaveDoc.slnx`、`src/` + `tests/`，或 `models/` 时会被视为工作区根。

## 模型文件

辅助脚本默认需要：

| 文件 | 用途 |
| --- | --- |
| `models/bge-m3.gguf` | embedding |
| `models/bge-reranker-v2-m3.gguf` | 当 `RAG_RERANKER_ENABLED=true` 时作为 reranker |
| `models/Qwen3.5-4B-Q4_K_M.gguf` 或其他非 embedding 的 `.gguf` | 供 `llama-server` 使用的聊天模型 |

覆盖聊天模型：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
```

## 启动本地栈

```bash
./scripts/run_weavedoc.sh
```

脚本会：

1. 检查 `llama.cpp/`、`models/`、`dotnet` 和 `curl`；
2. 在缺少 `llama.cpp/build/bin/llama-server` 时构建；
3. 复用已经健康的 `LLAMA_SERVER_BASE_URL`；
4. 否则启动 `llama-server`；
5. 导出 RAG 环境变量；
6. 启动 `src/WeaveDoc.App/WeaveDoc.App.csproj`。

常用覆盖参数：

```bash
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_SERVER_GPU_LAYERS=0 ./scripts/run_weavedoc.sh
RAG_RERANKER_ENABLED=false ./scripts/run_weavedoc.sh
RAG_RERANKER_BASE_URL=http://127.0.0.1:8083 ./scripts/run_weavedoc.sh
```

## 直接启动应用

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

当聊天端点已经在外部运行、使用云端设置，或希望从 UI 手动提供设置时，可以直接启动应用。

## 环境变量

核心检索选项：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `RAG_CHUNK_SIZE` | `520` | 目标分块大小 |
| `RAG_CHUNK_OVERLAP` | `96` | 分块重叠 |
| `RAG_TOP_K` | `8` | 最终上下文数量目标 |
| `RAG_CANDIDATE_POOL_SIZE` | `12` | 向量检索后的候选池 |
| `RAG_SPARSE_CANDIDATE_POOL_SIZE` | `48` | 稀疏预筛候选池 |
| `RAG_CONTEXT_WINDOW_RADIUS` | `1` | 邻近块窗口 |
| `RAG_EMBEDDING_MODEL_FILE` | `bge-m3.gguf` | `models/` 下的 embedding 模型文件名 |
| `RAG_EMBEDDING_GPU_LAYERS` | `999` | embedding GPU layer 数 |

重排选项：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `RAG_RERANKER_ENABLED` | `true` | 是否启用 reranker |
| `RAG_RERANKER_BASE_URL` | `http://127.0.0.1:8081` | reranker 端点 |
| `RAG_RERANKER_MODEL` | `bge-reranker-v2-m3` | reranker 模型名 |
| `RAG_RERANKER_TOP_N` | `12` | 重排条数 |
| `RAG_RERANKER_TIMEOUT_SECONDS` | `30` | reranker 超时 |
| `RAG_RERANKER_GPU_LAYERS` | `auto` | 脚本/提供商使用的 GPU layer 提示 |

本地聊天选项：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `LLAMA_SERVER_BASE_URL` | `http://127.0.0.1:8080` | 本地聊天端点 |
| `LLAMA_SERVER_CHAT_MODEL` | `local-model` | 发送给 `/chat/completions` 的模型名 |
| `LLAMA_SERVER_TEMPERATURE` | `0.2` | 生成温度 |
| `LLAMA_SERVER_MAX_TOKENS` | `1536` | 最大生成 token |
| `LLAMA_SERVER_TIMEOUT_SECONDS` | `300` | HTTP 超时时间 |

云端聊天选项：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `RAG_CHAT_PROVIDER` | `llama_server` | `llama_server`、`cloud` 或兼容旧值 `deepseek` |
| `CLOUD_API_KEY` | 空 | 云端 API key |
| `CLOUD_MODEL` | `deepseek-v4-pro` | 云端模型 |
| `CLOUD_BASE_URL` | `https://api.deepseek.com` | 云端接口地址 |
| `CLOUD_ENABLE_THINKING` | `false` | 启用提供商 reasoning/thinking 参数 |
| `CLOUD_REASONING_EFFORT` | `medium` | reasoning 开启后的推理强度 |

`DEEPSEEK_API_KEY`、`DEEPSEEK_MODEL`、`DEEPSEEK_BASE_URL`、`DEEPSEEK_ENABLE_THINKING`、`DEEPSEEK_REASONING_EFFORT` 会作为对应 `CLOUD_*` 值的 fallback。

UI 中修改的云端设置会保存到 `.rag/cloud-settings.json`。

## 公开接口

应用主要依赖：

- `LocalAiService`
- `RagOptions`
- `CloudApiSettings`
- `LlamaServerProcess`
- `EvalRunner`
- `ChatTurn`
- `DocumentChunk`
- `RagStreamChunk`

检索内部实现位于 `Services/Rag/`。本地 AI 服务实现拆分在 `Services/LocalAi/*.cs`。

## 离线评测

先启动或复用健康的聊天端点，然后运行：

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json
```

脚本默认把报告写入 `.eval/`。可以通过 `RAG_EVAL_REPORT_DIR` 或第二个脚本参数覆盖：

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json /path/to/report-dir
```

等价的直接入口：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

## 测试

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

当前查询理解、语料选择、检索启发式、兜底答案和流式聊天客户端测试范围见 [../../tests/WeaveDoc.Rag.Tests/README.md](../../tests/WeaveDoc.Rag.Tests/README.md)。
