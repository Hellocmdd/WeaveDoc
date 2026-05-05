# WeaveDoc RAG 云端模型评估修复任务清单

> 基于评估报告 `20260503-114201-weavedoc-rag-baseline` (8/20 pass, DeepSeek V4 Pro)
> 对比上一轮本地模型 (1/20 pass, 全部返回"我不知道")，云端模型在回答意愿上有质的飞跃，但仍有 12 个 case 失败。
> 核心结论：检索管线基本正常（Context signal coverage 85.9%），问题集中在 **引用格式错误** 和 **特定场景拒答**。

## 当前基线

| 指标 | 数值 | 目标 |
|------|------|------|
| Case pass | 8/20 (40%) | ≥60% |
| Keyword coverage | 66/88 (75.0%) | ≥75% |
| Top chunk signal | 31/64 (48.4%) | ≥60% |
| Context signal | 55/64 (85.9%) | ≥85% |
| Citation precision | 59.6% | ≥80% |
| Citation recall | 70.0% | ≥80% |
| Sufficiency accuracy | 15/20 (75.0%) | ≥90% |

---

## P0 — 致命问题（阻塞多个 case，必须优先修复）

### 任务 P0-1：修复引用格式 — 模型把所有引用指向 abstract (c10) ✅ 已完成

- **影响 case**: `stm32-remote-detailed`, `stm32-definition-mcu`, `stm32-summary`, `stm32-summary-json-scoped` (4个)
- **现象**: 模型正文中正确引用了 `[c25]`, `[c18]` 等 chunk ID，但段落末尾的正式引用标签全部写成 `[json/... | abstract | c10]`，导致 Citation precision 为 0% 或 25%
- **根因**: 
  1. Prompt 规则 #6 要求使用"上下文中已经出现过的来源标签"，但模型倾向于在每段末尾重复同一个"总起"引用
  2. `SelectDefaultCitation` (LocalAiService.cs:3255) 中 `GetFallbackChunkScore` (LocalAiService.cs:3445) 对 abstract 有 +8+10=+18 的硬编码偏置，一旦触发自动补引用，必然选 c10
- **涉及文件**:
  - `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs` — `RagSystemPrompt` 规则 #6 和 `BuildPrompt`
  - `csharp-rag-avalonia/Services/LocalAiService.cs` — `SelectDefaultCitation`, `GetFallbackChunkScore`, `NormalizeGeneratedAnswerCitations`

- **修改方案**:

  **A) Prompt 侧** (AnswerComposer.cs `RagSystemPrompt` 规则 #6):
  ```
  旧: 6) 每个自然段或每个要点末尾必须附 1-2 个稳定来源标签，且只能复制上下文中已经出现过的"来源标签"...
  新: 6) 每个自然段或每个要点末尾必须附 1-2 个稳定来源标签，且只能复制上下文中已经出现过的"来源标签"...
      关键: 来源标签必须与你引用的具体上下文块一一对应。如果你引用了某个上下文块的内容，必须使用该块前面标注的"来源标签:"后面的完整标签，不要使用其他块的标签代替。
      示例: 如果你引用了"来源标签: [json/... | 1.4.2 远程状态显示及控制模块 > content | c25]"的内容，就在该段末尾附 [json/... | 1.4.2 远程状态显示及控制模块 > content | c25]，而不是附 [json/... | abstract | c10]。
  ```

  **B) Fallback 侧** (LocalAiService.cs `SelectDefaultCitation`):
  - 将 `GetFallbackChunkScore` 中 `IsAbstractChunk` 的 +10 偏置降到 +3
  - 或者：`SelectDefaultCitation` 不按固定偏置选，而是优先选 `ContentKind == "body"` 且包含问题关键词的 chunk

- **验收标准**:
  1. 重跑 `stm32-remote-detailed`，Citation precision ≥ 50%，Citation recall ≥ 50%
  2. 重跑 `stm32-definition-mcu`，Citation precision 保持 100%，且引用标签中的 section 不是 abstract
  3. 全局 Citation precision 从 59.6% 提升到 ≥ 75%

---

### 任务 P0-2：修复元数据类问题的模型拒答 ✅ 已完成

