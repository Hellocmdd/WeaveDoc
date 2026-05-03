# WeaveDoc RAG 修复诊断（2026-05-01）

## 背景

基于 `.eval/20260501-204003-weavedoc-rag-baseline` 结果（5/20 pass，keyword coverage 59.1%，top chunk 51.6%，context 98.4%）和 `rag-system-problem-analysis-20260501.md` 的系统分析，本文记录针对具体失败 case 的精确根因和修复方案。

---

## 根因 1：stm32-control structural fail

**现象**

- context signal coverage 满分，top chunk 0/3，structural fail
- debug 显示 c44（结论段，score=0.780）排 [1]，c37（环境补偿，score=1.087）排 [7]

**精确根因**

`BuildProcedureFallbackAnswer` 从 workflow/architecture 类 chunk（c26、c27）提取内容，这些 chunk 使用"脉宽调制信号驱动电磁阀"但不含"模糊PID"。含"模糊PID"的 c35（PID调节）和 c37（环境补偿）在 context 中存在，但 fallback builder 没有扫描全部 context chunks 补充遗漏实体。

BGE-Reranker 把 c44（结论段）排第一，因为它高频出现"精准灌溉"，语义相关度高，但不是核心机制段落。这是排序目标（query-chunk relevance）与答案目标（核心机制覆盖）不一致的典型表现。

**修复方向（优先级 1）**

在 `BuildProcedureFallbackAnswer` 构建完结构化回答后，扫描所有 context chunks，提取技术实体（`TechnicalEntityRegex` + `ChineseEntityRegex`），与已生成回答做差集，将遗漏实体追加到回答末尾。

---

## 根因 2：stm32-remote structural fail

**现象**

- 所有 retrieval/context 指标 pass，structural fail
- structural check 要求同时包含 "阈值"/"指令"/"手动触发"/"即时灌溉"

**精确根因**

`BuildModuleImplementationSlotAnswer` 对 `directHit=True` 的 chunk（c25）做了内容截断，丢失了最后一句"用户可通过远程设定湿度阈值，或手动触发即时灌溉指令"，而这句话正好包含 structural check 所需的全部关键词。

**修复方向（优先级 2）**

对 `directHit=True` 的 chunk，保留完整内容，不做截断。`directHit` 语义上表示该 chunk 精确命中目标章节，截断反而丢失了最有价值的部分。

---

## 根因 3：top chunk 排序被泛化段落污染（51.6%）

**现象**

多个 procedure/composition 类问题的 top chunk 是摘要、引言或结论段，而非核心机制段落。

**精确根因**

当前 `RerankCandidates` 对所有 chunk 类型使用相同评分目标。摘要/引言/结论段因为包含高频领域词（"精准灌溉""传感器""控制"），在 query-chunk relevance 上得分高，但它们是泛化描述，不是核心机制证据。

**修复方向（优先级 3）**

在 `RerankCandidates` 中，当 intent 为 procedure/composition/module_implementation 时，对 `contentKind=summary` 或 `sectionTitle` 匹配 abstract/conclusion/introduction 的 chunk 施加惩罚因子（×0.7），使核心机制段落更容易排到首位。

---

## 修复优先级

| 优先级 | 修改位置 | 影响 case | 风险 |
|--------|----------|-----------|------|
| 1 | `BuildProcedureFallbackAnswer` — 追加遗漏技术实体 | stm32-control structural + keyword | 低，局限在 fallback builder |
| 2 | `BuildModuleImplementationSlotAnswer` — directHit 保留完整内容 | stm32-remote structural | 低，单条件判断 |
| 3 | `RerankCandidates` — 泛化段落惩罚因子 | 多个 procedure/composition case top chunk | 中，影响全局排序 |

---

## 与系统分析文档的对应关系

本文的三个根因对应 `rag-system-problem-analysis-20260501.md` 中的：

- 根因 1 → 问题 6（答案抽取没有结构化）+ 问题 3（EvidenceSet 与 RankedChunks 职责混用）
- 根因 2 → 问题 6（答案抽取没有结构化）
- 根因 3 → 问题 2（排序目标不等于答案目标）

系统分析文档描述的是架构层面的通用问题，本文是针对当前 baseline 失败 case 的精确定位，两者互补。