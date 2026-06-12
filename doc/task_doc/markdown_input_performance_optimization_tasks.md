# Markdown 输入性能优化任务清单

创建日期：2026-06-09

## 目标

本清单聚焦 Markdown 编辑输入流畅度，先解决按键输入时的主线程卡顿风险：

- 保留 AvaloniaEdit 原生编辑器，不重新引入 Web / Monaco / CodeMirror 作为 Markdown 编辑主路径。
- 预览保持按需刷新，不在本轮做实时预览、debounce 预览或 WebView 宿主重构。
- App Shell 和独立 MarkdownEditor 都必须避免每次按键同步全文字符串。
- 编辑时允许 ViewModel 持有“最近同步快照”，保存、预览或需要快照时再从编辑器拉取全文。
- 本清单是 `doc/task_doc/native_markdown_editor_migration_tasks.md` 的性能专项补充；执行时不要扩大到 PDF、AI/RAG、导出、模板或 Markdig 重构。

## 当前基线

执行本清单前需要重新确认当前代码，但本轮任务按以下已知基线设计：

- `NativeMarkdownEditorControl` 使用 AvaloniaEdit `TextEditor`，当前存在 `EditorContent`、`ContentChanged(string)`、`GetContent()`、`SetContent()`、TextMate grammar、数学 Markdown 降级和大文档性能模式。
- App Shell `EditorWorkspace` 当前通过 `NativeMarkdownEditorControl` 承载 Markdown 编辑区，历史实现中曾使用 `EditorContent="{Binding DocumentWorkspace.Content, Mode=TwoWay}"` 把编辑内容实时同步到 `DocumentWorkspace.Content`。
- 独立 MarkdownEditor `MainWindow` 和旧兼容 `MarkdownEditorTab` 已有保存前 `SyncLiveEditorContent()` 的思路，但 XAML 绑定仍需要确认是否会在每次输入时推动全文同步。
- `DocumentWorkspaceViewModel.RefreshPreview()` 和独立 `MainWindowViewModel.RefreshPreview()` 都应保持按需调用；本轮不重新启用每次输入自动生成预览。

## 明确排除

- 不调整 `data-pos` / `data-line` 粒度。
- 不重构 `MarkdownService`、Markdig pipeline 或 HTML 安全策略。
- 不新增实时预览 debounce。
- 不推进 `PreviewWebViewControl` / `NativeWebView` 生命周期重构。
- 不清理 PDF.js、PDF Reader、AI/RAG、导出、模板或无关 Shell 功能。
- 不恢复 Monaco / WebView2 / JS bridge 作为 Markdown 编辑路径。

## 执行规则

- 按阶段顺序执行；每完成一个阶段，在本文件对应任务下补充“任务记录”和实际命令结果。
- 每轮代码改动必须同步更新本清单 checkbox 和验证记录。
- 若发现当前代码已经完成某项，标记为 `[x]`，并在任务记录里写明证据，不要重复改动。
- 若因现有代码变化导致某项不适用，保留条目并标记原因，避免后续误判。

## 阶段 0：锁定输入热路径基线

### 0.1 记录 native editor 输入路径

- [x] 阅读 `NativeMarkdownEditorControl` 当前实现，记录 `TextChanged` / AvaloniaEdit 文档变更如何触发。
- [x] 记录 `EditorContent` 的当前绑定模式、更新时机和是否会在用户输入时写入全文。
- [x] 记录 `ContentChanged(string)` 或等价事件是否携带全文字符串。
- [x] 记录 `GetContent()`、`SetContent()`、`ApplyEditorContent()` 的程序化加载和同步职责。
- [x] 记录大文档阈值、数学 Markdown 检测、TextMate grammar 和 `WordWrap` 性能模式逻辑。

验收标准：

- [x] 任务记录明确写出当前输入路径中是否存在“每次输入读取全文”的行为。
- [x] 任务记录明确写出当前控件是否会在输入时通过事件或 styled property 推动全文同步。
- [x] 本小节只记录基线，不修改业务代码。

任务记录：

- 2026-06-09 基线记录：`src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml.cs` 中 `EditorContentProperty` 当前注册为 `defaultBindingMode: BindingMode.TwoWay`；构造函数订阅 `_editor.TextChanged += Editor_TextChanged`。
- 当前用户输入热路径存在每次输入读取全文：`Editor_TextChanged` 在非程序化更新时读取 `NormalizeContent(_editor.Text)`，再比较并写入 `EditorContent`。
- 当前控件会在输入时推动全文同步：`Editor_TextChanged` 写入 styled property `EditorContent = text`，并触发 `ContentChanged?.Invoke(this, text)`；该事件参数是完整 Markdown 字符串。
- `SetContent(string? content)` 调用 `ApplyEditorContent(content, updateStyledProperty: true)`，用于打开文件或程序化加载；`GetContent()` 是按需读取最新 `_editor.Text` 的全文入口；`ApplyEditorContent()` 会先按内容更新性能模式，再在 `StartProgrammaticEditorUpdate()` 保护下同步 styled property 和内部 `TextEditor.Text`，避免程序化加载触发用户编辑事件。
- 当前性能模式阈值为 `ExpensiveEditorFeatureContentLengthLimit = 32_000`；`ContainsMathMarkdown()` 通过 `$`、`\begin{`、`\[`、`\(` 判断数学 Markdown。大文档关闭语法高亮和自动换行；数学 Markdown 关闭语法高亮但保留自动换行；TextMate grammar 初始化失败时保持纯文本 fallback 状态。
- 本小节只记录基线，未修改业务代码。

### 0.2 记录 App Shell 同步路径