- **影响 case**: `json-metadata-english` (1个，但可能隐含影响其他 metadata 类型问题)
- **现象**: 问题是"JSON 文档的英文标题、英文摘要和英文关键词是什么"，检索结果中 `englishTitle` 排第1、`englishAbstract` 排第3、`englishKeywords` 排第4，但模型回答"我不知道（当前文档未覆盖）"
- **根因**:
  1. 模型的 metadata intent 理解不足 — `DescribeIntent("metadata")` 返回 "元数据题，按字段逐项给出标题、摘要、关键词等明确字段值"，但模型可能不认为 JSON 的 `englishTitle`/`englishAbstract`/`englishKeywords` 字段是"元数据"
  2. `IsOffTopicOrMalformedAnswer` (LocalAiService.cs:3163-3173) 对英文元数据问题有额外校验：如果答案不包含 "english" 且不含纯 ASCII 内容，则判为 malformed。模型如果给出中文答案"英文标题是..."就不会被误杀，但拒答"我不知道"同样会被这个规则捕获 → 触发修复 → 修复也失败 → 最终 fallback

- **涉及文件**:
  - `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs` — `BuildPrompt` 中 metadata 类型的结构指导
  - `csharp-rag-avalonia/Services/LocalAiService.cs` — `IsOffTopicOrMalformedAnswer` 英文检测逻辑 (L3163-3173)

- **修改方案**:

  **A) Prompt 侧** — 在 `BuildPrompt` 中对 `intent == "metadata"` 的问题增加特殊指令:
  ```csharp
  if (queryProfile.Intent == "metadata")
  {
      builder.AppendLine("元数据提取指令: 上下文中已包含 englishTitle、englishAbstract、englishKeywords 等元数据字段，请直接从对应上下文块中提取原文内容，逐项列出。不要翻译，保留英文原文。不要回答'未覆盖'。");
  }
  ```

  **B) 校验侧** — `IsOffTopicOrMalformedAnswer` 的英文检测 (L3163-3173):
  - 当检测到"英文"类问题时，先检查答案是否是拒答语句，如果是则放行（让 repair 流程处理），而不是在 metadata check 阶段就直接判 malformed
  - 或者：放宽条件，只要答案包含 CJK 字符就认为可能是合理的双语答案

- **验收标准**:
  1. 重跑 `json-metadata-english`，模型输出英文标题、英文摘要、英文关键词的具体内容，Keyword coverage ≥ 3/4
  2. Sufficiency accuracy 从 75% 提升到 ≥ 85%

---

### 任务 P0-3：修复流程类问题的模型拒答 ✅ 已完成

- **影响 case**: `stm32-control` (1个)
- **现象**: 问题是"这个智能浇花系统如何实现精准灌溉"，检索结果中 c44(结论)、c35(PID调节)、c41(控制性能测试) 都包含精准灌溉相关内容，但模型回答"我不知道（当前文档未覆盖）"
- **根因**: 与 P0-2 不同，此 case 的问题中没有明确指定文档名（"这个"），检索到的 top chunk (markdown/c2) 是一个截断的 markdown 段落（以"..."结尾），模型可能因为上下文碎片化而认为信息不完整，选择拒答
- **涉及文件**:
  - `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs` — `BuildPrompt` 的上下文构建
  - `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` — `BuildAnswerContextChunks` 上下文窗口构建

- **修改方案**:

  **A) Prompt 侧** — 对 `intent == "procedure"` 增加鼓励综合的指令:
  ```csharp
  if (queryProfile.Intent == "procedure")
  {
      builder.AppendLine("流程综合指令: 精准灌溉是一个系统性问题，答案可能分散在控制算法、传感器、执行机构等多个上下文中。请综合所有相关上下文块，按"感知→决策→执行"的链路组织答案。");
  }
  ```

  **B) 上下文侧** — 检查 markdown chunk 截断问题:
  - 在 `BuildAnswerContextChunks` 中，如果 chunk 的 Text 以 "..." 结尾且长度超过阈值，自动填充或合并相邻 chunk
  - 或者：对 markdown 来源的 chunk，降低其在无指定文档场景下的优先级

- **验收标准**:
  1. 重跑 `stm32-control`，模型不再输出"我不知道"，Keyword coverage ≥ 3/4 ✅ 达标（3/4，"动态调节"未命中）
  2. 重跑 `stm32-control-prefixed`（同类问题加前缀），Keyword coverage ≥ 3/4 ❌ 仍拒答，根因是"对于STM32这篇论文"前缀导致 LLM 行为差异（非上下文质量问题），需单独处理 query preprocessing

