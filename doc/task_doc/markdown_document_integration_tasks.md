# Markdown 文档接口纵切任务清单

## 目标

在当前 Avalonia Shell 中接入第一条真实文档业务纵切：Markdown 文件打开、编辑、实时预览、保存、未保存修改确认和状态反馈。

本清单已按 2026-06-05 合入 `origin/main` 后的代码库状态更新：`WeaveDoc.MarkdownEditor` 现在已有 Monaco 编辑器、`PreviewWebViewControl`、`PdfViewerControl`、PDF.js 资源和 `IMarkdownEditorHost`，因此本轮不再规划“从零做 PreviewHtml 文本占位”，而是规划“新 Shell 文档状态 + 现有 MarkdownEditor 控件能力的受控适配”。

## 当前代码基线

- Shell 当前仍是薄宿主：`MainWindow` 创建 `AppShellViewModel`，承载 `WorkspaceSidebar`、`EditorWorkspace`、`AiAssistantPanel` 和 `ShellStatusBar`。
- `AppShellViewModel` 只保存本地 UI 状态：侧栏标签、编辑/预览模式、主题、AI 面板展开状态；尚无文档路径、内容、脏状态或保存工作流。
- Shell 顶部和编辑区的 `打开` / `保存` 入口仍禁用；`tests/WeaveDoc.App.Tests/MainWindowTests.cs` 仍断言这些入口不可执行。
- `EditorWorkspace` 当前只显示空状态，不承载真实文本编辑器、`PreviewWebViewControl` 或 Markdown 文档内容。
- `WorkspaceSidebar` 当前只显示文档预览骨架空状态；页码、缩放和文档标签入口仍禁用。
- `WeaveDoc.MarkdownEditor.Views.MarkdownEditorTab` 已包含旧内部菜单、`Markdown Editor` / `PDF Reader` 内部 Tab、Monaco、HTML 预览和 PDF Reader，但也带有旧默认内容和旧窗口耦合。
- `WeaveDoc.MarkdownEditor.ViewModels.MainWindowViewModel` 仍初始化 `# Hello WeaveDoc!`，文件读写异常只写控制台，缺少 Shell 需要的状态反馈和未保存修改确认。
- `MonacoEditorControl` 已能通过 `IMarkdownEditorHost` 找宿主，但内容变更仍直接寻找旧 `MainWindowViewModel`，需要改造成 Shell 可控的数据流。
- `PreviewWebViewControl` 和 `PdfViewerControl` 已存在；三类 Web 控件已迁移到 `IWebViewHost` / `NativeWebView` 宿主，`PdfViewerControlTests` 已覆盖 PDF.js URL、兼容脚本、打开脚本和文本选择样式。
- `DocumentConversionEngine` 和 PDF 转换链已存在，但本清单的 Markdown 文档纵切不接导出、模板或转换流程。

## 已确认决策

- 纵切主线：从 Shell 骨架切换到真实 Markdown 文档接口。
- 首个模块：Markdown 文档打开、编辑、预览、保存和状态反馈。
- 接入方式：采用 Shell 原生文档状态，不整块嵌入旧 `MarkdownEditorTab`。
- 控件复用：优先复用 `MonacoEditorControl`、`PreviewWebViewControl`、`MarkdownService` 和必要的 host 接口；不复用旧 `MainWindowViewModel` 作为 Shell 文档状态。
- 预览深度：运行时接入真实 HTML 预览控件；自动化测试以 ViewModel/服务层 `PreviewHtml`、可见状态和 fake `IWebViewHost` 为主，不要求 headless 测试启动真实原生 WebView。
- Web 宿主前置：阶段 3 及之后的 Shell 文档工作流以跨平台 `IWebViewHost` / `NativeWebView` 迁移完成为前置条件，不再接受“WebView2 fallback 可用”作为跨平台验收标准。
- 文档模型：单文档；打开新文件会替换当前文档。
- 未保存修改：打开其他文件或关闭窗口前弹确认，固定为保存、放弃、取消三种结果。
- PDF Reader：代码库已有可用基础，但从 Markdown 主线拆为独立延后阶段；未完成 PDF 阶段前，Shell PDF 入口继续禁用。
- 任务记录：本文件作为当前 Markdown 文档接入纵切清单；不继续往已完成的 `avalonia_frontend_refactor_tasks.md` 追加新阶段。

## 明确排除

