# WeaveDoc — UI 组件定义（Component Specification）

---

# 组件分类索引

| 分类 | 组件 | 复用页面 |
|------|------|---------|
| **容器与布局** | ModalHost, SplitPane, TabBar, PanelContainer | P1, P2-P4, P5, P6, P7, P9 |
| **导航与命令** | Toolbar, StatusBar | P1 |
| **列表与数据展示** | FilterableList, Timeline, ChatMessageList | P2, P3, P4, P5, P6 |
| **表单与配置** | PathPicker, ToggleField, NumericField, ComboField, RadioListField | P5, P6, P7 |
| **AI 与系统状态** | ModelStatusIndicator, MemoryIndicator, StreamingTextBlock, CitationChip, FloatingCodeBlock | P1, P2, P6, P8 |
| **反馈与确认** | ProgressStepper, ConfirmDialog, Toast, EmptyState | P1-P9, 全局 |
| **内容编辑与渲染** | MarkdownEditor, PDFViewer, DiffViewer | P1, P4 |

---

## 组件：ModalHost

### 组件职责
为所有模态窗口提供统一的外壳容器——包括半透明遮罩层、标题栏、关闭按钮、键盘导航（Esc 关闭）、以及打开/关闭的动画过渡。所有阻塞式对话框（导出、设置、向导、帮助）均渲染在此容器内。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `isOpen` | boolean | 模态是否可见 |
| `title` | string | 标题栏文案 |
| `width` | number (px) | 模态窗口宽度，默认 540 |
| `height` | number (px) | 模态窗口高度，默认 560 |
| `closeOnOverlayClick` | boolean | 点击遮罩是否关闭，默认 false（防止误操作） |
| `closeOnEsc` | boolean | Esc 是否关闭，默认 true |
| `children` | object / Control | 模态内容插槽 |
| `footer` | object / Control | 底部操作栏插槽（可选） |
| `onClose` | callback | 关闭前回调，可在此拦截未保存修改 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击 [×] 关闭按钮 | 触发 onClose 回调 |
| 按 Esc | 触发 onClose 回调（closeOnEsc=true 时） |
| 点击遮罩 | 无响应（closeOnOverlayClick=false） |

### 状态
| 状态 | 表现 |
|------|------|
| `closed` | 模态不可见，IsVisible = false |
| `opening` | 模态以 fade-in + scale 动画进入（~200ms） |
| `open` | 模态完全可见，聚焦到第一个可聚焦元素 |
| `closing` | 模态以 fade-out 动画退出（~150ms） |
| `dirty-close` | 用户尝试关闭但有未保存修改——弹出子确认对话框"是否保存更改？" |

### AI 状态
无。ModalHost 为纯容器组件，不承载 AI 功能。

### 子组件
- `Overlay` — 半透明遮罩层 (rgba(0,0,0,0.4))
- `TitleBar` — 标题栏 (拖拽区域 + 标题文案 + [×] 按钮)
- `ContentSlot` — 内容插槽
- `FooterSlot` — 底部操作栏插槽

### 可复用场景
- PAGE 5: 导出对话框
- PAGE 6: 设置页面
- PAGE 7: 首次配置向导
- PAGE 9: 帮助/关于
- 未来任何需要模态阻塞式交互的场景

### 权限控制
无。所有用户均可打开模态。关闭时的"未保存拦截"基于组件内部 dirty 状态，不涉及权限。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `modal_open` | 模态完全打开 | `modal_id`, `source` (入口来源) |
| `modal_close` | 模态关闭 | `modal_id`, `close_reason` (button/esc/programmatic), `duration_ms` |
| `modal_dirty_close_blocked` | 拦截未保存关闭 | `modal_id` |

---

## 组件：SplitPane

### 组件职责
提供可拖拽调整比例的水平双栏布局容器。左侧固定渲染 PDF 阅读器，右侧渲染 Markdown 编辑器。中间分隔线可拖拽（范围 25%–50%）。当右侧上下文面板展开时，Markdown 编辑器区域自动收窄。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `leftChild` | object / Control | 左侧面板内容（PDF 阅读器） |
| `rightChild` | object / Control | 右侧面板内容（Markdown 编辑器） |
| `defaultRatio` | number (0–1) | 默认左侧占比，默认 0.35 |
| `minLeftWidth` | number (px) | 左侧最小宽度，默认 300 |
| `minRightWidth` | number (px) | 右侧最小宽度，默认 400 |
| `rightPanelOpen` | boolean | 上下文面板是否展开（挤压右侧） |
| `rightPanelWidth` | number (px) | 上下文面板宽度，默认 280 |
| `onRatioChange` | callback | 拖拽分隔线后的回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 拖拽分隔线 | 鼠标按下 → 水平拖动 → 松手，实时调整左右比例 |
| 双击分隔线 | 重置为默认比例 (35%:65%) |

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | 分隔线静止，显示默认鼠标样式 |
| `hover` | 鼠标悬停分隔线，鼠标变为 ↔ 调整大小样式，分隔线高亮 |
| `dragging` | 正在拖拽中，分隔线跟随鼠标移动，左右面板实时调整宽度 |
| `snapped` | 拖拽到边缘（<25% 或 >50%）时自动吸附到边界值 |

### AI 状态
无。SplitPane 为纯布局容器。

### 子组件
- `SplitDivider` — 可拖拽分隔线（2px 宽，hover 高亮）
- `LeftSlot` — 左侧内容插槽
- `RightSlot` — 右侧内容插槽

### 可复用场景
- PAGE 1: 工作台（PDF 阅读器 + Markdown 编辑器）
- 未来任何需要可拖拽分屏对比的场景（如 diff 对比视图）

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `split_drag_start` | 开始拖拽分隔线 | `current_ratio` |
| `split_drag_end` | 拖拽结束 | `from_ratio`, `to_ratio` |

---

## 组件：TabBar

### 组件职责
水平标签页切换控件。用于上下文面板容器（AI 问答 / 文献 / 快照）和设置页面（5 个标签页），提供视觉上清晰的当前激活标签指示和切换动画。支持与右侧折叠按钮共存。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `tabs` | TabDef[] | 标签定义数组：{ id, label, icon?, badge? } |
| `activeTabId` | string | 当前激活的标签 ID |
| `onTabChange` | callback | 切换标签时的回调 |
| `collapsible` | boolean | 是否在右侧显示折叠按钮，默认 false |
| `collapsed` | boolean | 面板是否已折叠 |
| `onCollapseToggle` | callback | 折叠/展开回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击标签 | 切换到对应标签内容区，标签高亮 |
| 点击折叠按钮 | 折叠/展开整个面板区域 |
| 键盘 ← → | 左右切换标签（focus 移动，Enter 确认） |

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | 当前激活标签高亮（底部 2px 强调色下划线），其余标签灰色 |
| `hover` | 悬停标签轻微变色 |
| `transitioning` | 标签内容区以 crossfade 或 slide 动画切换（~150ms） |
| `collapsed` | 所在 PanelContainer 宽度为 0，仅保留工作区边缘折叠指示条；TabItem 不在面板内保留图标列 |

### AI 状态
无。

### 子组件
- `TabItem × N` — 单个标签（icon + label + badge + 激活下划线）
- `CollapseButton` — 折叠/展开图标按钮（collapsible=true 时渲染）

