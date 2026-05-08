# 修复 Procedure 类问题退化 — 任务清单

> 创建日期: 2026-05-06
> 最后更新: 2026-05-07
> 状态: R0-1~3 已完成，R0-4 跳过，R0-5 待做
> 退化来源: 第四轮/第五轮修复的 CandidateRetriever + AnswerComposer + LocalAiService 改动引入了对 procedure 问题的过度优化，导致 stm32-control-prefixed 和 stm32-summary 答案质量严重退化（18/20 → 16/20）

## 退化度量

| 指标 | 之前 (21:51) | 退化 (23:03) | R0-1~3 后 (10:40) | 目标 |
|------|-------------|-------------|-------------------|------|
| Case pass | 18/20 | 16/20 | 18/20 | ≥18 |
| Keyword coverage | 80.6% | 75.3% | 77.3% | ≥78% |
| Citation recall | 100% | 95.0% | 100% | 100% |

| 退化案例 | 23:03 | 10:40 (R0-1~3) | 状态 |
|----------|-------|-----------------|------|
| stm32-control-prefixed | 1/4 FAIL | 4/4 PASS | ✅ 已修复 |
| stm32-summary | 1/2 FAIL | 2/2 PASS | ✅ 已修复 |
| stm32-control | 2/4 FAIL | 4/4 PASS | ✅ 已修复 |
| stm32-no-answer-bluetooth | PASS | PASS | ✅ 保持（中间波动为 LLM 随机性） |
| follow-up-detail | 1/3 FAIL | 1/3 FAIL | ❌ 仍失败 |
| stm32-compare-fuzzy-pid | 5/5 PASS | 4/5 FAIL | ❌ 新退化（compare 指令变更导致） |

---

## R0-1：收紧 CandidateRetriever Pass 1 名额 ✅ 已完成

- **涉及文件**: `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs`
- **位置**: `BuildAnswerContextChunks` 中 procedure 块的 Pass 1（L164-196）
- **修改**: Pass 1 最多取 `Math.Min(limit / 3, 4)` 个 chunk，其余名额留给 Pass 2 + Pass 3
- **效果**: stm32-control-prefixed 从 1/4 → 4/4 PASS

---

## R0-2：去掉 procedure 回答结构规则的元格式引导 ✅ 已完成

- **涉及文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: `BuildAnswerStructureRule` 和 `BuildContextAssemblyRule`
- **修改**: procedure 规则改为"直接陈述每个环节中系统实际使用的算法、组件、参数和结果，不要用'可以按...链路来理解'等元描述开头"
- **效果**: stm32-control-prefixed 答案不再以元描述开头

---

## R0-3：回退 AnswerComposer 流程综合指令中的过度筛选 ✅ 已完成

- **涉及文件**: `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs`
- **位置**: `BuildPrompt` L47 和 `BuildContextualPrompt` L144
- **修改**: 去掉"优先梳理与问题核心目标直接相关的主功能流程，而非外围辅助链路"，改为更温和的表述
- **效果**: 与 R0-1/R0-2 配合，主目标达成

---

## R0-4：诊断 stm32-summary 退化根因 → 跳过

- **判断**: stm32-summary 在 R0-1~3 之前已恢复 PASS（LLM 随机性），R0-1~3 后持续稳定
- **结论**: 无需额外排查

---

## R0-5：修复 follow-up-detail 和 compare 退化（scope 已更新）

### R0-5a：修复 follow-up-detail chunk 选择

- **涉及文件**: 待确认（`CandidateRetriever.cs` 或 `LocalAiService.cs`）
- **问题**: "详细一点" 是 follow-up 问题，历史上下文中的前轮回答已建立"模糊PID灌溉控制"主题，但当前 chunk 选择偏到了 c19（高精度数据采集与多模态通信：I²C/AHT20/SPI/OLED/USART/ESP-01S），而非展开模糊PID控制链路
- **根因猜测**:
  1. `ExtractAnchorTerms` 提取的锚定词可能被 `ProcedureSectionBoostTerms` 中"采集"等泛化词淹没
  2. Pass 1 section title 匹配中，"高精度数据采集与多模态通信" 命中了"采集"，抢占了名额
  3. follow-up expansion 指令未有效约束 chunk 选择范围
- **验收标准**:
  1. follow-up-detail 答案围绕模糊PID控制展开，命中 ≥3/3 关键词

### R0-5b：修复 stm32-compare-fuzzy-pid 退化

- **涉及文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`、`AnswerComposer.cs`
- **问题**: compare 指令从 1 行膨胀到 3-4 行，LLM 输出"二者的区别可以按上下文中的对应证据分别比较。"等元描述，内容覆盖从 5/5 掉到 4/5
- **修改方案**:
  1. compare 指令增加"不要用'可以按...比较'等元描述开头"
  2. 精简 AnswerComposer 中的对比综合指令，减少格式约束
- **验收标准**:
  1. stm32-compare-fuzzy-pid 恢复 5/5 PASS
  2. 答案以实际差异内容开头，不出现元描述

---

## R0-6：全量回归评估

- **说明**: 完成 R0-5 后，重跑全部 20 个 case
- **验收标准**:
  1. Case pass ≥ 18/20
  2. Keyword coverage ≥ 78%
  3. follow-up-detail 和 stm32-compare-fuzzy-pid 必须 PASS
  4. 已有通过的 case 不退化
  5. Citation precision/recall 不下降

---

## 修复顺序（更新）

```
R0-5a (follow-up-detail) ──┬──→ R0-6 (全量回归)
                            │
R0-5b (compare 退化) ──────┘
```

R0-5a 和 R0-5b 可以并行。R0-5b 改动小（主要是提示词层面），可以优先修。

---

## 变更范围

| 文件 | 涉及任务 |
|------|----------|
| `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` | R0-5a |
| `csharp-rag-avalonia/Services/LocalAiService.cs` | R0-5a, R0-5b |
| `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs` | R0-5b |
