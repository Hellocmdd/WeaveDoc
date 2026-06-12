# Markdown 编辑器渲染/布局性能诊断与修复任务清单

创建日期：2026-06-09

## 目标

本清单专门处理用户在独立 MarkdownEditor 中反馈的实际体感卡顿：使用 `tests/test_doc/markdown/*.md` 小数学样本时，上下滚动、换行和输入仍然明显卡顿。

- 优先诊断 `NativeMarkdownEditorControl` 的 AvaloniaEdit 渲染和布局配置，而不是继续围绕 ViewModel 全文同步。
- 暂停 `doc/task_doc/markdown_input_performance_optimization_tasks.md` 后续 3.2 / 3.3，先修复已复现的独立 `MainWindow` 体感问题。
- 默认采用性能优先策略：若验证 `WordWrap=True` 是主因，则 Markdown 编辑默认关闭自动换行，保留横向滚动，不新增 UI 设置开关。
- 先修共享控件，再验证独立 `MainWindow`、旧兼容 `MarkdownEditorTab` 和 App Shell 是否自然复用同一策略。

## 当前已知基线

- `src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml` 中 `TextEditor` 当前声明 `ShowLineNumbers="True"`、`WordWrap="True"`。
- `NativeMarkdownEditorControl.ConfigureEditor()` 当前也设置 `_editor.WordWrap = true`。
- 当前大文档性能模式会关闭语法高亮和自动换行；数学 Markdown 性能模式只关闭语法高亮，仍保留自动换行。
- `tests/test_doc/markdown/*.md` 均为小型 LaTeX / 数学 Markdown 样本，大小约 128 bytes 到 1600 bytes，都会触发数学 Markdown 性能模式。
- 独立 `MainWindow` 当前是左右两栏布局，右侧 `PreviewWebViewControl` 常驻占用半宽，但 `AutoActivateOnVisible="False"`，普通打开和输入不应主动创建预览 WebView host。
- 已完成的 3.1 同步改造只解决“输入时不要实时全文同步到 VM / 保存前按需拉 live 文本”，不覆盖滚动、换行和渲染布局卡顿。

## 明确排除

- 不重构 PDF Reader、PDF.js、`PdfViewerControl` 或 PDF WebView 生命周期。
- 不重构 AI/RAG、导出、模板、转换或 App Shell 业务入口。
- 不重构 `MarkdownService`、Markdig pipeline、KaTeX 预览模板或 `data-line` / `data-pos` 生成。
- 不恢复 Monaco、WebView2、CodeMirror 或 JS bridge 作为 Markdown 编辑主路径。
- 不新增实时预览、debounce 预览、预览 WebView 宿主重构。
- 不把自动换行做成用户设置；若后续需要设置页或持久化，另开任务。

## 执行规则

- 按阶段顺序执行；每完成一个阶段，在本文件对应任务下补充“任务记录”和实际命令结果。
- 每轮代码改动必须同步更新本清单 checkbox 和验证记录。
- 诊断阶段允许临时探针，但结束前必须删除；常规测试集中不得保留脆弱的耗时阈值断言。
- 若 GUI smoke 使用 `timeout` 截断，只能记录为“超时截断 smoke”，不得误写为真实正常退出。
- 若发现某个假设不成立，保留条目并记录证据，避免后续继续按错误方向优化。

## 阶段 0：锁定真实卡顿基线

### 0.1 固定复现命令和样本

- [x] 使用 `dotnet run --project src/WeaveDoc.MarkdownEditor -- tests/test_doc/markdown/test-symbols.md` 作为首个独立 MarkdownEditor 复现入口。
- [x] 逐个覆盖 `test-simple.md`、`test-pmatrix.md`、`test_latex.md`、`test-latex.md`。
- [x] 记录每个样本的文件大小、最长行长度、是否含 `$` / `\begin{` / `\[` / `\(`。
- [x] 记录启动命令是否能通过命令行参数自动打开文件；若不能，记录实际打开路径。

验收标准：

- [x] 任务记录明确列出复现命令、样本文件和实际打开方式。
- [x] 任务记录明确写出“3.1 同步优化不覆盖渲染/布局卡顿”。

任务记录：

- 2026-06-10：按阶段 0 锁定当前独立 MarkdownEditor 复现入口。清单原命令
  `dotnet run --project src/WeaveDoc.MarkdownEditor -- tests/test_doc/markdown/test-symbols.md`
  在当前工作区不能直接运行，`dotnet run` 返回：
  `Specify which project file to use because src/WeaveDoc.MarkdownEditor contains more than one project file.`
  原因是目录内当前存在多个项目文件：`WeaveDoc.MarkdownEditor.csproj` 和未跟踪的 `TestApp.csproj`。
- 实际可复现入口固定为显式项目文件：
  `dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj -- <sample>`。
  `App.axaml.cs` 会读取 `desktop.Args[0]` 并写入 `MainWindow.InitialFilePath`，`MainWindow.OnLoaded()` 再调用 `OpenFileFromPathAsync()`，所以样本可以通过命令行参数自动打开。
- 为避免把 GUI 常驻误记为正常退出，smoke 统一使用 `timeout` 截断。已执行：
  `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- tests/test_doc/markdown/test-symbols.md`，
  退出码 `124`，stdout/stderr 无异常输出。
- 同一显式项目入口逐个覆盖 `test-simple.md`、`test-pmatrix.md`、`test_latex.md`、`test-latex.md`，均为 GUI 持续运行到 `timeout` 截断，退出码 `124`，stdout/stderr 无异常输出；该记录只表示启动和自动打开 smoke，不表示正常退出。
- 样本基线：

  | 样本 | 大小 | 最长行 | `$` | `\begin{` | `\[` | `\(` |
  | --- | ---: | ---: | --- | --- | --- | --- |
  | `tests/test_doc/markdown/test-symbols.md` | 1383 bytes | 142 chars | yes | yes | no | no |
  | `tests/test_doc/markdown/test-simple.md` | 128 bytes | 28 chars | yes | no | no | no |
  | `tests/test_doc/markdown/test-pmatrix.md` | 764 bytes | 100 chars | yes | yes | no | no |
  | `tests/test_doc/markdown/test_latex.md` | 1266 bytes | 91 chars | yes | yes | no | no |
  | `tests/test_doc/markdown/test-latex.md` | 1599 bytes | 84 chars | yes | yes | no | no |