### 可复用场景
- PAGE 1: 工作台上下文面板容器（3 标签）
- PAGE 6: 设置页面（5 标签）
- 未来任何需要多面板切换的容器

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `tab_switch` | 用户切换标签 | `from_tab`, `to_tab`, `source` (click/keyboard) |
| `panel_collapse` | 折叠/展开面板 | `action` (collapse/expand) |

---

## 组件：PanelContainer

### 组件职责
右侧可折叠上下文面板的外层容器。统一管理三个子面板的 Tab 切换、面板展开/折叠的宽度动画、以及折叠后相邻工作区（Markdown 编辑器）的宽度自动扩展。折叠时面板宽度从 280px → 0px，相邻区域自动吸收空间。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `panels` | PanelDef[] | 面板定义数组：{ id, label, icon, content } |
| `activePanelId` | string | 当前激活的面板 ID |
| `collapsed` | boolean | 是否折叠 |
| `width` | number (px) | 展开宽度，默认 280 |
| `onPanelChange` | callback | 切换面板回调 |
| `onCollapseToggle` | callback | 折叠/展开回调 |
| `children` | — | 由 TabBar + 内容渲染区组成 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 见 TabBar 操作 | 标签切换、折叠展开 |
| 快捷键 | Ctrl+Shift+A (AI), Ctrl+Shift+L (文献), Ctrl+Shift+T (快照) |

### 状态
| 状态 | 表现 |
|------|------|
| `expanded` | 面板宽度 280px，内容完全可见 |
| `collapsed` | 面板宽度 0px，仅显示边缘折叠指示条 |
| `expanding` | 宽度从 0 → 280px 的滑动动画 (~200ms) |
| `collapsing` | 宽度从 280 → 0px 的滑动动画 (~150ms) |

### AI 状态
无。PanelContainer 为纯布局容器。内部面板各自处理 AI 状态。

### 子组件
- `TabBar` — 标签栏（复用）
- `PanelContentSlot` — 按 activePanelId 渲染对应面板内容

### 可复用场景
- PAGE 1: 工作台上下文面板容器
- 未来任何需要侧边可折叠面板的场景

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `panel_switch` | 切换面板 | `from_panel`, `to_panel` |
| `panel_toggle` | 折叠/展开 | `action`, `current_panel` |

---

## 组件：Toolbar

### 组件职责
工作台顶部的高频操作按钮组。最多容纳 7 个图标按钮，每个按钮有 tooltip 和关联快捷键。视觉权重最高的按钮（导出）使用主色调强调。遵循"3 次点击内完成核心任务"原则。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `items` | ToolbarItem[] | 按钮定义数组：{ id, icon, label, shortcut, primary?, onClick, disabled? } |
| `maxItems` | number | 最大可见按钮数，默认 7 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击按钮 | 触发对应 onClick 回调 |
| 悬停按钮 | 显示 tooltip（含快捷键提示） |

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | 所有可用按钮正常显示 |
| `item-disabled` | 某按钮灰色不可点击（如无文档打开时"保存"禁用） |
| `item-active` | Toggle 类型按钮激活态（如 AI 面板开关开启时高亮） |

### AI 状态
无。Toolbar 为纯命令控件。

### 子组件
- `ToolbarButton × N` — 每个图标按钮（含 tooltip + 快捷键标签）

### 可复用场景
- PAGE 1: 工作台工具条
- 未来子页面可能需要独立的小型工具条

### 权限控制
无。按钮的 disabled 状态由业务逻辑控制（如无文档时"导出"禁用）。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `toolbar_click` | 点击按钮 | `button_id`, `shortcut_used` (boolean) |

---

## 组件：StatusBar

### 组件职责
工作台底部的系统级状态信息条，默认可见，可通过 View 菜单隐藏。左侧显示文档状态（光标位置、字数、引用计数）和双屏同步滚动状态，右侧显示系统状态（AI 模型、内存占用）。状态值由系统更新；Refs、Sync、模型状态等部分标签可点击触发关联操作或跳转。高度固定 24px。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `leftItems` | StatusItem[] | 左侧状态项：{ id, label, value, onClick? } |
| `rightItems` | StatusItem[] | 右侧状态项 |
| `memoryUsage` | { used: GB, total: GB, percent: number } | 内存使用数据 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击"Refs: N" | 打开文献列表面板 |
| 点击"Sync On/Off" | 切换双屏同步滚动状态 |
| 点击模型状态 | 跳转 Settings → Model Management |
| 悬停内存指示器 | 显示详细 tooltip（各子系统内存分配） |
| 悬停模型状态 | 显示 tooltip（模型名称、加载时间、上次推理时间） |

### 状态
| 状态 | 表现 |
|------|------|
| `normal` | 使用主题映射的 StatusBar 背景与 Foreground/MutedForeground 文本，左右分组对齐 |
| `warning` | 某个指标达到阈值（如 Zotero 断连），对应项显示 `StatusIcon(warning)` |
| `critical` | 内存 > 80%，内存指示器变红 |
| `AI-unloaded` | 模型状态灯为红色或灰色 |
| `sync-off` | 同步滚动标签显示 "Sync Off"，可点击恢复为 "Sync On" |
| `hidden` | 用户通过 View 菜单隐藏状态栏，工作区高度自动吸收 24px |

### AI 状态
状态栏右侧的 AI 模型状态指示灯直接反映本地 LLM 状态：
| AI 子状态 | 表现 |
|-----------|------|
| `ai-idle` | 绿灯 + 模型名称 + "· 3.2 t/s" |
| `ai-loading` | 黄灯旋转 + "Loading Phi-4 7B... 67%" |
| `ai-unloaded` | 红灯 + "Model not loaded" |
| `ai-inference` | 绿灯脉冲 + "Generating... · 3.2 t/s" |
| `ai-unconfigured` | 灰灯 + "No model configured" |

### 子组件
- `StatusLabel × N` — 单项状态标签（icon + text，可选 onClick）
- `ModelStatusIndicator (compact)` — AI 模型状态（见 ModelStatusIndicator）
- `MemoryIndicator (compact)` — 内存占用进度条微型组件（见 MemoryIndicator）

### 可复用场景
- PAGE 1: 工作台状态栏
- 未来子窗口可能需要简版状态栏

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `statusbar_click` | 点击可交互状态项 | `item_id` |
| `memory_warning` | 内存超过 80% | `used_gb`, `total_gb`, `percent` |

---

## 组件：MemoryIndicator

### 组件职责
微型内存占用可视化指示器。在 StatusBar 中常驻显示，在 Settings 模型管理标签页中以更大尺寸展示。实时反映系统内存使用状况，超过配置的 critical 阈值（默认 80%）时触发视觉告警和 tooltip 建议操作。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `usedGB` | number | 已用内存 (GB) |
| `totalGB` | number | 总物理内存 (GB) |
| `threshold` | number (0–1) | critical 告警阈值，默认 0.80 |
| `variant` | 'compact' \| 'detailed' | 紧凑型（StatusBar）/ 详细型（Settings） |
| `onThresholdExceeded` | callback | 超过阈值时的回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 悬停 (compact) | tooltip 显示详细内存分配 |
| 查看 (detailed) | 显示进度条 + 数值 + 建议操作 |

### 状态
| 状态 | 表现 |
|------|------|
| `normal` | 绿色进度条，文字 "RAM 62% (9.9 / 16 GB)" |
| `warning` | 橙色进度条；默认阈值下为 70-80%，自定义阈值时为 `threshold - 10pp` 至 `threshold` |
| `critical` | 红色进度条；默认阈值下为 >80%，自定义阈值时为 >`threshold`，tooltip 显示"内存不足。建议: 释放 PDF 缓存 / 切换更小的模型 / 关闭其他应用" |

