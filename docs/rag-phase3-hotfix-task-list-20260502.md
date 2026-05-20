# WeaveDoc RAG Phase 3 热修复任务清单

> 日期：2026-05-02
> 背景：基于 `20260502-203321-weavedoc-rag-baseline.md` 报告的衰退分析，解决 Phase 3 重构后导致的 `stm32-control` 等 Case 失败及 Top chunk 丢失问题。

## 目标
- 修复 `abstract` 摘要块被 Metadata Policy 误判为 `-1000` 分的问题。
- 增强针对大块泛化 Markdown 文本的惩罚力度，确保细粒度的 `structured-json` 能够上位。
- 恢复甚至提升 Phase 3 阶段的 Top chunk 覆盖率及引用质量指标。

## 任务拆解与验收指标

### Task 1：修正 Abstract 节点的 Metadata Penalty
- **问题描述**：在 JSON 解析中 `abstract` 等总结段落的 `ContentKind` 被标记为了 `metadata`，导致触发了 `-1000` 极强降权，被彻底移出相关结果集。
- **实施方案**：
  - 在 `ChunkRanker.cs` 内部增加 `IsAbstractChunk` 的判断（根据 `SectionTitle` 或 `StructurePath` 是否包含 "abstract", "摘要", "englishAbstract" 等特征进行判断）。
  - 在 `TryRerankWithLearnedModelAsync` 和 `ApplySimplifiedMetadataPolicy` 的硬降权逻辑中，如果判断为 `IsAbstractChunk`，则豁免 `-1000f` 惩罚，转而应用 `-0.3f` 的轻微总结类惩罚。
- **验收指标**：
  - 能够正确为意图为 `summary` 等需要摘要的 Query 召回 `abstract` Chunk，得分处于合理阈值，且未遭遇 `-1000` 分的断崖式降权。
  - Eval 报告中 `stm32-summary` 的 Top chunk 命中恢复正常。

### Task 2：加大 Markdown 泛化块的结构化惩罚力度
- **问题描述**：在偏向 `structured-json` 的 `procedure` / `module_implementation` 问题中，包含大量冗杂上下文的长 Markdown 块评分（4.110）过高，原定的 `0.15f` 惩罚微乎其微。
- **实施方案**：
  - 在 `ChunkRanker.cs` 的 `TryRerankForGlobalRankingAsync` 中，将针对 Markdown body 且匹配 JSON 倾向问题的 `markdownBodyPenalty` 由 `0.15f` 上调至 `1.0f`。
- **验收指标**：
  - 执行 `procedure` 类意图检索时，针对具体的步骤、控制动作等 JSON 细粒度节点能够超越大块合并的 Markdown 节点。
  - Eval 报告中 `stm32-control` 的 `Top chunk` 命中由 0 恢复正常，能够抓取具体的“输入/前提”、“输出/执行”的 JSON 章节块。

### Task 3：回归验证与 Eval 确认
- **问题描述**：需要进行全面自动化测评来验证改动是否产生连锁负面效应。
- **实施方案**：
  - 在应用 Task 1 与 Task 2 的改动后，执行 RAG 系统基线 Eval 脚本。
  - 生成全新的 Eval 报告并审阅 `Case pass count` 与 `Top chunk signal coverage` 等关键指标。
- **验收指标**：
  - `Case pass count` 应明显回升（好于 4/20 的当前数据）。
  - `Top chunk signal coverage` 以及 `Context signal coverage` 必须提升。
  - 在保证以上两点的同时，不降低 `Citation from EvidenceSet` 等精度指标。
