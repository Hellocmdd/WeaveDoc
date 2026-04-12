# WeaveDoc.Converter

WeaveDoc 的文档转换核心库——将 Markdown 源文件通过 AFD 模板样式和 Pandoc 管道转换为格式规范的 Docx/PDF 学术文档。

> **负责人**：任逸青（语义及文本转换组）
> **计划书任务**：3.1 AFD 样式解析器 / 3.2 Pandoc 转换管道 / 3.3 本地配置管理
> **当前状态**：79 测试全部通过，0 构建错误

---

## 技术栈

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| .NET / C# | 10 / 13 | 运行时与语言 |
| DocumentFormat.OpenXml | 3.5.1 | Docx 结构读写与样式定义操作 |
| Markdig | 0.39.1 | Markdown AST 解析（预留扩展） |
| Microsoft.Data.Sqlite | 10.0.5 | 模板元信息本地 SQLite 存储 |
| System.Text.Json | 内置 | JSON 序列化（无额外依赖） |
| Pandoc | 3.9+ | 底层格式转换引擎（外部 CLI，构建时自动下载） |
| Tectonic | — | XeTeX 渲染引擎，PDF 输出所需（构建时自动下载） |

---

## 架构

```text
┌──────────────────────────────────────────────────────────────┐
│                    DocumentConversionEngine                   │
│                （顶层编排入口，组长唯一调用点）                  │
├──────────────┬───────────────────┬───────────────────────────┤
│  Afd 模板层   │   Pandoc 转换层    │   Config 配置层           │
├──────────────┼───────────────────┼───────────────────────────┤
│ AfdParser    │ PandocPipeline    │ ConfigManager             │
│ AfdStyleMapper│ ReferenceDocBuilder│ TemplateRepository       │
│ Models/*     │ OpenXmlStyleCorrector│ BibtexParser            │
│              │ LuaFilters/*      │                           │
└──────────────┴───────────────────┴───────────────────────────┘
```

### 转换流程

```text
Markdown ──→ [Pandoc + LuaFilters + reference.docx] ──→ raw.docx
                                                       │
                            ┌──────────────────────────┘
                            ↓
                 [OpenXmlStyleCorrector]
                   ├── WriteStyleDefinitions    (AFD → styles.xml)
                   ├── StripRedundantInline     (清除冗余内联属性)
                   ├── ApplyPageSettings        (页面尺寸/边距)
                   └── ApplyHeaderFooter        (页眉页脚)
                            │
                            ↓
                     修正后 DOCX ──→ 输出或经由 Pandoc+Tectonic 生成 PDF
```

---

## 目录结构

```text
WeaveDoc.Converter/
├── DocumentConversionEngine.cs        # 端到端编排入口
├── Afd/                               # AFD 模板系统（任务 3.1）
│   ├── AfdParser.cs                   #   JSON 模板解析与验证
│   ├── AfdStyleMapper.cs              #   AFD 样式键 ↔ OpenXML styleId 双向映射
│   ├── AfdParseException.cs           #   自定义异常
│   └── Models/                        #   数据模型
│       ├── AfdTemplate.cs             #     模板根对象
│       ├── AfdMeta.cs                 #     模板元信息
│       ├── AfdDefaults.cs             #     默认样式（字体、字号、行距、页面）
│       ├── AfdStyleDefinition.cs      #     单个样式定义
│       ├── AfdHeaderFooter.cs         #     页眉页脚配置
│       └── AfdNumbering.cs            #     编号配置（预留）
├── Pandoc/                            # Pandoc 转换管道（任务 3.2）
│   ├── PandocPipeline.cs             #   Pandoc CLI 封装
│   ├── ReferenceDocBuilder.cs        #   AFD 模板 → reference.docx
│   ├── OpenXmlStyleCorrector.cs      #   Docx 后处理（样式定义写入、冗余内联清除、页面/页眉页脚）
│   └── LuaFilters/                   #   Pandoc Lua 过滤器（自动发现）
│       ├── afd-heading-filter.lua    #     标题标记注入
│       └── assign-block-styles.lua   #     blockquote/codeblock 自定义样式注入
├── Config/                            # 本地配置管理（任务 3.3）
│   ├── ConfigManager.cs              #   公共 API：模板 CRUD + 种子发现
│   ├── TemplateRepository.cs         #   SQLite 元信息存储（internal）
│   ├── BibtexParser.cs              #   BibTeX 文献引用解析
│   └── TemplateSchemas/             #   内置模板（嵌入式资源）
│       ├── default-thesis.json       #     默认学术论文模板
│       ├── course-report.json        #     课程报告模板
│       └── lab-report.json           #     实验报告模板
└── WeaveDoc.Converter.csproj
```

