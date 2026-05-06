# WeaveDoc RAG 算法架构

[English](rag-architecture.md)

本文总结 `csharp-rag-avalonia` 中 RAG 部分的当前实现。流水线已完成 V2 大规模重构（完成于 2026-05-02），现采用**模型驱动**架构——将召回排序权威完全下放给 BGE-Reranker 交叉编码器，而不再依赖手工调权的启发式规则。

主要对应源文件：

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

## 1. 总体设计

当前实现是一套本地优先的桌面 RAG 流水线，主要分为 7 层：

1. 文档导入与规范化
2. 文档切块与元信息生成
3. Embedding 与本地缓存
4. 纯语义向量候选召回（Top-N）
5. BGE-Reranker 交叉编码器精排（权威排序）
6. 意图感知的上下文窗口组装
7. 回答生成、质量校验、回退与离线评测

当前 `refactored` 管线的主调用链：

```text
用户问题
  -> AskAsync
    -> NormalizeQuestionForRetrieval
    -> BuildQueryProfile          （意图、焦点词、文档 scope）
    -> FindRelevantChunksAsync
      -> （可选）FilePath 硬过滤
      -> 向量余弦相似度打分
      -> Top-N 候选（默认上限 50–100）
      -> TryRerankWithLearnedModelAsync（BGE-Reranker via /v1/rerank）
      -> 重排后意图微调（definition / compare / procedure / summary）
      -> BuildAnswerContextChunks  （意图感知槽位组装）
    -> BuildPrompt
    -> llama-server /v1/chat/completions
    -> IsOffTopicOrMalformedAnswer
      -> 修复提示词重试
      -> 意图特定回退答案生成器
```

## 2. 文档导入层

### 2.1 支持格式

当前支持从 `doc/` 目录索引以下文件：

- `.md`
- `.txt`
- `.json`
- `.pdf`（通过系统 `pdftotext` 提取文本）

对于 PDF，应用会调用系统 `pdftotext`，将抽取出的 UTF-8 文本送入同一套切块与检索流程。

### 2.2 导入策略

`AddDocumentAsync` 的策略如下：

- 如果文件已经位于 `doc/` 目录内，只刷新索引，不重复复制
- 如果知识库里已经存在内容完全相同的文件，也不重复复制
- 只有真正的新文件才会复制进入 `doc/`

这样做是为了避免导入阶段不断产生同名副本或时间戳副本。

### 2.3 JSON 规范化

JSON 文档不会直接按原始 JSON 字符串切块，而是先通过 `ReadDocumentContentAsync` 转成结构化文本：

- 自动提取 `title`、`name`、`documentTitle`、`document_name` 等字段作为文档标题
- object 转成分节标题 + `字段: 值`
- array 转成按项展开的小节或列表，并给对象数组项生成 `第N项 + 摘要` 形式的小节标题
- 最后形成一份带标题层级的纯文本，再进入现有 Markdown/文本切块流程

好处在于：嵌套 JSON 不再退化成一整段难检索的原始字符串，深层数组项也能获得 `StructurePath` 标签，并以 `[文件 | 章节 > 子章节 | cN]` 格式被引用。

## 3. 文档切块与元信息

### 3.1 切块策略

`SplitIntoChunks` 会尽量保留章节结构：

- 先按双换行切段
- 将短标题或 Markdown 标题视为 section 边界
- 正常段落在 `ChunkSize` 限制内累计
- 超长段落交给 `SplitLargeParagraph`
- 相邻切块通过 `ChunkOverlap` 保留重叠内容

### 3.2 Chunk 元信息

每个 `DocumentChunk` 至少包含：

- `FilePath`
- `Index`
- `DocumentTitle`
- `SectionTitle`
- `StructurePath`
- `ContentKind`
- `Text`

`StructurePath` 和 `ContentKind` 会在 reranker 文档格式化、意图感知上下文组装以及稳定引用标签中被复用。

## 4. Embedding 与缓存

### 4.1 Embedding 模型

系统使用本地 GGUF sentence embedding 模型：

- 默认模型：`bge-m3.gguf`
- 推理库：`LLamaSharp`
- pooling：使用 GGUF 元数据中的模型默认值

在真正计算 embedding 之前，会先通过 `PrepareTextForEmbedding` 做长度控制，避免超过上下文预算。

