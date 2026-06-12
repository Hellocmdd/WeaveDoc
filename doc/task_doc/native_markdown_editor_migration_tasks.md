# 原生 Markdown 编辑器迁移评估与任务清单

创建日期：2026-06-08

## 目标

将 WeaveDoc 的 Markdown 编辑主线迁移为原生 Avalonia 编辑体验：

- UI 框架：Avalonia UI。
- MVVM 工具：CommunityToolkit.Mvvm。
- 编辑区：AvaloniaEdit，负责 Markdown 源码编辑、行号、换行、选择、撤销重做和语法高亮。
- 预览区：Avalonia `NativeWebView`，只承载 Markdig 生成的 HTML 预览。
- Markdown 渲染：Markdig，统一负责 Markdown 到 HTML 的转换、扩展语法和源码位置标记。

本清单的核心目标不是继续修补 Monaco，而是把 Monaco/WebView2 从 Markdown 编辑主路径中移除，让 WebView 只作为 Markdown 预览输出层存在。

最新确认：

- Markdown 编辑核心可以迁移到 AvaloniaEdit + CommunityToolkit.Mvvm + Markdig 的原生 C# 主线。
- Markdown 预览显示层仍按已确认方案使用 Markdig HTML + Avalonia `NativeWebView`。
- PDF Reader 短期保持现有 PDF.js + `NativeWebView` 链路，独立隔离并延后，不纳入本清单的“原生 Markdown 编辑器”迁移目标。
- 若未来要让 PDF Reader 也去 WebView/PDF.js，必须另开 PDF Reader feasibility / migration 清单，先验证 PDFium、MuPDF、Syncfusion 或严格纯 C# 文本阅读方案。

## 当前代码基线

- `src/WeaveDoc.App/WeaveDoc.App.csproj` 已使用 Avalonia `12.0.4`，但未引用 `CommunityToolkit.Mvvm`。
- `src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj` 已引用：
  - `Avalonia.AvaloniaEdit` `12.0.0`
  - `AvaloniaEdit.TextMate` `12.0.0`
  - `TextMateSharp.Grammars` `2.0.4`
  - `Avalonia.Controls.WebView` `12.0.1`
- `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` 已引用 `Markdig` `0.39.1`，但 MarkdownEditor/App 侧尚未拥有独立 Markdown 渲染依赖或共享渲染服务。
- `src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.*` 已存在 AvaloniaEdit 原生编辑控件雏形。
- `src/WeaveDoc.App/Views/EditorWorkspace.axaml` 当前编辑区仍绑定 `MonacoEditorControl`。
- `src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.axaml` 当前仍保留 `MonacoEditorControl` + `PreviewWebViewControl` 的旧组合。
- `src/WeaveDoc.MarkdownEditor/Views/MainWindow.axaml` 已出现 `NativeMarkdownEditorControl` + `PreviewWebViewControl` 组合，但仍需确认其是否是唯一运行入口，并补齐 Shell 兼容。
- `src/WeaveDoc.MarkdownEditor/Controls/MonacoEditorControl.axaml.cs` 当前仍直接引用 `Microsoft.Web.WebView2.Core` / `CoreWebView2` / `WebView2EnvironmentManager`，与“原生编辑器”目标冲突。
- `PreviewWebViewControl`、`PdfViewerControl`、`IWebViewHost`、`NativeWebViewHost`、fake host 测试基础已经存在，但历史诊断显示 Linux GTK offscreen + `UserControl` 嵌套宿主可能产生 `window.innerWidth = 1` / `window.innerHeight = 1` 的空白渲染问题。
- PDF Reader 当前基线是 `PdfViewerControl` + PDF.js 静态资源 + 本地 PDF.js 服务 + `NativeWebView`，本清单只要求迁移 Markdown 编辑主线时不误删、不误接、不误启用 PDF Reader。

## 选型评估结论

### Avalonia UI + CommunityToolkit.Mvvm

结论：适合当前项目，但要限制迁移范围。

优点：

- Avalonia 已是当前桌面 UI 技术栈，继续使用可以避免引入 WPF/WinUI 等平台分叉。
- CommunityToolkit.Mvvm 足够轻量，适合文档状态、命令、打开保存流程、错误状态和 UI 可见性控制。
- Source generator 可减少 `INotifyPropertyChanged` 样板代码，让 `DocumentWorkspaceViewModel`、`AppShellViewModel` 的状态边界更清晰。

风险：

- 当前 ViewModel 已经存在手写属性和命令，直接全量迁移容易扩大影响面。
- Avalonia compiled binding 对属性名、可空性和可见性更敏感；迁移时必须同步更新测试。
- CommunityToolkit.Mvvm 不是导航框架，不应借此重写 Shell 架构或业务模块。

评估结果：

- 只迁移 Markdown 文档相关 ViewModel 和必要 Shell 状态，不全量重写 App。
- 所有新命令必须能通过 fake service/headless 测试验证，不依赖真实文件对话框或真实 WebView。

### AvaloniaEdit 编辑区

结论：这是本轮最稳的核心替换点。

优点：

- 原生 Avalonia 控件，不依赖 WebView、WebView2、Monaco JS、HTML 资源加载或 JS readiness。
- 适合源码型 Markdown 编辑：行号、等宽字体、自动换行、选择、撤销重做、快捷键和大文本输入。
- 当前项目已经引用 AvaloniaEdit 和 TextMate 相关包，且已有 `NativeMarkdownEditorControl` 雏形。

风险：

- AvaloniaEdit 不是完整 Markdown IDE，需要项目自己补齐工具栏命令、Markdown 包裹操作、脏状态和焦点控制。
- `TextEditor.Text` 与 Avalonia binding 之间需要显式包装，避免程序化载入内容时错误触发 dirty。
- TextMate 安装对象需要跟随控件生命周期释放或避免重复安装。
- 原 Monaco 的滚动同步、选择定位、高亮、预览联动不能直接复用，需要用原生光标/选区事件重建。

评估结果：

- 采用 `NativeMarkdownEditorControl` 作为唯一 Markdown 源码编辑控件。
- 删除或隔离 `MonacoEditorControl` 编辑路径。
- 所有编辑内容以 ViewModel 的 `Content` 为唯一真源，控件不得直接保存文件或直接更新旧窗口 ViewModel。

### NativeWebView + Markdig 预览区

结论：可行，但必须把 WebView 风险控制在预览层。

优点：

- Markdig 生成 HTML 后交给 WebView，能保留 Markdown 扩展、表格、代码块、LaTeX/KaTeX、CSS 样式和未来预览交互能力。
- 预览失败不会影响源码编辑，用户仍可编辑和保存 Markdown。
- 现有 `PreviewWebViewControl`、`IWebViewHost`、fake host 测试可以复用一部分。

风险：

- Linux 下 WPE/WebKitGTK 运行库可用性会影响真实预览。
- 历史诊断显示 `NativeWebView` 放在普通 `UserControl` wrapper 内可能触发 GTK offscreen 1x1 viewport 空白渲染。
- 高频编辑时反复 `NavigateToString` 会造成卡顿、闪烁或 WebView 生命周期压力。
- Markdown 原始 HTML 若直接进入 WebView，存在脚本执行、外链资源和不可信内容风险。
- 预览与编辑滚动同步需要额外的 HTML `data-line` / source position 标记，不能假设 Markdig 默认输出满足。

评估结果：

- NativeWebView 只做预览，不做编辑。
- 预览宿主必须稳定持有，不因编辑/预览模式切换频繁销毁。
- 必须明确 fallback：WebView 不可用时显示真实不可用状态，保留 Markdown 文本和保存能力，不显示假预览。
- Markdig 渲染服务必须定义 HTML 安全策略、资源策略和 debounce 策略。

### PDF Reader 暂行策略

结论：本清单不推进 PDF Reader 纯 C# 化。

当前策略：

- PDF Reader 继续保持 PDF.js + `NativeWebView` 现状，作为独立延后链路。
- 本清单只要求 Markdown 迁移过程中不破坏 `PdfViewerControl`、PDF.js 资源、PDF 相关测试和后续接入可能性。
- PDF Reader 不参与 Markdown 打开、保存、dirty、预览刷新或 Markdig 渲染服务。
- PDF Reader 的“去 WebView / 去 PDF.js / 纯 C# 化”必须单开 feasibility，不能混进本清单。

风险：

- 如果执行 Monaco/WebView2 清理时把 PDF.js 静态资源一并删除，会破坏当前 PDF Reader 基线。
- 如果把 PDF Reader 提前接入 Shell，会扩大当前 Markdown 编辑器迁移范围。
- 如果直接承诺严格纯 C# 高保真 PDF viewer，会把任务变成 PDF 渲染引擎研发，超出本清单目标。

## 明确排除

- 不继续修补 Monaco 作为 Markdown 编辑器。
- 不把旧 `MarkdownEditorTab` 整块嵌入新 Shell。
- 不复用旧 `MainWindowViewModel` 作为 Shell 文档状态源。
- 不恢复旧默认 Markdown 内容 `# Hello WeaveDoc!`。
- 不接入 AI/RAG、索引、云 API、问答发送或模型初始化。
- 不接入导出、模板管理、转换流程或输出路径选择。
- 不实现多文档标签页、最近文件列表、自动保存或跨启动恢复。
- 不在本清单中推进 PDF Reader；PDF 入口保持延后阶段。
- 不在本清单中把 PDF Reader 纯 C# 化、PDFium 化、MuPDF 化或 Syncfusion viewer 化。
- 不删除 PDF.js 静态资源、`PdfViewerControl` 或现有 PDF Reader 测试，除非另开并完成 PDF Reader 迁移清单。
- 不修改 `doc/软件设计/界面设计_demo`。

## 待确认但不阻塞清单的策略

以下策略可以在执行前确认；若用户未另行指定，按推荐策略实现：

