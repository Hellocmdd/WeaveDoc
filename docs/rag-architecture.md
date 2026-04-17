# WeaveDoc RAG 算法架构总结

本文总结当前 `csharp-rag-avalonia` 中 RAG 部分的实现架构，重点对应 `csharp-rag-avalonia/Services/LocalAiService.cs` 与 `csharp-rag-avalonia/Services/LlamaServerChatClient.cs` 的实际代码路径。

## 1. 总体架构

当前实现是一个本地优先的 RAG 流水线，分为 6 个阶段：

1. 文档加载与切块
2. Embedding 计算与缓存
3. 混合召回
4. 候选重排
5. 上下文窗口拼接
6. 基于 `llama-server` 的回答生成与失败回退

整体调用链如下：

```text
用户问题
  -> AskAsync
    -> NormalizeQuestionForRetrieval
    -> FindRelevantChunksAsync
      -> 向量相似度
      -> BM25
      -> 关键词命中
      -> 标题命中
      -> 噪声惩罚
      -> RerankCandidates
      -> BuildContextWindow
    -> BuildPrompt
    -> LlamaServerChatClient.CompleteAsync
    -> 回答质量校验
      -> 修复提示词重试
      -> 抽取式回退答案
```

## 2. 文档侧索引流程

### 2.1 文档来源

- 文档目录固定为工作区下的 `doc/`
- 当前只索引 `.md` 和 `.txt`
- 加载逻辑在 `LoadCorpusAsync`

### 2.2 切块策略

切块逻辑由 `SplitIntoChunks` 完成，设计目标是尽量保留段落和章节语义：

- 先按双换行切成段落
- 如果段落像标题，则切换 `sectionTitle`
- 普通段落按 `ChunkSize` 累积到缓冲区
- 超长段落由 `SplitLargeParagraph` 再切分
- 邻接切块之间通过 `ChunkOverlap` 保留重叠，降低语义断裂

每个切块都带有如下结构化元信息：

- `Source` / `FilePath`
- `Index`
- `DocumentTitle`
- `SectionTitle`
- `Text`

这意味着后续检索不是只对纯文本打分，而是能利用文档标题、章节标题和邻接关系进行增强。

### 2.3 标题抽取

标题抽取逻辑由 `ExtractDocumentTitle` 完成：

- 优先使用首个 Markdown 风格标题
- 如果没有，则退化为文件名

这一步直接影响后续 `ComputeTitleScore` 的效果。

## 3. Embedding 与索引构建

### 3.1 Embedding 模型

系统使用本地 GGUF embedding 模型：

- 默认模型：`sentence-transformers--all-MiniLM-L6-v2.gguf`
- 加载入口：`LoadEmbeddingModelAsync`
- 推理库：`LLamaSharp`

Embedding 前会通过 `PrepareTextForEmbedding` 做截断控制，保证输入不要超过 embedding 模型上下文预算。

### 3.2 Embedding 缓存

缓存由内部类 `EmbeddingCache` 管理，缓存文件位于：

- `.rag/embedding-cache.json`

缓存键由切块内容及关键配置共同决定，因此有两个效果：

- 文档内容不变时避免重复 embedding
- 文档改动或切块配置变化时自动失效并重算

### 3.3 词频索引

除向量外，系统还会为每个切块构建：

- `TokenFrequency`
- `TokenCount`
- 全局 `_documentFrequency`
- 全局 `_avgDocumentLength`

这部分为 BM25 和覆盖率打分提供支撑。

## 4. 查询理解层

查询进入 `AskAsync` 后不会直接检索，而是先做一层轻量问题理解。

### 4.1 问题归一化

`NormalizeQuestionForRetrieval` 负责：

- 去掉寒暄前缀
- 去掉边界噪声符号
- 对“详细一点 / 展开讲 / 继续说”之类追问，尝试拼接上一轮用户问题，避免检索词过空

### 4.2 查询词抽取

`ExtractQueryTokens` 使用正则提取：

- 英文数字 token
- 中文连续片段

对于较长中文词，还会额外切出 2-gram 子词，提高召回鲁棒性。

