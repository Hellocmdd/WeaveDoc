# WeaveDoc RAG问题诊断报告（2026-04-28）

## 1. 执行摘要

本轮评测显示系统在“召回可用但首位排序不稳、回退回答误杀、PDF文本噪声放大”三个层面存在耦合问题。整体表现为：

- Case通过率：7/20
- 关键词覆盖：58.0%
- Top chunk信号覆盖：53.1%
- Context信号覆盖：87.5%
- 平均引用精度：87.5%
- 平均引用召回：95.0%

关键结论：

1. 检索并非完全失效，问题主要集中在“Top1不稳定”和“回答生成/回退策略不稳”。
2. 多个失败样例属于“检索已命中关键证据，但最终回答仍失败”。
3. PDF语料断行与跨栏噪声明显拖累系统论文类问答质量。

---

## 2. 评测现象汇总

### 2.1 指标层面

- Top chunk覆盖显著低于Context覆盖（53.1% vs 87.5%），说明系统常出现“召回到但没排到第一”。
- 引用精度/召回整体较高，说明引用机制基本可用，主要短板不在“找不到引用”，而在“回答内容命中不足/结构不达标”。

### 2.2 失败类型分布

- engineering-paper：多题失败（procedure/composition/definition/explain混合）
- system-paper：架构、模块、定义、组成类问题集中失败
- definition/composition类型失败密度最高

---

## 3. 根因分析（按优先级）

## P0：Top检索判定口径过严（放大排序误差）

现象：

- 多个case中，Context命中足够，但Top chunk信号失败导致整体retrieval fail。

代码证据：

- 评测仅用 rankedChunks 的首条做top判断，见 Services/EvalRunner.cs 的 EvaluateRetrievalSignals。

影响：

- 将“Top2/Top3命中”的可用检索误判为失败，掩盖真实可用性。

---

## P0：定义/解释题存在“检索命中却返回我不知道”误杀

现象：

- stm32-definition-mcu、stm32-explain-compensation 中，Top chunk已包含直接答案，但最终回答为“我不知道（当前文档未覆盖）”。

代码证据：

- 早期unknown门控：ShouldReturnUnknownForUnsupportedRequestedDocumentTopic
- 定义回退依赖主题提取：BuildDefinitionFallbackAnswer + ExtractQuestionSubject

影响：

- 用户看到明显“答非所问/拒答”，属于可感知严重缺陷。

推断：

- 主题提取易抽到论文标题长token，而非“主控芯片/知识图谱”等实体词，导致回退匹配失败。

---

## P1：procedure/composition扩展词过宽，泛段落抢分

现象：

- “如何实现精准灌溉”等问题，Top1常落在引言或概述，而非控制策略/PWM/补偿细节段。

代码证据：

- 扩展词逻辑较泛：ExpandRetrievalTerms
- 混合打分融合：FindRelevantChunksAsync

影响：

- 造成“看起来相关但不够可答”的首段上浮，压制细节证据。

---

## P1：模块/流程回答缺少槽位约束，细节题容易漏关键项

现象：

- remote-detailed 类问题虽然检索命中，但结构检查仍失败（如遗漏APP展示或控制动作要点）。

代码证据：

- BuildModuleImplementationFallbackAnswer 以高分句拼接为主，缺少固定槽位（链路/协议/展示/控制）硬约束。

影响：

- 对“详细一点”“请分模块说明”等问题不稳定。

---

## P1：PDF提取噪声直接进入chunk，损伤system-paper问答质量

现象：

- 地质论文答案出现跨栏拼接残句、断裂短句、语义不完整。

代码证据：

- PDF抽取使用 pdftotext -layout -nopgbrk，保留版式信息同时保留较多断行噪声。

影响：

- 关键词/结构命中下降，回答可读性下降，system-paper组失分明显。

---

## P2：评测匹配以字面contains为主，语义同义不友好

现象：

- 部分答案内容方向正确，但因词形不同未计入命中。

代码证据：

- MatchExpectedKeywords、MatchSignals 均为规范化后contains匹配。

影响：

- 评测结果偏“保守”，低估真实可用性。

---

## 4. 问题与症状映射

1. Top chunk覆盖低但Context高
- 主要对应：排序稳定性不足 + Top1评测口径过严

2. 检索命中却“我不知道”
- 主要对应：unknown门控 + 主题抽取不稳

3. 系统论文类残句多
- 主要对应：PDF噪声未充分清洗

4. 细节题漏点
- 主要对应：回退生成未槽位化

---

## 5. 修复优先级建议

### 第一阶段（立即执行，1~2天）

1. 评测侧将Top检索判定从Top1放宽到TopN（建议N=3）。
2. 定义题主题提取增加短实体优先与停用词剔除（避免整句标题入槽）。
3. 对“检索已高置信命中”场景降低unknown误杀概率（增加兜底提取回答）。

### 第二阶段（短期，2~4天）

1. procedure/module/composition回退改为槽位生成：
- procedure：输入-决策-执行-结果
- module_implementation：链路-协议-展示-控制
- composition：硬件-软件-接口-存储/可视化
2. 收紧扩展词权重，降低泛词对引言段的提升。

### 第三阶段（中期，3~7天）

1. 引入PDF清洗层（去跨栏断裂、合并短行、去页眉页脚噪声）。
2. 评测增加同义词表/软匹配，降低字面误杀。

---

## 6. 验收目标（建议）

下一轮基线建议以以下目标作为修复达标线：

- Case pass count：>= 13/20
- Top chunk signal coverage：>= 70%
- Context signal coverage：>= 90%
- definition/explain题“误判我不知道”数量：趋近0
- system-paper分类通过率：>= 60%

---

## 7. 涉及核心代码位置

- 检索融合与重排：csharp-rag-avalonia/Services/LocalAiService.cs
- 上下文窗口构建：csharp-rag-avalonia/Services/LocalAiService.cs
- 回退与unknown门控：csharp-rag-avalonia/Services/LocalAiService.cs
- 评测打分与判定：csharp-rag-avalonia/Services/EvalRunner.cs

---

## 8. 结论

当前系统处于“可用召回 + 不稳排序 + 脆弱回退”的状态。优先修复Top评测口径与unknown误杀后，预计能快速抬升通过率；随后通过槽位化回答和PDF清洗，可进一步提升复杂文档问答稳定性与可解释性。
