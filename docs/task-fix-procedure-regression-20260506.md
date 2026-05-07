# 修复 Procedure 类问题退化 — 任务清单

> 创建日期: 2026-05-06
> 状态: 待实施
> 退化来源: 第四轮/第五轮修复的 CandidateRetriever + AnswerComposer + LocalAiService 改动引入了对 procedure 问题的过度优化，导致 stm32-control-prefixed 和 stm32-summary 答案质量严重退化（18/20 → 16/20）

## 退化度量

| 指标 | 之前 (21:51) | 现在 (23:03) | 变化 |
|------|-------------|-------------|------|
| Case pass | 18/20 | 16/20 | -2 |
| Keyword coverage | 80.6% | 75.3% | -5.3pp |
| Citation recall | 100% | 95.0% | -5.0pp |

| 退化案例 | 之前 keywords | 现在 keywords | 根因 |
|----------|-------------|-------------|------|
| stm32-control-prefixed | 5/4 PASS | 1/4 FAIL | Pass 1 泛化 chunk 抢占名额 + 元格式包装 |
| stm32-summary | 2/2 PASS | 1/2 FAIL | 待确认，疑与 scoring 变更或上下文窗口被挤占有关 |

---

## 根因分析

对比 `stm32-control-prefixed` 前后答案：

**之前**（21:51）：直接切入技术主题，引用 8 个 chunk（abstract, innovations, PID调节, 温湿度耦合模型, 实时补偿策略, 控制性能测试, 结论），关键词全命中。

**现在**（23:03）：以元描述开头——"可以按'输入/前提 → 判断或处理 → 执行动作 → 结果/效果'的链路来理解"，引用的 3 个 chunk 中 c27(workflow) 出现 3 次，缺失 "模糊PID"、"电磁阀"、"土壤湿度"、"PWM" 等核心技术术语。

三个改动共同导致退化：

1. **CandidateRetriever.cs L164-196（主因）**：新增 Pass 1 按 `ProcedureSectionBoostTerms`（14 个泛化词：控制、调节、补偿、策略...）匹配 section title。匹配面太广，泛化的 workflow/主程序循环 chunk 抢满了 `limit` 个名额，将 `3.2 PID调节阶段设计`、`3.3.1 温湿度耦合模型`、`3.3.2 实时补偿策略` 等核心技术 chunk 挤出上下文窗口。

2. **LocalAiService.cs L4789-4794（次因）**：新增 procedure AnswerStructureRule — "先点明系统要达成的核心目标，再围绕该目标梳理从感知/输入→决策/处理→执行→输出的闭环链路" — 引导 LLM 输出"可以按...链路来理解"的元格式包装，而非直接陈述技术内容。

3. **AnswerComposer.cs L47（辅助因子）**：流程综合指令加了 "优先梳理与问题核心目标直接相关的主功能流程，而非外围辅助链路"，进一步促使 LLM 过度筛选。

---

## 任务列表

### R0-1：收紧 CandidateRetriever Pass 1 名额

- **涉及文件**: `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs`
- **位置**: `BuildAnswerContextChunks` 中 procedure 块的 Pass 1（L164-196）
- **问题**: Pass 1 当前占满全部 `limit` 个名额，导致 Pass 2（结构化 chunk）和 Pass 3（filteredRanked 补足）没有空间
- **修改方案**:
  - **方案 A（推荐）**：Pass 1 最多取 `Math.Min(limit / 3, 4)` 个 chunk，其余名额留给 Pass 2 + Pass 3
  - **方案 B**：Pass 1 的 `combinedTerms` 不使用全部 `ProcedureSectionBoostTerms`（14 个），改为只用 `queryProfile.FocusTerms` 与 section title 做交集
  - **方案 C**：回退 Pass 1 逻辑到上一版本（仅 Pass 2 + Pass 3 两趟），Pass 1 作为实验性优化暂时关闭
- **验收标准**:
  1. `stm32-control-prefixed` 上下文窗口中 `3.2 PID调节阶段设计`、`3.3 环境补偿机制` 章节的 chunk 至少出现 2 个
  2. c27(workflow) 在上下文窗口中不占据主导地位（不超过 2 个）
  3. 全局 Keyword coverage 回到 ≥ 78%

---

### R0-2：去掉 procedure 回答结构规则的元格式引导

- **涉及文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: `BuildAnswerStructureRule`（L4789-4794）
- **问题**: "先点明系统要达成的核心目标，再围绕该目标梳理从感知/输入→决策/处理→执行→输出的闭环链路" 导致 LLM 把链路描述本身当作答案，而不是用链路作为组织框架来填充技术内容
- **修改方案**:
  - 将 detailed 版本改为:
    ```
    如果是在问流程或实现方式，按"核心目标 → 关键机制 → 具体步骤 → 执行方式 → 效果"的结构组织。直接陈述每个环节中系统实际使用的算法、组件、参数和结果，不要用"可以按...链路来理解"等元描述开头。
    ```
  - non-detailed 版本同理调整
