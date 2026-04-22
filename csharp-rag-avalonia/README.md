# RagAvalonia

English | [简体中文](README.zh-CN.md)

A local Avalonia desktop RAG app.

## Overview

- Embedding and chunk indexing run locally with LLamaSharp.
- Chat generation is delegated to a local `llama-server` (OpenAI-compatible endpoint).
- Retrieval uses a staged pipeline: sparse prefilter, semantic scoring, lightweight reranking, and adjacent context windows.
- Answers are expected to cite stable source labels such as `[doc/json/a.json | section name | c3]`.

## Models

The app expects these files in the workspace-level `models/` directory:

- `models/sentence-transformers--all-MiniLM-L6-v2.gguf`

And a chat model file that will be loaded by `llama-server`, for example:

- `models/gemma-4-E2B-it.gguf`

## What it does

- Loads markdown, text, and JSON files from `doc/`
- Creates local embeddings and semantic paragraph/section chunks
- Reuses a local embedding cache at `.rag/embedding-cache.json`
- Runs hybrid recall + rerank:
  sparse prefilter = BM25 + keyword + title + direct-hit bonus + noise penalty
  semantic stage = vector similarity only on the filtered candidate pool, with fallback to full semantic scan when sparse evidence is weak
  rerank stage = coverage + neighbor support + JSON branch support + intent boost
- Expands answer context with adjacent chunks and structure-related JSON chunks
- Sends grounded prompts plus short chat history to `llama-server` for answer generation
- Retries weak generations with a stricter repair prompt and uses summary-aware fallback for overview questions
- Supports adding, deleting, and refreshing indexed `.md` / `.txt` / `.json` documents from the UI
- Flattens JSON objects and arrays into titled plain-text sections before chunking
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
- start `llama-server` on `127.0.0.1:8080` if it is not already running
- reuse the existing server if one is already healthy
- launch the Avalonia desktop app after the health check passes

Useful overrides:

```bash
LLAMA_SERVER_MODEL=./models/your-chat-model.gguf ./scripts/run_weavedoc.sh
LLAMA_SERVER_PORT=8081 ./scripts/run_weavedoc.sh
```

If you want to run the two processes manually instead, the equivalent flow is:

```bash
./llama.cpp/build/bin/llama-server \
	-m ./models/gemma-4-E2B-it.gguf \
	--host 127.0.0.1 --port 8080

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

- each question
- the generated answer
- retrieval debug information
- simple expected-keyword coverage statistics

The retrieval debug output now also shows:

- sparse prefilter candidate count
- semantic-stage candidate count
- whether the query used sparse prefilter or fell back to full semantic scoring

## Environment Variables

You can override runtime behavior with env vars:

- `LLAMA_SERVER_BASE_URL` (default: `http://127.0.0.1:8080`)
- `LLAMA_SERVER_CHAT_MODEL` (default: `local-model`)
- `LLAMA_SERVER_TEMPERATURE` (default: `0.2`)
- `LLAMA_SERVER_MAX_TOKENS` (default: `1536`)
- `RAG_TOP_K` (default: `4`)
- `RAG_CANDIDATE_POOL_SIZE` (default: `12`)
- `RAG_SPARSE_CANDIDATE_POOL_SIZE` (default: `48`)
- `RAG_CONTEXT_WINDOW_RADIUS` (default: `1`)
- `RAG_VECTOR_WEIGHT` / `RAG_BM25_WEIGHT` / `RAG_KEYWORD_WEIGHT`
- `RAG_TITLE_WEIGHT` / `RAG_JSON_STRUCTURE_WEIGHT`
- `RAG_COVERAGE_WEIGHT` / `RAG_NEIGHBOR_WEIGHT` / `RAG_JSON_BRANCH_WEIGHT`
- `RAG_CHUNK_SIZE` (default: `520`)
- `RAG_CHUNK_OVERLAP` (default: `96`)
- `RAG_MIN_COMBINED_THRESHOLD` (default: `0.18`)
- `RAG_DIRECT_KEYWORD_BONUS` (default: `0.08`)
- `RAG_FALLBACK_SENTENCE_COUNT` (default: `2`)
- `LLAMA_SERVER_TIMEOUT_SECONDS` (default: `300`)

## Notes

- The workspace root is detected by walking upward from the app output directory until a `models/` directory is found.
- If `doc/` does not exist, the app creates it automatically and starts with an empty corpus.
- The embedding cache is content-addressed by model name, chunk settings, file path, chunk index, and chunk text, so edited documents are re-embedded automatically.
- JSON files are flattened into titled plain text sections before chunking, so nested objects and arrays can still participate in retrieval.
- The answer prompt uses stable source labels instead of temporary context numbering, which makes citations more consistent across runs and context windows.
- Summary questions use intent-specific prompting and fallback extraction so “what does this paper mainly discuss” style queries stay concise but grounded.
