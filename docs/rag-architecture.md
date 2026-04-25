# WeaveDoc RAG Architecture

[简体中文](rag-architecture.zh-CN.md)

This document summarizes the current RAG design implemented in `csharp-rag-avalonia`, mainly covering:

- `csharp-rag-avalonia/Services/LocalAiService.cs`
- `csharp-rag-avalonia/Services/LlamaServerChatClient.cs`
- `csharp-rag-avalonia/Services/RagOptions.cs`
- `csharp-rag-avalonia/Services/EvalRunner.cs`

## 1. System Overview

The current system is a local-first desktop RAG pipeline with seven layers:

1. document ingestion and normalization
2. chunking and metadata generation
3. embedding and local cache
4. two-stage retrieval
5. lightweight reranking
6. answer generation through `llama-server`
7. answer validation, fallback, and offline evaluation

High-level flow:

```text
user question
  -> AskAsync
    -> NormalizeQuestionForRetrieval
    -> BuildQueryProfile
    -> FindRelevantChunksAsync
      -> sparse prefilter
      -> semantic scoring on the filtered candidate pool
      -> RerankCandidates
      -> BuildContextWindow
    -> BuildPrompt
    -> llama-server /v1/chat/completions
    -> answer quality checks
      -> repair prompt retry
      -> summary-aware fallback
      -> extractive fallback
```

## 2. Document Ingestion

### 2.1 Supported formats

The indexer currently supports the following files under `doc/`:

- `.md`
- `.txt`
- `.json`
- `.pdf` (through system `pdftotext` extraction)

For PDFs, the app shells out to `pdftotext` and feeds extracted UTF-8 text into the same chunking pipeline.

### 2.2 Import policy

`AddDocumentAsync` uses the following behavior:

- if the file is already inside `doc/`, the app refreshes the index instead of copying it again
- if the knowledge base already contains a file with identical content, the app reuses the existing file instead of creating a duplicate
- only truly new files are copied into `doc/`

This avoids repeated “same document with another timestamp suffix” artifacts.

### 2.3 JSON normalization

JSON files are not chunked as raw JSON text. They are first converted into structured plain text by `ReadDocumentContentAsync`:

- title-like fields such as `title`, `name`, `documentTitle`, and `document_name` are used as the document title
- objects are expanded into section headings plus `field: value` lines
- arrays are expanded into section blocks or bullet-style lists, and object items receive `item N + preview` section labels
- the resulting text is then fed into the normal text/Markdown chunking pipeline

This makes JSON searchable through title and section signals instead of treating the whole file as a noisy blob.

It also avoids weak citations such as bare `[1]` / `[2]` labels for deep array items.

## 3. Chunking and Metadata

### 3.1 Chunking strategy

`SplitIntoChunks` preserves section structure as much as possible:

- split by double newlines first
- treat short title-like paragraphs or Markdown headings as section boundaries
- accumulate normal paragraphs up to `ChunkSize`
- send oversized paragraphs to `SplitLargeParagraph`
- keep `ChunkOverlap` between adjacent chunks

### 3.2 Chunk metadata

Each `DocumentChunk` includes:

- `FilePath`
- `Index`
- `DocumentTitle`
- `SectionTitle`
- `StructurePath`
- `ContentKind`
- `Text`

These fields are used by retrieval and the stable citation system. `StructurePath` and `ContentKind` are also reused by JSON-aware reranking, context expansion, and summary fallback selection.

## 4. Embedding and Cache

### 4.1 Embedding model

The embedding side runs locally with a GGUF sentence embedding model:

- default model: `sentence-transformers--all-MiniLM-L6-v2.gguf`
- runtime: `LLamaSharp`

Before embedding, `PrepareTextForEmbedding` trims long inputs so they stay within the embedding context budget.

### 4.2 Local cache

Embeddings are cached in:

- `.rag/embedding-cache.json`

The cache key includes:

- embedding model name
- chunk configuration
- file path
- chunk index
- chunk text

As a result, changed documents or changed chunk settings naturally invalidate old cached embeddings.

### 4.3 Sparse index data

Besides embeddings, the service also maintains:

- per-chunk `TokenFrequency`
- per-chunk `TokenCount`
- global `_documentFrequency`
- global `_avgDocumentLength`

These are required for BM25 and other sparse retrieval signals.

## 5. Query Understanding

### 5.1 Question normalization

`NormalizeQuestionForRetrieval` removes greeting/noise prefixes and can expand follow-up questions such as “be more detailed” or “continue” by borrowing the previous user turn.

### 5.2 Query token extraction

