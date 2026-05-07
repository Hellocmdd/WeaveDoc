# 通用化 ProcedureSectionBoostTerms — 从硬编码到语料动态提取

> 创建日期: 2026-05-06
> 状态: 待规划
> 优先级: 低（当前硬编码列表在技术文档场景下工作良好，回退机制保证不影响非技术文档的正确性）

## 动机

`ProcedureSectionBoostTerms` 当前的 14 个静态中文术语：

```
控制、调节、补偿、策略、机制、阶段、执行、算法、传感器、驱动、检测、采集、设定、阈值
```

偏向嵌入式/IoT 领域。若系统未来需要支持法律、金融、医学等其他领域的文档，这些术语在 Stage B Pass 1（section title 匹配）中的命中率会下降。虽然三趟回退机制保证正确性不受影响，但 procedure 类问题的上下文选取优化效果会减弱。

改为从文档语料中动态提取高频 section title 术语，可以让系统自适应不同领域的文档结构。

## 涉及文件

| 文件 | 用途 |
|------|------|
| `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` | `ProcedureSectionBoostTerms` 定义（L454）及两处使用（L174 合并、L596 评分） |
| `csharp-rag-avalonia/Services/LocalAiService.cs` | 语料加载（`LoadCorpusAsync`）与刷新（`RefreshCorpusAsync`）入口 |
| `csharp-rag-avalonia/Models/DocumentChunk.cs` | `SectionTitle` 字段来源 |

## 使用点分析

`ProcedureSectionBoostTerms` 在两个阶段被使用：

| 阶段 | 位置 | 作用 | 缺失时的影响 |
|------|------|------|-------------|
| Stage A | `ComputeProcedureTopChunkQualityBoost` L596 | +1.15 分给包含这些术语的 chunk | procedure chunk 排序略降，但不阻断 |
| Stage B | `BuildAnswerContextChunks` procedure 块 L174 | Pass 1 section title 匹配优先选取 | 回退到 Pass 2 + Pass 3，上下文窗口仍正确 |

两个位置都可以用动态列表替代，并保留静态列表作为 `??` 回退。

## 设计方案

### 阶段 A — 语料分析（文档导入/刷新时执行一次）

1. 遍历当前语料所有 chunk 的 `SectionTitle`（跳过 null/空白）
2. 对每个 section title 调用 `ExtractQueryTokens` 提取 CJK 二元组和 ASCII token
3. 过滤：
   - `IsMeaningfulFocusTerm` 为 false 的 token
   - 中文停用词：的、是、在、和、与、及、或、对、从、到、中、等、其、了、不、也、就、都、但、而、且、所、以、能、会、可、要、有、这、那
   - 跨领域宽泛词（区分度低，命中太泛）：系统、设计、分析、方法、研究、基于、实现、应用、介绍、相关、主要、包括、进行、通过
   - 纯数字或单字符
4. 统计每个 term 在不同 section title 中的出现频次（DF），排除仅出现 1 次的噪声词
5. 按 DF 降序排列，取 Top-30
6. 得到 `CorpusProcedureTerms`

**计算开销**：遍历全量 chunk + CJK 分词 + 词典频统计，预估 < 100ms（10,000 chunk 规模）。

### 阶段 B — 运行时替换

```csharp
// 实例字段
private string[]? _corpusProcedureTerms;

// BuildAnswerContextChunks procedure 块中
var dynamicTerms = _corpusProcedureTerms ?? ProcedureSectionBoostTerms;
var combinedTerms = queryProfile.FocusTerms
    .Where(t => !IsIntentExpansionOnlyToken(t))
    .Concat(dynamicTerms)
    .Distinct(StringComparer.Ordinal)
    .ToArray();

// ComputeProcedureTopChunkQualityBoost 中
var boostTerms = _corpusProcedureTerms ?? ProcedureSectionBoostTerms;
if (boostTerms.Any(t => lowerSection.Contains(t, StringComparison.Ordinal) 
                      || lowerStructure.Contains(t, StringComparison.Ordinal)))
{
    score += 1.15f;
}
```

### 阶段 C — 触发时机

- `LoadCorpusAsync` 末尾调用 `_corpusProcedureTerms = BuildCorpusProcedureTerms(allChunks)`
- `RefreshCorpusAsync` 末尾同上
- 空语料场景：`_corpusProcedureTerms` 保持 null → 回退到 `ProcedureSectionBoostTerms`

## 子任务拆解

| # | 子任务 | 说明 | 预估 |
|---|--------|------|------|
| 1 | 实现 `BuildCorpusProcedureTerms` | 静态方法 `IReadOnlyList<DocumentChunk> → string[]`，包含分词、DF 统计、停用词过滤、Top-30 选取 | 主要 |
| 2 | 添加停用词列表 | 中文停用词 + 跨领域宽泛词，作为 `private static readonly` 字段 | 辅助 |
| 3 | 语料刷新流程集成 | `RefreshCorpusAsync` 末尾调用，结果写入 `_corpusProcedureTerms` | 辅助 |
| 4 | 语料加载流程集成 | `LoadCorpusAsync` 末尾同样触发 | 辅助 |
| 5 | `BuildAnswerContextChunks` procedure 块改造 | 替换硬编码引用为 `_corpusProcedureTerms ?? ProcedureSectionBoostTerms` | 核心 |
| 6 | `ComputeProcedureTopChunkQualityBoost` 改造 | 同上 | 核心 |
| 7 | 单元测试 | 构造模拟语料验证：停用词过滤、宽泛词过滤、领域词提取、空语料回退 | 测试 |
| 8 | 回归评估 | STM32 语料重跑 20 case，确保动态列表与硬编码列表重叠 ≥ 60%，pass 数不降 | 验证 |

## 验收标准

1. STM32 浇花系统语料动态提取的 term 与现有 `ProcedureSectionBoostTerms` 重叠 ≥ 60%（验证对已知领域保持有效）
2. 全部 20 个 case 重跑，现有 pass 数不下降
3. 构造非嵌入式领域语料（如法律文档），验证动态术语中不出现"传感器"、"驱动"等嵌入式特有词
4. 构造空语料场景，验证回退到 `ProcedureSectionBoostTerms` 默认值
5. 语料刷新后 `_corpusProcedureTerms` 在 100ms 内完成更新
