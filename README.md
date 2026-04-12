# WeaveDoc — 语义转换系统与本地配置管理

本分支实现 [《软件计划项目书》](docs/软件计划项目书/《软件计划项目书》.md) 中分配给 **任逸青（语义及文本转换组）** 的全部任务，涵盖语义转换系统的 AFD 模板解析、Pandoc 转换管道、本地配置管理三大模块，以及配套的 Avalonia UI 桌面界面。

> **负责人**：任逸青（文档转换开发岗 / 语义及文本转换组）
> **计划书任务**：3.1 AFD 样式解析器 / 3.2 Pandoc 转换管道 / 3.3 本地配置管理
> **当前状态**：85 个测试全部通过（Converter 79 + UI 6），0 构建错误

---

## 计划书任务要求

依据计划书 2.4 节与 4.2 节，本分支的职责定义为：

> **文档转换开发岗** — 负责 AFD 协议解析、Pandoc 转换管线调教及 OpenXML 样式映射；管理本地配置与 BibTeX 索引。

| 编号 | 任务名称 | 计划时间 | 要求内容 | 完成度 |
| --- | --- | --- | --- | --- |
| **3** | 语义转换系统开发 | 第 6–11 周 | 完成样式解析与格式导出 | — |
| **3.1** | AFD 样式解析器 | 第 6–8 周 | 解析 JSON 模板中的样式定义（字体、行距、边距） | ~95% |
| **3.2** | Pandoc 转换管道 | 第 8–10 周 | 调用 Pandoc 将 MD 转换为 Word/PDF，保留样式映射 | ~95% |
| **3.3** | 本地配置管理 | 第 10–11 周 | 实现 BibTeX 文献库导入、样式模板增删改查 | ~65% |

### 关联验收标准

| 编号 | 要求 | 验收方法 | 当前状态 |
| --- | --- | --- | --- |
| **F-02** | 根据 AFD 模板将 MD 导出为样式一致的 DOCX/PDF | 演示与对比 | **完全满足** |
| **F-04** | 导入/管理 BibTeX 文献，切换样式模板 | 检查功能 | 部分满足（BibTeX 解析器就绪，GUI 集成待对接） |

### 关联性能要求（计划书 2.3 节）

- 导出 docx 关键样式项（标题层级、正文、引用、页边距、字体字号、行距、段前段后）一致率不低于 **95%**

---

## 具体实现内容

### 任务 3.1 — AFD 样式解析器

解析 AFD（Academic Format Definition）JSON 模板，实现内容与样式的分离。

**实现文件**：[src/WeaveDoc.Converter/Afd/](src/WeaveDoc.Converter/Afd/)

| 组件 | 职责 |
| --- | --- |
| `AfdParser.cs` | JSON 模板文件解析与结构验证，解析失败抛出 `AfdParseException` |
| `AfdStyleMapper.cs` | AFD 样式键 ↔ OpenXML styleId 双向映射（14 种） |
| `Models/` | 7 个数据模型：AfdTemplate、AfdMeta、AfdDefaults、AfdStyleDefinition、AfdHeaderFooter、AfdNumbering |

**样式映射覆盖**：

| AFD 键 | OpenXML styleId | AFD 键 | OpenXML styleId |
| --- | --- | --- | --- |
| `heading1`–`heading6` | `Heading1`–`Heading6` | `body` | `Normal` |
| `caption` | `Caption` | `footnote` | `FootnoteText` |
| `reference` | `Reference` | `abstract` | `Abstract` |
| `blockquote` | `Blockquote` | `list` | `ListParagraph` |
| `codeblock` | `CodeBlock` | | |

### 任务 3.2 — Pandoc 转换管道

封装 Pandoc CLI，实现 Markdown → Docx/PDF 的格式转换和 OpenXML 级别的样式精修。

**实现文件**：[src/WeaveDoc.Converter/Pandoc/](src/WeaveDoc.Converter/Pandoc/)

