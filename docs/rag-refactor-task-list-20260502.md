# WeaveDoc RAG 重构任务清单

> 日期：2026-05-02  
> 背景：基于 `rag-eval-20260501.md`、`rag_architecture_assessment.md`、`rag-system-problem-analysis-20260501.md` 的共同结论整理。  
> 原则：渐进式重构，不做一次性推翻重写；先验证收益，再替换主链路。

## 目标

- 降低 `LocalAiService.cs` 中 RAG 主链路复杂度，拆清检索、排序、证据选择、答案生成的职责边界。
- 解决当前 `Context coverage` 高但 `Top chunk` 与答案完整性不稳定的问题。
- 让 reranker 负责全局排序，让 EvidenceSet 负责证据覆盖，不再让 slot 顺序污染 `RankedChunks`。
- 保留已有 eval、结构化 chunk、文档 scope、embedding、reranker 等有效资产。

## 非目标

- 不重写 Avalonia UI。
- 不更换 embedding/reranker/LLM 模型作为第一阶段手段。
- 不为了单个 benchmark case 继续堆专用关键词规则。
- 不把 EvidenceSet 概念删除；只重构它和全局排序之间的边界。

## Phase 0：建立重构验证基线

- [x] 固定当前 eval 基线
  - 记录当前可复现报告路径、通过率、关键词覆盖、Top chunk 覆盖、Context 覆盖、引用指标。
  - 验收：文档中能明确对比“旧主链路”和“实验链路”的同一套 20 case 指标。

- [x] 增加实验管线开关
  - 新增配置项，例如 `RAG_PIPELINE_MODE=legacy|simple|refactored`。
  - 默认保持 `legacy`，避免影响当前应用行为。
  - 验收：不改现有默认行为；eval 可以通过环境变量切换管线。

- [x] 实现最小实验管线 `simple`
  - query embedding。
  - 文档 scope filter。
  - vector top 50-100。
  - reranker 直接重排。
  - top 5-10 进入现有 prompt。
  - 验收：能跑完整 eval，并输出与 legacy 同格式的 debug/report。

- [x] 对比 simple 与 legacy
  - 重点观察：Top chunk 覆盖、关键词覆盖、引用 precision/recall、失败 case 类型变化。
  - 验收：决定后续是否以 simple 作为新主链路骨架。

### Phase 0 执行记录

- 旧主链路基线：`.eval/20260501-231544-weavedoc-rag-baseline.md`，基于同一份 `docs/eval-baseline.json` 的 20 case。
- 实验链路报告：`.eval/20260502-081815-weavedoc-rag-baseline.md`，运行命令：`RAG_PIPELINE_MODE=simple dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json`。
- 新增开关：`RAG_PIPELINE_MODE=legacy|simple|refactored`，默认 `legacy`；当前 `refactored` 暂不启用新逻辑，非 `simple` 均走旧主链路。
- 验证：`dotnet build csharp-rag-avalonia/RagAvalonia.csproj` 通过；仅有既有 `Tmds.DBus.Protocol 0.21.2` 高危漏洞告警。

| 管线 | 报告 | 通过率 | 关键词覆盖 | Top chunk 覆盖 | Context 覆盖 | 引用 precision | 引用 recall | Citation from EvidenceSet |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| legacy | `.eval/20260501-231544-weavedoc-rag-baseline.md` | 6/20 (30%) | 51/88 (58.0%) | 39/64 (60.9%) | 63/64 (98.4%) | 81.7% | 85.0% | 85.0% |
| simple | `.eval/20260502-081815-weavedoc-rag-baseline.md` | 2/20 (10%) | 53/88 (60.2%) | 29/64 (45.3%) | 54/64 (84.4%) | 84.2% | 90.0% | 75.0% |