### 4.2 本地缓存

缓存文件位置：

- `.rag/embedding-cache.json`

缓存键由以下信息共同决定：

- embedding 模型名
- chunk 参数
- 文件路径
- chunk 序号
- chunk 文本内容

因此，当文档内容或 chunk 设置变化时，旧缓存会自然失效。

### 4.3 稀疏索引数据

除了 embedding，本地还维护：

- 每个 chunk 的 `TokenFrequency`
- 每个 chunk 的 `TokenCount`
- 全局 `_documentFrequency`
- 全局 `_avgDocumentLength`

这些数据为 BM25 等稀疏信号提供支撑。在默认 `refactored` 管线中，这些字段在候选召回阶段不参与排序（仅用于调试输出）；旧版 `legacy` 管线仍使用它们。

## 5. 查询理解层

### 5.1 问题归一化

`NormalizeQuestionForRetrieval` 负责：

- 去掉寒暄和边界噪声
- 对"详细一点 / 继续 / 展开说"等追问尝试补全上一轮用户问题

### 5.2 Query token 抽取

`ExtractQueryTokens` 会提取：

- 英文数字 token
- 中文连续片段
- 较长中文片段的 2-gram 子词

### 5.3 QueryProfile

`BuildQueryProfile`（通过 `QueryUnderstandingService`）会生成：

- `FocusTerms` — 从当前问题提取的最多 8 个有效 token
- `Intent` — 检测出的问题意图类型
- `WantsDetailedAnswer`
- `RequestedDocumentTitle` — 用于"《...》这篇文档"类定向问题
- `CompareSubjects` — 用于对比意图
- 元数据偏好标志（`RequestsEnglishMetadata`、`AvoidsEnglishMetadata` 等）

支持的 `Intent` 类型：

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

其中 `summary` 明确覆盖"主要讲什么 / 主要内容 / 概述 / 总结"这类问题。

这些信息同时影响 reranker 查询后缀、上下文窗口组装、回答结构和回退路径。

## 6. 候选召回层（模型驱动）

`FindRelevantChunksAsync` 在 `refactored` 模式下已换成极简模型驱动设计，废弃了原来复杂的启发式双阶段稀疏+语义架构。

### 6.1 文档 scope 硬过滤

如果问题明确命名了某个文档（通过标题检测或活动文档 scope 判断），候选池会先硬过滤到匹配文件的 chunk，这是等值 `FilePath` 过滤，不是打分加成。

### 6.2 纯向量候选召回

主召回步骤对所有 in-scope chunk 计算**余弦相似度**：

```text
semanticScore = CosineSimilarity(questionVector, chunkEmbedding)
```

按 `semanticScore` 降序排列，取前 `vectorCandidateLimit` 个候选送入重排。`vectorCandidateLimit` 被限制在 `[SparseCandidatePoolSize, 100]` 之间（默认 50–100）。

在 `refactored` 模式下，**BM25、关键词打分和标题打分均不参与此阶段**。它们的计算值在调试输出中可见，但不影响排序。

### 6.3 关键参数

- `RAG_SPARSE_CANDIDATE_POOL_SIZE`
  向量候选数量的下限（默认 48；截断后实际范围 50–100）
- `RAG_TOP_K`
  最终上下文 chunk 数（默认 8；`WantsDetailedAnswer` 时放大到 12）

## 7. BGE-Reranker 精排（权威排序）

`TryRerankWithLearnedModelAsync` 将全量向量候选池发送至 BGE-Reranker 交叉编码器，通过 llama.cpp 的 `/v1/rerank` 端点获取精排分数。Reranker 分数是**最终排序的唯一依据**。

### 7.1 Reranker 查询构造

`BuildRerankerQuery` 在原始问题后追加意图特定焦点提示：

| 意图 | 追加提示 |
|------|---------|
| `procedure` / `implementation` | 关注：精确技术参数、单一功能点的直接描述、具体数值/公式/代码逻辑 |
| `module_implementation` | 关注：该模块的技术参数、工作原理、接口与控制策略的直接描述 |
| `composition` / `module_list` / `list` | 关注：关键模块名称、硬件组成、技术栈、并列项的直接列举 |
| `definition` | 关注：术语定义、概念解释、对象身份与职责的直接描述 |
| 其他 | （无追加） |

### 7.2 Reranker 文档格式

