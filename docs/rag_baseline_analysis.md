# WeaveDoc RAG 基准测试分析报告

> 最新报告：`2026-04-29 22:56:00`  
> 对比报告：`2026-04-28 23:02:44`

---

## 一、核心指标横向对比

| 指标 | 上次 (04-28) | 本次 (04-29) | 变化 |
|------|-------------|-------------|------|
| **Case 通过率** | 7/20 (35%) | 8/20 (40%) | ↑ +5% |
| **关键词覆盖率** | 51/88 (58.0%) | 61/88 (69.3%) | ↑ +11.3% |
| **Top Chunk 信号覆盖** | 34/64 (53.1%) | 34/64 (53.1%) | → 持平 |
| **Context 信号覆盖** | 56/64 (87.5%) | 56/64 (87.5%) | → 持平 |
| **平均引用精确率** | 87.5% | 82.5% | ↓ -5% |
| **平均引用召回率** | 95.0% | 90.0% | ↓ -5% |

**结论性观察：** 关键词覆盖和 Case 通过率有改善，但引用精确率/召回率双双下降，说明有进步但引入了新的回答质量问题。

---

## 二、各 Case 通过情况（本次 04-29）

| Case | 类别 | 通过 | 失败原因 |
|------|------|------|---------|
| stm32-summary | engineering-paper | ❌ | Retrieval fail（Top chunk 1/2 不足） |
| stm32-control | engineering-paper | ❌ | Retrieval fail + Structural fail（关键词 2/4）|
| stm32-remote | engineering-paper | ✅ | - |
| stm32-remote-detailed | engineering-paper | ✅ | - |
| stm32-control-prefixed | engineering-paper | ❌ | Retrieval fail（关键词 3/5，Top chunk 0/3）|
| follow-up-detail | engineering-paper | ❌ | 全部失败（追问上下文丢失）|
| stm32-no-answer-bluetooth | robustness | ❌ | Structural fail（应回答"无"，但给了相关内容）|
| stm32-definition-mcu | definition | ✅ | - |
| stm32-composition-hardware | composition | ❌ | Top chunk 0/5，关键词 3/5 |
| stm32-usage-oled | usage | ✅ | - |
| stm32-explain-compensation | explain | ✅ | - |
| stm32-compare-fuzzy-pid | compare | ✅ | - |
| stm32-summary-json-scoped | json-ingestion | ✅ | - |
| stm32-remote-json-scoped | json-ingestion | ❌ | Citation fail（引用格式异常）|
| json-metadata-english | json-ingestion | ✅ | - |
| geology-system-architecture | system-paper | ❌ | 关键词 1/6，大量信息未提取 |
| geology-system-modules | system-paper | ❌ | 关键词 2/4，Top chunk 污染（出现浇花内容）|
| geology-modules-no-cross-doc | system-paper | ❌ | 关键词 2/4（即便明确指定文档和排除指令）|

---

## 三、问题层次分解

### 🔴 问题一：检索层（Retrieval）——最核心的瓶颈

**症状：** `stm32-control`、`stm32-control-prefixed`、`stm32-composition-hardware` 的 Top Chunk Signals 均为 0/N，检索出的 top-ranked 文档不包含期望的关键性 chunk。

**根因分析：**

#### 1.1 "精准灌溉"类问题的检索错误

以 `stm32-control`（"这个智能浇花系统如何实现精准灌溉"）为例：

期望命中 chunk：**模糊PID 控制器相关章节**（含"模糊PID"、"电磁阀"、"动态调节"等关键词）

实际 Top-4 命中：
```
[1] c16 - 0 引言（BM25=0.927 高，但是引言综述，非核心实现）
[2] c38 - PDF 结论/创新方案（噪声 chunk，含"遮阳帽"等混乱内容）  
[3] c10 - PDF 总体设计（表格区，信息碎片化）
[4] c35 - PID 调节阶段（这是正确的，但排名第4）
```

