通过对仓库代码和 `.eval/20260428-183350-weavedoc-rag-baseline.md` 测评结果的深度分析，我对 WeaveDoc 的 RAG 算法架构有了全面的理解。

### 1. 测评结果分析 (`stm32-summary` 案例)
最新的测评结果显示 **16/20** 的通过率，表现稳健。
*   **痛点：** 失败的 `stm32-summary` 案例虽然检索到了最相关的 `overview` 块（Score 0.992），但失败在 **Citation（引用）** 校验上。
*   **原因：** LLM 生成的总结内容非常详实，但部分关键词（如“数据采集”）未在生成的回答中显式命中，且引用精度（Citation Precision）仅 50%。这说明当前系统在**长文本总结**和**精确引用归位**上仍有提升空间。

### 2. 当前 RAG 算法架构亮点
WeaveDoc 实现了一套非常复杂的 **Hybrid Search（混合检索）** 体系，其核心优化点包括：
*   **多维分值融合 (`FindRelevantChunksAsync`)：** 
    检索分值由 `Semantic`（向量）、`BM25`（词频）、`Keyword`（关键词命中）、`Title`（标题匹配）、`JsonStructure`（JSON 路径权重）以及 `Neighbor`（结构邻居）等 10 多个维度加权合并。
*   **结构化文档感知的切片 (Structural Chunking)：** 
    针对 JSON 和 PDF，系统能保留文档层级路径（如 `root > content > 1.1 决策层`）。在检索时，`JsonStructureScore` 会通过分析问题焦点词是否命中路径（如“架构”、“摘要”）来动态提权。
*   **意图驱动的候选池管理：** 
    根据问题意图（`summary`, `procedure` 等）动态调整 Rerank 候选池大小。例如，对于操作步骤类问题（`procedure`），系统会自动扩大候选范围（3倍），确保不漏掉长流程中的任何环节。
*   **完善的降级与修复机制：** 
    代码中包含了 `BuildRepairPrompt` 和多套 `FallbackAnswer`（如针对 STM32 精准灌溉的硬核逻辑），这极大增强了系统在极端情况下的鲁棒性。

### 3. RAG 算法架构的进一步优化建议

针对测评中暴露的引用问题和当前代码结构，我建议从以下三个方向进行架构优化：

#### A. 引入“两阶段重排序” (Two-Stage Reranking)
*   **现状：** 目前使用启发式规则进行 Rerank。
*   **优化：** 在 `FindRelevantChunksAsync` 之后引入 **Cross-Encoder**（如 BGE-Reranker）。
*   **理由：** 向量余弦相似度（Bi-Encoder）对细微语义区别不敏感。Cross-Encoder 能深度交互问题与文档，极大提升 Top-1 的准确率，从而解决 `stm32-summary` 中相关块虽然排第一但 LLM 提炼不准的问题。

#### B. 引用校验与后置对齐 (Citation Refiner)
*   **现状：** `Citation checks: fail` 是主要的扣分项。
*   **优化：** 在 LLM 生成答案后，增加一个轻量级的 **Verification Step**。
*   **具体做法：** 使用一个小模型（如 Qwen-1.5B）快速检查生成的每个断句是否真的能从引用的对应 Chunk 中推导出来。如果推导失败，则自动剔除错误引用或重新对齐。

#### C. 语义路径嵌入 (Contextual Path Embedding)
*   **现状：** 目前 JSON 路径仅作为检索时的文本补充。
*   **优化：** 在对 Chunk 进行 Embedding 之前，将 **Document Title + Section Path** 以前缀形式永久固化到文本中。
*   **理由：** 这样可以解决“指代不明”问题（如文档中多个地方提到“系统采用...”，带上路径后向量空间会更清晰），显著提升语义检索的区分度。

#### D. 自动化负反馈循环 (Auto-Evaluation Loop)
*   **现状：** `.eval` 文件夹下是手动或脚本触发的结果。
*   **优化：** 将 `.eval` 中的失败案例自动转化为 `LocalAiService` 中的 **Few-shot Prompt**。
*   **理由：** 既然 `stm32-summary` 容易错，就将正确的总结模版作为示例喂给模型，实现架构级的“自进化”。

**总结建议：** 当前架构已经具备了极强的领域针对性（尤其是对工程类 JSON 文档的处理）。下一步的重点应从“搜得到”转向“合得准”和“指得对”。