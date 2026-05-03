# WeaveDoc RAG V2 测试结果修复清单

> 基于评估报告 `20260503-084809-weavedoc-rag-baseline` 的诊断分析。
> 核心结论：检索环节基本正常（Context signal coverage 85.9%），问题出在 **LLM 生成环节的系统性崩溃**——全部 20 个案例均返回"我不知道（当前文档未覆盖）"。

## 当前基线数据

| 指标 | 数值 | 目标 |
|------|------|------|
| Case pass | 1/20 (5%) | ≥50% |
| Keyword coverage | 0/88 (0.0%) | ≥30% |
| Top chunk signal | 30/64 (46.9%) | ≥60% |
| Context signal | 55/64 (85.9%) | ≥85% |

---

## P0 — 致命问题（必须立即修复）

### 任务 P0-1：重写 System Prompt 的自毁指令

- **文件**: `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs`
- **位置**: `RagSystemPrompt` 常量，规则 #2
- **问题**: `"若上下文没有覆盖问题，或只能支持很弱的猜测，直接回答'我不知道（当前文档未覆盖）'"` —— LLM 看到碎片化的 chunks（而非预写的完整总结），倾向判定"上下文未覆盖问题"，选择最简单出路：拒绝回答。这是全部 20 个案例返回"我不知道"的**首要根因**。
- **修改**: 将规则 #2 改为强制综合的指令：

```csharp
// 旧（自毁指令）
2) 若上下文没有覆盖问题，或只能支持很弱的猜测，直接回答"我不知道（当前文档未覆盖）"。如果上下文已经能明确支撑答案，不要因为措辞不同、没有逐字复用问题词，或没有单个句子同时命中全部关键词就拒答。

// 新（强制综合指令）
2) 上下文由多个信息块组成，需要你主动提炼、归纳和综合。即使信息分散在多个块中，没有单个块直接完整回答，你也必须尽力整合出完整答案。只有当上下文中确实找不到任何与问题相关的信息时，才能回答"根据当前文档，未找到相关信息"。
```

- **验收**: 跑 5 个以上 eval case，确认不再出现大面积的"我不知道"。

### 任务 P0-2：修复循环中加入反拒答指令

- **文件**: `csharp-rag-avalonia/Services/Rag/AnswerComposer.cs`
- **位置**: `BuildRepairPrompt` 方法 (~line 103)
- **问题**: 修复 Prompt 使用同一个 `RagSystemPrompt`（包含"说我不知道"的规则），导致 LLM 在修复轮次再次说"我不知道"。形成死循环：Prompt 教 LLM 拒答 → 验证拒绝 → 修复 Prompt 又教 LLM 拒答 → 最终 fallback 到硬编码的"我不知道"。
- **修改**: 在 `BuildRepairPrompt` 的"修正重点"段落中加入硬性反拒答指令：

```csharp
// 在"修正重点:"行后添加
builder.AppendLine("重要: 本次绝对不能输出\"我不知道\"、\"当前文档未覆盖\"或类似拒答语句。你必须基于下方上下文综合出具体、完整的回答，引用上下文中的事实和术语。");
```

- **验收**: 手动构造一个 LLM 首次回答为"我不知道"的场景，确认修复轮次不再返回拒答。

### 任务 P0-3：放宽 `IsOffTopicOrMalformedAnswer` 的引用硬性要求

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: `IsOffTopicOrMalformedAnswer` 方法，~line 3097
- **问题**: 当前要求**每个回答**必须包含 `[path | section | cNN]` 格式的引用标签，否则直接判定为 malformed。LLM 生成了语义正确的回答但忘记加引用 → 被拒绝 → 触发修复 → 修复轮次仍可能不加引用 → fallback 到"我不知道"。引用应该是"期望"而非"硬性门禁"。
- **修改**:

```csharp
// 方案 A（推荐）：仅在修复轮次跳过引用检查
// 给 IsOffTopicOrMalformedAnswer 增加 isRepairAttempt 参数，修复轮次不检查引用

// 方案 B（更激进）：完全移除引用硬性要求，将 CitationRegex 检查从 IsOffTopicOrMalformedAnswer 中删除
// 保留 System Prompt 中对引用的要求作为软约束
```

- **验收**: 跑 3 个 case 确认缺少引用的好回答不会被误杀。

---

## P1 — 严重影响（应该尽快修复）

