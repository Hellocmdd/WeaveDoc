# Avalonia 前端骨架重构任务清单

## 目标

把 WeaveDoc 桌面端前端重构为参考界面一致的 Avalonia 原生骨架。第一阶段只完成可运行、可测试的界面骨架和本地状态切换，不接入真实文档打开、Markdown/PDF 渲染、保存、AI/RAG 查询等业务能力。

## 已确认

- 重构对象：只包含 `src/WeaveDoc.App` 与 `tests/WeaveDoc.App.Tests`。
- 参考来源：`doc/软件设计/界面设计_demo`，以 final 完整稿风格为主。
- 默认内容：空状态，不展示假文件、假任务、假 Markdown、假 AI 对话。
- 交互范围：顶部菜单/工具栏入口、编辑/预览模式、辅助栏展开/收起、主题切换、三栏分隔线拖拽只做本地状态变化。
- 三栏拖拽策略：栏宽不跨重启持久化；左/中、 中/右两条分隔线都可拖动；默认最小宽度为左侧文档预览区 `300px`、中央编辑区 `400px`、右侧辅助栏展开态 `280px`。
- 辅助栏收起策略：收起后右侧区域完全隐藏；再次展开恢复本次运行内上次右栏宽度。
- 侧栏标签策略：当前 final 首屏没有侧边栏，任务 5 不新增侧栏标签切换。
- 组织方式：拆成 Avalonia `UserControl` 组合，`MainWindow` 只保留窗口外壳、布局组合和数据上下文绑定。
- 验收强度：构建、测试、短时启动烟测。
- 遗留文件：当前未跟踪的 `doc/软件设计/界面设计_demo/pnpm-workspace.yaml` 保留不动。

## 待确认决策

- 暂无。

## 明确排除

- 不修改 `doc/软件设计/界面设计_demo` 的任何参考实现、配置或资源。
- 不引入 Web、React、Next、Tailwind 或 demo 目录依赖到 Avalonia 应用。
- 不实现真实 Markdown/PDF 打开、保存、预览渲染、索引、AI/RAG 问答。
- 不新增公开业务 API；只允许新增界面控件、Shell 级 ViewModel 状态和本地 UI 命令。
- 未接入业务服务的按钮、菜单和命令必须保持禁用，或以明确空状态表达“待接入”。

## 执行任务

### 任务 1：确认重构边界

- [x] 复核当前 Avalonia 入口、主窗口和测试锚点，包括 `MainWindow.axaml`、`MainWindow.axaml.cs`、`ViewModels` 与 `MainWindowTests.cs`。
- [x] 确认本轮只重构桌面端前端骨架，不把 demo 工程纳入改造范围。
- [x] 在实现前记录当前未跟踪的 demo `pnpm-workspace.yaml`，执行过程中不修改、不删除、不提交。

执行记录：

- 2026-06-04：已复核 `Program.cs`、`App.axaml.cs`、`MainWindow.axaml`、`MainWindow.axaml.cs`、`RagTabViewModel.cs`、`MainWindowTests.cs` 与 `TestAppBuilder.cs`；当前入口为 `Program` -> `App` -> `MainWindow`，主窗口仍由 `MainTabs` 承载 RAG、转换、Markdown 编辑和模板管理区域，现有主窗口测试锚点为 `MainTabs` 与 `Markdown 编辑` 标签。
- 2026-06-04：已确认本轮任务 1 不修改 demo 工程；`doc/软件设计/界面设计_demo/pnpm-workspace.yaml` 当前为未跟踪文件，本轮保留不动、不删除、不纳入提交。

验收标准：

- [x] 后续 diff 只涉及 `src/WeaveDoc.App`、`tests/WeaveDoc.App.Tests`，以及本任务清单的状态更新。
- [x] demo 目录除原本未跟踪文件外没有新增改动。

### 任务 2：建立控件拆分结构

- [x] 移除不符合 final demo 首屏的左侧导航轨，保留顶部标题、菜单和工具栏入口。
- [x] 新增或调整工作区侧栏控件。
- [x] 新增或调整中央编辑工作区控件。
- [x] 新增或调整右侧辅助栏控件。
- [x] 新增或调整底部状态栏区域控件，并移除不符合 final demo 首屏的右下浮窗。
- [x] 将 `MainWindow` 收敛为窗口外壳、主布局组合和数据上下文绑定。

验收标准：