### AI 状态
此组件直接反映 AI 模型的内存占用：
| AI 子状态 | 表现 |
|-----------|------|
| `model-loaded` | 内存占比明显上升（含模型权重），进度条反映包含模型的总占用 |
| `model-unloaded` | 内存占比下降 |
| `model-loading` | 内存占比持续上升中 |
| `model-warning` | 加载模型后内存超过阈值，触发 critical 状态 |

### 子组件
- `ProgressBar` (微型/标准) — 内存占比可视化
- `Label` — 数值文字

### 可复用场景
- PAGE 1: StatusBar 右侧
- PAGE 6: Settings → Model Management 标签页
- 未来任何需要监控系统资源的场景

### 权限控制
无。系统级信息，所有用户可见。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `memory_threshold_crossed` | 内存超过/低于阈值 | `direction` (above/below), `percent`, `top_consumers` |

---

## 组件：FilterableList

### 组件职责
带搜索过滤、排序切换、状态筛选的紧凑型列表组件。用于文献列表面板（P3）、导出模板选择（P5）和设置页模板/模型列表（P6）。列表每行可展开显示详细信息，支持单选/多选模式，每行有状态图标和操作按钮。搜索输入实时过滤，无需按 Enter。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `items` | T[] | 列表数据 |
| `renderRow` | (item: T) → RowContent | 行渲染函数 |
| `renderExpanded` | (item: T) → ExpandedContent | 展开区渲染函数 |
| `searchEnabled` | boolean | 是否显示搜索框，默认 true；导出模板选择等紧凑单选场景可设为 false |
| `searchPlaceholder` | string | 搜索框 placeholder |
| `filterOptions` | FilterDef[] | 筛选器定义 |
| `sortOptions` | SortDef[] | 排序方式定义 |
| `selectionMode` | 'none' \| 'single' \| 'multi' | 选择模式 |
| `emptyStateText` | string | 空状态文案 |
| `onItemClick` | callback | 行点击回调 |
| `summaryText` | string | 统计摘要文案（如"共 34 条 · 已匹配: 28"） |

### 用户操作
| 操作 | 说明 |
|------|------|
| 搜索输入 | 实时过滤列表，高亮匹配文字 |
| 点击筛选按钮 | 展开筛选下拉菜单 |
| 点击排序按钮 | 切换当前场景定义的排序方式（如作者/年份/引用键、名称/状态/大小） |
| 点击行 | 选中行，展开详情（如有） |
| 双击行 / 拖拽 | 触发当前场景定义的默认动作；P3 文献列表可双击/拖拽插入引用，P5/P6 模板/模型列表不启用拖拽插入 |

### 状态
| 状态 | 表现 |
|------|------|
| `empty` | 显示空态占位 + 引导操作按钮 |
| `search-no-results` | "未找到匹配 '{query}' 的条目" + "清除搜索" 链接 |
| `normal` | 列表正常展示 |
| `loading` | 列表区域显示骨架屏 |
| `syncing` | 同步按钮旋转动画，列表保持当前数据 |

### AI 状态
无。但保留 AI 功能扩展接口：
| AI 扩展点 | 说明 |
|-----------|------|
| 智能排序 | 未来可增加"按与当前写作内容相关性"排序选项 |
| 相似度去重 | 基于标题/DOI 相似度标记疑似重复条目 |
| 引用推荐 | 在列表顶部展示"推荐引用"分区 |

### 子组件
- `SearchInput` — 带图标搜索框
- `FilterDropdown` — 筛选下拉菜单
- `SortToggle` — 排序切换图标按钮
- `ListItem × N` — 可展开行
- `StatusIcon` — 行状态图标（PathIcon + 语义色：success / warning / error）
- `ExpandableRowDetail` — 内联展开详情面板
- `EmptyState` — 空状态占位

### 可复用场景
- PAGE 3: 文献列表面板（核心使用）
- PAGE 5: 导出对话框模板选择区（selectionMode='single', 无搜索）
- PAGE 6: Settings → Template Library 标签页
- PAGE 6: Settings → Model Management 标签页（模型列表）

### 权限控制
无。所有用户可浏览和搜索。删除操作需确认对话框。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `list_search` | 输入搜索关键词 | `query_length`, `result_count` |
| `list_filter` | 切换筛选 | `filter_id`, `result_count` |
| `list_sort` | 切换排序 | `sort_by` |
| `list_item_expand` | 展开行详情 | `item_id` |
| `list_item_action` | 点击行操作按钮 | `item_id`, `action` (copy/delete/insert) |

---

## 组件：Timeline

### 组件职责
垂直时间轴可视化组件，用于展示按时间倒序排列的快照节点列表。每个节点包含时间戳、变更摘要（行数变化）以及节点状态（可用/损坏/当前/基线）；语义描述为未来 AI 扩展字段。时间轴由一条贯穿竖线连接所有节点，当前版本和基线版本有明确视觉区分。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `nodes` | TimelineNode[] | 节点数据：{ id, timestamp, summary, semanticLabel?, addedLines, removedLines, status } |
| `currentNodeId` | string | 当前版本对应的节点 ID |
| `selectedNodeId` | string \| null | 当前选中的节点 ID |
| `onNodeSelect` | callback | 选中节点回调 |
| `onNodeDoubleClick` | callback | 双击节点快捷恢复 |
| `emptyStateText` | string | 空时间轴文案 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击节点 | 选中节点，高亮显示，预览区加载该快照内容 |
| 双击节点 | 弹出恢复确认对话框（快捷操作） |
| 滚轮 | 滚动时间轴列表 |

### 状态
| 状态 | 表现 |
|------|------|
| `empty` | 时间轴竖线 + 空状态占位文案 |
| `normal` | 所有可用节点正常展示 |
| `node-selected` | 选中节点高亮（实心圆点 + 蓝色背景） |
| `node-corrupted` | 某节点灰色不可点击，tooltip "此快照已损坏，无法恢复" |
| `restoring` | 恢复操作进行中，对应节点显示进度指示与阶段文案 |

### AI 状态
| AI 扩展点 | 说明 |
|-----------|------|
| `semantic-label` | 当前 MVP 显示 "+N -M 行"；未来由本地 LLM 生成变更语义摘要，如"修改了第三章的方法论部分" |
| `risk-assessment` | 选中历史快照后，由 LLM 对比当前版本与目标版本，提示可能丢失的关键内容（显示在预览区上方） |

### 子组件
- `TimelineLine` — 贯穿竖线（视觉连接线）
- `TimelineNode × N` — 单个时间节点（圆点 + 时间戳 + 变更摘要；未来可追加语义标签）
- `CurrentVersionMarker` — 当前版本特殊标记（实心圆点 + 蓝色高亮）
- `BaselineMarker` — 基线快照特殊标记（虚线圆点 + 灰色文字）
- `CorruptedNodeMarker` — 损坏快照标记（灰色 + 删除线）
- `EmptyState` — 空状态占位

### 可复用场景
- PAGE 4: 快照时间轴面板（核心使用）
- 未来任何需要版本历史可视化的场景

### 权限控制
无。浏览历史版本无需权限。恢复操作需二次确认。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `timeline_node_select` | 选中快照节点 | `node_id`, `node_age_minutes` |
| `timeline_restore_initiate` | 点击恢复按钮 | `node_id`, `current_version_changes` |
| `timeline_restore_complete` | 恢复成功 | `node_id`, `duration_ms` |

