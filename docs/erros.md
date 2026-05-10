[QA-Flow] LLM-answer-accepted
=== Case 1: stm32-control ===
Category: engineering-paper
Question: 这个智能浇花系统如何实现精准灌溉
Expected keywords: 模糊pid, 土壤湿度, 土壤温湿度, 电磁阀, 动态调节, 动态调整, 动态修正
Matched keywords: 5/7
Required matches: 4
Structural checks: pass
Answer signal checks: pass (5/7)
Retrieval checks: fail (top 0/3, context 3/3)
Citation checks: pass (matched 1/1, precision 17%, recall 100%)
Evidence checks: scope=pass, kind=pass, slots=100%, sufficiency=pass, citations=pass
Source format checks: pass (.json, .md)
Answer:
核心目标：系统通过STM32L031G6主控芯片、模糊PID复合控制算法与环境补偿机制，将土壤湿度误差稳定控制在±3%以内，实现精准灌溉与水资源高效管理 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 6 结论 > content | c44]。其关键实现流程如下：

1.  **多传感器数据融合采集**：系统利用土壤温湿度复合传感器和环境温湿度传感器实时采集数据。数据经过滤波算法消除噪声，并按空间分布加权融合，以准确反映植物根系区域的实际状态 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]。

2.  **智能控制决策生成**：根据实时采集数据与目标湿度的偏差大小，系统自动切换控制模式 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]。
    *   **大偏差模糊控制阶段**：当偏差超过设定阈值时，激活模糊控制，快速匹配经验规则以迅速缩小偏差 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]。
    *   **小偏差PID调节阶段**：当偏差进入较小范围后，系统切换至PID算法进行精准微调。比例系数根据当前偏差强度调整反应；积分系数智能衰减以防止因长时间累积导致的阀门开度突变；微分系数则通过分析湿度变化速度提前动作，有效抑制超调 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.2 PID调节阶段设计 > content | c35]。

3.  **环境补偿机制动态调整**：在PID调节基础上，系统内置智能环境感知模型，对灌溉量进行动态补偿。
    *   **温湿度耦合模型**：当温度超过30℃且空气干燥时，判定为高温蒸发模式，灌溉量自动提升至标准值的120%；在梅雨季节则自动缩减15%水量。模型还针对沙土、黏土等不同土壤类型设有预设方案，例如沙质土会多分配25%的水量 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.1 温湿度耦合模型 > content | c36]。
    *   **实时补偿策略**：系统每5分钟评估一次环境状态，并结合昼夜节律调节。白天光照足时增加10%-20%灌溉量，夜间则启动节水模式。检测到持续阴雨时进入防涝状态，暂停常规灌溉 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.2 实时补偿策略 > content | c37]。

4.  **执行机构精准控制**：最终决策量通过脉宽调制信号驱动电磁阀执行灌溉。系统采用软启动策略减少机械冲击，并实时监测电流与温度，异常时触发多级保护机制，确保灌溉量精确、安全地输出 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]。

**结果**：在模拟测试中，该套精准灌溉系统从30%干燥状态调节至60%目标湿度，仅需4.6秒且无超调，全程未出现明显水量波动，在复杂环境下湿度波动幅度控制在±4%以内，较传统灌溉方式节水效率提升超40% [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.2 控制性能测试 > content | c41]。

Retrieval debug:
原始问题: 这个智能浇花系统如何实现精准灌溉
检索管线: refactored
问题类型: procedure
焦点词: 精准灌溉, 智能浇花系统
稀疏预筛候选: 0 条
进入语义计算: 50 条
是否使用稀疏预筛: 否（回退全量语义）
学习式重排: BGE-Reranker (global:ok (50/50))
主命中: 50 条
[1] score=9.894 (semantic=0.764, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c17]
    学养护与资源节约的双重效益。 结合当下较为热点的AI模型，进行远程控制与智能策略的深度融合。当云端AI分析历史数据发现灌溉规律后，可自动下发优化参数，系统动态调整模糊PID规则库以适应新策略。 本智能浇花系统通过融合多传感器数据与智能控制算...
[2] score=9.756 (semantic=0.705, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c2]
    ，结合自适应控制算法实现精准灌溉。 系统采用“感知、决策、执行、交互”四层架构，如图1所示。 1. 1.1 主控芯片低功耗设计与核心架构本系统选用 STM32L031G6作为核心控制器，该芯片 基于ARM Cortex-M0+32位RISC...
[3] score=9.432 (semantic=0.722, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c8]
    循环：采用事件驱动架构，通过定时中断触发周期性任务，处理异步事件如用户操作或网络指令。任务调度兼顾实时性与低功耗需求，空闲时段自动进入休眠模式以降低能耗。$\textcircled{3}$获取传感器数据：多通道采集土壤与环境参数，通过滤波算...
[4] score=9.031 (semantic=0.748, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c1]
    （广西民族师范学院，崇左532200） （Guangxi Minzu Normal University, Chongzuo 532200, China) 随着全球水资源短缺问题日益严峻，农业用水效率提升成为研究热点。据统计，传统灌溉方式的...
[5] score=8.807 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.2 实时补偿策略 > content | c37]
    content: 系统每5分钟动态评估一次环境状态，结合昼夜节律智能调节：白天光照充足时采用防蒸腾补偿，在基础灌溉量上额外增加10%-20%；夜间启动节水模式，减少灌溉频率并降低单次供水量。当检测到持续阴雨天气时，自动进入防涝状态，暂停常规...
[6] score=8.783 (semantic=0.677, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.2 PID调节阶段设计 > content | c35]
    content: 采用自整定参数微调，积分项动态衰减防止饱和。在系统进入精确控制阶段时，PID算法根据实时偏差动态调整灌溉量。比例系数决定了对当前偏差的反应强度，当土壤湿度与目标值差距较大时，系统自动增强比例作用，快速缩小偏差；接近目标值时...
