# WeaveDoc RAG Architecture

[简体中文](rag-architecture.zh-CN.md)

This document summarizes the current RAG design implemented in `csharp-rag-avalonia`. The pipeline has undergone a major V2 refactoring (completed 2026-05-02) and now follows a **model-driven** architecture that delegates retrieval ranking authority to the BGE-Reranker cross-encoder instead of hand-crafted heuristic rules.

Primary source files:

- `csharp-rag-avalonia/Services/LocalAiService.cs`
- `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs`
- `csharp-rag-avalonia/Services/Rag/ChunkRanker.cs`
- `csharp-rag-avalonia/Services/Rag/EvidenceSelector.cs`
- `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs`
- `csharp-rag-avalonia/Services/Rag/QueryUnderstandingService.cs`
- `csharp-rag-avalonia/Services/Rag/RagContracts.cs`
- `csharp-rag-avalonia/Services/LlamaServerChatClient.cs`
- `csharp-rag-avalonia/Services/RagOptions.cs`
- `csharp-rag-avalonia/Services/EvalRunner.cs`

## 1. System Overview

The current system is a local-first desktop RAG pipeline structured in seven layers:

1. document ingestion and normalization
2. chunking and metadata generation
3. embedding and local cache
4. semantic candidate retrieval (pure vector cosine, Top-N)
5. BGE-Reranker cross-encoder ranking (authoritative)
6. intent-aware context assembly
7. answer generation, quality validation, fallback, and offline evaluation

High-level flow (`refactored` pipeline mode):

