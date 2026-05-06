# RagAvalonia

English | [简体中文](README.zh-CN.md)

A local Avalonia desktop RAG app using a model-driven pipeline.

## Overview

- Embedding and chunk indexing run locally with LLamaSharp (`bge-m3.gguf`).
- Chat generation is delegated to a local `llama-server` (OpenAI-compatible endpoint) or optionally the DeepSeek cloud API.
- Retrieval uses the **`refactored` model-driven pipeline**: pure vector cosine Top-N candidate retrieval, followed by BGE-Reranker cross-encoder as the sole ranking authority, then intent-specific context assembly.
- Answers cite stable source labels such as `[doc/json/a.json | section name | c3]`.

## Models

The app expects these files in the workspace-level `models/` directory:

- `models/bge-m3.gguf`
- `models/bge-reranker-v2-m3.gguf`

And a chat model file that will be loaded by `llama-server`, for example:

- `models/Qwen3.5-4B-Q4_K_M.gguf`

## What it does

- Loads `.md`, `.txt`, `.json`, and `.pdf` files from `doc/`
- Creates local embeddings (`bge-m3.gguf`) and section-aware chunks; reuses cache at `.rag/embedding-cache.json`
- **Model-driven candidate retrieval**: pure vector cosine similarity over all in-scope chunks, Top-N (default 50–100)
- **BGE-Reranker as sole ranking authority**: sends the candidate pool to `bge-reranker-v2-m3` via `/v1/rerank`; the reranker score determines the final ranked order
- **Intent-specific context assembly**: selects and orders context chunks according to detected query intent (metadata slot prioritization, procedure structure preference, definition section-title matching, compare subject coverage, summary lead/support split)
- **Document-scope hard filter**: for title-named queries like `《...》这篇文档`, restricts candidates to the matched file before reranking
- Sends grounded prompts plus short conversation history to `llama-server` (or DeepSeek API) for answer generation
- Strong system prompt enforces grounding, terminology preservation, and citation discipline
- Retries weak generations with a stricter repair prompt; falls back to intent-specific extractive answer builders
- Supports adding, deleting, and refreshing indexed documents from the UI
- Flattens JSON objects and arrays into titled plain-text sections before chunking; generates stable `StructurePath` labels for deep array items
- Avoids duplicating files when the imported document is already inside `doc/` or has identical content to an indexed file

## Run

From the workspace root:

```bash
./scripts/run_weavedoc.sh
```

If `llama.cpp/` is not present yet, initialize the submodule first:

```bash
git submodule update --init --recursive
```

The launcher script will:

- build `llama.cpp` first if `llama-server` is missing
- start chat `llama-server` on `127.0.0.1:8080` and reranker `llama-server` on `127.0.0.1:8081` if they are not already running
- reuse the existing server if one is already healthy
- launch the Avalonia desktop app after the health check passes

Useful overrides:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8082 ./scripts/run_weavedoc.sh
LLAMA_RERANKER_PORT=8083 ./scripts/run_weavedoc.sh
```

If you want to run the two processes manually instead, the equivalent flow is:

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

## Evaluation

Run the offline baseline with:

```bash
./scripts/eval_rag.sh
```

The evaluation script expects a reachable `llama-server` first. If the server is not up yet, start it with `./scripts/run_weavedoc.sh` or launch `llama-server` manually.

Or provide a custom baseline file:

```bash
dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

The baseline runner prints:

- each question, generated answer, and retrieval debug output
- keyword coverage, top-chunk signal coverage, context signal coverage
- citation precision and recall per case
- scope accuracy and evidence kind accuracy

The retrieval debug output shows (in `refactored` pipeline mode):

- number of chunks entering vector scoring
- learned reranker status (`global:ok (N/N)` or failure reason)
- per-hit reranker score breakdown
- context window chunk count

## Environment Variables

You can override runtime behavior with env vars:

**Pipeline mode:**

- `RAG_PIPELINE_MODE` (allowed: `legacy`, `simple`, `refactored`; env default: `legacy`; actual runtime default: `refactored`)

**Local llama-server (default chat provider):**

- `LLAMA_SERVER_BASE_URL` (default: `http://127.0.0.1:8080`)
- `LLAMA_SERVER_CHAT_MODEL` (default: `local-model`)
- `LLAMA_SERVER_TEMPERATURE` (default: `0.2`)
- `LLAMA_SERVER_MAX_TOKENS` (default: `1536`)
- `LLAMA_SERVER_TIMEOUT_SECONDS` (default: `300`)

**DeepSeek cloud provider (optional alternative):**

- `RAG_CHAT_PROVIDER` (default: `llama_server`; set to `deepseek` to use DeepSeek API)
- `DEEPSEEK_API_KEY`
- `DEEPSEEK_MODEL` (default: `deepseek-v4-pro`)
- `DEEPSEEK_BASE_URL` (default: `https://api.deepseek.com`)
- `DEEPSEEK_ENABLE_THINKING` (default: `false`)
- `DEEPSEEK_REASONING_EFFORT` (default: `medium`)

**Embedding and reranker:**

- `RAG_EMBEDDING_MODEL_FILE` (default: `bge-m3.gguf`)
- `RAG_EMBEDDING_GPU_LAYERS` (default: `999`; requires a GPU-capable LLamaSharp backend)
- `RAG_RERANKER_ENABLED` (default: `true`)
- `RAG_RERANKER_BASE_URL` (default: `http://127.0.0.1:8081`)
- `RAG_RERANKER_MODEL` (default: `bge-reranker-v2-m3`)
- `RAG_RERANKER_TOP_N` (default: `12`)
- `RAG_RERANKER_TIMEOUT_SECONDS` (default: `30`)

**Retrieval knobs:**

- `RAG_TOP_K` (default: `8`)
- `RAG_CANDIDATE_POOL_SIZE` (default: `12`)
- `RAG_SPARSE_CANDIDATE_POOL_SIZE` (default: `48`; lower bound for vector candidate limit in `refactored` mode)
- `RAG_CONTEXT_WINDOW_RADIUS` (default: `1`)
- `RAG_MIN_COMBINED_THRESHOLD` (default: `0.18`)
- `RAG_VECTOR_WEIGHT` (default: `0.38`)
- `RAG_BM25_WEIGHT` / `RAG_KEYWORD_WEIGHT` / `RAG_TITLE_WEIGHT` / `RAG_JSON_STRUCTURE_WEIGHT` (legacy mode)
- `RAG_COVERAGE_WEIGHT` / `RAG_NEIGHBOR_WEIGHT` / `RAG_JSON_BRANCH_WEIGHT` (legacy mode)
- `RAG_DIRECT_KEYWORD_BONUS` (default: `0.08`)

**Chunking:**

- `RAG_CHUNK_SIZE` (default: `520`)
- `RAG_CHUNK_OVERLAP` (default: `96`)
- `RAG_FALLBACK_SENTENCE_COUNT` (default: `2`)

## Notes

- The workspace root is detected by walking upward from the app output directory until a `models/` directory is found.
- If `doc/` does not exist, the app creates it automatically and starts with an empty corpus.
- The embedding cache is content-addressed by model name, chunk settings, file path, chunk index, and chunk text, so edited documents are re-embedded automatically.
- JSON files are flattened into titled plain text sections before chunking, so nested objects and arrays can still participate in retrieval.
- The answer prompt uses stable source labels instead of temporary context numbering, which makes citations more consistent across runs and context windows.
- Summary questions use intent-specific prompting and fallback extraction so “what does this paper mainly discuss” style queries stay concise but grounded.
