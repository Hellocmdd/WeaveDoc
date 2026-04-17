# WeaveDoc.Converter.Ui

WeaveDoc 转换工具的 Avalonia UI 桌面应用——提供模板管理和文档转换的可视化操作界面。

> **关联模块**：WeaveDoc.Converter（项目引用）
> **当前状态**：7 个 Headless UI 测试全部通过

---

## 技术栈

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| .NET / C# | 10 / 13 | 运行时与语言 |
| Avalonia | 11.* | 跨平台桌面 UI 框架 |
| Avalonia.Desktop | 11.* | 桌面平台支持 |
| Avalonia.Themes.Fluent | 11.* | Fluent Design 主题（浅色模式） |
| Avalonia.Fonts.Inter | 11.* | 默认字体 |
| Avalonia.Controls.DataGrid | 11.* | 模板列表展示 |
| WeaveDoc.Converter | — | 文档转换核心库（项目引用） |

---

## 架构

采用代码后置（Code-Behind）模式，不使用 MVVM 框架。依赖通过方法注入（`SetConfigManager` / `SetServices`）。

```text
┌─────────────────────────────────────────────────────┐
│                    MainWindow                        │
│              "WeaveDoc 转换工具" 900×580               │
│              Background="#F7F8FC" 品牌色 #4A6FA5       │
├────────────────────┬────────────────────────────────┤
│    ConvertTab      │         TemplateTab             │
│    文档转换（首Tab） │         模板管理                 │
├────────────────────┼────────────────────────────────┤
│  Card 1: 选择文件   │  标题区 + 副标题                 │
│  Card 2: 模板+格式  │  圆角 DataGrid 卡片              │
│  Card 3: 输出设置   │  刷新 / 导入 / 种子模板          │
│  转换按钮           │  行内删除按钮（选中后显示）        │
│  状态指示灯 + 日志   │  状态栏（模板总数）              │
└────────────────────┴────────────────────────────────┘
          │                         │
          ▼                         ▼
    ConfigManager         DocumentConversionEngine
    (Converter 库)         (Converter 库)
```

---

## 目录结构

```text
WeaveDoc.Converter.Ui/
├── Program.cs                    # 入口：创建依赖、种子模板、启动 Avalonia
├── App.axaml + App.axaml.cs      # 应用配置：Fluent 浅色主题 + 依赖注入
├── Views/
│   ├── MainWindow.axaml + .cs    # 主窗口：TabControl 双标签页
│   ├── TemplateTab.axaml + .cs   # 模板管理标签页
│   └── ConvertTab.axaml + .cs    # 文档转换向导标签页
└── WeaveDoc.Converter.Ui.csproj
```

---

## 页面说明

### TemplateTab（模板管理）

- **标题区** — "模板管理" 标题 + "管理和导入 AFD 样式模板" 副标题
- **DataGrid** — 圆角卡片包裹（CornerRadius="10"），展示模板列表（ID、名称、版本、作者）
- **操作按钮**：
  - 刷新 — 重新加载模板列表
  - 导入 — 从外部 JSON 文件导入模板
  - 种子模板 — 调用 `EnsureSeedTemplatesAsync` 发现内置模板（品牌色按钮）
- **行内删除按钮** — 选中模板后显示，两次点击确认删除（第一次"删除选中模板"→"确认删除?"）
- **状态栏** — 显示当前模板总数
- **依赖注入** — `SetConfigManager(ConfigManager)`

### ConvertTab（文档转换向导）

卡片式三步操作界面（白色卡片 + 编号圆标 + 品牌色 #4A6FA5）：

1. **选择 Markdown 文件** — 文件浏览对话框，`.md` 过滤
2. **选择模板与格式** — ComboBox 绑定模板列表 + Button 切换按钮（DOCX/PDF），默认 DOCX
3. **输出设置** — 文件夹浏览对话框

附加区域：

- **转换按钮** — 品牌色主按钮，调用 `DocumentConversionEngine.ConvertAsync()`
- **状态指示灯** — 8×8 彩色圆点 + 文字标签，实时显示转换状态
- **日志框** — 失败时自动显示转换过程详细日志
- **依赖注入** — `SetServices(ConfigManager, DocumentConversionEngine)`

### MainWindow

- **TabControl** 布局，两个标签页：文档转换（首Tab）、模板管理
- 构造函数接收 `ConfigManager` + `DocumentConversionEngine`，分别注入两个标签页

### 启动流程（Program.cs）

1. 初始化 SQLite 数据库（`data/weavedoc.db`）
2. 创建 `ConfigManager` + `PandocPipeline` + `SyncfusionPdfConverter` + `DocumentConversionEngine`
3. 执行 `EnsureSeedTemplatesAsync()`，发现内置模板
4. 启动 Avalonia 应用

---

## 快速使用

```bash
# 构建（自动下载 Pandoc）
dotnet build src/WeaveDoc.Converter.Ui

# 运行
dotnet run --project src/WeaveDoc.Converter.Ui
```

启动后自动发现内置的学术论文、课程报告、实验报告模板。
