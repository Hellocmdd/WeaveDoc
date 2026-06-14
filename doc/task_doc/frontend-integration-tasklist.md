# WeaveDoc 前端集成任务清单

> 分支：`refactor/frontend-shell`
> 目标：将 Rag、Markdown 编辑器、Converter 三个后端模块接入 App Shell 前端界面
> 日期：2026-06-12
> **进度（2026-06-14 更新）**：工作线 0–4 全部实现并编译通过。仅余 0.3.4（WorkspaceSidebar 翻页/缩放对接 PDF）、3.5 引文校验（后端缺口）、Converter 端到端测试等待办。

---

## 现状概览

| 模块 | 后端状态 | 前端状态 | 阻塞点 |
|------|---------|---------|--------|
| Markdown 编辑器 | `NativeMarkdownEditorControl` + `PreviewWebViewControl` 已可用 | 编辑区已嵌入 `EditorWorkspace`，但打开/保存/新建按钮全部禁用 | 按钮未绑定，缺少 `NewAsync()` 方法 |
| Converter | `DocumentConversionEngine` + `ConfigManager` 完整可用 | `ConvertTab`、`TemplateTab` 两个 UserControl 已写好，但**未出现在 MainWindow 中** | 服务实例创建了但没传下去 |
| RAG | `LocalAiService` 完整可用（问答/语料管理/检索调试） | `RagTabViewModel` 已写好，但**没有对应的 View**，AI 面板三个 Tab 全是空占位 | `LocalAiService` 未在启动时创建 |

### 服务管线断裂

`Program.cs` 创建了 `ConfigManager` 和 `DocumentConversionEngine` → 传给 `App` → 传给 `MainWindow` 构造函数 → **但 MainWindow 第 33 行直接 `new AppShellViewModel()` 丢弃了这两个参数**，所有子控件拿不到服务。

---

## 工作线总览

```
工作线 0：Shell 布局修正（P0-，必须最先完成）
    │
    ↓
工作线 1：服务管线（P0，其他线都依赖它）
    │
    ├─→ 工作线 2：Markdown 编辑器（打开→编辑→保存→导出）
    ├─→ 工作线 3：Converter 接入（左侧栏改造 + 转换/模板面板嵌入）
    └─→ 工作线 4：RAG 接入（AI 面板三个 Tab 接活）
```

> 工作线 0 → 1 必须顺序执行；工作线 2 / 3 / 4 互不依赖，可以并行推进。

---

## 工作线 0：Shell 布局修正

> **优先级**：P0-（在工作线 1 之前完成，修正已有的布局设计问题，避免后续在错误结构上堆功能）
> **涉及文件**：`MainWindow.axaml`、`MainWindow.axaml.cs`、`EditorWorkspace.axaml`、`AiAssistantPanel.axaml`
> **状态**：✅ 0.1–0.4 已实现并编译通过（0.3.4 按计划推迟到工作线 2/3）；建议运行 GUI 做最终手动验收。

### 0.1 删除 Command Bar 中多余的「打开PDF」段落

当前 Command Bar 中「文件」区已有「打开」按钮，「PDF」区又单独放了一个「打开PDF」按钮，功能重复。

- [x] `MainWindow.axaml`：删除 `OpenPdfShellButton`（Column 8）及其前后的标签 `TextBlock "PDF"`（Column 7）和分隔线 `Border`（Column 9）
- [x] 相应缩减 Command Bar 的 `Grid.ColumnDefinitions`（20 列 → 17 列）
- [x] 调整后续列的 `Grid.Column` 索引（辅助区、设置、搜索、主题等按钮依次前移）

### 0.2 统一「打开」按钮，支持 .md 和 .pdf 双路由

- [x] `MainWindow.axaml`：`OpenShellDocumentButton` 的 `IsEnabled` 改为 `True`，并绑定 `Click="OnOpenDocumentClick"`
- [x] `MainWindow.axaml.cs`：实现 `OnOpenDocumentClick` / `OpenDocumentAsync`
  - 文件选择器同时接受 `*.md` 和 `*.pdf`（`FileTypeFilter` 包含两种）
  - 根据扩展名路由：
    - `.md` → 调用 `DocumentWorkspace.OpenAsync()` → 中间编辑区加载
    - `.pdf` → 调用 `PdfWorkspace.ShowPdfAsync()` → 左侧栏显示（见 0.3）