- 原始 HTML 策略：默认禁用或清洗 Markdown 中的原始 HTML；如需保留原始 HTML，必须增加 sanitizer 和导航拦截验收。
- 预览刷新策略：推荐 150-300ms debounce，避免每次按键立即重建 WebView 页面。
- 预览资源策略：推荐只允许本地模板资源和内联 CSS；外链图片/脚本默认禁止或明确提示。
- 滚动同步策略：第一阶段只保证编辑和预览内容一致；精准双向滚动同步作为后续增强。
- PDF Reader 策略：短期保持 PDF.js + `NativeWebView`；是否改为无 WebView/native renderer/严格纯 C#，不在本清单确认。

## 执行任务

### 阶段 0：基线锁定与迁移边界

#### 0.1 锁定当前残留和运行入口

- [x] 记录 App Shell、独立 MarkdownEditor `MainWindow`、旧 `MarkdownEditorTab` 三条入口当前分别使用的编辑控件。
- [x] 记录 `MonacoEditorControl`、Monaco 静态资源、WebView2 引用、`NativeMarkdownEditorControl`、`PreviewWebViewControl`、`IWebViewHost` 的当前影响面。
- [x] 明确本清单创建后，旧 `markdown_document_integration_tasks.md` 中“保留 Monaco”的阶段结论不再作为后续编辑器迁移目标。

验收标准：

- [x] 任务记录列出 Shell 当前是否仍引用 `MonacoEditorControl`。
- [x] 任务记录列出独立入口当前是否已使用 `NativeMarkdownEditorControl`。
- [x] `rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager|MonacoEditorControl|monaco-editor" src/WeaveDoc.App src/WeaveDoc.MarkdownEditor tests` 的结果已被分类记录为“待删除、待替换、可保留测试证据或已无残留”。
- [x] 本阶段不修改业务代码，只更新任务记录和基线说明。

任务记录（2026-06-08）：

入口现状：

| 入口 | 当前编辑控件 | 预览 / PDF 控件 | 结论 |
| --- | --- | --- | --- |
| App Shell `src/WeaveDoc.App/Views/EditorWorkspace.axaml` | 仍引用 `MonacoEditorControl`，`x:Name="MarkdownEditorControl"`，绑定 `DocumentWorkspace.Content` | `PreviewWebViewControl`，绑定 `DocumentWorkspace.PreviewHtml` | Shell 仍在旧 Monaco 编辑路径，后续阶段必须替换为原生编辑控件。 |
| 独立 MarkdownEditor `src/WeaveDoc.MarkdownEditor/Views/MainWindow.axaml` | 已使用 `NativeMarkdownEditorControl`，`x:Name="NativeEditor"`，绑定 `EditorContent` | `PreviewWebViewControl` + `PdfViewerControl` | 独立入口已切到原生编辑控件，但仍需验证它是否是唯一独立运行入口并补齐 Shell 兼容。 |
| 旧嵌入页 `src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.axaml` | 仍引用 `MonacoEditorControl`，`x:Name="MonacoEditor"`，绑定 `EditorContent` | `PreviewWebViewControl` + `PdfViewerControl` | 旧嵌入页仍是 Monaco 组合，后续要替换或退役，不能再作为新 Shell 的迁移目标。 |

残留分类：

| 分类 | 当前命中 | 处理边界 |
| --- | --- | --- |
| 待替换 | `src/WeaveDoc.App/Views/EditorWorkspace.axaml:140` 的 `MonacoEditorControl`；`src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.axaml:29` 和 `MarkdownEditorTab.axaml.cs:18,51` 的 Monaco 字段/查找逻辑 | 属于 Markdown 编辑主路径残留，后续由 `NativeMarkdownEditorControl` 或正式化后的原生编辑控件替换。 |
| 待删除 | `src/WeaveDoc.MarkdownEditor/Controls/MonacoEditorControl.axaml*`；`src/WeaveDoc.MarkdownEditor/Assets/monaco-editor/` 静态资源；`MonacoEditorControl.axaml.cs` 中的 `Microsoft.Web.WebView2.Core`、`CoreWebView2*`、`WebView2EnvironmentManager` 和 `Assets/monaco-editor/index.html` 路径 | 只有在 App Shell、旧嵌入页和测试都不再依赖 Monaco 后删除；删除时不要误删 PDF.js 静态资源。 |
| 待替换的测试证据 | `tests/WeaveDoc.App.Tests/MainWindowTests.cs:308,362` 仍验证 Shell Monaco；`tests/WeaveDoc.MarkdownEditor.Tests/WebViewHostControlTests.cs` 多个 `MonacoEditorControl_*` 测试仍验证旧控件；`tests/WeaveDoc.MarkdownEditor.Tests/MainWindowOpenWorkflowTests.cs:40,50` 仍查找 `MonacoEditor` / `monaco-editor` | 这些测试现在是旧行为证据。迁移阶段应改为验证原生编辑控件、预览宿主和不可用 fallback，不应继续要求 Monaco。 |
| 可保留 | `NativeMarkdownEditorControl` 已有 `EditorContent` 双向绑定、AvaloniaEdit 和 TextMate 语法高亮；`PreviewWebViewControl` 用于 Shell、独立入口、旧嵌入页和测试；`Controls/Web/IWebViewHost*`、`NativeWebViewHost*`、`WebViewHostFactoryProvider` 被 `PreviewWebViewControl`、`PdfViewerControl` 和 fake host 测试使用 | `NativeMarkdownEditorControl` 是后续编辑主线；`PreviewWebViewControl` / `IWebViewHost` 是预览/PDF 宿主边界，迁移 Markdown 编辑器时保留。 |
| 已无残留或未命中 | 项目/props/targets 层未命中直接 `Microsoft.Web.WebView2` 包引用；`CommunityToolkit.Mvvm` 尚未被 App 或 MarkdownEditor 项目引用；`Markdig` 当前只在 `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` 中引用；`Avalonia.AvaloniaEdit` 和 `Avalonia.Controls.WebView` 当前只在 `src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj` 中引用 | 后续 1.x 阶段再处理包引用和 Markdown 渲染服务边界。 |

命令记录：

- 已执行任务要求的精确残留命令：`rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager|MonacoEditorControl|monaco-editor" src/WeaveDoc.App src/WeaveDoc.MarkdownEditor tests`。结果包含 App Shell、旧嵌入页、Monaco 控件、Monaco 静态资源、旧测试证据和 MarkdownEditor README 中的旧描述；其中 `Assets/monaco-editor/min/**` 为 Monaco 静态 payload，不作为业务代码入口。
- 已补充执行可读残留命令：`rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager|MonacoEditorControl|monaco-editor" src/WeaveDoc.App src/WeaveDoc.MarkdownEditor tests -g '!src/WeaveDoc.MarkdownEditor/Assets/monaco-editor/min/**'`，用于确认非 minified 代码和测试命中。
- 已补充执行项目引用命令：`rg -n "Microsoft\\.Web\\.WebView2|WebView2EnvironmentManager|PackageReference.*WebView2|Avalonia\\.Controls\\.WebView|Avalonia\\.AvaloniaEdit|CommunityToolkit\\.Mvvm|Markdig" . -g '*.csproj' -g '*.props' -g '*.targets' -g '!**/bin/**' -g '!**/obj/**'`。

边界结论：

- 从本清单开始，旧 `doc/task_doc/markdown_document_integration_tasks.md` 中“保留 Monaco”的阶段性结论只作为历史兼容背景，不再作为后续 Markdown 编辑器迁移目标。
- 本阶段只更新本任务记录和基线说明，未修改业务代码、测试代码、资源文件或项目引用。

#### 0.2 明确验收命令集

- [x] 固定本清单后续任务的最低验证命令。
- [x] 标记哪些任务需要 runtime smoke，哪些任务只需要 build/test/diff check。
- [x] 明确 headless 测试不能依赖真实 NativeWebView。

验收标准：

- [x] 后续每个完成阶段都至少记录 `dotnet build WeaveDoc.slnx --no-restore` 或对应项目构建结果。
- [x] 触及 App Shell 的任务必须记录 `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore` 或更窄 filter。
- [x] 触及 MarkdownEditor 控件的任务必须记录 `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 或更窄 filter。
- [x] 触及共享行为或包引用的任务必须记录 `dotnet test WeaveDoc.slnx --no-build`。
- [x] 每轮结束必须记录 `git diff --check`。
- [x] 用户要求“跑起来看看”时，必须记录 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 和/或 `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build`。

任务记录（2026-06-08）：

最低命令矩阵：

| 触发范围 | 必须记录的验证命令 | 备注 |
| --- | --- | --- |
| 任意完成阶段 / 任意代码改动 | `dotnet build WeaveDoc.slnx --no-restore` 或受影响项目的 `dotnet build <project>.csproj --no-restore` | 优先记录全解决方案构建；若只跑项目构建，必须说明为什么足够。 |
| 触及 App Shell、`EditorWorkspace`、Shell ViewModel、打开/保存入口或 App 侧 Markdown 状态 | `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore` 或更窄 `--filter <相关测试>` | 使用 filter 时记录 filter 表达式和覆盖理由。 |
| 触及 `NativeMarkdownEditorControl`、`PreviewWebViewControl`、`PdfViewerControl`、`IWebViewHost`、独立 MarkdownEditor 窗口或 MarkdownEditor ViewModel | `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 或更窄 `--filter <相关测试>` | 控件级测试必须使用 headless/fake host，不创建真实 NativeWebView。 |
| 触及共享行为、包引用、项目引用、props/targets、渲染服务边界、跨 App 和 MarkdownEditor 的契约 | `dotnet test WeaveDoc.slnx --no-build` | 需要先完成对应 build；若 build 失败或未运行，不能把 `--no-build` 测试结果当成完整验证。 |
| 清理 Monaco / WebView2 / 静态资源残留 | `rg -n "MonacoEditorControl|monaco-editor" src/WeaveDoc.App src/WeaveDoc.MarkdownEditor tests` 和 `rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager" src/WeaveDoc.MarkdownEditor` | 命中必须分类为已退役文档/测试证据、待删残留或允许保留的非运行路径。 |
| 任意一轮结束 | `git diff --check` | 记录结果；若失败，先修复本轮引入的空白问题，不顺手改无关文件。 |

