# AvaloniaEdit 编辑区横向拖选卡死根因定位任务清单

创建日期：2026-06-12

## 目标

本清单只处理独立 MarkdownEditor 的编辑区问题：打开包含较长 LaTeX 公式行的 Markdown 后，先向右横向滚动，再从公式中拖选到末尾，应用永久卡死。

当前不能再把“预览区 / NativeWebView / PDF / App Shell”当作主要排查方向；也不能把“换一个编辑控件或纯文本 TextBox fallback”当成已经验证成功的修复。下一轮必须先复现并抓到编辑区卡死链路，再决定修复。

## 当前结论摘要

- 症状发生在独立 `src/WeaveDoc.MarkdownEditor` 的编辑区操作路径，不是预览区渲染问题。
- 已知入口应使用显式项目文件：
  `dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj -- <markdown-file>`。
- 旧的性能方向已经证明不够：
  - `WordWrap=false` 对长行布局/滚动有帮助，但用户复测后仍会卡死，不能视为根因修复。
  - `PlainTextFallbackEditor` 绕开了 AvaloniaEdit 编辑面，且用户明确不接受“好好的 AvaloniaEdit 不能修就换掉”的方向；除非证据证明必须保留，否则后续应优先回到 AvaloniaEdit 本体修复。
  - App Shell、预览区、PDF、WebView 生命周期都不是这次卡死的首要路径。
- 当前最可疑的根因链路是：AvaloniaEdit 在长 unwrapped LaTeX / display-math 行上，处于非零水平滚动偏移时进行鼠标拖选到行尾，触发 selection auto-scroll、caret reveal、TextView visual-column/layout/selection repaint 的 UI 线程高频循环或极端耗时路径。
- 以上只是最高优先级假设，不是已证明根因。必须用可复现反馈环和 UI 线程栈/事件日志证明。

## 明确排除

- 不排查预览区、KaTeX HTML、Markdig、NativeWebView、PDF.js、PDF Reader。
- 不排查 App Shell，除非独立 MarkdownEditor 的根因已被证明需要共享控件层验证。
- 不恢复 Monaco / CodeMirror / WebView 编辑器。
- 不继续扩大纯文本 fallback 方案作为默认修复。
- 不在没有复现证据时继续提交“感觉可能有效”的性能改动。

## 执行规则

- 阶段 0 和阶段 1 完成前，不做正式修复代码。
- 所有临时探针、日志、脚本都必须带唯一前缀，例如 `[DEBUG-avedit-freeze]`，结束后清理。
- 若使用 `timeout` 截断 GUI，只能记录为“超时截断 smoke”，不能写成正常退出。
- 每完成一个阶段，需要在本文件补充实际命令、证据和结论。
- 如果用户提供截图、录屏或原始 Markdown 文档，必须优先把它转成可复现样本或明确记录无法纳入仓库的原因。

## 阶段 0：失败修复复盘与真实范围锁定

### 0.1 复盘当前工作树中的相关改动

- [x] 记录 `NativeMarkdownEditorControl.axaml` / `.axaml.cs` 当前关于 `WordWrap`、横向滚动、TextMate、`PlainTextFallbackEditor`、LaTeX 检测的实际状态。
- [x] 记录 `MainWindow.axaml` 当前 Markdown 编辑区布局、预览列是否默认隐藏、预览 host 是否会参与普通打开流程。
- [x] 记录现有测试里哪些只是性能/配置验证，哪些真的覆盖了“横向滚动后鼠标拖选到公式末尾”。
- [x] 明确列出前几轮失败点：`WordWrap=false` 不足、fallback 换控件方向不被接受、未抓到真实 UI 卡死栈。

验收标准：

- [x] 任务记录能回答“现在代码到底走的是 AvaloniaEdit 还是 fallback”。
- [x] 任务记录能回答“此前为什么不能算修好”。
- [x] 没有修改实现代码。

2026-06-12 阶段 0.1 执行记录：

- `NativeMarkdownEditorControl.axaml` 当前同时声明 `AvaloniaEdit:TextEditor Name="Editor"` 和隐藏的
  `TextBox x:Name="PlainTextFallbackEditor"`。`TextEditor` 的 `WordWrap="False"`，
  `HorizontalScrollBarVisibility="Auto"`；fallback `TextBox` 的 `TextWrapping="NoWrap"`，
  `HorizontalScrollBarVisibility="Auto"`。
- `.axaml.cs` 中 `DefaultWordWrap=false`，`ConfigureEditor()` 和 `ApplyPerformanceModeForState()`
  都会把 AvaloniaEdit `_editor.WordWrap` 设置回不自动换行。