- 不把旧 `MarkdownEditorTab` 整块塞进新 Shell。
- 不复用旧 `WeaveDoc.MarkdownEditor.ViewModels.MainWindowViewModel` 作为新 Shell 的文档状态源。
- 不恢复旧默认 Markdown 内容 `# Hello WeaveDoc!`。
- 不接入文档导出、模板管理、转换引擎或输出路径选择。
- 不接入 AI/RAG、索引、模型初始化、云 API 设置或问答发送。
- 不实现多文档标签页、最近文件列表、自动保存或跨启动恢复。
- 不修改 `doc/软件设计/界面设计_demo`，不处理其未跟踪文件。

## 执行任务

### 阶段 0：任务文档与基线锁定

#### 0.1 更新任务清单

- [x] 将 `doc/task_doc/markdown_document_integration_tasks.md` 更新为合入 `origin/main` 后的代码库基线。
- [x] 明确记录 Shell、MarkdownEditor、PDF.js、Converter 和现有测试状态。
- [x] 将 PDF 从“完全排除”调整为“独立延后阶段”。

验收标准：

- [x] 文档说明当前 Shell 仍未接入真实 Markdown 文档状态。
- [x] 文档说明现有 MarkdownEditor 控件能力可复用，但旧 Tab / 旧 ViewModel 不能整块回流。
- [x] 未修改已完成的 `doc/task_doc/avalonia_frontend_refactor_tasks.md`。

#### 0.2 确认当前实现影响面

- [x] 复核 `AppShellViewModel`、`MainWindow`、`EditorWorkspace`、`WorkspaceSidebar`、`ShellStatusBar` 和 App shell 测试。
- [x] 复核 `MarkdownEditorTab`、`MainWindowViewModel`、`MonacoEditorControl`、`PreviewWebViewControl`、`PdfViewerControl`、`MarkdownService` 和 MarkdownEditor 测试。
- [x] 写明本纵切主要影响面：Shell 文档状态、Markdown 服务适配、编辑/预览控件数据流、打开/保存/未保存确认、Shell UI 状态同步。

验收标准：

- [x] 本清单可直接指导后续实现，不再以旧代码库假设为前提。
- [x] 本轮文档更新不要求改业务代码。

### 阶段 1：文档状态与服务层

#### 1.1 定义 Shell 文档接口与类型

- [x] 新增 `IMarkdownDocumentService`，负责读取、保存和生成 Markdown 预览数据。
- [x] 新增文件选择服务接口，生产环境封装 Avalonia `StorageProvider`，测试环境使用 fake 实现。
- [x] 新增未保存修改确认接口和 `UnsavedChangesDecision` 枚举，枚举值固定为 `Save`、`Discard`、`Cancel`。
- [x] 新增文档结果类型，携带内容、路径、显示名、预览 HTML、错误消息和成功状态。

验收标准：

- [x] 接口层不直接依赖具体 Avalonia 控件实例。
- [x] fake 文件选择和 fake 确认服务可以在 ViewModel 测试中替换真实 UI。
- [x] `UnsavedChangesDecision` 的三个分支都有命名清晰的测试入口。
- [x] 读取/保存失败能返回 UI 可显示错误，不抛出未处理异常。

#### 1.2 实现 Markdown 文档服务

- [x] 实现 Markdown 文件读取，支持 `.md`、`.markdown`、`.txt`。
- [x] 实现 Markdown 文件保存，写入当前路径。
- [x] 复用现有 `WeaveDoc.MarkdownEditor.Services.MarkdownService` 生成 `PreviewHtml`。
- [x] 保留 `MarkdownService` 的 LaTeX、行号和字符定位能力，不把它降级成简单 HTML 文本转换。
- [x] 对不存在路径、读取失败、保存失败返回可显示状态，不清空当前文档。

验收标准：

- [x] 读取成功后返回完整文件内容、路径和文件名。
- [x] 保存成功后磁盘文件内容与传入 `Content` 完全一致。
- [x] `# 标题` 输入至少生成包含标题结构的 `PreviewHtml`。
- [x] 含 LaTeX 的 Markdown 继续生成现有 `math-inline` / `math-display` 相关 HTML。
- [x] 读取或保存失败不会清空当前文档状态。

执行记录（2026-06-05）：

