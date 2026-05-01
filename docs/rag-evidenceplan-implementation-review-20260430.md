# WeaveDoc RAG EvidencePlan 改进回顾与遗留问题

> 日期：2026-04-30  
> 范围：回顾本轮基于 `EvidencePlan` 的 RAG 架构改造，说明已完成内容、验证情况、仍存在的问题和后续建议。

## 1. 本轮改进目标

本轮改造的目标不是继续调 embedding、BM25 或 reranker 权重，而是把系统从“TopK chunk 检索”推进到“证据契约驱动的问答”。核心思路是：

- 先确定问题应该在哪些文档范围内检索。
- 再判断当前问题需要哪种证据类型。
- 对组成、模块、实现流程等问题建立证据槽位。
- 只把满足证据计划的 EvidenceSet 交给回答阶段。
- 让 reranker 从全局裁判降级为组内排序器。
- 在评测中增加 scope、evidence kind、slot coverage、sufficiency、citation-from-evidence-set 等指标。

这对应此前报告中的判断：当前主要问题不是“完全搜不到”，而是缺少从候选 chunk 到合格证据集合的中间层。

## 2. 已完成的主要改动

### 2.1 引入 EvidencePlan / EvidenceSet

在 `csharp-rag-avalonia/Services/LocalAiService.cs` 中新增了内部证据规划相关类型：

- `EvidenceMode`
- `EvidencePlan`
- `EvidenceSlot`
- `EvidenceSet`
- `EvidenceDebugSnapshot`

`RetrievalResult` 现在携带 `EvidencePlan` 和 `EvidenceSet`，检索 debug 中也会输出：

- scope
- mode
- source preference
- sufficiency rule
- required slots
- covered slots
- sufficiency status

这让每次检索不再只是输出“命中了哪些 chunk”，而是能看到系统认为当前问题需要什么证据。

### 2.2 Scope 从软加分改成硬过滤

本轮将指定文档范围前移到候选阶段：

- 用户明确指定文档时，先过滤候选文档，不再只是给目标文档加 `docTarget` 分。
- 对 JSON/PDF 同源文档，结构化 JSON 在相关意图下优先。
- 对追问类问题增加 active document scope，上一轮主文档会成为下一轮省略指代的默认范围。

这能直接缓解多文档混答问题，尤其是 `geology-modules-no-cross-doc` 这类“不要混入 STM32 内容”的场景。

### 2.3 EvidenceMode 策略化

现有 `DetectIntent` 的结果被映射为 `EvidenceMode`：

- `summary` -> `Summary`
- `procedure` / `module_implementation` -> `Implementation`
- `composition` -> `Composition`
- `module_list` -> `ModuleList`
- `definition` -> `Definition`
- `compare` -> `Compare`
- `metadata` -> `Metadata`
- `usage` -> `Usage`
- `explain` -> `Explain`
- 未提及类问题 -> `AbsenceCheck`

不同 mode 有不同的 content kind 策略和章节偏好。例如中文 summary 默认排除英文摘要，普通问答默认排除 metadata，implementation 优先 body 证据。

### 2.4 槽位化 Evidence Selection

本轮为多点问题增加了槽位选择逻辑：

- `Composition`：主控/处理、传感/采集、执行/驱动、显示/交互、通信/联网等。
- `Implementation`：输入数据、控制决策、精细调节、环境补偿、执行机构。
- `ModuleList`：模块名、模块职责、模块关系。

`BuildEvidenceSet` 会先按 EvidencePlan 过滤候选，再为槽位选择证据。这样不再完全依赖 TopK 排序覆盖所有答案要点。

### 2.5 Reranker 权限收缩

新增 `TryRerankWithinEvidenceGroupsAsync`，使 learned reranker 不再直接对全局候选做最终裁判。

当前策略是：

1. 先按 scope 和 evidence policy 过滤。
2. 有槽位时按 slot 分组。
3. reranker 只在同一槽位或同类证据内排序。
4. 最终 EvidenceSet 优先保证 scope、证据类型和槽位覆盖。

这样可以降低 reranker 把摘要、引言、英文摘要排到实现章节前面的风险。

### 2.6 评测指标扩展

在 `csharp-rag-avalonia/Services/EvalRunner.cs` 中新增了 Evidence 相关指标：

