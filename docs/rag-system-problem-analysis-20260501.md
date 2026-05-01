# WeaveDoc RAG 系统问题本质分析（2026-05-01）

## 结论摘要

当前 RAG 质量问题的主因不是单纯的 LLM、Embedding 或 Reranker 模型能力不足，而是检索、排序、证据组织和答案抽取的系统设计问题。

从现有基准结果看：

- Context signal coverage：`63/64 (98.4%)`
- Citation recall：`100.0%`
- Scope accuracy：`100.0%`
- Source format accuracy：`100.0%`
- Top chunk signal coverage：`33/64 (51.6%)`
- Keyword coverage：`66/88 (75.0%)`

这些指标说明系统多数时候已经能把答案相关证据召回并放进上下文。真正的问题在于：

1. 找到了相关证据，但没有稳定把最核心证据排在最前。
2. 上下文已经包含答案，但最终回答没有完整保留关键并列项。
3. 当前管线仍偏向“相似 chunk 检索 + LLM 总结”，而不是“按问题类型规划证据 + 结构化抽取 + 生成答案”。

因此，继续为某一篇论文补关键词、补术语词表，只能短期提高当前 benchmark，通过不了换论文后的泛化检验。

## 模型因素分析

### Embedding 不是主因

Embedding 的主要职责是粗召回。当前 `Context signal coverage` 接近满分，说明 embedding 与 sparse 检索整体已经能找到答案相关片段。

如果 Embedding 是主因，典型现象应该是：

- 答案证据完全没有进入候选池；
- context 中缺少关键章节；
- 引用召回低；
- 指定文档经常跑偏。

但当前报告显示：

- 上下文几乎总能覆盖答案信号；
- citation recall 达到 `100.0%`；
- scope accuracy 达到 `100.0%`。

所以 Embedding 不是最主要瓶颈。它的问题更像是“泛相关召回太多”，例如摘要、引言、workflow、结论段都可能和问题语义相近，从而进入候选池。

换更强 Embedding 可能改善一部分排序，但不能解决组成题、技术栈题、流程题所需的结构化证据覆盖问题。

### Reranker 不是主因

Reranker 的能力边界是判断 query 与单个 chunk 的相关性。它不天然负责：

- 判断这道题需要多个证据共同回答；
- 判断哪些证据是核心证据，哪些只是泛化摘要；
- 保证技术栈题覆盖后端、前端、数据库、可视化等维度；
- 保证流程题按步骤、阶段、机制组织；
- 保证组成题不遗漏同级并列实体。

当前系统还存在一个更直接的算法问题：EvidenceSlot 的覆盖顺序曾经会影响 `RankedChunks[0]`，导致 top chunk 不再代表全局最相关证据，而是代表第一个槽位的证据。

这不是 reranker 模型能力问题，而是 reranker 的使用目标和排序结果被系统逻辑污染。

Reranker 可以作为局部排序组件，但不能替代 EvidenceSet 规划，也不能直接承担答案覆盖策略。

### LLM 有影响，但不是根因

LLM 的问题主要体现在答案生成阶段：

- 上下文中已有多个技术栈实体，但回答只保留少数几个；
- 上下文中已有多个硬件组成项，但回答只抓住某个局部句子；
- 上下文中已有定义、作用、上下位关系，但回答只抽到单句代表。

这些问题看起来像 LLM 漏项，但根因是系统没有在 LLM 前提供结构化抽取结果，也没有把任务约束成“完整保留并列实体”。

如果只是把原始 context 交给 LLM 自由生成，模型天然会压缩、概括、合并，从而丢失评测期待的关键词和并列项。

换更强 LLM 可以降低漏项概率，但不能从架构上保证稳定性。

## 当前系统的本质问题

### 1. 把论文问答简化成了相似 chunk 检索

当前 RAG 管线更接近：

```text
query -> 找相似 chunk -> 拼 context -> 让 LLM 总结
```

但论文问答需要的是：

```text
query -> 判断答案类型 -> 规划证据形态 -> 召回章节级证据 -> 覆盖关键维度 -> 结构化抽取 -> 生成答案
```

也就是说，系统缺少 answer-aware retrieval 和 evidence planning。

### 2. 排序目标不等于答案目标

相似度高的 chunk 不一定是最适合回答的 chunk。

例如“如何实现精准灌溉”这类问题，摘要或引言可能包含“精准灌溉”“传感器”“控制”等词，语义相关度很高，但真正核心证据在：

- 模糊 PID 复合控制器；
- PID 调节阶段；
- 环境补偿机制；
- 电磁阀 PWM 控制策略。

如果排序只追求 query-chunk relevance，就容易把泛化段落排在核心机制段落前面。

### 3. EvidenceSet 覆盖与 RankedChunks 排序职责混用

`RankedChunks` 应该表示全局相关性排序，用于 debug、top chunk、用户可解释性。

`EvidenceSet` 应该表示答案证据集合，用于覆盖多个槽位、多个方面、多个章节。

如果用 EvidenceSlot 顺序重写 `RankedChunks`，就会造成：

- top chunk 不是最核心证据；
- retrieval check 失败；
- debug 排名误导；
- 用户看到的首条证据不稳定。

### 4. Query intent 识别过于关键词化

当前系统依赖大量中文触发词判断 intent，例如 summary、procedure、composition、definition 等。

这种方式的问题是：

- 同一问题换一种说法可能识别失败；
- “技术栈 / 总体架构 / 架构和技术栈 / 前后端分离”这类系统设计题容易落到 general；
- 一旦 intent 错误，EvidencePlan、slot、上下文组织和答案结构都会连锁出错。

这不是某个词表缺少几个词的问题，而是 intent 体系缺少更通用的语义分类机制。