`ExtractQueryTokens` extracts:

- ASCII alphanumeric tokens
- continuous Chinese spans
- 2-gram sub-tokens for longer Chinese spans

### 5.3 QueryProfile

`BuildQueryProfile` generates:

- `FocusTerms`
- `Intent`
- `WantsDetailedAnswer`

`FocusTerms` now come from the current user question itself, while follow-up expansion still borrows prior turns through `BuildFocusTermSourceText` when needed.

Supported intent types:

- `compare`
- `explain`
- `procedure`
- `usage`
- `summary`
- `definition`
- `general`

The `summary` intent now explicitly covers prompts such as “主要讲什么 / 主要内容 / 概述 / 总结”.

These values influence reranking, prompt structure, and fallback answer selection.

## 6. Two-Stage Retrieval

`FindRelevantChunksAsync` now uses a two-stage retrieval design.

### 6.1 Stage 1: sparse prefilter

Every chunk first receives sparse-only signals:

- `BM25`
- `KeywordScore`
- `TitleScore`
- `JsonStructureScore`
- `HasDirectKeywordHit`
- `NoisePenalty`

This produces:

```text
sparseScore =
  bm25 * Bm25Weight
  + keyword * KeywordWeight
  + title * TitleWeight
  + jsonStructure * JsonStructureWeight
  + directHitBonus
  - noisePenalty
```

Chunks are sorted by this score to form a larger prefilter candidate pool.

### 6.2 Stage 2: semantic scoring

Only the filtered candidate pool is sent to the embedding similarity stage:

```text
finalCandidateScore =
  semantic * VectorWeight
  + sparseScore
```

This avoids running cosine similarity over every chunk in the corpus.

### 6.3 Semantic fallback

If sparse evidence is too weak for a query, such as:

- no meaningful keyword hits
- no title hits
- near-zero BM25 evidence

the system falls back to full semantic scoring so paraphrased or fuzzy questions are not over-pruned by the sparse stage.

### 6.4 Key retrieval knobs

- `RAG_CANDIDATE_POOL_SIZE`
  final candidate count sent into reranking
- `RAG_SPARSE_CANDIDATE_POOL_SIZE`
  sparse prefilter size before semantic scoring

## 7. Lightweight Reranking

`RerankCandidates` adds rule-based features on top of the candidate score.

### 7.1 Coverage

`ComputeCoverageScore` measures how completely the candidate covers the focus terms.

### 7.2 Neighbor support

`ComputeNeighborSupportScore` checks whether adjacent chunks from the same file also support the same topic.

### 7.3 JSON branch support

`ComputeJsonBranchSupportScore` checks whether sibling / parent / child chunks from the same JSON structure branch also support the focus terms.

This is mainly useful when:

- the best hit is a single array item and nearby structured evidence should remain attached
- the best hit is a deep node and the answer needs parent/child details from the same branch

### 7.4 Intent boost

`ComputeIntentBoost` adds a small extra score when the chunk contains intent-specific signals:

- `usage` prefers “作用 / 用途 / 功能 / 用于”
- `procedure` prefers “步骤 / 流程 / 首先 / 然后”
- `explain` prefers “原因 / 机制 / 因为 / 由于”

### 7.5 Rerank formula

```text
rerankScore =
  candidateScore
  + coverage * CoverageWeight
  + neighbor * NeighborWeight
  + jsonBranch * JsonBranchWeight
  + intentBoost
```

The final top `TopK` chunks become the main answer evidence.

## 8. Context Window Expansion

`BuildContextWindow` expands the top-ranked chunks with nearby chunks. For JSON hits it also pulls in supporting chunks from the same structure branch:

- the radius is controlled by `ContextWindowRadius`
- JSON branch supplements are capped at `max(1, ContextWindowRadius + 1)` per seed chunk
- each chunk appears at most once
- output is ordered by `FilePath + Index`

This helps preserve local context and reduces hard boundary loss caused by chunking.

## 9. Answer Generation

`LlamaServerChatClient` talks to local `llama-server` through the OpenAI-compatible API:

- health check: `/health`
- generation endpoint: `/v1/chat/completions`

### 9.1 Prompt structure

`BuildPrompt` and `BuildRepairPrompt` explicitly include:

- intent description
- prioritized evidence list
- context body
- answer rules
- an intent-aware cross-source synthesis rule

### 9.2 Stable citations

The system no longer relies on temporary prompt-local numbering like `[1] [2]`. Instead, it uses stable source labels such as:

```text
[doc/json/a.json | section name | c3]
```

Each label includes:

- relative file path
- `SectionTitle`
- chunk index

This makes citations far more stable even when context ordering changes.

## 10. Answer Validation and Fallback

### 10.1 Bad-answer detection

`IsOffTopicOrMalformedAnswer` checks for:

- empty answers
- missing citations
- English-only drift
- template-like off-topic output
- short answers to detailed questions
- generic answers for usage/function questions

For very short summary-style queries, the overlap check is intentionally relaxed so valid high-level summaries are not rejected just because the user only asked “what does this article mainly discuss”.

### 10.2 Repair retry

If the first answer is weak, the service retries with a stricter repair prompt.

### 10.3 Extractive fallback

If repair still fails, the service falls back to intent-specific builders:

- `BuildPaperSummaryFallbackAnswer`
- `BuildSummaryFallbackAnswer`
- `BuildExtractiveFallbackAnswer`
- `BuildUsageFallbackAnswer`

Summary fallback prefers sentences from summary / overview / architecture / method / result sections, filters out parameter-heavy fragments, and still emits stable citations instead of temporary numbering.

## 11. Observability

`LastRetrievalDebug` now reports:

- original vs retrieval question
- detected intent and focus terms
- sparse prefilter candidate count
- semantic-stage candidate count
- whether sparse prefilter was used or semantic fallback was triggered
- per-hit score breakdown

This is useful for tuning and regression analysis.

## 12. Offline Evaluation

The repository includes a minimal offline evaluation workflow:

- evaluation entry point: `EvalRunner`
- baseline file: `docs/eval-baseline.json`
- runner script: `scripts/eval_rag.sh`

The evaluation mode:

- runs a batch of questions
- prints the generated answers
- prints retrieval debug output
- computes expected-keyword coverage statistics
- runs per-case structural checks for selected baseline case IDs

This is still not a full semantic automatic grader, but it is already enough for day-to-day regression checks and before/after comparisons when tuning retrieval behavior.

## 13. Main Parameters

Important current parameters include:

- `RAG_CHUNK_SIZE = 520`
- `RAG_CHUNK_OVERLAP = 96`
- `RAG_TOP_K = 4`
- `RAG_CANDIDATE_POOL_SIZE = 12`
- `RAG_SPARSE_CANDIDATE_POOL_SIZE = 48`
- `RAG_CONTEXT_WINDOW_RADIUS = 1`
- `RAG_MIN_COMBINED_THRESHOLD = 0.18`
- `RAG_VECTOR_WEIGHT = 0.38`
- `RAG_BM25_WEIGHT = 0.20`
- `RAG_KEYWORD_WEIGHT = 0.18`
- `RAG_TITLE_WEIGHT = 0.12`
- `RAG_JSON_STRUCTURE_WEIGHT = 0.10`
- `RAG_COVERAGE_WEIGHT = 0.08`
- `RAG_NEIGHBOR_WEIGHT = 0.08`
- `RAG_JSON_BRANCH_WEIGHT = 0.06`
- `RAG_DIRECT_KEYWORD_BONUS = 0.08`
- `RAG_FALLBACK_SENTENCE_COUNT = 2`
- `LLAMA_SERVER_TEMPERATURE = 0.2`
- `LLAMA_SERVER_MAX_TOKENS = 1536`
- `LLAMA_SERVER_TIMEOUT_SECONDS = 300`

The default parameter style is intentionally conservative: stable, debuggable, and low-hallucination rather than aggressively long or overly permissive generation.

## 14. Strengths and Limitations

### Strengths

- no external vector database is required
- supports `.md`, `.txt`, `.json`, and `.pdf`
- JSON is normalized into a retrievable text structure
- retrieval already uses a two-stage design instead of full semantic scanning only
- document-title-aware retrieval can prioritize chunks from a user-requested source file
- citations are stable and easier to trace
- repair retry, summary-aware fallback, extractive fallback, and offline evaluation are already built in

### Limitations

- reranking is still rule-based rather than learned
- PDF ingestion currently depends on external `pdftotext` availability and text extraction quality
- sparse prefilter improves performance but is not yet an ANN vector index
- stable citations currently resolve to “file + section + chunk”, not page numbers or finer structural locations
- offline evaluation is mainly keyword + rule checks, not a stronger semantic automatic grading model

## 15. One-Sentence Summary

The current WeaveDoc RAG design normalizes local documents into section-aware chunks, retrieves evidence through sparse prefiltering plus localized semantic scoring and rule-based reranking, sends grounded context to local `llama-server`, and uses stable citations, repair retry, summary-aware fallback, and offline evaluation to keep answers more reliable.