- **验收标准**:
  1. `stm32-control-prefixed` 答案不以"可以按...链路来理解"等元描述开头
  2. 答案第一句话直接包含核心技术术语（模糊PID / 电磁阀 / 传感器等）

---

### R0-3：回退或弱化 AnswerComposer 流程综合指令

- **涉及文件**: `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs`
- **位置**: `BuildPrompt` 中 procedure intent 指令（L47）和 `BuildContextualPrompt` 中对应指令（L144）
- **问题**: "优先梳理与问题核心目标直接相关的主功能流程，而非外围辅助链路" 可能促使 LLM 过度筛选，丢弃了传感器数据融合、补偿机制等支撑性细节
- **修改方案**:
  - **方案 A（推荐）**：回退此句，保留原有指令不加"优先梳理主功能流程"
  - **方案 B**：改为更温和的表述 — "以问题核心目标为主线组织答案，同时保留支撑主流程的关键细节（如传感器数据融合、环境补偿等）"
- **验收标准**:
  1. 与 R0-2 配合，`stm32-control-prefixed` 答案包含传感器数据融合、环境补偿机制等支撑细节
  2. Keyword coverage 中 "土壤湿度"、"环境温湿度"、"PWM" 至少命中 3/5

---

### R0-4：诊断 stm32-summary 退化根因

- **涉及文件**: 待确认
- **问题**: `stm32-summary` 从 2/2 PASS 退化到 1/2 FAIL，但 summary 类不会触发 Pass 1（仅 procedure 触发），因此根因可能不同
- **排查方向**:
  1. 检查 scoring 变更（abstract -1.5f, workflow/overview -0.95f）是否导致 summary 问题的上下文窗口选到了不同 chunk
  2. 检查 answer 中 "感知—决策—执行—交互"（em-dash）是否与 baseline 关键词 "数据采集"、"决策控制"、"执行机构"、"交互显示" 的匹配逻辑有问题
  3. 如果答案质量本身没变差，只是关键词不匹配，考虑扩展 baseline 关键词（类似 P2-4）
- **验收标准**:
  1. 明确根因并归类（管道问题 or 评估框架问题）
  2. 若为管道问题，新增对应 R0 子任务；若为评估框架问题，扩展 baseline 关键词

---

### R0-5：重新评估 CandidateRetriever scoring 变更的副作用

- **涉及文件**: `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs`
- **位置**: `ComputeProcedureTopChunkQualityBoost`（L549-600）
- **问题**: 以下 scoring 变更可能有意料之外的副作用：
  - abstract/summary 扣分从 -1.25f 加重到 -1.5f
  - conclusion 扣分从 -0.75f 加重到 -1.0f
  - 新增 workflow/overview/概述/总体 扣分 -0.95f
  - 新增 ProcedureSectionBoostTerms 加分 +1.15f
  - 信号计数移到 section boost/penalty 之后（顺序变更）
- **排查方向**:
  1. 对比 stm32-summary 前后两次的上下文窗口 chunk 列表（从评估报告抽取）
  2. 如果 abstract chunk 被过度惩罚导致 summary 问题的上下文质量下降，回退扣分幅度
- **验收标准**:
  1. 明确 scoring 变更对非 procedure 类问题的实际影响
  2. 如有退化，回退到对非 procedure intent 保持旧评分逻辑

---

### R0-6：全量回归评估

- **说明**: 完成 R0-1 ~ R0-5 后，重跑全部 20 个 case
- **验收标准**:
  1. Case pass ≥ 18/20（恢复到退化前水平）
  2. Keyword coverage ≥ 78%
  3. stm32-control-prefixed 和 stm32-summary 必须 PASS
  4. 已有通过的 case（如 stm32-remote、geology-* 等）不退化
  5. Citation precision/recall 不下降

---

## 修复顺序

```
R0-1 (Pass 1 收紧名额) ──┐
                          ├──→ R0-2 (去元格式) ──→ R0-3 (回退指令) ──→ R0-6 (全量回归)
R0-4 (诊断 summary) ─────┤
                          │
R0-5 (scoring 副作用) ───┘  (可与上面并行)
```

R0-1 是主因修复，优先做。R0-2/R0-3 是提示词层面的防御性修复，与 R0-1 互补。R0-4/R0-5 可在 R0-1 完成后根据评估结果决定是否需要。

---

## 变更范围

| 文件 | 涉及任务 |
|------|----------|
| `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` | R0-1, R0-5 |
| `csharp-rag-avalonia/Services/LocalAiService.cs` | R0-2 |
| `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs` | R0-3 |
| `docs/eval-baseline.json` | R0-4（若确认为评估框架问题） |