### 5. Chunk 表示缺少章节级语义

JSON 和 Markdown 被切成很多小 chunk 后，单个 chunk 很精准，但上下游关系弱。

典型后果：

- 某个局部 chunk 抢到第一；
- 父章节标题没有充分参与排序；
- 兄弟节点不能共同表达完整答案；
- 跨多个子节点的问题容易漏项。

论文问答通常需要章节级或段落组级证据，而不是孤立句子级证据。

### 6. 答案抽取没有结构化

组成题、技术栈题、硬件题、模块题不适合只抽“最高分句子”。

这些问题需要保留并列实体，例如：

- 后端框架；
- 前端框架；
- 数据库；
- 可视化工具；
- 通信协议；
- 传感器；
- 执行机构；
- 显示模块。

如果只让 LLM 从 context 自由生成，或者每个槽位只选一句最高分句子，就会稳定出现漏项。

### 7. Markdown 清洗和结构恢复不足

Markdown 虽然比 PDF 更适合入库，但不等于干净语料。

常见问题包括：

- HTML 残片；
- 图片/OCR 标签；
- 作者、单位、预发布信息混入正文；
- 参考文献混入检索；
- 标题层级丢失；
- 异常短标题或 OCR 残片被当成 section title。

这些噪声不会导致完全召回失败，但会干扰排序、上下文组织和答案抽取。

## 失败类型归纳

### A 类：召回成功，但首位排序失败

特征：

- Context signals 满分或接近满分；
- Top chunk signals 很低；
- 答案大体可答，但 retrieval check fail。

本质原因：

- 全局排序目标被 evidence slot 覆盖顺序污染；
- 泛化段落被稀疏扩展词推高；
- 章节/父子结构没有参与首位排序。

### B 类：上下文有答案，但答案漏关键信号

特征：

- Retrieval checks pass；
- Context signals 满分；
- Answer signals 或 keyword coverage 低。

本质原因：

- LLM 自由生成时压缩丢项；
- 缺少结构化实体抽取；
- 技术栈/组成/定义类问题没有稳定保留并列实体。

### C 类：Markdown 噪声干扰排序和答案

特征：

- 来源是 Markdown；
- top chunks 或 context 中出现 HTML、图片标签、作者单位、异常标题；
- 答案引用正确文档，但内容不够聚焦。

本质原因：

- 入库清洗不足；
- 标题层级恢复不足；
- metadata 和正文边界不清。

## 不应该继续做的事

不应该继续为当前 benchmark 特化补词，例如：

- 针对某篇 STM32 论文补固定硬件词；
- 针对某篇 Spring/Vue 论文补固定技术栈词；
- 针对某些 case id 写专门回答模板；
- 为了让 top chunk 命中评测信号，手动调某几个章节的权重。

这些做法会提高当前报告分数，但不解决换论文后的泛化问题。

## 应该解决的通用问题

### 1. 建立通用 answer type

Query intent 不应只是关键词触发，而应该输出稳定的答案类型：

- summary；
- definition；
- procedure；
- composition/list；
- architecture/system-design；
- comparison；
- absence-check；
- metadata。

每个 answer type 对证据的需求不同。

### 2. 建立 answer-aware evidence planning

不同问题类型应该有不同证据目标：

- summary：摘要、方法、结果、结论；
- procedure：输入、步骤、机制、输出；
- composition/list：完整并列项；
- architecture/system-design：层次结构、模块、技术栈、数据流；
- definition：定义句、作用、上下位关系；
- comparison：对比双方和比较维度。

EvidencePlan 的职责应该是规划证据形态，而不是直接决定全局排名。

### 3. 分离全局排序和证据覆盖

应该明确区分：

- `RankedChunks`：全局相关性排序；
- `EvidenceSet`：面向答案的覆盖集合；
- `ContextChunks`：给 LLM 的组织后上下文；
- `AnswerExtraction`：结构化抽取后的答案要点。

这几个对象的目标函数不同，不能互相覆盖。

### 4. 引入章节级证据单元

除了小 chunk，还需要维护：

- parent section；
- sibling chunks；
- child chunks；
- section summary；
- heading path；
- content kind；
- noise score。

检索可以先定位章节，再从章节内选择证据。

### 5. 增加结构化抽取层

在 LLM 生成最终答案前，先抽取结构化 evidence facts。

例如 composition/list 题应先得到：

```json
{
  "items": [
    {"label": "后端", "entities": ["Spring Boot", "MyBatis-Plus"]},
    {"label": "前端", "entities": ["Vue3", "Element Plus"]},
    {"label": "数据", "entities": ["MySQL"]},
    {"label": "可视化", "entities": ["ECharts"]}
  ]
}
```

最终答案再基于 facts 生成，而不是直接从原始 context 自由生成。

### 6. 用评测区分召回、排序、抽取、生成

现有指标已经能看出一些问题，但还需要更明确地区分：

- recall@k：答案证据是否进入候选池；
- rerank@1 / rerank@k：核心证据是否排前；
- evidence coverage：证据集合是否覆盖答案维度；
- extraction completeness：结构化抽取是否漏实体；
- generation fidelity：最终答案是否忠于抽取 facts。

只有拆开这些阶段，才能判断问题到底来自模型还是算法。

## 最终判断

当前问题的主因是算法和架构，占主要比例。

更具体地说：

- Embedding：召回基本够用，不是主因；
- Reranker：可作为局部排序器，但当前使用方式和目标函数不够清晰；
- LLM：会漏项，但主要是因为没有结构化抽取和答案约束；
- RAG 算法：主因，尤其是 query intent、chunk 表示、evidence planning、ranking objective、answer extraction。

下一阶段应该先修通用 RAG 管线，而不是继续为单篇论文或单个 benchmark case 补规则。