[7] score=8.611 (semantic=0.671, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
    - 初始化阶段：系统启动时执行全面初始化：配置各类外设接口，设置通信协议与时钟源，加载预设参数与历史配置。完成传感器通断检测与执行机构自检，确保硬件状态正常，异常时通过显示模块提示错误信息。 - 主程序循环：采用事件驱动架构，通过定时中断触...
[8] score=8.489 (semantic=0.672, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.1 温湿度耦合模型 > content | c36]
    content: 系统内置智能环境感知模型，将温度与湿度的影响关联分析。当气温升高时，土壤水分蒸发加速，模型会自动增加基础灌溉量；反之在低温潮湿环境下，则减少供水量防止积水。例如，温度超过30℃且空气干燥时，系统判定为高温蒸发模式，灌溉量提...
[9] score=8.389 (semantic=0.719, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c16]
    明显水量波动。系统如同经验丰富的园丁，既能快速补水又避免“浇过头”，在昼夜温差变化、突降雨水等复杂环境下，湿度波动幅度始终控制在 $ \pm 4\% $以内，较传统方式节水效率提升超 40%。 在实际家庭环境测试中，智能浇花系统展现出显著节...
[10] score=8.349 (semantic=0.718, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c14]
    动态调整灌溉阈值。 $ \textcircled{3} $引入更加科学合理的模糊PID复合控制器。基于农业专家经验与历史数据训练模型，建立更加科学合理的控制规则，并根据实时偏差动态调整灌溉量。 在完成系统软硬件整体设计后，基于Keil μV...
[11] score=8.068 (semantic=0.699, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.2 控制性能测试 > content | c41]
    content: 在模拟真实园艺场景的对比测试中，模糊PID控制系统展现出显著优势。当土壤湿度从干燥状态的30%快速调整至目标值的60%时，传统PID需8秒完成调节且会出现12%的过度浇水，而模糊PID仅用4.6秒即精准稳定在目标范围，全程...
[12] score=8.040 (semantic=0.768, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 6 结论 > content | c44]
    content: 本智能浇花系统通过融合多传感器数据与智能控制算法，实现了精准灌溉与水资源高效管理。实际测试表明，系统可将土壤湿度误差稳定控制在±3%以内，较传统灌溉方式节水50%以上，尤其适用于需水量差异大的植物类型，如绿萝节水48%、多...
[13] score=8.006 (semantic=0.742, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > abstract | c10]
    abstract: 针对传统灌溉方式水资源浪费严重、自动化程度低的问题，设计了一种基于STM32单片机的智能浇花系统。系统采用模块化设计，包含数据采集、决策控制、执行机构和交互显示四大模块。通过对土壤湿度传感器、环境温湿度传感器实时采集的环...
[14] score=7.931 (semantic=0.665, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c11]
    稳定(ST):1%~+1%/min维持当前状态负小(NS):15%~5%缓慢变湿(SW):5%~1%/mi微量补水(20%占空比)负大(NB):＜15%快速变湿(FW):＜5%/min紧急关阀 小偏差，但在持续干旱或过湿时，系统会智能衰减积...
[15] score=7.830 (semantic=0.696, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c15]
    行效率，调整任务调度优先级。在连续72 小时压力测试中，系统维持湿度控制误差 $ \pm 3\% $以内，灌溉响应延迟低于1.2秒，较传统方式节水率达 52%最终实现稳定可靠的自动化运行目标。 在实验室标准湿度箱中，对传感器进行了三点校准优...
[16] score=7.759 (semantic=0.694, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 4 系统方案的创新 > innovations | c38]
    - 土壤温湿度的空间维度融合设计。在土壤不同层面上放入3个传感器，并对采集的温湿度数据进行加权计算，其中表层占20%、中层占60%、底层占20%。 - 环境温湿度的数据融合策略。环境传感器数据用于修正土壤表层蒸发误差，当空气温度>30℃且湿...
[17] score=7.150 (semantic=0.716, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c13]
    ，如同给植物戴上智能遮阳帽，在暴晒时段临时提升补水强度，待光照减弱后恢复常态。这种拟人化的环境适应能力，使系统既能应对突变的天气状况，又能遵循植物自然生长规律，较传统灌溉方式节水 35%以上。 $ \textcircled{1} $土壤温湿...
[18] score=7.009 (semantic=0.694, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c12]
    120%；而在梅雨季节，即使温度适中，也会自动缩减 15%水量避免烂根。针对不同土壤类型，模型内置沙土、黏土、营养土等多种预设方案，沙质土因渗水快，在同等温湿度下会比黏土多分配 25%水量。 系统每5分钟动态评估一次环境状态，结合昼夜节律智...
[19] score=6.938 (semantic=0.650, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c10]
    湿度偏差是指目标湿度与实际湿度差值，划分为5个模糊集；偏差变化率是指单位时间内湿度变化量，划分为 5个模糊集。基于农业专家经验与历史数据训练，建立25条控制规则，部分示例如表 $ 1^{[4]} $。 在系统进入精确控制阶段时，PID算法根...
[20] score=6.855 (semantic=0.680, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c6]
    无滞后。其次对占空比映射，设定线性区间：占空比 10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准数据；动态调整：根据反馈流量实时修正占空比。 通过SPI1接口驱动OLED显示屏，显示屏采用...
[21] score=6.713 (semantic=0.637, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c23]
    content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准...
[22] score=6.711 (semantic=0.618, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第4项 | c33]
    湿度偏差(E): 负小(NS):-15%~-5% 变化率(EC): 缓慢变湿(SW):-5%~-1%/min 输出动作: 微量补水(20%占空比)
[23] score=6.706 (semantic=0.627, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第2项 | c31]
    湿度偏差(E): 正小(PS):+5%~+15% 变化率(EC): 缓慢变干(SD):+1%~+5%/min 输出动作: 中速补水(60%占空比)
[24] score=6.626 (semantic=0.658, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c4]
    ，使CPU负载率低于 $ 35\%^{[1]} $ 。 针对传统电阻式传感器易腐蚀、精度衰减的问题，选用电容式土壤温湿度传感器，其优势在于： $ \textcircled{1} $非接触测量： 采用高频电容检测技术，避免电极与土壤直接接触导...
[25] score=6.611 (semantic=0.721, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.3 节水效果评估 > content | c42]
    content: 在实际家庭环境测试中，智能浇花系统展现出显著节水优势。针对绿萝盆栽的30天对比实验显示，传统人工灌溉日均用水量达320毫升，而系统通过精准监测土壤湿度，仅在植物需水时启动灌溉，日均用水降至166毫升，节水效率达48%。在多...
[26] score=6.606 (semantic=0.693, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.1 传感器标定实验 > content | c40]
    content: 在实验室标准湿度箱中，对传感器进行了三点校准优化。首先将探头置于完全干燥环境确定基准零点，随后在60%标准湿度箱中调整线性度，最后通过水饱和状态验证上限精度。校准后的传感器在沙土、黏土等不同基质中实测显示，湿度检测误差稳定...
[27] score=6.539 (semantic=0.681, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.2 感知层 > 1.2.2 环境温湿度传感器 > content | c21]
    content: 为精准评估蒸发量对灌溉需求的影响，选用数字式温湿度传感器，其特点包括：①集成化设计：将温湿度传感单元与信号处理电路集成于3×3mm芯片；②低功耗特性：工作电流仅0.2mA，适合长期连续监测；③快速响应：湿度检测响应时间<8...
[28] score=6.415 (semantic=0.671, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c7]
    电磁阀状态及系统健康信息封装为JSON格式，每5秒周期性推送至云端服务器。手机APP端同步展示多维数据，包括土壤湿度、环境参数、电磁阀状态。用户可通过远程设定湿度阈值，或手动触发即时灌溉指令。 系统软件采用事件驱动型多线程架构，通过优先级调...
[29] score=6.393 (semantic=0.615, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c5]
    hrm{mm} $芯片； $ \textcircled{2} $低功耗特性：工作电流仅 0.2mA，适合长期连续监测； $ \textcircled{3} $快速响应：湿度检测响应时间<8秒。 针对家庭园艺场景的小流量、低功耗需求，选用常开...
[30] score=6.341 (semantic=0.694, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c9]
    标直观反映灌溉进程。界面支持异常信息优先显示，用户可通过交互菜单查看历史记录与系统配置。$\textcircled{7}$灌溉终止管理：满足目标湿度、人工干预或检测到故障时，立即停止灌溉操作。执行机构关断过程包含能量释放与状态锁定，防止误触...
[31] score=6.179 (semantic=0.758, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 0 引言 > content | c16]
    content: 随着全球水资源短缺问题日益严峻，农业用水效率提升成为研究热点。据统计，传统灌溉方式的水分利用率不足50%而智能灌溉系统可提升至85%以上。家庭园艺场景中，现有浇花装置普遍存在以下问题：定时灌溉忽视环境变化、缺乏闭环控制、无...
[32] score=6.108 (semantic=0.697, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.2 高精度数据采集与多模态通信 > content | c19]
    content: 主控芯片配备的12位逐次逼近型ADC模块，支持10通道扫描模式与硬件过采样功能，可将有效分辨率提升至16位。针对土壤湿度传感器的0-3V模拟信号，ADC以100kSPS速率采样，结合内置温度传感器实时补偿环境漂移，使湿度检...
[33] score=6.100 (semantic=0.615, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第3项 | c32]
    湿度偏差(E): 零(ZO):-5%~+5% 变化率(EC): 稳定(ST):-1%~+1%/min 输出动作: 维持当前状态
[34] score=6.026 (semantic=0.687, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.2 远程状态显示及控制模块 > content | c25]
    content: 通过USART1接口连接ESP-01S WiFi模块，构建双向通信链路，实现数据远程传输与设备控制功能。状态信息数据上传，采用轻量级MQTT协议，将土壤温湿度、环境参数、电磁阀状态及系统健康信息封装为JSON格式，每5秒周...
[35] score=5.750 (semantic=0.706, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.4 优化方案 > content | c43]
    content: 结合当下较为热点的AI模型，进行远程控制与智能策略的深度融合。当云端AI分析历史数据发现灌溉规律后，可自动下发优化参数，系统动态调整模糊PID规则库以适应新策略。
[36] score=5.659 (semantic=0.664, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.1 电磁阀驱动模块 > content | c22]
    content: 针对家庭园艺场景的小流量、低功耗需求，选用常开型脉冲式电磁阀，其核心优势包括：①低压驱动：12V直流供电，适配系统电源设计；②精准控流：流量范围0.33L/min满足盆栽植物需水量；③快速响应：开启时间≤15ms，关闭时间...
[37] score=5.448 (semantic=0.677, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.2 感知层 > 1.2.1 土壤温湿度复合传感器 > content | c20]
    content: 针对传统电阻式传感器易腐蚀、精度衰减的问题，选用电容式土壤温湿度传感器，其优势在于：①非接触测量：采用高频电容检测技术，避免电极与土壤直接接触导致的电解腐蚀；②长期稳定性：陶瓷封装探针可耐受酸碱土壤环境；③温度补偿：内置N...
[38] score=5.233 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c28]
    - 灌溉终止管理：满足目标湿度、人工干预或检测到故障时，立即停止灌溉操作。执行机构关断过程包含能量释放与状态锁定，防止误触发或二次损害。 - 数据同步与存储：本地缓存关键运行数据，定期持久化存储防止丢失。无线模块将数据同步至远程平台，支持移...
