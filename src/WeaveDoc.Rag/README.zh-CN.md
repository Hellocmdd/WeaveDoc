# WeaveDoc.Rag

[English](README.md) | 简体中文

`WeaveDoc.Rag` 是 `WeaveDoc.App` 中 `RAG 问答` 页签背后的检索与聊天服务模块。它不再作为单独桌面程序发布；统一桌面应用负责 UI，本模块负责服务能力。

如果你只使用 Markdown 编辑、模板管理或文档转换，可以先跳过这个模块以及对应的模型配置。

## 主要职责

- 发现 `doc/`、`models/`、`.rag/`、`.eval/` 工作区路径
- 建立本地文档索引并刷新语料
- 混合检索向量、BM25、关键词、标题和 JSON 结构信号
- 通过 OpenAI 兼容 reranker 端点进行可选重排
- 接入本地 `llama-server` 聊天
- 接入 OpenAI 兼容云端聊天配置
- 生成兜底答案并规范化引用
- 通过 `EvalRunner` 执行离线评测

## 工作区目录

所有路径都以仓库工作区为根：

| 路径 | 用途 |
| --- | --- |
| `doc/` | 待索引的源文档 |
| `models/` | embedding、reranker、chat 使用的 GGUF 模型 |
| `.rag/` | 索引文件、日志和运行缓存 |
| `.eval/` | 评测报告 |

## 模型文件

本地栈默认使用：

| 文件 | 用途 |
| --- | --- |
| `models/bge-m3.gguf` | embedding |
| `models/bge-reranker-v2-m3.gguf` | 当 `RAG_RERANKER_ENABLED=true` 时作为 reranker |
| `models/Qwen3.5-4B-Q4_K_M.gguf` 或其他非 embedding 的 `.gguf` | 供 `llama-server` 使用的聊天模型 |

可以手动覆盖聊天模型：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
```

## 启动本地栈

```bash
./scripts/run_weavedoc.sh
```

脚本会检查 `models/`，构建或复用 `llama.cpp/build/bin/llama-server`，在没有健康服务时启动 `llama-server`，导出 RAG 环境变量，并启动 `WeaveDoc.App`。

常用覆盖参数：

```bash
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
RAG_RERANKER_ENABLED=false ./scripts/run_weavedoc.sh
RAG_RERANKER_BASE_URL=http://127.0.0.1:8083 ./scripts/run_weavedoc.sh
```

## 直接启动应用

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

当聊天端点已经在外部运行、使用云端提供商，或希望手动提供设置时，可以直接启动统一桌面入口。

常见环境变量：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `LLAMA_SERVER_BASE_URL` | `http://127.0.0.1:8080` | 本地聊天端点 |
| `LLAMA_SERVER_CHAT_MODEL` | `local-model` | 发送给聊天端点的模型名 |
| `LLAMA_SERVER_TEMPERATURE` | `0.2` | 聊天生成温度 |
| `LLAMA_SERVER_MAX_TOKENS` | `1536` | 聊天最大 token |
| `RAG_EMBEDDING_MODEL_FILE` | `bge-m3.gguf` | embedding 模型文件名 |
| `RAG_RERANKER_ENABLED` | `true` | 是否启用 reranker |
| `RAG_RERANKER_BASE_URL` | `http://127.0.0.1:8081` | reranker 端点 |
| `RAG_RERANKER_MODEL` | `bge-reranker-v2-m3` | reranker 模型名 |
| `RAG_CHAT_PROVIDER` | `llama_server` | `llama_server`、`cloud` 或 `deepseek` |
| `CLOUD_API_KEY` | 空 | 云端提供商 API key |
| `CLOUD_MODEL` | `deepseek-v4-pro` | 云端模型名 |
| `CLOUD_BASE_URL` | `https://api.deepseek.com` | 云端接口地址 |
| `CLOUD_ENABLE_THINKING` | `false` | 提供商支持时启用 reasoning/thinking 模式 |
| `CLOUD_REASONING_EFFORT` | `medium` | 提供商支持时的推理强度 |

`DEEPSEEK_API_KEY`、`DEEPSEEK_MODEL`、`DEEPSEEK_BASE_URL`、`DEEPSEEK_ENABLE_THINKING`、`DEEPSEEK_REASONING_EFFORT` 也会作为对应云端变量的 fallback。

## 离线评测

先启动或复用健康的聊天端点，然后运行：

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json
```

也可以直接调用应用入口：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval /path/to/eval-baseline.json
```

脚本默认把报告写到 `.eval/`。可以通过 `RAG_EVAL_REPORT_DIR` 或第二个脚本参数覆盖：

```bash
./scripts/eval_rag.sh /path/to/eval-baseline.json /path/to/report-dir
```

## 公开类型

应用和测试主要使用：

- `LocalAiService`
- `CloudApiSettings`
- `EvalRunner`
- `ChatTurn`
- `DocumentChunk`
- `RagOptions`

检索内部实现位于 `Services/Rag/`，默认作为实现细节维护。

## 测试

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

当前测试重点覆盖查询理解、追问识别、兜底答案生成、引用规范化和检索启发式行为。