**转换流程：**

```text
Markdown ──→ [Pandoc + LuaFilters + reference.docx] ──→ raw.docx
                                                       │
                            ┌──────────────────────────┘
                            ↓
                 [OpenXmlStyleCorrector]
                   ├── WriteStyleDefinitions    (AFD → styles.xml 样式定义)
                   ├── StripRedundantInline     (清除冗余内联字体/字号)
                   ├── ApplyPageSettings        (页面尺寸/边距)
                   └── ApplyHeaderFooter        (页眉页脚)
                            │
                            ↓
                     修正后 DOCX ──→ 输出或经由 Pandoc+Tectonic 生成 PDF
```

| 组件 | 职责 |
| --- | --- |
| `PandocPipeline.cs` | Pandoc CLI 封装：ToDocxAsync / FromDocxToPdfAsync / ToAstJsonAsync + Lua Filter 自动发现 |
| `ReferenceDocBuilder.cs` | AFD 模板 → reference.docx，预置样式定义 |
| `OpenXmlStyleCorrector.cs` | Docx 后处理：写入样式定义、清除冗余内联、页面设置、页眉页脚 |
| `LuaFilters/` | Pandoc 运行时自动加载的 Lua 过滤器（blockquote/codeblock 自定义样式注入） |

### 任务 3.3 — 本地配置管理

管理模板库的持久化存储和 BibTeX 文献引用解析。

**实现文件**：[src/WeaveDoc.Converter/Config/](src/WeaveDoc.Converter/Config/)

| 组件 | 职责 |
| --- | --- |
| `ConfigManager.cs` | 公共 API：模板 CRUD + 种子模板发现 |
| `TemplateRepository.cs` | SQLite 元信息存储（internal） |
| `BibtexParser.cs` | BibTeX 解析：@string 缩写展开、嵌套括号、引号值、畸形条目容错 |
| `TemplateSchemas/` | 3 个内置模板（嵌入式资源） |

**内置模板：**

| 模板文件 | 名称 | 特点 |
| --- | --- | --- |
| `default-thesis.json` | 默认学术论文 | h1-h6 全覆盖，页眉"学位论文"，页脚阿拉伯页码 |
| `course-report.json` | 课程报告 | h1-h4，页眉"课程报告"，页脚页码 |
| `lab-report.json` | 实验报告 | h1-h4，页眉"实验报告"，9pt 页眉字号 |

### Avalonia UI 桌面界面

配套的可视化操作界面，提供模板管理和文档转换向导。

**实现文件**：[src/WeaveDoc.Converter.Ui/](src/WeaveDoc.Converter.Ui/)

| 页面 | 功能 |
| --- | --- |
| `TemplateTab` | DataGrid 模板列表 + 刷新/种子/导入/删除 + 状态栏 |
| `ConvertTab` | 三步向导（选 MD → 选模板 → 选格式）+ 输出目录 + 转换按钮 + 日志 |
| `MainWindow` | TabControl 双标签页布局，依赖注入 ConfigManager + DocumentConversionEngine |

---

## 仓库结构