- [x] `MainWindow.axaml.cs`：删除独立的 `OnOpenPdfClick` 和 `OpenPdfFileAsync` 方法（及 `_temporaryPdfFilePath` 字段），逻辑合并到统一打开流程中；临时文件清理交由 `PdfWorkspace` 自管

### 0.3 PDF 查看器从中间编辑区移到左侧栏

当前 `PdfWorkspace` 作为覆盖层嵌在 `EditorWorkspace.axaml` 的 `Grid.Row="2"` 中，打开 PDF 会遮住 Markdown 编辑区，语义混乱。

**0.3.1 从 EditorWorkspace 中移除 PdfWorkspace**

- [x] `EditorWorkspace.axaml`：删除 `<views:PdfWorkspace>` 元素（约第 203-206 行）
- [x] `EditorWorkspace.axaml.cs`：移除与 PdfWorkspace 相关的引用（如有）—— 经核查无相关引用，无需改动

**0.3.2 将 PdfWorkspace 移入 MainWindow 左侧栏**

- [x] `MainWindow.axaml`：将 Column 0 从单独的 `WorkspaceSidebar` 改为容器（如 `Grid`），内含：
  ```
  <Grid Grid.Column="0">
      <views:WorkspaceSidebar />                      <!-- 文档预览，默认显示 -->
      <views:PdfWorkspace IsVisible="{Binding IsPdfWorkspaceVisible}" />  <!-- PDF 模式覆盖 -->
  </Grid>
  ```
- [x] PDF 打开时：左侧栏显示 `PdfWorkspace`，隐藏 `WorkspaceSidebar`
- [x] PDF 关闭时（`ClosePdfMode`）：恢复显示 `WorkspaceSidebar`，隐藏 `PdfWorkspace`
- [x] `AppShellViewModel` 计算属性控制两侧可见性 —— 实际**复用既有** `IsMarkdownWorkspaceVisible` / `IsPdfWorkspaceVisible`（与建议的 `IsSidebarDocumentViewVisible` / `IsSidebarPdfViewVisible` 功能等价，未新增冗余别名）：
  - `WorkspaceSidebar.IsVisible = IsMarkdownWorkspaceVisible`（非 PDF 时 true）
  - `PdfWorkspace.IsVisible = IsPdfWorkspaceVisible`（PDF 时 true）

**0.3.3 调整 PdfWorkspace 事件订阅**

- [x] `MainWindow.axaml.cs`：`OnMainWindowLoaded` 中 `pdfWorkspace` 的查找路径从 `EditorWorkspaceControl.FindControl` 改为直接引用 MainWindow Column 0 中的 `PdfWorkspace` 实例（`x:Name="PdfWorkspaceControl"`）
- [x] `PdfWorkspace.axaml.cs`：确认 `OpenPdfRequested` 事件仍然能正确触发（无需改动）

**0.3.4 WorkspaceSidebar 的翻页/缩放控件对接 PDF**

- [ ] 后续任务（可在工作线 2/3 中完成）：将 `WorkspaceSidebar` 中已有的翻页按钮（`DocumentPreviousPageButton` / `DocumentNextPageButton`）和缩放按钮（`DocumentZoomOutButton` / `DocumentZoomInButton`）绑定到 `PdfViewerControl` 的命令，使它们在 PDF 模式下生效

### 0.4 AI 面板非问答 Tab 隐藏底部输入框

当前 `AiAssistantPanel.axaml` 的 Row 3（输入框 + 发送/清空按钮）是面板级别的，无论选「问答」「文献」「快照」都显示。输入框只有问答场景有意义。

- [x] `AiAssistantPanel.axaml`：Row 3 的 Border 添加 `IsVisible="{Binding IsAiChatTabSelected}"`（`IsAiChatTabSelected` 已存在于 `AppShellViewModel`）
- [x] 后续接入 RAG（工作线 4）时，每个 Tab 自带操作区（问答有输入框，文献有添加/删除按钮，快照只读），届时 Row 3 可完全移除 —— 工作线 4 已完成，面板级共享输入框已移除，三个 Tab 各自带操作区

