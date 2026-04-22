# WeaveDoc RAG 算法架构

[English](rag-architecture.md)

本文总结当前 `csharp-rag-avalonia` 中 RAG 部分的实际实现，主要对应：

- `csharp-rag-avalonia/Services/LocalAiService.cs`
- `csharp-rag-avalonia/Services/LlamaServerChatClient.cs`
- `csharp-rag-avalonia/Services/RagOptions.cs`
- `csharp-rag-avalonia/Services/EvalRunner.cs`

## 1. 总体设计

当前实现是一套本地优先的桌面 RAG 流水线，主要分为 7 层：

1. 文档导入与规范化
2. 文档切块与元信息生成
3. Embedding 与本地缓存
4. 两阶段召回
5. 规则增强重排
6. 基于 `llama-server` 的回答生成
7. 质量校验、回退与离线评测

主调用链如下：

```text
用户问题
  -> AskAsync
    -> NormalizeQuestionForRetrieval
    -> BuildQueryProfile
    -> FindRelevantChunksAsync
      -> 稀疏预筛
      -> 只对候选池做语义打分
      -> RerankCandidates
      -> BuildContextWindow
    -> BuildPrompt
    -> llama-server /v1/chat/completions
    -> 回答质量校验
      -> 修复提示词重试
      -> 总结型回退
      -> 抽取式回退答案
```

## 2. 文档导入层

### 2.1 支持格式

当前支持从 `doc/` 目录索引以下文件：

- `.md`
- `.txt`
- `.json`

仓库里虽然可能存在 PDF 原文，但当前 RAG 索引链路并不会直接解析 PDF。

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

这样做的好处是：

- 检索仍能利用标题命中与章节命中
- 嵌套 JSON 不会退化成一整段难检索的原始字符串
- 深层数组项不再只剩下 `[1]`、`[2]` 这种弱标签
- 生成回答时引用仍能稳定落到“文件 + 章节 + chunk”

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

这些信息同时用于检索和稳定引用标签；其中 `StructurePath` 和 `ContentKind` 还会被 JSON 分支重排、上下文补全和 summary 回退逻辑复用。

## 4. Embedding 与缓存

### 4.1 Embedding 模型

系统使用本地 GGUF sentence embedding 模型：

- 默认模型：`sentence-transformers--all-MiniLM-L6-v2.gguf`
- 推理库：`LLamaSharp`

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

### 4.3 稀疏索引

除了 embedding，本地还维护：

- 每个 chunk 的 `TokenFrequency`
- 每个 chunk 的 `TokenCount`
- 全局 `_documentFrequency`
- 全局 `_avgDocumentLength`

这些数据为 BM25 与其他稀疏检索信号提供支撑。

## 5. 查询理解层

### 5.1 问题归一化

`NormalizeQuestionForRetrieval` 负责：

- 去掉寒暄和边界噪声
- 对“详细一点 / 继续 / 展开说”等追问尝试补全上一轮用户问题

### 5.2 Query token 抽取

`ExtractQueryTokens` 会提取：

- 英文数字 token
- 中文连续片段
- 较长中文片段的 2-gram 子词

### 5.3 QueryProfile

`BuildQueryProfile` 会生成：

- `FocusTerms`
- `Intent`
- `WantsDetailedAnswer`

现在 `FocusTerms` 直接从当前用户问题里提取；如果是“继续 / 展开说”这类追问，才会通过 `BuildFocusTermSourceText` 借助上一轮问题补全检索展开词。

支持的 `Intent` 包括：

- `compare`
- `explain`
- `procedure`
- `usage`
- `summary`
- `definition`
- `general`

其中 `summary` 明确覆盖“主要讲什么 / 主要内容 / 概述 / 总结”这类问题。

这些信息同时影响检索重排、回答结构和回退路径选择。

## 6. 两阶段召回

`FindRelevantChunksAsync` 现在已经采用两阶段召回，而不是对全量 chunk 直接做完整混合打分。

### 6.1 第一阶段：稀疏预筛

对所有 chunk 先只计算这些信号：

- `BM25`
- `KeywordScore`
- `TitleScore`
- `JsonStructureScore`
- `HasDirectKeywordHit`
- `NoisePenalty`

得到稀疏分数：

```text
sparseScore =
  bm25 * Bm25Weight
  + keyword * KeywordWeight
  + title * TitleWeight
  + jsonStructure * JsonStructureWeight
  + directHitBonus
  - noisePenalty
```

然后按稀疏分数排序，形成更大的预筛候选池。

### 6.2 第二阶段：语义打分

只对预筛出来的候选池再计算余弦相似度：

```text
finalCandidateScore =
  semantic * VectorWeight
  + sparseScore
```

这样可以避免对全量 chunk 都做 embedding similarity。

### 6.3 语义回退

如果某个问题在稀疏侧几乎没有有效证据，例如：

- 没有明显关键词命中
- 没有标题命中
- BM25 证据接近无效

系统会退回到“全量语义计算”，避免语义改写类问题被预筛误伤。

### 6.4 关键参数

- `RAG_CANDIDATE_POOL_SIZE`
  最终进入 rerank 的候选数
- `RAG_SPARSE_CANDIDATE_POOL_SIZE`
  稀疏阶段进入语义打分的候选数

## 7. 规则增强重排

`RerankCandidates` 会在候选分数上继续叠加规则特征。

### 7.1 Coverage

`ComputeCoverageScore` 衡量候选 chunk 对焦点词覆盖得有多完整。

### 7.2 Neighbor support

