# WeaveDoc.Converter

WeaveDoc 的文档转换核心库——将 Markdown 源文件通过 AFD 模板样式和 Pandoc 管道转换为格式化的 Docx/PDF 文档。

## 技术栈

- **.NET 10** / C# 13
- **DocumentFormat.OpenXml 3.5.1** — Docx 结构读写与样式操作
- **Markdig 0.39.1** — Markdown 解析（预留扩展）
- **Microsoft.Data.Sqlite 10.0.5** — 模板元信息本地存储
- **System.Text.Json** — JSON 序列化（内置，无额外依赖）
- **Pandoc 3.9+** — 底层格式转换引擎（外部 CLI 调用）
- **Tectonic** — XeTeX 渲染引擎，为 Pandoc 提供 PDF 输出能力（外部 CLI 调用）

## 架构

```text
┌─────────────────────────────────────────────────────────┐
│                  DocumentConversionEngine                │
│              （顶层编排入口，组长唯一调用点）               │
├──────────────┬──────────────────┬───────────────────────┤
│   Afd 模板层  │   Pandoc 转换层   │   Config 配置层       │
├──────────────┼──────────────────┼───────────────────────┤
│ AfdParser    │ PandocPipeline   │ ConfigManager         │
│ AfdStyleMapper│ ReferenceDocBuilder│ TemplateRepository  │
│ Models/*     │ OpenXmlStyleCorrector│ BibtexParser       │
└──────────────┴──────────────────┴───────────────────────┘
```

### 转换流程

```text
Markdown ──→ [Pandoc + reference.docx] ──→ raw.docx ──→ [OpenXML 样式精修] ──→ 修正后 DOCX
                  ↑                              ↑                                │
            AFD 模板生成                  AFD 模板样式应用                  ┌──────┴──────┐
          (ReferenceDocBuilder)        (OpenXmlStyleCorrector)           Docx          PDF
                                                                  (直接输出)   (Pandoc+Tectonic)
```

## 目录结构

```text
WeaveDoc.Converter/
├── DocumentConversionEngine.cs    # 端到端编排入口
├── Afd/                           # AFD 模板系统
│   ├── AfdParser.cs               #   JSON 模板解析与验证
│   ├── AfdStyleMapper.cs          #   AFD 样式键 ↔ OpenXML styleId 映射
│   ├── AfdParseException.cs       #   自定义异常
│   └── Models/                    #   数据模型
│       ├── AfdTemplate.cs         #     模板根对象
│       ├── AfdMeta.cs             #     模板元信息（ID、名称、版本、作者）
│       ├── AfdDefaults.cs         #     默认样式（字体、字号、行距、页面尺寸）
│       ├── AfdStyleDefinition.cs  #     单个样式定义
│       ├── AfdHeaderFooter.cs     #     页眉页脚配置
│       └── AfdNumbering.cs        #   编号配置
├── Pandoc/                        # Pandoc 转换管道
│   ├── PandocPipeline.cs          #   Pandoc CLI 封装（MD→Docx/PDF/AST）
│   ├── ReferenceDocBuilder.cs     #   AFD 模板 → Pandoc reference.docx
│   ├── OpenXmlStyleCorrector.cs   #   Docx 后处理：字体、页边距、页眉页脚
│   └── LuaFilters/
│       └── afd-heading-filter.lua #   Pandoc Lua 过滤器（标题标记）
├── Config/                        # 本地配置管理
│   ├── ConfigManager.cs           #   公共 API：模板库 CRUD + 种子模板 + 文件管理
│   ├── TemplateRepository.cs      #   SQLite 元信息存储（internal）
│   ├── BibtexParser.cs            #   BibTeX 文献引用解析
│   ├── BibtexEntry (record)       #   解析结果数据结构
│   └── TemplateSchemas/           #   内置模板文件
│       ├── default-thesis.json    #     默认学术论文模板
│       ├── course-report.json     #     课程报告模板
│       └── lab-report.json        #     实验报告模板
└── WeaveDoc.Converter.csproj
```

## 模块说明

### Afd 模板系统 (`Afd/`)

AFD（Academic Format Definition）是 WeaveDoc 自定义的文档格式描述体系。通过 JSON 模板文件定义排版规则。

- **AfdParser** — 解析 JSON 模板文件为 `AfdTemplate` 对象，包含结构验证
- **AfdStyleMapper** — AFD 样式键（如 `heading1`、`body`）与 OpenXML styleId（如 `Heading1`、`Normal`）双向映射
- **Models** — 7 个数据模型类，覆盖模板元信息、默认样式、样式定义、页眉页脚、编号

### Pandoc 转换管道 (`Pandoc/`)

封装 Pandoc CLI，实现文档格式转换和 OpenXML 级别的样式精修。

- **PandocPipeline** — Pandoc CLI 封装，支持 Markdown→Docx、DOCX→PDF（Tectonic 引擎，参数化字体）、Markdown→PDF、AST JSON 导出
- **ReferenceDocBuilder** — 将 AFD 模板转换为 Pandoc 可用的 `reference.docx` 样式参考文件
- **OpenXmlStyleCorrector** — 对 Pandoc 输出的 Docx 进行 OpenXML 级别的精确修正：字体/字号/行距（含表格内段落）、页边距/页面尺寸、页眉页脚

### 本地配置管理 (`Config/`)

管理模板库的持久化存储和文献引用解析。

- **ConfigManager** — 公共 API，提供模板的保存、获取、列表、删除、种子模板发现操作。编排 SQLite 元信息 + JSON 文件 + AfdParser
- **TemplateRepository** — SQLite 元信息 CRUD（internal 层），存储模板 ID、名称、版本、JSON 路径等
- **BibtexParser** — 纯文本 BibTeX 解析器，支持 `@string` 缩写展开、嵌套大括号、引号值、`@comment`/`@preamble` 跳过、畸形条目容错

### 引擎入口

- **DocumentConversionEngine** — 顶层编排：加载模板 → 生成 reference.docx → Pandoc 转换 → OpenXML 样式精修 → 输出 Docx 或 PDF

## 快速使用

```csharp
// 初始化
var pandoc = new PandocPipeline();
var config = new ConfigManager("config.db");
await config.EnsureSeedTemplatesAsync(); // 首次运行：发现内置模板
var engine = new DocumentConversionEngine(pandoc, config);

// 转换文档
var result = await engine.ConvertAsync("input.md", "course-report", "docx");
Console.WriteLine(result.Success ? result.OutputPath : result.ErrorMessage);
```

## 依赖

| 包 | 版本 | 用途 |
| ------ | ------ | ------ |
| DocumentFormat.OpenXml | 3.5.1 | Docx 结构读写 |
| Markdig | 0.39.1 | Markdown AST 解析（预留） |
| Microsoft.Data.Sqlite | 10.0.5 | 模板元信息本地 SQLite 存储 |
| Pandoc | 3.9+ | 外部 CLI，需单独安装或内嵌 |
| Tectonic | — | XeTeX 渲染引擎，Pandoc PDF 输出所需 |