[39] score=5.221 (semantic=0.624, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > content | c29]
    content: 湿度偏差是指目标湿度与实际湿度差值，划分为5个模糊集；偏差变化率是指单位时间内湿度变化量，划分为5个模糊集。基于农业专家经验与历史数据训练，建立25条控制规则。
[40] score=5.186 (semantic=0.632, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c3]
    SRAM配备硬件奇偶校验功能，确保户外复杂环境下的数据可靠性。32MHz主频与单周期I/O访问技术，使得传感器数据采集、模糊PID算法运算、多任务调度等操作可在5ms内完成，满足实时控制需求。 主控芯片配备的12位逐次逼近型ADC模块，支持...
[41] score=4.188 (semantic=0.626, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > architecture | c26]
    architecture: 系统软件采用事件驱动型多线程架构，通过优先级调度与状态机管理实现高效资源利用。整体流程划分为数据采集、控制决策、执行输出三个核心线程，辅以用户交互后台任务
[42] score=4.115 (semantic=0.657, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.1 主控芯片低功耗设计与核心架构 > content | c18]
    content: 本系统选用STM32L031G6作为核心控制器，该芯片基于ARM Cortex-M0+32位RISC处理器内核，在实现高效运算的同时显著降低能耗。其动态功耗仅为30μA/MHz，在停机模式下功耗可降至0.35μA。芯片集成...
[43] score=3.694 (semantic=0.660, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.1 本地状态显示模块 > content | c24]
    content: 通过SPI1接口驱动OLED显示屏，显示屏采用分层可视化设计，动态数据区与状态指示区协同呈现关键信息。动态数据区划分为左右两列，左列实时显示土壤参数，上方为湿度数据，下方为温度信息；右列展示环境参数，顶部显示空气湿度，底部...
[44] score=2.475 (semantic=0.680, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c12]
    nd environmental temperature and humidity sensors, combined with fuzzy PID control algorithm to dynamically adjust the o...
[45] score=2.214 (semantic=0.629, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > keywords | c14]
    - STM32 - 智能灌溉 - 模糊PID - 电磁阀
[46] score=1.553 (semantic=0.634, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c13]
    l moisture deviation within the range of ±2.8%, which improves the water-saving efficiency by 35% compared to traditiona...
[47] score=1.277 (semantic=0.677, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c11]
    englishAbstract: In response to the serious waste of water resources and low automation level in traditional irrigation ...
[48] score=1.236 (semantic=0.623, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishTitle | c2]
    englishTitle: Design of Intelligent Watering System Based on STM32 Microcontroller
[49] score=0.940 (semantic=0.639, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > title | c1]
    title: 基于STM32单片机的智能浇花系统设计
[50] score=0.372 (semantic=0.662, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > overview | c17]
    overview: 系统采用“感知、决策、执行、交互”四层架构
上下文窗口: 8 条

llama_context: constructing llama_context
llama_context: n_seq_max     = 64
llama_context: n_ctx         = 8192
llama_context: n_ctx_seq     = 8192
llama_context: n_batch       = 512
llama_context: n_ubatch      = 512
llama_context: causal_attn   = 0
llama_context: flash_attn    = enabled
llama_context: kv_unified    = true
llama_context: freq_base     = 10000.0
llama_context: freq_scale    = 1
set_abort_callback: call
llama_context:        CPU  output buffer size =    61.04 MiB
llama_context: enumerating backends
llama_context: backend_ptrs.size() = 1
llama_context: max_nodes = 3112
llama_context: worst-case: n_tokens = 512, n_seqs = 64, n_outputs = 64
graph_reserve: reserving a graph for ubatch with n_tokens =  512, n_seqs = 64, n_outputs =  512
graph_reserve: reserving a graph for ubatch with n_tokens =   64, n_seqs = 64, n_outputs =   64
graph_reserve: reserving a graph for ubatch with n_tokens =  512, n_seqs = 64, n_outputs =  512
llama_context:        CPU compute buffer size =    14.51 MiB
llama_context: graph nodes  = 779
llama_context: graph splits = 1
set_embeddings: value = 1
decode: cannot decode batches with this context (calling encode() instead)
init: embeddings required but some input tokens were not marked as outputs -> overriding
~llama_context:        CPU compute buffer size is  14.5059 MiB, matches expectation of  14.5059 MiB
[QA-Reject] A4-title-author-block
[QA-Flow] LLM-answer-rejected, attempting repair... q='这个智能浇花系统如何实现精准灌溉'
[QA-Reject] A7-unknown-citation
[QA-Flow] Repair-rejected, falling back to local fallback...
[QA-Flow] Fallback-accepted
=== Case 2: stm32-control-prefixed ===
Category: engineering-paper
Question: 对于STM32这篇论文，这个智能浇花系统如何实现精准灌溉
Expected keywords: 土壤湿度, 环境温湿度, 模糊pid, 电磁阀, pwm
Matched keywords: 1/5
Required matches: 4
Structural checks: fail
Answer signal checks: fail (1/5)
Retrieval checks: fail (top 0/3, context 3/3)
Citation checks: pass (matched 1/1, precision 33%, recall 100%)
Evidence checks: scope=pass, kind=pass, slots=100%, sufficiency=pass, citations=pass
Source format checks: pass (.json, .md)
Answer:
[json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
- 输入/前提：- 获取传感器数据：多通道采集土壤与环境参数，通过滤波算法消除噪声干扰，结合动态校准策略提升数据可靠性。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
- 判断或处理：在系统进入精确控制阶段时，PID算法根据实时偏差动态调整灌溉量。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.2 PID调节阶段设计 > content | c35]
- 执行动作：- 主程序循环：采用事件驱动架构，通过定时中断触发周期性任务，处理异步事件如用户操作或网络指令。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
- 结果/效果：系统在昼夜温差变化、突降雨水等复杂环境下，湿度波动幅度始终控制在±4%以内，较传统方式节水效率提升超40%。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.2 控制性能测试 > content | c41]