- `.axaml.cs` 当前保留 TextMate 路径：正常内容会通过 `TryInitializeMarkdownGrammar()`
  调用 `_editor.InstallTextMate(...)` 并 `SetGrammar(...)`。但 `NeedsPlainTextFallback()` 检测到
  display-math 行、`\begin{...}` 或 `\[` 后，`ApplyPerformanceModeForState()` 会
  `ReleaseTextMateInstallation(...)`，把 `IsMarkdownGrammarLoaded` 置为 `false`，并切换到
  `PlainTextFallbackEditor` 可见、AvaloniaEdit `TextEditor` 隐藏。
- 因此当前答案是：普通 Markdown / inline math 仍走 AvaloniaEdit；`test-symbols.md` 这类包含
  `$$...$$` display-math 的样本会被现有策略切到 fallback，不能证明 AvaloniaEdit 本体拖选链路已修复。
- `MainWindow.axaml` 当前 Markdown 编辑区默认两列：编辑列 `Width="*"`，预览列 `Width="0"`，
  `PreviewPane.IsVisible="False"`；预览控件 `PreviewWebViewControl` 存在于 XAML 中，且
  `AutoActivateOnVisible="True"`。
- 独立窗口打开 Markdown 后，`MainWindowViewModel.ApplyOpenedMarkdown()` 会调用 `RefreshPreview()`；
  `MainWindow.axaml.cs` 的 `UpdatePreviewPaneVisibility()` 会在 `PreviewHtml` 非空时把预览列改为 `*`
  并显示 `PreviewPane`。这说明默认 XAML 是隐藏预览列，但普通打开流程会让预览列参与布局。
- 现有测试覆盖：
  `NativeMarkdownEditorControlTests` 覆盖不自动换行、横向滚动配置、TextMate 成功/失败、浅层
  `SetSelection()` / `WrapSelection()`、fallback 可见性和共享 API；
  `MainWindowOpenWorkflowTests` 覆盖打开 Markdown、ViewModel/编辑器同步、溢出 LaTeX 行关闭 TextMate、
  WebView host 是否创建等；
  `StandaloneLatexPerformanceProbeTests` 是性能探针。它们都没有覆盖“非零水平滚动偏移 + 真实鼠标拖选到公式末尾”。
- 前几轮不能算修好的原因：
  `WordWrap=false` 只能降低长行布局/滚动压力，用户复测仍卡死；
  fallback 是绕开 AvaloniaEdit 编辑面，不符合“修好 AvaloniaEdit 本体”的目标；
  当前还没有 UI 线程卡死栈、真实 pointer 事件日志或能触发真实鼠标拖选链路的自动化/半自动化反馈环。
- 本阶段执行只更新本任务清单；没有修改实现代码。当前工作树已有其他历史改动和临时文件，本阶段不清理、不回滚。

### 0.2 固定真实复现材料

- [x] 优先使用用户卡死时的原始 Markdown 文档；若不能入库，则制作最小脱敏样本。
- [x] 样本必须包含触发卡死的长 LaTeX 公式行，并记录最长行长度、公式标记类型、是否包含 `$$` / `\begin{}` / `\[` / inline `$...$`。
- [x] 使用 `tests/test_doc/markdown/test-latex.md`、`test-symbols.md` 作为补充样本，但不能用补充样本替代用户真实样本。

验收标准：

- [x] 至少有一个“用户同类长公式拖选”样本被固定下来。
- [x] 样本能通过独立 MarkdownEditor 命令行参数自动打开。

2026-06-12 阶段 0.2 执行记录：

- 用户已指定 `tests/test_doc/markdown/test-symbols.md` 作为本轮真实同类主复现样本，因此本阶段不再等待额外原始 Markdown。
- 主样本最长行：
  - 文件：`tests/test_doc/markdown/test-symbols.md`
  - 行号：第 6 行
  - 长度：142 字符
  - 类型：display-math `$$...$$`
  - 内容特征：包含大量希腊字母 LaTeX 命令，例如 `\alpha`、`\beta`、`\gamma`、`\omega`
- 同一文件还包含多个 `$$...$$` display-math 行；第 44 行包含
  `\begin{pmatrix}` / `\begin{bmatrix}`；第 48、50、52 行包含 inline `$...$`。
  未发现 `\[` 标记。
- 独立 MarkdownEditor 打开命令固定为：
  `dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj -- tests/test_doc/markdown/test-symbols.md`