- `scope accuracy`
- `evidence kind accuracy`
- `slot coverage`
- `sufficiency accuracy`
- `citation-from-evidence-set`

这些指标会写入控制台和 Markdown/JSON 评测报告。它们比单纯的 Top chunk signal 更贴近本轮架构目标。

## 3. 已观察到的改善

本轮局部运行验证中观察到以下正向变化：

- `stm32-summary` 在禁用 reranker 时仍能命中 JSON 结构化文档，并排除英文摘要作为主要上下文。
- `stm32-control` 的检索阶段已经能稳定覆盖 `3 模糊PID复合控制器`、`3.2 PID调节阶段`、`3.3 环境补偿机制`、`1.3.2 电磁阀PWM控制策略` 等关键证据。
- `stm32-remote` 能稳定从远程状态显示及控制模块中抽出 USART、ESP-01S、MQTT、JSON、APP、阈值/指令等要点。
- Evidence debug 能清楚显示每个问题的 mode、scope、slots 和 coveredSlots，方便定位“证据计划错了”还是“证据选择错了”。
- 在 `RAG_RERANKER_ENABLED=false` 下，关键 case 的证据类型仍保持较稳定，说明证据计划开始承担结构约束职责。

## 4. 仍存在的问题

### 4.1 本轮未完成稳定的 20 case 全量评测

由于本地模型执行耗时较长，本轮只完成了局部评测观察和编译验证，没有产出一份完整、稳定、可作为最终结论的 20 case 新报告。

当前不能严谨声称已经达到：

- `>= 14/20` case pass
- Context signal coverage `>= 90%`
- 所有 citation-from-evidence-set 均通过

后续必须跑完完整 eval，并基于新 `.eval/*.md` 报告继续修正。

### 4.2 `Implementation` 槽位仍有泛化风险

`Implementation` 默认槽位目前偏向“精准灌溉”这类控制链路问题：

- 输入数据
- 控制决策
- 精细调节
- 环境补偿
- 执行机构

但 `module_implementation` 问题，例如“远程状态显示及控制模块是怎么做的”，更应该使用：

- 接口/连接
- 协议/传输
- 数据格式
- 展示端
- 控制动作

目前远程模块问题能通过，部分原因是已有专门 fallback 生效，不代表 EvidencePlan 的槽位设计已经泛化良好。

### 4.3 Procedure fallback 仍有规则堆叠痕迹

`BuildImplementationSlotFallbackAnswer` 已经开始按槽位组织答案，但仍存在以下问题：

- 规则词表较手工化。
- 对具体领域有偏置，例如 `PID`、`PWM`、`电磁阀`。
- 如果换成其他论文主题，可能需要新增槽位或词表。
- 槽位句子选择仍是 sentence-level heuristic，不是真正的证据对象级选择。

这说明当前只是 EvidencePlan 的规则化 v1，还不是通用 planner。

### 4.4 JSON/PDF 同源竞争还没有彻底解决

本轮做了结构化 JSON 优先，但局部评测中仍观察到 PDF 噪声可能进入候选或 EvidenceSet，尤其是：

- PDF 断行、跨栏拼接导致句子残缺。
- 同源 JSON 已有完整证据时，PDF 仍可能因词面密度进入排名。
- procedure fallback 曾被 PDF 残句抢走执行机构槽位。

后续需要在 EvidencePlan 层做更硬的同源策略：如果同源 JSON 存在且命中充分，PDF 默认不进入 EvidenceSet，只作为无 JSON 时的补充。

### 4.5 AbsenceCheck 仍需更严格验证

本轮新增了 `AbsenceCheck` 模式，用于“有没有写某主题”类问题，但仍需验证：

- 目标文档 scope 是否始终正确。
- 同义词扩展是否足够。
- 负例是否会被语义相近内容误判为覆盖。
- “当前文档未覆盖”是否只在证据不足时触发。

尤其是 `stm32-no-answer-bluetooth` 这类问题，需要完整评测确认不会被“通信/WiFi/远程控制”等相近段落误导。

### 4.6 评测新增指标仍偏启发式

新增的 scope/evidence kind/slot/sufficiency/citation 指标目前主要基于：

- case id 规则
- question 文本特征
- context chunk 文件路径
- answer citation 是否出现在 EvidenceSet

