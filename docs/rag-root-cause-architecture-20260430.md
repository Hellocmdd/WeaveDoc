# WeaveDoc RAG 根因思考与架构判断

> 日期：2026-04-30  
> 目的：在具体调参或修复前，先澄清当前 RAG 问题的本质。

## 1. 结论摘要

当前 WeaveDoc RAG 的关键瓶颈，不是 embedding 模型不够好，也不是 BM25、reranker、prompt 其中某一个模块单独失效。

更核心的问题是：

**系统把不同类型的文档问答任务都压扁成了“找 TopK 个最相关 chunk”，但真实问题需要的是“满足任务契约的证据集合”。**

换句话说，当前系统已经具备不少结构信息：

- 文档标题
- 文件路径
- 章节路径
- JSON 字段语义
- chunk 类型
- 问题意图
- 引用标签

但这些信息大多只是参与打分的特征，还没有升级成检索和回答阶段必须遵守的结构约束。因此系统会反复出现：

- 相关 chunk 被找到，但不是最适合回答的证据。
- 正确证据在 context 中，但回答没有覆盖关键槽位。
- 同源 JSON/PDF 同时竞争，PDF 噪声进入答案。
- 指定文档只是软加分，多文档场景仍可能混淆。
- reranker 把“语义相似”的引言或摘要排到“真正可回答”的实现章节前面。

## 2. 现象不是根因

评测报告里出现的失败现象包括：

- Top chunk 信号覆盖低。
- Context 信号覆盖明显高于 Top chunk。
- procedure 类问题常命中引言或结论，而不是实现章节。
- composition 类问题漏掉并列组成项。
- 多文档问题会混入其他文档内容。
- “未提及”类问题没有稳定回答“我不知道”。
- 引用偶尔出现格式或归属异常。

这些都是重要现象，但它们不是同一层的问题。

如果只从现象出发，很容易得到碎片化修复：

- 降低引言权重。
- 提高 PID 章节权重。
- 扩大 TopK。
- 加强 prompt。
- 修改引用正则。
- 给某些关键词加特判。

这些修复能改善个别 case，但会继续产生互相牵扯。原因是系统没有先回答一个更基础的问题：

**为了回答当前问题，什么样的证据才算合格？**

## 3. 当前架构的核心错位

当前主流程可以概括为：

```text
用户问题
  -> 归一化
  -> intent / focus terms
  -> sparse + semantic 候选排序
  -> 规则重排 / learned reranker
  -> TopK + 邻近 chunk 组成上下文
  -> LLM 生成
  -> 修复 / fallback
```

这个流程隐含了一个假设：

**只要找到最相关的几个 chunk，LLM 就能组织出正确答案。**

但评测中失败最多的 case 恰好说明这个假设不成立。

### 3.1 相关性不等于可回答性

“这个智能浇花系统如何实现精准灌溉”这类问题中，引言段会大量出现“精准灌溉”“土壤湿度”“环境变化”等词，因此语义和词面都很相关。

但它只是问题背景，不是实现证据。

真正可回答的证据应该来自：

- 模糊控制阶段
- PID 调节阶段
- 环境补偿机制
- 电磁阀 / PWM 执行控制

也就是说，正确答案需要的是“实现链路证据”，不是单个最相似段落。

### 3.2 TopK 不等于证据集合

“硬件组成包括哪些部分”不是要找一个最像“硬件组成”的 chunk，而是要覆盖多个槽位：

- 主控 / 决策层
- 传感 / 感知层
- 执行 / 电磁阀
- 显示 / OLED
- 通信 / ESP-01S WiFi

普通 TopK 排序可能把某个 workflow chunk 排到前面，因为它包含“硬件状态”等相近词。但这个 chunk 即使相关，也不能代表完整硬件组成。

这类问题需要的是 slot coverage，而不是 rank coverage。

### 3.3 文档匹配不等于文档作用域

当前系统能识别部分“指定文档”表达，并给目标文件加分。但这仍然是软约束。

在多文档 RAG 中，“用户正在问哪篇文档”不是一个排序特征，而应该是检索作用域。

特别是这些表达：

- “这篇论文”
- “这个系统”
- “对于 STM32 这篇论文”
- “不要回答浇花系统相关内容”

它们都应该影响 active document scope，而不是只影响 chunk 分数。

如果没有稳定的 scope，跨文档混淆是架构必然结果，不是偶发错误。

### 3.4 reranker 不是最终裁判

BGE-Reranker 这类 cross-encoder 很适合判断 query 和 passage 的语义相关性，但它不知道当前问题需要哪类证据。

例如：

- 对 procedure 问题，它可能偏好“高度概括实现目标”的引言。
- 对 summary 问题，它可能偏好英文摘要切片。
- 对 system-paper 问题，它可能偏好关键词密集段，而不是架构或模块说明段。