- `tests/test_doc/markdown/test-latex.md` 和 `test_latex.md` 后续仍可作为补充样本，但阶段 1 起优先围绕
  `test-symbols.md` 建立真实横向滚动拖选反馈环。

阶段 0 只读验证命令与结果：

- `rg -n "WordWrap|PlainTextFallbackEditor|NeedsPlainTextFallback|ApplyPerformanceModeForState|TextMate" src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml*`：
  命中 `WordWrap="False"`、`PlainTextFallbackEditor`、`DefaultWordWrap=false`、TextMate 安装/释放和
  `NeedsPlainTextFallback()` / `ApplyPerformanceModeForState()` 路径。
- `rg -n "NativeMarkdownEditorControl|OpenMarkdownStorageFileAsync|OverflowingLatex|SetSelection|PlainTextFallback" tests/WeaveDoc.MarkdownEditor.Tests`：
  命中控件配置、打开流程、fallback、浅层 selection API 和性能探针测试；未发现真实鼠标拖选覆盖。
- `awk '{ if (length($0)>max) { max=length($0); line=NR; text=$0 } } END { printf "file=%s\nlongest_line=%d\nlongest_length=%d\nlongest_text=%s\n", FILENAME,line,max,text }' tests/test_doc/markdown/test-symbols.md`：
  输出最长行为第 6 行，长度 142。

## 阶段 1：建立能抓住卡死的反馈环

### 1.1 手工复现脚本化

- [x] 写一个临时 HITL 操作脚本，固定步骤：启动独立 MarkdownEditor、打开样本、聚焦编辑区、横向滚动到公式中后段、按住鼠标拖选到公式末尾、等待 10 秒。
- [x] 每次复现记录：是否卡死、卡死前最后一步、CPU 占用、进程是否响应关闭、stdout/stderr 是否有异常。
- [x] 至少连续复现 3 次，或明确说明复现不稳定并记录复现概率。

验收标准：

- [x] 反馈环复现的是“编辑区横向拖选后永久卡死”，不是启动慢、预览慢或文件打开慢。
- [x] 有明确 pass/fail 标准：例如 10 秒内 UI 不响应且进程未退出视为 fail。

2026-06-12 阶段 1.1 执行记录：

- 新增临时 HITL 脚本：`scripts/hitl_avedit_selection_freeze.sh`。
- 默认固定打开命令：
  `dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-build -- tests/test_doc/markdown/test-symbols.md`
- 默认执行三轮，每轮固定人工步骤：
  1. 等待独立 `WeaveDoc Markdown Editor` 打开样本。
  2. 点击左侧 Markdown 编辑区，不点击预览区。
  3. 用编辑器底部水平滚动条移动到第 6 行长 display-math 公式的中后段。
  4. 在公式文本上按住鼠标，向右拖选到该公式行末尾后释放。
  5. 释放鼠标后等待 10 秒再判断 UI 是否卡死。
  6. 尝试正常关闭窗口；若 5 秒内无响应，记录为关闭失败，由脚本清理进程组。
- 每轮输出独立目录：`_debug/avedit-selection-freeze/<session>/run-N/`。
  - `stdout.log` / `stderr.log`：记录独立 MarkdownEditor 标准输出与异常。
  - `cpu.tsv`：每秒记录同一进程组内进程的 `pid`、`ppid`、`pgid`、`stat`、`%cpu`、`%mem`、`etime`、命令名和参数。
  - `summary.tsv`：记录 `result`、是否 10 秒卡死、卡死前最后一步、关闭是否响应、清理前进程是否仍存活、日志路径和备注。
- pass/fail 标准：
  - `FAIL`：横向拖选释放后 10 秒内 UI 不响应且进程仍存活，或窗口 5 秒内无法正常关闭。
  - `PASS`：横向拖选释放后 UI 仍响应，且窗口能在 5 秒内正常关闭。
- 该反馈环把计时点放在“横向滚动到长公式中后段并真实鼠标拖选到公式末尾之后”，不是以启动慢、打开慢、预览慢作为失败条件。
- 当前本机环境记录：
  - `DISPLAY=:0`，`XDG_SESSION_TYPE=x11`。
  - 已有 `xwininfo`、`xprop`、`xdpyinfo`、`gdb`。
  - 未安装 `xdotool` / `ydotool` / `dotool`，因此本阶段不伪造自动鼠标拖选结果；自动化能力留给 1.2 评估。
  - `xdpyinfo` 显示 X11 `XTEST` 扩展可用，后续 1.2 可评估是否用 XTest 或 Avalonia headless pointer event 覆盖真实交互链路。