这些指标有实用价值，但还不是严格标注驱动。后续更好的做法是在 `docs/eval-baseline.json` 中显式加入：

- expected scope
- expected evidence mode
- required slots
- forbidden source patterns
- expected unknown policy

否则 EvalRunner 仍会把部分评测逻辑写死在代码里。

### 4.7 `LocalAiService.cs` 继续膨胀

本轮改造主要集中在 `LocalAiService.cs`，该文件已经承担过多职责：

- 文档加载
- chunk 构建
- query understanding
- sparse/semantic 检索
- rerank
- evidence planning
- evidence selection
- prompt 构造
- fallback answer
- citation 校验

这会增加后续维护成本。EvidencePlan 相关逻辑应在下一轮拆到独立服务或文件中，例如：

- `EvidencePlanner`
- `EvidenceSelector`
- `EvidenceSufficiencyChecker`
- `QueryScopeResolver`

### 4.8 learned reranker 服务失败时仍缺少清晰降级指标

局部运行中曾出现 reranker 请求失败或禁用场景。虽然当前系统可以降级到规则排序，但评测报告需要更清楚地区分：

- reranker disabled
- reranker timeout
- reranker HTTP failure
- reranker empty response
- grouped rerank fallback

否则很难判断波动来自模型服务，还是来自 EvidencePlan 本身。

## 5. 建议的下一步

### 5.1 先跑完整基线并归档报告

建议先运行：

```bash
RAG_RERANKER_ENABLED=false dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

再运行：

```bash
RAG_RERANKER_ENABLED=true dotnet run --project csharp-rag-avalonia/RagAvalonia.csproj -- --eval ./docs/eval-baseline.json
```

对比两份报告中的：

- case pass count
- context signal coverage
- scope accuracy
- slot coverage
- citation-from-evidence-set
- reranker status

### 5.2 修正 JSON/PDF 同源策略

建议新增规则：

- 如果同源 JSON 存在且 EvidenceSet 已充分，PDF 不进入 EvidenceSet。
- PDF 只在 JSON 不存在、JSON 证据不足、或用户明确指定 PDF 时使用。
- PDF chunk 需要先通过噪声清洗和句子完整性检查。

### 5.3 将 eval baseline 显式证据化

建议在 `docs/eval-baseline.json` 中逐步加入字段：

```json
{
  "expectedScope": ["json/基于STM32单片机的智能浇花系统设计_彭霞.json"],
  "expectedEvidenceMode": "Implementation",
  "requiredSlots": ["输入数据", "控制决策", "精细调节", "环境补偿", "执行机构"],
  "forbiddenSources": ["englishAbstract", ".pdf"],
  "unknownExpected": false
}
```

这样 EvalRunner 不再依赖 case id 推断。

### 5.4 拆分 LocalAiService

建议下一轮重构时先拆 Evidence 相关逻辑，不改外部 UI：

- `EvidencePlanner.cs`
- `EvidenceSelector.cs`
- `EvidenceModels.cs`
- `ScopeResolver.cs`

这样可以降低 `LocalAiService.cs` 的复杂度，也方便单测 EvidencePlan。

### 5.5 增加针对 EvidencePlan 的单元测试

建议新增不依赖 LLM 的测试：

- query -> EvidenceMode
- query -> scope
- EvidencePlan -> slots
- candidates -> EvidenceSet
- EvidenceSet -> sufficiency
- answer citations -> all in EvidenceSet

这些测试比端到端 eval 更快，更适合在 CI 中跑。

## 6. 当前结论

本轮改造已经把 WeaveDoc RAG 的主流程从“TopK 排序”推进到了“EvidencePlan 驱动”的雏形：scope、mode、slot、sufficiency 和 citation consistency 都已成为系统可见对象。

但当前仍是规则化 v1，主要风险在：

- 完整评测尚未闭环。
- 槽位策略仍偏手工。
- PDF 噪声仍可能竞争证据。
- EvalRunner 新指标仍需要 baseline 显式标注支撑。
- `LocalAiService.cs` 复杂度继续上升，需要拆分。

因此，下一轮不建议继续大幅增加规则，而应优先：完整跑评测、修 JSON/PDF 同源策略、让 eval baseline 显式描述证据契约，并把 EvidencePlan 逻辑拆出可测试模块。
