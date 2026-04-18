# Eval Report: weavedoc-rag-baseline

- Baseline: `/home/tby/桌面/WeaveDoc/test/docs/eval-baseline.json`
- Executed at: `2026-04-18 09:52:19 +08:00`
- Case pass count: `5/8`
- Keyword coverage: `23/32 (71.9%)`

## Cases

### stm32-summary

- Category: `engineering-paper`
- Passed: `yes`
- Question: 总结一下基于STM32单片机的智能浇花系统设计这篇文档主要写了什么
- Expected keywords: `stm32`, `数据采集`, `决策控制`, `执行机构`, `交互显示`
- Matched keywords: `5/5`
- Required matches: `5`

**Answer**

```text
本文针对传统灌溉方式水资源浪费严重、自动化程度低的问题，设计了一种基于STM32单片机的智能浇花系统，通过多传感器融合与自适应控制算法实现精准灌溉，实验结果表明系统具备良好的实用性和推广价值 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > abstract | c6]。

1. **研究对象与问题**：传统灌溉方式水分利用率不足50%，存在定时灌溉忽视环境变化、缺乏闭环控制、无法远程监控等问题 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 0 引言 > content | c9]。

2. **方法或系统设计**：系统采用"感知、决策、执行、交互"四层架构，核心控制器选用STM32L031G6芯片，集成64KB Flash与8KB SRAM，动态功耗30μA/MHz，停机模式功耗0.35μA [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.1 主控芯片低功耗设计与核心架构 > content | c11]。系统包含数据采集、决策控制、执行机构和交互显示四大模块，硬件设计包括土壤湿度传感器、环境温湿度传感器、PWM驱动电磁阀模块、OLED显示及远程WiFi模块 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > abstract | c6]。

3. **关键发现或创新**：系统可将土壤湿度偏差控制在±2.8%范围内，较传统灌溉方式节水效率提升35%，植物存活率达98%以上 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > abstract | c6]。采用模糊PID控制算法动态调节电磁阀开度，实现精准灌溉，32MHz主频与单周期I/O访问技术使传感器数据采集、模糊PID算法运算、多任务调度等操作可在5ms内完成 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.1 主控芯片低功耗设计与核心架构 > content | c11]。
```

**Retrieval Debug**

```text
原始问题: 总结一下基于STM32单片机的智能浇花系统设计这篇文档主要写了什么
问题类型: summary
焦点词: overview, abstract, summary, stm32
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.951 (semantic=0.896, bm25=1.000, keyword=0.523, title=0.575, jsonStructure=0.426, coverage=0.750, neighbor=0.250, jsonBranch=0.250, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > overview | c10]
    overview: 系统采用“感知、决策、执行、交互”四层架构
[2] score=0.752 (semantic=0.842, bm25=0.499, keyword=0.318, title=0.250, jsonStructure=0.250, coverage=0.250, neighbor=0.750, jsonBranch=1.000, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 | c5]
    affiliation: 广西民族师范学院，崇左532200
[3] score=0.736 (semantic=0.885, bm25=0.536, keyword=0.318, title=0.250, jsonStructure=0.250, coverage=0.250, neighbor=0.250, jsonBranch=1.000, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 | c1]
    title: 基于STM32单片机的智能浇花系统设计
[4] score=0.723 (semantic=0.710, bm25=0.509, keyword=0.318, title=0.300, jsonStructure=0.124, coverage=0.250, neighbor=0.750, jsonBranch=0.750, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > keywords | c7]
    - STM32 - 智能灌溉 - 模糊PID - 电磁阀
上下文窗口: 10 条
```

### stm32-control

- Category: `engineering-paper`
- Passed: `yes`
- Question: 这个智能浇花系统如何实现精准灌溉
- Expected keywords: `模糊pid`, `土壤湿度`, `电磁阀`, `动态调节`
- Matched keywords: `4/4`
- Required matches: `4`

**Answer**