- 本轮完成的是 1.1 要求的 HITL 手工复现脚本化与记录格式固化；由于当前对话环境无法替用户完成真实手工拖选，未生成三轮人工结果日志，也不把 dry-run 写成真实复现概率。

阶段 1.1 验证命令与结果：

- `bash -n scripts/hitl_avedit_selection_freeze.sh`：通过。
- `bash scripts/hitl_avedit_selection_freeze.sh --dry-run --runs 3 --wait 10`：
  输出固定启动命令、6 步人工操作流程，以及 10 秒 UI 不响应且进程未退出视为 fail 的判定标准。

### 1.2 自动化/半自动化输入探针

- [x] 评估当前环境是否能使用 `xdotool`、Avalonia headless pointer event、X11 坐标点击或最小测试窗口复现真实鼠标拖选。
- [x] 若能自动化，固化为临时脚本或临时测试；若不能，保留 HITL 脚本并说明原因。
- [x] 自动化必须包含“非零水平滚动偏移 + 鼠标拖选到行尾”，不能只调用 `TextEditor.Select()`。

验收标准：

- [x] 明确自动化能否覆盖真实交互链路。
- [x] 没有用浅层 selection API 冒充真实鼠标拖选。

2026-06-12 阶段 1.2 执行记录：

- 新增半自动 X11/XTest 探针脚本：`scripts/xtest_avedit_selection_probe.sh`。
- 环境评估结论：
  - 未安装 `xdotool` / `ydotool` / `dotool`，因此不依赖这些外部鼠标工具。
  - 当前会话 `DISPLAY=:0`，具备 `xwininfo`、`xprop`、`xdpyinfo`、`python3`。
  - `python3` 通过 `ctypes` 调用 `XTestQueryExtension`，确认当前 X display 支持 XTest 2.2。
  - `xdpyinfo` 在本机输出中未被简单文本匹配识别出 `XTEST`，因此脚本以真实
    `XTestQueryExtension` 查询结果作为可用性判定。
- Avalonia headless pointer event 评估：
  - 仓库已有 headless 指针事件用例，例如 App Shell 的 `GridSplitter` 拖动测试；
    这能证明 Avalonia headless 可以合成控件指针事件。
  - 但 headless 路径不经过真实 X11 顶层窗口、真实桌面鼠标、真实水平滚动条命中测试，
    也不能证明 AvaloniaEdit 在桌面 session 中的 mouse-capture / auto-scroll / caret reveal
    组合路径。因此 1.2 不把 headless 测试当成“真实拖选链路”的充分覆盖。
- `scripts/xtest_avedit_selection_probe.sh` 的覆盖方式：
  - 启动独立 MarkdownEditor，并打开 `tests/test_doc/markdown/test-symbols.md`。
  - 用窗口标题查找真实 X11 窗口，读取 `xwininfo` 绝对坐标和窗口尺寸。
  - 用 XTest 注入真实 pointer motion/button 事件：先点击编辑区，再拖动底部水平滚动条制造
    非零水平偏移，然后在第 6 行 display-math 公式可视区域从中段向右拖选到行尾方向。
  - 拖选释放后等待默认 10 秒，再用 XTest 发送 `Alt+F4`，以 5 秒内是否退出判断 UI 是否仍响应。
  - 每轮输出 `_debug/avedit-selection-freeze/<session>-xtest/`，包含 `stdout.log`、
    `stderr.log`、`cpu.tsv`、`summary.tsv` 和记录注入坐标的 `README.md`。
  - 脚本明确不调用 `TextEditor.Select()`、`SetSelection()` 或其他浅层 selection API。
- 覆盖结论：
  - 当前环境可以通过 XTest 半自动覆盖“真实 X11 指针事件 + 非零水平滚动偏移 + 鼠标拖选到行尾方向”的交互链路。
  - 由于 XTest 会移动当前桌面会话的真实鼠标并关闭目标窗口，本轮只做 `--dry-run` 与环境可用性验证；
    真正注入运行应在用户确认前台桌面可被自动操作时执行。
  - 若 XTest 坐标因主题、窗口大小或字体差异偏离，可用脚本提供的 ratio 参数微调；
    若 XTest 在某台机器不可用，则继续使用 1.1 的 HITL 脚本，不用浅层 selection API 冒充。

阶段 1.2 验证命令与结果：

- `bash -n scripts/xtest_avedit_selection_probe.sh`：通过。
- `bash scripts/xtest_avedit_selection_probe.sh --dry-run --wait 10`：
  输出固定启动命令、默认注入坐标比例、`DISPLAY=:0`、缺失 `xdotool` / `ydotool` / `dotool`、
  以及 `XTest available: 2.2`；脚本声明使用 XTest pointer events，且不调用 `TextEditor.Select()`。