Retrieval debug:
原始问题: 对于STM32这篇论文，这个智能浇花系统如何实现精准灌溉
检索问题: 这个智能浇花系统如何实现精准灌溉
检索管线: refactored
问题类型: procedure
焦点词: 精准灌溉, 智能浇花系统
稀疏预筛候选: 0 条
进入语义计算: 50 条
是否使用稀疏预筛: 否（回退全量语义）
学习式重排: BGE-Reranker (global:ok (50/50))
主命中: 50 条
[1] score=9.894 (semantic=0.764, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c17]
    学养护与资源节约的双重效益。 结合当下较为热点的AI模型，进行远程控制与智能策略的深度融合。当云端AI分析历史数据发现灌溉规律后，可自动下发优化参数，系统动态调整模糊PID规则库以适应新策略。 本智能浇花系统通过融合多传感器数据与智能控制算...
[2] score=9.756 (semantic=0.705, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c2]
    ，结合自适应控制算法实现精准灌溉。 系统采用“感知、决策、执行、交互”四层架构，如图1所示。 1. 1.1 主控芯片低功耗设计与核心架构本系统选用 STM32L031G6作为核心控制器，该芯片 基于ARM Cortex-M0+32位RISC...
[3] score=9.432 (semantic=0.722, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c8]
    循环：采用事件驱动架构，通过定时中断触发周期性任务，处理异步事件如用户操作或网络指令。任务调度兼顾实时性与低功耗需求，空闲时段自动进入休眠模式以降低能耗。$\textcircled{3}$获取传感器数据：多通道采集土壤与环境参数，通过滤波算...
[4] score=9.040 (semantic=0.748, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c1]
    （广西民族师范学院，崇左532200） （Guangxi Minzu Normal University, Chongzuo 532200, China) 随着全球水资源短缺问题日益严峻，农业用水效率提升成为研究热点。据统计，传统灌溉方式的...
[5] score=8.807 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.2 实时补偿策略 > content | c37]
    content: 系统每5分钟动态评估一次环境状态，结合昼夜节律智能调节：白天光照充足时采用防蒸腾补偿，在基础灌溉量上额外增加10%-20%；夜间启动节水模式，减少灌溉频率并降低单次供水量。当检测到持续阴雨天气时，自动进入防涝状态，暂停常规...
[6] score=8.783 (semantic=0.677, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.2 PID调节阶段设计 > content | c35]
    content: 采用自整定参数微调，积分项动态衰减防止饱和。在系统进入精确控制阶段时，PID算法根据实时偏差动态调整灌溉量。比例系数决定了对当前偏差的反应强度，当土壤湿度与目标值差距较大时，系统自动增强比例作用，快速缩小偏差；接近目标值时...
