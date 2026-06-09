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

- [ ] 移除 `EditorWorkspace.axaml` 中 native editor 到 `DocumentWorkspace.Content` 的实时 TwoWay 绑定。
- [ ] 打开文档后由宿主将 `DocumentWorkspace.Content` 程序化写入 `NativeMarkdownEditorControl.SetContent()` 或等价入口。
- [ ] 确保切换编辑/预览模式不重建或清空编辑器内容。

验收标准：

- [ ] 用户输入不会立即更新 `DocumentWorkspace.Content`。
- [ ] 打开文件后编辑区仍显示文档内容。
- [ ] Shell 空状态和文档状态显示不回归。

任务记录：

- 待执行。

### 2.2 增加轻量 dirty 标记

- [ ] 在 `EditorWorkspace` 监听 native editor 的轻量编辑事件。
- [ ] 在 `DocumentWorkspaceViewModel` 新增或调整方法，例如 `MarkEdited()`。
- [ ] `MarkEdited()` 只更新 dirty、保存状态和状态栏文本，不更新全文、不刷新预览。
- [ ] 程序化加载文档不得调用 `MarkEdited()`。

验收标准：

- [ ] 输入后 `DocumentWorkspace.IsDirty = true`。
- [ ] 输入后 `DocumentWorkspace.CanSave = true`。
- [ ] 输入后 `DocumentWorkspace.Content` 仍为最近同步快照。
- [ ] 输入后 `DocumentWorkspace.PreviewHtml` 不刷新。

任务记录：

- 待执行。

### 2.3 增加 Shell 内容同步点

- [ ] 在 `EditorWorkspace` 新增宿主同步方法，例如 `SyncEditorContentToWorkspace()`。
- [ ] 进入预览前先同步 `MarkdownEditor.GetContent()` 到 `DocumentWorkspace.Content`。
- [ ] 保存入口接入时必须先同步编辑器内容；若保存入口仍禁用，记录为后续阶段边界。
- [ ] 其他需要快照的命令也必须先显式同步，不允许恢复实时 TwoWay 绑定。

验收标准：

- [ ] 点击“预览”后 `DocumentWorkspace.Content` 更新为最新编辑文本。
- [ ] 点击“预览”后再调用 `RefreshPreview()`，预览 HTML 来自最新文本。
- [ ] 工具栏包裹后不自动刷新预览；进入预览后才刷新。

任务记录：

- 待执行。

### 2.4 保持 Shell 范围边界

- [ ] 不启用 AI/RAG。
- [ ] 不启用 PDF Reader。
- [ ] 不启用导出、模板或转换流程。
- [ ] 不改变 Shell 无文档空状态策略。

验收标准：

- [ ] 相关 headless 测试仍能证明无关入口未被误启用。
- [ ] Shell 仍显示真实空状态，不恢复旧默认 `# Hello WeaveDoc!`。

任务记录：

- 待执行。

## 阶段 3：改造独立 MarkdownEditor 入口

### 3.1 改造独立 `MainWindow`

- [ ] 移除 native editor 到 `MainWindowViewModel.EditorContent` 的实时 TwoWay 全文绑定。
- [ ] 打开 Markdown 后程序化写入 native editor。
- [ ] 保存前继续调用 `SyncLiveEditorContent()`，从 `GetContent()` 拉取最新文本。
- [ ] 若存在预览刷新入口，刷新前先同步 live editor 内容。
- [ ] 程序化打开文件不得误触发用户编辑事件。

验收标准：

- [ ] 打开 Markdown 后编辑区内容正确。
- [ ] 编辑后保存写入最新内容。
- [ ] 普通输入不生成预览、不创建预览 WebView host、不刷控制台。
- [ ] `MainWindowViewModel.EditorContent` 不再每次输入实时更新。

任务记录：

- 待执行。

### 3.2 改造旧兼容 `MarkdownEditorTab`

- [ ] 将旧兼容入口同步策略与独立 `MainWindow` 对齐。
- [ ] 打开 Markdown 后程序化写入 native editor。
- [ ] 保存前从 native editor 拉取最新文本。
- [ ] 预览刷新前同步 live editor 内容。
- [ ] 保留 PDF Reader 延后和隔离边界。

验收标准：

- [ ] 旧兼容入口打开、编辑、保存行为与独立 `MainWindow` 一致。
- [ ] 旧兼容入口输入时不推动全文绑定。
- [ ] PDF Reader 相关测试不因本轮改动失效。

任务记录：

- 待执行。

### 3.3 统一宿主同步约定

- [ ] 明确 native editor 的 live 文本只由控件内部 `TextDocument` 持有。
- [ ] ViewModel 文本属性只作为打开、保存、预览、测试断言时的同步快照。
- [ ] 所有宿主保存和预览前都必须显式同步。
- [ ] 在任务记录里写明该语义变化，避免后续任务恢复实时绑定。