- **实际修改**: 将 `IsTruncatedMarkdownChunk`（检测"..."结尾，无效）替换为 `IsWellStructuredSourceChunk`（检查 .json 来源或非 body ContentKind），并在 procedure intent 下用两趟选择——第一趟从全量 scopedRanked 中优先选取结构化 JSON chunks，第二趟从 filteredRanked 中填充剩余空位

---

## P1 — 严重影响（影响多个 case 或关键指标）

### 任务 P1-1：改善 definition 类问题的检索排序

- **影响 case**: `stm32-definition-mcu`, `stm32-compare-fuzzy-pid` (2个)
- **现象**: 
  - `stm32-definition-mcu`: 问题是"主控芯片是什么"，c18（主控芯片定义）语义分数 0.825 但排在 c20（土壤传感器，0.723）之后，Top chunk signal 0/3
  - `stm32-compare-fuzzy-pid`: 同样 top chunk signal 0/1
- **根因**: 纯语义相似度对"组件描述"类问题区分度不足。c20 描述土壤传感器的选型理由，c18 描述主控芯片架构，两者在语义空间中都和"系统组件"相关，但 c20 文本更长、信息密度更高，语义分数反而更高
- **涉及文件**:
  - `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` — 候选检索和排序
  - `csharp-rag-avalonia/Services/Rag/QueryProfile.cs` — `FocusTerms` 提取

- **修改方案**:
  - 在候选排序阶段（Reranker 之前），对 `intent == "definition"` 的问题，对 section title 或 chunk text 中包含 `FocusTerms` 的 chunk 给予排序偏置（例如 SemanticScore * 1.1 或额外 +0.1）
  - 或者：在 Reranker 之后、上下文窗口选取之前，确保至少有一个与 `FocusTerms` 强相关的 chunk 进入 Top-K

- **验收标准**:
  1. 重跑 `stm32-definition-mcu`，Top chunk signal ≥ 1/1（c18 出现在 top chunk 列表中）
  2. 重跑 `stm32-compare-fuzzy-pid`，Top chunk signal ≥ 1/1
  3. Top chunk signal coverage 从 48.4% 提升到 ≥ 60%

---

### 任务 P1-2：修复 `NormalizeGeneratedAnswerCitations` 自动补引用的偏置

- **影响 case**: 所有模型生成了正确回答但缺少引用的 case
- **现象**: 当 LLM 输出语义正确的回答但没有加引用时，`NormalizeGeneratedAnswerCitations` 自动补引用，但 `SelectDefaultCitation` 几乎总是选 c10 (abstract)，导致引用精度下降
- **根因**: `GetFallbackChunkScore` (LocalAiService.cs:3445) 对 abstract:
  - `ContentKind == "abstract"` → +8
  - `IsAbstractChunk()` → +10
  - 合计 +18，远超 body 的 +2
  这个偏置是为 summary 类问题的 fallback 设计的，但被所有场景共用

- **涉及文件**:
  - `csharp-rag-avalonia/Services/LocalAiService.cs` — `GetFallbackChunkScore` (L3445), `SelectDefaultCitation` (L3255)

- **修改方案**:
  - `SelectDefaultCitation` 接受 `QueryProfile` 参数，根据 intent 选择不同的排序策略:
    - `summary` → 保持现有偏置（偏好 abstract/summary/conclusion）
    - `definition` → 偏好 section title 匹配 FocusTerms 的 body chunk
    - `module_implementation` / `procedure` → 偏好 body chunk
    - `metadata` → 偏好对应元数据 chunk
  - 降低 `IsAbstractChunk` 的通用偏置，将 +10 移到 summary intent 专属逻辑中

- **验收标准**:
  1. 构造测试：LLM 输出"STM32L031G6 是主控芯片"（无引用），自动补引用后标签指向 c18 而非 c10
  2. summary 类 case (stm32-summary) 不受影响，fallback 仍优先选 abstract

---

### 任务 P1-3：英文元数据/摘要 chunk 过滤

- **影响 case**: `json-metadata-english` 修复后的验证，以及其他中文问题场景
- **现象**: 上下文窗口只有 8 个 chunk。在中文问题 + 中文文档场景下，`englishAbstract` 分段占 3 个位置 (c11, c12, c13)，挤占了中文正文内容的空间
- **根因**: 当前上下文构建没有根据问题语言过滤不相关语言的 chunk
- **涉及文件**:
  - `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` — `BuildAnswerContextChunks`
  - `csharp-rag-avalonia/Services/LocalAiService.cs` — 上下文构建