- [x] 主窗口结构能清晰定位五个核心区域：顶部入口区、左侧文档预览区、编辑区、辅助栏、底部状态栏。
- [x] `MainWindow` 不再承载大量区域内部 UI 细节。
- [x] 控件命名、命名空间和 XAML 资源引用与现有 Avalonia 项目风格一致。

### 任务 3：重整 Shell 级 ViewModel

- [x] 建立 Shell 级状态中心，承载当前导航、侧栏页签、编辑/预览模式、辅助栏展开状态、主题状态。
- [x] 将界面按钮、页签和切换控件绑定到本地状态命令或属性。
- [x] 对未接入的文件打开、保存、AI 查询等业务命令设置禁用或待接入状态。
- [x] 将默认数据改为空状态，不初始化假文件、假任务、假 Markdown、假 AI 对话。

执行记录：

- 2026-06-04：新增 `AppShellViewModel` 作为 Shell 级状态中心，承载导航、侧栏页签、编辑/预览模式、AI 面板展开状态和主题状态；`MainWindow` 改为绑定该 ViewModel，不再在窗口打开时初始化 `RagTabViewModel` 或 RAG 服务。
- 2026-06-04：将顶部入口区、工作区侧栏、中央编辑区、右侧 AI 面板和底部状态栏统一改为本地状态切换；中央区替换为空状态编辑/预览骨架，不再嵌入真实业务 Tab。
- 2026-06-04：文件导入、添加、刷新、删除、打开、保存、导出、AI 输入、清空和发送等未接入入口已禁用；默认界面只展示空状态文案，不初始化演示文件、演示 Markdown、演示 AI 对话或 RAG 检索状态。
- 2026-06-04：已更新 `MainWindowTests.cs`，覆盖默认 Shell 状态、本地状态切换、未接入入口禁用，以及旧 RAG/演示文案不回流。

验收标准：

- [x] 启动后界面显示空状态，而不是演示数据。
- [x] 本地 UI 状态可以被测试直接断言。
- [x] 未接入业务入口不会触发真实文件、网络、RAG 或保存操作。

### 任务 4：复刻 final 完整稿主视觉

- [x] 参考 `doc/软件设计/界面设计_demo/app/page.tsx` 的 final 完整稿布局和层级。
- [x] 实现顶部入口区、文件/工作区侧栏、中央编辑区、右侧辅助栏与底部状态栏风格。
- [x] 复用现有 Avalonia 主题资源体系，按需要补充局部样式资源。
- [x] 保证窗口最小尺寸下主要区域不互相遮挡，文本不溢出关键控件。

执行记录：

- 2026-06-04：将 Shell 视觉资源集中到 `App.axaml`，主窗口改为顶部标题/菜单/工具区、左侧深色导航轨、工作区侧栏、中央编辑空状态区、右侧 AI 面板和底部状态栏的 final 完整稿层级；Shell 子控件统一改用资源画刷和紧凑桌面分区风格。
- 2026-06-04：新增 UI 回归断言，覆盖 final 骨架核心区域存在性、最小窗口尺寸下区域可见性，以及 Shell 视觉资源加载；未引入 Web/demo 技术栈依赖，默认界面继续保持空状态。
- 2026-06-04：验证已通过 `dotnet build WeaveDoc.slnx --no-restore`、`dotnet test WeaveDoc.slnx --no-build`；短时启动烟测 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 无错误输出，并由 timeout 结束运行中的桌面应用。
- 2026-06-04：按复核反馈修正左侧主栏，将可见的文档/设置管理侧栏替换为 demo final 稿里的文档预览面板骨架，包括页码/缩放工具条、文件标签条、纸张预览画布和真实空状态；旧管理侧栏入口不再作为首屏可见内容。
- 2026-06-04：同步更新 `MainWindowTests.cs`，新增左侧文档预览骨架断言，并调整禁用入口与最小窗口布局断言；验证已通过 `dotnet build WeaveDoc.slnx --no-restore`、`dotnet test WeaveDoc.slnx --no-build`、`git diff --check`，短时启动烟测 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 无错误输出并由 timeout 结束运行中的桌面应用。
- 2026-06-04：按复核反馈移除右下角 `+ / AI` 常驻浮窗；该浮窗不属于 demo final 稿默认首屏，AI 面板收起仍由右侧 AI 面板自身按钮作为本地状态切换入口覆盖。
- 2026-06-04：移除右下浮窗后再次验证通过 `dotnet build WeaveDoc.slnx --no-restore`、`dotnet test WeaveDoc.slnx --no-build`、`git diff --check`，短时启动烟测无错误输出并由 timeout 结束运行中的桌面应用。
- 2026-06-04：按复核反馈移除最左侧 `问/编/转/设` 竖向导航轨；demo final 稿默认首屏由顶部标题、菜单和工具栏展示入口，主工作区直接进入文档预览、编辑区和 AI 面板三栏结构。
- 2026-06-04：移除左侧导航轨后再次验证通过 `dotnet build WeaveDoc.slnx --no-restore`、`dotnet test WeaveDoc.slnx --no-build`、`git diff --check`，短时启动烟测无错误输出并由 timeout 结束运行中的桌面应用。