```text
根据当前文档，系统实现精准灌溉主要依靠以下环节：
1. abstract: 针对传统灌溉方式水资源浪费严重、自动化程度低的问题，设计了一种基于STM32单片机的智能浇花系统。系统采用模块化设计，包含数据采集、决策控制、执行机构和交互显示四大模块。通过对土壤湿度传感器、环境温湿度传感器实时采集的环境数据，结合模糊PID控制算法动态调节电磁阀开度，实现精准灌溉，并把数据信息显示及上传。硬件设计包括传感器信号采集、PWM驱动电磁阀模块、OLED显示及远程WiFi模块。实验结果表明，系统可将土壤湿度偏差控制在±2.8%范围内，较传统灌溉方式在节水效率上提升35%，植物存活率达98%以上，具备良好的实用性和推广价值，为家庭园艺与小型农业场景提供了高效解决方案。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > abstract | c6]
2. title: 基于STM32单片机的智能浇花系统设计 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 | c1]
3. content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准数据；动态调整：根据反馈流量实时修正占空比。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c16]
4. content: 针对家庭园艺场景的小流量、低功耗需求，选用常开型脉冲式电磁阀，其核心优势包括：①低压驱动：12V直流供电，适配系统电源设计；②精准控流：流量范围0.33L/min满足盆栽植物需水量；③快速响应：开启时间≤15ms，关闭时间≤20ms；④节能特性：脉冲式工作模式，稳态保持无需持续供电。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.1 电磁阀驱动模块 > content | c15]
```

**Retrieval Debug**

```text
原始问题: 这个智能浇花系统如何实现精准灌溉
问题类型: procedure
焦点词: 模糊pid, 土壤湿度, 控制算法, pid, 电磁阀, pwm, 占空比
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.738 (semantic=0.524, bm25=1.000, keyword=0.357, title=0.000, jsonStructure=0.000, coverage=0.429, neighbor=0.857, jsonBranch=0.857, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > keywords | c7]
    - STM32 - 智能灌溉 - 模糊PID - 电磁阀
[2] score=0.639 (semantic=0.587, bm25=0.668, keyword=0.321, title=0.286, jsonStructure=0.157, coverage=0.429, neighbor=0.429, jsonBranch=0.429, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第2项 | c24]
    湿度偏差(E): 正小(PS):+5%~+15% 变化率(EC): 缓慢变干(SD):+1%~+5%/min 输出动作: 中速补水(60%占空比)
[3] score=0.638 (semantic=0.616, bm25=0.668, keyword=0.321, title=0.286, jsonStructure=0.157, coverage=0.429, neighbor=0.286, jsonBranch=0.429, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第4项 | c26]
    湿度偏差(E): 负小(NS):-15%~-5% 变化率(EC): 缓慢变湿(SW):-5%~-1%/min 输出动作: 微量补水(20%占空比)
[4] score=0.638 (semantic=0.517, bm25=0.875, keyword=0.321, title=0.286, jsonStructure=0.286, coverage=0.429, neighbor=0.143, jsonBranch=0.000, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c16]
    content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准...
上下文窗口: 13 条
```

### stm32-remote

- Category: `engineering-paper`
- Passed: `yes`
- Question: 文档里远程状态显示及控制模块是怎么做的
- Expected keywords: `esp-01s`, `wifi`, `mqtt`, `远程`
- Matched keywords: `4/4`
- Required matches: `4`

**Answer**

```text
根据当前文档，系统实现精准灌溉主要依靠以下环节：
1. content: 通过USART1接口连接ESP-01S WiFi模块，构建双向通信链路，实现数据远程传输与设备控制功能。状态信息数据上传，采用轻量级MQTT协议，将土壤温湿度、环境参数、电磁阀状态及系统健康信息封装为JSON格式，每5秒周期性推送至云端服务器。手机APP端同步展示多维数据，包括土壤湿度、环境参数、电磁阀状态。用户可通过远程设定湿度阈值，或手动触发即时灌溉指令。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.2 远程状态显示及控制模块 > content | c18]
2. content: 通过SPI1接口驱动OLED显示屏，显示屏采用分层可视化设计，动态数据区与状态指示区协同呈现关键信息。动态数据区划分为左右两列，左列实时显示土壤参数，上方为湿度数据，下方为温度信息；右列展示环境参数，顶部显示空气湿度，底部为环境温度。电磁阀状态通过动态图标居中显示，开启时呈现蓝色水滴坠落动画，关闭时切换为灰色静态阀门图标。状态指示区位于屏幕顶端，WiFi信号强度以四格阶梯图标表示，正常运行时显示绿色环形进度条，异常时转为红色闪烁警示图标。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.1 本地状态显示模块 > content | c17]
3. architecture: 系统软件采用事件驱动型多线程架构，通过优先级调度与状态机管理实现高效资源利用。整体流程划分为数据采集、控制决策、执行输出三个核心线程，辅以用户交互后台任务 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > architecture | c19]
4. content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准数据；动态调整：根据反馈流量实时修正占空比。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c16]
```