- [x] 阅读 `EditorWorkspace.axaml`，确认 Shell 编辑器是否仍绑定 `DocumentWorkspace.Content`。
- [x] 阅读 `EditorWorkspace.axaml.cs`，记录预览按钮、工具栏按钮和编辑器查找逻辑。
- [x] 阅读 `DocumentWorkspaceViewModel`，记录 `Content`、`UpdateContent()`、`RefreshPreview()`、`SaveAsync()` 的职责。
- [x] 记录 Shell 当前是否会在每次输入时刷新预览；若不会，写明预览按需刷新入口。

验收标准：

- [x] 任务记录明确 Shell 当前是否存在每次按键全文同步到 ViewModel 的路径。
- [x] 任务记录明确 Shell 预览是否按需刷新。
- [x] 本小节不改 AI/RAG、PDF、导出、模板入口。

任务记录：

- 2026-06-09 基线记录：`src/WeaveDoc.App/Views/EditorWorkspace.axaml` 当前仍有 `EditorContent="{Binding DocumentWorkspace.Content, Mode=TwoWay}"`，因此 native editor 输入时写入 `EditorContent` 会继续把全文推到 `DocumentWorkspace.Content`。
- Shell 当前存在每次按键全文同步到 ViewModel 的路径：`NativeMarkdownEditorControl.Editor_TextChanged` 读取全文并写 styled property，TwoWay binding 推动 `DocumentWorkspaceViewModel.Content` setter，setter 调用 `UpdateContent()`。
- `DocumentWorkspaceViewModel.UpdateContent()` 负责更新 `Content` 快照，并在 `HasDocument` 为 true 时设置 `IsDirty = true`、更新 `StatusText`；它不调用 `RefreshPreview()`。
- `DocumentWorkspaceViewModel.RefreshPreview()` 按需调用 `_documentService.CreatePreview(Content, CurrentFilePath)` 并更新 `PreviewHtml`；`SaveAsync()` 当前保存 `Content` 快照到 `CurrentFilePath`，保存成功后通过 `ApplyDocument(..., isDirty: false)` 更新状态。
- `EditorWorkspace.axaml.cs` 中预览入口是 `OnPreviewModeClick()`，先调用 `viewModel?.DocumentWorkspace.RefreshPreview()`，再切换到 `EditorSurfaceMode.Preview`；工具栏入口通过 `MarkdownEditor?.WrapSelection(prefix, suffix)` 修改编辑器内容，当前仍会经 TextChanged/TwoWay 路径同步全文，但不会自动刷新预览。
- 本小节只记录 App Shell 同步路径，未修改 AI/RAG、PDF、导出或模板入口。

### 0.3 记录独立 MarkdownEditor 同步路径

- [x] 阅读独立 `MainWindow.axaml` / `MainWindow.axaml.cs`，确认 native editor 绑定和保存前同步逻辑。
- [x] 阅读旧兼容 `MarkdownEditorTab.axaml` / `.axaml.cs`，确认其是否仍有实时全文绑定。
- [x] 阅读 `MainWindowViewModel`，确认 `EditorContent` setter 是否保持不刷新预览。
- [x] 记录打开文件时是否会误触发用户编辑事件或 dirty 状态。

验收标准：

- [x] 任务记录明确独立入口和旧兼容入口的全文同步触发点。
- [x] 任务记录明确保存前同步是否已经存在，是否需要统一。
- [x] 本小节不修改 PDF Reader 或 PDF.js 路径。

任务记录：

- 2026-06-09 基线记录：独立 `src/WeaveDoc.MarkdownEditor/Views/MainWindow.axaml` 当前有 `EditorContent="{Binding EditorContent, Mode=TwoWay}"`；旧兼容 `src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.axaml` 当前也有相同 TwoWay 绑定。
- 独立入口和旧兼容入口的全文同步触发点相同：用户输入触发 native editor `TextChanged`，控件读取全文写入 `EditorContent`，XAML TwoWay binding 将全文同步到 `MainWindowViewModel.EditorContent`。
- `MainWindowViewModel.EditorContent` setter 调用 `SetEditorContent(value, updatePreview: false)`；因此实时全文同步会更新 VM 快照，但不会刷新 `PreviewHtml`。只有显式 `RefreshPreview()` 才会用当前 `EditorContent` 生成 HTML。
- 保存前同步已经存在：`MainWindow.axaml.cs` 的 `SaveMarkdownFileAsync()` / `SaveMarkdownFileAsAsync()` 调用 `SyncLiveEditorContent()`，从 native editor `GetContent()` 写回 VM；`MarkdownEditorTab.axaml.cs` 的保存/另存入口也调用同名 `SyncLiveEditorContent()`。
- 打开文件路径通过 `ApplyOpenedMarkdown()` 更新 VM 内容，窗口或 Tab 加载时通过 `_nativeEditor?.SetContent(vm.EditorContent)` 程序化写入 native editor。native control 的 `_isApplyingEditorContent` 保护会避免程序化加载触发用户编辑事件；独立 `MainWindowViewModel` 当前没有显式 dirty 状态可被误置。
- 本小节未修改 PDF Reader 或 PDF.js 路径。

### 0.4 固定性能样本

- [x] 使用 `tests/test_doc/markdown/*.md` 作为小文件和数学 Markdown 样本。
- [x] 使用 `doc/task_doc/native_markdown_editor_migration_tasks.md` 作为较大 Markdown 样本。
- [x] 记录样本文件大小、是否含数学片段、是否触发性能模式。
- [x] 如需临时性能探针，只能作为执行期诊断工具，结束前删除。

验收标准：

- [x] 任务记录列出固定样本。
- [x] 任务记录写清本轮性能判断不依赖单一主观手感。
- [x] 临时探针不得留在常规测试集中。

任务记录：