### 计划外修复（本次随工作线 0 顺带完成）

验收工作线 0 时发现并修复的两个 Markdown 预览渲染缺陷（不在原清单内）：

- [x] **GFM 表格渲染** —— `MarkdigMarkdownRenderService` 的手写 AST 渲染器缺少 `Table` 分支，表格被当成段落平铺；已补 `<table>/<thead>/<tbody>/<tr>/<th>/<td>` 渲染 + 列对齐（预览 CSS 早已就绪）。新增测试 `RenderPreviewHtml_PipeTable_RendersTableHtml`。
- [x] **数学公式渲染（`$…$` / `$$…$$`）** —— `RelaxedMathInlineParser` 三处缺陷已修：① 公式内容切片丢最后一字（`end - openDollars` → `+1`）；② 同行相邻公式必须加空格（去掉 `pc == match` 限制）；③ 单行 `$$…$$` 现按 display 渲染。新增 4 个回归测试。

---

> **优先级**：P0（必须先完成，否则后续工作线全部无法运行）
> **涉及文件**：`Program.cs`、`App.axaml.cs`、`MainWindow.axaml.cs`、`AppShellViewModel.cs`
> **状态**：✅ 1.1–1.5 已实现并编译通过，0 个新增测试回归。

### 1.1 Program.cs 中创建 LocalAiService

- [x] 在 `Program.Main()` 中创建 `RagOptions` 实例（默认配置）—— `LocalAiService` 内部通过 `RagOptions.LoadFromEnvironment()` 读取，无需显式传入
- [x] 创建 `CloudApiSettings` 实例（默认配置，后续可在设置界面修改）—— `LocalAiService` 内部通过 `CloudApiSettings.Load()` 读取，`RagTabViewModel` 保留独立的 `CloudApiSettings` 用于 UI 绑定和同步
- [x] 创建 `LocalAiService` 实例
- [x] 将 `LocalAiService` 传入 `BuildAvaloniaApp`（新增第三个参数）

### 1.2 服务实例向下传递

- [x] `App` 构造函数新增 `LocalAiService` 参数，存为字段
- [x] `MainWindow` 构造函数新增 `LocalAiService` 参数
- [x] `AppShellViewModel` 构造函数新增参数，接收三个服务并暴露为属性：
  - `ConfigManager`（Converter 用）
  - `DocumentConversionEngine`（Converter 用）
  - `LocalAiService`（RAG 用）
- [x] 保留无参构造函数作为设计时回退（调用 `AppShellViewModel(workspace, null, null, null)`）

### 1.3 RagTabViewModel 获得服务引用

- [x] `AppShellViewModel` 创建并持有 `RagTabViewModel` 实例（仅当 `LocalAiService` 非 null 时）
- [x] `RagTabViewModel` 构造时接收 `LocalAiService`（构造器注入，不再内部 `new()`）
- [x] 暴露 `RagTabViewModel` 为公共属性，供 AI 面板 View 绑定

### 1.4 Converter 子控件获得服务引用

- [x] `AppShellViewModel` 暴露 `ConfigManager` 和 `ConversionEngine` 属性
- [x] `MainWindow.OnMainWindowLoaded` 中新增 `InjectConverterServices()` 方法，通过 `FindControl` 查找 `ConvertTabControl` / `TemplateTabControl` 并调用 `SetServices` / `SetConfigManager`
- 注：ConvertTab / TemplateTab 尚未嵌入可视化树（属工作线 3 P1），注入管线已就绪，待嵌入后自动生效

### 1.5 资源清理

- [x] `AppShellViewModel` 实现 `IDisposable`，`Dispose()` 中释放 `RagTabViewModel`
- [x] `RagTabViewModel.Dispose()` 不再释放注入的 `LocalAiService`（遵循"谁创建谁释放"原则）
- [x] `MainWindow.OnClosed` 中按序调用 `_viewModel.Dispose()` 和 `_aiService?.Dispose()`
- [x] `ConfigManager` 和 `DocumentConversionEngine` 未实现 `IDisposable`，其 SQLite 连接由连接池管理，无需显式释放

---

## 工作线 2：Markdown 编辑器完整流程