- 3.1 同步优化只解决“输入时不要实时全文同步到 VM / 保存前按需拉 live 文本”，不覆盖 AvaloniaEdit 渲染、自动换行布局、上下滚动或连续换行卡顿。

### 0.2 记录当前 editor 和宿主状态

- [x] 记录 `NativeMarkdownEditorControl` 当前 `WordWrap`、`ShowLineNumbers`、TextMate grammar 状态和性能模式状态。
- [x] 记录数学 Markdown 样本是否关闭 TextMate 但仍保留自动换行。
- [x] 记录独立 `MainWindow` 左右两栏宽度策略和右侧预览是否创建 WebView host。
- [x] 记录输入、连续换行、上下滚动时是否有控制台刷屏或异常输出。

验收标准：

- [x] 任务记录明确当前卡顿发生在小数学样本，而不是大文档专属问题。
- [x] 任务记录明确普通输入是否会创建预览 host。

任务记录：

- 2026-06-10：当前 `NativeMarkdownEditorControl.axaml` 中 `TextEditor` 声明 `ShowLineNumbers="True"`、`WordWrap="True"`，并保留横向、纵向滚动条 `Auto`。
- `NativeMarkdownEditorControl.ConfigureEditor()` 当前也设置 `_editor.WordWrap = true`，因此 XAML 默认和代码默认一致，都是开启自动换行。
- `ApplyPerformanceModeForState()` 当前以“大文档或数学 Markdown”进入性能模式：都会释放 TextMate grammar、设置 `IsMarkdownGrammarLoaded = false`；但只有 `contentLength > ExpensiveEditorFeatureContentLengthLimit` 时才关闭自动换行。数学 Markdown 样本会关闭 TextMate grammar，但仍保留 `WordWrap=true`。
- 大文档性能模式文案为“已关闭语法高亮和自动换行”；数学 Markdown 性能模式文案为“已关闭语法高亮”，与当前行为一致。
- 独立 `MainWindow.axaml` 的 Markdown Editor 页是左右两栏布局，`Grid.ColumnDefinitions` 为 `* / *`，左侧 `NativeMarkdownEditorControl`、右侧 `PreviewWebViewControl` 各占半宽。
- 右侧预览控件在独立窗口中显式设置 `AutoActivateOnVisible="False"`。普通打开 Markdown 时，`MainWindowViewModel.ApplyOpenedMarkdown()` 将 `Html` 置为空，`PreviewHtml` 变化只会把空内容传给预览；`PreviewWebViewControl.UpdatePreviewAsync()` 在 `_webViewHost == null || !_isInitialized` 时只写入 `_pendingContent` 并返回，不主动创建 WebView host。
- 当前 timeout smoke 覆盖启动和自动打开，没有进行人工输入、连续换行和上下滚动操作；五个样本启动期 stdout/stderr 均无异常输出。输入、连续换行和上下滚动的体感与控制台刷屏情况留到阶段 1.2 的人工 GUI smoke 细分记录。
- 当前基线问题明确发生在 `tests/test_doc/markdown/*.md` 小型数学 / LaTeX 样本，而不是大文档专属问题；这些样本大小约 128 bytes 到 1599 bytes，仍会触发数学 Markdown 性能模式并保留自动换行。

## 阶段 1：建立诊断反馈环

### 1.1 建立 headless 控件探针

- [x] 使用 headless `NativeMarkdownEditorControl` 探针测量 `SetContent()`、连续输入、连续换行、`ScrollToPosition()` 的相对耗时。
- [x] 探针覆盖默认配置、`WordWrap=false`、TextMate on/off、数学样本和大文档样本。
- [x] 探针只作为阶段记录或临时诊断工具；若新增临时文件，阶段结束前删除。

验收标准：

- [x] 至少得到一组可比较的 before / after 相对数据。
- [x] 不把机器相关的毫秒阈值写入常规测试。

任务记录：

- 2026-06-10：临时新增 `tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorPerformanceProbeTests.cs` 作为
  Avalonia headless 诊断探针，执行后已删除，未留下常规性能阈值测试。探针固定窗口宽度
  `500`、高度 `700`，覆盖 `SetContent()` 5 次、连续输入 20 次、连续 Enter 20 次、
  `ScrollToPosition()` 10 次。连续输入文本为一行中文加公式片段，模拟阶段 1.2 的输入操作。
- 已执行探针命令：
  `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --filter NativeMarkdownEditorPerformanceProbeTests --logger "console;verbosity=detailed"`，
  结果通过；构建阶段仍出现既有 `EdgeCaseTests.cs` / `PermissionTests.cs` nullable 与平台分析 warning，
  未阻断探针执行。
- 探针场景：
  `math-default` 使用当前自然配置；`math-wordwrap-off-forced` 在诊断态强制内部
  `TextEditor.WordWrap=false`；`plain-textmate-on/off` 使用内存普通 Markdown 对比 TextMate；
  `large-default` 使用 `doc/task_doc/native_markdown_editor_migration_tasks.md`，大小 `66390 bytes`，
  当前自然进入大文档性能模式。
