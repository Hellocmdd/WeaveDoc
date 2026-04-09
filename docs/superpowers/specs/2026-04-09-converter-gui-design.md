---
title: Converter GUI 设计规格
date: 2026-04-09
wbs: WBS 3 语义转换系统
owner: 任逸青
status: draft
---

# WeaveDoc.Converter.Ui 设计规格

## 1. 问题陈述

WBS 3（语义转换系统）的后端逻辑已全部实现并通过测试，但仅有 CLI/API 层面可用。需要一个简易 GUI 演示工具，让团队成员和评审者直观体验模板管理和文档转换功能。

## 2. 设计决策

| # | 决策点 | 选择 | 理由 |
|---|--------|------|------|
| D1 | GUI 定位 | 简易演示工具 | 非正式产品，内部功能验证和演示 |
| D2 | 功能范围 | 全量 WBS 3 覆盖 | 模板管理 + 文档转换 + 配置管理 |
| D3 | UI 框架 | Avalonia UI | 项目计划书已确定，跨平台 |
| D4 | 窗口布局 | Tab 标签页 | 两个标签：模板管理 / 文档转换 |
| D5 | 编辑器 | 不需要 | Markdown 编辑器非 WBS 3 职责 |
| D6 | 架构模式 | Code-behind | 轻量，不引入 MVVM 框架 |
| D7 | 项目结构 | 独立项目 | `src/WeaveDoc.Converter.Ui/`，引用 Converter 库 |
| D8 | 转换流程 | 向导式 | 选 MD → 选模板 → 选格式 → 转换 → 打开结果 |

## 3. 项目结构

```
src/WeaveDoc.Converter.Ui/
├── WeaveDoc.Converter.Ui.csproj      # Avalonia UI 项目
├── Program.cs                        # 入口点
├── Views/
│   ├── MainWindow.axaml              # 主窗口 + TabControl
│   ├── MainWindow.axaml.cs
│   ├── TemplateTab.axaml             # 模板管理标签页 (UserControl)
│   ├── TemplateTab.axaml.cs
│   └── ConvertTab.axaml              # 文档转换标签页 (UserControl)
│       ConvertTab.axaml.cs
└── Assets/                           # Avalonia 资源（图标等）
```

## 4. 界面设计

### 4.1 主窗口 MainWindow

```
┌─────────────────────────────────────────────┐
│  WeaveDoc 转换工具                        _ □ X │
├─────────────────────────────────────────────┤
│  [模板管理]  [文档转换]                      │
│─────────────────────────────────────────────│
│                                             │
│   ← TemplateTab 或 ConvertTab 内容          │
│                                             │
└─────────────────────────────────────────────┘
```

- 窗口尺寸：800×500，可调整
- WindowStartupLocation：CenterScreen

### 4.2 模板管理标签页 TemplateTab

```
┌─────────────────────────────────────────────┐
│ 工具栏: [刷新] [种子模板] [导入] [删除]      │
├─────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────┐ │
│ │  模板列表 (DataGrid)                     │ │
│ │                                         │ │
│ │  ID          | 名称        | 版本 | 作者 │ │
│ │  default-thesis| 默认学术论文| 1.0 | WeaveDoc│
│ │  course-report | 课程报告   | 1.0 | WeaveDoc│
│ │  lab-report    | 实验报告   | 1.0 | WeaveDoc│
│ │                                         │ │
│ └─────────────────────────────────────────┘ │
│ 状态栏: 共 3 个模板                          │
└─────────────────────────────────────────────┘
```

**功能：**
- **刷新**：重新加载模板列表
- **种子模板**：调用 `EnsureSeedTemplatesAsync()`，弹窗提示结果
- **导入**：文件对话框选择 JSON → `SaveTemplateAsync()`
- **删除**：选中行 → 确认对话框 → `DeleteTemplateAsync()`

### 4.3 文档转换标签页 ConvertTab（向导式流程）

```
┌─────────────────────────────────────────────┐
│ Step 1: 选择 Markdown 文件                   │
│ ┌───────────────────────────────┐ [浏览...]  │
│ │ C:\docs\paper.md              │            │
│ └───────────────────────────────┘            │
│                                              │
│ Step 2: 选择模板                              │
│ ┌───────────────────────────────┐            │
│ │ 课程报告 (course-report)     ▼│  (ComboBox) │
│ └───────────────────────────────┘            │
│                                              │
│ Step 3: 导出格式                              │
│ (●) DOCX  (○) PDF                            │
│                                              │
│ 输出目录: ┌─────────────────────┐ [浏览...]   │
│          │ C:\output\           │             │
│          └─────────────────────┘             │
│                                              │
│           [ 开始转换 ]                        │
│                                              │
│ 状态: ● 就绪                                  │
│ 日志:                                         │
│ ┌───────────────────────────────────────────┐│
│ │                                           ││
│ └───────────────────────────────────────────┘│
└─────────────────────────────────────────────┘
```

**功能：**
- **浏览 MD 文件**：OpenFileDialog，过滤 `*.md`
- **模板下拉框**：从 ConfigManager 加载 `ListTemplatesAsync()`
- **格式选择**：RadioButton (DOCX/PDF)
- **输出目录**：文件夹选择对话框
- **开始转换**：调用 `DocumentConversionEngine.ConvertAsync()` → 进度/结果显示在日志区
- **日志区**：TextBox 只读，滚动显示转换过程输出

## 5. 技术依赖

```xml
<!-- WeaveDoc.Converter.Ui.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WeaveDoc.Converter\WeaveDoc.Converter.csproj" />
  </ItemGroup>
</Project>
```

## 6. 启动流程

```
Program.cs Main()
  → 创建 ConfigManager(dbPath)
    → dbPath = Path.Combine(AppContext.BaseDirectory, "data", "weavedoc.db")
  → 调用 EnsureSeedTemplatesAsync()（首次运行种子内置模板）
  → 创建 PandocPipeline()（默认路径：tools/pandoc/pandoc.exe）
  → 创建 DocumentConversionEngine(pandocPipeline, configManager)
  → 启动 Avalonia MainWindow(configManager, engine)
```

## 6.1 解决方案集成

将新项目添加到 `WeaveDoc.slnx`（已有 Converter + Tests + 根项目）。

## 7. 不做的事情

- 不做 Markdown 编辑器（WBS 2 职责）
- 不做 PDF 预览（WBS 2 职责）
- 不做 MVVM 架构（简易工具不需要）
- 不做主题切换/国际化
- 不做 BibTeX GUI 管理（BibTeX 解析器已实现但 GUI 不在本期范围，按需添加）
- 不做 Pandoc 路径配置 UI（使用默认路径，运行前确保 `tools/pandoc/pandoc.exe` 可用）