---

## 模块说明

### Afd 模板系统（`Afd/`）— 任务 3.1

AFD（Academic Format Definition）是 WeaveDoc 自定义的文档格式描述协议。通过 JSON 模板文件定义排版规则，实现"内容与样式分离"。

**AfdParser** — 将 JSON 模板文件解析为 `AfdTemplate` 对象。内置结构验证：

- 模板名称和样式表非空
- 字体大小为正数
- 解析失败抛出 `AfdParseException`（含原异常链）

**AfdStyleMapper** — AFD 样式键与 OpenXML styleId 的双向映射：

| AFD 键 | OpenXML styleId | 说明 |
| --- | --- | --- |
| `heading1` - `heading6` | `Heading1` - `Heading6` | 六级标题 |
| `body` | `Normal` | 正文 |
| `caption` | `Caption` | 题注 |
| `footnote` | `FootnoteText` | 脚注 |
| `reference` | `Reference` | 参考文献 |
| `abstract` | `Abstract` | 摘要 |
| `blockquote` | `Blockquote` | 引用块 |
| `list` | `ListParagraph` | 列表段落 |
| `codeblock` | `CodeBlock` | 代码块 |

**Models** — 7 个数据模型类，覆盖模板元信息、默认样式、样式定义、页眉页脚、编号。

### Pandoc 转换管道（`Pandoc/`）— 任务 3.2

封装 Pandoc CLI，实现文档格式转换和 OpenXML 级别的样式精修。

**PandocPipeline** — Pandoc CLI 封装，支持：

- `ToDocxAsync` — Markdown → Docx（可选 reference.docx）
- `FromDocxToPdfAsync` — Docx → PDF（通过 Tectonic，参数化 CJK 字体）
- `ToAstJsonAsync` — 导出 Pandoc AST JSON
- Lua Filters 自动发现：运行时扫描 `LuaFilters/*.lua`，自动附加到 Pandoc 命令

**ReferenceDocBuilder** — 将 AFD 模板转换为 Pandoc 可用的 `reference.docx`，在 `StyleDefinitionsPart` 中预置样式定义。

**OpenXmlStyleCorrector** — 对 Pandoc 输出的 Docx 进行 OpenXML 级别的精确修正：

- `ApplyAfdStyles` — 将 AFD 属性写入 `styles.xml` 的样式定义（非内联格式），并清除匹配段落中的冗余内联属性（字体/字号始终清除；Bold/Italic 仅在样式定义要求时清除，保留用户有意的行内格式）
- `ApplyPageSettings` — 设置页面尺寸（mm → twips）和页边距
- `ApplyHeaderFooter` — 添加页眉文本和页脚页码

**LuaFilters** — Pandoc 运行时自动加载的 Lua 过滤器：

- `afd-heading-filter.lua` — 为标题元素注入标记
- `assign-block-styles.lua` — 将 BlockQuote/CodeBlock 包裹在 `custom-style` 的 Div 中，使 Pandoc 输出正确的段落样式

### 本地配置管理（`Config/`）— 任务 3.3

管理模板库的持久化存储和文献引用解析。

**ConfigManager** — 公共 API，编排 SQLite 元信息 + JSON 文件 + AfdParser：