每个发给 reranker 的 chunk 格式如下：

```text
file: <FilePath>
section: <StructurePath 或 SectionTitle>
kind: <ContentKind>
<chunk 检索文本>
```

### 7.3 排序输出

Reranker 返回每个候选的相关度分数，按分数降序排列。分数相同时，优先取向量分数更高的，再取原始排名更低的。

### 7.4 重排后意图微调

在 reranker 完成排序后，对 `definition`、`compare`、`procedure`、`summary` 意图应用一个轻量级**意图分数微调**（不是规则重排，而是基于 reranker 分数的叠加微调）：

- `sectionTitle` 命中意图词：+0.9
- `structurePath` 命中：+0.7
- 检索文本命中：+0.35
- `body` ContentKind：+0.35
- 结构良好的来源（JSON / abstract / conclusion）：+0.2
- `compare` 双主体同时命中：+0.8
- `procedure` 来源为 JSON：+1.4；无结构 Markdown body：−2.1

### 7.5 Reranker 不可用回退

如果 reranker 服务禁用、不可达、超时或返回无效响应，召回回退到向量分数排序（`refactored` 模式下不再应用规则 rerankScore 公式）。

## 8. 上下文窗口组装

`BuildAnswerContextChunks` 从重排列表中组装最终上下文，采用**意图特定的槽位策略**：

| 意图 | 组装策略 |
|------|---------|
| `metadata` | 优先槽位：englishTitle → englishAbstract → englishKeywords → abstract；再填充到上限，同 ContentKind 的英文字段去重 |
| `procedure` | 优先结构良好的块（JSON / abstract / conclusion），再从过滤列表填充 |
| `definition` | 优先 `SectionTitle` 包含焦点词的块，再填充 |
| `compare` | 优先同时命中两个对比主体的块，再按对比词命中，再填充 |
| `summary` | 最多 2 个摘要/概述/结论主块 + 支撑块（按优先级打分），再填充 |
| 其他 | 过滤后重排列表前 N 个 |

默认上限 8 个，`WantsDetailedAnswer` 查询放大到 12 个。

`ShouldKeepChunkForAnswer` 过滤器会针对当前意图排除不合适的 chunk（如中文总结问题中的纯英文字段块）。

> **注意：** 上下文窗口邻近扩展（`BuildContextWindow`）和 JSON 结构分支补充逻辑在代码中保留，但当前 `refactored` 管线不调用它们。意图感知的 `BuildAnswerContextChunks` 替代了原有的邻近扩展逻辑。

## 9. 生成层

`LlamaServerChatClient` 通过本地 `llama-server` 的 OpenAI 兼容接口完成生成：

- 健康检查：`/health`
- 生成接口：`/v1/chat/completions`

### 9.1 系统提示词

每次请求都嵌入了一份强约束系统提示词（`RagSystemPrompt`），核心规则：

1. 只能依据提供的上下文回答，不得使用外部知识
2. 必须跨多个上下文块综合作答，不得轻易回答"未找到"
3. 保留上下文中的具体参数、术语、模块名和技术词汇原文
4. 按意图组织答案结构：结论先行，流程题按步骤，对比题按维度
5. 每个要点必须附稳定来源标签，必须复制 `来源标签:` 原文，禁止写 `[c3]` 这类短引用
6. 回答语言必须与用户问题一致

### 9.2 提示词结构

`BuildPrompt` 显式包含：

- 当前问题类型描述
- 答案结构要求
- 跨来源综合要求
- 领域细节要求
- 输出要求提醒
- 意图特定指令块（metadata / procedure / compare / 文档约束）
- 近期对话历史（最多 6 轮，仅用于指代解析）
- 优先证据列表（重排后 chunk 的稳定引用）
- 上下文正文（所有上下文 chunk 带稳定标签）

### 9.3 稳定引用标签

系统不再使用 prompt 内临时编号 `[1] [2]`，而是使用：

```text
[doc/json/a.json | 某章节 | c3]
```

标签包含：

- 相对文件路径
- `SectionTitle`
- chunk 序号

即使上下文顺序在多次调用间变化，引用也保持稳定。

## 10. 回答质量控制与回退

### 10.1 异常回答检测

`IsOffTopicOrMalformedAnswer` 主要检测：