验收标准：

- [x] 第一屏视觉结构与 final 完整稿的区域分布一致。
- [x] 顶部入口区、左侧文档预览区、中央工作区、右侧辅助栏和底部状态信息均可见。
- [x] 不出现 Web/demo 技术栈依赖。

### 任务 5：实现骨架级本地交互

- [x] 顶部菜单/工具栏入口能体现本地状态和选中样式。
- [x] 编辑/预览模式切换能改变中央区域状态。
- [x] 三栏分隔线拖拽能调整左侧文档预览区、中央编辑区、右侧辅助栏宽度；实现前先确认持久化策略、可拖分隔线范围和最小宽度值。
- [x] 辅助栏展开/收起能改变右侧区域可见性或宽度状态。
- [x] 主题切换能沿用现有主题机制并反映到界面。

执行记录：

- 2026-06-04：经确认，当前 final 首屏没有侧边栏，已按要求删除任务 5 中“侧栏标签切换”条目；本轮不新增侧栏或侧栏标签。
- 2026-06-04：顶部仅启用本地状态入口：工具栏 `AI 助手` 用于展开/收起右侧 AI 面板并反映 active 样式，顶部主题按钮用于切换 Shell 主题；文件、打开、保存、导出、搜索、设置等未接入业务入口继续禁用。
- 2026-06-04：编辑/预览按钮随 `EditorMode` 更新 active 样式，并只切换中央空状态内容。
- 2026-06-04：三栏分隔线改为 Avalonia 原生 `GridSplitter`；左/中、 中/右两条分隔线都可拖动，最小宽度为左侧文档预览区 `300px`、中央编辑区 `400px`、右侧 AI 面板展开态 `280px`，栏宽只保存在本次运行内。
- 2026-06-04：AI 面板收起后右侧列宽和右分隔线宽度变为 `0`，面板完全隐藏；再次展开恢复本次运行内上次右栏宽度。
- 2026-06-04：主题切换沿用 Shell 资源体系，切换时同步更新 `RequestedThemeVariant` 与共享 `SolidColorBrush` 资源，确保整套 Shell 配色变化。
- 2026-06-04：验证已通过 `dotnet build WeaveDoc.slnx --no-restore`、`dotnet test WeaveDoc.slnx --no-build`、`git diff --check`；短时启动烟测 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 无错误输出并由 timeout 结束；demo 目录仍只有原本未跟踪的 `doc/软件设计/界面设计_demo/pnpm-workspace.yaml`。
- 2026-06-05：按命名复核反馈，将可见文案从单一 `AI` 命名收口为综合 `辅助` 栏；顶部组名改为 `辅助`，展开/收起文案改为 `展开辅助栏` / `收起辅助栏`，右侧页签和空状态改为 `问答`、`问答辅助`、`暂无问答记录` 等任务域表达，内部 `Ai*` 控件和 ViewModel 命名暂不做结构性重命名。

验收标准：

- [x] 所有骨架交互只改变本地 UI 状态。
- [x] 禁用业务入口不会被误认为已实现。
- [x] 三栏拖拽只改变本地布局状态，不触发文件、渲染、AI/RAG 或保存操作。
- [x] 栏宽变化受实现前确认的最小宽度约束，拖动后布局稳定，不出现控件重叠或明显跳动。

### 任务 6：更新 UI 回归测试

- [x] 更新或新增 Avalonia headless 测试，覆盖主布局核心区域存在性。
- [x] 覆盖顶部入口、编辑/预览、辅助栏和主题切换。
- [x] 覆盖三栏宽度调整、最小宽度约束和拖拽后布局稳定。
- [x] 覆盖默认空状态。
- [x] 增加断言，确认旧的假文件、假任务、假 Markdown、假 AI 对话不再出现。