- `SaveTemplateAsync` / `GetTemplateAsync` / `ListTemplatesAsync` / `DeleteTemplateAsync` — 模板 CRUD
- `EnsureSeedTemplatesAsync` — 首次运行时自动发现 `TemplateSchemas/` 中的内置模板并注册到数据库

**TemplateRepository** — SQLite 元信息 CRUD（internal 层），存储模板 ID、名称、版本、JSON 路径。

**BibtexParser** — 纯文本 BibTeX 解析器，支持：

- `@string` 缩写展开
- 嵌套大括号值
- 引号包裹值
- `@comment` / `@preamble` 跳过
- 畸形条目容错（静默跳过）

### 引擎入口

**DocumentConversionEngine** — 顶层编排：

1. 从 ConfigManager 加载 AFD 模板
2. ReferenceDocBuilder 生成 reference.docx
3. PandocPipeline 执行 Markdown → Docx 转换
4. OpenXmlStyleCorrector 精修样式（写入样式定义 → 清除冗余内联 → 页面设置 → 页眉页脚）
5. 若输出 PDF，调用 PandocPipeline.FromDocxToPdfAsync

---

## 构建与外部依赖

Pandoc 和 Tectonic 无需手动安装。通过 MSBuild Target（`tools/DownloadExternalTools.targets`）在 `BeforeBuild` 阶段自动检测：

- 若 `tools/pandoc/` 或 `tools/tectonic/` 目录不存在，自动执行 `tools/setup-tools.ps1` 下载
- 下载的工具二进制通过 `Content` 配置复制到输出目录

```bash
dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj   # 自动下载依赖
dotnet test tests/WeaveDoc.Converter.Tests/                     # 79 测试
```

---

## 快速使用

```csharp
// 初始化
var pandoc = new PandocPipeline();           // 自动检测 tools/pandoc
var config = new ConfigManager("config.db");
await config.EnsureSeedTemplatesAsync();     // 首次运行：注册内置模板
var engine = new DocumentConversionEngine(pandoc, config);

// 转换为 Docx
var result = await engine.ConvertAsync("input.md", "default-thesis", "docx");
Console.WriteLine(result.Success ? result.OutputPath : result.ErrorMessage);

// 转换为 PDF
var pdfResult = await engine.ConvertAsync("input.md", "course-report", "pdf");
```

---

## 内置模板

| 模板文件 | 名称 | 特点 |
| --- | --- | --- |
| `default-thesis.json` | 默认学术论文 | h1-h6 全覆盖，页眉"学位论文"，页脚阿拉伯页码 |
| `course-report.json` | 课程报告 | h1-h4，页眉"课程报告"，页脚页码 |
| `lab-report.json` | 实验报告 | h1-h4，页眉"实验报告"，9pt 页眉字号 |

三个模板均包含 `blockquote`、`list`、`codeblock` 样式定义。

---

## 测试覆盖

79 个测试，分布在 4 个测试类中：

| 测试类 | 数量 | 覆盖范围 |
| --- | --- | --- |
| `AfdParserTests` | 13 | 模板解析、验证、异常处理 |
| `AfdStyleMapperTests` | 5 | 双向映射（14 个 AFD 键 + 异常/空值） |
| `BibtexParserTests` | 10 | BibTeX 解析全场景 |
| `ConfigManagerTests` | 8 | 模板 CRUD + 种子发现 |
| `PandocPipelineTests` | 43 | 管道转换、样式修正、页眉页脚、3 模板端到端 |

端到端测试覆盖完整链路：`Parse → ReferenceDoc → Pandoc → StyleCorrector → PageSettings → HeaderFooter`，验证样式定义（非内联）包含正确的字体/字号属性。

---

## 与计划书验收标准的对应关系

| 验收标准 | 要求 | 当前状态 |
| --- | --- | --- |
| F-02 | 根据 AFD 模板将 MD 导出为样式一致的 DOCX/PDF | 完全满足 |
| F-04 | 导入/管理 BibTeX 文献，切换样式模板 | 部分满足（BibTeX 解析器就绪，GUI 集成待第 12-13 周对接） |