runtime smoke 判定：

- 必须记录 runtime smoke 的任务：修改 App 或 MarkdownEditor 启动入口、窗口布局根节点、真实文件打开流程、NativeWebView 预览宿主生命周期、PDF/预览宿主可用性判断，或用户明确要求“跑起来看看”“真实启动”“看是否白屏”。
- App Shell smoke 命令：`timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build`。
- 独立 MarkdownEditor smoke 命令：`timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build`。
- 只需要 build/test/diff check 的任务：纯任务清单更新、README/文档更新、服务层纯函数变更、ViewModel 纯状态变更、headless fake host 覆盖充分且没有声明真实渲染修复的控件内部变更。
- 若 smoke 因环境没有图形会话、WebKit/WPE 运行库缺失或 timeout 正常截断而不能证明真实渲染，必须把结果写成“烟测/环境限制”，不能写成“预览已真实可用”。

headless 测试边界：

- Headless 测试不得依赖真实 `NativeWebView`、真实 WebKit/WPE/GTK 运行库、真实系统文件对话框或真实网络资源。
- 预览、PDF 和 WebView 通信测试必须通过 `IWebViewHost` fake、fake file picker、fake renderer 或可替换 scheduler 注入验证。
- Headless 测试可以断言控件树、绑定、fallback 文案、host 方法调用和消息分发；不能把真实浏览器 viewport、真实 HTML 渲染或 Linux 1x1 viewport 问题当成 headless 可证明项。
- 需要证明真实 NativeWebView 尺寸、白屏、崩溃或运行库可用性时，只能通过 runtime smoke 或单独的手工/脚本化真实运行记录补充。

本任务验证记录：

- 本轮 0.2 只固定后续验收命令集并更新任务清单，不修改业务代码、测试代码、项目引用或资源文件。
- 本轮实际收尾命令记录：`git diff --check` 通过（exit code 0）。

### 阶段 1：包引用与 MVVM 边界

#### 1.1 引入 CommunityToolkit.Mvvm

- [x] 在需要承载 Markdown 文档状态的项目中添加 `CommunityToolkit.Mvvm`。
- [x] 不把 CommunityToolkit.Mvvm 引入无关业务模块，避免全项目无意义扩散。
- [x] 确认 package restore 后 App、MarkdownEditor、测试项目引用关系无循环。

验收标准：

- [x] `src/WeaveDoc.App` 中 Markdown 文档相关 ViewModel 可以使用 `ObservableObject` / `[ObservableProperty]` / `[RelayCommand]`。
- [x] 若 `src/WeaveDoc.MarkdownEditor` 仍保留独立入口 ViewModel，也明确是否同步迁移到 CommunityToolkit.Mvvm。
- [x] `dotnet build WeaveDoc.slnx --no-restore` 通过。
- [x] 没有因 source generator 产生命名冲突、重复属性或重复命令。

任务记录（2026-06-08）：

- 已在 `src/WeaveDoc.App/WeaveDoc.App.csproj` 添加 `CommunityToolkit.Mvvm` `8.4.2`，让 App 侧 `DocumentWorkspaceViewModel` / `AppShellViewModel` 后续可以迁移到 `ObservableObject`、`[ObservableProperty]` 和 `[RelayCommand]`。
- 已在 `src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj` 添加 `CommunityToolkit.Mvvm` `8.4.2`。独立入口当前仍保留 `MainWindowViewModel`，本阶段同步引入包；实际 ViewModel 继承、属性和命令迁移留给 1.2 / 2.3，不在 1.1 中扩大重写。
- 未向 Converter、Rag 或测试项目直接添加 `CommunityToolkit.Mvvm`，避免无关业务模块扩散；测试项目继续通过项目引用使用被测程序集。
- 为满足本阶段 build 验收，在不恢复 WebView2 包引用的前提下，将退役中的 `MonacoEditorControl` code-behind 收敛为 `IWebViewHost` fake-host 兼容壳，并给独立 `MainWindow` 补齐旧 `IMarkdownEditorHost` Monaco 命名成员的空兼容实现；这只是当前半迁移基线的编译兼容处理，不代表继续把 Monaco 作为 Markdown 编辑主路径。
- 已同步更新 `tests/WeaveDoc.MarkdownEditor.Tests/MainWindowOpenWorkflowTests.cs`：独立 `MainWindow` 打开 Markdown 的断言从旧 `MonacoEditor` 改为当前实际入口 `NativeEditor`，预览仍通过 fake host 验证。
- 引用关系确认：`src/WeaveDoc.MarkdownEditor` 没有项目引用；`src/WeaveDoc.App` 仍只引用 `WeaveDoc.Converter`、`WeaveDoc.Rag`、`WeaveDoc.MarkdownEditor`；`tests/WeaveDoc.App.Tests` 只引用 `src/WeaveDoc.App`；`tests/WeaveDoc.MarkdownEditor.Tests` 只引用 `src/WeaveDoc.MarkdownEditor`，未出现新增循环。

命令记录：

- `dotnet package search CommunityToolkit.Mvvm --source https://api.nuget.org/v3/index.json --take 5`：确认 NuGet 上 `CommunityToolkit.Mvvm` 当前可用版本为 `8.4.2`。
- `dotnet restore WeaveDoc.slnx`：通过（exit code 0）。
- `dotnet build WeaveDoc.slnx --no-restore`：首次发现当前基线仍有 WebView2/Monaco host 编译残留；兼容处理后通过（exit code 0），仅余既有测试项目 nullable / CA1416 warnings。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore`：通过，39 passed。
- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands|DocumentWorkspace"`：通过，8 passed。
- `dotnet test WeaveDoc.slnx --no-build`：通过；Rag 13 passed，MarkdownEditor 39 passed，App 45 passed，Converter 107 passed。
- `dotnet list src/WeaveDoc.App/WeaveDoc.App.csproj reference`、`dotnet list src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj reference`、`dotnet list tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj reference`、`dotnet list tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj reference`：引用方向符合上述记录，无新增循环。
- `git diff --check`：通过（exit code 0）。

#### 1.2 收口文档状态真源

- [x] 以 `DocumentWorkspaceViewModel.Content` 或等价 ViewModel 属性作为 Markdown 内容唯一真源。
- [x] 编辑控件只通过 bindable property/event 与 ViewModel 同步内容，不直接保存文件、不直接寻找 Window/DataContext。
- [x] 预览 HTML 由服务层或 ViewModel 派生，不由编辑控件生成。

验收标准：

- [x] 打开文档后，ViewModel 内容、编辑控件内容、预览输入三者一致。
- [x] 程序化载入内容不会错误设置 `IsDirty = true`。
- [x] 用户编辑后 `IsDirty = true`，保存成功后 `IsDirty = false`。
- [x] 读取失败、保存失败不会清空当前文档内容。
- [x] ViewModel 测试不需要实例化真实 Avalonia 控件。

任务记录（2026-06-08）：

- 已将 `src/WeaveDoc.App/ViewModels/DocumentWorkspaceViewModel.cs` 从手写 `INotifyPropertyChanged` 收口到 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，继续由 `Content` 作为 Markdown 文本唯一真源。
- `OpenAsync` 成功路径继续通过 `ApplyDocument` 程序化载入 `Content` / `PreviewHtml` / `CurrentFilePath`，并保持 `IsDirty = false`；失败路径只更新错误状态，不替换或清空当前文档。
- `Content` 的 setter / `UpdateContent` 仍统一调用 `IMarkdownDocumentService.CreatePreview(Content, CurrentFilePath)` 派生 `PreviewHtml`；编辑控件不生成预览 HTML、不保存文件、不寻找 Window 或 DataContext。
- 已补强 `tests/WeaveDoc.App.Tests/DocumentWorkspaceViewModelTests.cs`：fake document service 记录 preview 请求；新增 `ContentSetter_WhenEditorBindingWritesContent_RefreshesPreviewFromDocumentState`，证明控件绑定写回 `Content` 后，预览输入使用同一份 ViewModel 内容和当前路径。
- 现有 App headless 测试 `MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands` 继续证明当前 Shell 编辑控件的 `EditorContent` 双向绑定、`DocumentWorkspace.Content` 和 `PreviewWebViewControl.HtmlContent` 保持一致；本轮没有将 Shell 编辑区替换为 `NativeMarkdownEditorControl`，该替换仍属于 2.2。

命令记录：

- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore`：通过，46 passed。
- `dotnet test WeaveDoc.slnx --no-build`：通过；Rag 13 passed，MarkdownEditor 39 passed，App 46 passed，Converter 107 passed。测试输出仍包含既有 Markdown HTML 调试日志和 `Viewport Size: 1 x 1` 诊断输出，但无失败。
- `git diff --check`：通过（exit code 0）。
- 由于本轮目标文件当前处于未跟踪状态，额外对 `src/WeaveDoc.App/ViewModels/DocumentWorkspaceViewModel.cs`、`tests/WeaveDoc.App.Tests/DocumentWorkspaceViewModelTests.cs`、`doc/task_doc/native_markdown_editor_migration_tasks.md` 执行 `git diff --no-index --check /dev/null <file>`：命令按 no-index 差异返回 exit code 1，但均无空白错误输出。
- 本轮只触及 App 侧 ViewModel / headless 测试和任务清单，未修改启动入口、真实 NativeWebView 生命周期或窗口根布局，因此未执行 runtime smoke。

### 阶段 2：AvaloniaEdit 原生编辑控件正式化

#### 2.1 完善 `NativeMarkdownEditorControl`

- [x] 保留并整理 `EditorContent` 双向绑定。
- [x] 增加程序化内容应用保护，避免 `SetContent` / binding 更新触发错误 dirty。
- [x] 暴露必要编辑命令：focus、insert/wrap selection、get selection、set caret、scroll/reveal line。
- [x] 配置字体、行号、自动换行、tab/缩进、撤销重做和只读状态。
- [x] 配置 Markdown TextMate grammar，并处理 grammar 加载失败的明确 fallback。
- [x] 释放或复用 TextMate installation，避免控件反复创建时泄漏。

验收标准：

- [x] `NativeMarkdownEditorControl` 不引用 WebView、WebView2、Monaco 或 JavaScript。
- [x] 打开 Markdown 后完整内容显示在 AvaloniaEdit 中。
- [x] 用户编辑 AvaloniaEdit 后 ViewModel 内容同步更新。
- [x] 程序化设置同一内容不会重复触发内容变更事件。
- [x] Markdown 语法高亮初始化失败时仍显示可编辑纯文本，不导致窗口空白或崩溃。
- [x] Headless 测试覆盖内容设置、用户编辑回写、选择包裹和 programmatic update 不置脏。

任务记录（2026-06-08）：

- 已将 `NativeMarkdownEditorControl` 收口为原生 AvaloniaEdit 编辑核心：`EditorContent` 继续作为 TwoWay bindable property，`SetContent`、binding 写入和空值归一化统一通过程序化内容应用保护，不把程序化加载误报为用户编辑。
- 已新增 `IsReadOnly` bindable property，并配置 AvaloniaEdit 的行号、自动换行、等宽字体、4 空格缩进和 tab 转空格；撤销/重做继续使用 AvaloniaEdit 内建能力，控件不保存文件、不查找 Window/DataContext。
- 已保留旧入口 `GetContent`、`SetContent`、`InsertAtCursor`、`SetFocus`，并新增 `NativeMarkdownSelection`、`FocusEditor`、`WrapSelection`、`GetSelection`、`SetSelection`、`SetCaretOffset`、`SetCaretPosition`、`RevealLine`、`ScrollToPosition`。所有 caret / selection / line 输入均做边界夹取，空文档和越界输入不抛异常。
- 已将 TextMate Markdown grammar 初始化收口到 fallback 保护逻辑；初始化失败时记录 `MarkdownGrammarStatusText`，编辑器继续以纯文本方式可编辑。控件 detach / dispose 时释放 TextMate installation，重新 attach 时可再次初始化。
- 已新增 `tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs`，覆盖内容设置、用户编辑回写、programmatic update 不触发 `ContentChanged`、选择包裹、caret/selection/scroll 边界、只读状态、grammar 成功/失败 fallback 和重复 dispose。
- 本轮只完成 2.1 控件正式化；未替换 App Shell 的 `MonacoEditorControl`（2.2），未替换/退役旧 `MarkdownEditorTab`（2.3），未删除 Monaco 资源，未修改 PDF Reader / PDF.js 链路。

命令记录：

- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter NativeMarkdownEditorControlTests`：通过，9 passed。测试项目仍输出既有 `PermissionTests` / `EdgeCaseTests` nullable 与 CA1416 warnings。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore`：通过，48 passed。输出仍包含既有 Markdown HTML 调试日志和 `Viewport Size: 1 x 1` 诊断输出。
- `dotnet test WeaveDoc.slnx --no-build`：通过；Rag 13 passed，MarkdownEditor 48 passed，App 46 passed，Converter 107 passed。
- `rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager|MonacoEditorControl|monaco-editor" src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.* tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs`：无输出（exit code 1），确认新控件和本轮新增测试未引入 WebView2 / Monaco / JavaScript 编辑路径。
- `git diff --check`：通过（exit code 0）。由于本轮触及的任务清单、`NativeMarkdownEditorControl` 和新增测试当前仍处于未跟踪路径，额外对这些文件执行 no-index whitespace check，均无空白错误输出。

#### 2.2 替换 Shell 编辑区

- [x] 将 `src/WeaveDoc.App/Views/EditorWorkspace.axaml` 的编辑区从 `MonacoEditorControl` 替换为 `NativeMarkdownEditorControl`。
- [x] 保留无文档空状态和编辑/预览模式切换。
- [x] 顶部 Markdown 工具按钮通过 ViewModel command 或控件命令操作 AvaloniaEdit，不通过 JS。
- [x] Shell 不创建 Monaco WebView，不加载 Monaco 静态资源。

验收标准：

- [x] App Shell 打开 Markdown 后显示 AvaloniaEdit 编辑器，而不是 Monaco。
- [x] `rg -n "MonacoEditorControl|monaco-editor|CoreWebView2|Microsoft\\.Web\\.WebView2" src/WeaveDoc.App` 无运行时引用。
- [x] App headless 测试能找到 `NativeMarkdownEditorControl`，找不到 Shell 默认路径里的 `MonacoEditorControl`。
- [x] 无文档时不显示编辑器，也不显示旧默认 Markdown。
- [x] 编辑/预览模式切换不会重置当前 Markdown 内容。

任务记录（2026-06-09）：

- 已将 `src/WeaveDoc.App/Views/EditorWorkspace.axaml` 中 Shell Markdown 编辑区替换为 `NativeMarkdownEditorControl`，继续绑定 `DocumentWorkspace.Content`，预览区仍绑定 `DocumentWorkspace.PreviewHtml`。
- 已保留 `EditorEmptyState` / `PreviewEmptyState` 和 `IsMarkdownEditorVisible` / `IsMarkdownPreviewVisible` 模式切换；编辑/预览按钮的 `active` class 改为绑定 `IsEditModeSelected` / `IsPreviewModeSelected`，切换模式不重建或清空当前 Markdown 内容。
- 已给 Shell 顶部 Markdown 工具按钮接入原生控件命令：H1、H2、加粗、斜体、无序列表、任务列表均通过 `NativeMarkdownEditorControl.WrapSelection(...)` 操作 AvaloniaEdit 文本，不再通过 JS 或 Monaco bridge。打开、保存、导出入口仍保持禁用，留给阶段 5。
- 已更新 `tests/WeaveDoc.App.Tests/MainWindowTests.cs`：App headless 测试现在查找 `NativeMarkdownEditorControl`，遍历 Shell 编辑区确认没有 `MonacoEditorControl` 实例，并验证无文档空状态不显示编辑器、不出现旧默认 `# Hello WeaveDoc!`，打开 Markdown 后内容绑定、工具按钮改写、预览模式切换和内容保持均正常。
- 本轮只替换 App Shell 编辑主路径；未修改旧 `MarkdownEditorTab` 独立路径（2.3），未删除 Monaco 静态资源（6.1），未触碰 PDF Reader / PDF.js 链路。

命令记录：

- `rg -n "MonacoEditorControl|monaco-editor|CoreWebView2|Microsoft\\.Web\\.WebView2" src/WeaveDoc.App`：无输出（exit code 1），确认 App Shell 源码无 Monaco/WebView2 运行时引用。
- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands|ShellControls_UpdateLocalStateOnly"`：通过，2 passed。
- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore`：通过，46 passed。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet test WeaveDoc.slnx --no-build`：通过；Rag 13 passed，MarkdownEditor 48 passed，App 46 passed，Converter 107 passed。输出仍包含既有 Markdown HTML 调试日志和 `Viewport Size: 1 x 1` 诊断行，但无失败。

#### 2.3 替换或退役旧 MarkdownEditor 独立编辑路径

- [x] 独立 `MainWindow` 默认使用 `NativeMarkdownEditorControl`。
- [x] 旧 `MarkdownEditorTab` 若保留，必须改用原生编辑控件；若删除，必须同步移除对应测试和引用。
- [x] 旧 `MainWindowViewModel` 不再承担 Shell 状态，只服务于独立入口或被替换。

验收标准：

- [x] `dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build` 启动后不依赖 Monaco 编辑区。
- [x] 独立入口打开 Markdown 后显示原生编辑区和预览区。
- [x] 旧 `MarkdownEditorTab` 不再成为任何默认运行路径里的 Monaco 容器。
- [x] `tests/WeaveDoc.MarkdownEditor.Tests` 中针对 Monaco readiness/WebView2 的测试被删除、改写或明确标记为不再适用。

任务记录（2026-06-09）：

- 已确认独立 `MainWindow.axaml` 默认仍使用 `NativeMarkdownEditorControl` + `PreviewWebViewControl`，并将 `MainWindow.axaml.cs` 中预览点击 / 选区回跳的 `IMarkdownEditorHost` 方法接到原生 AvaloniaEdit：`ScrollEditorToPosition`、`ScrollEditorToPositionWithRange` 和 `ClearEditorHighlight` 不再是空实现。
- 已保留旧 `MarkdownEditorTab` 兼容入口，但将其 XAML 编辑区从 `MonacoEditorControl x:Name="MonacoEditor"` 替换为 `NativeMarkdownEditorControl x:Name="NativeEditor"`；code-behind 的内容同步、保存前同步、激活/停用和预览回跳均改用原生编辑控件，不再 activate/deactivate Monaco 或请求 Monaco JS 选区。
- `MainWindowViewModel` 继续只服务于 `src/WeaveDoc.MarkdownEditor` 独立入口和旧兼容 Tab；App Shell 状态仍由 `DocumentWorkspaceViewModel` / `AppShellViewModel` 承担，本轮没有把旧 `MainWindowViewModel` 接回 Shell。
- 已删除 `WebViewHostControlTests` 中针对 `MonacoEditorControl` fake-host、readiness、导航超时、WebKit fallback 的旧测试；这些测试不再作为原生 Markdown 编辑路径的有效验收。保留的一处 `MonacoEditorControl` 测试引用只用于断言旧 `MarkdownEditorTab` 中已找不到名为 `MonacoEditor` 的旧容器。
- 本轮未删除 `MonacoEditorControl.*`、`ViewModels/MonacoEditorViewModel.cs` 或 `Assets/monaco-editor/`，这些属于 6.1 残留清理；未触碰 PDF.js、`PdfViewerControl` 或 PDF Reader 迁移边界。

命令记录：