执行记录：

- 2026-06-05：补强 `MainWindowTests.cs`，默认 Shell 状态新增空状态字符串断言；三栏测试新增 `WorkspaceSplitters_CanBeDraggedAndKeepColumnMinimums`，通过 Avalonia.Headless 指针事件拖动左右 `GridSplitter`，并验证左侧文档预览区、中央编辑区和右侧 AI 面板展开态保持 `300px`、`400px`、`280px` 最小宽度，拖动后 splitter 与核心区域布局稳定。
- 2026-06-05：扩展旧演示内容回流断言，覆盖 RAG、转换、模板、假论文、假 Markdown、假任务和假 AI 对话相关文本；测试仍只依赖 headless UI 与本地 ViewModel 状态，不接触真实文件系统文档、网络服务或 RAG 后端。
- 2026-06-05：验证已通过 `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore`，结果为 23 个测试全部通过；本轮未执行任务 7 的完整构建、全量测试或启动烟测。

验收标准：

- [x] `tests/WeaveDoc.App.Tests` 能证明主界面骨架存在且可切换。
- [x] 测试能捕获旧占位示例内容回流。
- [x] 测试不依赖真实文件系统文档、网络服务或 RAG 后端。

### 任务 7：执行验收命令

- [x] 运行 `dotnet build WeaveDoc.slnx --no-restore`。
- [x] 运行 `dotnet test WeaveDoc.slnx --no-build`。
- [x] 运行短时 `dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 启动烟测。
- [x] 记录所有失败并在同一轮实现中修复，直到验收通过或明确阻塞原因。

执行记录：

- 2026-06-05：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 Warning，0 Error。
- 2026-06-05：`dotnet test WeaveDoc.slnx --no-build` 通过，4 个测试项目共 150 个测试全部通过。
- 2026-06-05：短时启动烟测 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 无错误输出；命令由 `timeout` 结束运行中的桌面应用并返回 124，符合短时烟测预期。
- 2026-06-05：本轮任务 7 未发现失败项，无需修复代码。

验收标准：

- [x] 构建通过。
- [x] 测试通过。
- [x] 应用能短时启动并退出烟测。

### 任务 8：收尾检查与清单更新

- [x] 运行 `git diff --check`。
- [x] 确认 demo 目录没有被本轮修改。
- [x] 确认 `doc/软件设计/界面设计_demo/pnpm-workspace.yaml` 仍保持当前未跟踪状态，不纳入本次提交。
- [x] 将已完成任务在本清单中勾选，并在交付说明中列出验证命令结果。

执行记录：

- 2026-06-05：`git diff --check` 通过，无空白错误输出。
- 2026-06-05：`git diff --name-status -- doc/软件设计/界面设计_demo` 无输出，确认 demo 目录没有 tracked diff。
- 2026-06-05：`git status --short -- doc/软件设计/界面设计_demo/pnpm-workspace.yaml` 仍显示该文件为未跟踪状态；本轮未修改、未删除、未纳入提交。
- 2026-06-05：最终复验 `dotnet build WeaveDoc.slnx --no-restore` 通过，0 Warning，0 Error。
- 2026-06-05：最终复验 `dotnet test WeaveDoc.slnx --no-build` 通过，4 个测试项目共 150 个测试全部通过。
- 2026-06-05：最终复验短时启动烟测 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 无错误输出；命令由 `timeout` 结束运行中的桌面应用并返回 124，符合短时烟测预期。
- 2026-06-05：本清单任务 1-8 与最终完成标准已按当前验证结果同步勾选；交付说明需列出构建、测试、启动烟测、diff 检查和 demo 未跟踪文件结果。

验收标准：

- [x] `git diff --check` 无空白错误。
- [x] 本清单能准确反映实际完成进度。
- [x] 交付说明明确列出未处理的 demo 未跟踪文件。

## 最终完成标准

- [x] Avalonia 桌面端启动后展示与 final 完整稿一致的五区骨架布局。
- [x] 默认界面为空状态，没有任何误导性的演示业务数据。
- [x] 骨架级本地交互全部可用并有测试覆盖。
- [x] 未接入的业务入口不会执行真实业务操作。
- [x] 构建、测试、短时启动烟测和 `git diff --check` 均通过。
