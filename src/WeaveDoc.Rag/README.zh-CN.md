# WeaveDoc.Rag

`WeaveDoc.Rag` 是供 `WeaveDoc.App` 使用的 RAG 服务类库，不再单独作为一个 Avalonia 桌面程序存在。

## 主要职责

- 以 `models/` 为锚点的工作区路径发现
- 本地 embedding、召回、rerank 和答案拼装
- 本地 `llama-server` 聊天接入
- OpenAI 兼容云端 API 的配置与聊天接入
- 语料刷新、文档导入/删除辅助能力
- 通过 `EvalRunner` 执行离线基线评测

## 主要公开类型

供应用或测试直接使用的公开类型包括：

- `LocalAiService`
- `CloudApiSettings`
- `EvalRunner`
- `ChatTurn`
- `RagOptions`

检索细节大多仍然保留在 `Services/Rag/` 下作为内部实现。

## 在仓库中的位置

统一后的主结构为：

- UI 外壳：`src/WeaveDoc.App`
- Converter 核心：`src/WeaveDoc.Converter`
- RAG 核心：`src/WeaveDoc.Rag`

工作区级数据目录继续保留在仓库根部：

- `doc/`
- `models/`
- `.rag/`
- `.eval/`

## 运行与评测

当前通过统一入口使用这套类库：

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

也可以配合脚本：

```bash
./scripts/run_weavedoc.sh
./scripts/eval_rag.sh
```

## 模型要求

工作区级 `models/` 目录下需要准备：

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`
- 一个聊天模型，例如 `models/Qwen3.5-4B-Q4_K_M.gguf`

## 备注

- 聊天 `llama-server` 仍由 `scripts/run_weavedoc.sh` 在需要时拉起
- reranker 生命周期已经内聚到 `WeaveDoc.Rag` 内部，不再依赖外部脚本
- 离线评测通过 `WeaveDoc.App/Program.cs` 的 `--eval` 分支执行，不会初始化 Avalonia