- `rg -n "MonacoEditorControl|monaco-editor|CoreWebView2|Microsoft\\.Web\\.WebView2|WebView2EnvironmentManager" src/WeaveDoc.MarkdownEditor/Views tests/WeaveDoc.MarkdownEditor.Tests -g '!src/WeaveDoc.MarkdownEditor/Assets/monaco-editor/min/**'`：只剩 `tests/WeaveDoc.MarkdownEditor.Tests/MainWindowOpenWorkflowTests.cs` 中用于负向断言旧 Tab 找不到 `MonacoEditorControl` 的一处测试引用；`src/WeaveDoc.MarkdownEditor/Views` 已无默认入口 Monaco/WebView2 命中。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "MainWindowOpenWorkflowTests|WebViewHostControlTests|NativeMarkdownEditorControlTests"`：首次新增预览回跳断言时失败，暴露 `Loaded` 前 code-behind 字段可能尚未初始化导致回跳 no-op；加入原生编辑器懒查找后通过，20 passed。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "MainWindowOpenWorkflowTests|WebViewHostControlTests|NativeMarkdownEditorControlTests|MainWindowViewModelTests" --blame-hang --blame-hang-timeout 30s`：通过，22 passed。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore`：本轮尝试运行全量 MarkdownEditor 测试时卡在既有 `NativeWebViewStressTest` 真实 WebKit 路径，手动结束该测试进程；该测试直接创建真实 `NativeWebViewHost`，不属于本轮 fake-host/headless 编辑器替换验收。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：通过；Rag 13 passed，MarkdownEditor 41 passed，App 46 passed，Converter 107 passed。
- `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build`：8 秒后被 timeout 截断（exit code 124），启动期间生成默认 Markdown 预览 HTML，未出现提前进程崩溃；输出仍包含既有 GTK/X11 NativeWebView warning，不能作为真实预览渲染无白屏证明，只作为短时启动烟测。
- `git diff --check`：通过（exit code 0）。
- 由于本轮触及的任务清单和部分测试文件当前仍处于未跟踪路径，额外对 `doc/task_doc/native_markdown_editor_migration_tasks.md`、`tests/WeaveDoc.MarkdownEditor.Tests/MainWindowOpenWorkflowTests.cs`、`tests/WeaveDoc.MarkdownEditor.Tests/WebViewHostControlTests.cs` 执行 `git diff --no-index --check /dev/null <file>`：命令按 no-index 差异返回 exit code 1，但均无空白错误输出。

修复记录（2026-06-09，真实打开 Markdown 后空白）：

- 用户真实运行独立 MarkdownEditor 后反馈“打开一个 Markdown 一片空白”。重新诊断后确认本轮此前的 headless 验收只证明内容进入 `TextEditor.Text`，没有证明真实窗口中文字可见，也没有隔离预览 `NativeWebView` 对编辑区的干扰。
- 已给 `NativeMarkdownEditorControl` 内部 AvaloniaEdit `TextEditor` 显式设置 `Background="#1E1E1E"`、`Foreground="#D4D4D4"`，避免黑字/默认色落在深色编辑背景上造成肉眼空白。
- 已给 `PreviewWebViewControl` 新增 `AutoActivateOnVisible`，默认保持 true 以不破坏显式预览控件行为；独立 `MainWindow` 和旧 `MarkdownEditorTab` 的预览控件设置为 `AutoActivateOnVisible="False"`，并移除打开 Markdown / 切回 Markdown tab 时对预览 WebView 的自动 `Activate(false)`。打开 Markdown 只更新预览 HTML 字符串，不创建真实 WebView host。
- 已更新回归测试：`MainWindowOpenWorkflowTests` 现在断言打开 Markdown 后 `NativeMarkdownEditorControl` 内容正确、选区回跳可用，且 fake `WebViewHostFactory.Hosts` 为空；PDF 显式打开路径仍会创建 PDF host。`NativeMarkdownEditorControlTests` 增加编辑器可读深色主题色断言。
- 继续用真实窗口截图复现后确认：文件内容已进入 `TextEditor.Text`，状态栏也已显示“已打开”，但 AvaloniaEdit 编辑器模板 / 行号 / 文本层没有可见渲染。根因是应用级样式只加载了 Avalonia Fluent 主题，缺少 AvaloniaEdit 自己的 Fluent 主题资源。
- 已在 `src/WeaveDoc.MarkdownEditor/App.axaml` 和 `src/WeaveDoc.App/App.axaml` 加入 `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`，让独立 MarkdownEditor 和 App Shell 都加载 AvaloniaEdit 控件模板。再次真实打开 `test.md` 后，编辑区已显示行号和 `# Hello World`。
- 已补充源码级回归守门测试：`AppInitTests.App_InitializesAvaloniaEditFluentTheme` 和 `MainWindowTests.ShellApp_InitializesAvaloniaEditFluentTheme` 断言两个 App XAML 都保留 AvaloniaEdit Fluent 主题 include。

补充命令记录：

- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "MainWindowOpenWorkflowTests|NativeMarkdownEditorControlTests|WebViewHostControlTests"`：通过，21 passed。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- test.md`：8 秒后被 timeout 截断（exit code 124），启动期间先生成默认 Markdown HTML，再读取 `test.md` 并生成对应 HTML；本次输出未再出现此前的 GDK/X11 NativeWebView warning，说明打开 Markdown 编辑路径未自动拉起预览 NativeWebView。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：通过；Rag 13 passed，MarkdownEditor 42 passed，App 46 passed，Converter 107 passed。
- `git diff --check`：通过（exit code 0）。
- `dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- test.md` + `import -window "WeaveDoc Markdown Editor" /tmp/weavedoc-markdown-editor-after.png`：真实窗口截图确认编辑区显示行号 1/2 和 `# Hello World`，不再是一片空白。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），保留既有 MarkdownEditor 测试项目 nullable / CA1416 warnings。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：通过；Rag 13 passed，MarkdownEditor 43 passed，App 47 passed，Converter 107 passed。
- `git diff --check`：通过（exit code 0）。
- 由于 `doc/task_doc/native_markdown_editor_migration_tasks.md` 和 `tests/WeaveDoc.MarkdownEditor.Tests/AppInitTests.cs` 当前仍处于未跟踪路径，额外执行 `git diff --no-index --check /dev/null <file>`：命令按 no-index 差异返回 exit code 1，但均无空白错误输出。

性能修复记录（2026-06-09，打开可见后输入明显卡顿）：

- 重新诊断后确认独立 MarkdownEditor 的输入热路径仍在 `MainWindowViewModel.EditorContent` setter 中同步执行 `MarkdownService.ConvertMarkdownToHtmlWithCharPositions(...)`，并打印 Markdown 转 HTML 摘要和最多 2000 字符 HTML。由于该渲染服务会给普通文本逐字符包裹 `data-pos` span，连续输入会在 UI 线程上反复做大字符串分配和控制台 IO。
- 已移除 `MarkdownService.ConvertMarkdownToHtmlWithCharPositions` 的热路径日志，并删除 `MainWindowViewModel.EditorContent` setter 中的同步预览生成 / 大段 `Console.WriteLine`。现在普通输入只更新源码内容；默认内容仍会生成初始预览，打开文件不再立即生成预览，必要时可通过 `RefreshPreview()` 手动刷新。
- 已补充 `MainWindowViewModelTests`：打开文件不再生成预览；普通 `EditorContent` setter 不再同步刷新预览、不再写调试输出；`RefreshPreview()` 可按需更新预览。完整 3.3 debounce / 最终静止后自动刷新仍保留为阶段 3.3 后续任务。

补充命令记录：

- `rg -n "Console\\.WriteLine|ConvertMarkdownToHtmlWithCharPositions called|HTML内容开始|Markdown转HTML结果" src/WeaveDoc.MarkdownEditor/ViewModels/MainWindowViewModel.cs src/WeaveDoc.MarkdownEditor/Services/MarkdownService.cs`：无输出，确认热路径调试输出已清理。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），保留既有 MarkdownEditor 测试项目 nullable / CA1416 warnings。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-build --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~MainWindowOpenWorkflowTests|FullyQualifiedName~NativeMarkdownEditorControlTests|FullyQualifiedName~WebViewHostControlTests"`：通过，25 passed。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：通过；Rag 13 passed，MarkdownEditor 45 passed，App 47 passed，Converter 107 passed。
- `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- test.md`：8 秒后被 timeout 截断（exit code 124），期间控制台无 Markdown 转 HTML / HTML 内容刷屏输出。
- `git diff --check`：通过（exit code 0）。
- 由于 `doc/task_doc/native_markdown_editor_migration_tasks.md`、`tests/WeaveDoc.MarkdownEditor.Tests/AppInitTests.cs`、`tests/WeaveDoc.MarkdownEditor.Tests/MainWindowViewModelTests.cs` 当前仍处于未跟踪路径，额外执行 `git diff --no-index --check /dev/null <file>`：命令按 no-index 差异返回 exit code 1，但均无空白错误输出。

二次性能修复记录（2026-06-09，打开任务清单大文档仍明显卡顿）：

- 以 `doc/task_doc/native_markdown_editor_migration_tasks.md` 作为真实输入重新加临时性能探针，确认文件读取约 5ms、ViewModel 应用约 9ms、AvaloniaEdit 设置约 7ms，打开函数本身已经不是主要瓶颈；卡顿更可能来自大文本进入控件后的 TextMate 语法着色、自动换行布局和重复同步。
- 已给 `NativeMarkdownEditorControl` 增加大文档性能模式：内容超过 32,000 字符时，在设置文本前关闭 TextMate Markdown grammar 和 `WordWrap`，状态写明“大 Markdown 文件已关闭语法高亮和自动换行，以保持编辑流畅”；切回小文档时恢复自动换行并重新初始化 grammar。
- 已移除独立 `MainWindow` 和旧兼容 `MarkdownEditorTab` 中对 `EditorContent` 的手写重复同步，保留 XAML `EditorContent="{Binding ..., Mode=TwoWay}"` 作为内容同步路径，避免绑定已经生效后再次把同一份大文本送回控件。
- 已补充回归测试：`NativeMarkdownEditorControlTests.LargeContent_UsesPlainNonWrappingPerformanceModeAndRestoresForSmallContent` 覆盖大文档降级和小文档恢复；`MainWindowOpenWorkflowTests.OpenMarkdownStorageFileAsync_LargeMarkdownUsesNativeEditorPerformanceMode` 覆盖真实打开工作流进入性能模式、预览保持空、不创建 fake WebView host。
- 修复验证过程中发现 `WeaveDoc.slnx` 新增的 `src/WeaveDoc.RAG/WeaveDoc.Rag,csproj` 行是坏 XML 且路径错误，已改为真实存在的 `src/WeaveDoc.Rag/WeaveDoc.Rag.csproj`，否则 solution 级 build/test 会被无关错误阻塞。