[7] score=8.611 (semantic=0.671, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
    - 初始化阶段：系统启动时执行全面初始化：配置各类外设接口，设置通信协议与时钟源，加载预设参数与历史配置。完成传感器通断检测与执行机构自检，确保硬件状态正常，异常时通过显示模块提示错误信息。 - 主程序循环：采用事件驱动架构，通过定时中断触...
[8] score=8.489 (semantic=0.672, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.1 温湿度耦合模型 > content | c36]
    content: 系统内置智能环境感知模型，将温度与湿度的影响关联分析。当气温升高时，土壤水分蒸发加速，模型会自动增加基础灌溉量；反之在低温潮湿环境下，则减少供水量防止积水。例如，温度超过30℃且空气干燥时，系统判定为高温蒸发模式，灌溉量提...
[9] score=8.389 (semantic=0.719, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c16]
    明显水量波动。系统如同经验丰富的园丁，既能快速补水又避免“浇过头”，在昼夜温差变化、突降雨水等复杂环境下，湿度波动幅度始终控制在 $ \pm 4\% $以内，较传统方式节水效率提升超 40%。 在实际家庭环境测试中，智能浇花系统展现出显著节...
[10] score=8.349 (semantic=0.718, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c14]
    动态调整灌溉阈值。 $ \textcircled{3} $引入更加科学合理的模糊PID复合控制器。基于农业专家经验与历史数据训练模型，建立更加科学合理的控制规则，并根据实时偏差动态调整灌溉量。 在完成系统软硬件整体设计后，基于Keil μV...
[11] score=8.068 (semantic=0.699, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.2 控制性能测试 > content | c41]
    content: 在模拟真实园艺场景的对比测试中，模糊PID控制系统展现出显著优势。当土壤湿度从干燥状态的30%快速调整至目标值的60%时，传统PID需8秒完成调节且会出现12%的过度浇水，而模糊PID仅用4.6秒即精准稳定在目标范围，全程...
[12] score=8.048 (semantic=0.768, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 6 结论 > content | c44]
    content: 本智能浇花系统通过融合多传感器数据与智能控制算法，实现了精准灌溉与水资源高效管理。实际测试表明，系统可将土壤湿度误差稳定控制在±3%以内，较传统灌溉方式节水50%以上，尤其适用于需水量差异大的植物类型，如绿萝节水48%、多...
[13] score=8.006 (semantic=0.742, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > abstract | c10]
    abstract: 针对传统灌溉方式水资源浪费严重、自动化程度低的问题，设计了一种基于STM32单片机的智能浇花系统。系统采用模块化设计，包含数据采集、决策控制、执行机构和交互显示四大模块。通过对土壤湿度传感器、环境温湿度传感器实时采集的环...
[14] score=7.931 (semantic=0.665, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c11]
    稳定(ST):1%~+1%/min维持当前状态负小(NS):15%~5%缓慢变湿(SW):5%~1%/mi微量补水(20%占空比)负大(NB):＜15%快速变湿(FW):＜5%/min紧急关阀 小偏差，但在持续干旱或过湿时，系统会智能衰减积...
[15] score=7.830 (semantic=0.696, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c15]
    行效率，调整任务调度优先级。在连续72 小时压力测试中，系统维持湿度控制误差 $ \pm 3\% $以内，灌溉响应延迟低于1.2秒，较传统方式节水率达 52%最终实现稳定可靠的自动化运行目标。 在实验室标准湿度箱中，对传感器进行了三点校准优...
[16] score=7.759 (semantic=0.694, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 4 系统方案的创新 > innovations | c38]
    - 土壤温湿度的空间维度融合设计。在土壤不同层面上放入3个传感器，并对采集的温湿度数据进行加权计算，其中表层占20%、中层占60%、底层占20%。 - 环境温湿度的数据融合策略。环境传感器数据用于修正土壤表层蒸发误差，当空气温度>30℃且湿...
[17] score=7.150 (semantic=0.716, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c13]
    ，如同给植物戴上智能遮阳帽，在暴晒时段临时提升补水强度，待光照减弱后恢复常态。这种拟人化的环境适应能力，使系统既能应对突变的天气状况，又能遵循植物自然生长规律，较传统灌溉方式节水 35%以上。 $ \textcircled{1} $土壤温湿...
[18] score=7.009 (semantic=0.694, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c12]
    120%；而在梅雨季节，即使温度适中，也会自动缩减 15%水量避免烂根。针对不同土壤类型，模型内置沙土、黏土、营养土等多种预设方案，沙质土因渗水快，在同等温湿度下会比黏土多分配 25%水量。 系统每5分钟动态评估一次环境状态，结合昼夜节律智...
[19] score=6.938 (semantic=0.650, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c10]
    湿度偏差是指目标湿度与实际湿度差值，划分为5个模糊集；偏差变化率是指单位时间内湿度变化量，划分为 5个模糊集。基于农业专家经验与历史数据训练，建立25条控制规则，部分示例如表 $ 1^{[4]} $。 在系统进入精确控制阶段时，PID算法根...
[20] score=6.855 (semantic=0.680, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c6]
    无滞后。其次对占空比映射，设定线性区间：占空比 10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准数据；动态调整：根据反馈流量实时修正占空比。 通过SPI1接口驱动OLED显示屏，显示屏采用...
[21] score=6.713 (semantic=0.637, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c23]
    content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准...
[22] score=6.711 (semantic=0.618, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第4项 | c33]
    湿度偏差(E): 负小(NS):-15%~-5% 变化率(EC): 缓慢变湿(SW):-5%~-1%/min 输出动作: 微量补水(20%占空比)
[23] score=6.701 (semantic=0.627, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第2项 | c31]
    湿度偏差(E): 正小(PS):+5%~+15% 变化率(EC): 缓慢变干(SD):+1%~+5%/min 输出动作: 中速补水(60%占空比)
[24] score=6.626 (semantic=0.658, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c4]
    ，使CPU负载率低于 $ 35\%^{[1]} $ 。 针对传统电阻式传感器易腐蚀、精度衰减的问题，选用电容式土壤温湿度传感器，其优势在于： $ \textcircled{1} $非接触测量： 采用高频电容检测技术，避免电极与土壤直接接触导...
[25] score=6.611 (semantic=0.721, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.3 节水效果评估 > content | c42]
    content: 在实际家庭环境测试中，智能浇花系统展现出显著节水优势。针对绿萝盆栽的30天对比实验显示，传统人工灌溉日均用水量达320毫升，而系统通过精准监测土壤湿度，仅在植物需水时启动灌溉，日均用水降至166毫升，节水效率达48%。在多...
[26] score=6.606 (semantic=0.693, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.1 传感器标定实验 > content | c40]
    content: 在实验室标准湿度箱中，对传感器进行了三点校准优化。首先将探头置于完全干燥环境确定基准零点，随后在60%标准湿度箱中调整线性度，最后通过水饱和状态验证上限精度。校准后的传感器在沙土、黏土等不同基质中实测显示，湿度检测误差稳定...
[27] score=6.539 (semantic=0.681, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.2 感知层 > 1.2.2 环境温湿度传感器 > content | c21]
    content: 为精准评估蒸发量对灌溉需求的影响，选用数字式温湿度传感器，其特点包括：①集成化设计：将温湿度传感单元与信号处理电路集成于3×3mm芯片；②低功耗特性：工作电流仅0.2mA，适合长期连续监测；③快速响应：湿度检测响应时间<8...
[28] score=6.415 (semantic=0.671, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c7]
    电磁阀状态及系统健康信息封装为JSON格式，每5秒周期性推送至云端服务器。手机APP端同步展示多维数据，包括土壤湿度、环境参数、电磁阀状态。用户可通过远程设定湿度阈值，或手动触发即时灌溉指令。 系统软件采用事件驱动型多线程架构，通过优先级调...
[29] score=6.393 (semantic=0.615, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c5]
    hrm{mm} $芯片； $ \textcircled{2} $低功耗特性：工作电流仅 0.2mA，适合长期连续监测； $ \textcircled{3} $快速响应：湿度检测响应时间<8秒。 针对家庭园艺场景的小流量、低功耗需求，选用常开...
[30] score=6.341 (semantic=0.694, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c9]
    标直观反映灌溉进程。界面支持异常信息优先显示，用户可通过交互菜单查看历史记录与系统配置。$\textcircled{7}$灌溉终止管理：满足目标湿度、人工干预或检测到故障时，立即停止灌溉操作。执行机构关断过程包含能量释放与状态锁定，防止误触...
[31] score=6.179 (semantic=0.758, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 0 引言 > content | c16]
    content: 随着全球水资源短缺问题日益严峻，农业用水效率提升成为研究热点。据统计，传统灌溉方式的水分利用率不足50%而智能灌溉系统可提升至85%以上。家庭园艺场景中，现有浇花装置普遍存在以下问题：定时灌溉忽视环境变化、缺乏闭环控制、无...
[32] score=6.108 (semantic=0.697, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.2 高精度数据采集与多模态通信 > content | c19]
    content: 主控芯片配备的12位逐次逼近型ADC模块，支持10通道扫描模式与硬件过采样功能，可将有效分辨率提升至16位。针对土壤湿度传感器的0-3V模拟信号，ADC以100kSPS速率采样，结合内置温度传感器实时补偿环境漂移，使湿度检...
[33] score=6.100 (semantic=0.615, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第3项 | c32]
    湿度偏差(E): 零(ZO):-5%~+5% 变化率(EC): 稳定(ST):-1%~+1%/min 输出动作: 维持当前状态
[34] score=6.026 (semantic=0.687, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.2 远程状态显示及控制模块 > content | c25]
    content: 通过USART1接口连接ESP-01S WiFi模块，构建双向通信链路，实现数据远程传输与设备控制功能。状态信息数据上传，采用轻量级MQTT协议，将土壤温湿度、环境参数、电磁阀状态及系统健康信息封装为JSON格式，每5秒周...
[35] score=5.750 (semantic=0.706, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.4 优化方案 > content | c43]
    content: 结合当下较为热点的AI模型，进行远程控制与智能策略的深度融合。当云端AI分析历史数据发现灌溉规律后，可自动下发优化参数，系统动态调整模糊PID规则库以适应新策略。
[36] score=5.659 (semantic=0.664, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.1 电磁阀驱动模块 > content | c22]
    content: 针对家庭园艺场景的小流量、低功耗需求，选用常开型脉冲式电磁阀，其核心优势包括：①低压驱动：12V直流供电，适配系统电源设计；②精准控流：流量范围0.33L/min满足盆栽植物需水量；③快速响应：开启时间≤15ms，关闭时间...
[37] score=5.448 (semantic=0.677, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.2 感知层 > 1.2.1 土壤温湿度复合传感器 > content | c20]
    content: 针对传统电阻式传感器易腐蚀、精度衰减的问题，选用电容式土壤温湿度传感器，其优势在于：①非接触测量：采用高频电容检测技术，避免电极与土壤直接接触导致的电解腐蚀；②长期稳定性：陶瓷封装探针可耐受酸碱土壤环境；③温度补偿：内置N...
[38] score=5.233 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c28]
    - 灌溉终止管理：满足目标湿度、人工干预或检测到故障时，立即停止灌溉操作。执行机构关断过程包含能量释放与状态锁定，防止误触发或二次损害。 - 数据同步与存储：本地缓存关键运行数据，定期持久化存储防止丢失。无线模块将数据同步至远程平台，支持移...