**关键发现：**
- **BM25 词频主导了初筛和排序**：c16（引言）BM25=0.927 最高，因为引言大段描述了"精准灌溉"、"土壤湿度"等词。但引言只是宏观描述，不是实现细节。
- **Reranker 未能纠正**：BGE-Reranker 使 c35（PID 调节）最终分 0.695 排第四，而引言 c16 排第一（0.680，差异极小）——说明 Reranker 区分度不足，无法区分"宏观概述"和"具体实现"。
- **JSON 结构信号对 procedure 类问题失效**：jsonStructure/title/coverage 都是 0，意味着这类问题的精确结构定位能力丧失，完全退化为语义+BM25 竞争。

#### 1.2 Composition（硬件组成）问题的检索问题

`stm32-composition-hardware`（Top chunk 0/5）：

期望包含：土壤湿度传感器、环境温湿度传感器、电磁阀、OLED、ESP-01S。  
实际 top-1 是 `workflow` chunk（c27，软件流程描述），这是完全不相关的 chunk。

**根因**：问题类型被识别为 `composition`，聚焦词 "硬件组成/硬件设计/主控芯片/传感器"，BM25 预筛时 c27（workflow 含"硬件状态"等词）得分 0.811 最高，从而占据 top-1 位置。

#### 1.3 跨文档问题的隔离失败

`geology-system-modules`（"这篇地质信息抽取论文有哪些功能模块"）：

LLM 的回答竟然出现了 STM32 浇花系统的 chunks（c3, c12, c14），说明在多文档场景下，没有文档隔离约束时，检索模块会召回错误文档的内容。这是一个严重的**多文档混淆**问题。

---

### 🟡 问题二：LLM 生成层——答案质量参差不齐

#### 2.1 `stm32-no-answer-bluetooth`（蓝牙 Mesh 组网）的鲁棒性失败

问题要求回答文档是否有蓝牙 Mesh 相关内容（期望答"无"）。  
实际 LLM 输出了无关但相似领域的内容（英文摘要、WiFi 模块介绍），**未能做出"不存在"的明确判断**。

检索层做了正确的事情（返回的 top chunks 确实不含蓝牙内容），但 LLM 没有利用这个"空"信号给出"无"的结论，而是用相关内容硬凑了一段回答。

**这是 LLM 的 prompt/指令遵循问题**，不是检索问题。

#### 2.2 引用精确率下降（82.5% vs 87.5%）

`stm32-remote-json-scoped` case 中，LLM 的引用格式出现了严重异常：
```
[json/通过USART1接口连接ESP-01S WiFi模块，构建双向通信链路...]
```
这显然不是一个有效的 chunk ID，而是把 chunk 内容直接当做引用路径了。这说明 LLM 在某些情况下会出现**引用格式幻觉**，在生成引用时混淆了内容和标识符。

#### 2.3 追问（follow-up）上下文管理失败

`follow-up-detail`（"详细一点"）：

检索问题被重建为 "这个智能浇花系统如何实现精准灌溉；补充要求：详细一点"——上下文感知正常。  
但 Top chunk 0/3，Context chunk 0/3，**和不带追问时检索结果完全一致**，说明"详细"指令没有改变检索策略，也没有扩大 top-k 的获取范围以纳入更细节的 chunk（如模糊PID 具体参数 c29-c34）。

---

### 🟡 问题三：排序层（Reranker）——区分度不足

从检索日志可以观察到一个系统性问题：

**失败 case 中，top-4 chunk 的分数极度接近（0.660~0.700 区间），正确 chunk 排名靠后但分差极小**：

| Case | 正确 chunk 排名 | 正确/最高分差 |
|------|--------------|------------|
| stm32-control | c35 排第4 | 0.695 vs 0.680（差 0.015）|
| stm32-control-prefixed | c35 排第4 | 0.695 vs 0.680 |

这意味着 BGE-Reranker 在这类"同文档内不同粒度问题"上区分度很差，**无法区分引言综述 vs 核心实现章节**。

对比成功的 case（如 `stm32-remote`）：
- 正确 chunk 分 1.013，第二名 0.691——分差 0.322，区分度极高。