### 4.3 焦点词筛选

`BuildQueryProfile` 会进一步过滤停用词，只保留更有区分度的焦点词，用于：

- 标题命中打分
- 覆盖率计算
- 邻居支持计算

### 4.4 意图识别

当前使用规则式意图分类 `DetectIntent`，支持：

- `compare`
- `explain`
- `procedure`
- `usage`
- `summary`
- `definition`
- `general`

意图不仅影响生成提示词，也会在重排阶段产生额外意图增益。

## 5. 混合召回层

召回核心在 `FindRelevantChunksAsync`。

### 5.1 向量召回

对问题计算 embedding 后，与每个切块 embedding 做余弦相似度：

- 相似度函数：`CosineSimilarity`
- 优点：适合语义近义表达和改写问题

### 5.2 BM25 召回

`ComputeBm25` 基于：

- 局部词频 `tf`
- 全局文档频率 `df`
- 平均长度归一化

这是典型稀疏检索信号，适合用户问题中的关键术语、专有名词和短语命中。

### 5.3 关键词匹配

`ComputeKeywordScore` 会统计 query token 在切块文本中的命中覆盖率，并给更长 token 更高权重：

- 长 token 权重更高
- 若命中非 CJK 短词或较长中文词，记为 `HasDirectKeywordHit`

随后可额外加上 `DirectKeywordBonus`。

### 5.4 标题匹配

`ComputeTitleScore` 用焦点词匹配：

- `DocumentTitle`
- `SectionTitle`

这使得标题、章节名与问题主题一致的切块更容易进入候选池。

### 5.5 初始打分公式

候选池分数由以下部分线性组合得到：

```text
初始分数 =
  semantic * VectorWeight
  + bm25 * Bm25Weight
  + keyword * KeywordWeight
  + title * TitleWeight
  + directHitBonus
  - noisePenalty
```

其中 `noisePenalty` 会打压以下噪声块：

- 关键词行
- DOI / 中图分类号 / 文献标识码
- 参考文献
- 很短的标题块
- 英文摘要型元信息块

### 5.6 候选池截断

全部切块算完分后，会按分数排序，截取前 `CandidatePoolSize` 个作为重排输入。

## 6. 重排层

重排入口是 `RerankCandidates`。当前不是引入外部 cross-encoder，而是在已有得分上叠加轻量规则特征。

### 6.1 覆盖率得分

`ComputeCoverageScore` 统计焦点词在候选切块中覆盖了多少：

- 如果一个切块覆盖的问题焦点更全，说明它更可能是“主答案块”

### 6.2 邻居支持得分

`ComputeNeighborSupportScore` 检查同一文档中前后相邻切块是否也包含焦点词：

- 如果邻居块也支持当前主题，说明这个片段位于连续的主题区间
- 这能降低孤立误命中的概率

### 6.3 意图增益

`ComputeIntentBoost` 根据问题意图，为包含特定信号词的切块加小分：

- `usage` 倾向命中“用于 / 作用 / 功能”
- `procedure` 倾向命中“步骤 / 流程 / 首先 / 然后”
- `explain` 倾向命中“原因 / 机制 / 由于 / 因为”

### 6.4 重排后分数

```text
最终分数 =
  初始分数
  + coverage * CoverageWeight
  + neighbor * NeighborWeight
  + intentBoost
```

然后取前 `TopK` 个主命中切块作为最终高分结果。

## 7. 上下文扩窗层

最终答案并不是只看 `TopK` 主块，而是通过 `BuildContextWindow` 对每个命中块做邻接扩窗：

- 取当前块前后 `ContextWindowRadius` 个切块
- 同一块只保留一次
- 最后按 `FilePath + Index` 稳定排序

这样做的目的有两个：

- 保留高分块周围的补充语义
- 减少切块边界带来的信息截断

因此，这套系统实际上是：

- 用高分块决定“哪里相关”
- 用邻接窗口决定“给模型看多少上下文”

## 8. 生成层

生成由 `BuildPrompt` + `LlamaServerChatClient.CompleteAsync` 完成。