[39] score=5.214 (semantic=0.624, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > content | c29]
    content: 湿度偏差是指目标湿度与实际湿度差值，划分为5个模糊集；偏差变化率是指单位时间内湿度变化量，划分为5个模糊集。基于农业专家经验与历史数据训练，建立25条控制规则。
[40] score=5.186 (semantic=0.632, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [markdown/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.md | 基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14 | c3]
    SRAM配备硬件奇偶校验功能，确保户外复杂环境下的数据可靠性。32MHz主频与单周期I/O访问技术，使得传感器数据采集、模糊PID算法运算、多任务调度等操作可在5ms内完成，满足实时控制需求。 主控芯片配备的12位逐次逼近型ADC模块，支持...
[41] score=4.197 (semantic=0.626, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > architecture | c26]
    architecture: 系统软件采用事件驱动型多线程架构，通过优先级调度与状态机管理实现高效资源利用。整体流程划分为数据采集、控制决策、执行输出三个核心线程，辅以用户交互后台任务
[42] score=4.115 (semantic=0.657, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.1 主控芯片低功耗设计与核心架构 > content | c18]
    content: 本系统选用STM32L031G6作为核心控制器，该芯片基于ARM Cortex-M0+32位RISC处理器内核，在实现高效运算的同时显著降低能耗。其动态功耗仅为30μA/MHz，在停机模式下功耗可降至0.35μA。芯片集成...
[43] score=3.694 (semantic=0.660, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.1 本地状态显示模块 > content | c24]
    content: 通过SPI1接口驱动OLED显示屏，显示屏采用分层可视化设计，动态数据区与状态指示区协同呈现关键信息。动态数据区划分为左右两列，左列实时显示土壤参数，上方为湿度数据，下方为温度信息；右列展示环境参数，顶部显示空气湿度，底部...
[44] score=2.475 (semantic=0.680, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c12]
    nd environmental temperature and humidity sensors, combined with fuzzy PID control algorithm to dynamically adjust the o...
[45] score=2.215 (semantic=0.629, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > keywords | c14]
    - STM32 - 智能灌溉 - 模糊PID - 电磁阀
[46] score=1.553 (semantic=0.634, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c13]
    l moisture deviation within the range of ±2.8%, which improves the water-saving efficiency by 35% compared to traditiona...
[47] score=1.266 (semantic=0.677, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c11]
    englishAbstract: In response to the serious waste of water resources and low automation level in traditional irrigation ...
[48] score=1.241 (semantic=0.623, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishTitle | c2]
    englishTitle: Design of Intelligent Watering System Based on STM32 Microcontroller
[49] score=0.940 (semantic=0.639, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > title | c1]
    title: 基于STM32单片机的智能浇花系统设计
[50] score=0.372 (semantic=0.662, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > overview | c17]
    overview: 系统采用“感知、决策、执行、交互”四层架构
上下文窗口: 8 条

llama_context: constructing llama_context
llama_context: n_seq_max     = 64
llama_context: n_ctx         = 8192
llama_context: n_ctx_seq     = 8192
llama_context: n_batch       = 512
llama_context: n_ubatch      = 512
llama_context: causal_attn   = 0
llama_context: flash_attn    = enabled
llama_context: kv_unified    = true
llama_context: freq_base     = 10000.0
llama_context: freq_scale    = 1
set_abort_callback: call
llama_context:        CPU  output buffer size =    61.04 MiB
llama_context: enumerating backends
llama_context: backend_ptrs.size() = 1
llama_context: max_nodes = 3112
llama_context: worst-case: n_tokens = 512, n_seqs = 64, n_outputs = 64
graph_reserve: reserving a graph for ubatch with n_tokens =  512, n_seqs = 64, n_outputs =  512
graph_reserve: reserving a graph for ubatch with n_tokens =   64, n_seqs = 64, n_outputs =   64
graph_reserve: reserving a graph for ubatch with n_tokens =  512, n_seqs = 64, n_outputs =  512
llama_context:        CPU compute buffer size =    14.51 MiB
llama_context: graph nodes  = 779
llama_context: graph splits = 1
set_embeddings: value = 1
decode: cannot decode batches with this context (calling encode() instead)
init: embeddings required but some input tokens were not marked as outputs -> overriding
~llama_context:        CPU compute buffer size is  14.5059 MiB, matches expectation of  14.5059 MiB
[QA-Reject] A7-unknown-citation
[QA-Flow] LLM-answer-rejected, attempting repair... q='这个智能浇花系统如何实现精准灌溉；补充要求：详细一点。重点关注：PID、PWM、pid、pwm、经模糊'
[QA-Reject] A4-title-author-block
[QA-Flow] Repair-rejected, falling back to local fallback...
[QA-Flow] Fallback-accepted
=== Case 3: follow-up-detail ===
Category: engineering-paper
Question: 详细一点
Expected keywords: 模糊pid, 电磁阀, 土壤湿度, 土壤温湿度
Matched keywords: 1/4
Required matches: 3
Structural checks: fail
Answer signal checks: fail (1/4)
Retrieval checks: pass (top 2/3, context 3/3)
Citation checks: pass (matched 1/1, precision 33%, recall 100%)
Evidence checks: scope=pass, kind=pass, slots=100%, sufficiency=pass, citations=pass
Source format checks: pass (.json)
Answer:
[json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
- 输入/前提：- 获取传感器数据：多通道采集土壤与环境参数，通过滤波算法消除噪声干扰，结合动态校准策略提升数据可靠性。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
- 判断或处理：在系统进入精确控制阶段时，PID算法根据实时偏差动态调整灌溉量。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.2 PID调节阶段设计 > content | c35]
- 执行动作：- 主程序循环：采用事件驱动架构，通过定时中断触发周期性任务，处理异步事件如用户操作或网络指令。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
- 结果/效果：系统在昼夜温差变化、突降雨水等复杂环境下，湿度波动幅度始终控制在±4%以内，较传统方式节水效率提升超40%。 [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.2 控制性能测试 > content | c41]

Retrieval debug:
原始问题: 详细一点
检索问题: 这个智能浇花系统如何实现精准灌溉；补充要求：详细一点。重点关注：PID、PWM、pid、pwm、经模糊
检索管线: refactored
问题类型: procedure
焦点词: 精准灌溉, 智能浇花系统
稀疏预筛候选: 0 条
进入语义计算: 44 条
是否使用稀疏预筛: 否（回退全量语义）
学习式重排: BGE-Reranker (global:ok (44/44))
主命中: 44 条
[1] score=9.970 (semantic=0.705, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.2 实时补偿策略 > content | c37]
    content: 系统每5分钟动态评估一次环境状态，结合昼夜节律智能调节：白天光照充足时采用防蒸腾补偿，在基础灌溉量上额外增加10%-20%；夜间启动节水模式，减少灌溉频率并降低单次供水量。当检测到持续阴雨天气时，自动进入防涝状态，暂停常规...
[2] score=9.623 (semantic=0.727, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.2 PID调节阶段设计 > content | c35]
    content: 采用自整定参数微调，积分项动态衰减防止饱和。在系统进入精确控制阶段时，PID算法根据实时偏差动态调整灌溉量。比例系数决定了对当前偏差的反应强度，当土壤湿度与目标值差距较大时，系统自动增强比例作用，快速缩小偏差；接近目标值时...
[3] score=9.405 (semantic=0.679, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c27]
    - 初始化阶段：系统启动时执行全面初始化：配置各类外设接口，设置通信协议与时钟源，加载预设参数与历史配置。完成传感器通断检测与执行机构自检，确保硬件状态正常，异常时通过显示模块提示错误信息。 - 主程序循环：采用事件驱动架构，通过定时中断触...
[4] score=9.199 (semantic=0.736, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.2 控制性能测试 > content | c41]
    content: 在模拟真实园艺场景的对比测试中，模糊PID控制系统展现出显著优势。当土壤湿度从干燥状态的30%快速调整至目标值的60%时，传统PID需8秒完成调节且会出现12%的过度浇水，而模糊PID仅用4.6秒即精准稳定在目标范围，全程...
[5] score=9.149 (semantic=0.693, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.3 环境补偿机制 > 3.3.1 温湿度耦合模型 > content | c36]
    content: 系统内置智能环境感知模型，将温度与湿度的影响关联分析。当气温升高时，土壤水分蒸发加速，模型会自动增加基础灌溉量；反之在低温潮湿环境下，则减少供水量防止积水。例如，温度超过30℃且空气干燥时，系统判定为高温蒸发模式，灌溉量提...
[6] score=8.832 (semantic=0.709, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 4 系统方案的创新 > innovations | c38]
    - 土壤温湿度的空间维度融合设计。在土壤不同层面上放入3个传感器，并对采集的温湿度数据进行加权计算，其中表层占20%、中层占60%、底层占20%。 - 环境温湿度的数据融合策略。环境传感器数据用于修正土壤表层蒸发误差，当空气温度>30℃且湿...
[7] score=7.919 (semantic=0.737, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > abstract | c10]
    abstract: 针对传统灌溉方式水资源浪费严重、自动化程度低的问题，设计了一种基于STM32单片机的智能浇花系统。系统采用模块化设计，包含数据采集、决策控制、执行机构和交互显示四大模块。通过对土壤湿度传感器、环境温湿度传感器实时采集的环...
[8] score=7.661 (semantic=0.732, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第2项 | c31]
    湿度偏差(E): 正小(PS):+5%~+15% 变化率(EC): 缓慢变干(SD):+1%~+5%/min 输出动作: 中速补水(60%占空比)
[9] score=7.522 (semantic=0.720, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第4项 | c33]
    湿度偏差(E): 负小(NS):-15%~-5% 变化率(EC): 缓慢变湿(SW):-5%~-1%/min 输出动作: 微量补水(20%占空比)
[10] score=7.288 (semantic=0.743, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 6 结论 > content | c44]
    content: 本智能浇花系统通过融合多传感器数据与智能控制算法，实现了精准灌溉与水资源高效管理。实际测试表明，系统可将土壤湿度误差稳定控制在±3%以内，较传统灌溉方式节水50%以上，尤其适用于需水量差异大的植物类型，如绿萝节水48%、多...
[11] score=7.267 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.2 电磁阀PWM控制策略 > content | c23]
    content: 首先对频率选择，设定PWM频率1kHz，避免可闻噪声，同时确保电磁阀机械响应无滞后。其次对占空比映射，设定线性区间：占空比10%~80%对应流量0.3~3L/min；设定非线性补偿：通过实验标定建立查找表，存储256组校准...
