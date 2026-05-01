# WeaveDoc RAG 基准测试根因分析（2026-05-01）

## 背景

本次基准测试已经排除了 PDF 文档，语料主要来自 Markdown 和 JSON：

- 评估报告：`.eval/20260501-191419-weavedoc-rag-baseline.md`
- 基准配置：`docs/eval-baseline.json`
- 允许来源格式：`.md`、`.json`
- 禁止来源格式：`.pdf`

评估结果显示：

- Case pass count: `10/20`
- Keyword coverage: `66/88 (75.0%)`
- Top chunk signal coverage: `33/64 (51.6%)`
- Context signal coverage: `63/64 (98.4%)`
- Average citation precision: `88.0%`
- Average citation recall: `100.0%`
- Scope accuracy: `100.0%`
- Source format accuracy: `100.0%`

结论很明确：这次失败不是 PDF 残留导致的。系统几乎总能把答案相关证据放进上下文，但不能稳定把最核心证据排到第一位，也不能稳定从上下文中抽取完整答案。

## 关键现象

### 1. 上下文几乎全覆盖，但 top chunk 覆盖低

`Context signal coverage` 达到 `98.4%`，说明召回层总体能找到答案相关片段。

但 `Top chunk signal coverage` 只有 `51.6%`。这说明排序层没有稳定把问题最核心的证据放在首位。

评测代码里，top chunk 实际只检查第一条 ranked chunk，而不是 top-k：

```csharp
var topChunkText = rankedChunks.FirstOrDefault() is { } topChunk
    ? BuildChunkInspectionText(topChunk)
    : string.Empty;
```

位置：`csharp-rag-avalonia/Services/EvalRunner.cs`

因此，只要第一条不是预期章节，即使第 2-10 条和 context 里都有答案，retrieval check 仍可能失败。

### 2. JSON/Markdown 来源格式没有问题

本次报告中 `Source format accuracy` 为 `100.0%`，失败 case 的引用来源也基本都是 `.json` 或 `.md`。

这说明文档格式过滤已经生效，当前问题不是“PDF 解析质量差”，而是进入 RAG 后的检索、排序、证据组织和答案生成策略问题。

### 3. 答案经常引用正确上下文，但漏掉关键并列项

例如地质系统技术栈问题中，上下文已经包含：

- Spring Boot
- Vue3
- MyBatis-Plus
- MySQL/数据库相关描述
- ECharts

但最终答案只命中 `Spring Boot` 或少量关键词。这说明 evidence set 到 answer 的抽取策略没有保留“技术栈/组成清单”类问题中的完整并列实体。

## 根因分析

### 根因 1：分组重排把“证据覆盖顺序”污染成了“全局相关性排序”

当前检索流程大致为：

1. 稀疏检索：BM25、关键词、标题、JSON 结构分数
2. 向量语义分数
3. rule rerank
4. BGE reranker
5. EvidencePlan 分槽位选证据
6. EvidenceSet 进入上下文和答案生成

问题出在带槽位的重排逻辑。`TryRerankWithinEvidenceGroupsAsync` 会按 EvidenceSlot 顺序逐槽选择候选：

- Implementation 问题的槽位顺序是：
  - 输入数据
  - 控制决策
  - 精细调节
  - 环境补偿
  - 执行机构

这会导致“如何实现精准灌溉”这类问题第一条经常变成“输入数据/引言/传感器采集”，而不是用户真正关心的“模糊 PID 控制器、PID 调节阶段、环境补偿机制”。

典型失败：

- `stm32-control`
- `stm32-control-prefixed`
- `follow-up-detail`

这些 case 的 context signals 都是满分，但 top chunk signals 是 `0/3`。

根因不是没召回，而是排序被“槽位覆盖”重写了。

### 根因 2：意图识别漏掉“技术栈/总体架构”类系统题

`geology-system-architecture` 的问题是：

> 《基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现》采用了什么总体架构和技术栈

它被识别成 `general`，而不是 `composition` 或 system-paper 专门模式。