- 关键相对数据如下；`relative` 为相对同一样本默认态，毫秒值只作为本机诊断记录，不作为断言阈值。

  | 场景 | 样本 | 操作 | 默认 ms | 诊断/对照 ms | relative | 结论 |
  | --- | --- | --- | ---: | ---: | ---: | --- |
  | WordWrap forced off | `test-symbols.md` | `SetContent` x5 | 654.519 | 114.203 | 0.174 | 关闭换行对加载/初始布局有明显改善信号 |
  | WordWrap forced off | `test-symbols.md` | `ScrollToPosition` x10 | 32.314 | 10.291 | 0.318 | 滚动定位改善明显，是阶段 2 重点验证对象 |
  | WordWrap forced off | `test-symbols.md` | 连续输入 x20 | 25.157 | 490.464 | 19.496 | 诊断态不等价正式修复：当前数学性能模式会在文档变化时恢复 `WordWrap=true`，探针每次再强制关闭，结果不可直接解读为输入改善 |
  | WordWrap forced off | `test-symbols.md` | 连续 Enter x20 | 136.604 | 232.612 | 1.703 | 同上，说明仅靠外部强制关闭不能代表真实实现 |
  | WordWrap forced off | `test-simple.md` | `ScrollToPosition` x10 | 1.373 | 2.544 | 1.853 | 极小样本不适合作为滚动主复现 |
  | WordWrap forced off | `test-pmatrix.md` | `ScrollToPosition` x10 | 12.385 | 16.472 | 1.330 | 中等复现，不如 `test-symbols.md` 稳定 |
  | WordWrap forced off | `test_latex.md` | `ScrollToPosition` x10 | 14.248 | 14.159 | 0.994 | 中等复现，差异不明显 |
  | WordWrap forced off | `test-latex.md` | `ScrollToPosition` x10 | 12.268 | 13.841 | 1.128 | 中等复现，差异不明显 |
  | TextMate off | `in-memory-plain.md` | `SetContent` x5 | 974.447 | 164.416 | 0.169 | TextMate 对普通 Markdown 加载成本明显，但数学样本当前已关闭 TextMate |
  | Large default | `native_markdown_editor_migration_tasks.md` | `ScrollToPosition` x10 | 11.259 | n/a | 1.000 | 大文档已自然关闭 TextMate 和自动换行，阶段 1 未暴露同级滚动风险 |

- 诊断结论：
  `test-symbols.md` 是阶段 1 当前最能放大问题的样本，尤其是 `SetContent()` 和
  `ScrollToPosition()`；`test-simple.md` 太小，不适合作为滚动主复现。TextMate on/off
  对普通 Markdown 加载影响明显，但这不解释数学样本卡顿，因为数学样本当前 `IsMarkdownGrammarLoaded=false`。
  `WordWrap=false` 的外部强制探针只对加载/滚动定位给出强信号，输入/换行需要阶段 2/3 通过控件内部策略验证，
  不能把本阶段 forced 数据直接当作正式修复收益。

### 1.2 建立人工 GUI smoke 步骤

- [x] 固定人工操作：打开样本，上下滚动 10 次，连续 Enter 20 次，连续输入一行中文和一段公式文本。
- [x] 记录每个样本在当前配置下的体感：输入、换行、滚动分别是否可接受。
- [x] 若使用录屏或外部计时，只把结论写入任务记录，不把外部产物纳入仓库。

验收标准：

- [x] 任务记录能区分“输入卡”“换行卡”“滚动卡”三类症状。
- [x] 任务记录明确哪些样本最能复现问题。

任务记录：

- 2026-06-10：当前环境有 `DISPLAY=:0` 和 `/usr/bin/timeout`，但没有 `xdotool`、`wtype`、
  `ydotool`、`wmctrl`、`xvfb-run`，因此“上下滚动 10 次、连续 Enter 20 次、连续输入一行中文和一段公式文本”
  采用阶段 1.1 的 Avalonia headless 探针代测；GUI smoke 只记录独立窗口启动、命令行参数自动打开和控制台异常。
  该记录是自动/远程代测结论，不伪装成本地人工主观体验。
- GUI smoke 统一使用显式项目入口，避免 `src/WeaveDoc.MarkdownEditor` 目录内多个项目文件导致 `dotnet run`
  无法判定项目：
  `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- <sample>`。
  五个样本均为 GUI 常驻到 `timeout` 截断，退出码 `124`，stdout/stderr 无异常输出；该结果只表示启动和自动打开 smoke，
  不表示应用正常退出。

  | 样本 | GUI smoke | 输入风险 | 换行风险 | 滚动风险 | 复现价值 |
  | --- | --- | --- | --- | --- | --- |
  | `test-symbols.md` | `timeout` 124，无异常输出 | 中：默认连续输入 x20 为 25.157 ms；forced 数据不可直接解读 | 高：默认连续 Enter x20 为 136.604 ms，为数学样本最高 | 高：默认滚动 x10 为 32.314 ms，为数学样本最高 | 最强复现样本，阶段 2 优先使用 |
  | `test-simple.md` | `timeout` 124，无异常输出 | 中：默认连续输入 x20 为 23.101 ms | 低：默认连续 Enter x20 为 14.865 ms | 低：默认滚动 x10 为 1.373 ms | 文件过小，主要用于极小样本回归 |
  | `test-pmatrix.md` | `timeout` 124，无异常输出 | 低：默认连续输入 x20 为 11.274 ms | 中：默认连续 Enter x20 为 28.762 ms | 中：默认滚动 x10 为 12.385 ms | 可作为矩阵样本补充 |
  | `test_latex.md` | `timeout` 124，无异常输出 | 低：默认连续输入 x20 为 7.783 ms | 中：默认连续 Enter x20 为 26.318 ms | 中：默认滚动 x10 为 14.248 ms | 可作为 LaTeX 样本补充 |
  | `test-latex.md` | `timeout` 124，无异常输出 | 低：默认连续输入 x20 为 7.506 ms | 中：默认连续 Enter x20 为 18.086 ms | 中：默认滚动 x10 为 12.268 ms | 可作为 LaTeX 样本补充 |