### 8.1 提示词结构

提示词包含以下部分：

- 角色约束
- 回答规则
- 当前问题类型说明
- 最近对话摘要
- 高分证据清单
- 上下文切块正文
- 用户问题

回答规则比较强，核心目标是：

- 必须基于上下文
- 每句尽量带来源编号
- 禁止输出题头、作者、单位、模板化杂质
- 按问题类型组织回答

### 8.2 模型调用方式

聊天模型不直接嵌在进程内，而是通过 `llama-server` 的 OpenAI 兼容接口调用：

- 健康检查：`/health`
- 生成接口：`/v1/chat/completions`

这让系统实现上将：

- embedding
- 检索与拼 prompt
- 生成模型服务

三者解耦开来。

### 8.3 长回答续写

若服务返回 `finish_reason = length`，客户端会自动追加：

- 上一段 assistant 内容
- “继续，紧接上文输出，不要重复前文...”

从而完成分段续写。

## 9. 回答质量控制与回退

这是当前实现里比较实用的一层。

### 9.1 异常回答检测

`IsOffTopicOrMalformedAnswer` 会检测：

- 空回答
- 没有引用编号
- 明显跑题的模板化回答
- 全英文摘要式误答
- 对“详细回答”却返回过短内容
- 对“作用/用途”问题给出过泛、过空的答案

### 9.2 修复重试

如果首答被判为低质量，会重新走一次 `BuildRepairPrompt`：

- 限制更强
- 明确要求重答
- 强化引用、语言和结构约束

### 9.3 抽取式回退

如果修复后仍不可靠，则进入 `BuildExtractiveFallbackAnswer`：

- 从命中切块中抽取最相关句子
- 对“作用/用途”类问题走专门回退逻辑
- 对详细型问题会返回更多句子，避免只给一条残句

这层设计的价值是：即便生成模型不稳定，也尽量输出“有依据的保底答案”。

## 10. 参数配置

当前主要参数来自 `RagOptions`，可通过环境变量覆盖。

默认关键参数如下：

- `RAG_CHUNK_SIZE = 520`
- `RAG_CHUNK_OVERLAP = 96`
- `RAG_TOP_K = 4`
- `RAG_CANDIDATE_POOL_SIZE = 12`
- `RAG_CONTEXT_WINDOW_RADIUS = 1`
- `RAG_MIN_COMBINED_THRESHOLD = 0.18`
- `RAG_VECTOR_WEIGHT = 0.38`
- `RAG_BM25_WEIGHT = 0.20`
- `RAG_KEYWORD_WEIGHT = 0.18`
- `RAG_TITLE_WEIGHT = 0.12`
- `RAG_COVERAGE_WEIGHT = 0.08`
- `RAG_NEIGHBOR_WEIGHT = 0.08`
- `RAG_DIRECT_KEYWORD_BONUS = 0.08`
- `LLAMA_SERVER_TEMPERATURE = 0.2`
- `LLAMA_SERVER_MAX_TOKENS = 1536`

默认设计明显偏向“稳健、可控、低幻觉”，而不是激进生成。

## 11. 当前架构的特点

### 优点

- 向量、BM25、关键词、标题信号同时使用，召回更稳
- 不依赖外部向量数据库，部署简单
- 利用章节标题、邻居块和意图信号做轻量重排，工程成本低
- 有完整的回答修复和抽取式回退链路
- 本地 embedding + 本地 `llama-server`，隐私性较好

### 局限

- 重排仍是规则增强，不是学习式 reranker
- 当前只支持 `.md` / `.txt`
- 依赖 chunk 级线性扫描，数据量继续增大后检索成本会上升
- 引用编号是上下文内编号，不是文档真实页码或章节定位
- 多轮对话改写仍较轻量，复杂指代场景下能力有限

## 12. 一句话总结

这套 RAG 的核心思路是：

**用本地 embedding 做语义召回，用 BM25/关键词/标题做精确补强，用覆盖率与邻居支持做轻量重排，再把邻接窗口送入 `llama-server` 生成，并用修复与抽取回退兜底回答质量。**