- 新增 `MarkdownDocumentService`，保持 1.1 接口不变，提供 Markdown 文件读写和预览生成。
- 预览生成调用 `ConvertMarkdownToHtmlWithCharPositions`，保留 `data-line`、字符定位和 LaTeX 标记。
- 读取失败返回失败结果和可显示错误；保存失败结果保留传入内容、路径和当前预览，后续 ViewModel 负责不替换当前文档状态。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter MarkdownDocument` 通过，14 passed。
- 验证：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过，8 passed。
- 验证：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warning / 0 error。
- 验证：`git diff --check` 无输出。

#### 1.3 建立 `DocumentWorkspaceViewModel`

- [x] 新增 `DocumentWorkspaceViewModel`，承载 `CurrentFilePath`、`DisplayName`、`Content`、`PreviewHtml`、`HasDocument`、`IsDirty`、`CanSave`、`StatusText` 和错误状态。
- [x] 初始状态保持空文档，不初始化旧示例 Markdown。
- [x] 打开文件后更新文档路径、显示名、内容、预览数据和状态。
- [x] 编辑内容时同步更新 `PreviewHtml`，并将 `IsDirty` 置为 `true`。
- [x] 保存成功后将 `IsDirty` 置为 `false`，并更新状态文本。
- [x] 将 `DocumentWorkspaceViewModel` 作为 `AppShellViewModel` 的子状态暴露，避免把文件读写逻辑塞进 Shell 状态中心。

验收标准：

- [x] 初始状态为 `HasDocument=false`、`IsDirty=false`、`CanSave=false`。
- [x] 打开文件后 `HasDocument=true`、`IsDirty=false`、`DisplayName` 等于文件名。
- [x] 编辑后 `IsDirty=true`、`CanSave=true`、`PreviewHtml` 发生变化。
- [x] 保存后 `IsDirty=false`、`CanSave=false`。
- [x] 旧 `MainWindowViewModel` 的默认 `# Hello WeaveDoc!` 不会进入新 Shell 状态。

执行记录（2026-06-05）：

- 新增 `DocumentWorkspaceViewModel`，通过 `IMarkdownDocumentService` 完成按路径打开、内容编辑预览刷新、保存和错误状态反馈。
- 初始状态保持空文档；打开失败和保存失败只更新可见错误状态，不清空当前文档或脏状态。
- `AppShellViewModel` 暴露 `DocumentWorkspace` 子状态，并转发当前文档标题、路径、状态文本和 `HasDocuments`，Shell 自身不承载文件读写逻辑。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter DocumentWorkspace` 通过，7 passed。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore` 通过，44 passed。
- 验证：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warning / 0 error。
- 验证：`dotnet test WeaveDoc.slnx --no-build` 通过，App 44 passed、Converter 107 passed、RAG 13 passed、MarkdownEditor 8 passed。
- 验证：`git diff --check` 无输出。

### 阶段 2：MarkdownEditor 控件适配

#### 2.1 改造编辑器内容数据流

- [x] 保留 `MonacoEditorControl` 作为运行时编辑器控件。
- [x] 移除或隔离 `MonacoEditorControl` 对旧 `MainWindowViewModel` 的直接查找。
- [x] 通过 host 接口、事件或可绑定属性把编辑器内容变更推给 `DocumentWorkspaceViewModel`。
- [x] 支持 Shell 将当前文档内容设置回 Monaco，并避免初始化时反复触发脏状态。

验收标准：

- [x] Monaco 内容变更不会依赖旧窗口 ViewModel。
- [x] 打开文件后 Monaco 收到完整 Markdown 内容。
- [x] 用户编辑 Monaco 后 `DocumentWorkspaceViewModel.Content` 同步变化。
- [x] 初始化/打开文件时不会错误标记为已修改。

执行记录（2026-06-05）：

- 为 `MonacoEditorControl` 新增双向 `EditorContent` 可绑定属性，`contentChanged` 不再查找旧 `MainWindowViewModel`。
- Shell `EditorWorkspace` 接入 `MarkdownEditorControl`，有文档且处于编辑模式时显示 Monaco，无文档时保留原空状态；打开、保存、导出入口继续禁用。
- 独立 `MainWindow` 和 `MarkdownEditorTab` 为 Monaco 增加 `EditorContent` 双向绑定，保留旧 VM 和旧预览同步方式。
- 新增 App headless 覆盖：无文档时 Monaco 隐藏；`DocumentWorkspace.OpenAsync` 后 Monaco 收到完整内容且不置脏；编辑 Monaco 后同步回 `DocumentWorkspaceViewModel.Content`；预览模式继续显示 2.2 前空状态。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands` 通过，1 passed。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore` 通过，45 passed。
- 验证：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过，8 passed。
- 验证：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warning / 0 error。
- 验证：`dotnet test WeaveDoc.slnx --no-build` 通过，App 45 passed、Converter 107 passed、RAG 13 passed、MarkdownEditor 8 passed。
- 验证：`git diff --check` 无输出。