```text
user question
  -> AskAsync
    -> NormalizeQuestionForRetrieval
    -> BuildQueryProfile          (intent, focus terms, doc scope)
    -> FindRelevantChunksAsync
      -> (optional) hard FilePath scope filter
      -> vector cosine similarity over all scoped chunks
      -> Top-N candidates (default cap 50–100)
      -> TryRerankWithLearnedModelAsync  (BGE-Reranker via /v1/rerank)
      -> post-rerank intent boost nudge (definition / compare / procedure / summary)
      -> BuildAnswerContextChunks        (intent-specific slot assembly)
    -> BuildPrompt
    -> llama-server /v1/chat/completions
    -> IsOffTopicOrMalformedAnswer
      -> repair prompt retry
      -> intent-specific fallback answer builders
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

This avoids repeated "same document with another timestamp suffix" artifacts.

### 2.3 JSON normalization

JSON files are not chunked as raw JSON text. They are first converted into structured plain text by `ReadDocumentContentAsync`:

- title-like fields such as `title`, `name`, `documentTitle`, and `document_name` are used as the document title
- objects are expanded into section headings plus `field: value` lines
- arrays are expanded into section blocks or bullet-style lists, and object items receive `item N + preview` section labels
- the resulting text is then fed into the normal text/Markdown chunking pipeline

This makes JSON searchable through title and section signals instead of treating the whole file as a noisy blob. Deep array items get stable `StructurePath` labels and are citable as `[file | section > sub | cN]`.

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

`StructurePath` and `ContentKind` are used throughout: by the reranker document builder, by intent-specific context assembly, and by the stable citation system.

## 4. Embedding and Cache

### 4.1 Embedding model

The embedding side runs locally with a GGUF sentence embedding model:

- default model: `bge-m3.gguf`
- runtime: `LLamaSharp`
- pooling: model default from GGUF metadata

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

These are available for BM25 and other sparse retrieval signals, though the default `refactored` pipeline does not use BM25 in the primary candidate retrieval path.

## 5. Query Understanding

### 5.1 Question normalization

`NormalizeQuestionForRetrieval` removes greeting/noise prefixes and can expand follow-up questions such as "be more detailed" or "continue". For follow-up questions, `ShouldAugmentWithPreviousQuestion` → `IsFollowUpExpansionRequest` detects the pattern (requires < 2 meaningful focus terms in the current question), then `ExtractAnchorTerms` extracts anchor terms from the previous assistant answer — new concepts introduced by the assistant that weren't in the original user question (up to 4). These are appended as "重点关注：..." to the augmented question, keeping follow-ups anchored on the established conversation topic.

### 5.2 Query token extraction

`ExtractQueryTokens` extracts:

- ASCII alphanumeric tokens
- continuous Chinese spans
- 2-gram sub-tokens for longer Chinese spans

### 5.3 QueryProfile

`BuildQueryProfile` (via `QueryUnderstandingService`) generates:

- `FocusTerms` — up to 8 meaningful tokens from the current question
- `Intent` — document-classified query intent
- `WantsDetailedAnswer`
- `IsFollowUpExpansion` — whether this is a follow-up expansion on the previous topic (e.g. "be more detailed")
- `RequestedDocumentTitle` — for document-scoped queries like `《...》这篇论文`
- `CompareSubjects` — for `compare` intent
- metadata preference flags (`RequestsEnglishMetadata`, `AvoidsEnglishMetadata`, etc.)

Supported intent types:

- `compare`
- `explain`
- `procedure`
- `usage`
- `summary`
- `definition`
- `module_list`
- `module_implementation`
- `composition`
- `metadata`
- `general`

The `summary` intent explicitly covers prompts such as "主要讲什么 / 主要内容 / 概述 / 总结".

These values influence the reranker query suffix, context window assembly, prompt structure, and fallback answer selection.

## 6. Candidate Retrieval (Model-Driven)

`FindRelevantChunksAsync` now implements a lean, model-driven retrieval design. The heuristic-heavy two-stage sparse+semantic pipeline has been replaced in the `refactored` pipeline mode.

### 6.1 Hard scope filter

If the question names a specific document (via title detection or active-document context), the candidate set is first restricted to chunks from the matching file(s). This is a hard `FilePath` equality filter, not a score bonus.

### 6.2 Vector-only candidate retrieval

The primary retrieval step scores all in-scope chunks by **cosine similarity** between the question embedding and each chunk embedding:

```text
semanticScore = CosineSimilarity(questionVector, chunkEmbedding)
```

Chunks are sorted descending by `semanticScore`. The top `vectorCandidateLimit` candidates proceed to reranking. `vectorCandidateLimit` is clamped to `[SparseCandidatePoolSize, 100]` (default 50–100).

BM25, keyword scoring, and title scoring are **not** applied to this candidate selection step in the `refactored` mode. Their computed values are stored for debug output but do not influence ranking.

### 6.3 Key retrieval knobs

- `RAG_SPARSE_CANDIDATE_POOL_SIZE`
  lower bound for the vector candidate limit (default 48; effective range after clamping: 50–100)
- `RAG_TOP_K`
  final context chunk count (default 8; bumped to 12 for `WantsDetailedAnswer` queries)

## 7. BGE-Reranker Ranking (Authoritative)

`TryRerankWithLearnedModelAsync` sends the full vector candidate pool to the BGE-Reranker cross-encoder via llama.cpp's `/v1/rerank` endpoint. The reranker score becomes the **sole ordering criterion** for the ranked list.

### 7.1 Reranker query construction

`BuildRerankerQuery` appends an intent-specific focus hint to the raw question before sending to the reranker:

| Intent | Appended hint |
|--------|--------------|
| `procedure` / `implementation` | 关注：精确技术参数、单一功能点的直接描述、具体数值/公式/代码逻辑 |
| `module_implementation` | 关注：该模块的技术参数、工作原理、接口与控制策略的直接描述 |
| `composition` / `module_list` / `list` | 关注：关键模块名称、硬件组成、技术栈、并列项的直接列举 |
| `definition` | 关注：术语定义、概念解释、对象身份与职责的直接描述 |
| other | (no suffix) |

### 7.2 Reranker document format

Each chunk sent to the reranker is formatted as:

```text
file: <FilePath>
section: <StructurePath or SectionTitle>
kind: <ContentKind>
<chunk retrieval text>
```

### 7.3 Ranking output

The reranker returns a relevance score per candidate. Chunks are sorted in descending score order. If two chunks tie on reranker score, the one with a higher original vector score wins, then the lower original rank.

### 7.4 Post-rerank intent boost

After the reranker orders the list, a lightweight **intent boost nudge** is applied to the reranked scores for `definition`, `compare`, `procedure`, and `summary` intents. This is additive and does not override the reranker — it only resolves near-tie ordering within intent-specific signals:

- `sectionTitle` match to intent term: +0.9
- `structurePath` match: +0.7
- retrieval text match: +0.35
- `body` ContentKind: +0.35
- well-structured source (JSON / abstract / conclusion): +0.2
- `compare` subject match: +0.8
- `procedure` JSON source: +1.4, unstructured Markdown body: −2.1

The `procedure` intent has an additional specialized boost via `ComputeProcedureTopChunkQualityBoost`:

- `abstract` / `summary` / `intro` ContentKind: −1.5 (prevent summary blocks from crowding out technical detail)
- `conclusion` ContentKind: −1.0
- section title / structure path containing `workflow`, `overview`, `概述`, or `总体`: −0.95 (prevent generic main-loop descriptions from taking slots)
- section title / structure path matching `ProcedureSectionBoostTerms` (14 domain terms: 控制, 调节, 补偿, 策略, 机制, 阶段, 执行, 算法, 传感器, 驱动, 检测, 采集, 设定, 阈值): +1.15
- 20 procedure signal terms (输入, 前提, 处理, 执行, 输出...) hit count × 0.22
- Signal counting executes after section boost/penalty to avoid being suppressed by penalties

### 7.5 Reranker fallback

If the reranker server is disabled, unreachable, times out, or returns an unusable response, retrieval falls back to the vector-sorted candidate order (no rule-based rerankScore is applied in `refactored` mode).

## 8. Context Window Assembly

`BuildAnswerContextChunks` assembles the final context from the reranked list. Assembly is **intent-specific**:

| Intent | Assembly strategy |
|--------|-----------------|
| `metadata` | Priority slots: englishTitle → englishAbstract → englishKeywords → abstract; then fill to limit, deduplicate English fields |
| `procedure` | **Three-pass assembly**: Pass 1a — prefer chunks whose SectionTitle matches query focus terms (FocusTerms), capped at `limit/3` (max 4); Pass 1b — fill remaining Pass 1 slots with `ProcedureSectionBoostTerms` matches (14 generic procedure terms); Pass 2 — add well-structured chunks (JSON / abstract / conclusion); Pass 3 — fill to limit from filtered ranked list. Pass 1a priority ensures question-specific anchor terms aren't crowded out by generic matches |
| `definition` | Prefer chunks whose `SectionTitle` contains focus terms, then fill |
| `compare` | Prefer chunks matching both compare subjects, then chunks matching any compare term, then fill |
| `summary` | 2 summary-lead chunks (abstract / overview / conclusion) + support chunks by priority score, then fill |
| other | Top-N from filtered ranked list |

The limit is 8 chunks by default and 12 for `WantsDetailedAnswer` queries.

An additional `ShouldKeepChunkForAnswer` filter removes chunks that should not appear in final context for the given intent (e.g. English-only chunks for Chinese summary questions).

> **Note:** Context window radius expansion (`BuildContextWindow`) and JSON branch supplement logic are available but not invoked in the current `refactored` pipeline mode. The intent-specific assembly in `BuildAnswerContextChunks` effectively replaces them.

## 9. Answer Generation

`LlamaServerChatClient` talks to local `llama-server` through the OpenAI-compatible API:

- health check: `/health`
- generation endpoint: `/v1/chat/completions`

### 9.1 System prompt

A strong system prompt (`RagSystemPrompt`) is embedded in every request. It enforces:

1. answer only from the provided context — no external knowledge
2. synthesize across multiple context blocks, do not collapse to "not found"
3. preserve specific parameters, terminology, module names, and technical vocabulary verbatim
4. structure answers by intent: conclusion-first, then details; steps for procedure; dimensions for compare
5. one stable citation label per claim — must copy the `来源标签:` exactly, no short `[c3]` references
6. language must match the question language

### 9.2 Prompt structure

`BuildPrompt` explicitly includes:

- intent description
- answer structure rule
- cross-source synthesis rule
- domain guidance
- output requirement reminder
- intent-specific instruction block (metadata / procedure / compare / document-scope constraint)
- conversation history (up to 6 recent turns, for pronoun resolution only)
- prioritized evidence list (ranked chunk citations)
- context body (all context chunks with stable labels)

### 9.3 Stable citations

The system does not use temporary prompt-local numbering like `[1] [2]`. Instead it uses stable source labels:

```text
[doc/json/a.json | section name | c3]
```

Each label includes:

- relative file path
- `SectionTitle`
- chunk index

Citations are stable even when context ordering changes between calls.

## 10. Answer Validation and Fallback

### 10.1 Bad-answer detection

`IsOffTopicOrMalformedAnswer` checks for:

- empty answers
- missing citations
- English-only drift
- template-like off-topic output
- short answers to detailed questions
- generic answers for usage/function questions

For very short summary-style queries, the overlap check is intentionally relaxed so valid high-level summaries are not rejected.

When the question concerns a topic not covered by the current documents, the system no longer returns a generic "I don't know." Instead, `BuildUnknownAnswer` extracts subject terms from the question (e.g. "Bluetooth Mesh networking") and returns "未找到关于{subject}的相关内容" with a default citation, making negative answers more informative.

### 10.2 Per-line citation selection

After answer generation, instead of appending the same default citation to every line, `SelectCitationForAnswerLine` scores each answer line against all available chunks (term overlap in section/text + procedure structure signals) and selects the most relevant citation per line. This makes citations more precisely traceable to specific portions of the answer.

### 10.3 Repair retry

If the first answer is weak, the service retries with a stricter repair prompt. The repair prompt adds a hard "must not say 我不知道 or 未覆盖" rule and repeats the citation correction instruction.

### 10.4 Extractive fallback

If repair still fails, the service falls back to intent-specific builders:

- `BuildPaperSummaryFallbackAnswer`
- `BuildSummaryFallbackAnswer`
- `BuildExtractiveFallbackAnswer`
- `BuildUsageFallbackAnswer`
- **`BuildLocalProcedureFallbackAnswer`** — builds a four-part answer (input/precondition → judgment/processing → action/execution → result/effect) by selecting representative chunks per part via `FindRepresentativeChunk`; chunks matching focus terms are prioritized over those matching only generic signals
- **`BuildLocalCompareFallbackAnswer`** — selects representative chunks per compare subject
- **`BuildLocalCompositionFallbackAnswer`** — constructs structured composition list fallback
- **`BuildLocalExplainFallbackAnswer`** — builds a three-part answer (reason → mechanism → impact)

All local fallbacks use the `FindRepresentativeChunk` (QueryProfile overload), which adds focus-term scores atop signal-term scores and sorts by `FocusScore > 0` first — ensuring chunks completely unrelated to the question focus (e.g. a data-acquisition section for an irrigation-control question) cannot take representative slots.

Summary fallback prefers sentences from summary / overview / architecture / method / result sections, filters out parameter-heavy fragments, and still emits stable citations.

## 11. Observability

`LastRetrievalDebug` reports:

- original vs retrieval question
- detected intent, focus terms, target document
- number of chunks entering vector scoring
- whether sparse prefilter was used (always `false` / full semantic in `refactored` mode)
- learned reranker status: `global:ok (N/N)` or failure reason
- per-hit score breakdown (reranker score as `score`, semantic cosine as `semantic`, all other fields zero in `refactored` mode)
- context window chunk count

This makes it straightforward to inspect reranker behavior and pinpoint retrieval regressions.

## 12. Offline Evaluation

The repository includes a minimal offline evaluation workflow:

- evaluation entry point: `EvalRunner`
- baseline file: `docs/eval-baseline.json`
- runner script: `scripts/eval_rag.sh`

The evaluator measures:

- **Case pass count** — fraction of cases meeting required keyword + structural checks
- **Keyword coverage** — fraction of expected keywords present in the answer
- **Top chunk signal coverage** — fraction of expected signal chunks appearing in the reranked top list
- **Context signal coverage** — fraction of expected signal chunks appearing in the final context window
- **Citation precision / recall** — against expected citation labels per case
- **Scope accuracy** — whether the answer stays in the correct document scope
- **Evidence kind accuracy** — whether the correct content types were retrieved
- **Slot coverage / Sufficiency accuracy** — legacy compatibility metrics

The baseline schema expresses answer expectations, retrieval expectations, and citation expectations separately, so regressions can be localized to generation, retrieval, or grounding.

### Latest benchmark (2026-05-07)

| Metric | Value |
|--------|-------|
| Case pass count | 18 / 20 (90.0%) |
| Keyword coverage | 75 / 97 (77.3%) |
| Top chunk signal coverage | 43 / 64 (67.2%) |
| Context signal coverage | 63 / 64 (98.4%) |
| Avg citation precision | 72.7% |
| Avg citation recall | 100.0% |
| Scope accuracy | 20/20 (100.0%) |
| Evidence kind accuracy | 20/20 (100.0%) |
| Sufficiency accuracy | 19/20 (95.0%) |
| Citation from EvidenceSet | 20/20 (100.0%) |
| Source format accuracy | 20/20 (100.0%) |

**Key observations (2026-05-07):**

- Case pass improved from 15/20 to 18/20. R0-1~3 fixes (Pass 1 cap + anti-meta-description + AnswerComposer instruction softening) resolved the regressions on `stm32-control-prefixed` and `stm32-control`.
- Citation recall reached 100% — all expected citations are present in answers.
- Context signal coverage (98.4%) remains excellent.
- Top chunk signal coverage improved from 57.8% to 67.2% — the Pass 1 focus-term-first strategy improved chunk ordering for procedure-type queries.
- 2 remaining failures: `follow-up-detail` (follow-up expansion chunk selection drifts to peripheral communication-interface sections) and `stm32-compare-fuzzy-pid` (compare instruction bloat causes meta-description to crowd out technical content); both are addressed in R0-5 pending verification.

## 13. Main Parameters

Important current parameters (defaults in `RagOptions.LoadFromEnvironment`):

- `RAG_CHUNK_SIZE = 520`
- `RAG_CHUNK_OVERLAP = 96`
- `RAG_TOP_K = 8`  ← effective default for `refactored` mode; env var default is 8
- `RAG_CANDIDATE_POOL_SIZE = 12`  (pool size multiplier base; not the hard vector cap in `refactored` mode)
- `RAG_SPARSE_CANDIDATE_POOL_SIZE = 48`  (lower bound for vector candidate limit; clamped to 50–100)
- `RAG_CONTEXT_WINDOW_RADIUS = 1`
- `RAG_MIN_COMBINED_THRESHOLD = 0.18`
- `RAG_VECTOR_WEIGHT = 0.38`
- `RAG_BM25_WEIGHT = 0.20`  (maintained for legacy mode; not used in `refactored` ranking)
- `RAG_KEYWORD_WEIGHT = 0.18`  (same — legacy mode only)
- `RAG_TITLE_WEIGHT = 0.12`
- `RAG_JSON_STRUCTURE_WEIGHT = 0.10`
- `RAG_COVERAGE_WEIGHT = 0.08`
- `RAG_NEIGHBOR_WEIGHT = 0.08`
- `RAG_JSON_BRANCH_WEIGHT = 0.06`
- `RAG_DIRECT_KEYWORD_BONUS = 0.08`
- `RAG_FALLBACK_SENTENCE_COUNT = 2`
- `RAG_PIPELINE_MODE = refactored`  ← actual runtime mode (overrides env default `legacy`)
- `RAG_RERANKER_ENABLED = true`
- `RAG_RERANKER_BASE_URL = http://127.0.0.1:8081`
- `RAG_RERANKER_MODEL = bge-reranker-v2-m3`
- `RAG_RERANKER_TOP_N = 12`
- `RAG_RERANKER_TIMEOUT_SECONDS = 30`
- `LLAMA_SERVER_TEMPERATURE = 0.2`
- `LLAMA_SERVER_MAX_TOKENS = 1536`
- `LLAMA_SERVER_TIMEOUT_SECONDS = 300`
- `RAG_CHAT_PROVIDER = llama_server`  (also supports `deepseek` for cloud fallback)