> **优先级**：P1
> **涉及文件**：`MainWindow.axaml`、`MainWindow.axaml.cs`、`EditorWorkspace.axaml`、`DocumentWorkspaceViewModel.cs`、`AppShellViewModel.cs`

### 2.1 文件打开

- [x] `MainWindow.axaml`：`OpenShellDocumentButton` 的 `IsEnabled` 改为 `True`，绑定 Click 事件
- [x] `MainWindow.axaml.cs`：实现 `OnOpenDocumentClick` —— 调用 `StorageProvider.OpenFilePickerAsync`（过滤 `*.md`）
- [x] 将选中文件路径传给 `DocumentWorkspace.OpenAsync()`
- [x] 验证：打开后编辑器自动加载内容（`EditorWorkspace.SyncDocumentSnapshotToEditor` 已有此逻辑）

### 2.2 文件保存

- [x] `MainWindow.axaml`：`SaveShellDocumentButton` 的 `IsEnabled` 绑定到 `DocumentWorkspace.CanSave`
- [x] `MainWindow.axaml.cs`：实现 `OnSaveDocumentClick` —— 调用 `DocumentWorkspace.SaveAsync()`；新建文档无路径时自动弹「另存为」
- [x] `EditorWorkspace.axaml.cs`：保存前先调用 `SyncEditorContentToWorkspace()` 将编辑器内容同步回 ViewModel（已改为 public）
- [x] `DocumentWorkspaceViewModel`：新增 `SaveAsAsync(filePath)` 方法，支持另存为流程

### 2.3 新建文件

- [x] `DocumentWorkspaceViewModel` 新增 `NewAsync()` 方法：清空 `Content`、`CurrentFilePath`，设置 `HasDocument = true`，`IsDirty = false`，`DisplayName = "未命名文档"`
- [x] `MainWindow.axaml`：`NewShellDocumentButton` 的 `IsEnabled` 改为 `True`，绑定 `Click="OnNewDocumentClick"`
- [x] `MainWindow.axaml.cs`：实现 `OnNewDocumentClick` —— 调用 `DocumentWorkspace.NewAsync()`

### 2.4 编辑器内工具栏按钮打通

- [x] `EditorWorkspace.axaml`：移除 `OpenDocumentButton` / `SaveDocumentButton`（与 Command Bar 重复，避免用户混淆）
- [x] 决定：编辑器内不再放「打开/保存」，统一由 Command Bar 操作

### 2.5 导出（与 Converter 联动）

- [x] `MainWindow.axaml`：`ExportShellDocumentButton` 的 `IsEnabled` 绑定到 `DocumentWorkspace.HasDocument`，绑定 `Click="OnExportDocumentClick"`
- [x] `MainWindow.axaml.cs`：实现 `OnExportDocumentClick` —— 当前实现为「另存为」（弹 Save As 对话框）；待 Converter 接入后可改为切换到 Converter Tab 并自动填充文件路径
- [x] 可选：如果 `DocumentWorkspace.CurrentFilePath` 非空，自动填充到导出入口 —— 工作线 3 改为对话框方案后，`OnExportDocumentClick` 已把 `workspace.CurrentFilePath` 作为 `sourcePath` 传入 `ExportDialog`（原计划的 `ConvertTab.MdPathBox` 随 ConvertTab 一并移除）

---

## 工作线 3：Converter 接入

> **优先级**：P1
> **涉及文件**：`ExportDialog.axaml(.cs)`（新建）、`SettingsDialog.axaml(.cs)`（新建）、`MainWindow.axaml(.cs)`
> **状态**：✅ 已按界面设计 demo 重新实现（对话框方案），编译通过，0 个新增回归。
>
> **方案修正说明**：初版曾把 Converter 做成左侧栏的「文档/转换/模板」Tab，但这违背了
> `doc/软件设计/界面设计_demo/app/page.tsx` 的设计——demo 里左侧栏是纯文档/PDF 预览区（无 Tab），
> Converter 的真正入口是工具栏**「导出」按钮 → `ExportDialog` 模态框**（选择排版模板 / 导出配置 / 进度），
> 模板管理在**「设置」按钮 → `SettingsDialog` 的「模板库」Tab**。已撤销侧栏 Tab 方案，改为对话框。