```text
WeaveDoc/
├── WeaveDoc.slnx                           # 解决方案文件（4 个项目）
├── WeaveDoc.csproj                         # 根项目（入口占位）
├── src/
│   ├── WeaveDoc.Converter/                 # 文档转换核心库（任务 3.1/3.2/3.3）
│   │   ├── DocumentConversionEngine.cs     #   端到端编排入口
│   │   ├── Afd/                            #   AFD 模板解析与样式映射
│   │   ├── Pandoc/                         #   Pandoc 管道 + OpenXML 样式精修 + Lua Filters
│   │   └── Config/                         #   配置管理 + BibTeX 解析 + 内置模板
│   └── WeaveDoc.Converter.Ui/              # Avalonia UI 桌面应用
│       ├── Program.cs                      #   启动入口
│       └── Views/                          #   MainWindow + TemplateTab + ConvertTab
├── tests/
│   ├── WeaveDoc.Converter.Tests/           # 核心库测试（79 个）
│   └── WeaveDoc.Converter.Ui.Tests/        # Headless UI 测试（6 个）
├── tools/
│   ├── DownloadExternalTools.targets       # MSBuild 自动下载 Pandoc/Tectonic
│   ├── setup-tools.ps1                     # 下载脚本
│   ├── pandoc/                             # Pandoc 二进制（自动下载）
│   └── tectonic/                           # Tectonic 二进制（自动下载）
└── docs/
    ├── 软件计划项目书/                      # 项目计划书 + 图片
    ├── technical-reference/                 # 技术参考文档
    └── 待优化项清单.md                      # 待优化项跟踪
```

---

## 技术栈

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| .NET / C# | 10 / 13 | 运行时与语言 |
| Avalonia | 11.* | 跨平台桌面 UI 框架 |
| DocumentFormat.OpenXml | 3.5.1 | Docx 结构读写与样式定义操作 |
| Markdig | 0.39.1 | Markdown AST 解析 |
| Microsoft.Data.Sqlite | 10.0.5 | 模板元信息本地 SQLite 存储 |
| xUnit | 2 | 测试框架 |
| Avalonia.Headless | 11.* | 无头 UI 测试 |
| Pandoc | 3.9+ | 底层格式转换引擎（构建时自动下载） |
| Tectonic | — | XeTeX 渲染引擎，PDF 输出所需（构建时自动下载） |

---

## 快速开始

### 前置要求

- .NET 10 SDK
- Windows 10/11 (x64) 或 Linux (Ubuntu 24.04+)

### 构建与运行

```bash
# 构建（首次自动下载 Pandoc + Tectonic，约 100MB）
dotnet build

# 运行 UI 应用
dotnet run --project src/WeaveDoc.Converter.Ui
```

Pandoc 和 Tectonic 通过 MSBuild Target 在首次构建时自动下载，无需手动安装。

### 运行测试

```bash
# 运行全部测试（85 个）
dotnet test -v n

# 仅运行核心库测试（79 个）
dotnet test tests/WeaveDoc.Converter.Tests -v n

# 仅运行 UI 测试（6 个）
dotnet test tests/WeaveDoc.Converter.Ui.Tests -v n
```

---

## 测试覆盖

| 测试项目 | 数量 | 覆盖范围 |
| --- | --- | --- |
| `WeaveDoc.Converter.Tests` | 79 | AFD 解析（13）、样式映射（5）、Pandoc 管道（43）、配置管理（8）、BibTeX 解析（10） |
| `WeaveDoc.Converter.Ui.Tests` | 6 | DataGrid 绑定（3）、ComboBox 填充（3），Headless 模式 |

端到端测试覆盖完整链路：`Parse → ReferenceDoc → Pandoc → StyleCorrector → PageSettings → HeaderFooter`，验证样式定义（非内联）包含正确的字体/字号属性。

---

## 文档索引

| 文档 | 位置 |
| --- | --- |
| 软件计划项目书 | [《软件计划项目书》.md](docs/软件计划项目书/《软件计划项目书》.md) |
| 待优化项清单 | [待优化项清单.md](docs/待优化项清单.md) |
| Converter 核心库 README | [WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md) |
| Converter UI README | [WeaveDoc.Converter.Ui/README.md](src/WeaveDoc.Converter.Ui/README.md) |
| Converter 测试 README | [WeaveDoc.Converter.Tests/README.md](tests/WeaveDoc.Converter.Tests/README.md) |
| UI 测试 README | [WeaveDoc.Converter.Ui.Tests/README.md](tests/WeaveDoc.Converter.Ui.Tests/README.md) |
| 技术参考 | [docs/technical-reference/](docs/technical-reference/) |