**Retrieval Debug**

```text
原始问题: 文档里远程状态显示及控制模块是怎么做的
问题类型: procedure
焦点词: 多特征关联分析, esp-01s, 信息抽取模块, 多模态检索, usart, 知识图谱, wifi, mqtt
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.623 (semantic=0.488, bm25=1.000, keyword=0.486, title=0.000, jsonStructure=0.000, coverage=0.500, neighbor=0.125, jsonBranch=0.000, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.2 远程状态显示及控制模块 > content | c18]
    content: 通过USART1接口连接ESP-01S WiFi模块，构建双向通信链路，实现数据远程传输与设备控制功能。状态信息数据上传，采用轻量级MQTT协议，将土壤温湿度、环境参数、电磁阀状态及系统健康信息封装为JSON格式，每5秒周...
[2] score=0.581 (semantic=0.385, bm25=0.988, keyword=0.486, title=0.000, jsonStructure=0.000, coverage=0.500, neighbor=0.125, jsonBranch=0.000, directHit=True) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-18_01-06-27.md | 1.4.2 远程状态显示及控制模块 | c23]
    ## 1.4.2 远程状态显示及控制模块  通过USART1接口连接ESP-01SWiFi模块，构建双向通信链路，实现数据远程传输与设备控制功能 $ ^{[2]} $。状态信息数据上传，采用轻量级MQTT协议，将土壤温湿度、环境参数、电磁阀...
[3] score=0.417 (semantic=0.495, bm25=0.325, keyword=0.189, title=0.000, jsonStructure=0.000, coverage=0.125, neighbor=0.500, jsonBranch=0.000, directHit=True) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-18_01-06-27.md | 1.4.1 本地状态显示模块 | c22]
    ## 1.4.1 本地状态显示模块  通过SPI1接口驱动OLED显示屏，显示屏采用分层可视化设计，动态数据区与状态指示区协同呈现关键信息。动态数据区划分为左右两列，左列实时显示土壤参数，上方为湿度数据，下方为温度信息；右列展示环境参数，顶...
[4] score=0.394 (semantic=0.444, bm25=0.306, keyword=0.189, title=0.000, jsonStructure=0.000, coverage=0.125, neighbor=0.500, jsonBranch=0.000, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.1 本地状态显示模块 > content | c17]
    content: 通过SPI1接口驱动OLED显示屏，显示屏采用分层可视化设计，动态数据区与状态指示区协同呈现关键信息。动态数据区划分为左右两列，左列实时显示土壤参数，上方为湿度数据，下方为温度信息；右列展示环境参数，顶部显示空气湿度，底部...
上下文窗口: 10 条
```

### follow-up-detail

- Category: `engineering-paper`
- Passed: `yes`
- Question: 详细一点
- Expected keywords: `模糊pid`, `电磁阀`, `土壤湿度`
- Matched keywords: `3/3`
- Required matches: `3`

**Answer**

```text
根据当前文档，系统实现精准灌溉主要依靠以下环节：
1. abstract: 针对传统灌溉方式水资源浪费严重、自动化程度低的问题，设计了一种基于STM32单片机的智能浇花系统。系统采用模块化设计，包含数据采集、决策控制、执行机构和交互显示四大模块。通过对土壤湿度传感器、环境温湿度传感器实时采集的环境数据，结合模糊PID控制算法动态调节电磁阀开度，实现精准灌溉，并把数据信息显示及上传。硬件设计包括传感器信号采集、PWM驱动电磁阀模块、OLED显示及远程WiFi模块。实验结果表明，系统可将土壤湿度偏差控制在±2.8%范围内，较传统灌溉方式在节水效率上提升35%，植物存活率达98%以上，具备良好的实用性和推广价值，为家庭园艺与小型农业场景提供了高效解决方案。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > abstract | c6]
2. title: 基于STM32单片机的智能浇花系统设计 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 | c1]
3. content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准数据；动态调整：根据反馈流量实时修正占空比。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c16]
4. content: 针对家庭园艺场景的小流量、低功耗需求，选用常开型脉冲式电磁阀，其核心优势包括：①低压驱动：12V直流供电，适配系统电源设计；②精准控流：流量范围0.33L/min满足盆栽植物需水量；③快速响应：开启时间≤15ms，关闭时间≤20ms；④节能特性：脉冲式工作模式，稳态保持无需持续供电。 [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.1 电磁阀驱动模块 > content | c15]
```