因此 learned reranker 不应该拥有推翻证据类型约束的最终权力。

更合理的定位是：

**reranker 只能在同一证据类型、同一文档作用域、同一候选组内排序。**

它可以解决“同类证据谁更好”，但不能决定“这道题需要什么证据”。

## 4. 缺失的中间层：Evidence Contract

当前系统缺少一个明确的中间对象，可以称为 `EvidenceContract` 或 `EvidencePlan`。

它应该把用户问题转成回答前必须满足的证据契约。

建议包含以下字段：

```text
EvidencePlan
  Scope
  EvidenceMode
  RequiredSlots
  PreferredSections
  AllowedContentKinds
  DisallowedContentKinds
  SourcePreference
  RerankerPolicy
  SufficiencyRule
  UnknownPolicy
```

### 4.1 Scope

表示本轮检索应该在哪些文档内发生。

可能取值：

- explicit document：用户明确指定标题。
- active document：上一轮或当前会话已形成上下文文档。
- file format scoped：用户指定 JSON / PDF。
- corpus-wide：用户明确要求全库查找或没有足够线索。

关键点：

**Scope 应该先于 chunk 排序生效。**

指定文档时，默认不应让其他文档参与竞争。只有用户明确要求跨文档比较或补充时，才扩大范围。

### 4.2 EvidenceMode

表示当前问题需要哪类证据。

可先支持这些模式：

- `summary`：摘要、概述、主线、结论。
- `implementation`：实现步骤、控制链路、算法机制。
- `composition`：组成项、硬件/软件/模块/接口。
- `module_list`：功能模块清单。
- `definition`：概念身份、职责、定义句。
- `compare`：两侧对象和比较维度。
- `absence_check`：目标主题是否存在。
- `metadata`：标题、作者、摘要、关键词等字段。

这些模式不应该只是 prompt 指令，而应该影响候选生成、证据选择和充分性判断。

### 4.3 RequiredSlots

对于多点问题，必须显式建槽位。

示例：

```text
硬件组成:
  - 主控/处理
  - 传感/采集
  - 执行/驱动
  - 显示/交互
  - 通信/联网
```

```text
精准灌溉实现:
  - 输入数据
  - 控制决策
  - 精细调节
  - 环境补偿
  - 执行机构
```

```text
功能模块:
  - 模块名
  - 模块职责
  - 模块之间的数据或调用关系
```

如果一个答案需要覆盖多个槽位，Top1 再准也不够。系统应选择覆盖槽位的证据集合，而不是选择分数最高的若干 chunk。

### 4.4 PreferredSections

章节路径应该从弱特征升级成强偏好。

例如：

- procedure / implementation 优先深层方法、控制、流程、策略、机制章节。
- summary 优先摘要、概述、结论、overview，但要避免不必要的英文摘要。
- composition 优先总体设计、层级结构、模块、硬件/软件组成章节。
- metadata 只允许元数据字段。
- absence_check 先查目标主题和同义主题，而不是返回语义相近段落。

这能解决“引言相关但证据类型不对”的问题。

### 4.5 AllowedContentKinds / DisallowedContentKinds

不同问题应有不同的内容类型边界。

例如：

- 中文 summary 默认不使用 `englishAbstract`，除非用户明确问英文摘要。
- metadata 问题允许 metadata。
- 普通问答禁用作者、单位、参考文献、DOI 等 metadata。
- procedure 问题降低 abstract / intro / conclusion 作为主证据的资格。
- 同源 JSON 与 PDF 同时存在时，结构化 JSON 优先，PDF 只作为补充或无 JSON 时使用。

这些不能只靠分数微调，因为噪声 chunk 一旦进入上下文，LLM 仍可能使用。

### 4.6 RerankerPolicy

reranker 应受 EvidencePlan 约束。

建议策略：

```text
filter by scope
  -> group by evidence type / slot
  -> retrieve candidates per group
  -> rerank within each group
  -> select evidence set by slot coverage and source quality
```

不要让 learned reranker 直接对所有候选做最终排序。

原因很简单：

**它能判断相似性，但不能理解任务契约。**

### 4.7 SufficiencyRule

回答前应判断证据是否足够。

示例：

- definition：至少一个定义/身份句命中主题。
- composition：至少覆盖 N 个组成槽位。
- implementation：至少覆盖输入、决策、执行中的若干关键槽位。
- module_list：至少抽到多个模块名或原文明确的模块列表。
- absence_check：目标文档内没有主题命中，且没有足够同义主题支持。

如果不满足，就不要让 LLM 硬答。

## 5. 对已有报告结论的修正

此前报告说“主要问题在检索层和排序层，不是 embedding 模型本身”。这个方向基本正确，但还不够精确。

更准确的说法是：

**问题发生在从候选 chunk 到可回答证据集合的转换层。**

检索层负责找到可能相关的材料；排序层负责局部排序；生成层负责组织语言。但系统缺少一个层来决定：