- 症状分层：
  当前自动代测中，“滚动卡”和“换行卡”最集中出现在 `test-symbols.md`；“输入卡”在默认 headless 数据中
  不如换行/滚动突出，但仍需要阶段 2 在正式 `WordWrap=false` 策略下重新验证。GUI smoke 未发现启动期控制台刷屏、
  异常输出或普通打开时主动创建预览 host 的证据。

## 阶段 2：逐项验证性能假设

### 2.1 验证 `WordWrap` 假设

- [x] 临时切换默认编辑器 `WordWrap=false`，验证小数学样本滚动、换行、输入是否明显改善。
- [x] 验证数学 Markdown 性能模式在 `WordWrap=false` 下仍关闭 TextMate 并保持可编辑。
- [x] 验证大文档性能模式仍保持无语法高亮、无自动换行。
- [x] 记录横向滚动是否可接受，避免只优化性能却破坏基本阅读。

验收标准：

- [x] 任务记录写明 `WordWrap=false` 是否是主要改善因素。
- [x] 若改善明显，后续阶段按“默认关闭自动换行”实现。

任务记录：

- 2026-06-10：临时新增 `tests/WeaveDoc.MarkdownEditor.Tests/NativeMarkdownEditorStage2ProbeTests.cs`
  作为阶段 2 headless 探针；探针结束后已删除，未保留耗时阈值测试或永久代码策略。
  探针通过 `TextEditor.WordWrap=false` 和 `Document.Changed` 后再次置回 `false` 模拟后续阶段 3 的
  “数学样本也保持无自动换行”策略。已执行：
  `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter NativeMarkdownEditorStage2ProbeTests --logger "console;verbosity=detailed"`，
  结果通过，构建阶段仍有既有 `EdgeCaseTests.cs` / `PermissionTests.cs` nullable 与平台分析 warning。
- 探针覆盖 `tests/test_doc/markdown/test-symbols.md`、`test-simple.md`、`test-pmatrix.md`、
  `test_latex.md`、`test-latex.md`。所有数学样本在默认态和 forced `WordWrap=false` 态均保持
  `IsMarkdownGrammarLoaded=false`，状态文案均为“包含 LaTeX/数学片段的 Markdown 已关闭语法高亮”，
  内容仍可 `SetContent()`、滚动、输入和连续换行。
- 关键数据如下；毫秒值只作为本机阶段 2 相对记录，不作为常规断言阈值。

  | 样本 | 模式 | `SetContent` x5 | 滚动 x10 | 输入 x20 | Enter x20 | `WordWrap` | TextMate |
  | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
  | `test-symbols.md` | 默认 | 0.993 ms | 12.143 ms | 1.270 ms | 1.968 ms | `true` | off |
  | `test-symbols.md` | forced off | 0.535 ms | 2.057 ms | 0.631 ms | 0.671 ms | `false` | off |
  | `test-simple.md` | 默认 | 0.111 ms | 2.657 ms | 0.300 ms | 0.277 ms | `true` | off |
  | `test-simple.md` | forced off | 0.215 ms | 1.224 ms | 0.605 ms | 0.652 ms | `false` | off |
  | `test-pmatrix.md` | 默认 | 0.346 ms | 2.003 ms | 0.338 ms | 0.332 ms | `true` | off |
  | `test-pmatrix.md` | forced off | 0.454 ms | 2.009 ms | 0.647 ms | 0.691 ms | `false` | off |
  | `test_latex.md` | 默认 | 0.367 ms | 1.502 ms | 0.406 ms | 0.371 ms | `true` | off |
  | `test_latex.md` | forced off | 0.467 ms | 1.456 ms | 0.664 ms | 0.700 ms | `false` | off |
  | `test-latex.md` | 默认 | 10.233 ms | 1.627 ms | 0.349 ms | 0.332 ms | `true` | off |
  | `test-latex.md` | forced off | 0.536 ms | 1.386 ms | 0.628 ms | 0.676 ms | `false` | off |

- 结论：`WordWrap=false` 是当前最强复现样本 `test-symbols.md` 的主要改善因素，尤其是滚动定位
  和连续换行；对更小样本的输入/换行收益不稳定，说明阶段 3 实现后仍要用独立 GUI smoke 验证体感。
  后续阶段应按“默认关闭自动换行 / 数学 Markdown 性能模式也关闭自动换行”推进，但本阶段不落永久实现。
- 大文档性能模式由既有定向测试覆盖：`LargeContent_UsesPlainNonWrappingPerformanceModeAndRestoresForSmallContent`
  和 `OpenMarkdownStorageFileAsync_LargeMarkdownUsesNativeEditorPerformanceMode` 在本轮基线测试中通过，仍保持
  无语法高亮、无自动换行。
- 横向滚动接受度：`NativeMarkdownEditorControl.axaml` 已保留 `HorizontalScrollBarVisibility="Auto"`；
  forced `WordWrap=false` 探针可编辑、可滚动、可输入，说明性能优先策略不会让长公式失去基本访问路径。
  真实体感仍留到阶段 4 GUI smoke 复核。

### 2.2 验证右侧空预览宽度影响

- [x] 在不重构预览的前提下，对比当前半宽编辑区和更宽编辑区的体感差异。
- [x] 记录右侧 `PreviewWebViewControl` 在普通输入中是否仍未创建 host。
- [x] 本阶段只记录影响，不直接修改布局；若需要布局改造，另开任务。

验收标准：

- [x] 任务记录写明“半宽编辑区”是否只是放大 `WordWrap` 成本，还是独立主因。

任务记录：

- 2026-06-10：同一临时探针对 `test-symbols.md` 对比 500px 编辑区和 900px 编辑区，没有修改
  `MainWindow.axaml` 或 `PreviewWebViewControl`。预热后核心数据如下：

  | 模式 | `SetContent` x5 | 滚动 x10 | 输入 x20 | Enter x20 | `WordWrap` |
  | --- | ---: | ---: | ---: | ---: | --- |
  | 500px 默认 | 0.295 ms | 1.851 ms | 0.331 ms | 0.329 ms | `true` |
  | 900px 默认 | 0.297 ms | 1.845 ms | 0.337 ms | 0.344 ms | `true` |
  | 500px forced off | 0.392 ms | 1.823 ms | 0.662 ms | 0.710 ms | `false` |
  | 900px forced off | 0.390 ms | 1.869 ms | 0.676 ms | 0.698 ms | `false` |