**Retrieval Debug**

```text
原始问题: 详细一点
检索问题: 这个智能浇花系统如何实现精准灌溉；补充要求：详细一点
问题类型: procedure
焦点词: 模糊pid, 土壤湿度, 控制算法, pid, 电磁阀, pwm, 占空比
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.735 (semantic=0.517, bm25=1.000, keyword=0.357, title=0.000, jsonStructure=0.000, coverage=0.429, neighbor=0.857, jsonBranch=0.857, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > keywords | c7]
    - STM32 - 智能灌溉 - 模糊PID - 电磁阀
[2] score=0.634 (semantic=0.605, bm25=0.668, keyword=0.321, title=0.286, jsonStructure=0.157, coverage=0.429, neighbor=0.286, jsonBranch=0.429, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第4项 | c26]
    湿度偏差(E): 负小(NS):-15%~-5% 变化率(EC): 缓慢变湿(SW):-5%~-1%/min 输出动作: 微量补水(20%占空比)
[3] score=0.634 (semantic=0.507, bm25=0.875, keyword=0.321, title=0.286, jsonStructure=0.286, coverage=0.429, neighbor=0.143, jsonBranch=0.000, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c16]
    content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准...
[4] score=0.632 (semantic=0.569, bm25=0.668, keyword=0.321, title=0.286, jsonStructure=0.157, coverage=0.429, neighbor=0.429, jsonBranch=0.429, directHit=True) | [json/基于STM32单片机的智能浇花系统设计_彭霞.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第2项 | c24]
    湿度偏差(E): 正小(PS):+5%~+15% 变化率(EC): 缓慢变干(SD):+1%~+5%/min 输出动作: 中速补水(60%占空比)
上下文窗口: 13 条
```

### ai-shortdrama-summary

- Category: `humanities-paper`
- Passed: `yes`
- Question: 总结一下《从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示》这篇论文主要写了什么
- Expected keywords: `ai微短剧`, `文化传承`, `科幻`, `人机`
- Matched keywords: `4/4`
- Required matches: `4`

**Answer**

```text
title: 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示
author: 徐畅 [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 | c1]
abstract: 近年来，AI微短剧在技术突破和短视频生态演进的背景之下，成为数字内容领域的新范式。国产AI微短剧主要聚焦文化传承与科幻想象两大题材。本文深入讨论AI微短剧的发展现状，分析AI技术如何重构微短剧的生产机制、叙事形态与文化表达，并揭示其创作方法存在的艺术局限。通过案例分析与文本细读，文章发现AI技术虽能实现中华优秀传统文化的创造性转化，并拓展科幻题材的艺术表现力，但也会因此导致叙事同质化、戏剧性弱化与文化失真等问题。研究表明，破解上述困境需构建人机协同创作生态，以AI负责技术生态，以人类创作者主导；立意和情感深度，通过'技术赋能+人文引领'实现文化表达的创新突破。 [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > abstract | c5]
```

**Retrieval Debug**

```text
原始问题: 总结一下《从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示》这篇论文主要写了什么
问题类型: summary
焦点词: 微短剧发展的新趋势与新启示, 从文化传承到科幻未来, overview, abstract, summary, 未来发展路径, 叙事同质化, ai看典籍
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.794 (semantic=0.887, bm25=0.851, keyword=0.330, title=0.250, jsonStructure=0.250, coverage=0.250, neighbor=0.250, jsonBranch=0.875, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 | c1]
    title: 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 author: 徐畅
[2] score=0.791 (semantic=0.662, bm25=1.000, keyword=0.352, title=0.300, jsonStructure=0.124, coverage=0.250, neighbor=0.625, jsonBranch=0.625, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > keywords | c6]
    - AI微短剧 - 文化传承 - 科幻叙事 - 人机共创
[3] score=0.750 (semantic=0.753, bm25=0.845, keyword=0.352, title=0.375, jsonStructure=0.238, coverage=0.375, neighbor=0.375, jsonBranch=0.375, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > chapters > 第2项 > subsections > 第2项 | c18]
    title: 《AI看典籍》
[4] score=0.742 (semantic=0.699, bm25=0.944, keyword=0.352, title=0.375, jsonStructure=0.238, coverage=0.375, neighbor=0.375, jsonBranch=0.250, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > chapters > 第5项 > subsections > 第2项 | c60]
    title: 未来发展路径
上下文窗口: 12 条
```