- 这些材料是不是同一篇文档的？
- 它们是不是这个问题需要的证据类型？
- 它们是否覆盖了必要槽位？
- 是否应该拒答？
- 是否允许某类 chunk 进入最终上下文？

因此，单独优化任何一层都会有限：

- 只调 BM25：会影响召回，但不能理解证据类型。
- 只加 reranker：会提高相似性，但可能压低真正证据。
- 只扩大 TopK：会增加正确证据出现概率，也会增加噪声。
- 只改 prompt：LLM 仍会被错误上下文牵引。
- 只加 fallback：容易继续堆 case-specific 规则。

## 6. 建议的新主流程

建议把主流程调整为：

```text
用户问题
  -> Query Understanding
     - 解析问题主题
     - 解析文档指代
     - 解析回答任务
     - 识别是否为追问

  -> Scope Resolution
     - explicit document
     - active document
     - format preference
     - corpus-wide fallback

  -> Evidence Planning
     - EvidenceMode
     - RequiredSlots
     - PreferredSections
     - ContentKind policy
     - Source preference
     - Sufficiency rule

  -> Candidate Retrieval
     - 按 scope 过滤
     - 按 slot / evidence type 召回
     - 稀疏、语义、结构信号仍可使用

  -> Evidence Selection
     - 同槽位内 rerank
     - 跨槽位做 coverage selection
     - 去重、去噪、同源优先
     - 输出最终 EvidenceSet

  -> Sufficiency Check
     - 足够：生成答案
     - 不足：回答未覆盖或我不知道

  -> Generation
     - 只基于 EvidenceSet
     - 引用必须来自 EvidenceSet

  -> Citation Validation
     - 校验引用格式
     - 校验引用是否存在于 EvidenceSet
```

这条链路的重点不是增加复杂度，而是把原来混在一个排序分数里的职责拆开。

## 7. 最小可行改造顺序

如果要逐步落地，不建议一开始大拆全部代码。可以先做四个最小但方向正确的改造。

### 第一步：引入 Scope 硬边界

目标：

- 用户明确指定文档时，检索先硬过滤到目标文档。
- 用户指定 JSON/PDF 时，优先对应格式。
- 追问继承上一轮 active document。
- 只有无 scope 时才全库竞争。

验收：

- `geology-modules-no-cross-doc` 不再混入 STM32。
- 指定 STM32 的问题不再让同名 PDF 噪声和 JSON 无限制竞争。

### 第二步：让 EvidenceMode 影响候选资格

目标：

- procedure 主证据优先深层 body 章节，intro/abstract 只能辅助。
- summary 可用 abstract/overview/conclusion，但中文问题默认排除 englishAbstract。
- composition 进入 slot retrieval。
- metadata 只查元数据字段。

验收：

- `stm32-control` 不再把引言作为主证据。
- `stm32-summary` 不再把英文摘要切片作为主要上下文。

### 第三步：实现 Slot Evidence Selection

目标：

- composition / module_list / implementation 不再直接用 TopK。
- 每类问题先定义槽位，再为每个槽位选 0-1 个最佳证据。

验收：

- `stm32-composition-hardware` 能覆盖传感、执行、显示、通信。
- `geology-system-modules` 能覆盖多个功能模块，而不是只答一两个关键词。

### 第四步：reranker 降级为组内排序器

目标：

- reranker 不再对全局候选做最终裁判。
- 先按 scope、evidence type、slot 分组，再在组内排序。

验收：

- learned reranker 开启和关闭时，证据类型不应大幅漂移。
- 评测不应出现 16/20 到 8/20 这种大幅结构性波动。

## 8. 关于评测指标

当前评测有价值，但还在用 chunk 信号间接衡量证据质量。

建议后续增加更贴近 EvidencePlan 的指标：

- scope accuracy：是否只使用目标文档。
- evidence kind accuracy：主证据类型是否正确。
- slot coverage：组成/模块/流程槽位覆盖率。
- sufficiency accuracy：该回答时回答，该拒答时拒答。
- citation validity：引用是否来自最终 EvidenceSet。
- answer grounding：答案中的关键声明是否能映射到证据。

Top chunk signal coverage 仍可保留，但它不应是唯一核心指标。对多槽位问题，Top1 本来就不是完整目标。

## 9. 最终判断

WeaveDoc 当前已经不是“搜不到”的阶段。

它的问题更接近：

**搜到了一批可能相关的材料，但没有稳定地把它们组织成符合问题任务的证据集合。**

因此，下一步真正重要的不是继续堆权重，而是把以下概念变成一等公民：

- 文档作用域
- 证据类型
- 证据槽位
- 证据充分性
- reranker 权限边界
- 引用与证据集合的一致性

一句话总结：

**从“TopK chunk 检索系统”升级为“EvidencePlan 驱动的文档问答系统”。**