#### 2.2 接入真实 HTML 预览

- [x] 保留 `PreviewWebViewControl` 作为运行时 HTML 预览控件。
- [x] 预览内容由 `DocumentWorkspaceViewModel.PreviewHtml` 驱动。
- [x] 编辑内容后，预览控件收到更新后的 HTML。
- [x] Headless 测试不强制创建真实原生 WebView；通过 ViewModel、控件存在性、fake host 和显式 fallback 状态验证。

验收标准：

- [x] 预览模式能展示当前文档生成的 HTML。
- [x] 编辑后再次预览，显示内容随 `PreviewHtml` 更新。
- [x] 跨平台 WebView 不可用时显示明确失败/不可用状态，不显示假内容。
- [x] `PreviewWebViewControl` 的接入不恢复旧 `MarkdownEditorTab` 内部 Tab。

执行记录（2026-06-05）：

- 为 `AppShellViewModel` 增加 `IsMarkdownPreviewVisible` 和 `IsPreviewEmptyStateVisible`，有文档且处于预览模式时显示真实预览控件，无文档时保留空状态。
- Shell `EditorWorkspace` 接入 `MarkdownPreviewControl`，直接绑定 `DocumentWorkspace.PreviewHtml`，没有嵌入旧 `MarkdownEditorTab`。
- 为 `PreviewWebViewControl` 增加 `IsUsingFallback` 和 `FallbackStatusText`，WebView 未初始化、初始化失败或系统 WebKit/WPE 运行库缺失时显示明确不可用状态，不显示假内容。
- 更新 App headless 覆盖：打开文档后预览控件接收 `PreviewHtml`；编辑内容后预览 HTML 同步变化；headless 环境通过 fallback 状态验证。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore` 通过，45 passed。
- 验证：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过，8 passed。
- 验证：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warning / 0 error。
- 验证：`dotnet test WeaveDoc.slnx --no-build` 通过，App 45 passed、Converter 107 passed、RAG 13 passed、MarkdownEditor 8 passed。

#### 2.3 保留旧 MarkdownEditor 独立入口

- [x] 独立 `WeaveDoc.MarkdownEditor` 项目仍可运行。
- [x] 独立窗口和旧 `MarkdownEditorTab` 的兼容性不阻塞新 Shell 适配。
- [x] 若调整共享控件接口，更新旧窗口/旧 Tab 调用点，避免编译破坏。

验收标准：

- [x] `src/WeaveDoc.MarkdownEditor` 构建通过。
- [x] `tests/WeaveDoc.MarkdownEditor.Tests` 继续通过。
- [x] 新 Shell 没有继承旧内部菜单、旧 Tab 或默认示例内容。

执行记录（2026-06-05）：

- 复核独立 `WeaveDoc.MarkdownEditor` 入口仍保留旧 `MainWindowViewModel`、旧窗口和旧 `MarkdownEditorTab` 路径；共享控件接口调整后，旧窗口/旧 Tab 已通过 `EditorContent` / `HtmlContent` 绑定继续适配。
- `WeaveDoc.MarkdownEditor` 项目构建通过，确认共享控件 API 没有破坏独立项目编译。
- `tests/WeaveDoc.MarkdownEditor.Tests` 继续通过，确认 Markdown 服务和 PDF Viewer 相关兼容测试未回退。
- Shell 侧相关回归测试继续证明新 Shell 使用 `DocumentWorkspaceViewModel`，没有继承旧内部菜单、旧 `MarkdownEditorTab` 或旧默认示例内容。
- 独立入口短超时启动可进入 Markdown Editor 路径；当前环境 WebView 初始化异常被记录并由现有受控处理承接，进程未提前崩溃。
- 验证：`dotnet build src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-restore` 通过，0 warning / 0 error。
- 验证：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过，8 passed。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands|DocumentWorkspace"` 通过，8 passed。
- 验证：`timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build` 进入 Markdown Editor 路径后由 `timeout` 截断，退出码 124；未观察到应用提前崩溃。
- 验证：`git diff --check` 无输出。