`ComputeNeighborSupportScore` 检查同文件相邻 chunk 是否也支持当前主题。

### 7.3 JSON branch support

`ComputeJsonBranchSupportScore` 会额外检查 JSON 同一结构分支上的 sibling / parent / child chunk 是否也包含焦点词。

这一步主要解决两类问题：

- 命中的是数组项时，希望同组规则或同模块的相邻字段也能提供支持
- 命中的是深层节点时，希望父子层级中的补充信息能进入重排

### 7.4 Intent boost

`ComputeIntentBoost` 会根据问题类型给包含特定信号词的 chunk 加小分：

- `usage` 偏好“作用 / 用途 / 功能 / 用于”
- `procedure` 偏好“步骤 / 流程 / 首先 / 然后”
- `explain` 偏好“原因 / 机制 / 因为 / 由于”

### 7.5 重排公式

```text
rerankScore =
  candidateScore
  + coverage * CoverageWeight
  + neighbor * NeighborWeight
  + jsonBranch * JsonBranchWeight
  + intentBoost
```

最终取前 `TopK` 个主命中 chunk。

## 8. 上下文窗口

`BuildContextWindow` 不只保留主命中 chunk，还会取其前后相邻 chunk；如果命中的是 JSON 结构化 chunk，还会补充同一结构分支上的辅助证据：

- 范围由 `ContextWindowRadius` 控制
- 对每个命中 JSON chunk，结构分支补充数量上限为 `max(1, ContextWindowRadius + 1)`
- 同一 chunk 只保留一次
- 最终按 `FilePath + Index` 排序

这样做可以保留局部上下文，减少 chunk 边界带来的信息截断。

## 9. 生成层

`LlamaServerChatClient` 通过本地 `llama-server` 的 OpenAI 兼容接口完成生成：

- 健康检查：`/health`
- 生成接口：`/v1/chat/completions`

### 9.1 提示词结构

`BuildPrompt` 与 `BuildRepairPrompt` 都会显式提供：

- 当前问题类型
- 优先证据
- 上下文正文
- 回答规则
- 按问题类型变化的跨来源综合规则

### 9.2 稳定引用标签

系统已不再依赖 prompt 内临时编号 `[1] [2]`，而是使用稳定来源标签：

```text
[doc/json/a.json | 某章节 | c3]
```

标签包含：

- 相对文件路径
- `SectionTitle`
- chunk 序号

这样即使上下文顺序变化，回答中的引用也更容易稳定定位。

## 10. 回答质量控制与回退

### 10.1 异常回答检测

`IsOffTopicOrMalformedAnswer` 主要检测：

- 空回答
- 没有引用
- 全英文误答
- 模板化跑题
- 详细问题却回答过短
- 用途类问题却给出空泛回答

对于“这篇文章主要写什么”这类极短总结问题，词项重叠校验会适当放宽，避免高层概述因为没有逐字复用提问词而被误判成跑题。

### 10.2 修复重试

如果首答不可靠，会用更严格的 repair prompt 再试一次。

### 10.3 抽取式回退

如果修复仍失败，则会按问题类型进入不同回退器：

- `BuildPaperSummaryFallbackAnswer`
- `BuildSummaryFallbackAnswer`
- `BuildExtractiveFallbackAnswer`
- `BuildUsageFallbackAnswer`

其中 summary 回退会优先挑选摘要、概述、架构、方法、结果等位置上的代表句，过滤参数堆叠型句子，并继续使用稳定引用标签。

## 11. 调试与可观测性

检索调试文本 `LastRetrievalDebug` 现在会输出：

- 原始问题与检索问题
- 问题类型与焦点词
- 稀疏预筛候选数
- 进入语义计算的候选数
- 是否启用了稀疏预筛
- 每个主命中 chunk 的详细打分拆解

这使得调权和回归分析比早期版本更直接。

## 12. 离线评测

仓库已经提供最小可用的离线评测链路：

- 评测入口：`EvalRunner`
- 基线文件：`docs/eval-baseline.json`
- 运行脚本：`scripts/eval_rag.sh`

评测流程会：

- 批量执行一组问题
- 输出回答
- 输出检索调试信息
- 做简单的 `expectedKeywords` 覆盖率统计

这套机制还不是完整自动评分框架，但已经足够用于日常回归检查和参数调整前后的横向对比。

## 13. 关键参数

当前重要参数包括：

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

整体参数风格偏保守，目标是稳定、低幻觉、可调试，而不是极端生成长度或极端召回覆盖。

## 14. 当前特点与局限

### 优点

- 不依赖外部向量数据库，部署简单
- 支持 `.md` / `.txt` / `.json`
- JSON 可结构化展平后再检索
- 已经从全量混合打分升级为两阶段召回
- 引用标签稳定，可追溯性更好
- 有修复重试、summary 回退、抽取回退和离线评测链路

### 局限

- 重排仍然是规则增强，不是学习式 reranker
- 仍未直接支持 PDF 解析
- 稀疏预筛虽提升了性能，但还不是 ANN 向量索引
- 稳定引用目前到“文件 + 章节 + chunk”，还没有页码或更细粒度结构定位
- 评测仍然以关键词覆盖为主，尚未引入更强的自动评分器

## 15. 一句话总结

当前 WeaveDoc 的 RAG 核心思路是：

**先把本地文档规范化并切成带章节信息的 chunk，再用“稀疏预筛 + 局部语义打分 + 规则重排”找出最相关上下文，把这些上下文送入本地 `llama-server` 生成答案，并通过稳定引用、修复重试、summary 回退和离线评测来保证回答质量。**