[12] score=7.256 (semantic=0.711, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第1项 | c30]
    湿度偏差(E): 正大(PB):＞+15% 变化率(EC): 快速变干(FD):＞+5%/min 输出动作: 全开阀门(100%占空比)
[13] score=7.220 (semantic=0.714, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第3项 | c32]
    湿度偏差(E): 零(ZO):-5%~+5% 变化率(EC): 稳定(ST):-1%~+1%/min 输出动作: 维持当前状态
[14] score=6.951 (semantic=0.715, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.4 优化方案 > content | c43]
    content: 结合当下较为热点的AI模型，进行远程控制与智能策略的深度融合。当云端AI分析历史数据发现灌溉规律后，可自动下发优化参数，系统动态调整模糊PID规则库以适应新策略。
[15] score=6.865 (semantic=0.686, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.1 传感器标定实验 > content | c40]
    content: 在实验室标准湿度箱中，对传感器进行了三点校准优化。首先将探头置于完全干燥环境确定基准零点，随后在60%标准湿度箱中调整线性度，最后通过水饱和状态验证上限精度。校准后的传感器在沙土、黏土等不同基质中实测显示，湿度检测误差稳定...
[16] score=6.865 (semantic=0.685, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.2 远程状态显示及控制模块 > content | c25]
    content: 通过USART1接口连接ESP-01S WiFi模块，构建双向通信链路，实现数据远程传输与设备控制功能。状态信息数据上传，采用轻量级MQTT协议，将土壤温湿度、环境参数、电磁阀状态及系统健康信息封装为JSON格式，每5秒周...
[17] score=6.860 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.2 感知层 > 1.2.2 环境温湿度传感器 > content | c21]
    content: 为精准评估蒸发量对灌溉需求的影响，选用数字式温湿度传感器，其特点包括：①集成化设计：将温湿度传感单元与信号处理电路集成于3×3mm芯片；②低功耗特性：工作电流仅0.2mA，适合长期连续监测；③快速响应：湿度检测响应时间<8...
[18] score=6.858 (semantic=0.710, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > controlRulesExample > 第5项 | c34]
    湿度偏差(E): 负大(NB):＜-15% 变化率(EC): 快速变湿(FW):＜-5%/min 输出动作: 紧急关阀
[19] score=6.799 (semantic=0.704, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 3 模糊PID复合控制器的简要设计 > 3.1 模糊控制阶段：输入变量定义 > content | c29]
    content: 湿度偏差是指目标湿度与实际湿度差值，划分为5个模糊集；偏差变化率是指单位时间内湿度变化量，划分为5个模糊集。基于农业专家经验与历史数据训练，建立25条控制规则。