### 3.1 撤销侧栏 Tab 方案（恢复纯文档预览）

- [x] `WorkspaceSidebar.axaml` / `.cs` 还原为工作线 0/1 的纯文档预览（Toolbar + TabStrip + Canvas）
- [x] `AppShellViewModel.cs` 枚举还原为 `{ Documents, Settings }`，恢复 `IsSettingsTabSelected`
- [x] `MainWindow.axaml.cs` 删除 `InjectConverterServices()`（控件不再嵌入侧栏）

### 3.2 新建 ExportDialog（承载 Converter，忠实复刻 demo）

- [x] 新建 `Views/ExportDialog.axaml` + `.axaml.cs`：Avalonia `Window` + `ShowDialog`（模态），Shell 画刷统一深色，540×620
- [x] ① 选择排版模板：动态生成模板行（单选 + 名称/版本 + 「可导出」徽标），「管理模板」链接跳转 SettingsDialog 模板库 Tab
- [x] ② 导出配置：Word/PDF 格式切换、PDF 单栏/双栏 `PdfLayoutMode`、输出文件路径 + 浏览（`SaveFilePicker`）
- [x] ③ 引文校验：**后端无此能力，省略**（已知缺口）
- [x] ④ 进度：不确定 `ProgressBar` + 状态文本（待导出/转换中/完成/失败）+ 错误日志
- [x] 转换逻辑：`engine.ConvertAsync(mdPath, templateId, format, pdfLayoutMode, ct)` + 输出路径校验/移动 + 成功状态文案（复刻自 `ConvertTab.axaml.cs`）

### 3.3 新建 SettingsDialog（模板库接通，忠实复刻 demo）

- [x] 新建 `Views/SettingsDialog.axaml` + `.axaml.cs`：`Window` + `ShowDialog`，640×520
- [x] 5 个 Tab：通用 / 模型管理 / Zotero / **模板库** / 快照策略（与 demo 一致）
- [x] 「模板库」Tab：模板列表 + 导入/种子/刷新/删除（CRUD 接 `ConfigManager`，逻辑复刻自 `TemplateTab.axaml.cs`）
- [x] 其余 4 个 Tab：显示居中「该功能待接入」占位（模型管理属工作线 4）
- [x] 支持 `initialTab` 参数：ExportDialog「管理模板」可定位到「模板库」Tab

### 3.4 接通按钮

- [x] `MainWindow.axaml`：「设置」按钮 `IsEnabled` → `True`，`Click="OnOpenSettingsClick"`
- [x] `MainWindow.axaml.cs` `OnExportDocumentClick` 改为：确保文档存盘 → 弹 `ExportDialog(configManager, engine, currentFilePath)`
- [x] 新增 `OnOpenSettingsClick` → 弹 `SettingsDialog(configManager)`
- [x] 「保存」按钮 `OnSaveDocumentClick` **完全不动**（保存 = 存 .md，与导出无关）
- [x] 测试 `MainWindowTests` 更新：`SetupShellCommandButton` 从「待接入禁用」列表移除

### 关于现有 ConvertTab / TemplateTab

- `ConvertTab.axaml(.cs)` / `TemplateTab.axaml(.cs)` 及其测试**保留不动**（仍编译、测试仍通过），现已被对话框取代，后续可在独立清理 PR 中移除。

### 已知后端缺口（记录在案，本次不实现）

1. **无转换进度上报** → ExportDialog 用不确定进度条，不伪造步骤。
2. **无引文校验** → ExportDialog 省略该节。
3. **设置项无后端**（语言/字体/模型/Zotero/快照策略）→ SettingsDialog 非「模板库」Tab 显示占位。

### 3.5 后续可选优化

- [x] 转换成功后，如果输出是 PDF，提示用户是否直接在 PdfWorkspace 中打开
  - ExportDialog PDF 转换成功后，进度区显示「在 PDF 查看器中打开」按钮；点击后关闭对话框，MainWindow 读取 `ExportDialog.PendingOpenPdfPath` 并调用 `PdfWorkspaceControl.ShowPdfAsync(path, name, isTemporary:false)` 在左侧栏打开（侧栏切到 PDF 模式）