### ai-shortdrama-challenges

- Category: `humanities-paper`
- Passed: `no`
- Question: 文中认为国产AI微短剧当前面临哪些问题，未来应走向什么路径
- Expected keywords: `叙事同质化`, `文化失真`, `人机`, `人文引领`
- Matched keywords: `1/4`
- Required matches: `3`

**Answer**

```text
content: 国产AI微短剧的快速发展为文化传承与科幻想象提供了全新的表达方式，但同时也面临着多重挑战。在技术层面，当前AI生成的内容仍存在叙事同质化、文化细节失真等问题，过度依赖数据训练可能导致短剧创作陷入'套路化'困境；在艺术层面，如何平衡AI算法的生产效率与短剧作为艺术品的戏剧性深度表达，避免'技术展演'掩盖人文内涵，成为行业亟需突破的瓶颈；而在伦理层面，AI对文化符号的挪用与重构，也引发了关于历史真实性、文化记忆归属权的争议和两难困境。 [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > chapters > 第5项 > subsections > 第1项 > content | c59]
title: 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示
author: 徐畅 [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 | c1]
```

**Retrieval Debug**

```text
原始问题: 文中认为国产AI微短剧当前面临哪些问题，未来应走向什么路径
问题类型: general
焦点词: 文中认为国产, 未来发展路径, 叙事同质化, 研究局限, 人机合作, 人机共创, 文化失真, 戏剧性
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.653 (semantic=0.691, bm25=1.000, keyword=0.170, title=0.125, jsonStructure=0.100, coverage=0.125, neighbor=0.375, jsonBranch=0.250, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > chapters > 第5项 > subsections > 第2项 | c60]
    title: 未来发展路径
[2] score=0.600 (semantic=0.690, bm25=0.904, keyword=0.149, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.250, jsonBranch=0.500, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > chapters > 第5项 > subsections > 第1项 | c58]
    title: 当前挑战
[3] score=0.581 (semantic=0.616, bm25=0.689, keyword=0.149, title=0.000, jsonStructure=0.000, coverage=0.125, neighbor=0.375, jsonBranch=0.375, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > keywords | c6]
    - AI微短剧 - 文化传承 - 科幻叙事 - 人机共创
[4] score=0.546 (semantic=0.677, bm25=0.677, keyword=0.170, title=0.125, jsonStructure=0.100, coverage=0.125, neighbor=0.000, jsonBranch=0.125, directHit=True) | [json/从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示_徐畅-2026-04-18_01-06-03.json | 从文化传承到科幻未来：国产AI微短剧发展的新趋势与新启示 > chapters > 第5项 > subsections > 第4项 | c64]
    title: 研究局限与未来方向
上下文窗口: 12 条
```

### geology-system-architecture

- Category: `system-paper`
- Passed: `no`
- Question: 《基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现》采用了什么总体架构和技术栈
- Expected keywords: `spring boot`, `vue3`, `mybatis-plus`, `echarts`
- Matched keywords: `2/4`
- Required matches: `4`

**Answer**

```text
title: 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现
title_en: Design and implementation of an intelligent extraction and visualization system for geological information via spring boot and vue [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 | c1]
content: 在具体技术选择上，系统结合了成熟度、易用性及在科研场景适用性进行权衡：
(1) 后端采用Spring Boot构建。该框架不仅能快速搭建服务，还集成内置Tomcat容器，便于系统独立运行和部署。系统利用Spring MVC来实现RESTful API接口，从而为前端提供统一的数据服务。在数据库交互方面，选用MyBatis-Plus，在保持MyBatis灵活性同时，简化了常见的CRUD操作，能显著降低开发成本。特别值得一提的是，核心的信息抽取功能通过HTTP方式调用本地部署的LLMs模型，这使得后端业务逻辑与模型推理解耦，便于后续替换或升级不同模型。 [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > sections > 第1项 > sections > 第1项 > sections > 第1项 > content | c31]
```