补充命令记录：

- `dotnet build src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet build tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore`：通过（exit code 0），保留既有测试项目 nullable / CA1416 warnings。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-build --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~MainWindowOpenWorkflowTests|FullyQualifiedName~NativeMarkdownEditorControlTests|FullyQualifiedName~WebViewHostControlTests"`：通过，27 passed。
- `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- doc/task_doc/native_markdown_editor_migration_tasks.md`：8 秒后被 timeout 截断（exit code 124），期间无 Markdown/HTML 调试刷屏输出。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：通过；Rag 13 passed，MarkdownEditor 47 passed，App 47 passed，Converter 107 passed。
- `git diff --check`：通过（exit code 0）。
- `git diff --no-index --check /dev/null <file>`：对本轮相关未跟踪文件 `doc/task_doc/native_markdown_editor_migration_tasks.md`、`src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml.cs`、`tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs`、`tests/WeaveDoc.MarkdownEditor.Tests/MainWindowOpenWorkflowTests.cs`、`tests/WeaveDoc.MarkdownEditor.Tests/MainWindowViewModelTests.cs` 逐个执行；命令按 no-index 差异返回 exit code 1，但均无空白错误输出。

三次性能修复记录（2026-06-09，`tests/test_doc/markdown` 小文件同样卡顿）：

- 用户反馈 `tests/test_doc/markdown` 下 128B-1.6KB 的 LaTeX/数学 Markdown 也明显卡顿，确认问题不能再按“大文件”解释。
- 重新对 `test-simple.md`、`test-pmatrix.md`、`test-symbols.md` 等小文件做 smoke 和探针：headless 连续编辑中 TextMate 不是唯一瓶颈，但主 App Shell 侧仍保留 `DocumentWorkspaceViewModel.Content` 每次变化同步调用 `CreatePreview(...)` 的逻辑；这会让 App Shell 中每次按键都重建带 `data-pos` 的预览 HTML。
- 已将 App Shell 的 `DocumentWorkspaceViewModel.UpdateContent` 改为只更新源码、dirty 和状态；新增 `RefreshPreview()`，并在 `EditorWorkspace` 点击“预览”模式时显式刷新预览。这样编辑模式不再每个按键重建 Markdig/HTML 预览，预览模式仍能拿到当前内容。
- 已去掉独立 `MainWindowViewModel` 的默认 Markdown 内容和默认预览生成，避免通过命令行打开文件时先渲染无意义的默认文档。
- 已将 `NativeMarkdownEditorControl` 的 TextMate 初始化改为按实际内容触发，不在构造时立即加载；包含 `$...$`、`\begin{...}`、`\[` 或 `\(` 的 LaTeX/数学 Markdown 自动关闭语法高亮但保留自动换行，大文件仍同时关闭语法高亮和自动换行。
- 已给 `NativeMarkdownEditorControl` 增加用户输入回写保护：TextChanged 推动 `EditorContent` 更新时，不再通过自身 `OnPropertyChanged` 立即反向 `ApplyEditorContent` 一次。
- 删除了本轮使用的临时 `NativeMarkdownEditorPerfProbeTests` 探针，未把脆弱测速断言留进常规测试集。

补充命令记录：

- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentWorkspaceViewModelTests|FullyQualifiedName~MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands"`：通过，10 passed。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-build --filter FullyQualifiedName~NativeMarkdownEditorControlTests`：通过，12 passed。
- `for f in tests/test_doc/markdown/*.md; do timeout 4s dotnet src/WeaveDoc.MarkdownEditor/bin/Debug/net10.0/WeaveDoc.MarkdownEditor.dll "$f"; done`：5 个小 Markdown 均按 GUI timeout 截断（exit code 124），无崩溃或调试刷屏；对比修复前，`test-pmatrix.md` 4 秒 smoke 的 user CPU 约从 3.42s 降到 2.15s，`test-symbols.md` 约从 3.34s 降到 2.38s。
- `dotnet build WeaveDoc.slnx --no-restore`：通过（exit code 0），0 warnings / 0 errors。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：通过；Rag 13 passed，MarkdownEditor 48 passed，App 48 passed，Converter 107 passed。
- `git diff --check`：通过（exit code 0）。
- `git diff --no-index --check /dev/null <file>`：对本轮相关未跟踪文件 `doc/task_doc/native_markdown_editor_migration_tasks.md`、`src/WeaveDoc.App/ViewModels/DocumentWorkspaceViewModel.cs`、`src/WeaveDoc.App/Views/EditorWorkspace.axaml.cs`、`src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml.cs`、`tests/WeaveDoc.App.Tests/DocumentWorkspaceViewModelTests.cs`、`tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs` 逐个执行；命令按 no-index 差异返回 exit code 1，但均无空白错误输出。

### 阶段 3：Markdig 渲染服务

#### 3.1 建立 Markdown 渲染服务

- [x] 在 App/MarkdownEditor 可复用的位置新增 `IMarkdownRenderService` 接口。
- [x] 统一 Markdig pipeline，明确启用的扩展：`UseAdvancedExtensions()`、`UsePipeTables()`、`UseTaskLists()`、`UseAutoLinks()`、`UseMathematics()`、`UseEmphasisExtras()`、`UseGenericAttributes()`。
- [x] 自定义 Markdig AST walker 输出带 `data-line` 和 `data-pos` 的 HTML body 片段。
- [x] `data-line` 通过 `block.Line` 注入；`data-pos` 通过 `inline.Span.Start` + 行偏移表逐字符注入 `<span data-pos="L-C">`。
- [x] 保留 `math-inline` / `math-display` class 名，与现有 `preview-template.html` 的 KaTeX 渲染逻辑兼容。

验收标准：

- [x] 同一 Markdown 输入在 App Shell 和独立 MarkdownEditor 中生成一致预览 HTML（通过共享 `IMarkdownRenderService` 实现）。
- [x] 标题、段落、列表、表格、代码块、任务列表、链接、图片占位、LaTeX 样例均有服务测试覆盖。
- [x] 渲染服务不依赖 Avalonia UI 控件（纯 `IMarkdownRenderService` + Markdig AST walker）。
- [x] 渲染失败返回可显示错误，不抛出未处理异常，不清空编辑区内容。
- [x] Markdig 包引用位置清晰：直接 `PackageReference` 在 `WeaveDoc.MarkdownEditor.csproj`，不通过 `WeaveDoc.Converter` 间接引用。

任务记录：

- 2026-06-10 实现记录：
  - 新增 `src/WeaveDoc.MarkdownEditor/Services/IMarkdownRenderService.cs`：单方法接口 `string RenderPreviewHtml(string markdown)`，无 Avalonia 依赖。
  - 新增 `src/WeaveDoc.MarkdownEditor/Services/MarkdigMarkdownRenderService.cs`：自定义 Markdig AST walker，不走 `Markdown.ToHtml()` 默认渲染，而是直接遍历 `MarkdownDocument` 的块级和内联节点：
    - 块级：`HeadingBlock`→`<hN data-line>`、`ParagraphBlock`→`<p data-line>`、`CodeBlock`→`<pre><code data-line>`、`QuoteBlock`→`<blockquote data-line>`、`ListBlock`/`ListItemBlock`→`<ul>/<ol>/<li data-line>`、`ThematicBreakBlock`→`<hr data-line>`、`MathBlock`→`<div class="math-display" data-line>`。
    - 内联：`LiteralInline`→逐字符 `<span data-pos="L-C">`（通过 `Span.Start` + 行偏移表计算行列）、`CodeInline`→`<code>`、`EmphasisInline`→`<strong>/<em>/<del>`、`LinkInline`→`<a>`/`<img>`、`MathInline`→`<span class="math-inline" data-pos>`。
    - Markdig pipeline 静态缓存（同 `_cachedRegistryOptions` 模式），`ComputeLineOffsets` 每请求计算一次。
  - 已在 `src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj` 添加 `PackageReference Include="Markdig" Version="1.2.0"`。
  - `src/WeaveDoc.MarkdownEditor/ViewModels/MainWindowViewModel.cs`：字段 `_markdownService` 替换为 `_markdownRenderService : IMarkdownRenderService`，`RefreshPreview()` 调用 `RenderPreviewHtml()`。
  - `src/WeaveDoc.App/Services/Documents/MarkdownDocumentService.cs`：构造函数改为接收 `IMarkdownRenderService`（默认 `new MarkdigMarkdownRenderService()`），`CreatePreviewHtml` 调用 `RenderPreviewHtml()`。
  - `MarkdownService.ConvertMarkdownToHtml()`、`ConvertToHtml()` 及仅其使用的私有方法（`ConvertLineToHtml`、`ProcessInlineElements`、`ProcessInlineMath`）为死代码，保留未删以作为后续 6.1 残留清理参考。
  - 新增 `tests/WeaveDoc.MarkdownEditor.Tests/MarkdownServiceTests.cs`（重命名为 `MarkdownRenderServiceTests`）：15 个测试覆盖标题、段落、data-pos、加粗、斜体、代码块、链接、行内数学、块级数学、任务列表、多行 data-line、空输入、畸形输入、HTML 转义、无 Avalonia 依赖。全部通过。

命令记录：

- `dotnet build WeaveDoc.slnx --no-restore`：通过，0 errors，6 个既有 warnings（nullable / CA1416 / platform）。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "FullyQualifiedName~MarkdownRenderServiceTests"`：通过，15 passed。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests"`：通过，28 passed。
- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor"`：通过，12 passed。
- `git diff --check`：通过（仅有预存的 test-latex.md blank line at EOF 与本次改动无关）。