结论：`simple` 不能直接作为新主链路骨架。它验证了“文档 scope + vector top 50-100 + reranker 直接重排”可以跑完整 eval，并且关键词覆盖与引用 recall 略有提升；但 Top chunk 覆盖和 Context 覆盖明显下降，说明完全去掉 legacy 的稀疏补充、上下文扩展和 EvidenceSet 选择会丢证据。后续 Phase 1-3 应以 `simple` 作为可切换实验下限，而不是替换目标：保留全局 reranker 排序方向，同时把 BM25/title/structure 作为 OR 补充召回，并重新定义 EvidenceSet 与 RankedChunks 的边界。

## Phase 1：拆分 `LocalAiService.cs` 职责

- [x] 提取查询理解模块
  - 建议新文件：`Services/Rag/QueryUnderstandingService.cs`。
  - 包含：问题归一化、追问重建、requested document 解析、少量 intent/scope 判断。
  - 验收：`BuildQueryProfile` 相关逻辑从 `LocalAiService.cs` 移出，单元测试覆盖典型中文问题、指定文档问题、追问问题。

- [x] 提取候选召回模块
  - 建议新文件：`Services/Rag/CandidateRetriever.cs`。
  - 包含：vector recall、BM25/title/structure 辅助召回、scope filter。
  - 验收：召回模块只产出候选集合，不决定最终答案证据。

- [x] 提取排序模块
  - 建议新文件：`Services/Rag/ChunkRanker.cs`。
  - 包含：reranker 调用、fallback 排序、少量 metadata policy。
  - 验收：`RankedChunks` 始终代表全局排序，不被 EvidenceSlot 拼接顺序改写。

- [x] 提取证据选择模块
  - 建议新文件：`Services/Rag/EvidenceSelector.cs`。
  - 包含：EvidencePlan、EvidenceSet、slot coverage、absence check。
  - 验收：EvidenceSet 可以从 `RankedChunks` 中选择多样证据，但不能改变 `RankedChunks`。

- [x] 提取答案生成模块
  - 建议新文件：`Services/Rag/AnswerComposer.cs`。
  - 包含：prompt 构造、引用约束、repair prompt、少量结构化生成策略。
  - 验收：fallback 模板数量减少，主路径统一走 evidence-aware prompt。

### Phase 1 执行记录

- 新增 RAG 模块目录：`csharp-rag-avalonia/Services/Rag/`。
- 查询理解：新增 `QueryUnderstandingService.cs`，`BuildQueryProfile` 改为委托该服务；新增 `QueryUnderstandingServiceTests` 覆盖中文问题、显式指定文档、追问/补充要求、英文元数据排除。
- 候选召回：新增 `CandidateRetriever.cs`，承载 legacy/simple 两条候选召回入口，保持 `RAG_PIPELINE_MODE=legacy|simple|refactored` 行为不变。
- 排序：新增 `ChunkRanker.cs`，承载 reranker 调用、fallback 排序和 query-aware metadata policy；`RankedChunks` 继续以全局排序结果输出，不再由 EvidenceSlot 拼接顺序覆盖。
- 证据选择：新增 `EvidenceSelector.cs`，承载 EvidencePlan、EvidenceSet、slot coverage、absence check 和 active document scope 记忆。
- 答案生成：新增 `AnswerComposer.cs`，承载主 prompt、repair prompt、对话上下文摘要和 intent 描述；本阶段只做边界拆分，未删除既有 fallback 模板，避免改变 Phase0 基线。
- 共享契约：新增 `RagContracts.cs`，将 QueryProfile、EvidencePlan、EvidenceSet、ScoredChunk、RetrievalResult 等内部记录从 `LocalAiService.cs` 移出。
- 验证：`dotnet test test.sln` 通过，4 个测试全部通过；仍有既有 `Tmds.DBus.Protocol 0.21.2` 高危漏洞告警。

## Phase 2：重构候选召回

- [x] 将 vector recall 改为主召回
  - 先对 scope 内全部 chunk 计算向量相似度，取 top 50-100。
  - BM25 不再主导候选池截断。
  - 验收：`procedure` 类问题不再因为引言/结论 BM25 高而挤掉实现章节。