---

## 组件：ChatMessageList

### 组件职责
AI 问答面板的核心消息展示区。渲染用户与 AI 的对话历史气泡列表。支持：用户消息（右对齐，浅色背景）、AI 消息（左对齐，带左边框强调色）、流式文本逐 Token 打印、引用溯源链接渲染、自动滚动到底部、以及"回到底部"浮动按钮。消息间距紧凑（8px），最大化面板信息密度。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `messages` | Message[] | 消息数组：{ id, role, content, citations?, timestamp, status } |
| `streamingMessageId` | string \| null | 当前正在流式生成的消息 ID |
| `streamingTokens` | string | 当前流式已接收的 token 文本 |
| `onCitationClick` | callback | 点击引用溯源链接回调（跳转 PDF） |
| `onClear` | callback | 清空对话回调 |
| `emptyStateText` | string | 空对话引导文案 |
| `exampleQuestions` | string[] | 示例问题列表（空对话态展示） |

### 用户操作
| 操作 | 说明 |
|------|------|
| 查看消息 | 滚动浏览对话历史 |
| 点击引用溯源链接 | PDF 阅读器跳转到对应页码 |
| 点击"回到底部"按钮 | 自动滚动到最新消息 |
| 点击示例问题 | 自动填充到输入框并发送 |
| 点击"Clear" | 弹出确认 → 清空消息列表 |

### 状态
| 状态 | 表现 |
|------|------|
| `empty` | 中央显示欢迎引导 + 示例问题列表（可点击） |
| `normal` | 对话消息列表正常展示 |
| `streaming` | 最新 AI 消息气泡逐 Token 打印，光标闪烁，流式追加 |
| `streaming-paused` | 用户向上滚动查看历史，新消息继续到达，显示"↓ 回到底部"浮动按钮 |
| `error` | 错误消息以红色边框气泡展示（非正常 AI 回答气泡） |

### AI 状态
| AI 子状态 | 表现 |
|-----------|------|
| `ai-thinking` | AI 消息气泡出现，内容为空，显示闪烁光标，首字尚未到达 |
| `ai-streaming` | AI 消息气泡逐 Token 打印（打字机效果），引用溯源链接在流式完成后统一渲染 |
| `ai-completed` | AI 消息气泡完整渲染，引用溯源链接高亮可点击，底部显示完成时间 |
| `ai-stopped` | 用户点击停止生成，已生成 Token 保留，显示 "[生成已停止]" 标注 |
| `ai-error` | 红色边框气泡显示错误信息："推理失败: {错误详情}"，提供操作建议 |

### 子组件
- `ChatBubble (user)` — 用户消息气泡（右对齐，浅色背景）
- `ChatBubble (ai)` — AI 消息气泡（左对齐，带左边框强调色，Markdown 渲染）
- `StreamingText` — 流式文本渲染（逐 Token 动画）
- `CitationChip × N` — 引用溯源链接芯片
- `ScrollToBottomButton` — 回到底部浮动按钮
- `ClearConversationButton` — 清空对话按钮
- `EmptyState` — 欢迎引导空状态（含示例问题）
- `ErrorMessage` — 错误消息渲染

### 可复用场景
- PAGE 2: AI 问答面板（核心使用）
- 未来任何需要对话式 AI 交互的场景

### 权限控制
无。所有用户可查看和发送消息。AI 模型未加载时输入框禁用。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `chat_message_send` | 用户发送消息 | `message_length`, `conversation_turn` |
| `chat_stream_start` | 首 Token 到达 | `time_to_first_token_ms` |
| `chat_stream_complete` | 流式完成 | `total_tokens`, `total_time_ms`, `tokens_per_sec` |
| `chat_stream_stop` | 用户手动停止 | `tokens_generated`, `time_ms` |
| `chat_citation_click` | 点击引用溯源链接 | `citation_index`, `source_page` |
| `chat_clear` | 清空对话 | `messages_count` |

---

## 组件：ModelStatusIndicator

### 组件职责
本地 AI 模型的状态可视化指示器。在 AI 问答面板顶部（面板内）和 StatusBar 右侧（全局）双重出现，并在 Settings → Model Management 的模型列表行内复用 `row` 变体。用于向用户传递"模型当前是否可用"的明确信号，包含状态灯（颜色编码）、模型名称、推理速度（tokens/s）、内存占比。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `modelName` | string | 当前加载的模型名称 |
| `status` | 'unconfigured' \| 'unloaded' \| 'loading' \| 'idle' \| 'inference' | 模型状态 |
| `loadProgress` | number (0–100) | 加载进度百分比（loading 状态） |
| `tokensPerSecond` | number \| null | 推理速度（idle/inference） |
| `memoryPercent` | number | 当前模型/AI 进程相关内存占比（detailed 变体显示为 "RAM xx%"；全局总内存仍由 MemoryIndicator 展示） |
| `variant` | 'compact' \| 'detailed' \| 'row' | 紧凑型（StatusBar）/ 详细型（AI 面板顶部）/ 行内型（Settings 模型列表） |
| `onClick` | callback | 点击跳转 Settings → Model Management |

### 用户操作
| 操作 | 说明 |
|------|------|
| 悬停 | tooltip 显示详情 |
| 点击 | 跳转 Settings → Model Management |

### 状态
| 状态 | 表现 |
|------|------|
| `unconfigured` | 灰灯 + "No model configured" |
| `unloaded` | 红灯 + 模型名称 + "Not loaded" |
| `loading` | 黄灯旋转 + "Loading {model}... {progress}%" |
| `idle` | 绿灯 + 模型名称 + "{tokens_per_sec} t/s" |
| `inference` | 绿灯脉冲动画 + "Generating... · {tokens_per_sec} t/s" |

`row` 变体用于 Settings → Model Management 的 FilterableList 行内展示：只渲染状态灯、短状态标签（Loaded / Not loaded / Loading xx%）和可选性能文本，不改变 FilterableList 的行高与选择行为。

### AI 状态
此组件本身即为 AI 状态的可视化载体。
| AI 子状态 | 表现 |
|-----------|------|
| `ai-unconfigured` | 灰灯 + 点击引导配置 |
| `ai-unloaded` | 红灯 + 点击引导加载 |
| `ai-loading` | 黄灯旋转 + 进度百分比 |
| `ai-idle` | 绿灯静态 + 性能指标 |
| `ai-inference` | 绿灯脉冲 + 实时速度更新 |

### 子组件
- `StatusDot` — 颜色编码圆点指示灯
- `ModelNameLabel` — 模型名称
- `PerformanceLabel` — tokens/s + 内存占比
- `LoadingProgress` — 加载进度条（仅 loading 状态）

### 可复用场景
- PAGE 2: AI 问答面板顶部（detailed variant）
- PAGE 1: StatusBar 右侧（compact variant）
- PAGE 6: Settings → Model Management 标签页（row variant）

### 权限控制
无。所有用户可见。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `model_status_change` | 状态切换 | `from_status`, `to_status`, `model_name` |
| `model_indicator_click` | 点击跳转设置 | `current_status` |

---

## 组件：StreamingTextBlock + CitationChip

### 组件职责

**StreamingTextBlock**：渲染 AI 流式输出的文本，支持逐 Token 打字机动画、Markdown 渲染、光标闪烁效果。每个新 Token 追加时触发平滑滚动。