- 空回答
- 没有引用
- 全英文误答
- 模板化跑题
- 详细问题却回答过短
- 用途类问题却给出空泛回答

对于"这篇文章主要写什么"这类极短总结问题，词项重叠校验会适当放宽，避免高层概述被误判成跑题。

### 10.2 修复重试

如果首答不可靠，会用更严格的 repair prompt 再试一次。Repair prompt 新增硬性规则"绝对不能输出'我不知道'或'未覆盖'"，并再次强调引用格式修正。

### 10.3 抽取式回退

如果修复仍失败，则会按问题类型进入不同回退器：

- `BuildPaperSummaryFallbackAnswer`
- `BuildSummaryFallbackAnswer`
- `BuildExtractiveFallbackAnswer`
- `BuildUsageFallbackAnswer`

其中 summary 回退会优先挑选摘要、概述、架构、方法、结果等位置上的代表句，过滤参数堆叠型句子，并继续使用稳定引用标签。

## 11. 调试与可观测性

`LastRetrievalDebug` 现在会输出：

- 原始问题与检索管线标识
- 问题类型、焦点词、指定文档
- 进入语义计算的候选数
- 是否使用稀疏预筛（`refactored` 模式下始终为否，回退全量语义）
- 学习式重排状态：`global:ok (N/N)` 或失败原因
- 每个主命中 chunk 的详细打分拆解（reranker 分数为 `score`，其他字段在 `refactored` 模式下均为 0）
- 上下文窗口 chunk 数

这使得 reranker 行为分析和检索回归定位比早期版本更直接。

## 12. 离线评测

仓库已经提供最小可用的离线评测链路：

- 评测入口：`EvalRunner`
- 基线文件：`docs/eval-baseline.json`
- 运行脚本：`scripts/eval_rag.sh`

评测指标包括：

- **Case pass count** — 通过所需关键词 + 结构化检查的用例比例
- **Keyword coverage** — 期望关键词在回答中的覆盖率
- **Top chunk signal coverage** — 期望关键 chunk 出现在重排 Top 列表中的比例
- **Context signal coverage** — 期望关键 chunk 出现在最终上下文窗口中的比例
- **Citation precision / recall** — 对每个用例的预期引用标签的精确率和召回率
- **Scope accuracy** — 回答是否保持在正确的文档 scope 内
- **Evidence kind accuracy** — 是否召回了正确的内容类型

新的基线 schema 可以把"答案内容期望""检索证据期望""引用期望"分开表达，更容易区分问题出在生成、召回还是 grounding。

### 最新基线结果（2026-05-06）

| 指标 | 数值 |
|------|------|
| Case pass count | 15 / 20 (75.0%) |
| Keyword coverage | 79 / 88 (89.8%) |
| Top chunk signal coverage | 37 / 64 (57.8%) |
| Context signal coverage | 63 / 64 (98.4%) |
| 平均 citation precision | 74.3% |
| 平均 citation recall | 95.0% |
| Scope accuracy | 100.0% |
| Evidence kind accuracy | 100.0% |

**关键观察：**

- 上下文信号覆盖率（98.4%）优秀 — 最终上下文窗口几乎总是包含期望的关键证据。
- Top chunk 信号覆盖率（57.8%）是主要差距 — 期望的关键 chunk 被召回了，但有时未能在重排器排序的 Top 位出现。
- Citation recall（95.0%）强劲；precision（74.3%）有提升空间（答案引用的 chunk 数多于实际必要的数量）。
- `stm32-control` 用例失败是已知模式：对于 `procedure` 类问题，`.md` 格式的 Markdown 正文片段在 reranker 中有时会胜过等价的 `.json` 结构化 chunk，因为 Markdown 片段包含更多连续自然语言文本。

## 13. 关键参数

当前主要参数（`RagOptions.LoadFromEnvironment` 中的默认值）：

