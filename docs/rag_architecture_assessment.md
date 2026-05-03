# WeaveDoc RAG 架构客观评估报告

> 评估时间：2026-05-01  
> 基于：最新 eval 报告（04-29 基线 + 05-01 最新运行）、LocalAiService.cs 源码、历史 eval 趋势

---

## 一、当前架构概述

```
用户问题
  ↓
① 问题归一化 / 追问重建（NormalizeQuestionForRetrieval）
  ↓
② 问题分析（BuildQueryProfile）→ intent / focusTerms / requestedDoc
  ↓
③ 稀疏预筛（BM25 + keyword + title + jsonStructure）→ SparseCandidatePool
  ↓
④ 语义计算（BGE-Embedding 向量余弦相似度）→ Combined Score
  ↓
⑤ 规则重排（RerankCandidates）
  ↓
⑥ 学习式重排（BGE-Reranker，两路：全局 + 按证据槽位）
  ↓
⑦ 证据规划（EvidencePlan / EvidenceSet）
  ↓
⑧ 多路 Fallback 答案生成（procedure / composition / summary...）
  ↓
⑨ LLM 生成（Qwen/Llama llama-server）
```

系统定制程度极高：自定义了 intent 分类、多维度打分、证据槽位、多路 fallback，代码量约 8000 行（单文件 LocalAiService.cs）。

---

## 二、关键性能数据趋势（客观）

| 时间 | Case 通过率 | 关键词覆盖 | Top Chunk 覆盖 | Context 覆盖 |
|------|------------|-----------|--------------|------------|
| 04-18 (早期) | ~25% | ~40% | ~35% | ~65% |
| 04-28 | 7/20 (35%) | 58.0% | 53.1% | 87.5% |
| 04-29 | 8/20 (40%) | 69.3% | 53.1% | 87.5% |
| **05-01 最新** | **5/20 (25%)** | **59.1%** | **51.6%** | **98.4%** |

> [!WARNING]
> 05-01 的最新数据相比 04-29 **出现了显著回退**：通过率从 40% 降到 25%，关键词覆盖从 69.3% 降到 59.1%，说明最近一周的修改产生了**负收益**。

---

## 三、已识别的架构层级问题（按严重度）

### 🔴 P0：根本性问题——打分公式的反直觉行为（当前修改未能解决）

**具体表现（来自 05-01 stm32-control 检索日志）：**

| 排名 | Chunk | Score | BM25 | Semantic | 正确？|
|------|-------|-------|------|---------|-------|
| 1 | c16（引言） | 0.818 | **1.000** | 0.757 | ❌ |
| 2 | c44（结论） | 0.780 | 0.824 | 0.767 | ❌ |
| 3 | **c35（模糊PID 核心实现）** | 0.973 | 0.452 | 0.677 | ✅ |

注意：c35 的综合得分 **0.973 > 0.818**，但它排名第3！

这说明一个严重的架构缺陷：**打分计算是正确的，但最终排序不是按 Score 降序**。  
查看 `stm32-control-prefixed` 的 `Top Ranked Chunks` 顺序与 `Retrieval Debug` 中的 score 顺序**不一致**——Top Chunks 显示 c44、c16、c35 顺序，而 score 上 c35=0.973 是最高的。

**根因推测**：`TryRerankWithinEvidenceGroupsAsync` 的 slot 分配逻辑会按 slot 填充候选，**改变了最终顺序**，导致 score 较高的 chunk 因 slot 分组后被降权到后位，使得 Top Ranked Chunks 的实际顺序并不等于 score 降序。这是一个隐蔽的逻辑 Bug。

---

### 🔴 P0：BM25 对"概述类"文本的严重过度拉升

**根本原因**：引言（c16）和结论（c44）这类文本天然包含大量关键词（它们本来就是全文的综述），导致 BM25 分数最高。  

当前架构中 BM25 是预筛阶段的主要过滤信号，而引言和结论经常突破 `SparseCandidatePoolSize` 限制冲到最前面。这个问题**在整个 4 月份的修复过程中从未被根本解决**，只是通过各种 `NoisePenalty` 和 `JseonStructureScore` 来抵消。

---

### 🟡 P1：打分权重组合爆炸导致的不可预测性