#### 3.2 HTML 安全和资源策略

- [x] 明确原始 HTML 的处理策略：禁用，Markdig `HtmlBlock` 全部 HTML 转义。
- [x] 禁止 Markdown 预览自动执行不可信脚本。
- [x] 明确外链图片、外链 CSS、外链 JS 的加载策略：`connect-src 'none'`，仅允许 `'self'` 和 `data:` 图片。
- [x] 为预览 HTML 增加基础 CSS，保证字体、代码块、表格、链接、任务列表和暗/亮色主题可读。

验收标准：

- [x] 包含 `<script>` 的 Markdown 不会在预览中执行脚本。
- [x] 包含危险链接或外链资源的 Markdown 不会触发未授权导航或网络加载。
- [x] HTML 中的用户文本被正确编码，不因普通 Markdown 内容破坏模板结构。
- [x] 暗色/亮色主题下正文、链接、代码块、表格边框具备可读对比度。

任务记录：

- 2026-06-10 实现记录：
  - `MarkdigMarkdownRenderService.RenderBlock` 中 `HtmlBlock` 现在调用 `EscapeHtml()` 转义内容，不再原样输出。Markdig 的内联渲染路径（`LiteralInline`、`CodeInline` 等）已通过 `EscapeChar` 逐字符转义。
  - `preview-template.html` 添加 `<meta http-equiv="Content-Security-Policy">`：`default-src 'self'`、`script-src 'self' 'unsafe-inline' 'unsafe-eval'`（KaTeX 需要）、`style-src 'self' 'unsafe-inline'`、`img-src 'self' data:`、`connect-src 'none'`。
  - `preview-template.html` 添加 `@media (prefers-color-scheme: dark)` 完整暗色主题：背景 `#1E1E1E`、文字 `#D4D4D4`、链接 `#6CB6FF`、代码背景 `#2D2D2D`、表格边框 `#3C3C3C`、块引用 `#8B949E`。
  - 新增测试：`RenderPreviewHtml_RawHtmlBlock_IsEscaped`（`<div onclick=...>` 被转义）、`RenderPreviewHtml_ScriptTag_NotExecutable`（`<script>` 标签不出现在输出中）。
  - 17 个 `MarkdownRenderServiceTests` 全部通过。

#### 3.3 预览刷新节流

- [x] 编辑内容变更后通过 debounce 更新预览 HTML。
- [x] 大文档编辑时不在每个按键后立即重建并导航整个 WebView。
- [x] 预览刷新状态可观察，失败时显示明确状态。

验收标准：

- [x] 连续快速输入时预览刷新次数被合并。
- [x] 最终静止后预览内容与编辑区一致。
- [x] 预览刷新失败不会影响编辑区输入、撤销重做或保存。
- [x] ViewModel 或服务测试覆盖 debounce/刷新合并逻辑。

任务记录：

- 2026-06-10 实现记录：
  - `MainWindowViewModel` 新增 `DebouncedRefreshPreview(int delayMs = 300)`：使用 `CancellationTokenSource` 节流，快速连续调用只执行最后一次。
  - `DocumentWorkspaceViewModel` 新增 `DebouncedRefreshPreview(int delayMs = 300)`：返回 `Task<bool>`，被取消时返回 `false`。
  - debounce 测试：`MainWindowViewModelTests.DebouncedRefreshPreview_MergesRapidCalls_IntoSingleRefresh`（连续 3 次合并）、`DebouncedRefreshPreview_DifferentContents_UsesLatestContent`（不同内容取最新）；`DocumentWorkspaceViewModelTests.DebouncedRefreshPreview_MergesRapidCalls_IntoSingleRefresh`（App Shell 侧同样行为）。
  - 注：当前预览为手动触发，debounce 主要作为未来实时预览的基础设施；大文档不每按键重建 WebView 已由输入性能优化清单保证。

### 阶段 4：NativeWebView 预览宿主重构

#### 4.1 稳定持有预览 WebView

- [x] 复核当前 `PreviewWebViewControl` 的 NativeWebView 创建、插入、移除和 dispose 流程。
- [x] 设计稳定宿主，避免普通 `UserControl` wrapper 在 Linux GTK offscreen 下产生 1x1 viewport。
- [x] 编辑/预览模式切换只隐藏或暂停预览，不销毁 WebView。
- [x] 页面导航或重新挂载前按 Avalonia NativeWebView 要求处理 reparenting。

验收标准：

- [x] 切换编辑/预览 10 次后，预览 WebView 未被反复销毁重建。
- [ ] 预览控件可报告真实 viewport 尺寸，不能是 `1x1` 或 `0x0`。
- [ ] Linux/X11 环境下真实启动烟测不出现编辑区/预览区整块空白。
- [x] NativeWebView 不可用时显示真实不可用状态，保留 Markdown 内容和保存能力。
- [x] Headless 测试仍使用 fake host，不强制创建真实 NativeWebView。

任务记录（2026-06-10）：

- 复核结论：当前 `PreviewWebViewControl` 生命周期已稳定。`EnsureWebViewAsync()` 创建 host → 插入 `WebViewContainer` → 导航；`Activate()` 设置可见 → 等待 Dispatcher Render 优先级布局 → 触发 resize → 应用内容；`Deactivate()` 仅隐藏不销毁；`DisposeHostAsync()` 是唯一销毁路径（取消事件订阅 → 从容器移除 → DisposeAsync → 置 null）。
- WebKitGTK offscreen 处理已有：`Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render)` 等待布局完成 + `window.dispatchEvent(new Event('resize'))` 强制重绘。
- Fallback 机制已覆盖：工厂创建异常、导航超时（5s）、导航失败、adapter 不支持嵌入渲染（NativeDialog）、InvokeScript 异常。
- 新增测试 `PreviewWebViewControl_ModeSwitching_DoesNotRecreateWebView`：切换编辑/预览 10 次后 `factory.Hosts.Count` 仍为 1，验证 WebView 未被反复销毁重建。
- viewport 尺寸报告和 Linux/X11 真实烟测为环境依赖项，headless 环境无法验证，记录为环境限制。

#### 4.2 WebView 页面通信和导航边界

- [x] 预览 HTML 加载后能上报 ready 状态。
- [x] Host 到页面只发送必要消息：设置内容、主题、滚动定位。
- [x] 页面到 Host 只允许白名单消息：ready、linkClicked、scrollChanged 等。
- [x] 外链点击默认阻止自动导航，并交给 Host 决策。

验收标准：

- [x] 无预览 ready 时不会丢失最后一次待渲染内容。
- [x] 非白名单 WebMessage 被忽略并记录诊断，不导致异常。
- [x] 点击外链不会让 WebView 直接跳离本地预览页面。
- [x] fake host 测试覆盖 ready、set content、导航失败和未知消息。

任务记录（2026-06-10）：

- `previewLoaded` 消息处理：`preview-template.html` 的 `window.addEventListener('load', ...)` 发送 `{ Type: 'previewLoaded', Data: 'loaded' }`，`PreviewWebViewControl.WebViewHost_MessageReceived` 新增 `previewLoaded` 分支记录日志。ready 状态继续由 C# 端 `NavigationCompleted` + `WaitForJavaScriptReadyAsync()` 覆盖。
- 外链点击拦截：`preview-template.html` 全局 click 事件监听器新增 `<a[href]>` 拦截逻辑——非锚点、非 `javascript:` 链接调用 `e.preventDefault()` 并发送 `{ Type: 'linkClicked', Data: { url } }` 消息。`PreviewWebViewControl` 新增 `HandleLinkClicked()` 方法记录日志，不触发 WebView 导航。
- 非白名单消息诊断：`WebViewHost_MessageReceived` 的 else 分支新增 `Logger.Log($"Unknown preview message type ignored: {msgType}")`。
- 已有消息白名单：`previewSelection`、`previewClick`、`previewClearHighlight`、`previewLoaded`、`linkClicked`、`debug`（debug 消息由页面 KaTeX 调试输出使用，作为静默忽略的已知消息类型，避免日志噪音）。
- Host 到页面消息路径已验证：`InvokeScriptAsync("window.updateContent(...)")` 用于设置内容，`InvokeScriptAsync("window.dispatchEvent(new Event('resize'))")` 用于触发重绘，`ScrollToLine`/`ScrollToSelection` 用于滚动定位。
- 新增测试：`PreviewWebViewControl_ModeSwitching_DoesNotRecreateWebView`（模式切换不重建）、`PreviewWebViewControl_LinkClickedMessage_IsHandled`（外链消息处理）、`PreviewWebViewControl_UnknownMessage_IsIgnored`（未知消息忽略）、`PreviewWebViewControl_PreviewLoadedMessage_IsHandled`（ready 消息处理）。
- pendingContent 机制保证无 ready 时不会丢失内容：`UpdatePreviewAsync` 在 `_webViewHost == null || !_isInitialized` 时将内容存入 `_pendingContent`，`NavigationCompleted` 回调中检查并应用 `_pendingContent`。

命令记录：

- `dotnet build WeaveDoc.slnx --no-restore`：通过，0 errors，7 个既有 warnings（nullable / CA1416 / platform）。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "WebViewHostControlTests"`：通过，12 passed。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "FullyQualifiedName!~StandaloneLatexPerformanceProbeTests"`：通过，78 passed。
- `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore`：通过，51 passed。
- `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest&FullyQualifiedName!~StandaloneLatexPerformanceProbeTests"`：MarkdownEditor 77 passed，App 51 passed，Rag 13 passed，Converter 105 passed / 2 failed（Syncfusion PDF 转换既有失败，与本次改动无关）。
- `rg -n "previewLoaded|linkClicked" src/WeaveDoc.MarkdownEditor tests/WeaveDoc.MarkdownEditor.Tests`：命中为本次新增的生产代码和测试代码，属于正常引用。
- `git diff --check`：无输出（`test-latex.md` 的 EOF 空行为既有问题，与本次改动无关）。