- [ ] 引文校验后端能力具备后，补回 ExportDialog 的「引文校验」节
  - **暂不做（已知后端缺口）**：`BibtexParser` 能解析 .bib 提取 `CitationKey`，但 `DocumentConversionEngine.ConvertAsync` 流程不碰引文校验，也无 .bib 来源（demo 里靠 Zotero 集成，现仍是占位）
- [x] 清理移除已取代的 `ConvertTab` / `TemplateTab`
  - 已删除 `ConvertTab.axaml(.cs)` / `TemplateTab.axaml(.cs)` 及测试 `ConvertTabTests.cs` / `TemplateTabTests.cs`；转换/模板逻辑已由 ExportDialog/SettingsDialog 复刻；`AppShellViewModel` doc 注释改指向新对话框

---

## 工作线 4：RAG 接入

> **优先级**：P1
> **涉及文件**：新建 3 个 View 文件 + 改造 `AiAssistantPanel.axaml` + `AiAssistantPanel.axaml.cs` + 流式后端
> **状态**：✅ 4.1–4.6 已实现并编译通过；新增 SSE 流式后端 + VM/前端测试，0 个新增回归。
>
> **与原任务描述的偏离（已确认的设计调整）：**
> 1. **流式输出**（用户新增要求）：`LlamaServerChatClient.StreamCompletionAsync` (SSE) + `LocalAiService.AskStreamAsync`，问答气泡逐字浮现；`AskAsync` 保留供评测。
> 2. **对话数据源**：用 `ObservableCollection<ChatTurn> Turns`（结构化气泡）取代 `ConversationText`（纯文本）。
> 3. **「快照」View 命名**：原写 `RagDebugView`，实际实现为 `RagSnapshotView`（语义=检索块结构化卡片，非纯调试文本）。
> 4. **云 API 设置位置**：原计划放 `RagCorpusView` 折叠面板，实际按用户决定放进 `SettingsDialog`「模型管理」Tab 的独立栏目（顶部「当前生效配置」横幅 + 推理后端单选）。

### 4.1 创建 RAG 对话 View

- [x] 新建 `Views/RagChatView.axaml` + `.axaml.cs`
- [x] 布局：上方对话记录（`ItemsControl` 绑定 `RagTabViewModel.Turns`，按 `IsUser` 分助手/用户气泡），下方输入框 + 发送按钮 —— 用 `Turns` 取代原计划的 `ConversationText`，支持逐字流式
- [x] 输入框绑定 `InputText`，发送按钮调用 `SendAsync`（Enter 发送 / Shift+Enter 换行）
- [x] 显示加载状态（`IsBusy`，发送按钮变「停止」，可取消）
- [x] 清空按钮调用 `ClearConversation`

### 4.2 创建语料管理 View

- [x] 新建 `Views/RagCorpusView.axaml` + `.axaml.cs`
- [x] 布局：
  - 语料文件列表（卡片 `ItemsControl` 绑定 `CorpusFiles` + 本地搜索过滤）
  - 选中项绑定 `SelectedDocument`
  - 添加文档按钮（文件选择器 → `AddDocumentFromPathAsync`）
  - 刷新按钮 → `RefreshCorpusAsync`
  - 删除按钮 → `DeleteSelectedDocumentAsync`
- [x] 显示状态（`StatusText`）和加载状态（`IsBusy`）；计数行显示文件数 + `CorpusChunkCount`

### 4.3 创建检索快照 View

- [x] 新建 `Views/RagSnapshotView.axaml` + `.axaml.cs`（原计划名 `RagDebugView`，重命名以匹配 demo「快照」语义）
- [x] 布局：
  - 顶部计数行（`LastRankedChunks` 个检索块 + `LastUsedSparsePrefilter`）
  - 检索块卡片（`ItemsControl`）：`Citation` 角标 + `FilePath · SectionTitle` + `ContentKind` + `Text` 截断
  - 检索调试原文（`RetrievalDebugText`，Expander 只读展开）
  - 未提问时空态

### 4.4 改造 AiAssistantPanel