原因是组成题触发词里有：

- 组成
- 构成
- 包括哪些
- 硬件组成
- 软件组成
- 部署环境
- 运行环境

但没有稳定覆盖：

- 技术栈
- 总体架构
- 架构和技术栈
- 前后端分离

结果 EvidencePlan 变成 General，没有按后端、前端、数据、可视化等槽位组织证据，最终答案复述了泛化摘要，漏掉技术栈。

### 根因 3：稀疏扩展词过泛，容易把泛段排到前面

procedure 类问题会扩展很多通用词：

- 方法
- 步骤
- 流程
- 策略
- 机制
- 算法
- 控制
- 调节
- 动态
- 反馈
- 执行
- 输出
- 偏差

这些词在引言、workflow、结论段里都大量出现。对“精准灌溉”这种问题，核心证据应该是：

- 模糊 PID 复合控制器
- PID 调节阶段
- 环境补偿机制
- 电磁阀 PWM 控制策略

但泛扩展词会提高引言、workflow、系统概述类段落的分数，使其排在更前。

### 根因 4：Markdown 结构清洗不足

Markdown 文档虽然替代了 PDF，但不是天然干净语料。

地质论文 Markdown 里出现了这些问题：

- HTML 残片，如 `</div>`
- OCR 图片标签
- 作者单位、预发布信息混入正文
- 异常 section title，如 `及在科研场景适用性进行权衡：`
- 标题层级缺失或被误切

当前 Markdown chunking 主要按段落和标题粗切，噪声会作为普通 chunk 参与检索和重排。

这会导致某些摘要/作者/HTML 附近的段落因为包含大量关键词而进入高排名，影响第一命中质量和答案抽取质量。

### 根因 5：EvidenceSet 能覆盖槽位，但答案抽取只取“单句代表”

组成题和技术栈题需要保留并列实体，例如：

- 后端：Spring Boot、Spring MVC、MyBatis-Plus、RESTful API
- 前端：Vue3、VueRouter、Element Plus
- 可视化：ECharts
- 数据：数据库、配置文件、结构化数据

当前 slot fallback 逻辑倾向于每个槽位挑一句最高分句子。这样很容易出现：

- 句子说到了 Spring MVC，但漏掉 Spring Boot
- 句子说到了前端展示，但漏掉 Vue3
- 句子说到了可视化，但漏掉 ECharts

因此报告中会出现“context signals 满分，但 answer signals 很低”的情况。

典型失败：

- `geology-system-architecture`
- `geology-composition-stack`
- `stm32-composition-hardware`
- `geology-definition-kg`

### 根因 6：结构化 JSON chunk 太碎，父子章节信号没有充分进入 ranking

JSON 文档被拆成很多结构化小 chunk，这是必要的，但会带来一个副作用：

- 单个 chunk 很精准，但上下游关系弱
- 章节标题、父节点、兄弟节点的整体语义没有充分参与首位排序
- 需要综合多个子节点的问题，容易由某个局部高分 chunk 抢到第一

例如硬件组成问题中，context 里覆盖了主控、传感器、电磁阀、OLED、ESP-01S，但 top chunk 第一条只是“主控芯片低功耗设计与核心架构”，没有覆盖评测期待的组成信号。

这说明 JSON 结构优势已经被部分利用，但还没形成“章节级/父子级 evidence ranking”。

## 失败案例归类

### A 类：召回成功，首位排序失败

特点：

- Context signals 满分或接近满分
- Top chunk signals 低
- 答案大体能答，但 retrieval check fail

代表：

- `stm32-control`
- `stm32-control-prefixed`
- `follow-up-detail`
- `stm32-definition-mcu`
- `stm32-composition-hardware`

根因：

- 分组重排顺序污染全局 ranking
- 通用扩展词过泛
- 结构上下文没有用于第一证据排序

### B 类：上下文有答案，但答案漏关键信号

特点：

- Retrieval checks pass
- Context signals 满分
- Answer signals/keywords 低

代表：