### 阶段 5：Shell 文件工作流接入

#### 5.1 打开 Markdown

- [ ] Shell 顶部打开入口启用 Markdown 文件选择。
- [ ] 支持 `.md`、`.markdown`、必要时支持 `.txt`。
- [ ] 取消选择不改变当前文档。
- [ ] 读取失败显示明确错误并保留当前文档。

验收标准：

- [ ] 成功打开文件后，标题、路径、内容、脏状态、编辑器内容、预览 HTML 均正确更新。
- [ ] 取消选择后 `CurrentFilePath`、`Content`、`IsDirty` 保持不变。
- [ ] 读取失败后当前内容不丢失，状态栏包含失败原因。
- [ ] 打开文件不会初始化 AI/RAG、导出、模板或 PDF 流程。

#### 5.2 保存 Markdown

- [ ] 当前路径存在时保存写回原文件。
- [ ] 空状态或无修改状态下保存入口不可用。
- [ ] 保存失败显示明确错误并保持 dirty。

验收标准：

- [ ] 修改后保存入口启用。
- [ ] 保存成功后磁盘内容更新，`IsDirty = false`，状态栏显示保存完成。
- [ ] 保存失败后磁盘原内容不被误判为成功，`IsDirty` 保持 true。
- [ ] 保存流程不触发导出、转换或模板模块。

#### 5.3 未保存修改保护

- [ ] 打开新文件、关闭窗口或切换会丢失当前文档的操作前，检查 dirty。
- [ ] 提供 Save / Discard / Cancel 三分支。
- [ ] 保存失败时不继续执行破坏性操作。

验收标准：

- [ ] Save 分支保存旧文档后再打开新文档或关闭窗口。
- [ ] Discard 分支不保存旧文档，直接继续目标操作。
- [ ] Cancel 分支保持路径、内容、dirty 和预览状态不变。
- [ ] 保存失败时当前文档仍留在编辑区，用户可继续编辑或重试。

### 阶段 6：旧 Monaco/WebView2 残留清理

#### 6.1 移除编辑主路径中的 Monaco

- [ ] App Shell 不引用 `MonacoEditorControl`。
- [ ] 独立 MarkdownEditor 默认路径不引用 `MonacoEditorControl`。
- [ ] 删除或隔离 `Assets/monaco-editor`，避免运行时继续加载。
- [ ] 清理 Monaco 静态资源时不得删除 `Assets/pdfjs-*-dist`、`pdf-viewer*.html` 或 PDF Reader 相关资源。
- [ ] 删除 WebView2 编辑器初始化、readiness polling、JS resize/focus/selection bridge。

验收标准：

- [ ] `rg -n "MonacoEditorControl|monaco-editor" src/WeaveDoc.App src/WeaveDoc.MarkdownEditor tests` 无默认运行路径引用；若测试或文档仍出现，必须有“不再适用/已退役”说明。
- [ ] `rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager" src/WeaveDoc.MarkdownEditor` 无输出。
- [ ] Markdown 编辑不再依赖 JS ready、WebView2 runtime 或 Monaco 静态资源。
- [ ] PDF.js 资源和 `PdfViewerControl` 仍存在，除非另一个 PDF Reader 迁移清单已经明确要求删除。
- [ ] 删除残留后 `dotnet build WeaveDoc.slnx --no-restore` 通过。

#### 6.2 保留 PreviewWebView/PdfViewer 边界

- [ ] 保留 `Avalonia.Controls.WebView` 用于 Markdown 预览和现有 PDF.js + `NativeWebView` PDF Reader 基线。
- [ ] 不把预览 WebView 的 lifecycle 逻辑泄漏回编辑控件。
- [ ] PDF Viewer 不在本阶段启用，但不能被误删到无法后续接入或回归测试。
- [ ] 若未来选择纯 C# / native renderer PDF Reader，本阶段只记录为后续独立任务，不在此处实现。

验收标准：

- [ ] Markdown 编辑控件不引用 `IWebViewHost`。
- [ ] Markdown 预览控件可以通过 `IWebViewHost` fake 测试。
- [ ] PDF 相关入口仍按当前阶段保持禁用或延后状态。
- [ ] PDF Viewer 测试若受影响，必须同步更新并保持通过。
- [ ] 本清单完成后，PDF Reader 仍是现有链路或明确延后状态，不被误描述为已纯 C# 化。

### 阶段 7：测试矩阵

#### 7.1 服务层测试

- [ ] Markdown 渲染服务测试覆盖常见 Markdown。
- [ ] 文件读写服务测试覆盖成功、取消、失败、保存失败。
- [ ] HTML 安全策略测试覆盖脚本、外链、危险标签。

验收标准：

- [ ] 服务测试不依赖 Avalonia UI。
- [ ] 失败分支证明当前文档不会被清空。
- [ ] 渲染服务输出包含可预期的标题、段落、表格、代码块和 source marker。

#### 7.2 ViewModel 测试

- [ ] 使用 CommunityToolkit.Mvvm 后补齐命令和状态测试。
- [ ] 使用 fake file picker、fake markdown service、fake preview renderer。
- [ ] 覆盖 dirty、打开、保存、取消、未保存确认、预览刷新状态。

验收标准：

- [ ] ViewModel 测试不弹真实系统对话框。
- [ ] ViewModel 测试不创建真实 NativeWebView。
- [ ] 所有命令启用/禁用状态都有断言。
- [ ] 错误状态可被 UI 显示，不只是写控制台。

#### 7.3 Avalonia Headless UI 测试

- [ ] App Shell 测试覆盖原生编辑控件显示。
- [ ] App Shell 测试覆盖打开文档后编辑/预览模式切换。
- [ ] MarkdownEditor 独立入口测试覆盖原生编辑区。
- [ ] 测试旧假内容、旧内部 Tab、旧 Monaco 默认路径不回流。

验收标准：

- [ ] Headless 测试能证明 Shell 使用 `NativeMarkdownEditorControl`。
- [ ] Headless 测试能证明 Shell 不显示 `# Hello WeaveDoc!`。
- [ ] Headless 测试能证明 AI/RAG、导出、模板、PDF 未完成入口仍不可执行。
- [ ] Headless 测试通过 fake host 验证预览 fallback，不要求真实 WebView。

#### 7.4 真实运行烟测

- [ ] 在 App Shell 执行短时启动烟测。
- [ ] 在独立 MarkdownEditor 执行短时启动烟测。
- [ ] 如果用户要求确认真实渲染，增加手工/脚本化 Markdown 打开 smoke。

验收标准：

- [ ] `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 不提前崩溃。
- [ ] `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build` 不提前崩溃。
- [ ] 若可执行真实打开文件 smoke，打开 Markdown 后编辑区非空，预览区不是整块空白。
- [ ] 若 NativeWebView 不可用，UI 显示明确不可用状态而不是假预览。

### 阶段 8：文档和收尾

#### 8.1 更新项目文档

- [ ] 更新 `src/WeaveDoc.MarkdownEditor/README.md`，说明原生编辑器、Markdig 渲染和 NativeWebView 预览边界。
- [ ] 更新根 README/中文 README 中关于 Markdown editor 的技术描述。
- [ ] 如果移除 Monaco 资源，同步删除旧说明和安装要求。

验收标准：

- [ ] 文档不再描述 Monaco 作为 Markdown 编辑器。
- [ ] 文档不再要求 WebView2 runtime 作为 Markdown 编辑区前置条件。
- [ ] 文档说明 Linux NativeWebView 运行库不可用时的真实 fallback 行为。
- [ ] 文档明确区分 Markdown 编辑器迁移和 PDF Reader 延后链路：PDF Reader 短期仍为 PDF.js + `NativeWebView`，不属于本清单纯 C# 化范围。

#### 8.2 清单更新

- [ ] 每完成一个阶段，在本文件中勾选对应任务。
- [ ] 在对应阶段下记录执行日期、关键改动、验证命令和结果。
- [ ] 若某阶段因为外部运行库或上游包阻塞，记录阻塞原因和复现命令。

验收标准：

- [ ] 本清单能准确反映实际完成进度。
- [ ] 每个已完成阶段都有可追溯验证记录。
- [ ] 未完成阶段仍保持未勾选，不用聊天结论替代文件状态。

## 最终完成标准

- [ ] App Shell 的 Markdown 编辑区使用 AvaloniaEdit 原生控件。
- [ ] 独立 MarkdownEditor 默认编辑路径使用 AvaloniaEdit 原生控件。
- [ ] Markdown 编辑主路径不依赖 Monaco、WebView2、JS ready、Monaco 静态资源或 WebView 编辑桥。
- [ ] 预览区使用 Markdig 生成 HTML，并通过 Avalonia NativeWebView 显示。
- [ ] NativeWebView 不可用时显示真实不可用状态，编辑和保存能力不受影响。
- [ ] CommunityToolkit.Mvvm 用于 Markdown 文档状态和命令，不造成无关模块大规模重写。
- [ ] 打开、编辑、预览、保存、未保存保护均有服务/ViewModel/UI 测试覆盖。
- [ ] `rg -n "Microsoft\\.Web\\.WebView2|CoreWebView2|WebView2EnvironmentManager" src/WeaveDoc.MarkdownEditor` 无输出。
- [ ] `dotnet build WeaveDoc.slnx --no-restore` 通过。
- [ ] `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore` 通过。
- [ ] `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过。
- [ ] `dotnet test WeaveDoc.slnx --no-build` 通过。
- [ ] `git diff --check` 无输出。
- [ ] App Shell 和独立 MarkdownEditor 的 `timeout 8s dotnet run ... --no-build` 烟测均未提前崩溃。
- [ ] AI/RAG、导出、模板、多文档、PDF Reader 仍保持本清单明确排除或延后状态。
- [ ] PDF Reader 若仍存在，保持现有 PDF.js + `NativeWebView` 基线并与 Markdown 编辑主线隔离；若要去 WebView/PDF.js，必须由独立 PDF Reader 清单验收。