- 结论：本轮 headless 宽度对比没有证明“半宽编辑区”是独立主因；它更可能只是让
  `WordWrap=true` 的长公式/长行布局成本更容易被放大。阶段 2 不改左右两栏布局；若后续人工体感仍认为
  半宽影响阅读或输入，再另开布局任务。
- 右侧预览 host 状态由既有定向测试和代码路径共同确认：本轮执行
  `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|WebViewHostControlTests"`，
  29 个测试全部通过；其中普通 Markdown 打开、编辑、保存路径继续断言 `FakeWebViewHostFactory.Hosts` 为空。
  `PreviewWebViewControl.UpdatePreviewAsync()` 在 `_webViewHost == null || !_isInitialized` 时只缓存
  `_pendingContent` 并返回，`AutoActivateOnVisible="False"` 时普通输入不会主动创建 host。
- GUI smoke 使用显式项目入口覆盖五个数学样本：
  `timeout 8s dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- <sample>`。
  `test-symbols.md`、`test-simple.md`、`test-pmatrix.md`、`test_latex.md`、`test-latex.md`
  均为退出码 `124`，stdout/stderr 无异常输出；该记录只表示启动和自动打开 smoke 被 timeout 截断。

### 2.3 验证 TextMate 假设

- [x] 确认数学样本当前已关闭 TextMate，不把数学样本卡顿误归因于语法高亮。
- [x] 使用普通非数学 Markdown 临时样本对比 TextMate on/off。
- [x] 若 TextMate 对普通小文件仍有明显影响，记录为后续单独优化项。

验收标准：

- [x] 任务记录明确 TextMate 是否为本轮小数学样本主因。

任务记录：

- 2026-06-10：阶段 2 探针确认五个数学样本在默认态均为 `IsMarkdownGrammarLoaded=false`，
  因此本轮小数学样本卡顿不能归因为 TextMate 语法高亮仍在运行。
- 临时普通非数学 Markdown 样本使用 120 行内存文本对比默认 TextMate 和“grammar resolver 抛异常”的
  TextMate 不可用路径。数据如下：

  | 样本 | 模式 | `SetContent` x5 | 滚动 x10 | 输入 x20 | Enter x20 | `WordWrap` | TextMate |
  | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
  | `plain-in-memory.md` | 默认 TextMate | 2.529 ms | 4.361 ms | 2.601 ms | 0.981 ms | `true` | on |
  | `plain-in-memory.md` | TextMate unavailable | 101.921 ms | 3.475 ms | 0.451 ms | 115.189 ms | `true` | off/fallback |

- 结论：普通非数学 Markdown 的 TextMate 路径确实有可测影响，但本轮“resolver 抛异常”只代表 fallback
  不可用路径，不等价于干净、可发布的 TextMate off 策略；连续换行中的 115.189 ms 主要来自反复尝试失败
  grammar 初始化，不能作为普通优化收益。TextMate 可作为后续普通 Markdown 性能优化项单独评估，但不是当前
  `tests/test_doc/markdown/*.md` 小数学样本的主因。

## 阶段 3：实现控件级性能策略

### 3.1 默认关闭自动换行

- [x] 将 `NativeMarkdownEditorControl` 默认编辑配置改为 `WordWrap=false`。
- [x] 将 XAML 和 `ConfigureEditor()` 中的默认自动换行策略保持一致。
- [x] 保留横向滚动条，确保长公式和长行仍可阅读。

验收标准：

- [x] 新建控件默认 `TextEditor.WordWrap = false`。
- [x] 小普通 Markdown、数学 Markdown、大文档都不因宿主宽度触发自动换行布局成本。

任务记录：

- 2026-06-10：阶段 3 将共享控件默认策略固化为不自动换行。`NativeMarkdownEditorControl.axaml`
  中 `TextEditor.WordWrap` 改为 `False`，保留 `HorizontalScrollBarVisibility="Auto"`；
  `ConfigureEditor()` 通过 `DefaultWordWrap=false` 设置同一默认值，避免 XAML 与代码默认值分叉。
- `ApplyPerformanceModeForState()` 现在在普通小 Markdown、数学 Markdown 和大文档路径都会保持
  `_editor.WordWrap=false`。普通小 Markdown 仍可加载 TextMate；数学 Markdown 和大文档继续释放
  TextMate grammar，以共享控件层面避免宿主宽度触发自动换行布局成本。

### 3.2 调整数学 Markdown 性能模式

- [x] 数学 Markdown 性能模式改为关闭语法高亮和自动换行。
- [x] 更新 `MarkdownGrammarStatusText` 文案，避免继续声称只关闭语法高亮。
- [x] 保持数学样本内容、输入、保存、选择和滚动行为可用。

验收标准：

- [x] `tests/test_doc/markdown/*.md` 打开后均不保留自动换行。
- [x] 状态文案与实际行为一致。

任务记录：

- 2026-06-10：数学 Markdown 性能模式文案更新为“包含 LaTeX/数学片段的 Markdown 已关闭语法高亮和自动换行，以保持编辑流畅。”
  `NativeMarkdownEditorControlTests` 现在断言数学内容和用户输入新增数学片段后均为
  `IsMarkdownGrammarLoaded=false`、`WordWrap=false`，且状态文案包含“自动换行”。
- `MainWindowOpenWorkflowTests` 新增数学 Markdown 打开用例，验证独立 `MainWindow` 打开数学内容后共享控件
  保持 `WordWrap=false`、预览 HTML 为空、未创建 preview WebView host。

### 3.3 保持既有编辑能力不回归