- `geology-system-architecture`
- `geology-composition-stack`
- `geology-definition-kg`
- `stm32-explain-compensation`

根因：

- 意图识别不准
- slot fallback 每槽只选一句
- 对技术栈/组成/定义类问题缺少实体清单抽取

### C 类：Markdown 噪声干扰排序和答案

特点：

- 来源是 Markdown
- top chunks 中出现 `</div>`、OCR 图片、作者单位、异常标题

代表：

- `geology-system-architecture`
- `geology-composition-stack`
- `geology-definition-kg`

根因：

- Markdown 清洗不足
- 章节层级不稳定
- metadata/HTML 噪声没有足够降权或剔除

## 修复优先级

### P0：分离 RankedChunks 和 EvidenceSet

不要让 EvidenceSlot 的覆盖顺序决定 `RankedChunks[0]`。

建议：

- `RankedChunks` 保持全局相关性排序，用于 top chunk/debug/用户可解释性。
- `EvidenceSet` 单独负责槽位覆盖，用于上下文组织和答案生成。
- 分组 reranker 可以产出 evidence candidates，但不要覆盖原始全局排名。

这应该能直接提升 top chunk signal coverage。

### P0：补强意图识别

把这些问题稳定识别为 composition/system design：

- 技术栈
- 总体架构
- 架构和技术栈
- 前后端分离
- 采用了什么架构
- 采用了哪些技术

同时，`composition` 的 EvidencePlan 应覆盖：

- 总体架构
- 后端/服务
- 前端/交互
- 数据/存储
- 可视化/输出

### P1：技术栈/组成类答案改成实体保留策略

对组成题不要只抽“最高分一句”，而要从同一 evidence chunk 中保留关键并列实体。

建议增加实体/术语抽取：

- 框架名：Spring Boot、Vue3、MyBatis-Plus、Element Plus
- 协议/接口：RESTful API、HTTP、JSON、MQTT、USART、SPI、I2C
- 数据库/存储：MySQL、数据库、配置文件、持久化
- 可视化：ECharts、知识图谱
- 硬件：STM32L031G6、AHT20、SSD1306 OLED、ESP-01S、电磁阀

### P1：Markdown 清洗和章节结构恢复

建议在 Markdown 入库前做清洗：

- 去掉 HTML 标签和图片块
- 降权或剔除作者、单位、预发布、参考文献等 metadata
- 修复标题层级
- 保留 heading path，而不是只有当前 sectionTitle
- 对异常短标题或 OCR 残片做噪声惩罚

### P1：限制泛扩展词的权重

procedure 类扩展词不要与用户原词同等权重。

建议：

- 用户原词：高权重
- 领域实体词：高权重
- 意图扩展词：低权重
- 泛词如“方法、流程、控制、实现”：只作为弱信号

否则泛段会持续抢占高排名。

### P2：引入章节级父子证据

对 JSON 和 Markdown 都可以维护一个轻量章节图：

- parent section
- sibling chunks
- child chunks
- section summary

检索时先定位章节，再在章节内选证据。这样“硬件组成”“精准灌溉”“技术栈”这类跨多个子块的问题会更稳定。

### P2：调整评测口径或命名

当前 `Top chunk signals` 实际是 `Top-1 chunk signals`。

如果目标是测第一条证据质量，保留即可，但建议改名为 `Top1 chunk signal coverage`。

如果目标是测 top-k 检索质量，应改为检查 top 3 或 top 5，而不是只检查第一条。

## 总结

本次基准测试说明：

1. PDF 不是当前主要问题。
2. 文档来源格式过滤已经生效。
3. 召回基本成功，context 覆盖率很高。
4. 主要短板在排序首位、意图识别、结构化证据组织和答案抽取。
5. 最优先应该修 `RankedChunks` 与 `EvidenceSet` 的职责混用，其次修意图识别和组成/技术栈抽取。

一句话概括：现在系统已经“找得到”，但还没有稳定做到“把最核心的先拿出来，并完整说出来”。