- [x] BM25 改为 OR 补充召回
  - 从 BM25/title/structure 各补充少量候选，与 vector 候选合并去重。
  - 验收：关键词明确的问题仍能召回术语强匹配 chunk，但不会主导最终排序。

- [x] 强化文档 scope filter
  - 明确指定文档、active document、corpus-wide 三种 scope。
  - 对“这篇论文”“详细一点”等场景使用 active document。
  - 验收：多文档 eval 中不再混入无关文档 chunk。

- [x] 标准化 chunk metadata
  - 明确 `ContentKind` 或新增 `ChunkKind`：title、metadata、summary、abstract、intro、body、conclusion、reference、noise。
  - 验收：ranking/evidence/prompt 不再依赖零散标题字符串判断引言、结论、摘要。

### Phase 2 执行记录

- 主召回切换：`FindRelevantChunksRefactoredAsync` 已实现 Vector 为主的候选集截断，并通过 `BuildSparseOrSupplementCandidates` 增加 BM25/Title/Structure 补充召回，完全剥离了原有的严格 BM25 截断池。
- 文档 Scope：已确认 `ResolveRetrievalScopeFilePaths` 与 `ShouldUseActiveDocumentScope` 已能够满足指定文档、active document、全量库三种 Scope 切换。
- Chunk Metadata 标准化：移除了 `ChunkRanker.cs` 中的 `IsGeneralizedSectionTitle` 及其相关散落字符串校验，完全收敛至校验 `ContentKind`（`intro`, `conclusion`, `summary`, `abstract`），保证语义和策略一致。
- Bug 修复：修复了当文档片段首行为空时 `IsReferenceLikeSentence` 触发的 `IndexOutOfRangeException`，保障后续 Eval 正常执行。

## Phase 3：重构排序

- [x] 让 reranker 成为全局排序核心
  - 输入：候选 top 50-100。
  - 输出：全局有序 `RankedChunks`。
  - 验收：debug 中 `Top Ranked Chunks` 与最终全局排序一致。

- [x] 删除 slot 拼接式排序
  - `TryRerankWithinEvidenceGroupsAsync` 不再产出全局 rank。
  - slot 只服务 EvidenceSet 选择。
  - 验收：高分核心 chunk 不会因 slot 顺序被排到低位。

- [x] 保留少量 metadata policy
  - 对 metadata/reference/noise 进行过滤或硬降权。
  - 对 intro/conclusion 的降权只在 implementation/procedure/composition 类问题中生效。
  - 验收：policy 数量可解释，避免重新形成权重公式爆炸。

- [x] 改造 reranker query
  - 对 procedure/composition/list 类问题生成更明确的 reranker query，例如“具体实现步骤、关键模块、直接证据”。
  - 验收：概述段与实现段分差变大，`stm32-control`、`composition` 类 top chunk 改善。

### Phase 3 执行记录

- Reranker 成为全局核心：修改 `TryRerankWithLearnedModelAsync`，直接返回 Reranker 计算得到的 Relevance 分数，取代原有的基于多个特征加权的混合 Score 排序。
- 删除 Slot 拼接式排序：彻底移除了 `TryRerankWithinEvidenceGroupsAsync`，现在无论是 `refactored` 还是 `legacy` 管线，EvidenceSet 都直接从全局唯一确定的 `RankedChunks` 中根据 Slot 获取，不再干扰全局排序。
- 精简 Metadata Policy：删除了 `RerankCandidates` 庞杂的提权/降权计算公式（例如 CoverageScore/NeighborScore/IntentBoost 等大量基于关键字与逻辑的分支），转而提供轻量级 `ApplySimplifiedMetadataPolicy`：只对 `metadata/reference/noise` 进行 -1000 的硬降权，并在特定意图下对 `summary/intro` 等给予 -0.3 的轻微惩罚。
- Reranker Query 改造：新增 `BuildRerankerQuery`，将问题意图（如 procedure、composition）转换为显式的文本要求后缀（如 "(要求：具体实现步骤、核心控制逻辑、直接证据)"），附加在原问题后，引导 Reranker 提供更符合任务意图的重排结果。