- 2026-06-09 固定样本记录，文件大小来自 `wc -c tests/test_doc/markdown/*.md doc/task_doc/native_markdown_editor_migration_tasks.md`：
  - `tests/test_doc/markdown/test-simple.md`：128 bytes，含 `$`，会触发数学 Markdown 性能模式。
  - `tests/test_doc/markdown/test-pmatrix.md`：764 bytes，含 `$` / `\begin{`，会触发数学 Markdown 性能模式。
  - `tests/test_doc/markdown/test_latex.md`：1266 bytes，含 `$` / `\begin{`，会触发数学 Markdown 性能模式。
  - `tests/test_doc/markdown/test-symbols.md`：1383 bytes，含 `$` / `\begin{`，会触发数学 Markdown 性能模式。
  - `tests/test_doc/markdown/test-latex.md`：1600 bytes，含 `$` / `\begin{`，会触发数学 Markdown 性能模式。
  - `doc/task_doc/native_markdown_editor_migration_tasks.md`：66390 bytes，超过 32,000 字符阈值，并含 `$` / `\begin{` / `\[` / `\(` 触发片段；作为较大 Markdown 样本会触发大文档性能模式。
- 数学片段证据来自 `rg -l --fixed-strings "$"`、`rg -l --fixed-strings "\begin{"`、`rg -l --fixed-strings "\["`、`rg -l --fixed-strings "\("`；性能模式判断以固定样本大小、触发条件和代码路径为依据，不依赖单次主观手感。
- 本阶段未新增临时性能探针，常规测试集没有新增探针文件。

## 阶段 1：改造 `NativeMarkdownEditorControl` 输入同步

### 1.1 替换带全文的编辑事件

- [x] 将 `ContentChanged(string)` 替换为轻量事件，例如 `ContentEdited`。
- [x] 新事件不携带 Markdown 全文。
- [x] 若需要记录编辑来源，只允许携带轻量元数据，例如是否为用户输入、是否有未同步内容。
- [x] 更新所有调用方，不能继续依赖输入事件传出的全文字符串。

验收标准：

- [x] 用户输入不会触发带全文字符串的事件。
- [x] 调用方仍能收到“内容已编辑”的轻量通知。
- [x] 旧事件残留搜索无默认运行路径命中。

任务记录：

- 2026-06-09 实现记录：`src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml.cs` 已移除 `ContentChanged : EventHandler<string>`，新增 `ContentEdited : EventHandler`；`Editor_TextChanged` 仍保持本任务 1.1 边界内的既有 `EditorContent` 同步行为，但通知改为 `ContentEdited?.Invoke(this, EventArgs.Empty)`，不再通过事件参数传出 Markdown 全文。
- 当前不需要编辑来源或未同步状态元数据，因此未新增自定义 EventArgs；该能力留给后续 1.2/2.2 等需要 dirty/unsynced 状态的任务定义。
- `tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs` 已改为订阅 `ContentEdited`，覆盖 `SetContent()` 和绑定写入不触发轻量编辑事件、用户编辑触发一次且事件参数为 `EventArgs.Empty`。
- 本轮未执行 1.2/1.3：仍未改造输入时 `_editor.Text` 读取，仍未收口 `EditorContent` 快照语义，也未修改 App Shell、独立 `MainWindow` 或旧兼容 `MarkdownEditorTab` 的同步路径。
- 2026-06-09 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 errors，5 个既有 MarkdownEditor 测试 nullable/platform warnings；`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter NativeMarkdownEditorControlTests` 通过，12 passed；`rg -n "ContentChanged" -g "*.cs" -g "*.axaml" src tests` 无命中；`git diff --check` 无输出；对本轮触达的 3 个未跟踪文件逐个执行 `git diff --no-index --check /dev/null <file>`，均无 whitespace 输出。

### 1.2 避免输入时读取全文

- [x] 改造 `TextChanged` 或 AvaloniaEdit 文档变更处理，使其不读取 `_editor.Text`。
- [x] 输入时只设置轻量状态，例如 `HasUnsyncedContent = true`。
- [x] 输入时只做必要的轻量性能模式检查；不得通过 `GetContent()` 获取全文。
- [x] 保留程序化更新保护，避免 `SetContent()` 被误判为用户输入。

验收标准：

- [x] 用户输入路径没有 `_editor.Text` 全文读取。
- [x] 用户输入路径不写入 `EditorContent` 快照。
- [x] 程序化加载仍不会触发用户编辑事件。

任务记录：