- `RAG_CHUNK_SIZE = 520`
- `RAG_CHUNK_OVERLAP = 96`
- `RAG_TOP_K = 8`（`refactored` 模式下的有效默认值；环境变量默认也是 8）
- `RAG_CANDIDATE_POOL_SIZE = 12`（候选池乘数基数；`refactored` 模式下不是向量候选上限的硬限制）
- `RAG_SPARSE_CANDIDATE_POOL_SIZE = 48`（向量候选数量下限；截断后实际范围 50–100）
- `RAG_CONTEXT_WINDOW_RADIUS = 1`
- `RAG_MIN_COMBINED_THRESHOLD = 0.18`
- `RAG_VECTOR_WEIGHT = 0.38`
- `RAG_BM25_WEIGHT = 0.20`（仅 `legacy` 模式生效）
- `RAG_KEYWORD_WEIGHT = 0.18`（同上）
- `RAG_TITLE_WEIGHT = 0.12`
- `RAG_JSON_STRUCTURE_WEIGHT = 0.10`
- `RAG_COVERAGE_WEIGHT = 0.08`
- `RAG_NEIGHBOR_WEIGHT = 0.08`
- `RAG_JSON_BRANCH_WEIGHT = 0.06`
- `RAG_DIRECT_KEYWORD_BONUS = 0.08`
- `RAG_FALLBACK_SENTENCE_COUNT = 2`
- `RAG_PIPELINE_MODE = refactored`（实际运行模式；与环境变量默认值 `legacy` 不一致，待修正）
- `RAG_RERANKER_ENABLED = true`
- `RAG_RERANKER_BASE_URL = http://127.0.0.1:8081`
- `RAG_RERANKER_MODEL = bge-reranker-v2-m3`
- `RAG_RERANKER_TOP_N = 12`
- `RAG_RERANKER_TIMEOUT_SECONDS = 30`
- `LLAMA_SERVER_TEMPERATURE = 0.2`
- `LLAMA_SERVER_MAX_TOKENS = 1536`
- `LLAMA_SERVER_TIMEOUT_SECONDS = 300`
- `RAG_CHAT_PROVIDER = llama_server`（也支持 `deepseek` 云端回退）

整体参数风格偏保守，目标是稳定、低幻觉、可调试，而不是极端生成长度或极端召回覆盖。

## 14. 管线模式说明

`RAG_PIPELINE_MODE` 支持三个值：

| 模式 | 说明 |
|------|------|
| `refactored` | **当前实际运行模式。** 纯向量余弦 Top-N 候选召回 → BGE-Reranker 作为唯一排序权威 → 意图感知上下文组装。主路径不使用 BM25。 |
| `legacy` | 原始启发式管线：BM25 稀疏预筛 + 语义打分 + 规则重排公式。保留用于回归对比。 |
| `simple` | 最简模式，保留用于调试。 |

## 15. 当前特点与局限

### 优点

- 不依赖外部向量数据库，部署简单
- 支持 `.md` / `.txt` / `.json` / `.pdf`
- JSON 可结构化展平并生成稳定的分节级引用
- BGE-Reranker 交叉编码器作为权威排序者 — 主路径中没有手工调权的打分公式
- 文档标题感知的硬 scope 过滤，将问题隔离到指定来源文档
- 引用标签稳定，可追溯性更好
- 意图感知的上下文组装确保每种问题类型都能优先获取正确的 chunk 类型
- 强约束系统提示词统一执行 grounding、术语保留和引用规范
- 有修复重试、summary 回退和多维度离线评测链路
- 支持 DeepSeek 云端 Chat Provider 作为本地 llama-server 的替代方案

### 局限

- 学习式重排依赖单独的本地 reranker 服务；服务不可用时会回退到向量分数排序
- PDF 能力依赖外部 `pdftotext` 命令及文本抽取质量
- Top chunk 信号覆盖率（57.8%）说明 reranker 对 `procedure` 类问题有时仍将 Markdown 正文片段排在等价 JSON 结构化 chunk 之前 — 这是当前已知的待改进问题
- 稳定引用目前到"文件 + 章节 + chunk"，还没有页码或更细粒度结构定位
- 评测目前仍以关键词 + 规则校验为主，尚未引入更强的语义自动评分器
- `RAG_PIPELINE_MODE` 环境变量默认值为 `legacy`，但实际运行使用 `refactored` — 这个不一致应该修正

## 16. 一句话总结

当前 WeaveDoc 的 RAG 核心思路是：

**先把本地文档规范化并切成带章节信息的 chunk，用纯向量余弦召回 Top-N 候选，再由 BGE-Reranker 交叉编码器作为权威排序者确定最终顺序，通过意图感知的上下文组装把最相关内容送入 `llama-server`，用强约束系统提示词、稳定引用、修复重试和离线评测保障回答质量与可追溯性。**