当前打分公式涉及的变量：
```
score = semantic * VectorWeight
      + bm25 * Bm25Weight
      + keyword * KeywordWeight
      + title * TitleWeight
      + jsonStructure * JsonStructureWeight
      + neighbor * NeighborWeight
      + jsonBranch * JsonBranchWeight
      + docTarget * DocTargetScore
      + (directHit ? DirectKeywordBonus : 0)
      - noisePenalty
```

**问题**：各权重之间相互耦合，改一个权重会影响其他权重的效果。从评估历史看，反复调整权重（`Bm25Weight`、`KeywordWeight` 等）会产生"一个 case 好了、另一个变差"的 whack-a-mole 现象。

---

### 🟡 P1：证据槽位排序逻辑污染了全局排序

当前 `TryRerankWithinEvidenceGroupsAsync` 的逻辑：
1. 按 slot（输入/前提、决策、过程、执行、约束）分组
2. 每个 slot 单独调用 Reranker
3. 按 slot 顺序拼接结果

**问题**：当某个 slot 的候选集中，正确 chunk 不是 Top-1（Reranker 区分度不足时），该 chunk 仍会被 slot 中其他 chunk 覆盖。而跨 slot 的 chunk（如 c35 模糊PID，可能属于"决策"slot 但 Reranker 被 c37 环境补偿覆盖），就会消失在 Top Ranked Chunks 中。

---

### 🟡 P1：`procedure` 类问题的关键词扩展缺失

当问题被识别为 `procedure`（如"如何实现精准灌溉"），focusTerms 未被扩展到包含具体技术词（"模糊PID"、"电磁阀"、"PID控制"）。  
这导致 `jsonStructure` 和 `titleScore` 对核心实现章节的得分为 0，无法帮助 c35 突破引言的 BM25 优势。

---

### 🟡 P1：多路 Fallback 机制可靠性差

从 05-01 的日志来看，`procedure` fallback（`BuildProcedureFallbackAnswer`）被频繁触发，但答案质量不稳定：
- `stm32-control`：使用了 procedure fallback，但 slot 中命中 c27（workflow）而非 c35（PID实现），导致关键词仅匹配 1/4
- 答案模板（"1.输入/前提: ... 2.核心处理/决策: ..."）的格式固定，无法根据 chunk 内容的实际完整性动态调整

---

### 🟢 P2：LLM 生成层问题（相对次要）

- 引用格式幻觉（json-scoped case）
- "无法判断文档不包含"的鲁棒性问题
- 这些是 prompt 工程问题，可以独立修复，不影响检索架构

---

## 四、一周修复效果回顾

基于 eval 时间戳的客观分析：

| 阶段 | 时间 | 改动方向 | 效果 |
|------|------|---------|------|
| 初期 | ~04-18 | 建立基础架构 | 25% 通过率 |
| 阶段1 | 04-22~04-24 | 证据槽位 + Reranker | 基本持平 |
| 阶段2 | 04-25~04-26 | 多轮 fallback 调整 | 小幅改善 |
| 阶段3 | 04-27~04-28 | 大量权重/逻辑调整 | 35%→40% |
| 阶段4 | **04-29~05-01** | **（本周修改）** | **40%→25% 回退** |

**结论**：本周的修改不仅没有改善，还产生了明显的性能下降。

---

## 五、问题本质的诊断

这套架构存在的**根本性困境**：

### 困境一：规则与学习模型的角色混淆

当前架构同时依赖：
- 手工规则（intent 分类、BM25 权重、slot 匹配、focusTerms）
- 学习模型（BGE-Embedding、BGE-Reranker）

两者的责任边界不清晰：当 Reranker 改变排名时，规则层的权重计算就变得无效；当规则层大量干预候选集时，Reranker 的能力就被限制。

### 困境二：复杂度飙升而可解释性下降

`LocalAiService.cs` 已经 8000 行，包含数十个权重参数、多套 fallback 逻辑、复杂的 slot 机制。每次新增规则都在增加干扰路径，而不是解决根本问题。

### 困境三：核心问题（引言>实现）从未被根本解决

从 04-18 到 05-01，`stm32-control` 这个 case 从未通过，一直是同样的失败模式（引言 c16 排第一）。这说明当前架构的组合打分方式在这类 case 上存在**结构性失败**，不是参数调优能解决的。

---

## 六、建议：继续修复 vs 彻底重构

### 选项 A：继续在当前架构修复