验收标准：

- [ ] App Shell、独立 `MainWindow`、旧兼容 `MarkdownEditorTab` 同步约定一致。
- [ ] 测试断言不再假设每次输入后 ViewModel 文本立即等于编辑器文本。

任务记录：

- 待执行。

## 阶段 4：测试更新

### 4.1 控件级测试

- [ ] 更新 `NativeMarkdownEditorControlTests`：用户输入不更新 `EditorContent` 快照。
- [ ] 覆盖用户输入不触发带全文事件。
- [ ] 覆盖 `GetContent()` 返回最新编辑内容。
- [ ] 覆盖 `SetContent()` 更新快照、清空未同步状态并更新性能模式。
- [ ] 保留选择、包裹、只读、grammar fallback、dispose 回归测试。

验收标准：

- [ ] `NativeMarkdownEditorControlTests` 能覆盖新同步语义。
- [ ] 不引入真实 WebView 依赖。

任务记录：

- 待执行。

### 4.2 App Shell 测试

- [ ] 更新 `DocumentWorkspaceViewModelTests`：`MarkEdited()` 只 dirty，不刷新预览，不更新内容快照。
- [ ] 更新 `MainWindowTests` 或相关 Shell headless 测试：输入只标 dirty，预览点击才同步并刷新。
- [ ] 调整旧断言，不再要求输入后 `DocumentWorkspace.Content` 立即等于编辑器内容。
- [ ] 保留打开文档后编辑区可见、预览按需刷新、工具栏包裹和空状态断言。

验收标准：

- [ ] Shell 测试证明输入路径不刷新预览。
- [ ] Shell 测试证明预览前同步最新文本。
- [ ] Shell 测试证明无关入口没有被误启用。

任务记录：

- 待执行。

### 4.3 独立入口测试

- [ ] 更新 `MainWindowOpenWorkflowTests`：打开文件后 native editor 内容正确。
- [ ] 增加或更新保存前同步最新编辑内容的测试。
- [ ] 更新 `MainWindowViewModelTests`：普通 `EditorContent` 变化不生成预览。
- [ ] 更新 `MarkdownEditorTab` 相关测试，保证旧兼容入口同步策略一致。

验收标准：

- [ ] 独立入口普通输入不生成预览。
- [ ] 编辑后保存使用 `GetContent()` 最新文本。
- [ ] fake WebView host 不因普通输入被创建。

任务记录：

- 待执行。

## 阶段 5：性能样本和验证命令

### 5.1 固定样本 smoke / probe

- [ ] 对 `tests/test_doc/markdown/*.md` 做独立 MarkdownEditor 短时 smoke。
- [ ] 对 `doc/task_doc/native_markdown_editor_migration_tasks.md` 做独立 MarkdownEditor 短时 smoke。
- [ ] 记录输入路径不再每按键同步全文的代码证据或探针证据。
- [ ] 若使用临时探针，验证完成后删除。

验收标准：

- [ ] 小数学文件和较大任务清单均无崩溃、无控制台刷屏。
- [ ] 任务记录写明 smoke 是 GUI timeout 截断还是异常退出。
- [ ] 常规测试集中不保留脆弱耗时断言。

任务记录：

- 待执行。

### 5.2 必跑验证命令

- [ ] `dotnet build WeaveDoc.slnx --no-restore`
- [ ] `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "DocumentWorkspaceViewModelTests|MarkdownEditor"`
- [ ] `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests|WebViewHostControlTests"`
- [ ] `dotnet test WeaveDoc.slnx --no-build --filter "FullyQualifiedName!~NativeWebViewStressTest"`
- [ ] `git diff --check`
- [ ] 对本轮未跟踪文件逐个执行 `git diff --no-index --check /dev/null <file>`，确认无空白错误输出。

验收标准：

- [ ] 所有命令结果记录到本清单。
- [ ] 如某命令因环境限制无法完成，记录具体原因和替代验证。
- [ ] 不把 GUI timeout 截断误写为真实关闭成功。

任务记录：

- 待执行。

## 最终完成标准

- [ ] App Shell 输入时不再每按键同步全文到 `DocumentWorkspace.Content`。
- [ ] 独立 `MainWindow` 输入时不再每按键同步全文到 `MainWindowViewModel.EditorContent`。
- [ ] 旧兼容 `MarkdownEditorTab` 输入时不再每按键同步全文。
- [ ] 预览仍为按需刷新；进入预览前能同步最新文本。
- [ ] 保存使用编辑器最新内容。
- [ ] 大文档和数学 Markdown 性能模式不回归。
- [ ] 任务清单、代码、测试和验证记录保持一致。