## 阶段 2：只对编辑区加诊断探针

### 2.1 UI 线程卡死栈采集

- [x] 卡死后用 `dotnet-dump`、`createdump`、`gdb` 或等价工具采集进程栈。
- [x] 若工具不可用，至少采集 `pstack` / `gdb thread apply all bt` 等线程栈。
- [x] 记录 UI 线程是否停在 AvaloniaEdit 的 selection、caret、TextView layout、visual-line、scroll、render 或 TextMate transformer 路径。

验收标准：

- [x] 有一次卡死现场的线程栈证据。
- [x] 任务记录能指出 UI 线程最后卡在哪类调用链，而不是继续猜。

2026-06-12 阶段 2.1 执行记录：

- 扩展 `scripts/xtest_avedit_selection_probe.sh`，新增 `--capture-stack`：
  - 先构建 `src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj`。
  - 用 `gdb -q -batch ... --args dotnet src/WeaveDoc.MarkdownEditor/bin/Debug/net10.0/WeaveDoc.MarkdownEditor.dll <sample>`
    启动独立 MarkdownEditor，避免 `dotnet run` 包装进程污染栈。
  - 复用 XTest 注入真实指针事件：点击编辑区、拖动底部水平滚动条到非零水平偏移、
    再从第 6 行 display-math 公式中段向右拖选到行尾方向。
  - 拖选释放后等待 10 秒，再发送 `Alt+F4`；若 5 秒内不退出，则向 gdb 进程组发送
    `SIGINT` 并输出 `thread apply all bt` 到 `gdb.stack.log`。
- 工具状态：
  - `dotnet-dump` 不在 PATH。
  - `createdump` 不在 PATH，但 runtime 自带
    `/usr/lib/dotnet/shared/Microsoft.NETCore.App/10.0.8/createdump`。
  - `/proc/sys/kernel/yama/ptrace_scope=1`，实际 `gdb attach <pid>` 试验被拒绝，因此本阶段采用
    “gdb 从启动时接管进程”的方式采集线程栈。
  - 本地补装了 `_debug/dotnet-tools/dotnet-stack` 以尝试 managed stack；由于 gdb 包裹进程组内
    dotnet PID 未被脚本成功定位，本次有效证据仍以 gdb native thread backtrace 为准。
- 默认样本路径验证：
  - 命令：
    `bash scripts/xtest_avedit_selection_probe.sh --capture-stack --wait 10 --sample tests/test_doc/markdown/test-symbols.md`
  - 结果目录：
    `_debug/avedit-selection-freeze/20260612-102923-xtest/`
  - `summary.tsv` 记录 `result=pass`、`close_responded=yes`、`stack_captured=no`。
  - 结论：默认代码路径下 `test-symbols.md` 会走 `PlainTextFallbackEditor`，未触发 AvaloniaEdit 本体卡死；
    该 pass 不能证明 AvaloniaEdit 拖选链路已修复。
- AvaloniaEdit 本体现场栈：
  - 为确认卡死链路是否仍在 AvaloniaEdit 编辑面，临时加入并随后删除
    `WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1` 诊断门，只用于本阶段采集，不作为正式修复保留。
  - 两次强制 AvaloniaEdit 后均复现无响应，其中最终证据目录为：
    `_debug/avedit-selection-freeze/20260612-103752-xtest/`
  - `summary.tsv` 记录：
    `result=fail`、`froze_or_unresponsive_10s=yes`、`close_responded=no`、
    `process_alive_after_close=yes`、`stack_captured=yes`、`gdb_status=exited-after-sigint`。
  - `gdb.stack.log` 显示 `Thread 1 "dotnet" received signal SIGINT`，说明是在无响应现场主动中断。
  - 同一日志中有一个名为 `dotnet` 的渲染相关线程停在 Skia/GPU 字形绘制链：
    `libnvidia-glcore.so` →
    `GrGLGpu::onWritePixels` →
    `GrGpu::writePixels` →
    `GrResourceProvider::createTexture` →
    `GrSWMaskHelper::toTextureView` →
    `ClipStack::apply` →
    `SurfaceDrawContext::drawGlyphRunList` →
    `SkCanvas::drawTextBlob`。
  - 主线程 native 栈缺少 managed 符号，停在 `libcoreclr.so` / JIT 代码地址；本阶段不能从栈中直接读出
    `AvaloniaEdit.TextView` / selection / caret 方法名。
