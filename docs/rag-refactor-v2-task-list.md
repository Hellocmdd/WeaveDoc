# WeaveDoc RAG V2 彻底重构计划与任务清单

> 目标：将高度耦合、脆弱的"规则驱动（Heuristic-driven）"架构彻底重构为简洁、鲁棒的"模型驱动（Model-driven）"架构。
> 核心原则：大幅删减 C# 侧的硬规则干预，让 Embedding 专注语义召回，Reranker 专注精准排序，LLM 专注推理与兜底。完全不受旧有复杂槽位（Slots）、多路打分公式的代码影响。

## 完成进度总览

| 阶段 | 状态 | 完成时间 |
|------|------|----------|
| Phase 1：清理历史包袱 | ✅ 已完成 | 2026-05-02 |
| Phase 2：极简召回层 | ✅ 已完成 | 2026-05-02 |
| Phase 3：Reranker 排序权威 | ✅ 已完成 | 2026-05-02 |
| Phase 4：提示词工程 | ✅ 已完成 | 2026-05-03 |
| Phase 5：评估与验证回归 | ⏳ 待执行 | — |

## 阶段任务拆解与验收指标

### Phase 1：清理历史包袱（极简破局） ✅

- ✅ **任务 1.1**：移除或注释掉所有与 `EvidencePlan`、`EvidenceSlot`、`EvidenceMode` 相关的复杂槽位选择逻辑。
  - 从 `RagContracts.cs` 删除 `EvidencePlan`、`EvidenceSlot`、`EvidenceSet` 数据结构定义。
  - 从 `EvidenceSelector.cs` 删除 `BuildEvidencePlan`、`BuildEvidenceSet`、`BuildEvidenceSlots` 等全部槽位逻辑，保留 `ShouldUseActiveDocumentScope` 及 `RememberActiveDocumentScope`（已改签名）。
  - 从 `LocalAiService.cs` 删除 `EvidenceDebugSnapshot` 类型、`CreateEvidenceDebugSnapshot` 方法及所有对 `EvidenceSet`/`EvidencePlan` 字段的引用（含日志、充足性判断）。
  - 从 `EvalRunner.cs` 删除 `EvidenceMode` 推断逻辑及 `EvidenceDebugSnapshot` 参数传递。
- ✅ **任务 1.2**：删除或封存各种"硬规则"惩罚计算（如 `ApplySimplifiedMetadataPolicy`，针对 metadata/noise 的 `-1000` 降权，针对 abstract/intro/conclusion 的 `-0.3` 惩罚，针对 Markdown body 的 `-0.15` 二次降权）。
  - 在 `ChunkRanker.cs` 的 `TryRerankWithLearnedModelAsync` 方法中移除 `penalty` 变量和所有基于 `ContentKind` 的惩罚计算。
  - 删除 `TryRerankForGlobalRankingAsync`（含 Markdown body penalty 二次修正）和 `ApplySimplifiedMetadataPolicy`。
- ✅ **任务 1.3**：移除基于 Intent 的过度定制化处理（保留文档 scope 判断，移除槽位分配和充足性门控）。
  - `LocalAiService.AskAsync` 中移除 `EvidenceSet.IsSufficient` 门控，改为直接走 Reranker 排序结果判断。
- **验收指标**：✅ `dotnet build` 零错误通过，核心流程不再出现任何 `EvidencePlan`/`EvidenceSlot` 相关调用。

### Phase 2：构建"极简"召回层 (Dumb Retriever) ✅

- ✅ **任务 2.1**：重写 `FindRelevantChunksAsync` 预筛逻辑（`CandidateRetriever.cs`）。
  - 第一步：仅依赖向量余弦相似度（Cosine Similarity）进行初步打分。
  - 第二步：提取 Top-100。
  - 第三步：完全移除 BM25 的主导地位（BM25 不参与初筛），消除其对引言/结论块的偏爱。
- ✅ **任务 2.2**：在召回阶段应用"硬过滤"（直接基于 `FilePath` 进行文档 Scope 过滤）。
- **验收指标**：⏳ 待实际运行验证（可通过观察 `LastRetrievalDebug` 日志和 `LastRankedChunkSnapshots` 中具体实现块是否稳定进入 Top-5 进行确认）。

### Phase 3：确立 Reranker 的绝对排序权威 (Smart Ranker) ✅

- ✅ **任务 3.1**：将 Phase 2 召回的 Top-100 候选集，**不加任何干预地**送入 BGE-Reranker。在发送前，应用 `BuildRerankerQuery` 根据 Intent 添加 Focus 提示，以提升排序精准度。
- ✅ **任务 3.2**：完全按照 Reranker 返回的分数进行**严格降序排列**，取 Top-8 到 Top-12 作为最终上下文。不再进行任何槽位拼装或二次人为降权。
- **验收指标**：解决核心 P0 Bug，即得分最高（如 0.97）的核心实现 Chunk（如 `c35`）能稳定排在最终列表的第一或第二名，不再被低分引言（`c16`）压制或被槽位分配挤出。

### Phase 4：提示词工程与 LLM 生成 (Prompt & Generate) ✅

- ✅ **任务 4.1**：删除 C# 侧多套复杂的答案生成模板分支（如 `BuildProcedureFallbackAnswer`）。
- ✅ **任务 4.2**：编写一个强大的系统提示词，明确要求大语言模型："严格遵循提供的上下文块中的具体参数、代码实现和专业术语，拒绝泛泛而谈，按逻辑条理输出"。
- **验收指标**：在 LLM 拥有优质 Top-N 的情况下，回答不再丢失具体术语（如不再漏掉 `vue 3`、`mybatis-plus` 等技术栈名词），减少概括性幻觉。

### Phase 5：评估与验证回归 (Eval & Baseline) ⏳

- ⏳ **任务 5.1**：运行自动化 Eval 脚本 (`eval-baseline`) 对比基线。
- **验收指标**：
  1. `Case pass count` 突破原有天花板（期望达到 50% 以上，高于 4/28 的 40% 和最新的 25%）。
  2. `Top chunk signal coverage` 显著提升，证明 Reranker 真实排出了有用的具体信息段落。
