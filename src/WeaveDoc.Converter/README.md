# WeaveDoc.Converter

`WeaveDoc.Converter` 是 WeaveDoc 的文档转换核心库，负责把 Markdown 按 AFD 模板转换为 DOCX 或 PDF。应用层通常只需要调用 `DocumentConversionEngine`，模板解析、Pandoc 调用、DOCX 样式修正和 PDF 引擎选择都在本模块内完成。

## 对应计划书任务

本模块对应 [《软件计划项目书》](../../doc/《软件计划项目书》.md) 中语义及文本转换组的文档转换职责。

| 编号 | 任务 | 模块落点 |
| --- | --- | --- |
| 3.1 | AFD 样式解析器 | `Afd/AfdParser.cs`、`Afd/AfdStyleMapper.cs`、`Afd/Models/` |
| 3.2 | Pandoc 转换管道 | `Pandoc/PandocPipeline.cs`、`ReferenceDocBuilder.cs`、`OpenXmlStyleCorrector.cs`、`LuaFilters/` |
| 3.3 | 本地配置管理 | `Config/ConfigManager.cs`、`TemplateRepository.cs`、`BibtexParser.cs`、`TemplateSchemas/` |

验收目标覆盖 Markdown 到 DOCX/PDF、AFD 样式一致性、模板管理和 BibTeX 文献解析。GUI 入口由 `src/WeaveDoc.App/Views/ConvertTab.*` 和 `TemplateTab.*` 承担。

## 技术栈

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| .NET / C# | `net10.0` | 运行时与语言 |
| DocumentFormat.OpenXml | 3.5.1 | DOCX 结构、样式、页面和页眉页脚修正 |
| Markdig | 0.39.1 | Markdown 解析扩展预留 |
| Microsoft.Data.Sqlite | 10.0.5 | 模板元信息本地存储 |
| Pandoc | 3.9.x | Markdown 到 DOCX/AST 的外部转换引擎 |
| Microsoft Word COM | 本机安装 | Windows 高保真 DOCX 到 PDF |
| LibreOffice | 本机安装 | 可选 headless DOCX 到 PDF |
| Syncfusion DocIO/PDF | 33.1.49 | 无外部渲染器时的 PDF 兜底 |

## 架构

```text
DocumentConversionEngine
├── ConfigManager
│   ├── TemplateRepository
│   ├── AfdParser
│   └── BibtexParser
├── ReferenceDocBuilder
├── PandocPipeline
│   ├── MarkdownMathNormalizer
│   ├── MarkdownHtmlTableNormalizer
│   ├── MarkdownHtmlImageNormalizer
│   └── LuaFilters/
├── OpenXmlStyleCorrector
└── IPdfConverter
    └── CompositePdfConverter
        ├── WordComPdfConverter
        ├── LibreOfficePdfConverter
        └── SyncfusionPdfConverter
```

## 转换流程

```text
Markdown
  -> Markdown 预处理（公式、HTML table、HTML img）
  -> Pandoc + Lua filters + reference.docx
  -> raw.docx
  -> OpenXmlStyleCorrector
       - 写入 AFD 样式定义
       - 清理冗余内联字体/字号
       - 设置页面尺寸和页边距
       - 设置页眉页脚
       - PDF 输出时应用单列/双列版式
  -> DOCX 或 PDF
```

PDF 输出通过 `CompositePdfConverter` 按以下顺序尝试：

1. Microsoft Word COM
2. LibreOffice `soffice`
3. Syncfusion DocIO/PDF fallback

## 目录结构

```text
WeaveDoc.Converter/
├── DocumentConversionEngine.cs
├── ConversionErrorFormatter.cs
├── Afd/
│   ├── AfdParser.cs
│   ├── AfdStyleMapper.cs
│   ├── AfdParseException.cs
│   └── Models/
├── Config/
│   ├── ConfigManager.cs
│   ├── TemplateRepository.cs
│   ├── BibtexParser.cs
│   └── TemplateSchemas/
├── Pandoc/
│   ├── PandocPipeline.cs
│   ├── ReferenceDocBuilder.cs
│   ├── OpenXmlStyleCorrector.cs
│   ├── MarkdownMathNormalizer.cs
│   ├── MarkdownHtmlTableNormalizer.cs
│   ├── MarkdownHtmlImageNormalizer.cs
│   ├── PdfRendererDetector.cs
│   ├── CompositePdfConverter.cs
│   ├── WordComPdfConverter.cs
│   ├── LibreOfficePdfConverter.cs
│   ├── SyncfusionPdfConverter.cs
│   ├── PdfLayoutMode.cs
│   └── LuaFilters/
└── WeaveDoc.Converter.csproj
```

## AFD 模板

AFD（Academic Format Definition）是 WeaveDoc 使用的 JSON 样式模板格式，核心内容包括：

- `meta`：模板名称、版本、作者、描述
- `defaults`：默认字体、字号、行距、页面设置
- `styles`：正文、标题、引用、列表、代码块等样式定义
- `headerFooter`：页眉、页脚和页码设置

内置模板通过嵌入式资源发布：

| 模板文件 | 名称 | 特点 |
| --- | --- | --- |
| `default-thesis.json` | 默认学术论文 | h1-h6，论文页眉，页脚页码 |
| `course-report.json` | 课程报告 | h1-h4，课程报告页眉 |
| `lab-report.json` | 实验报告 | h1-h4，实验报告页眉 |

`ConfigManager.EnsureSeedTemplatesAsync()` 会在首次运行时把内置模板注册到本地 SQLite 数据库。

## 快速使用

```csharp
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;

var config = new ConfigManager("weavedoc.db");
await config.EnsureSeedTemplatesAsync();

var pandoc = new PandocPipeline();
var pdfConverter = new CompositePdfConverter(new PdfRendererDetector());
var engine = new DocumentConversionEngine(pandoc, pdfConverter, config);

var docx = await engine.ConvertAsync("paper.md", "default-thesis", "docx");
var pdf = await engine.ConvertAsync(
    "paper.md",
    "course-report",
    "pdf",
    PdfLayoutMode.TwoColumn);
```

转换结果通过 `ConversionResult` 返回，失败时会同时给出用户可读错误摘要和技术详情。

## 构建与依赖准备

```bash
dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj
```

项目导入 `tools/DownloadExternalTools.targets`。当 `tools/pandoc/` 缺失时，构建会调用平台脚本准备 Pandoc：

- Windows：`scripts/setup-tools.ps1`
- Linux/macOS：`scripts/setup-tools.sh`

如需跳过自动下载，可设置 `SkipExternalToolsDownload=true`。

## 测试

```bash
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
```

测试覆盖：

- AFD JSON 解析、验证和异常处理
- AFD 样式键与 OpenXML styleId 映射
- Markdown 公式、HTML 表格、HTML 图片规范化
- Pandoc 到 DOCX 的端到端转换
- `reference.docx` 生成与 OpenXML 样式修正
- 页面、页眉页脚、表格版式和 PDF 单列/双列设置
- PDF 引擎检测与 Word -> LibreOffice -> Syncfusion 优先级
- 模板 CRUD、种子模板导入和 BibTeX 解析

详细测试说明见 [../../tests/WeaveDoc.Converter.Tests/README.md](../../tests/WeaveDoc.Converter.Tests/README.md)。