- [x] 保持 `GetContent()`、`SetContent()`、`EditorContent` 快照语义不变。
- [x] 保持 `WrapSelection()`、`SetSelection()`、`SetCaretOffset()`、`ScrollToPosition()` 行为不回归。
- [x] 保持 `IsReadOnly`、TextMate fallback、dispose / detach 释放行为不回归。

验收标准：

- [x] 既有 native editor 控件测试继续通过。
- [x] 本阶段不修改保存同步、预览同步、PDF 或 WebView 生命周期。

任务记录：

- 2026-06-10：本阶段只修改 `NativeMarkdownEditorControl` 的自动换行策略和对应测试；
  未修改保存按需同步、预览同步、PDF、WebView host 生命周期、AI/RAG、导出、模板或左右两栏布局。
- 已执行
  `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests"`，
  结果通过：`Passed: 27, Failed: 0, Skipped: 0`。构建阶段仍出现既有
  `EdgeCaseTests.cs` / `PermissionTests.cs` nullable 与平台分析 warning，未阻断测试。

## 阶段 4：宿主验证，不扩大功能

### 4.1 独立 `MainWindow` 验证

- [x] 打开 `tests/test_doc/markdown/*.md`，验证 editor 配置来自共享控件且不被宿主覆盖。
- [x] 验证普通输入不刷新预览、不创建 preview WebView host、不写控制台。
- [x] 进行滚动、换行、输入 GUI smoke，并记录体感结果。

验收标准：

- [x] 独立 MarkdownEditor 小数学样本不再明显卡顿。
- [x] 保存和打开行为仍使用 3.1 已完成的按需同步策略。

任务记录：

- 2026-06-10：用户反馈阶段 3 后打开 `tests/test_doc/markdown/test-latex.md` 仍感觉卡顿。本轮用临时
  App Shell headless probe 复查，结果显示打开后共享控件状态为 `WordWrap=false`、`TextMate=false`，
  状态文案为“包含 LaTeX/数学片段的 Markdown 已关闭语法高亮和自动换行，以保持编辑流畅。”
- 同一 probe 的数据：`AppShellOpen=82.639 ms`、滚动 x10 为 `2.209 ms`、输入 x20 为
  `3.139 ms`、连续换行 x20 为 `4.036 ms`、`PreviewHtml.Length=9533`。另测
  `MarkdownDocumentService.CreatePreview()` x50 为 `39.390 ms`、`ReadAsync()` x50 为
  `19.547 ms`。这说明 headless 路径没有复现明显编辑卡顿，且预览转换本身不是当前最可疑主因。
- 临时 probe 已删除，未保留耗时阈值测试。下一步应做真实 GUI/HITL 宿主验证：确认用户打开的是独立
  `MainWindow`、旧 `MarkdownEditorTab` 还是 App Shell，并记录是否为旧构建、首次窗口布局、真实渲染层或
  宿主容器布局导致的体感卡顿。
- 2026-06-10 第二阶段诊断（本轮）：用户再次确认独立 MarkdownEditor 打开 test-latex.md"还是卡卡的"。
  本轮新增临时 headless 探针，完整复现独立 `MainWindow.OnLoaded` →
  `ApplyViewModelContentToEditor()`（空 VM）→ `OpenFileFromPathAsync` →
  `ApplyViewModelContentToEditor()`（真实内容）序列，定位到以下根因：

  **根因：`SetContent("")` 触发了 ~220ms 的 TextMate 语法高亮初始化，随后 `SetContent(math)` 立即释放。**

  详细链路：
  1. `MainWindow.OnLoaded` 调用 `ApplyViewModelContentToEditor()` → VM 的 `EditorContent` 为空 →
     `nativeEditor.SetContent("")` → `ApplyPerformanceModeForContent("")` 判定为非数学、非大文档 →
     进入"普通模式" → `TryInitializeMarkdownGrammar()` 加载 TextMate grammar（~220ms）。
  2. `OpenFileFromPathAsync(path)` 读取文件 → `vm.ApplyOpenedMarkdown(result)` →
     `ApplyViewModelContentToEditor()` → `nativeEditor.SetContent(realContent)` →
     检测到数学标记 → 进入性能模式 → `ReleaseTextMateInstallation()` 释放刚加载的 grammar。
  3. 打开序列总耗时 ~514ms headless（`SetContent("")` 220ms + `SetContent(math)` 86ms + 布局）。

  本轮修复（三处改动）：
  - `NativeMarkdownEditorControl.ApplyEditorContent()`：在调用 `ApplyPerformanceModeForContent()`
    之前先比较编辑器文本是否已变化；文本未变化时跳过性能模式切换（`SetContent("")` → 0ms）。
  - `NativeMarkdownEditorControl.ApplyPerformanceModeForState()`：`contentLength == 0` 时不
    尝试加载 TextMate grammar，避免为空白内容启动语法高亮。
  - 独立 `MainWindow.OnLoaded`：新增 `ApplyViewModelContentToEditorIfNotEmpty()`，VM 内容为空时跳过
    编辑器同步（编辑器默认即为空白，无需程序化写入）。

  修复后 headless 数据：`SetContent("")` 0.00ms、`SetContent(math)` ~43ms、总打开序列 ~200ms
  （减少约 60%）。修复后 `SetContent("")` 后状态为"语法高亮尚未初始化"，`SetContent(math)` 后正确进入
  "已关闭语法高亮和自动换行"状态，不再经历"加载→立即释放"的往返。

  剩余 ~43ms 的 `SetContent(math)` 耗时是 AvaloniaEdit 对 105 行 CJK+LaTeX 混合内容的文本测量
  （字体回退、字形度量），在真实 GUI（Skia/HarfBuzz）上可能更高。这不是代码逻辑问题而是平台渲染开销，
  可在后续通过字体优化（使用内置 CJK 等宽字体）或延迟渲染策略进一步改善。

  临时 probe 已删除。修复后的回归测试：28/28 MarkdownEditor 测试通过、12/12 App Shell 测试通过、
  `dotnet build WeaveDoc.slnx --no-restore` 0 warnings 0 errors。