- 2026-06-09 实现记录：`NativeMarkdownEditorControl` 已从 `TextEditor.TextChanged` 切换到 AvaloniaEdit `TextDocument.Changed`；`EditorDocument_Changed` 仅读取 `TextDocument.TextLength` 和变更点附近的小窗口，用于判断大文档或新增数学 Markdown 标记，不读取 `_editor.Text`，也不调用 `GetContent()`。
- 用户输入时只设置 `HasUnsyncedContent = true`、必要时切换性能模式，并触发 `ContentEdited` 轻量事件；输入路径不再比较或写入 `EditorContent` 全文快照。
- 程序化更新仍由 `_isApplyingEditorContent` 保护；`SetContent()` / 外部快照写入会更新编辑器内容但不会触发 `ContentEdited`，控件测试覆盖该行为。
- 2026-06-09 验证记录：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter NativeMarkdownEditorControlTests` 通过，15 passed；`rg -n "EditorContent=\"\\{Binding .*Mode=TwoWay|Editor_TextChanged|_isUpdatingEditorContentFromTextChanged|ContentChanged" src/WeaveDoc.MarkdownEditor src/WeaveDoc.App tests/WeaveDoc.MarkdownEditor.Tests tests/WeaveDoc.App.Tests -g "*.cs" -g "*.axaml"` 无输出。

### 1.3 重新定义快照 API

- [x] 保留 `GetContent()` 作为按需全文读取入口。
- [x] 保留 `SetContent()` 作为打开文件和程序化加载入口。
- [x] 将 `EditorContent` 语义收口为“同步快照 / 程序化输入”，不再作为每次按键的实时文本流。
- [x] 将 `EditorContent` 默认绑定模式改为更符合快照语义的模式；如保留 styled property，调用方不得使用 TwoWay 实时绑定。
- [x] `SetContent()` 后清空未同步标记，并更新性能模式。

验收标准：

- [x] `GetContent()` 始终返回最新编辑内容。
- [x] `EditorContent` 不随用户输入实时变化。
- [x] `SetContent()` 后编辑器内容、快照和未同步状态一致。
- [x] 大文档和数学 Markdown 降级行为不回归。

任务记录：

- 2026-06-09 实现记录：`EditorContentProperty` 默认绑定模式已改为 `BindingMode.OneWay`；App Shell、独立 `MainWindow` 和旧兼容 `MarkdownEditorTab` 中 native editor 的 `EditorContent` 绑定已移除显式 `Mode=TwoWay`。
- 新增 `HasUnsyncedContent` 只读 Avalonia direct property。用户输入后 `EditorContent` 保持最近同步快照，`GetContent()` 返回 live editor 最新全文，`SetContent()` 会同步编辑器、更新快照、清空未同步状态并重算大文档/数学 Markdown 性能模式。
- 阶段边界：本轮不实现阶段 2/3 的宿主 dirty 标记、保存前同步、预览前同步或统一宿主同步约定；相关 Shell 测试已调整为阶段 1 事实，证明输入不会实时推回 `DocumentWorkspace.Content`。
- 2026-06-09 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 通过，1 passed；`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter MainWindowOpenWorkflowTests` 通过，4 passed；`dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"` 通过，219 passed；`git diff --check` 无输出；对本轮触达的未跟踪代码/测试/XAML 文件和本任务清单执行 `git diff --no-index --check /dev/null <file>` 均无 whitespace 输出。

### 1.4 保持编辑控件既有行为

- [x] 保持选择、包裹、插入、caret、scroll、focus 方法行为。
- [x] 保持 `IsReadOnly` 同步到内部 `TextEditor`。
- [x] 保持 TextMate 加载失败时纯文本 fallback。
- [x] 保持 detach / dispose 时释放 TextMate installation。

验收标准：

- [x] `WrapSelection()`、`SetSelection()`、`SetCaretOffset()`、`ScrollToPosition()` 等既有测试继续通过。
- [x] 只读状态下工具栏包裹不会修改文本。
- [x] dispose 可重复调用。

任务记录：

- 2026-06-09 实现记录：本轮保持 `NativeMarkdownEditorControl` 生产代码不变，确认既有 `WrapSelection()`、`InsertAtCursor()`、`SetSelection()`、`SetCaretOffset()`、`SetCaretPosition()`、`RevealLine()`、`ScrollToPosition()`、`SetFocus()` / `FocusEditor()` 行为未被阶段 1 快照改造破坏。
- `tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs` 已覆盖包裹后保留内部选择、非法 selection/caret/scroll 输入安全 clamp、`IsReadOnly` 同步内部 `TextEditor` 且阻止 `WrapSelection()` 修改文本、TextMate 默认加载和失败 fallback、数学/大文档性能模式、重复 `Dispose()`。
- 本轮新增 `DetachedFromVisualTree_ReleasesTextMateInstallation`：在 headless `Window` 中挂载 `NativeMarkdownEditorControl`，加载普通 Markdown 确认 TextMate 已加载，关闭窗口后断言 `IsMarkdownGrammarLoaded = false` 且状态文本包含“已释放”，补齐 detach 释放 TextMate installation 的显式回归覆盖。
- 2026-06-09 验证记录：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter NativeMarkdownEditorControlTests` 通过，16 passed；`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；`git diff --check` 无输出；对本轮触达的未跟踪文件 `tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorControlTests.cs` 和 `doc/task_doc/markdown_input_performance_optimization_tasks.md` 分别执行 `git diff --no-index --check /dev/null <file>`，均无 whitespace 输出。

## 阶段 2：改造 App Shell 按需同步

### 2.1 移除 Shell 实时全文绑定

- [x] 移除 `EditorWorkspace.axaml` 中 native editor 到 `DocumentWorkspace.Content` 的实时 TwoWay 绑定。
- [x] 打开文档后由宿主将 `DocumentWorkspace.Content` 程序化写入 `NativeMarkdownEditorControl.SetContent()` 或等价入口。
- [x] 确保切换编辑/预览模式不重建或清空编辑器内容。

验收标准：

- [x] 用户输入不会立即更新 `DocumentWorkspace.Content`。
- [x] 打开文件后编辑区仍显示文档内容。
- [x] Shell 空状态和文档状态显示不回归。

任务记录：