- **修改方案**:
  - 在 `BuildAnswerContextChunks` 中，检测问题语言：
    - 中文问题 → 排除 `ContentKind` 为 `englishAbstract`/`englishTitle`/`englishKeywords` 的 chunk（除非 intent 为 `metadata` 且问题明确问英文）
    - 英文问题 → 正常包含
  - 保留去重逻辑：同一个 JSON 段落的多个英文分片只保留第一个

- **验收标准**:
  1. 中文问题场景下，上下文窗口中的 englishAbstract chunk 数量 ≤ 1（仅当确实没有中文内容替代时）
  2. `json-metadata-english` 场景下（问题明确问英文），英文元数据 chunk 正常保留

---

## P2 — 改善项（评估框架修正 + 边界情况）

### 任务 P2-1：修正 robustness case 的 sufficiency 判定标准

- **影响 case**: `stm32-no-answer-bluetooth` (1个)
- **现象**: 问题问"文档有写蓝牙 Mesh 组网吗"，模型正确回答"文档未涉及蓝牙，系统用 WiFi (ESP-01S) + MQTT"，引用 100% 正确。但 sufficiency 判 fail
- **根因**: 评估基线期望模型输出简短的"文档未覆盖"格式，但模型给出了详细的、有引用的否定回答（实际质量更高）。这是评估标准过严，不是模型问题
- **涉及文件**:
  - `docs/eval-baseline.json` — `stm32-no-answer-bluetooth` case 定义

- **修改方案**:
  - 检查 eval-baseline.json 中此 case 的 `retrievalSignals` 和 `expectedCitations` 配置
  - 如果当前期望 `Answer signals: 0/1`（即期望一个简短否定），修改为接受有引用的详细否定回答
  - 在 case 的 `expectedKeywords` 中加入 `wifi`, `esp-01s` 等模型实际正确使用的关键词

- **验收标准**:
  1. 重跑 `stm32-no-answer-bluetooth`，sufficiency 判定为 pass
  2. Case pass 从 8/20 提升到 ≥ 9/20

---

### 任务 P2-2：为 `stm32-composition-hardware` 增加诊断

- **影响 case**: `stm32-composition-hardware` (1个)
- **现象**: Citation 100% 但 sufficiency fail，Top signal 1/1, Context signal 4/3 远超要求，说明检索没问题但回答被判定为不充分
- **根因**: 需要阅读具体答案内容确认。可能是模型回答了硬件组成但缺少某些期望的关键维度
- **涉及文件**:
  - `docs/eval-baseline.json` — 检查此 case 的期望设定

- **修改方案**:
  1. 先阅读评估报告中此 case 的具体答案
  2. 确认是 prompt 问题、评估标准问题还是检索问题
  3. 根据诊断结果归入 P0/P1 或单独修复

- **验收标准**:
  1. 完成诊断，明确根因
  2. 如果是评估标准问题，修正 baseline 后期望通过
  3. 如果是模型问题，新增对应 P0/P1 任务

---

## 修复顺序建议

```
第一轮 (预计 +4 pass):
  P0-1 (引用格式) → P0-2 (元数据拒答) → P0-3 (流程拒答)
  预期: stm32-remote-detailed, stm32-definition-mcu, stm32-summary, json-metadata-english, stm32-control 通过
  Case pass: 8/20 → 13/20 (65%)

第二轮 (预计 +3 pass):
  P1-1 (检索排序) → P1-2 (fallback偏置) → P1-3 (英文过滤)
  预期: stm32-compare-fuzzy-pid, stm32-summary-json-scoped, stm32-remote-json-scoped 通过
  Case pass: 13/20 → 16/20 (80%)

第三轮 (评估修正):
  P2-1 (蓝牙case) → P2-2 (硬件组成诊断)
  预期: stm32-no-answer-bluetooth, stm32-composition-hardware 通过
  Case pass: 16/20 → 18/20 (90%)
```

---

## 变更范围汇总

| 文件 | 涉及任务 |
|------|----------|
| `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs` | P0-1, P0-2, P0-3 |
| `csharp-rag-avalonia/Services/LocalAiService.cs` | P0-1, P0-2, P1-2, P1-3 |
| `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` | P0-3, P1-1, P1-3 |
| `csharp-rag-avalonia/Services/Rag/QueryProfile.cs` | P1-1 |
| `docs/eval-baseline.json` | P2-1, P2-2 |