**可做的有限改动（高确定性）：**
1. 修复 Top Ranked Chunks 排序 Bug（score 高的 c35 实际出现在第三位）
2. 对 abstract/conclusion/引言类 chunk 添加 chunkType 标签，`procedure` 类问题时硬性降权
3. 简化 slot 拼接逻辑，改为 score 降序 + slot 标注

**预期效果上限**：可能从 25% 回到 35-40%，难以突破

**风险**：继续在 8000 行代码上叠加规则，复杂度进一步提升，下一次修改又可能引发回退

---

### 选项 B：彻底重构（推荐）

**核心思路转变**：

当前架构的核心错误是**用规则模拟模型应该做的事**。正确的现代 RAG 设计应该是：

```
用户问题
  ↓
① Query 改写（可选）
  ↓
② 向量检索（Top-K，K 较大，如 50-100）
  ↓  ← 不用 BM25 主导，BM25 作为辅助
③ BGE-Reranker 直接对 Top-K 重排
  ↓
④ 取 Top-N（N=5-10）送 LLM
  ↓
⑤ LLM 生成（prompt 中包含文档类型感知的指令）
```

**重构的关键改变：**

| 方面 | 当前 | 重构后 |
|------|------|--------|
| 主要检索信号 | BM25 主导预筛 | 向量检索主导，BM25 辅助（融合或 OR 补充） |
| Reranker 定位 | 后置，权重混合 | 核心排序器，直接决定最终顺序 |
| intent 分类 | 8000 行手工规则 | 仅保留最简单的 2-3 个分支（document-scope / corpus-wide / followup） |
| Fallback | 复杂的多路 fallback | 统一交给 LLM（prompt 改善） |
| 多文档隔离 | 检索层约束 | metadata 过滤（简单可靠） |

**重构预期效果**：
- 架构复杂度大幅下降（目标 1000-2000 行核心检索逻辑）
- 可调参数减少，行为更可预测
- 预期通过率：50-65%（基于 context signals 已达 98.4%，说明 Reranker 如果拿到正确候选集，可以做得很好）

---

## 七、我的客观建议

> [!IMPORTANT]
> **不建议继续在当前架构上修复**，理由如下：
>
> 1. **本周修复产生了负收益**（40% → 25%），说明修改方向出现了问题，再修下去风险很大
> 2. **核心问题（引言排名高于实现）在过去两周内始终未被解决**，说明这是架构性缺陷
> 3. **8000行的复杂度使得每次修改的影响难以预测**，这是最危险的信号

**建议推翻重构，但要保留的宝贵资产：**
- eval 框架（评估体系本身是非常好的！）
- JSON 文档结构化解析逻辑（把 JSON 拆成有层次的 chunks 是正确的）
- BGE-Embedding + BGE-Reranker 的双模型组合（方向对）
- 文档 scope 推断的基本思路（active-document / corpus-wide）

**建议丢弃的：**
- BM25 作为主要预筛信号（改为向量检索主导）
- 复杂的多维度加权打分公式
- 8+套 Fallback 答案模板
- intent 分类驱动的检索策略差异（保留 2-3 个关键分支即可）
- 证据槽位排序机制（逻辑太复杂且会破坏 score 排序）

---

## 八、重构的具体起点建议

如果你决定重构，第一步可以用最简单的方式验证假设：

```csharp
// 最简 RAG，验证向量检索 + Reranker 的上限
var questionVector = await GetEmbeddingAsync(question);
var top100 = _indexedChunks
    .Select(c => (chunk: c, score: CosineSimilarity(questionVector, c.Embedding)))
    .OrderByDescending(x => x.score)
    .Take(100)
    .ToList();

// 如果有文档 scope，先过滤
if (targetFile != null)
    top100 = top100.Where(x => x.chunk.FilePath == targetFile).ToList();

// Reranker 直接决定最终排名
var reranked = await RerankerAsync(question, top100.Select(x => x.chunk).ToList());
var topN = reranked.Take(8).ToList();

// LLM 生成
var answer = await LlmGenerateAsync(question, topN);
```

先跑一遍 eval，看通过率是多少。如果基线已经接近或超过当前的 40%，就说明重构方向正确，然后再逐步加入文档 scope、多文档隔离等必要功能。

---

*本报告基于客观 eval 数据和源码分析，不含主观臆测。*
