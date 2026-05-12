# WeaveDoc.Rag

[English](README.md) | 简体中文

`WeaveDoc.Rag` 是 `WeaveDoc.App` 中 `RAG 问答` 页签背后的服务模块。根目录 README 现在只保留仓库总览；本地 AI 的模型准备、运行方式和离线评测说明统一收敛到这里。

如果你只使用文档转换和模板管理功能，可以先忽略这个模块以及对应的模型配置。

## 主要职责

- 以 `models/` 为锚点的工作区路径发现
- 本地 embedding、召回、rerank 和答案拼装
- 本地 `llama-server` 聊天接入
- OpenAI 兼容云端 API 的配置与聊天接入
- 语料刷新、文档导入/删除辅助能力
- 通过 `EvalRunner` 执行离线基线评测

## 工作区要求

这套模块依赖的工作区数据目录都保留在仓库根目录：

- `doc/`：待索引的源文档
- `models/`：embedding、reranker、chat 使用的 GGUF 模型
- `.rag/`：本地索引、日志和运行缓存
- `.eval/`：评测输出

## 模型要求

- `models/bge-m3.gguf`：embedding 模型
- `models/bge-reranker-v2-m3.gguf`：当 `RAG_RERANKER_ENABLED=true` 时使用的 reranker 模型
- 一个聊天模型 GGUF；`scripts/run_weavedoc.sh` 会优先查找 `models/Qwen3.5-4B-Q4_K_M.gguf`，如果没有，再回退到目录里第一个非 embedding 的 `.gguf` 文件

## 启动本地 RAG 栈

```bash
./scripts/run_weavedoc.sh
```

这个脚本会负责：

- 检查 `models/` 和 `llama.cpp` 子模块
- 在缺少二进制时先构建 `llama-server`
- 当当前没有健康的服务时自动启动 `llama-server`
- 导出桌面程序需要的 RAG 环境变量
- 最后启动 `WeaveDoc.App`

常用覆盖参数：

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
RAG_RERANKER_ENABLED=false ./scripts/run_weavedoc.sh
RAG_RERANKER_BASE_URL=http://127.0.0.1:8083 ./scripts/run_weavedoc.sh
```

## 直接启动应用

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

当聊天服务已经在外部运行，或者你想手动指定设置时，可以直接走统一桌面入口。常见环境变量包括：

- `LLAMA_SERVER_BASE_URL`
- `RAG_EMBEDDING_MODEL_FILE`
- `RAG_RERANKER_ENABLED`
- `RAG_RERANKER_BASE_URL`
- `RAG_RERANKER_MODEL`
- `RAG_CHAT_PROVIDER`，可选 `llama_server`、`cloud`、`deepseek`
- `CLOUD_API_KEY`、`CLOUD_MODEL`、`CLOUD_BASE_URL`
- `CLOUD_ENABLE_THINKING`、`CLOUD_REASONING_EFFORT`

## 离线评测

在聊天 `llama-server` 已经可访问后，可以运行：

```bash
./scripts/eval_rag.sh
```

也可以直接复用统一入口：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

`eval_rag.sh` 依赖一个健康的聊天服务；`--eval` 模式会走 `EvalRunner`，并且不会启动 Avalonia UI。

## 主要公开类型

供应用或测试直接使用的公开类型包括：

- `LocalAiService`
- `CloudApiSettings`
- `EvalRunner`
- `ChatTurn`
- `RagOptions`

检索细节大多仍然保留在 `Services/Rag/` 下作为内部实现。

## 备注

- 这个模块已经不再单独作为桌面应用发布
- 聊天 `llama-server` 可以交给 `scripts/run_weavedoc.sh` 管理
- reranker 生命周期已经内聚到 `WeaveDoc.Rag` 内部，不再依赖单独的启动脚本