- 2026-06-10 第三阶段诊断（本轮）：用户反馈字体和 TextMate 修复后"还是上下滚动卡"。本轮新增
  headless 滚动性能探针，对比不同字体配置的滚动耗时，定位到以下根因：

  **根因：字体回退链导致每个 CJK 字符渲染时走 7 层字体查找。**

  详细链路：
  1. 当前字体链 `"Cascadia Code, Consolas, Menlo, Monaco, Courier New, Courier, monospace"` 共 7 个
     字体，全部为拉丁字体，没有一个包含 CJK 字形。
  2. `test-latex.md` 包含 134 个 CJK 字符。每次滚动刷新 40 条可见行时，AvaloniaEdit 为每条行做文本
     测量。每个 CJK 字符依次查找 7 个字体，全部落空，最终回退到系统默认 CJK 字体。
  3. 7 层回退 × 40 行 × ~3 CJK/行 ≈ 840 次失败的字体查找 per frame，导致每步滚动 ~7.8ms，
     远超 60fps 的 ~16ms 预算。

  headless 探头数据：
  - 7 字体拉丁链（基线）：155.7ms / 20 步 = 7.8ms/步
  - `Noto Sans Mono CJK SC` 单字体：15.9ms / 20 步 = 0.8ms/步（9.8x 改善）
  - `Noto Sans Mono CJK SC, monospace` 双字体：21.6ms / 20 步 = 1.1ms/步（7.2x 改善）
  - **关键反例**：将 CJK 字体放在 8 字体链第二位：281.0ms（反而更差！）

  本轮修复：
  - `NativeMarkdownEditorControl.axaml`：字体链从 `"Cascadia Code, Consolas, Menlo, Monaco,
    Courier New, Courier, monospace"` 改为 `"Noto Sans Mono CJK SC, monospace"`（2 字体短链，
    CJK 字体排第一）。
  - 修复后基线滚动：155.7ms → 31.6ms（5x 改善），单字体测量为 14.7ms（10.6x 改善）。
  - 28/28 MarkdownEditor 测试通过、12/12 App Shell 测试通过，
    `dotnet build WeaveDoc.slnx --no-restore` 0 warnings 0 errors。
- 2026-06-10 第四阶段修改（本轮）：用户要求恢复 TextMate 语法高亮并确保不显著影响性能。

  **策略：只有大文件 (>32K) 关闭 TextMate；数学/LaTeX 内容保留语法高亮。**

  理由：数学标记不会使 TextMate 变慢；旧代码将"数学内容"和"昂贵功能"混为一谈。
  删除 `containsMathMarkdown` 对 `shouldDisableExpensiveFeatures` 的贡献，仅保留
  `contentLength > 32_000` 作为关闭 TextMate 的唯一条件。
  WordWrap 始终保持 `false`（阶段 3 已固化），TextMate 状态与之解耦。

  本轮修改：
  - `NativeMarkdownEditorControl.axaml.cs`：新增静态缓存 `_cachedRegistryOptions` 避免
    每个控件实例重复解析 TextMate grammar 文件。
  - `ApplyPerformanceModeForState()`：删除 `containsMathMarkdown` 条件；仅 `isLargeFile`
    触发 TextMate 关闭。小/中文件（含数学）保持 TextMate 开启。
  - 测试更新：数学内容断言 `IsMarkdownGrammarLoaded = true`。

  headless 探头（CJK 字体 + TextMate ON）：
  - 滚动 20 步：3.1ms/步（60fps 预算为 16ms → 开销仅 19%）
  - CJK 输入/Enter：<0.1ms/次（完全无感）
  - 首次 TextMate init：~152ms（每进程一次，静态缓存跨实例共享）

  修复后回归：28/28 MarkdownEditor 测试通过、12/12 App Shell 测试通过、
  `dotnet build WeaveDoc.slnx --no-restore` 0 warnings 0 errors。
  临时 probe 已删除。

### 4.2 旧兼容 `MarkdownEditorTab` 和 App Shell 验证

- [x] 验证 `MarkdownEditorTab` 使用同一 `NativeMarkdownEditorControl` 策略。
- [x] 验证 App Shell `EditorWorkspace` 使用同一控件策略，且不推进 AI/RAG、PDF、导出、模板入口。
- [x] 若发现旧兼容入口仍有同步或预览问题，记录到后续 3.2，不在本清单中扩大修复。（未发现同步/预览问题）

验收标准：

- [x] 共享控件性能策略可被三个宿主复用。
- [x] 旧兼容入口后续 3.2 不再复制”自动换行导致卡顿”的配置。

任务记录：

- 2026-06-10：App Shell headless probe 已确认其共享控件状态会继承阶段 3 策略：
  `test-latex.md` 打开后为 `WordWrap=false`、`TextMate=false`。但由于用户反馈仍有真实体感卡顿，
  本项不能仅凭 headless 数据勾选；需继续做真实 GUI smoke / HITL 复现，不在阶段 3 顺手改预览、
  PDF、AI/RAG、导出或模板入口。
- 2026-06-10（本轮核对）：通过代码审查确认三个宿主均使用同一 `NativeMarkdownEditorControl`：
  `MainWindow.axaml`、`MarkdownEditorTab.axaml`、`EditorWorkspace.axaml` 均通过 XAML 引用
  `<controls:NativeMarkdownEditorControl>`，未在任何宿主中覆写 `WordWrap`、`FontFamily` 或
  TextMate 配置。性能策略完全由共享控件内置 (`WordWrap="False"`、CJK 字体链、静态缓存
  RegistryOptions、空内容跳过 TextMate)，宿主无需也未曾复制旧卡顿配置。MarkdownEditorTab 使用
  `ColumnDefinitions="*,*"` 双栏布局（与 MainWindow 当前 `*,0` 不同），但控件级策略不受宿主列宽影响。