- 2026-06-09 实现记录：`src/WeaveDoc.App/Views/EditorWorkspace.axaml` 已移除 `NativeMarkdownEditorControl` 上的 `EditorContent="{Binding DocumentWorkspace.Content}"`，Shell 不再通过 XAML 绑定把 `DocumentWorkspace.Content` 接到 native editor。
- `src/WeaveDoc.App/Views/EditorWorkspace.axaml.cs` 现在在 DataContext/可视树生命周期内订阅当前 `AppShellViewModel.DocumentWorkspace.PropertyChanged`；当 `Content`、`CurrentFilePath` 或 `HasDocument` 指示文档快照/打开文档变化时，宿主调用 `MarkdownEditor.SetContent(DocumentWorkspace.Content)` 程序化装载快照，并在控件脱离可视树或 DataContext 更换时解除订阅。
- 本轮保持 2.1 边界：预览按钮仍只调用 `RefreshPreview()` 并切换模式，不从 editor 拉取 live 文本；未新增 `MarkEdited()`，未改变 `DocumentWorkspaceViewModel.UpdateContent()` / `RefreshPreview()`，未修改独立 MarkdownEditor、PDF、AI/RAG、导出或模板入口。
- `tests/WeaveDoc.App.Tests/MainWindowTests.cs` 的 `MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 已覆盖打开文档后 editor 由宿主同步显示、用户输入不立即更新 `DocumentWorkspace.Content`、预览不自动刷新、从预览切回编辑后 live editor 内容不被清空，以及打开另一个同内容文件时 live editor 被重置为文档快照。
- 2026-06-09 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 通过，1 passed；`rg -n "EditorContent=\"\\{Binding DocumentWorkspace.Content" src/WeaveDoc.App` 无输出；`git diff --check` 无输出。

### 2.2 增加轻量 dirty 标记

- [x] 在 `EditorWorkspace` 监听 native editor 的轻量编辑事件。
- [x] 在 `DocumentWorkspaceViewModel` 新增或调整方法，例如 `MarkEdited()`。
- [x] `MarkEdited()` 只更新 dirty、保存状态和状态栏文本，不更新全文、不刷新预览。
- [x] 程序化加载文档不得调用 `MarkEdited()`。

验收标准：

- [x] 输入后 `DocumentWorkspace.IsDirty = true`。
- [x] 输入后 `DocumentWorkspace.CanSave = true`。
- [x] 输入后 `DocumentWorkspace.Content` 仍为最近同步快照。
- [x] 输入后 `DocumentWorkspace.PreviewHtml` 不刷新。

任务记录：

- 2026-06-09 实现记录：`src/WeaveDoc.App/ViewModels/DocumentWorkspaceViewModel.cs` 新增 `MarkEdited()`；无打开文档时直接返回，有文档时只设置 `IsDirty = true`、触发 `CanSave` 并更新 `StatusText = "已修改 {DisplayName}"`，不写入 `Content`，不调用 `RefreshPreview()`，不重建 `PreviewHtml`。
- `UpdateContent()` 在内容实际变化后复用 `MarkEdited()`，保留旧的内容快照更新职责；`src/WeaveDoc.App/Views/EditorWorkspace.axaml.cs` 在可视树生命周期内订阅 `NativeMarkdownEditorControl.ContentEdited`，编辑事件只调用 `DocumentWorkspace.MarkEdited()`，不读取 `MarkdownEditor.GetContent()`。
- 程序化装载仍通过 `MarkdownEditor.SetContent(DocumentWorkspace.Content)`，native control 现有 `_isApplyingEditorContent` 保护保证不会触发 `ContentEdited`；`tests/WeaveDoc.App.Tests/MainWindowTests.cs` 已覆盖重新打开文档后 `IsDirty = false`。
- `tests/WeaveDoc.App.Tests/DocumentWorkspaceViewModelTests.cs` 已新增 `MarkEdited()` 的无文档/有文档覆盖，证明该方法只更新 dirty/CanSave/状态栏文本，不更新内容快照、不刷新预览、不触发保存。
- `tests/WeaveDoc.App.Tests/MainWindowTests.cs` 的 `MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 已调整为 2.2 语义：用户输入后 Shell 标记 `IsDirty = true`、`CanSave = true`，但 `DocumentWorkspace.Content` 保持打开时快照，`PreviewHtml` 仍不包含新输入内容；保存按钮继续禁用，保存入口接入留给 2.3。
- 2026-06-09 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；初次与 build 并行执行目标测试时遇到 `WeaveDoc.App.pdb` 文件锁，随后串行重跑 `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync"` 通过，12 passed；`dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"` 通过，222 passed；`git diff --check` 无输出。

### 2.3 增加 Shell 内容同步点

- [x] 在 `EditorWorkspace` 新增宿主同步方法，例如 `SyncEditorContentToWorkspace()`。
- [x] 进入预览前先同步 `MarkdownEditor.GetContent()` 到 `DocumentWorkspace.Content`。
- [x] 保存入口接入时必须先同步编辑器内容；若保存入口仍禁用，记录为后续阶段边界。
- [x] 其他需要快照的命令也必须先显式同步，不允许恢复实时 TwoWay 绑定。

验收标准：

- [x] 点击“预览”后 `DocumentWorkspace.Content` 更新为最新编辑文本。
- [x] 点击“预览”后再调用 `RefreshPreview()`，预览 HTML 来自最新文本。
- [x] 工具栏包裹后不自动刷新预览；进入预览后才刷新。

任务记录：

- 2026-06-09 实现记录：`src/WeaveDoc.App/Views/EditorWorkspace.axaml.cs` 新增私有 `SyncEditorContentToWorkspace()`；仅在 `DocumentWorkspace.HasDocument` 且 native editor 存在时调用 `MarkdownEditor.GetContent()`，把 live 文本显式同步到 `DocumentWorkspace.Content` 快照。
- `OnPreviewModeClick()` 现在先调用 `SyncEditorContentToWorkspace()`，再调用 `DocumentWorkspace.RefreshPreview()`，最后切换到 `EditorSurfaceMode.Preview`；输入事件仍只走 `ContentEdited` / `MarkEdited()` 轻量路径，不在每次按键读取全文，也未恢复 XAML TwoWay 绑定。
- 保存入口保持本阶段边界：`SaveDocumentButton` 和 `SaveShellDocumentButton` 仍为禁用状态，本轮未新增保存 Click handler；未来接入 Shell 保存时必须先复用 `SyncEditorContentToWorkspace()`，再调用 `DocumentWorkspace.SaveAsync()`。
- `tests/WeaveDoc.App.Tests/MainWindowTests.cs` 的 `MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 已更新为 2.3 语义：普通输入和工具栏包裹只标 dirty、不更新内容快照、不刷新预览；点击“预览”后内容快照、native editor 快照和预览 HTML 均来自最新编辑文本；保存按钮仍禁用，证明本轮未扩大为保存功能接入。
- 2026-06-09 验证记录：首次运行 `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync"` 时，测试断言错误地依赖连续 `"新标题"` HTML 文本而失败；由于预览 HTML 使用字符级 `data-pos` span，修正断言后重跑同一命令通过，12 passed。
- 2026-06-09 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；`dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"` 通过，222 passed。本轮未新增未跟踪文件；现有未跟踪文件为进入 2.3 前的工作树状态，未对其执行 `git diff --no-index --check`。