**Retrieval Debug**

```text
原始问题: 《基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现》采用了什么总体架构和技术栈
问题类型: general
焦点词: mybatis-plus, spring boot, restful api, echarts, spring, 系统总体架构, 前后端分离, mysql
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.832 (semantic=0.700, bm25=1.000, keyword=0.596, title=0.375, jsonStructure=0.238, coverage=0.375, neighbor=0.250, jsonBranch=1.000, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > sections > 第1项 > sections > 第1项 | c29]
    level: 2 number: 1.1 title: 系统总体架构
[2] score=0.749 (semantic=0.887, bm25=0.708, keyword=0.509, title=0.250, jsonStructure=0.138, coverage=0.250, neighbor=0.250, jsonBranch=0.250, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > authors > 第3项 | c7]
    name: 吴麒瑞
[3] score=0.748 (semantic=0.886, bm25=0.708, keyword=0.509, title=0.250, jsonStructure=0.138, coverage=0.250, neighbor=0.250, jsonBranch=0.250, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > authors > 第1项 | c2]
    name: 邱芹军
[4] score=0.748 (semantic=0.886, bm25=0.708, keyword=0.509, title=0.250, jsonStructure=0.138, coverage=0.250, neighbor=0.250, jsonBranch=0.250, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > authors > 第6项 | c14]
    name: 陶留锋
上下文窗口: 14 条
```

### geology-system-modules

- Category: `system-paper`
- Passed: `no`
- Question: 这篇地质信息抽取与可视化系统论文主要有哪些功能模块
- Expected keywords: `信息抽取`, `知识图谱`, `多模态检索`, `多特征关联分析`
- Matched keywords: `0/4`
- Required matches: `4`

**Answer**

```text
我不知道（当前文档未覆盖）。
```

**Retrieval Debug**

```text
原始问题: 这篇地质信息抽取与可视化系统论文主要有哪些功能模块
问题类型: usage
焦点词: 多特征关联分析, 信息抽取模块, 多模态检索, 知识图谱, 可视化
稀疏预筛候选: 371 条
进入语义计算: 48 条
是否使用稀疏预筛: 是
主命中: 4 条
[1] score=0.710 (semantic=0.450, bm25=0.861, keyword=0.400, title=0.400, jsonStructure=0.270, coverage=0.400, neighbor=0.600, jsonBranch=1.000, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > sections > 第2项 > sections > 第3项 | c64]
    level: 2 number: 2.3 title: 多模态检索模块
[2] score=0.707 (semantic=0.471, bm25=1.000, keyword=0.429, title=0.400, jsonStructure=0.270, coverage=0.400, neighbor=0.200, jsonBranch=0.800, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > sections > 第2项 > sections > 第1项 | c50]
    level: 2 number: 2.1 title: 基于大模型的地质信息抽取模块
[3] score=0.685 (semantic=0.493, bm25=0.903, keyword=0.457, title=0.200, jsonStructure=0.110, coverage=0.400, neighbor=0.200, jsonBranch=0.200, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > sections > 第3项 > sections > 第2项 > content | c74]
    content: 前端基于Vue3和Composition API进行开发，利用Vue Router管理页面路由。核心页面组件包括lithoextract.vue和KGragh.vue等。信息抽取功能主要由lithoextract.vue组...
[4] score=0.685 (semantic=0.432, bm25=0.850, keyword=0.400, title=0.400, jsonStructure=0.270, coverage=0.400, neighbor=0.400, jsonBranch=1.000, directHit=True) | [json/基于Spring_Boot和Vue的地质信息智能抽取与可视化系统设计与实现_邱芹军-2026-04-18_01-08-12.json | 基于Spring Boot和Vue的地质信息智能抽取与可视化系统设计与实现 > sections > 第2项 > sections > 第4项 | c67]
    level: 2 number: 2.4 title: 多特征关联分析模块
上下文窗口: 16 条
```