**CitationChip**：渲染 AI 回答中的引用溯源链接。显示为小型芯片（chip/tag）样式，文本标签统一为 `[1] p.3`；可选来源图标使用 `PathIcon`，不写入标签文本。点击后 PDF 阅读器跳转到对应页码。在 AI 回答流式完成后统一渲染。

### 输入数据

**StreamingTextBlock**:
| 参数 | 类型 | 说明 |
|------|------|------|
| `text` | string | 当前已接收的完整文本 |
| `isComplete` | boolean | 流式是否已完成 |
| `isError` | boolean | 是否为错误消息 |
| `markdownEnabled` | boolean | 是否渲染 Markdown，默认 true |

**CitationChip**:
| 参数 | 类型 | 说明 |
|------|------|------|
| `citationIndex` | number | 引用序号 [1], [2]... |
| `sourcePage` | number | 来源页码 |
| `sourceText` | string | 来源文本片段摘要（tooltip） |
| `documentId` | string | 目标 PDF 文档 ID |
| `onClick` | callback | 点击跳转回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 悬停引用芯片 | tooltip 显示来源文本片段 |
| 点击引用芯片 | PDF 阅读器跳转到对应页码，高亮段落 |

### 状态
| 状态 | 表现 |
|------|------|
| `streaming` | 光标闪烁，文本逐 Token 追加 |
| `complete` | 文本完整渲染，引用芯片出现 |
| `error` | 红色边框包围错误文本 |

### AI 状态
| AI 子状态 | 表现 |
|-----------|------|
| `ai-thinking` | 文本为空，光标闪烁（首 Token 到达前） |
| `ai-streaming` | 逐 Token 追加，每追加一个 Token 光标微闪 |
| `ai-complete` | 文本完整 + 引用芯片渲染 |

### 子组件
- `AnimatedToken` — 逐 Token 动画
- `MarkdownRenderer` — Markdown 渲染器
- `CursorBlink` — 闪烁光标
- `CitationChip × N` — 引用溯源芯片

### 可复用场景
- PAGE 2: AI 问答面板 AI 消息气泡
- 未来任何需要流式 AI 文本展示的场景

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `citation_click` | 点击引用芯片 | `citation_index`, `source_page`, `document_id` |

---

## 组件：FloatingCodeBlock

### 组件职责
上下文浮层中的代码展示和编辑组件。用于公式提取浮层（P8）中展示 OCR 识别出的 LaTeX 代码。等宽字体、深色背景、可选择文本、支持只读/编辑两种模式切换。包含点击复制和关闭按钮。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `code` | string | 代码内容（LaTeX） |
| `language` | string | 语言标识，用于语法高亮，默认 'latex' |
| `readOnly` | boolean | 是否只读，默认 true |
| `confidence` | number (0–1) \| null | 识别置信度（可选） |
| `onCopy` | callback | 复制回调 |
| `onRetry` | callback | 重新框选回调 |
| `onClose` | callback | 关闭浮层回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 选中文本 | 鼠标拖拽选中代码 |
| 复制 | 点击"复制 LaTeX" → 复制到剪贴板 → 按钮变为"已复制"并显示 Check 图标 → 浮层自动关闭 |
| 编辑 | 点击编辑按钮切换为可编辑模式，手动修正后复制 |
| 重新框选 | 点击"重新框选"关闭浮层 |
| 关闭 | 点击 [×] 或浮层外部 |

### 状态
| 状态 | 表现 |
|------|------|
| `loading` | 代码区域显示进度指示 + 阶段文案（如"正在识别公式..."）；超过 1s 时显示取消入口，不只显示无限 spinner |
| `result` | 代码完成展示，置信度标签显示，操作按钮可用 |
| `error` | 提示"未能识别到公式。请确保框选区域包含完整的数学公式。" |
| `model-not-loaded` | "OCR 模型未加载。正在加载..." + 进度指示 + 取消入口（由框选公式显式触发） |
| `editing` | 代码块边框变为编辑态（蓝色边框），光标闪烁，可修改文字 |
| `copied` | 按钮短暂变为"已复制"并显示 Check 图标，浮层 1s 后自动关闭 |

### AI 状态
| AI 子状态 | 表现 |
|-----------|------|
| `ai-ocr-loading` | 模型加载中，显示进度 |
| `ai-ocr-processing` | 正在识别，显示 "正在识别公式..." |
| `ai-ocr-complete` | 识别完成，展示结果 + 置信度 |
| `ai-ocr-failed` | 识别失败，提示重新框选 |

### 子组件
- `CodeEditor` — 等宽字体代码展示/编辑区
- `ConfidenceBadge` — 置信度标签
- `CopyButton` — 复制按钮（含"已复制"反馈动画）
- `RetryButton` — 重新框选按钮
- `CloseButton` — 关闭按钮
- `LoadingProgressIndicator` — 加载/识别中的进度指示与阶段文案

### 可复用场景
- PAGE 8: 公式提取浮动工具栏（核心使用）
- 未来任何需要 AI 代码输出展示的场景（如代码生成、翻译结果）

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `ocr_start` | 开始识别 | `selection_size_px` |
| `ocr_complete` | 识别完成 | `confidence`, `duration_ms`, `code_length` |
| `ocr_fail` | 识别失败 | `error_reason` |
| `ocr_copy` | 复制 LaTeX | `code_length` |
| `ocr_edit` | 手动编辑代码 | `edits_count` |

---

## 组件：ProgressStepper

### 组件职责
多步骤流程的可视化进度指示器。支持两种模式：(1) 水平步骤条（Setup Wizard P7），N 个步骤圆点 + 连接线 + 步骤标题；(2) 垂直阶段列表（Export Dialog P5），4 个管线阶段，每行有状态图标 + 阶段名称 + 实时进度条。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `steps` | StepDef[] | 步骤定义：{ id, label, status } |
| `currentStepId` | string | 当前激活步骤 ID |
| `orientation` | 'horizontal' \| 'vertical' | 布局方向 |
| `variant` | 'wizard' \| 'pipeline' | 向导模式 / 管线模式 |
| `overallProgress` | number (0–100) | 总体进度百分比 (pipeline mode) |

### 用户操作
| 操作 | 说明 |
|------|------|
| 无直接操作 | 纯信息展示，不可交互 |
| (wizard variant) | 步骤切换通过上一步/下一步按钮完成 |

### 状态
| 步骤子状态 | 表现 (wizard) | 表现 (pipeline) |
|-----------|-------------|-----------------|
| `pending` | 空心圆点 + 灰色文字 | `StatusIcon(pending)` + 灰色文字 |
| `active` | 实心圆点 + 蓝色高亮 | `StatusIcon(active, spinning)` + 蓝色文字 |
| `success` | `StatusIcon(success)` + 绿色 | `StatusIcon(success)` + 绿色文字 |
| `error` | `StatusIcon(error)` + 红色 | `StatusIcon(error)` + 红色文字 + 错误详情 |

### AI 状态
无直接 AI 状态。但在 Setup Wizard 的 Step 2（模型配置）中，步骤内容区包含 AI 模型下载进度。

### 子组件
- `StepDot × N` — 步骤圆点（空心/实心/状态图标，使用 PathIcon / StatusDot，不使用 emoji glyph）
- `StepLabel × N` — 步骤标题文字
- `ConnectorLine × N-1` — 圆点之间连接线
- `ProgressBar` — 总体进度条 (pipeline mode)