- [x] `AiAssistantPanel.axaml`：Row 0 三 Tab（问答/文献/快照）+ 内容区三个子 View 堆叠（`RagChatView` / `RagCorpusView` / `RagSnapshotView`）
- [x] `AiAssistantPanel.axaml.cs`：
  - `OnLoaded` 取 `AppShellViewModel.RagTabViewModel` 赋为三个子 View 的 `DataContext`
  - 子 View 显隐由代码后端按 `SelectedAiPanelTab` 切换（非 XAML 绑定，避免 DataContext 覆盖导致全显堆叠）
- [x] 底部输入框：已内嵌进 `RagChatView`，面板级共享输入框移除

### 4.5 RAG 初始化流程

- [x] 首次切到问答 Tab / 输入框获得焦点时触发 `RagTabViewModel.InitializeAsync()`（`RagChatView` GotFocus 预热，幂等）
- [x] 初始化期间显示加载状态（`StatusText`）
- [x] 初始化失败写入 `StatusText`，不阻塞应用

### 4.6 云 API 设置

- [x] `SettingsDialog`「模型管理」Tab 增设「云 API」栏目（非 `RagCorpusView` 折叠面板，按用户决定）
- [x] 顶部「当前生效配置」横幅 + 推理后端单选（本地 llama-server / 云 API），显式标明当前在用哪个后端
- [x] 绑定 `CloudBaseUrl` / `CloudApiKey`(密码遮罩) / `CloudModel` / `CloudEnableThinking` / `CloudReasoningEffort`
- [x] 保存按钮调用 `SaveCloudSettings`

---

## 跨工作线收尾任务

> 所有工作线完成后的集成验证

### 验收清单

**工作线 0（Shell 布局修正）：**

- [x] Command Bar 中不再有独立的「打开PDF」按钮
- [x] 「打开」按钮可选择 .md 和 .pdf 文件，按扩展名自动路由到编辑器或 PDF 查看器
- [x] 打开 PDF 后查看器显示在左侧栏，中间编辑区保持不变（不切换为空/被覆盖）
- [x] 关闭 PDF 后左侧栏恢复为文档预览面板
- [x] AI 面板切换到「文献」或「快照」Tab 时，底部输入框隐藏

**工作线 1-4（模块接入）：**

- [x] 从「打开」按钮选择 .md 文件 → 编辑器加载内容 → 修改 → 「保存」写回文件
- [x] 编辑器中切换「编辑/预览」模式正常
- [x] 「导出」按钮 → `ExportDialog` 选模板 → 转换成功 → 输出文件可打开（PDF 可直接在左侧 PdfWorkspace 打开）—— 注：Converter 入口已由原计划的「侧栏转换 Tab」改为「导出」按钮模态框（见工作线 3 方案修正）
- [x] 「设置」→ `SettingsDialog`「模板库」Tab → 导入/删除模板正常 —— 注：模板管理已由原计划的「侧栏模板 Tab」改为设置对话框（见工作线 3）
- [x] AI 面板「问答」Tab → 输入问题 → RAG 流式返回答案（逐字浮现 + 可停止）
- [x] AI 面板「文献」Tab → 添加/删除/刷新语料文档
- [x] AI 面板「快照」Tab → 查看本次检索的结构化块 + 调试原文
- [x] 深色/浅色主题切换正常（含本轮修复：编辑器深色区禁用按钮可见性、PDF 左栏主题一致性、编辑器占位标签隐藏）
- [x] 无内存泄漏（关闭窗口时所有服务正确 Dispose，见工作线 1.5）

### 测试补充

- [x] `RagTabViewModel` 单元测试 —— `RagTabViewModelTests`（6 项：Turns/SendEnabled/Provider 规范化/清空/ActiveProviderSummary 跟踪）
- [x] `AppShellViewModel` 覆盖 —— 部分覆盖（`MainWindowTests` 启动装配 + `EditorChromeThemeTests` 主题切换/文档打开/编辑器标签显隐）
- [x] 服务管线注入集成测试 —— `MainWindowTests` 覆盖 `Program`→`App`→`MainWindow`→`AppShellViewModel` 装配 + AvaloniaEdit Fluent 主题加载
- [ ] Converter 接入后的端到端测试 —— `ExportDialog` 已可用但未补端到端转换测试（转换核心逻辑在 `WeaveDoc.Converter.Tests` 有覆盖）