### 任务 P1-1：中文问题场景下过滤英文摘要/元数据 chunks

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: Context 构建逻辑（`AskAsync` 或 `FindRelevantChunksAsync` 中取 Top-N context 的逻辑）
- **问题**: 上下文窗口仅 8 个 chunks。案例 #1 的 Top-8 中有 2 个英文摘要（c11、c12），占据 25% 空间。中文问题 + 中文文档场景下，`englishAbstract`/`englishTitle`/`englishKeywords` 完全无用。
- **修改**: 在构建 context chunk 列表时，对中文问题过滤掉 `ContentKind` 为以下类型的 chunks：
  - `englishAbstract` / `englishTitle` / `englishKeywords`
  - 纯元数据：`authors` / `affiliation` / `funding` / `references`
  
  过滤逻辑仅在检测到问题为中文时生效（英文问题保留英文摘要）。

- **验收**: 检查 3 个以上 case 的 context chunks，确认中文问题场景下不再出现英文摘要/作者信息占据 context 窗口。

### 任务 P1-2：增加 context 窗口大小

- **文件**: `csharp-rag-avalonia/Services/RagOptions.cs`
- **位置**: `TopK` 默认值（或相关 context window 参数）
- **问题**: 当前 context 窗口仅 8 个 chunks。去除英文摘要等噪音后还剩 6-7 个，仍然偏少。
- **修改**: 将 context 窗口（`RAG_TOP_K`）默认值从 4 提升到 8，或将 `ContextWindowRadius` 从 1 提升到 2。同时调大 `RerankerTopN` 从 8 到 12。

```csharp
// RagOptions.cs 默认值调整
TopK: GetInt("RAG_TOP_K", 8, 1, 20),        // 原 4
RerankerTopN: GetInt("RAG_RERANKER_TOP_N", 12, 1, 50),  // 原 8
```

- **验收**: 确认 eval 中 context signal coverage 不再下降，Top chunk signal coverage 有所提升。

---

## P2 — 改善效果（时间允许时修复）

### 任务 P2-1：改进 Markdown chunking 质量

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: 文档分块/文本预处理逻辑
- **问题**: Markdown 文件的 chunk 包含 LaTeX 公式碎片（`$\textcircled{1}$`、`$ \pm 4\% $`）、特殊字符、句中截断等，严重降低上下文可读性，影响 LLM 理解。
- **修改**: 在分块前对 Markdown 文本做预处理：
  1. 移除 LaTeX 公式块和行内公式
  2. 将 Markdown 图片语法 `![...](...)` 替换为图片 alt 文本
  3. 规范化连续空白字符
  4. 在自然段落边界（而非固定字符数）处切分

- **验收**: 检查 eval 报告中 markdown 来源的 chunk 文本质量，确认不再出现 LaTeX 碎片。

### 任务 P2-2：`ShouldReturnUnknownForUnsupportedRequestedDocumentTopic` 改用语义匹配

- **文件**: `csharp-rag-avalonia/Services/LocalAiService.cs`
- **位置**: `ShouldReturnUnknownForUnsupportedRequestedDocumentTopic` 方法 (~line 3188)
- **问题**: 当前使用 `combinedText.Contains(token)` 做精确子串匹配。术语变体（如"精准灌溉"vs"精确灌溉"）会导致匹配失败，误判为"文档未覆盖"。
- **修改**: 方案 A — 直接移除这个方法，让 LLM 自己判断（P0 修复后 LLM 应该能正确处理）。方案 B — 改用 Embedding 相似度做语义级匹配。
- **建议**: 先采用方案 A（移除），P0 修复后 LLM 已有能力自行判断，这个预检门控反而可能误杀合法问题。

---

## 执行顺序

```
P0-1 (System Prompt) ──┐
P0-2 (修复循环)     ──┼── 并行执行 ──→ 跑 Eval 验证 ──→ 预期 pass rate 回到 30%+
P0-3 (放宽引用检查) ──┘
                              │
P1-1 (过滤英文chunks) ──┐
P1-2 (增大context窗口) ──┘──→ 跑 Eval 验证 ──→ 预期 pass rate 达到 50%+

P2-1 (Markdown质量) ──── 单独执行
P2-2 (语义匹配)     ──── P0 生效后再评估是否需要
```

## 验证方法

每次修复后用以下命令跑 eval 验证：

```bash
dotnet test csharp-rag-avalonia.Tests/ --filter "FullyQualifiedName~Eval" --verbosity normal
```

检查生成的 `.eval/` 目录下最新报告中的 `Case pass count` 和 `Keyword coverage`。