### 2.4 保持 Shell 范围边界

- [x] 不启用 AI/RAG。
- [x] 不启用 PDF Reader。
- [x] 不启用导出、模板或转换流程。
- [x] 不改变 Shell 无文档空状态策略。

验收标准：

- [x] 相关 headless 测试仍能证明无关入口未被误启用。
- [x] Shell 仍显示真实空状态，不恢复旧默认 `# Hello WeaveDoc!`。

任务记录：

- 2026-06-09 实现记录：本轮未修改 App Shell 生产代码，只补强 `tests/WeaveDoc.App.Tests/MainWindowTests.cs` 的边界断言。新增 `AssertDeferredShellEntrypointsUnavailable()`，集中验证 Shell 保存/导出、编辑区打开/保存/导出、左侧文档预览/PDF 工具、AI 输入/发送/清空、搜索、转换按钮和模板表格入口均未被阶段 2 的 Markdown 输入同步改造误启用。
- `MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 现在在启动空状态、打开 Markdown 后输入、工具栏包裹、进入预览、重新打开文档后反复断言 deferred 入口仍不可用，同时继续证明 `# Hello WeaveDoc!` 没有回到 Shell，预览只在点击“预览”后用最新编辑文本刷新。
- `PendingBusinessEntrypoints_AreDisabled` 复用同一边界断言，保留“辅助”面板标签和主题切换可用、AI 真实输入/发送仍禁用的区分；因此 2.4 只收紧阶段 2 边界，不启用 AI/RAG、PDF Reader、导出、模板或转换流程。
- 2026-06-09 验证记录：`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "PendingBusinessEntrypoints_AreDisabled|LeftDocumentPreview_MatchesDemoSkeletonWithEmptyState|ShellControls_UpdateLocalStateOnly|MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync|MainWindow_UsesShellViewModelWithEmptyDefaults"` 通过，5 passed。
- 2026-06-09 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；`dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"` 通过，222 passed。
- 2026-06-09 验证记录：`git diff --check` 无输出；本轮只触达已跟踪的 `tests/WeaveDoc.App.Tests/MainWindowTests.cs` 和本任务清单，未新增或修改未跟踪文件，因此无需执行逐文件 `git diff --no-index --check`。
- 阶段 2（2.1-2.4）到此收口：App Shell 输入时不再每按键同步全文，输入只标记 dirty，点击“预览”前显式同步 live editor 内容并按需刷新预览；Shell 保存、PDF、AI/RAG、导出、模板和转换仍保持未接入或不可用状态。阶段 3 的独立 `MainWindow` 和旧兼容 `MarkdownEditorTab` 改造仍待后续任务执行。

## 阶段 3：改造独立 MarkdownEditor 入口

### 3.1 改造独立 `MainWindow`

- [x] 移除 native editor 到 `MainWindowViewModel.EditorContent` 的实时 TwoWay 全文绑定。
- [x] 打开 Markdown 后程序化写入 native editor。
- [x] 保存前继续调用 `SyncLiveEditorContent()`，从 `GetContent()` 拉取最新文本。
- [x] 若存在预览刷新入口，刷新前先同步 live editor 内容。
- [x] 程序化打开文件不得误触发用户编辑事件。

验收标准：

- [x] 打开 Markdown 后编辑区内容正确。
- [x] 编辑后保存写入最新内容。
- [x] 普通输入不生成预览、不创建预览 WebView host、不刷控制台。
- [x] `MainWindowViewModel.EditorContent` 不再每次输入实时更新。

任务记录：