#### 2.4 迁移跨平台 Web 宿主

- [x] `WeaveDoc.MarkdownEditor` 移除直接 `Microsoft.Web.WebView2` 包引用，改用 `Avalonia.Controls.WebView` `12.0.1`。
- [x] 删除旧 `WebView2EnvironmentManager`，新增 `IWebViewHost`、`IWebViewHostFactory`、`NativeWebViewHost`、`NativeWebViewHostFactory`、`WebViewBridge` 和 `WebViewHostFactoryProvider`。
- [x] `MonacoEditorControl`、`PreviewWebViewControl`、`PdfViewerControl` 全部通过 `IWebViewHost` 工作，移除控件层对 HWND、`CoreWebView2Controller`、`AddHostObjectToScript` 和 WebView2 专属初始化流程的依赖。
- [x] Monaco、HTML 预览和 PDF.js 页面脚本统一通过 `weaveDocBridge` 发送 `{ Type, Data }` 消息；业务消息继续保留 `contentChanged`、`selectionChanged`、`previewClick`、`previewSelection`、`previewClearHighlight` 和 PDF debug/open 语义。
- [x] Linux 缺少 WebKit/WPE 运行库或宿主初始化失败时显示真实不可用状态，保留文档数据，不弹外部 WebDialog，不降级成假预览。
- [x] App headless 测试使用 fake host，不创建真实原生 WebView；隐藏控件不会在 `Loaded` 时抢先初始化 WebView。

验收标准：

- [x] `rg "Microsoft.Web.WebView2|CoreWebView2|WebView2EnvironmentManager" src/WeaveDoc.MarkdownEditor` 无直接依赖残留。
- [x] `tests/WeaveDoc.MarkdownEditor.Tests` 覆盖 fake host 的 Monaco 内容同步、预览 HTML 更新、fallback 状态和 PDF.js helper。
- [x] `tests/WeaveDoc.App.Tests` 保留新 Shell 不嵌入旧 `MarkdownEditorTab`、不显示旧默认 Markdown、编辑/预览状态可见且 headless 不创建真实 WebView 的覆盖。

执行记录（2026-06-05）：

