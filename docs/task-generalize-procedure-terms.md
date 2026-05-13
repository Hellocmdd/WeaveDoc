# 检索优化：引入中文分词与 Procedure 术语动态提取

> 创建日期: 2026-05-13
> 状态: 待规划
> 优先级: 高（解决跨领域论文检索召回率低、专业名词分词破碎的核心痛点）

## 动机

根据最新 RAG 链路分析报告，目前针对中文学术论文的问答能力在跨领域场景中面临两个明显的局限性：

1. **中文分词粒度粗糙（2-gram 噪声）**
   当前的 `ExtractQueryTokens` 使用连续字符串 + 2-gram 的简单切分方式。这导致像“注意力机制”或“卷积神经网络”等专业术语被切分成“意力”、“力机”等无意义片段。这不仅破坏了 FocusTerms（焦点词）的准确性，还会导致关键词命中计算产生大量噪声。
   
2. **ProcedureSectionBoostTerms 领域固化**
   当前的 14 个静态中文术语（控制、调节、补偿、策略、机制、阶段、执行、算法、传感器、驱动、检测、采集、设定、阈值）强烈偏向嵌入式/IoT 领域。对于 NLP/CV、医学等领域的论文，由于无法命中领域特定的流程词汇（如“预训练”、“特征提取”、“编码”），导致 `procedure` 意图下的上下文选取质量显著下降。

为了实现领域的自适应并提升跨领域问答能力，需要**引入成熟的中文分词器替代 2-gram，并基于分词结果从文档语料中动态提取高频 Section Title 术语**。

## 涉及文件

| 文件 | 用途 |
|------|------|
| `WeaveDoc.Rag.csproj` | 引入第三方中文分词库依赖（如 `jieba.NET` 或类似生态库） |
| `csharp-rag-avalonia/Services/LocalAiService.cs` | 1. `ExtractQueryTokens` 分词逻辑重构<br>2. 语料加载（`LoadCorpusAsync`）与刷新（`RefreshCorpusAsync`）入口触发动态提取 |
| `csharp-rag-avalonia/Services/Rag/CandidateRetriever.cs` | `ProcedureSectionBoostTerms` 替换为动态列表及相关评分逻辑修改 |

## 设计方案

### 阶段 A — 引入中文分词器彻底重构 Tokenizer

1. 引入中文分词库（如 Jieba.NET 或基于词典的高效实现）。
2. 改造 `EnumerateTokens`：
   - 移除原来的 2-gram 切词逻辑。
   - 使用分词器对输入文本进行切词。
   - 保留英文/数字 token 的正则提取规则，确保英文缩写不被破坏。
3. 这些改动将直接提升 `BuildRetrievalQueryTokens` 中 FocusTerms 的提取质量。

### 阶段 B — 语料分析与动态提取（文档导入/刷新时执行一次）

1. 遍历当前语料所有 chunk 的 `SectionTitle`（跳过 null/空白）。
2. 对每个 section title 调用重构后的分词器进行分词。
3. **多级过滤**：
   - 基础过滤：`IsMeaningfulFocusTerm` 为 false 的 token。
   - 中文停用词：的、是、在、和、与、及、或、对、从、到、中、等、其、了、不、也、就、都、但、而、且、所、以、能、会、可、要、有、这、那 等。
   - 跨领域宽泛词（区分度低，命中太泛）：系统、设计、分析、方法、研究、基于、实现、应用、介绍、相关、主要、包括、进行、通过。
   - 纯数字、标点或单字符。
4. 统计每个 term 在不同 section title 中的出现频次（Document Frequency, DF），排除仅出现 1 次的噪声词。
5. 按 DF 降序排列，取 Top-30 得到 `CorpusProcedureTerms`。

**计算开销**：遍历全量 chunk + 分词 + 频次统计，预估 10,000 chunk 规模耗时可控制在百毫秒级。

### 阶段 C — 运行时自适应替换

```csharp
// 实例字段
private string[]? _corpusProcedureTerms;

// BuildAnswerContextChunks procedure 块中 (Pass 1a / 1b)
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

触发时机为：
- `LoadCorpusAsync` 和 `RefreshCorpusAsync` 末尾自动触发 `BuildCorpusProcedureTerms(allChunks)`。
- 如果语料为空或未能提取出有效词汇，`_corpusProcedureTerms` 保持为 null，系统安全回退到内置的 IoT 偏向术语表。

## 子任务拆解

| # | 子任务 | 说明 | 预估 |
|---|--------|------|------|
| 1 | 引入中文分词库 | 调研并引入适合 .NET 10/Avalonia 的分词库，或实现轻量级词典分词 | 核心 |
| 2 | 重构 `ExtractQueryTokens` | 移除 2-gram，接入新分词器，保留英文短正则提取 | 核心 |
| 3 | 实现 `BuildCorpusProcedureTerms` | 静态方法，包含全量分词、DF 统计、停用词/宽泛词过滤、Top-30 选取 | 核心 |
| 4 | 配置停用词与宽泛词表 | 添加中文停用词和跨领域宽泛词作为静态集合 | 辅助 |
| 5 | 语料生命周期集成 | `LoadCorpusAsync` 和 `RefreshCorpusAsync` 触发更新 | 辅助 |
| 6 | 检索管线改造 | `BuildAnswerContextChunks` 和 `ComputeProcedureTopChunkQualityBoost` 替换静态引用 | 辅助 |
| 7 | 单元测试 | 验证“注意力机制”不破碎；模拟医学语料验证能抽出特定领域术语 | 测试 |
| 8 | 回归评估 | STM32 语料重跑 20 case，验证动态术语对旧 IoT 文档表现稳定且 pass 数不降 | 验证 |

## 验收标准

1. **分词准确性**：“注意力机制”、“卷积神经网络”等专业词汇被正确切分，不产生“力机”、“神经”等碎片 2-gram。
2. **自适应提取**：导入非嵌入式文档（如计算机视觉论文）后，系统能动态提取出“特征提取”、“卷积”、“编码”等该领域的 Procedure 核心词。
3. **向后兼容**：对已有的 STM32 语料，动态提取的 term 能够覆盖原硬编码列表的核心概念（重叠度 ≥ 60%）。
4. **性能验证**：分词改造和动态提取步骤不明显拖慢语料加载流程（10000 块以内 < 500ms）。
5. **回归测试**：现有的 Eval Baseline 20 个 case 不出现指标衰退。