**区分度差的原因**：BGE-Reranker 在 `procedure` 类问题（"如何实现 X"）上，对"含有 X 的概述文本"和"详细描述 X 实现的文本"之间的语义差异感知不足。

---

### 🟢 问题四：评估体系本身

#### 4.1 `stm32-summary` 的通过逻辑矛盾

- Answer signals: 5/5 ✅，Keyword: 5/5 ✅，Citation: 100/100 ✅
- 却因 "Top chunk signals 1/2，Context signals 1/2 不满足 required context 2" 而失败

这说明评估标准对 **summary 类问题要求过于严苛**——即使答案事实上已经覆盖了所有关键词且引用了正确来源，仍因为 chunk 信号不满足而判定失败。这可能是评估设计需要商榷的地方。

#### 4.2 引用召回下降与 json-scoped 案例相关

`stm32-remote-json-scoped` 的 citation recall=0（因为引用格式损坏），这拉低了整体平均引用召回率，属于一个 bug 级别的问题而非系统性能问题。

---

## 四、问题归因总结

| 问题 | 归因 | 严重度 |
|------|------|--------|
| procedure 类问题检索 chunk 粒度错误 | **检索层 BM25 偏向综述文本** | 🔴 高 |
| 多文档场景文档隔离失败 | **检索无文档约束** | 🔴 高 |
| Reranker 对"概述 vs 实现"区分度差 | **Reranker 模型能力/prompt** | 🟡 中 |
| LLM 无法识别"文档未提及"场景 | **LLM 系统 prompt 设计** | 🟡 中 |
| LLM 引用格式幻觉 | **LLM 输出解析/约束** | 🟡 中 |
| 追问场景检索策略不变 | **query 重写未影响检索策略** | 🟡 中 |
| composition 类 top-1 错误 | **聚焦词提取偏移** | 🟡 中 |

> **结论：主要问题在检索层和排序层，不是 Embedding 模型本身的问题**。  
> Embedding（BGE）在语义层面召回的候选集质量尚可（context signals 87.5% 证明正确 chunk 通常在上下文窗口内），但最终排序（top-1 到 top-4）经常出错，而 LLM 只看最靠前的 chunk 生成答案。

---

## 五、具体改进建议

### 检索层

1. **BM25 预筛降权引言/结论 chunk**：对识别为 overview/abstract/conclusion 类型的 chunk 在 BM25 分数上打折，避免因大量关键词堆砌而误排第一。

2. **procedure 类问题的 jsonStructure 加权**：当问题类型为 `procedure`/`module_implementation` 时，优先检索 section 级别的 content chunk，而非 overview/abstract 类 chunk。可以通过增加 `jsonBranch` 权重来实现（目前 procedure 类问题 jsonBranch 全为 0）。

3. **多文档场景强制文档约束**：当无法识别 `指定文档` 时（如追问、无前缀），在稀疏预筛阶段需要更保守的处理，避免跨文档召回。

4. **composition 类聚焦词扩展**：硬件组成类问题需要从多个子 section 召回（传感器、执行层、交互层都应命中），当前聚焦词策略只能命中单一路径。考虑 multi-query 扩展。

### 排序层（Reranker）

5. **Reranker 的 prompt 优化**：当前 BGE-Reranker 区分不了"宏观描述X"和"详细实现X"，可以尝试在 rerank 时构造更明确的 query（如"X 的具体实现步骤和技术细节" vs 单纯的"如何实现X"）。

6. **考虑章节深度惩罚**：较浅的章节（overview、引言）相对于同等语义分的深层 section，应有轻微的降权。

### LLM 生成层

7. **强化"不存在"识别指令**：在 system prompt 中增加明确规则：当检索到的 top chunks 均不包含问题相关内容时，应明确回答"文档中未提及"，而不是用相关内容凑答案。

8. **引用格式校验**：在 LLM 输出后增加引用格式的后处理校验，剔除不合法的引用格式（如引用路径包含正文内容的情况）。

9. **追问场景 top-k 扩展**：当识别到追问（`详细一点`、`展开说说` 等）时，应扩大 top-k 检索数量并适当调整排序，以纳入更细粒度的 chunk。