- 新增跨平台 Web 宿主抽象，`NativeWebViewHost` 适配 Avalonia `NativeWebView` 的导航、HTML 字符串导航、脚本执行、host/page 消息和不可用状态。
- `PdfViewerControl` 迁到同一 host；由于 `NativeWebView` 没有公开 document-created 注入 API，PDF.js 兼容脚本由本地 HTTP 服务在返回 `viewer.html` 前注入。
- 新增 `tests/WeaveDoc.MarkdownEditor.Tests` fake host 和 Avalonia headless NUnit 覆盖，验证 Monaco 内容同步、host 消息回写、预览 HTML 更新和初始化失败 fallback。
- `MonacoEditorControl`、`PreviewWebViewControl`、`PdfViewerControl` 的 host 容器插入/移除增加安全兜底，初始化失败时进入明确 fallback，不在清理路径抛 `NullReferenceException`。
- README 已从 WebView2 依赖说明改为 `NativeWebView`：Windows 使用 WebView2 后端，Linux 使用 WebKit/WPE 后端。
- 验证：`rg -n "Microsoft\.Web\.WebView2|CoreWebView2|WebView2EnvironmentManager" src/WeaveDoc.MarkdownEditor` 无输出。
- 验证：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warning / 0 error。
- 验证：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过，13 passed。
- 验证：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "MarkdownEditor_BindsDocumentWorkspaceContentWithoutEnablingFileCommands|DocumentWorkspace"` 通过，8 passed。
- 验证：`dotnet test WeaveDoc.slnx --no-build` 通过，App 45 passed、Converter 107 passed、RAG 13 passed、MarkdownEditor 13 passed。
- 验证：`timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build` 由 `timeout` 截断，退出码 124；未观察到应用提前崩溃。
- 验证：`timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build` 由 `timeout` 截断，退出码 124；未观察到应用提前崩溃。
- 验证：`git diff --check` 无输出。

### 阶段 3：文件工作流

#### 3.1 打开 Markdown

- [ ] 启用顶部 `打开` 入口和编辑区 `打开` 入口。
- [ ] 无未保存修改时，文件选择成功后直接打开选中文件。
- [ ] 文件选择取消时不改变当前文档。
- [ ] 文件读取失败时保留当前文档，并更新失败状态。

验收标准：

- [ ] 打开测试 `.md` 文件后，编辑器内容等于文件内容。
- [ ] 取消文件选择后，`CurrentFilePath`、`Content` 和 `IsDirty` 保持不变。
- [ ] 读取失败后状态文本包含失败原因，原文档内容不丢失。
- [ ] 顶部和编辑区打开入口使用同一工作流。

#### 3.2 保存 Markdown

- [ ] 启用顶部 `保存` 入口和编辑区 `保存` 入口。
- [ ] 仅当存在当前文档且内容已修改时允许保存。
- [ ] 保存写回当前路径，不在本轮实现另存为。
- [ ] 保存失败时保留 `IsDirty=true` 并显示失败状态。

验收标准：

- [ ] 空状态下保存入口不可用。
- [ ] 打开未修改文档后保存入口不可用。
- [ ] 修改文档后保存入口可用。
- [ ] 保存成功后磁盘内容更新，保存入口重新不可用。

#### 3.3 处理未保存修改

- [ ] 打开新文件前若当前文档 `IsDirty=true`，先触发未保存确认。
- [ ] 选择 `Save` 时，先保存当前文档，保存成功后继续打开新文件。
- [ ] 选择 `Discard` 时，不保存当前文档，直接打开新文件。
- [ ] 选择 `Cancel` 时，取消打开新文件并保持当前状态。

验收标准：

- [ ] `Save` 分支：旧文件内容更新，新文件成为当前文档。
- [ ] `Discard` 分支：旧文件内容不变，新文件成为当前文档。
- [ ] `Cancel` 分支：当前路径、内容和脏状态全部保持不变。
- [ ] 保存失败时不会继续打开新文件。

#### 3.4 关闭窗口保护

- [ ] 窗口关闭时若 `IsDirty=true`，复用未保存确认逻辑。
- [ ] 选择 `Save` 时保存成功后允许关闭。
- [ ] 选择 `Discard` 时允许关闭。
- [ ] 选择 `Cancel` 时阻止关闭。

验收标准：

- [ ] `Cancel` 分支能在测试中证明关闭被取消。
- [ ] `Save` 分支能证明文件已写入并允许关闭。
- [ ] `Discard` 分支能证明未写入文件但允许关闭。

### 阶段 4：Shell UI 接入

#### 4.1 编辑区接入真实内容

- [ ] 将 `EditorWorkspace` 编辑模式从空状态替换为可编辑 Markdown 区域。
- [ ] 运行时使用适配后的 Monaco 编辑器；跨平台 WebView 不可用时显示明确 fallback 状态。
- [ ] 将编辑区域绑定到 `DocumentWorkspaceViewModel.Content`。
- [ ] 无文档时继续显示真实空状态。

验收标准：

- [ ] 打开文件后编辑区显示文件内容。
- [ ] 在编辑区修改文本后 ViewModel 内容同步变化。
- [ ] 无文档时不显示旧 `# Hello WeaveDoc!` 或任何假 Markdown。
- [ ] 编辑/预览模式切换继续更新 active 样式和 `EditorMode`。

#### 4.2 预览区接入真实预览

- [ ] 将 `EditorWorkspace` 预览模式绑定到 `DocumentWorkspaceViewModel.PreviewHtml`。
- [ ] 运行时通过 `PreviewWebViewControl` 展示 HTML。
- [ ] 编辑内容后，切换到预览模式能看到更新后的预览。

验收标准：

- [ ] 预览模式能显示当前文档生成的 HTML。
- [ ] 编辑后再次预览，显示内容随之变化。
- [ ] 运行主窗口不会创建旧 `MarkdownEditorInnerTabs`。

#### 4.3 顶部入口与状态栏

- [ ] 顶部标题区域显示当前 Markdown 文件名。
- [ ] 状态栏显示当前文档状态，包括未打开、已打开、已修改、已保存、失败。
- [ ] 顶部 `打开` / `保存` 和编辑区 `打开` / `保存` 使用同一文档工作流。
- [ ] 保存入口样式和可用状态跟随 `CanSave`。

验收标准：

- [ ] 打开 `demo.md` 后标题区域或文档标题显示 `demo.md`。
- [ ] 修改后状态栏能显示未保存状态。
- [ ] 保存后状态栏能显示保存完成状态。
- [ ] 顶部和编辑区保存入口行为一致。