### 可复用场景
- PAGE 5: 导出对话框进度区（vertical pipeline mode）
- PAGE 7: 首次配置向导（horizontal wizard mode）
- 未来任何多步骤流程

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `step_transition` | 步骤切换 | `from_step`, `to_step`, `variant` |
| `step_complete` | 某步骤完成 | `step_id`, `duration_ms` |
| `step_fail` | 某步骤失败 | `step_id`, `error_code` |

---

## 组件：ConfirmDialog

### 组件职责
破坏性或覆盖性操作（恢复快照、删除模型、清除快照、重置配置、清空对话）前的二次确认弹窗。由全局 `ConfirmDialogHost` 承载：当来源是普通工作区/上下文面板时覆盖在工作区之上；当来源已在 ModalHost 内时作为“模态中的模态”叠加在当前模态之上。对话框必须明确列出操作后果、保护措施和风险提示，避免用户误操作。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `title` | string | 确认标题 |
| `message` | string | 操作后果描述 |
| `warningText` | string \| null | 额外警告文案（红色显示） |
| `confirmLabel` | string | 确认按钮文案，默认"确定" |
| `cancelLabel` | string | 取消按钮文案，默认"取消" |
| `variant` | 'destructive' \| 'warning' | destructive = 红色确认按钮 / warning = 橙色确认按钮 |
| `onConfirm` | callback | 确认回调 |
| `onCancel` | callback | 取消回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击"确定" / "确定恢复" / "确认删除" | 触发 onConfirm |
| 点击"取消" | 触发 onCancel，关闭对话框 |
| 按 Esc | 等同于"取消" |

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | 确认对话框正常展示 |
| `confirming` | 确认按钮 loading（如恢复操作执行中） |

### AI 状态
无。

### 子组件
- `MessageText` — 后果描述文字
- `WarningText` — 红色/橙色警告文案
- `ConfirmButton` — 确认按钮（destructive 变体红色）
- `CancelButton` — 取消按钮

### 可复用场景
- PAGE 4: 快照恢复操作确认
- PAGE 6: 删除模型 (Settings Tab B)
- PAGE 6: 删除模板 (Settings Tab D)
- PAGE 6: 重置文献索引 (Settings Tab C)
- PAGE 6: 清除全部快照 (Settings Tab E)
- PAGE 6: 恢复默认配置 (Settings)
- PAGE 2: 清空对话历史 (Chat Panel)
- 任何破坏性操作

### 权限控制
无。所有用户可见确认对话框。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `confirm_dialog_show` | 弹出确认 | `dialog_id`, `variant` |
| `confirm_dialog_confirm` | 确认操作 | `dialog_id` |
| `confirm_dialog_cancel` | 取消操作 | `dialog_id` |

---

## 组件：Toast

### 组件职责
非阻塞式轻量反馈通知。用于操作完成后的短暂提示（如"已复制到剪贴板"、"同步完成"、"导出成功"）。统一从底部滑入，2-3 秒后自动消失。不影响用户当前操作。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `message` | string | 提示文案 |
| `type` | 'success' \| 'info' \| 'warning' \| 'error' | 消息类型 |
| `duration` | number (ms) | 自动消失时间，默认 3000 |
| `action` | { label: string, onClick: callback } \| null | 可选的操作按钮（如"撤销"） |
| `onDismiss` | callback | 消失回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 无需操作 | 自动消失 |
| 点击操作按钮 | 触发快捷操作（如"打开文件夹"） |
| 点击 [×] | 提前关闭 |

### 状态
| 状态 | 表现 |
|------|------|
| `entering` | 从底部滑入动画 (~200ms) |
| `visible` | 完全可见，倒计时 |
| `exiting` | 淡出动画 (~200ms) |
| `hover` | 鼠标悬停时暂停倒计时 |

### AI 状态
无。但 AI 相关操作的结果通过 Toast 反馈（如"模型加载完成"、"索引构建完成"）。

### 可复用场景
- PAGE 3: 复制引用键成功 → "已复制: he2024llm"
- PAGE 3: Zotero 同步完成 → "同步完成 · 34 条文献"
- PAGE 5: 导出成功默认在导出对话框进度区内展示；仅当对话框关闭后仍需保留全局反馈时使用 Toast + "打开文件夹"
- PAGE 8: LaTeX 复制成功默认使用浮层内按钮反馈并自动关闭；仅当浮层已关闭但仍需全局可见反馈时使用 Toast
- PAGE 9: 审计日志导出成功
- 全局：内存告警、模型加载完成

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `toast_show` | Toast 出现 | `type`, `message_id` |
| `toast_action` | 点击行动按钮 | `message_id`, `action_label` |

---

## 组件：EmptyState

### 组件职责
空状态的集中展示组件。当列表、面板、对话框内容为空时，展示引导性文案和操作入口。用于降低用户首次使用时的困惑——明确告知当前状态是什么、为什么、以及下一步可以做什么。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `icon` | Icon \| null | 大号空状态图标 |
| `title` | string | 主文案（如"暂无文献条目"） |
| `description` | string \| null | 辅助说明文案 |
| `actions` | ActionDef[] \| null | 操作按钮定义：{ label, onClick, primary? } |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击操作按钮 | 触发对应操作（如"导入 .bib 文件"） |

### 状态
| 状态 | 表现 |
|------|------|
| `empty` | 图标 + 文案 + 操作按钮居中展示 |
| `search-empty` | 搜索无结果变体，显示"未找到匹配" + "清除搜索"链接 |

### AI 状态
| AI 子状态 | 表现 |
|-----------|------|
| `ai-model-unconfigured` | "请先在 Settings → Model Management 中下载并加载模型" + 快捷跳转按钮 |

### 子组件
- `Icon` (large, muted)
- `TitleLabel`
- `DescriptionLabel`
- `ActionButton × N`

### 可复用场景
- PAGE 3: 文献列表面板空态
- PAGE 4: 快照时间轴面板空态
- PAGE 2: AI 问答面板空对话态（含示例问题）
- PAGE 1: 工作区 PDF/MD 未加载态
- PAGE 5: 导出对话框无可用模板态
- 任何需要空状态引导的场景

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `empty_state_action` | 点击空状态操作按钮 | `empty_state_type`, `action_label` |

---

## 组件：PathPicker

### 组件职责
文件/目录路径选择器。由文本输入框 + Browse 按钮组成。Browse 按钮打开系统原生文件/文件夹选择对话框，选中后路径回填到输入框。用于导出路径选择、Zotero .bib 路径配置、模型存储路径、工作区路径等所有路径选择场景。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | string | 当前路径值 |
| `placeholder` | string | 占位符 |
| `mode` | 'file' \| 'directory' \| 'save' | 文件选择 / 目录选择 / 保存文件；`save` 返回完整目标文件路径（含文件名和扩展名），不要再额外拆出独立文件名输入 |
| `filters` | FileFilter[] \| null | 文件类型过滤（如 '.bib', '.gguf', '.json'） |
| `onChange` | callback | 路径变更回调 |
| `validation` | (path: string) => ValidationResult | 路径校验函数 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 点击 Browse | 打开系统原生选择器 → 选择 → 路径回填 |
| 直接输入 | 手动输入/编辑路径文本 |
| 失焦 | 触发 validation 校验 |

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | 输入框 + Browse 按钮 |
| `valid` | 路径校验通过，绿色勾 |
| `invalid` | 路径校验失败，红色边框 + 错误提示（如"文件不存在"） |
| `browsing` | 系统文件选择器打开中 |