[20] score=6.590 (semantic=0.713, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.2 高精度数据采集与多模态通信 > content | c19]
    content: 主控芯片配备的12位逐次逼近型ADC模块，支持10通道扫描模式与硬件过采样功能，可将有效分辨率提升至16位。针对土壤湿度传感器的0-3V模拟信号，ADC以100kSPS速率采样，结合内置温度传感器实时补偿环境漂移，使湿度检...
[21] score=6.580 (semantic=0.684, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 > 5.3 节水效果评估 > content | c42]
    content: 在实际家庭环境测试中，智能浇花系统展现出显著节水优势。针对绿萝盆栽的30天对比实验显示，传统人工灌溉日均用水量达320毫升，而系统通过精准监测土壤湿度，仅在植物需水时启动灌溉，日均用水降至166毫升，节水效率达48%。在多...
[22] score=6.193 (semantic=0.582, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 5 系统仿真与测试验证 | c39]
    testEnvironment: 基于Keil μVision5集成开发环境对程序代码进行编译与固件调试，生成可执行HEX文件后通过ST-LINK/V2调试器完成单片机烧录。利用仿真器对各功能模块进行逐项验证，包括传感器数据采集精度测试、电...
[23] score=5.892 (semantic=0.673, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.2 感知层 > 1.2.1 土壤温湿度复合传感器 > content | c20]
    content: 针对传统电阻式传感器易腐蚀、精度衰减的问题，选用电容式土壤温湿度传感器，其优势在于：①非接触测量：采用高频电容检测技术，避免电极与土壤直接接触导致的电解腐蚀；②长期稳定性：陶瓷封装探针可耐受酸碱土壤环境；③温度补偿：内置N...
[24] score=5.799 (semantic=0.657, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.3 执行层 > 1.3.1 电磁阀驱动模块 > content | c22]
    content: 针对家庭园艺场景的小流量、低功耗需求，选用常开型脉冲式电磁阀，其核心优势包括：①低压驱动：12V直流供电，适配系统电源设计；②精准控流：流量范围0.33L/min满足盆栽植物需水量；③快速响应：开启时间≤15ms，关闭时间...
[25] score=5.797 (semantic=0.754, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 0 引言 > content | c16]
    content: 随着全球水资源短缺问题日益严峻，农业用水效率提升成为研究热点。据统计，传统灌溉方式的水分利用率不足50%而智能灌溉系统可提升至85%以上。家庭园艺场景中，现有浇花装置普遍存在以下问题：定时灌溉忽视环境变化、缺乏闭环控制、无...
[26] score=5.708 (semantic=0.690, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > workflow | c28]
    - 灌溉终止管理：满足目标湿度、人工干预或检测到故障时，立即停止灌溉操作。执行机构关断过程包含能量释放与状态锁定，防止误触发或二次损害。 - 数据同步与存储：本地缓存关键运行数据，定期持久化存储防止丢失。无线模块将数据同步至远程平台，支持移...
[27] score=5.385 (semantic=0.657, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.1 决策层 > 1.1.1 主控芯片低功耗设计与核心架构 > content | c18]
    content: 本系统选用STM32L031G6作为核心控制器，该芯片基于ARM Cortex-M0+32位RISC处理器内核，在实现高效运算的同时显著降低能耗。其动态功耗仅为30μA/MHz，在停机模式下功耗可降至0.35μA。芯片集成...
[28] score=4.641 (semantic=0.641, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 2 软件系统的总体设计 > architecture | c26]
    architecture: 系统软件采用事件驱动型多线程架构，通过优先级调度与状态机管理实现高效资源利用。整体流程划分为数据采集、控制决策、执行输出三个核心线程，辅以用户交互后台任务
[29] score=4.489 (semantic=0.678, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > 1.4 交互层 > 1.4.1 本地状态显示模块 > content | c24]
    content: 通过SPI1接口驱动OLED显示屏，显示屏采用分层可视化设计，动态数据区与状态指示区协同呈现关键信息。动态数据区划分为左右两列，左列实时显示土壤参数，上方为湿度数据，下方为温度信息；右列展示环境参数，顶部显示空气湿度，底部...
[30] score=3.995 (semantic=0.602, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 | c9]
    affiliation: 广西民族师范学院，崇左532200 englishAffiliation: Guangxi Minzu Normal University, Chongzuo 532200, China
[31] score=3.658 (semantic=0.703, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > keywords | c14]
    - STM32 - 智能灌溉 - 模糊PID - 电磁阀
[32] score=3.277 (semantic=0.706, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c12]
    nd environmental temperature and humidity sensors, combined with fuzzy PID control algorithm to dynamically adjust the o...
[33] score=2.630 (semantic=0.650, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishKeywords | c15]
    - STM32 - intelligent irrigation - fuzzy PID - solenoid valve
[34] score=2.043 (semantic=0.624, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > authors > 彭霞 > name | c3]
    name: 彭霞
[35] score=2.009 (semantic=0.626, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c13]
    l moisture deviation within the range of ±2.8%, which improves the water-saving efficiency by 35% compared to traditiona...
[36] score=1.970 (semantic=0.618, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > authors > 黄灵敏 | c6]
    englishName: HUANG Ling-min
[37] score=1.970 (semantic=0.598, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > authors > 黄秀梅 > name | c7]
    name: 黄秀梅
[38] score=1.959 (semantic=0.608, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > authors > 黄灵敏 > name | c5]
    name: 黄灵敏
[39] score=1.930 (semantic=0.605, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > authors > 黄秀梅 | c8]
    englishName: HUANG Xiu-mei
[40] score=1.847 (semantic=0.664, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishTitle | c2]
    englishTitle: Design of Intelligent Watering System Based on STM32 Microcontroller
[41] score=1.744 (semantic=0.688, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > englishAbstract | c11]
    englishAbstract: In response to the serious waste of water resources and low automation level in traditional irrigation ...
[42] score=1.725 (semantic=0.629, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > authors > 彭霞 | c4]
    englishName: PENG Xia
[43] score=1.631 (semantic=0.674, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > title | c1]
    title: 基于STM32单片机的智能浇花系统设计
[44] score=1.097 (semantic=0.682, bm25=0.000, keyword=0.000, title=0.000, jsonStructure=0.000, coverage=0.000, neighbor=0.000, jsonBranch=0.000, docTarget=0.000, directHit=False) | [json/基于STM32单片机的智能浇花系统设计_彭霞-2026-04-30_01-08-14.json | 基于STM32单片机的智能浇花系统设计 > content > 1 系统总体设计方案 > overview | c17]
    overview: 系统采用“感知、决策、执行、交互”四层架构
上下文窗口: 12 条

=== Summary ===
Case pass count: 0/3
Keyword coverage: 7/16 (43.8%)
Top chunk signal coverage: 2/9 (22.2%)
Context signal coverage: 9/9 (100.0%)
Average citation precision: 27.8%
Average citation recall: 100.0%
Scope accuracy: 3/3 (100.0%)
Evidence kind accuracy: 3/3 (100.0%)
Average slot coverage: 100.0%
Sufficiency accuracy: 3/3 (100.0%)
Citation from EvidenceSet: 3/3 (100.0%)
Source format accuracy: 3/3 (100.0%)
JSON report: /home/tby/桌面/WeaveDoc/test/.eval/20260509-185812-weavedoc-rag-debug.json
Markdown report: /home/tby/桌面/WeaveDoc/test/.eval/20260509-185812-weavedoc-rag-debug.md