The default parameter style is intentionally conservative: stable, debuggable, and low-hallucination rather than aggressively long or overly permissive generation.

## 14. Pipeline Mode Reference

`RAG_PIPELINE_MODE` accepts three values:

| Mode | Description |
|------|-------------|
| `refactored` | **Default runtime mode.** Pure vector cosine Top-N candidate retrieval → BGE-Reranker as sole ordering authority → intent-specific context assembly. No BM25 in primary path. |
| `legacy` | Original heuristic pipeline: BM25 sparse prefilter + semantic scoring + rule-based rerank formula. Kept for regression comparison. |
| `simple` | Minimal mode retained for debugging. |

## 15. Strengths and Limitations

### Strengths

- no external vector database is required
- supports `.md`, `.txt`, `.json`, and `.pdf`
- JSON is normalized into a retrievable text structure with stable section-level citations
- BGE-Reranker cross-encoder acts as the authoritative ranker — no hand-crafted score formula in the primary path
- document-title-aware hard-scope filter isolates queries to the requested source file
- citations are stable and easier to trace
- intent-specific context assembly ensures the right chunk types are prioritized per query type
- strong system prompt enforces grounding, terminology preservation, and citation discipline
- repair retry, summary-aware fallback, and offline evaluation with multi-dimensional metrics are built in
- cloud chat provider (DeepSeek) is supported as an alternative to local `llama-server`