### AI 状态
无。

### 子组件
- `TextBox` — 路径输入框
- `BrowseButton` — 浏览按钮
- `ValidationIcon` — 校验状态图标

### 可复用场景
- PAGE 5: 导出对话框完整输出文件路径选择
- PAGE 6: Settings → General 默认工作区路径
- PAGE 6: Settings → Zotero .bib 路径
- PAGE 6: Settings → Model Management 模型存储路径
- PAGE 6: Settings → Snapshot Policy 快照存储路径
- PAGE 7: Setup Wizard Zotero .bib 路径

### 权限控制
无。文件系统访问权限由操作系统控制，系统文件选择器返回的路径受 OS 权限机制保护。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `path_picker_browse` | 打开文件选择器 | `mode`, `filters` |
| `path_picker_select` | 选中文件/目录 | `path_length`, `mode` |

---

## 组件：ToggleField / NumericField / ComboField / RadioListField

### 组件职责
四类标准化表单字段组件。封装 Label + Control + Validation 的标准表单行布局 (Label 左对齐，Control 紧跟，高度统一 32px)。用于 Settings 所有标签页和导出对话框配置区，确保跨页面表单一致性。

### 统一输入参数（所有变体共享）
| 参数 | 类型 | 说明 |
|------|------|------|
| `label` | string | 字段标签文字 |
| `value` | T (泛型) | 当前值 |
| `onChange` | callback | 值变更回调 |
| `disabled` | boolean | 是否禁用 |
| `hint` | string \| null | 字段下方灰色提示文字 |

### 变体定义

#### ToggleField
| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | boolean | 开关状态 |

**状态**: `on` / `off`
**子组件**: `Label` + `SwitchControl`
**复用**: Settings → General (启动时恢复工作区), Zotero (自动同步), Snapshot Policy (启用快照)
**边界**: `ToggleField` 是完整表单行，不用于 FilterableList 行内开关。列表行内启用/禁用统一使用 `SwitchControl (compact)`，避免表单行布局挤压列表行。

#### NumericField
| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | number | 当前数值 |
| `min` | number | 最小值 |
| `max` | number | 最大值 |
| `unit` | string | 单位标签（如 "px", "分钟"） |

**状态**: `idle` / `focused` / `invalid` (超出范围)
**子组件**: `Label` + `NumericUpDown` + `UnitLabel`
**复用**: Settings → General (字号、文档自动保存间隔), Model Management (内存水位线), Snapshot Policy (历史快照间隔、最大快照数)

#### ComboField
| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | string | 当前选中值 |
| `options` | { value: string, label: string }[] | 下拉选项列表 |

**状态**: `idle` / `expanded` / `selected`
**子组件**: `Label` + `ComboBox` (下拉按钮 + 弹出选项列表)
**复用**: Settings → General (语言、编辑器字体), Model Management (Embedding/OCR 模型选择)

#### RadioListField
| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | string | 当前选中值 |
| `options` | RadioOption[] | 选项：{ value, label, description?, status?, disabled? } |

**状态**: `idle` / `selected` / `option-disabled`
**子组件**: `Label` + `RadioButton × N` + `OptionDescription` + `OptionStatusIcon`
**复用**: PAGE 5 输出格式选择、Setup Wizard 获取方式选择，以及其他小规模单选配置。模板列表和模型列表使用 FilterableList，避免列表样式分叉。

### AI 状态
无。纯表单控件。

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `field_change` | 字段值变更 | `field_id`, `from_value`, `to_value` |

---

## 组件：MarkdownEditor (Core)

### 组件职责
系统核心的 Markdown 文本编辑器。基于 AvaloniaEdit TextEditor，提供 CommonMark 语法高亮、行号 Gutter、Ctrl+F 搜索替换栏、引用标记 `[@key]` 自动补全下拉、锚点链接点击跳转 PDF、以及 NFR-01 要求的每次击键 < 30ms 响应延迟。编辑器内容变更触发自动快照计时器。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | string | Markdown 文本内容 |
| `filePath` | string \| null | 关联的 .md 文件路径 |
| `readOnly` | boolean | 只读模式（快照预览等场景使用），默认 false |
| `variant` | 'editor' \| 'preview' | editor = 完整编辑器；preview = 只读预览变体，隐藏编辑工具并保留滚动/选中文本能力 |
| `citationKeys` | string[] | 可用引用键列表（用于自动补全） |
| `onValueChange` | callback | 内容变更回调（触发快照计时器、状态栏更新） |
| `onAnchorClick` | callback | 锚点链接点击回调（跳转 PDF） |
| `onCitationInsert` | callback | 引用标记插入回调 |
| `onCursorChange` | callback | 光标位置变更回调 |

### 用户操作
| 操作 | 说明 |
|------|------|
| 文本输入 | 键盘输入，实时语法高亮，延迟 < 30ms |
| 插入引用 | 输入 `[@` → 自动补全下拉 → 选择完成 |
| 点击锚点链接 | PDF 阅读器跳转到对应位置 |
| 搜索替换 | Ctrl+F → 搜索栏出现 → 输入即高亮匹配 |
| 撤销/重做 | Ctrl+Z / Ctrl+Shift+Z | 

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | 编辑器就绪，光标闪烁 |
| `loading` | 大文档（>10000 字）加载中，显示进度 |
| `readonly` | 只读模式，光标隐藏，不可编辑（快照预览） |
| `dirty` | 有未保存修改 |

### AI 状态
无直接 AI 状态。输入 `[@` 触发的引用自动补全基于 CitationIndex 规则匹配；未来的语义引用推荐应作为独立 AI 扩展点接入，不改变当前补全交互。

### 子组件
- `EditorCore` — AvaloniaEdit 编辑器核心
- `LineNumberGutter` — 行号列
- `SearchReplaceBar` — 搜索替换栏（浮动）
- `CitationAutocomplete` — 引用标记自动补全下拉
- `AnchorLink` — 内联可点击锚点链接
- `CitationTooltip` — 引用标记悬停预览卡片

### 可复用场景
- PAGE 1: 工作台 Markdown 编辑器（核心使用）
- PAGE 4: 快照预览区（readOnly=true）
- 未来任何需要 Markdown 编辑的场景

### 权限控制
无。readOnly 模式由快照预览场景控制。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `editor_open` | 打开文档 | `file_size_bytes`, `word_count` |
| `editor_citation_insert` | 插入引用标记 | `citation_key`, `insert_method` (autocomplete/manual) |
| `editor_anchor_click` | 点击锚点链接 | `anchor_id`, `target_page` |
| `editor_keystroke_latency` | 每次击键 | `latency_ms` (监控 NFR-01 <30ms) |

---

## 组件：PDFViewer (Core)

### 组件职责
系统核心的 PDF 文献阅读器。基于 Avalonia 渲染 PDF 页面，提供缩放（50%–400%，默认"适应宽度"）、翻页、页码直接跳转。支持：鼠标拖拽框选公式区域（触发 OCR 浮层）、点击段落生成锚点、已生成锚点的区域/页面标记、以及接收来自 Markdown 锚点点击的跳转指令。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `filePath` | string \| null | PDF 文件路径 |
| `currentPage` | number | 当前页码 |
| `zoom` | number (0.5–4.0) | 缩放级别 |
| `anchors` | AnchorDef[] | 已生成的锚点列表（页码 + 位置） |
| `onPageChange` | callback | 翻页回调 |
| `onZoomChange` | callback | 缩放回调 |
| `onRegionSelect` | callback | 框选区域回调（公式 OCR 触发） |
| `onParagraphClick` | callback | 段落点击回调（锚点生成） |
| `jumpToPage` | number \| null | 外部跳转指令（来自锚点点击/引用点击） |