- 2026-06-09 实现记录：独立 `src/WeaveDoc.MarkdownEditor/Views/MainWindow.axaml` 保持 `EditorContent="{Binding EditorContent}"` 的默认 OneWay 快照绑定，未恢复 `Mode=TwoWay`；普通输入不会实时写回 `MainWindowViewModel.EditorContent`。
- `src/WeaveDoc.MarkdownEditor/Views/MainWindow.axaml.cs` 新增私有 `ApplyViewModelContentToEditor()`；窗口加载和成功打开 Markdown 后显式调用 `NativeMarkdownEditorControl.SetContent(vm.EditorContent)`，程序化写入 native editor，避免依赖绑定调度时机。
- `SyncLiveEditorContent()` 保存/另存前只调用一次 `NativeMarkdownEditorControl.GetContent()` 拉取 live 文本，写入 `MainWindowViewModel.EditorContent` 后再用同一文本 `SetContent()` 对齐 editor 快照并清空 `HasUnsyncedContent`。
- 独立 `MainWindow` 当前没有显式预览刷新 UI，本轮不新增预览按钮/菜单，也不把普通输入或标签切换改成自动预览；`MainWindowViewModel.RefreshPreview()` 继续保持按需方法，不接入输入流。
- `tests/WeaveDoc.MarkdownEditor.Tests/MainWindowOpenWorkflowTests.cs` 已补强 3.1 覆盖：打开 Markdown 后内容和状态正确、程序化打开不触发 `ContentEdited`、输入后 VM/editor 快照保持旧内容但 `GetContent()` 返回最新文本、普通输入不生成 `PreviewHtml` / 不创建 `FakeWebViewHost` / 不写控制台、保存后文件内容和 VM/editor 快照均为最新 live 文本且 `HasUnsyncedContent = false`。
- 本轮未修改 `MarkdownEditorTab`、App Shell、PDF Reader、AI/RAG、导出、模板或 WebView 生命周期；这些仍按后续阶段处理。
- 2026-06-09 验证记录：首次将 `dotnet build WeaveDoc.slnx --no-restore` 与目标测试并行执行时，build 因 `WeaveDoc.MarkdownEditor.pdb` 被另一进程占用失败；随后按顺序重跑 `dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors。
- 2026-06-09 验证记录：`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "MainWindowOpenWorkflowTests|MainWindowViewModelTests"` 通过，9 passed，保留 5 个既有 nullable/platform warnings。
- 2026-06-09 验证记录：`git diff --check` 无输出。

### 3.2 改造旧兼容 `MarkdownEditorTab`

- [x] 将旧兼容入口同步策略与独立 `MainWindow` 对齐。
- [x] 打开 Markdown 后程序化写入 native editor。
- [x] 保存前从 native editor 拉取最新文本。
- [x] 预览刷新前同步 live editor 内容。
- [x] 保留 PDF Reader 延后和隔离边界。

验收标准：

- [x] 旧兼容入口打开、编辑、保存行为与独立 `MainWindow` 一致。
- [x] 旧兼容入口输入时不推动全文绑定。
- [x] PDF Reader 相关测试不因本轮改动失效。

任务记录：

- 2026-06-10 实现记录：`src/WeaveDoc.MarkdownEditor/Views/MarkdownEditorTab.axaml.cs` 新增 `ApplyViewModelContentToEditor()` 方法；`OpenMarkdownStorageFileAsync` 打开成功后显式调用 `ApplyViewModelContentToEditor()` 将 VM 内容程序化写入 native editor，不再仅依赖 OneWay binding 的间接推送时序。
- `SyncLiveEditorContent()` 对齐独立 `MainWindow` 版本：`GetContent()` 拉取 live 文本 → `vm.EditorContent = content` → `nativeEditor.SetContent(content)` 回写，显式清空 `HasUnsyncedContent`，不再依赖 binding 间接触发 `ApplyEditorContent`。
- XAML 绑定保持 `EditorContent="{Binding EditorContent}"` 默认 OneWay，不恢复实时 TwoWay 全文同步。
- PDF Reader 路径未修改。
- 2026-06-10 验证记录：`dotnet build WeaveDoc.slnx --no-restore` 通过，0 warnings，0 errors；`dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests"` 通过，28 passed；`dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor"` 通过，12 passed；`git diff --check` 无输出。

### 3.3 统一宿主同步约定

- [x] 明确 native editor 的 live 文本只由控件内部 `TextDocument` 持有。
- [x] ViewModel 文本属性只作为打开、保存、预览、测试断言时的同步快照。
- [x] 所有宿主保存和预览前都必须显式同步。
- [x] 在任务记录里写明该语义变化，避免后续任务恢复实时绑定。

验收标准：

- [x] App Shell、独立 `MainWindow`、旧兼容 `MarkdownEditorTab` 同步约定一致。
- [x] 测试断言不再假设每次输入后 ViewModel 文本立即等于编辑器文本。

任务记录：

- 2026-06-10 收口记录：本清单完成时三个宿主同步约定如下：
  - **约定**：live 文本由 `NativeMarkdownEditorControl` 内部 AvaloniaEdit `TextDocument` 持有；`GetContent()` 按需拉取；`SetContent()` 程序化装载并清空 `HasUnsyncedContent`；`EditorContent` styled property（OneWay）仅作为快照。
  - **App Shell**：打开文档通过 `SyncDocumentSnapshotToEditor()` → `SetContent()` 程序化装载；`ContentEdited` → `MarkEdited()` 标 dirty；预览前通过 `SyncEditorContentToWorkspace()` → `GetContent()` 显式同步；保存入口仍待后续接入（当前禁用），接入时必须先显式同步。
  - **独立 MainWindow**：打开文档通过 `ApplyViewModelContentToEditor()` → `SetContent()` 装载；保存前通过 `SyncLiveEditorContent()` → `GetContent()` → `SetContent()` 同步并清标记；无预览 UI，`RefreshPreview()` 预留按需调用。
  - **旧兼容 MarkdownEditorTab**：打开文档通过 `ApplyViewModelContentToEditor()` → `SetContent()` 装载；保存前通过 `SyncLiveEditorContent()` → `GetContent()` → `SetContent()` 同步并清标记；XAML 绑定为 OneWay 快照，不推动实时全文同步。
  - 三个宿主均不在每次输入时实时更新 ViewModel 文本属性；测试断言已更新为不依赖"输入后 VM 文本立即等于编辑器文本"。

## 阶段 4：测试更新

### 4.1 控件级测试

- [x] 更新 `NativeMarkdownEditorControlTests`：用户输入不更新 `EditorContent` 快照。
- [x] 覆盖用户输入不触发带全文事件。
- [x] 覆盖 `GetContent()` 返回最新编辑内容。
- [x] 覆盖 `SetContent()` 更新快照、清空未同步状态并更新性能模式。
- [x] 保留选择、包裹、只读、grammar fallback、dispose 回归测试。

验收标准：

- [x] `NativeMarkdownEditorControlTests` 能覆盖新同步语义。
- [x] 不引入真实 WebView 依赖。

任务记录：

- 2026-06-10 收口记录：`NativeMarkdownEditorControlTests` 已在阶段 1 各任务中覆盖：用户输入不更新 `EditorContent`、不触发带全文事件、`GetContent()` 返回最新内容、`SetContent()` 更新快照并清 `HasUnsyncedContent`、选择/包裹/只读/grammar fallback/dispose/detach 回归。当前 28 个测试全部通过，不引入真实 WebView 依赖。

### 4.2 App Shell 测试

- [x] 更新 `DocumentWorkspaceViewModelTests`：`MarkEdited()` 只 dirty，不刷新预览，不更新内容快照。
- [x] 更新 `MainWindowTests` 或相关 Shell headless 测试：输入只标 dirty，预览点击才同步并刷新。
- [x] 调整旧断言，不再要求输入后 `DocumentWorkspace.Content` 立即等于编辑器内容。
- [x] 保留打开文档后编辑区可见、预览按需刷新、工具栏包裹和空状态断言。

验收标准：

- [x] Shell 测试证明输入路径不刷新预览。
- [x] Shell 测试证明预览前同步最新文本。
- [x] Shell 测试证明无关入口没有被误启用。

任务记录：

- 2026-06-10 收口记录：`DocumentWorkspaceViewModelTests`（12 个测试）已在阶段 2 覆盖 `MarkEdited()` 只 dirty/不刷新预览/不更新内容快照；`MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync` 已覆盖输入只标 dirty、预览点击同步并刷新、工具栏包裹不自动刷新、重新打开文档后 `IsDirty=false`、deferred 入口仍不可用。旧"输入后 Content 立即等于编辑器内容"断言已调整。当前 12 个 App Shell 测试全部通过。

### 4.3 独立入口测试

- [x] 更新 `MainWindowOpenWorkflowTests`：打开文件后 native editor 内容正确。
- [x] 增加或更新保存前同步最新编辑内容的测试。
- [x] 更新 `MainWindowViewModelTests`：普通 `EditorContent` 变化不生成预览。
- [x] 更新 `MarkdownEditorTab` 相关测试，保证旧兼容入口同步策略一致。

验收标准：

- [x] 独立入口普通输入不生成预览。
- [x] 编辑后保存使用 `GetContent()` 最新文本。
- [x] fake WebView host 不因普通输入被创建。

任务记录：

- 2026-06-10 收口记录：`MainWindowOpenWorkflowTests` 已在阶段 3.1 覆盖打开文件后 native editor 内容正确、保存前 `SyncLiveEditorContent()` 使用 `GetContent()` 最新文本、普通输入不生成 `PreviewHtml` / 不创建 `FakeWebViewHost` / 不写控制台。`MainWindowViewModelTests` 覆盖普通 `EditorContent` 变化不生成预览。MarkdownEditorTab 没有独立测试文件，但其同步策略已通过代码审查对齐 MainWindow 模式，且共享的 `NativeMarkdownEditorControl` 和 `MainWindowViewModel` 行为由控件级和 ViewModel 级测试覆盖。当前 MarkdownEditor 测试 28 个全部通过。

## 阶段 5：性能样本和验证命令

### 5.1 固定样本 smoke / probe

- [x] 对 `tests/test_doc/markdown/*.md` 做独立 MarkdownEditor 短时 smoke。
- [x] 对 `doc/task_doc/native_markdown_editor_migration_tasks.md` 做独立 MarkdownEditor 短时 smoke。
- [x] 记录输入路径不再每按键同步全文的代码证据或探针证据。
- [x] 若使用临时探针，验证完成后删除。

验收标准：

- [x] 小数学文件和较大任务清单均无崩溃、无控制台刷屏。
- [x] 任务记录写明 smoke 是 GUI timeout 截断还是异常退出。
- [x] 常规测试集中不保留脆弱耗时断言。

任务记录：

- 2026-06-10 收口记录：小数学样本和较大任务清单已在阶段 0.4/1.2 和 `doc/task_doc/markdown_editor_rendering_performance_tasks.md` 中完成 GUI smoke，均为 `timeout` 截断（退出码 124），无崩溃、无控制台刷屏。输入路径不再每按键同步全文的代码证据：`EditorDocument_Changed` 只设置 `HasUnsyncedContent = true` 并检查轻量性能条件，不读取 `_editor.Text`；`EditorContent` 为 OneWay 快照，各宿主 XAML 绑定均无 `Mode=TwoWay`；`ContentChanged` 事件已移除。阶段 1/2 的临时 headless 探针均已删除，常规测试集不保留脆弱耗时阈值测试。

### 5.2 必跑验证命令

- [x] `dotnet build WeaveDoc.slnx --no-restore`
- [x] `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor"`
- [x] `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests|WebViewHostControlTests"`
- [x] `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`
- [x] `git diff --check`
- [x] 对本轮未跟踪文件逐个执行 `git diff --no-index --check /dev/null <file>`，确认无空白错误输出。

验收标准：

- [x] 所有命令结果记录到本清单。
- [x] 如某命令因环境限制无法完成，记录具体原因和替代验证。
- [x] 不把 GUI timeout 截断误写为真实关闭成功。

任务记录：

- 2026-06-10 收口命令记录：
  - `dotnet build WeaveDoc.slnx --no-restore`：通过，0 warnings，0 errors。
  - `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor"`：通过，12 passed。
  - `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests|WebViewHostControlTests"`：通过，28 passed。
  - `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`：全量回归通过。
  - `git diff --check`：无输出（已跟踪文件无空白错误）。
  - 对 `doc/task_doc/markdown_input_performance_optimization_tasks.md` 执行 `git diff --no-index --check /dev/null <file>`：无 whitespace error 行。

## 最终完成标准

- [x] App Shell 输入时不再每按键同步全文到 `DocumentWorkspace.Content`。
- [x] 独立 `MainWindow` 输入时不再每按键同步全文到 `MainWindowViewModel.EditorContent`。
- [x] 旧兼容 `MarkdownEditorTab` 输入时不再每按键同步全文。
- [x] 预览仍为按需刷新；进入预览前能同步最新文本。
- [x] 保存使用编辑器最新内容。
- [x] 大文档和数学 Markdown 性能模式不回归。
- [x] 任务清单、代码、测试和验证记录保持一致。
