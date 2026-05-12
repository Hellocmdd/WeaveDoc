# WeaveDoc.Rag

`WeaveDoc.Rag` is the RAG service library used by `WeaveDoc.App`. It no longer ships as its own Avalonia application.

## Responsibilities

- workspace path discovery rooted at `models/`
- local embedding, retrieval, reranking, and answer composition
- local `llama-server` chat integration
- OpenAI-compatible cloud API settings and chat integration
- corpus refresh, document import/delete support, and evaluation helpers
- offline baseline execution through `EvalRunner`

## Main Surface Area

Public types intended for app or test usage include:

- `LocalAiService`
- `CloudApiSettings`
- `EvalRunner`
- `ChatTurn`
- `RagOptions`

Most retrieval internals remain implementation details under `Services/Rag/`.

## Repository Position

The unified runtime structure is:

- UI shell: `src/WeaveDoc.App`
- converter core: `src/WeaveDoc.Converter`
- RAG core: `src/WeaveDoc.Rag`

Workspace data remains at the repository root:

- `doc/`
- `models/`
- `.rag/`
- `.eval/`

## Run And Eval

The library is exercised through the unified app entry:

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

Or via helper scripts:

```bash
./scripts/run_weavedoc.sh
./scripts/eval_rag.sh
```

## Models

Expected workspace-level model files:

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`
- a chat GGUF such as `models/Qwen3.5-4B-Q4_K_M.gguf`

## Notes

- chat `llama-server` is still launched by `scripts/run_weavedoc.sh` when needed
- the reranker lifecycle is managed inside `WeaveDoc.Rag`, not by external scripts
- offline evaluation runs through `WeaveDoc.App/Program.cs`, which short-circuits Avalonia startup when `--eval` is supplied