### 用户操作
| 操作 | 说明 |
|------|------|
| 翻页 | 按钮/键盘/滚轮 |
| 缩放 | 滑块/Ctrl+滚轮 |
| 跳转页码 | 在页码输入框中输入数字 |
| 框选区域 | 鼠标拖拽 → 蓝色半透明选区矩形 → 松手触发 OCR |
| 点击段落 | 触发锚点生成 |
| 点击锚点标记 | 显示锚点信息 tooltip |

### 状态
| 状态 | 表现 |
|------|------|
| `idle` | PDF 正常渲染 |
| `loading` | 正在加载/渲染页面 |
| `no-document` | 空状态："拖入 PDF 文献以开始阅读" |
| `scan-version` | 扫描版 PDF 提示（文本提取不可用，降级为页码级锚点） |
| `jumping` | 正在跳转到指定页面 |

### AI 状态
| AI 子状态 | 表现 |
|-----------|------|
| `region-selecting` | 鼠标拖拽中，蓝色半透明选区跟随鼠标 |
| `ocr-triggered` | 选区确认后 → 浮动工具栏弹出（见 FloatingCodeBlock） |

### 子组件
- `PDFRenderer` — PDF 页面渲染核心
- `PageNumberInput` — 页码输入 + 总页数显示
- `ZoomSlider` — 缩放滑块
- `PrevNextButtons` — 上一页/下一页按钮
- `SelectionOverlay` — 选区蓝色半透明矩形
- `AnchorMarker × N` — 锚点位置标记图标
- `ScrollBar` — 垂直滚动条

### 可复用场景
- PAGE 1: 工作台 PDF 阅读器（核心使用）

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `pdf_open` | 打开 PDF | `file_size_mb`, `page_count` |
| `pdf_page_jump` | 跳转页面 | `from_page`, `to_page`, `source` (manual/anchor/citation) |
| `pdf_region_select` | 框选公式区域 | `selection_area_px` |
| `pdf_anchor_create` | 生成锚点 | `page`, `paragraph_index` |

---

## 组件：DiffViewer

### 组件职责
文本差异对比视图。默认 `side-by-side` 用于宽视图：左侧红色背景删除行，右侧绿色背景新增行，中间有连接线指示变更映射。快照时间轴的 280px 上下文面板必须使用 `inline-compact`：删除/新增块纵向排列，不做左右并排。只读模式，不支持编辑。

### 输入数据
| 参数 | 类型 | 说明 |
|------|------|------|
| `oldText` | string | 旧版本文本（历史快照） |
| `newText` | string | 新版本文本（当前版本） |
| `diffAlgorithm` | 'line' \| 'word' | 对比粒度，默认 'line' |
| `layoutMode` | 'side-by-side' \| 'inline-compact' | 布局模式；280px 上下文面板固定使用 `inline-compact` |

### 用户操作
| 操作 | 说明 |
|------|------|
| 滚动 | `side-by-side` 两侧同步滚动；`inline-compact` 单列滚动 |
| 无其他操作 | 只读展示 |

### 状态
| 状态 | 表现 |
|------|------|
| `computing` | 正在计算 diff |
| `no-diff` | "两个版本内容相同" |
| `diff-ready` | `side-by-side` 展示左右对比；`inline-compact` 展示单列变更块：删除行（红底）+ 新增行（绿底）+ 未变行（灰色） |

### AI 状态
无。

### 子组件
- `DiffPane (old)` — 旧版本面板（红色删除行高亮，side-by-side）
- `DiffPane (new)` — 新版本面板（绿色新增行高亮，side-by-side）
- `InlineDiffBlock × N` — 单列变更块（inline-compact）
- `DiffConnector` — 行间变更映射连线（仅 side-by-side）
- `SyncScrollController` — 两侧同步滚动（仅 side-by-side）

### 可复用场景
- PAGE 4: 快照时间轴预览区"显示差异"模式（inline-compact）
- 未来任何需要版本对比的场景

### 权限控制
无。

### 日志/埋点建议
| 事件 | 时机 | 参数 |
|------|------|------|
| `diff_view` | 打开差异对比 | `diff_lines_added`, `diff_lines_removed` |

---

# 附录：组件矩阵

| 组件 | 职责类型 | AI 耦合 | 复用页面 | 页面定义引用 |
|------|---------|---------|---------|------------|
| ModalHost | 容器 | 无 | P5, P6, P7, P9 | PAGE 5/6/7/9 |
| SplitPane | 布局 | 无 | P1 | PAGE 1 |
| TabBar | 导航 | 无 | P1 上下文面板容器（承载 P2-P4）, P6 | PAGE 1/6；P2-P4 共享 PAGE 1 的上下文面板 TabBar |
| PanelContainer | 容器 | 无 | P1 | PAGE 1 |
| Toolbar | 命令 | 无 | P1 | PAGE 1 |
| StatusBar | 信息展示 | 嵌入 (ModelStatusIndicator, MemoryIndicator) | P1 | PAGE 1 |
| MemoryIndicator | 指标 | AI 内存占用 | P1, P6 | PAGE 1/6 |
| FilterableList | 数据展示 | 扩展点（智能排序/去重） | P3, P5, P6 | PAGE 3/5/6 |
| Timeline | 数据展示 | 扩展点（语义摘要/风险评估） | P4 | PAGE 4 |
| ChatMessageList | AI 交互 | 核心 AI 载体 | P2 | PAGE 2 |
| ModelStatusIndicator | AI 状态 | 核心 AI 载体 | P1, P2, P6 | PAGE 1/2/6 |
| StreamingTextBlock | AI 渲染 | 核心 AI 载体 | P2 | PAGE 2 |
| CitationChip | 交互 | AI 输出辅助 | P2 | PAGE 2 |
| FloatingCodeBlock | AI 交互 | 核心 AI 载体 | P8 | PAGE 8 |
| ProgressStepper | 反馈 | 无 | P5, P7 | PAGE 5/7 |
| ConfirmDialog | 反馈 | 无 | P2, P4, P6 | PAGE 2/4/6 |
| Toast | 反馈 | 无 | P3, P5(可选), P8(可选), P9, 全局 | PAGE 3/5/8/9 + 全局 |
| EmptyState | 引导 | 扩展点 | P1, P2, P3, P4, P5 | PAGE 1/2/3/4/5 |
| PathPicker | 表单 | 无 | P5, P6, P7 | PAGE 5/6/7 |
| ToggleField | 表单 | 无 | P6 | PAGE 6 |
| NumericField | 表单 | 无 | P6 | PAGE 6 |
| ComboField | 表单 | 无 | P6 | PAGE 6 |
| RadioListField | 表单 | 无 | P5, P7 | PAGE 5/7 |
| MarkdownEditor | 内容编辑 | 规则型引用补全；AI 引文推荐为未来扩展点 | P1, P4 | PAGE 1/4 |
| PDFViewer | 内容渲染 | OCR 触发 | P1 | PAGE 1 |
| DiffViewer | 内容渲染 | 无 | P4 | PAGE 4 |