#### 4.4 左侧文档预览区

- [ ] 左侧文档标签从空状态切换为当前文件名。
- [ ] 左侧页码、缩放和 PDF 相关按钮继续禁用，直到 PDF 阶段完成。
- [ ] 不在 Markdown 主线阶段渲染 PDF 页面缩略图。

验收标准：

- [ ] 打开 Markdown 后左侧标签显示当前文件名。
- [ ] 页码仍为无页面状态，缩放按钮仍不可用。
- [ ] 左侧区域不出现旧 PDF Reader 内部 Tab。

### 阶段 5：PDF Reader 独立延后阶段

> 本阶段只在 Markdown 主线稳定后执行。未执行本阶段前，PDF 打开、PDF 预览、页码和缩放入口继续禁用。

#### 5.1 接入现有 `PdfViewerControl`

- [ ] 在 Shell 中设计单独的 PDF 打开入口，不混入 Markdown 保存流程。
- [ ] 复用 `PdfViewerControl` 加载本地 PDF。
- [ ] 保留现有 PDF.js HTTP 当前文件端点和兼容脚本。
- [ ] PDF 打开失败时显示明确状态，不影响当前 Markdown 文档。

验收标准：

- [ ] 打开 PDF 后左侧或指定预览区显示 PDF 文件名。
- [ ] `PdfViewerControlTests` 继续通过。
- [ ] PDF 打开失败不会清空 Markdown 文档状态。
- [ ] PDF 阶段不启用导出、模板或转换流程。

#### 5.2 PDF UI 状态

- [ ] PDF 阶段完成后才启用相关页码、缩放和关闭入口。
- [ ] PDF 和 Markdown 当前状态分离，避免保存 Markdown 时误操作 PDF。
- [ ] 切换 Markdown 编辑/预览不会销毁正在查看的 PDF 状态，除非明确关闭 PDF。

验收标准：

- [ ] PDF 入口启用状态由 PDF 阶段测试覆盖。
- [ ] Markdown 保存、未保存确认和 PDF 打开互不污染。
- [ ] 没有旧 `PDF Reader` 内部 Tab 结构回流到新 Shell。

### 阶段 6：边界守卫

#### 6.1 避免旧模块整块回流

- [ ] 不嵌入旧 `MarkdownEditorTab`。
- [ ] 不复用旧 `MainWindowViewModel` 作为 Shell 文档状态。
- [ ] 不恢复旧内部菜单、旧 PDF Reader 页签或旧默认示例 Markdown。

验收标准：

- [ ] 新 Shell 中找不到旧 `Markdown Editor` / `PDF Reader` 内部 Tab 结构。
- [ ] 启动后不会出现 `# Hello WeaveDoc!`。
- [ ] `MarkdownEditorTab` 相关控件不成为新 Shell 的默认子控件。

#### 6.2 业务模块保持未接入

- [ ] 不初始化 RAG 服务或 `RagTabViewModel`。
- [ ] 不启用 AI 输入、发送、清空、索引、刷新、删除文档等入口。
- [ ] 不启用导出、模板管理或转换流程。
- [ ] PDF 阶段未完成前，不启用 PDF 打开或 PDF 预览入口。

验收标准：

- [ ] `Shell_DoesNotShowRagOrDemoState` 类测试继续通过或被等价增强。
- [ ] AI/RAG、导出、模板入口在 UI 测试中仍不可执行。
- [ ] PDF 入口状态与阶段完成情况一致。
- [ ] 启动烟测不输出 RAG 初始化错误。

### 阶段 7：测试拆分

#### 7.1 服务测试

- [x] 测试 Markdown 文件读取成功。
- [x] 测试 Markdown 文件保存成功。
- [x] 测试 Markdown 预览 HTML 生成。
- [x] 测试 LaTeX Markdown 保留数学渲染标记。
- [x] 测试读取不存在文件失败。
- [x] 测试保存失败不会清空当前状态。

验收标准：

- [x] 服务测试不依赖 Avalonia UI。
- [x] 每个测试只覆盖一个输入输出行为。
- [x] 临时文件和目录在测试结束后清理。

执行记录（2026-06-05）：

- 新增 `MarkdownDocumentServiceTests`，使用临时目录和真实文件 I/O 覆盖服务层行为。
- 覆盖 `.md` 读取、`.markdown` / `.txt` 支持、保存、标题预览、LaTeX 标记、不存在路径失败和保存失败状态保留。