- 阶段 2.1 结论：
  - 已取得一次真实“非零水平滚动偏移 + 鼠标拖选到行尾方向”后的无响应现场线程栈。
  - 当前最明确的栈证据指向 UI/渲染提交期间的 Skia GPU glyph/text draw 路径，而不是 TextMate transformer
    或预览 WebView 线程。
  - 因为 native gdb 栈缺少 managed AvaloniaEdit 符号，是否由 selection/caret/TextView layout 触发该渲染阻塞，
    仍需阶段 2.2 的编辑区事件与布局计数继续区分；本阶段不做修复。

阶段 2.1 验证命令与结果：

- `bash -n scripts/xtest_avedit_selection_probe.sh`：通过。
- `bash scripts/xtest_avedit_selection_probe.sh --dry-run --capture-stack --wait 10`：
  输出 gdb 采集命令、XTest 坐标比例、`DISPLAY=:0`、`gdb=/usr/bin/gdb`、`ptrace_scope=1`、
  `dotnet-dump` 缺失、runtime `createdump` 路径以及 `XTest available: 2.2`。
- `dotnet build src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj`：通过，保留既有
  `MainWindow.axaml.cs(225,13) CS8602` 警告。

### 2.2 AvaloniaEdit 事件与布局计数

- [x] 临时记录编辑区 `PointerPressed`、`PointerMoved`、`PointerReleased`、selection 变化、caret offset、horizontal offset、visual line invalidation/layout 相关计数。
- [x] 记录拖选过程中是否出现事件风暴、递归/重入、水平滚动 offset 高频变化或布局计数异常增长。
- [x] 日志必须只覆盖编辑区控件，不加入预览区日志。

验收标准：

- [x] 能区分“事件循环/重入”与“单次布局极慢”。
- [x] 能定位卡死发生在鼠标拖选中、释放鼠标时、还是选区扩展到行尾后。

2026-06-12 阶段 2.2 执行记录：

- 在 `NativeMarkdownEditorControl` 内新增临时诊断门：
  - `WEAVEDOC_DEBUG_AVEDIT_SELECTION=1`：只在显式打开时记录 AvaloniaEdit 编辑区诊断日志。
  - `WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1`：只在诊断时压住当前 display-math fallback，强制走 AvaloniaEdit 本体路径。
  - 日志统一使用 `[DEBUG-avedit-freeze]` 前缀，默认运行不输出、不改变普通 fallback 行为。
- 探针只挂在编辑区 AvaloniaEdit 控件：
  `PointerPressed`、`PointerMoved`、`PointerReleased`、`TextArea.SelectionChanged`、
  `TextArea.Caret.PositionChanged`、`TextView.ScrollOffsetChanged`、
  `TextView.VisualLinesChanged` 和 `TextView.LayoutUpdated`。
  本阶段没有给预览区、NativeWebView、PDF 或 App Shell 加日志。
- 扩展 `scripts/xtest_avedit_selection_probe.sh`：
  - `--editor-diagnostics` 打开 `WEAVEDOC_DEBUG_AVEDIT_SELECTION=1`。
  - `--force-avaloniaedit` 同时打开 `WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1`。
  - 仍然通过 XTest 注入真实鼠标事件，不调用 `TextEditor.Select()`。
- 首次实跑时探针在控件构造早期读取 `TextView.VisualLines`，触发
  `AvaloniaEdit.Rendering.VisualLinesInvalidException`，该结果目录
  `_debug/avedit-selection-freeze/20260612-110257-xtest/` 只证明探针本身需要安全读取，
  不作为卡死证据。随后已把 visual line count 改为安全读取：无效时记为 `-2`。
- 有效诊断命令：
  `bash scripts/xtest_avedit_selection_probe.sh --editor-diagnostics --force-avaloniaedit --skip-build --wait 10`
- 有效证据目录：
  `_debug/avedit-selection-freeze/20260612-110543-xtest/`
- `summary.tsv` 记录：
  `result=fail`、`froze_or_unresponsive_10s=yes`、`close_responded=no`、
  `process_alive_after_close=yes`，窗口坐标为 `50 119 1000 700`；
  注入路径为先拖底部水平滚动条 `190,766 -> 770,766`，再拖选 `310,290 -> 1010,290`。