### Limitations

- learned reranking requires a separate local reranker server; the pipeline falls back to vector-sorted order when unavailable
- PDF ingestion currently depends on external `pdftotext` availability and text extraction quality
- top chunk signal coverage (67.2%) still has room for improvement — generic terms in `ProcedureSectionBoostTerms` (e.g. "采集"/acquisition) can sometimes elevate data-acquisition/communication-interface sections over technically core sections like PID control
- `ProcedureSectionBoostTerms` currently consists of 14 static Chinese terms biased toward embedded/IoT domains; cross-domain coverage may degrade for non-engineering documents (dynamic corpus extraction planned in `task-generalize-procedure-terms.md`)
- stable citations currently resolve to "file + section + chunk", not page numbers or finer structural locations
- offline evaluation is mainly keyword + rule checks, not a stronger semantic automatic grading model
- `RAG_PIPELINE_MODE` env var default is `legacy` but the actual runtime uses `refactored` — this inconsistency should be resolved
- follow-up expansion chunk selection still has optimization room: for very short follow-ups (e.g. "more detail"), even with anchor term extraction, Pass 1 generic-term matching in context assembly can pull peripheral sections into the context window

## 16. One-Sentence Summary

The current WeaveDoc RAG design normalizes local documents into section-aware chunks, retrieves evidence through pure vector cosine candidate retrieval and BGE-Reranker cross-encoder ranking, assembles intent-specific context windows, sends grounded prompts to local `llama-server`, and uses stable citations, a strong system prompt, repair retry, and offline evaluation to keep answers reliable and traceable.