#### 7.2 ViewModel 测试

- [ ] 测试初始空状态。
- [ ] 测试打开文件状态变化。
- [ ] 测试编辑后脏状态和保存可用状态。
- [ ] 测试保存后脏状态清除。
- [ ] 测试未保存确认的 `Save`、`Discard`、`Cancel` 三个分支。
- [ ] 测试关闭窗口确认分支。

验收标准：

- [ ] 所有 ViewModel 测试使用 fake 服务，不弹真实系统对话框。
- [ ] `Save`、`Discard`、`Cancel` 三个分支都有独立断言。
- [ ] 失败分支能证明当前文档不会被错误替换。

#### 7.3 Avalonia Headless UI 测试

- [ ] 测试主窗口默认空状态仍正确。
- [ ] 测试打开文件后编辑区、标题、左侧标签和状态栏更新。
- [ ] 测试编辑/预览模式切换后显示对应内容。
- [ ] 测试保存按钮可用状态跟随 `IsDirty`。
- [ ] 测试排除项仍禁用，包括导出、AI/RAG、模板入口，以及未完成阶段的 PDF 入口。
- [ ] 测试旧假内容不回流。

验收标准：

- [ ] `tests/WeaveDoc.App.Tests` 覆盖新 Shell 文档纵切行为。
- [ ] headless 测试不依赖真实文件选择对话框。
- [ ] 旧骨架交互测试继续通过或被等价更新。

#### 7.4 MarkdownEditor 兼容测试

- [x] 现有 `MarkdownServiceTests` 继续通过。
- [x] 现有 `PdfViewerControlTests` 继续通过。
- [x] 若改造 `MonacoEditorControl` host 数据流，新增不启动真实原生 WebView 的可测试接口/事件测试。
- [ ] 若调整 `IMarkdownEditorHost`，为 Shell host 和旧 Tab host 各保留编译级覆盖。

验收标准：

- [x] `tests/WeaveDoc.MarkdownEditor.Tests` 全部通过。
- [x] 新 Shell 适配不会破坏独立 MarkdownEditor 项目构建。

执行记录（2026-06-05）：

- `tests/WeaveDoc.MarkdownEditor.Tests` 从 8 个测试扩展到 13 个测试，新增 fake host 控件级覆盖。
- 验证：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore` 通过，13 passed。
- 验证：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warning / 0 error。

### 阶段 8：最终验收与收尾

#### 8.1 自动化验收

- [ ] 运行 `dotnet build WeaveDoc.slnx --no-restore`。
- [ ] 运行 `dotnet test WeaveDoc.slnx --no-build`。
- [ ] 运行 `git diff --check`。
- [ ] 运行短时启动烟测 `timeout 8s dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj --no-build`。

验收标准：

- [ ] 构建通过，0 Error。
- [ ] 测试全量通过。
- [ ] `git diff --check` 无输出。
- [ ] 启动烟测无错误输出；由 `timeout` 结束运行中的桌面应用属于预期。

#### 8.2 清单更新

- [ ] 将本清单中完成的任务逐项勾选。
- [ ] 在对应阶段下记录执行日期、关键改动和验证命令结果。
- [ ] 确认 `doc/软件设计/界面设计_demo` 没有 tracked diff。

验收标准：

- [ ] 本清单能准确反映实际完成进度。
- [ ] 每个完成阶段都有可追溯执行记录。
- [ ] demo 目录未被本纵切修改。

## Markdown 主线最终完成标准

- [ ] Markdown 文件可以从新 Shell 打开，并显示在中央编辑区。
- [ ] 编辑内容会更新脏状态和 HTML 预览数据流。
- [ ] Markdown 文件可以保存回原路径，且保存后脏状态清除。
- [ ] 打开其他文件或关闭窗口时，未保存修改会触发保存、放弃、取消确认。
- [ ] 顶部标题、状态栏和左侧文档标签能反映当前 Markdown 文档。
- [ ] 新 Shell 不显示旧默认 Markdown、不显示旧内部 `Markdown Editor` / `PDF Reader` Tab。
- [ ] 导出、模板、AI/RAG、多文档仍保持排除状态。
- [ ] PDF 入口在 PDF 阶段未完成前保持禁用；若执行 PDF 阶段，则由阶段 5 验收标准另行证明。
- [ ] 自动化测试、构建、diff 检查和短时启动烟测全部通过。