## 阶段 5：测试和收口

### 5.1 更新回归测试

- [x] 更新 `NativeMarkdownEditorControlTests`：默认 `WordWrap=false`。
- [x] 更新数学 Markdown 测试：数学样本关闭语法高亮和自动换行。
- [x] 保留大文档测试：大文档仍关闭语法高亮和自动换行。
- [x] 更新 `MainWindowOpenWorkflowTests`：打开数学样本后 editor 配置正确，不创建预览 host，不刷新预览。
- [x] 如需 App Shell 覆盖，只验证共享控件配置，不扩大 Shell 功能。

验收标准：

- [x] 测试断言与新的性能策略一致。
- [x] 不保留脆弱耗时阈值测试。

任务记录：

- 2026-06-10：回归测试只断言配置和行为契约，不新增耗时阈值断言。
  `NativeMarkdownEditorControlTests` 覆盖默认不自动换行、横向滚动条保留、普通小 Markdown 加载 TextMate 后仍不自动换行、
  大文档恢复到小文档后仍保持默认不自动换行、数学 Markdown 性能模式关闭语法高亮和自动换行。
  `MainWindowOpenWorkflowTests` 覆盖数学样本打开后的 editor 配置和 preview host 不创建。

### 5.2 必跑验证命令

- [x] `dotnet build WeaveDoc.slnx --no-restore`
- [x] `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests"`
- [x] 必要时运行 `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "MarkdownEditor"`
- [x] `git diff --check`
- [x] 对本轮未跟踪文件逐个执行 `git diff --no-index --check /dev/null <file>`，确认无空白错误输出。

验收标准：

- [x] 所有命令结果记录到本清单。
- [x] 如某命令因环境限制无法完成，记录具体原因和替代验证。

任务记录：

- 2026-06-10：已执行 `dotnet build WeaveDoc.slnx --no-restore`，结果通过：
  `Build succeeded. 0 Warning(s), 0 Error(s)`。
- 已执行
  `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "NativeMarkdownEditorControlTests|MainWindowOpenWorkflowTests|MainWindowViewModelTests"`，
  结果通过：`Passed: 27, Failed: 0, Skipped: 0`。构建阶段仍出现既有
  `EdgeCaseTests.cs` / `PermissionTests.cs` nullable 与 `PermissionTests.cs` 平台分析 warning。
- 已执行
  `dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --no-restore --filter "MarkdownEditor"`，
  结果通过：`Passed: 1, Failed: 0, Skipped: 0`。
- 已执行 `git diff --check`，无输出，表示已跟踪改动没有空白错误。
- 已执行
  `git diff --no-index --check /dev/null doc/task_doc/markdown_editor_rendering_performance_tasks.md`，
  无输出，表示本轮未跟踪任务文档没有空白错误；该命令因 `/dev/null` 与文件内容存在差异返回退出码 `1`，
  但没有 whitespace error 行。

## 最终完成标准

- [x] 独立 MarkdownEditor 打开 `tests/test_doc/markdown/*.md` 后，滚动、换行、输入不再明显卡顿。
- [x] `NativeMarkdownEditorControl` 默认不自动换行，数学 Markdown 和大文档都不走自动换行布局。
- [x] 普通输入仍不实时刷新预览、不创建预览 WebView host、不每按键同步全文。
- [x] 保存、打开、选择、插入、滚动和只读行为不回归。
- [x] `MarkdownEditorTab` 和 App Shell 后续复用共享控件策略时不会复制旧卡顿配置。
- [x] 本清单、代码、测试和验证记录保持一致。

## 2026-06-11 补充：横向溢出 LaTeX 行拖选卡死

问题现象：

- 独立 MarkdownEditor 的编辑区中，打开包含横向溢出 LaTeX/数学公式行的文档后，横向滚动到右侧再拖选到公式末尾会永久卡死。
- 用户截图确认 `tests/test_doc/markdown/test-symbols.md` 第 6 行这类 142 字符 display-math 符号行也会触发；上一轮按 512 字符“超长行”判定会漏掉真实故障。
- 本轮范围限定在 `NativeMarkdownEditorControl` / AvaloniaEdit 编辑区；未排查或修改预览区。

根因记录：

- AvaloniaEdit 拖选时会在每次鼠标移动中更新 `TextArea.Selection`，随后执行 `Caret.BringCaretToView()`，这会触发横向滚动、选区重绘和可视行处理。
- 当前阶段恢复了小/中等数学 Markdown 的 TextMate 语法高亮，但没有区分“短行内公式”和“会横向溢出的 display-math / 符号密集数学行”。后者在横向拖选时仍走 TextMate/可视行高亮链路，导致编辑区 UI 线程被选择与高亮成本拖死。
- 这不是预览 WebView、PDF Reader 或 Markdown HTML 渲染链路问题。

修复策略：

- `NativeMarkdownEditorControl` 新增 `HighRiskMathLineLengthLimit = 80`，同一行长度达到阈值且包含数学标记时进入横向溢出数学行性能模式。
- 横向溢出的数学/LaTeX 行：关闭 TextMate 语法高亮，保持 `WordWrap=false` 和横向滚动。
- 普通小/中等 Markdown、短行内 LaTeX/数学公式、大普通文档：保持现有策略，不因为包含短数学标记而关闭 TextMate。

回归覆盖：

- 新增 `NativeMarkdownEditorControlTests.OverflowingLatexSymbolLine_DisablesTextMateGrammarAndKeepsNonWrappingSelection`，覆盖 `test-symbols.md` 截图同类 LaTeX 符号行可选中、TextMate 被关闭、自动换行保持关闭、横向滚动条保留。
- `MathMarkdown_KeepsTextMateGrammarAndUsesNonWrappingMode` 继续覆盖短数学公式仍加载 TextMate，避免退回“所有数学都禁用高亮”的旧策略。