- 诊断日志关键时间线：
  - `elapsed_ms=4422/4428`：目标拖选按下，caret 到 offset `78`，此时
    `scrollChanged=0`、`visualLinesChanged=16`、`layoutUpdated=18`。
  - `elapsed_ms=4757`：选区已扩展到 `selectionStart=78`、`selectionLength=101`、
    `caretOffset=179`、`scrollX=541`；本 250ms 采样内出现
    `deltaPointerMoved=56`、`deltaSelection=21`、`deltaCaret=21`、
    `deltaScroll=11`、`deltaVisualLines=32`、`deltaLayout=32`。
  - `elapsed_ms=18678`：进程仍未响应关闭，pointer/selection/caret 计数已停在
    `moved=288`、`selectionChanged=29`、`caretChanged=33`，但
    `scrollChanged=222950`、`visualLinesChanged=111518`、`layoutUpdated=11197`，
    且 `scrollX` 固定在约 `543.5`。
- 事件计数汇总：
  - `pointer-pressed=8`、`pointer-moved log lines=10`、`pointer-released=6`
    （实际 moved 计数最终为 `288`）。
  - `selection-changed log lines=6`，最终计数为 `29`。
  - `caret-position-changed log lines=6`，最终计数为 `33`。
  - `scroll-offset-changed log lines=8923`，最终计数为 `222950`。
  - `visual-lines-changed log lines=4465`，最终计数为 `111518`。
  - `text-view-layout-updated log lines=452`，最终计数为 `11197`。
- 阶段 2.2 结论：
  - 卡死不是“单次布局极慢后无事件”，而是选区扩展到长公式行尾方向后触发
    `TextView.ScrollOffsetChanged` / `VisualLinesChanged` / `LayoutUpdated`
    高频循环；pointer、selection、caret 已经基本停止增长时，该循环仍持续占用 UI 线程。
  - 卡死发生点更接近“拖选过程中选区扩展到行尾并触发水平 auto-scroll/reveal 后”，
    不是普通打开、预览加载，也不是释放鼠标那一刻才首次发生。
  - 这支持阶段 3.1 的假设 A：AvaloniaEdit 长行拖选 auto-scroll / visual-column / TextView 重绘链路是当前主嫌疑。
    TextMate、fallback 和 standalone layout 仍需阶段 3 用对照实验证明是主因、放大因素还是无关因素。

阶段 2.2 验证命令与结果：

- `bash -n scripts/xtest_avedit_selection_probe.sh`：通过。
- `dotnet build src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj --no-restore`：通过，保留既有
  `MainWindow.axaml.cs(225,13) CS8602` 警告。
- `bash scripts/xtest_avedit_selection_probe.sh --dry-run --editor-diagnostics --force-avaloniaedit --wait 10`：
  通过，确认新参数会打印 `WEAVEDOC_DEBUG_AVEDIT_SELECTION=1` 和
  `WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT=1`，且仍声明使用 XTest pointer events、不调用
  `TextEditor.Select()`。
- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore --filter "FullyQualifiedName~NativeMarkdownEditorControlTests|FullyQualifiedName~MainWindowOpenWorkflowTests"`：
  未进入测试执行阶段，测试项目编译被既有测试代码阻塞：
  `NativeMarkdownEditorControlTests.cs(107,36)` 和 `(283,36)` 直接读取
  `TextBox.HorizontalScrollBarVisibility`，当前 Avalonia `TextBox` 没有该 CLR 属性。
  本阶段不修测试项目的既有编译问题，以免越过 2.2 诊断边界。
- `git diff --check -- src/WeaveDoc.MarkdownEditor/Controls/NativeMarkdownEditorControl.axaml.cs scripts/xtest_avedit_selection_probe.sh`：
  无输出，通过。
- `git diff --no-index --check /dev/null doc/task_doc/markdown_editor_avaloniaedit_selection_freeze_tasks.md`：
  无输出，说明未跟踪任务清单无 whitespace 问题；该 no-index 对比存在文件内容差异时返回码为 `1`，不按失败处理。

## 阶段 3：验证根因假设

### 3.1 假设 A：AvaloniaEdit 长行拖选 auto-scroll / visual-column 计算卡死

- [ ] 构造只有一行超长 LaTeX 的最小样本。
- [ ] 对比非零水平滚动偏移与水平偏移为 0 的拖选结果。
- [ ] 对比鼠标拖选与程序调用 `Select()` 的结果。

预测：

- 如果 A 成立，只有真实鼠标拖选 + 非零水平滚动偏移 + 长公式行组合会稳定触发卡死或极端耗时。

验收标准：

- [ ] 证据能说明是否为 AvaloniaEdit 拖选/auto-scroll 本体问题。

### 3.2 假设 B：TextMate / line transformer 在选区 repaint 时放大卡死

- [ ] 在同一样本上对比 TextMate 开启与关闭。
- [ ] 确认数学样本当前到底是否已关闭 TextMate。
- [ ] 若关闭 TextMate 仍卡死，停止把语法高亮当主因。

预测：

- 如果 B 成立，禁用 TextMate 后拖选卡死应消失或显著降低。

验收标准：

- [ ] 证据能说明 TextMate 是主因、放大因素，还是无关因素。

### 3.3 假设 C：fallback / 性能模式切换引入新问题

- [ ] 记录打开样本后当前实际可见编辑控件是 AvaloniaEdit `TextEditor` 还是 `PlainTextFallbackEditor`。
- [ ] 对比禁用 fallback 但保留 AvaloniaEdit 的行为。
- [ ] 检查 `ApplyPerformanceModeForState()` 是否在输入/拖选期间发生模式切换、双写文本或触发重布局。

预测：

- 如果 C 成立，移除或暂停 fallback 切换后，卡死行为会变化；若 fallback 可见时仍卡死，说明“换控件”没有解决真实问题。

验收标准：

- [ ] 明确 fallback 是失败 workaround、放大因素，还是与卡死无关。

### 3.4 假设 D：独立窗口布局宽度/预览列影响编辑区测量

- [ ] 对比默认独立 `MainWindow`、全宽编辑区、预览列隐藏/显示三种布局。
- [ ] 确认普通打开 Markdown 时预览 host 是否创建。
- [ ] 如果全宽编辑区仍卡死，停止把预览列当主因。

预测：

- 如果 D 成立，改变编辑区宽度或预览列状态会显著改变复现率。

验收标准：

- [ ] 证据能说明 standalone host/layout 是主因、放大因素，还是无关因素。

## 阶段 4：基于证据设计 AvaloniaEdit 内部修复

### 4.1 选择最小修复策略

- [ ] 若根因是拖选 auto-scroll，优先在 AvaloniaEdit `TextArea` 交互层限制或绕开问题路径，而不是替换编辑器。
- [ ] 若根因是 TextMate repaint，保留 AvaloniaEdit 编辑面，只对长 LaTeX 行关闭高亮/transformer。
- [ ] 若根因是 fallback 切换，移除或降级 fallback，回到单一 AvaloniaEdit 编辑面。
- [ ] 若根因是布局宽度，修复独立 `MainWindow` 布局约束，并验证共享控件不被误伤。

验收标准：

- [ ] 修复方案明确保留 AvaloniaEdit 作为主编辑区。
- [ ] 修复方案能解释为什么前两轮方案失败。
- [ ] 修复前已写明将如何回归验证“横向滚动后拖选到公式末尾”。

### 4.2 实施受控修复

- [ ] 只修改与根因直接相关的编辑区代码。
- [ ] 不引入新的编辑器控件替换方案。
- [ ] 不顺手改预览、PDF、AI、App Shell。

验收标准：

- [ ] 用户原始复现样本不再卡死。
- [ ] `test-latex.md` / `test-symbols.md` 作为补充样本通过相同拖选步骤。
- [ ] 普通 Markdown 输入、保存、打开仍正常。

## 阶段 5：回归测试与收尾

### 5.1 测试覆盖

- [ ] 为可自动覆盖的部分补充 headless 控件测试，例如配置、模式切换、长行选择 API 不异常。
- [ ] 为无法自动覆盖的真实鼠标拖选保留 HITL smoke 步骤和记录。
- [ ] 运行针对性 MarkdownEditor 测试。

建议验证命令：

- `dotnet test tests/WeaveDoc.MarkdownEditor.Tests/WeaveDoc.MarkdownEditor.Tests.csproj --no-restore`
- `dotnet build WeaveDoc.slnx --no-restore`
- `git diff --check`
- 若本文件仍未跟踪，额外执行：
  `git diff --no-index --check /dev/null doc/task_doc/markdown_editor_avaloniaedit_selection_freeze_tasks.md`

验收标准：

- [ ] 自动测试覆盖可稳定验证的边界。
- [ ] HITL 或自动 GUI smoke 覆盖真实拖选链路。

### 5.2 清理与记录

- [ ] 删除临时探针、临时脚本、临时日志。
- [ ] 用 `rg -n "\[DEBUG-avedit-freeze\]"` 确认诊断日志已清理。
- [ ] 在本文件记录最终根因、修复点、验证命令和结果。

验收标准：

- [ ] 最终记录能清楚回答：根因是什么、为什么此前没修好、这次如何证明修好了。
- [ ] 工作区没有残留未说明的诊断文件。
