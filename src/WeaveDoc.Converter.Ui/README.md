# WeaveDoc.Converter.Ui

WeaveDoc 转换工具的 Avalonia UI 桌面应用——提供模板管理和文档转换的可视化操作界面。

## 技术栈

- **.NET 10** / C# 13
- **Avalonia UI 11.x** — 跨平台桌面 UI 框架
- **Avalonia.Themes.Fluent** — Fluent Design 主题（浅色模式）
- **Avalonia.Fonts.Inter** — 默认字体
- **Avalonia.Controls.DataGrid** — 模板列表展示

## 架构

采用代码后置（Code-Behhind）模式，不使用 MVVM 框架。依赖通过构造函数注入。

```
┌──────────────────────────────────────────────┐
│                  MainWindow                  │
│         (TabControl 双标签页布局)              │
├──────────────────┬───────────────────────────┤
│   TemplateTab    │       ConvertTab          │
│   模板管理       │       文档转换向导          │
├──────────────────┼───────────────────────────┤
│ DataGrid 展示    │ Step 1: 选择 Markdown     │
│ 刷新/种子/导入   │ Step 2: 选择模板           │
│ 删除操作         │ Step 3: 导出格式           │
│ 状态栏           │ 输出目录 + 转换 + 日志     │
└──────────────────┴───────────────────────────┘
         │                    │
         ▼                    ▼
   ConfigManager      DocumentConversionEngine
   (Converter 库)     (Converter 库)
```

## 目录结构

```
WeaveDoc.Converter.Ui/
├── Program.cs                    # 入口：创建依赖、种子模板、启动 Avalonia
├── App.axaml + App.axaml.cs      # 应用配置：Fluent 浅色主题
├── Views/
│   ├── MainWindow.axaml + .cs    # 主窗口：TabControl 双标签页
│   ├── TemplateTab.axaml + .cs   # 模板管理标签页
│   └── ConvertTab.axaml + .cs    # 文档转换向导标签页
└── WeaveDoc.Converter.Ui.csproj
```

## 页面说明

### TemplateTab（模板管理）

- **DataGrid** 展示模板列表（ID、名称、版本、作者）
- **操作按钮**：刷新、种子模板（`EnsureSeedTemplatesAsync`）、导入、删除
- **状态栏**：显示当前模板总数
- **依赖注入**：`SetConfigManager(ConfigManager)`

### ConvertTab（文档转换向导）

向导式三步操作界面：

1. **选择 Markdown 文件** — 文件浏览对话框，支持 `.md` 过滤
2. **选择模板** — ComboBox 绑定模板列表，`DisplayMemberBinding="{Binding TemplateName}"`
3. **导出格式** — DOCX / PDF 单选按钮，默认 DOCX

附加区域：
- **输出目录** — 文件夹浏览对话框
- **转换按钮** — 调用 `DocumentConversionEngine.ConvertAsync()` 执行转换
- **状态标签** — 实时显示转换状态
- **日志框** — 显示转换过程详细日志
- **依赖注入**：`SetServices(ConfigManager, DocumentConversionEngine)`

### MainWindow

- **TabControl** 布局，两个标签页：模板管理、文档转换
- 构造函数接收 `ConfigManager` + `DocumentConversionEngine`，分别注入两个标签页

## 快速使用

```bash
# 构建
dotnet build src/WeaveDoc.Converter.Ui

# 运行（需要系统安装 Pandoc）
dotnet run --project src/WeaveDoc.Converter.Ui
```

启动后自动执行 `EnsureSeedTemplatesAsync()`，发现内置的学术论文、课程报告、实验报告模板。

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| Avalonia | 11.* | 跨平台 UI 框架 |
| Avalonia.Desktop | 11.* | 桌面平台支持 |
| Avalonia.Themes.Fluent | 11.* | Fluent Design 主题 |
| Avalonia.Fonts.Inter | 11.* | Inter 字体 |
| Avalonia.Controls.DataGrid | 11.* | 数据表格控件 |
| WeaveDoc.Converter | — | 文档转换核心库（项目引用） |