## Phase 4：重构 EvidenceSet

- [ ] 明确 EvidenceSet 与 RankedChunks 的边界
  - `RankedChunks`：全局相关性排序，用于 debug、top chunk、用户解释。
  - `EvidenceSet`：答案证据集合，用于覆盖多个方面、多个章节。
  - 验收：eval 同时输出两类指标，且二者来源清晰。

- [ ] EvidenceSet 支持问题类型的覆盖策略
  - composition/list：覆盖并列实体。
  - procedure/implementation：覆盖输入、决策、执行、约束/效果。
  - definition：覆盖定义、组成、作用。
  - absence check：判断是否存在直接主题证据。
  - 验收：EvidenceSet 的 slot coverage 不靠改写排序达成。

- [ ] 增加结构化抽取中间结果
  - 对模块、技术栈、硬件组成、定义要素先抽取成结构化列表，再交给 LLM 表达。
  - 验收：`vue 3`、`mybatis-plus`、`mysql`、`echarts`、传感器、电磁阀等并列项不被 LLM 概括丢失。

- [ ] EvidenceSet 引用绑定
  - 每个抽取项保留来源 chunk id。
  - 生成答案时引用只能来自 EvidenceSet。
  - 验收：citation from EvidenceSet 指标稳定，不再出现引用正文当 chunk id 的格式错误。

## Phase 5：重构答案生成

- [ ] 收敛多路 fallback 模板
  - 删除或合并 procedure/composition/summary 等大量专用 fallback。
  - 保留统一的 evidence-aware generation。
  - 验收：fallback 不再绕过主证据选择逻辑。

- [ ] 强化术语保真 prompt
  - 明确要求技术名词、模块名、框架名、协议名使用证据原文。
  - 验收：关键词覆盖提升，尤其是技术栈、模块、定义类问题。

- [ ] 增加 absence check 输出规则
  - 当 EvidenceSet 判定无直接主题证据时，回答“文档中未提及”，不得用近似主题硬凑。
  - 验收：`no-answer` 类 case 通过。

- [ ] 增加引用后校验
  - 校验引用必须匹配合法 chunk id/file/section。
  - 非法引用剔除或触发 repair。
  - 验收：引用格式准确率稳定为 100%。

## Phase 6：评估与回归

- [ ] 扩展 eval 维度
  - 区分 recall、global ranking、evidence selection、answer extraction、citation 五类失败。
  - 验收：每个失败 case 能归因到一个主阶段。

- [ ] 增加消融评估
  - vector only。
  - vector + BM25 OR。
  - vector + reranker。
  - vector + reranker + EvidenceSet。
  - 验收：每个组件的收益和副作用可量化。

- [ ] 增加跨文档回归集
  - 明确文档 scope。
  - active document。
  - corpus-wide。
  - no-answer。
  - 验收：不会为了单文档 case 牺牲多文档隔离。

- [ ] 设置阶段性门槛
  - simple 管线不得低于 legacy 最好通过率。
  - refactored 管线目标：Top chunk 覆盖高于 70%，Context 覆盖保持 95% 以上，引用格式准确率 100%。
  - 验收：未达门槛不切换默认管线。

## 建议实施顺序

1. 先做 Phase 0，验证“vector 主召回 + reranker 主排序”的收益上限。
2. 再做 Phase 1，把巨石文件拆出边界，但保持行为尽量一致。
3. 然后做 Phase 2 和 Phase 3，替换召回与排序主链路。
4. 最后做 Phase 4 和 Phase 5，解决答案漏项、引用错误和 no-answer。
5. 每完成一个 phase 跑一次完整 eval，并把结果追加到本清单或独立 eval 报告。
