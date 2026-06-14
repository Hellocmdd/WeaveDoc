# WeaveDoc.Converter

`WeaveDoc.Converter` is the conversion core for WeaveDoc. It turns Markdown into styled DOCX/PDF output through AFD templates, Pandoc, OpenXML post-processing, and a renderer chain for PDF export.

## Responsibilities

- Parse and validate AFD JSON templates.
- Seed built-in templates into a local SQLite-backed template catalog.
- Normalize Markdown before Pandoc conversion, including math, HTML tables, HTML/Markdown images, and remote images.
- Build a Pandoc `reference.docx` from AFD styles.
- Run Pandoc for Markdown-to-DOCX and AST JSON output.
- Correct the generated DOCX at the OpenXML level.
- Convert DOCX to PDF through Microsoft Word, LibreOffice, or Syncfusion fallback renderers.

## Main Pipeline

```text
Markdown
  -> MarkdownHtmlTableNormalizer / MarkdownHtmlImageNormalizer / MarkdownMathNormalizer
  -> PandocPipeline (+ LuaFilters + reference.docx)
  -> raw.docx
  -> OpenXmlStyleCorrector
       - style definitions
       - table layout and borders
       - redundant inline-format cleanup
       - page settings
       - headers and footers
       - optional PDF layout
  -> DOCX
  -> optional PDF through CompositePdfConverter
```

The top-level entry is `DocumentConversionEngine.ConvertAsync(markdownPath, templateId, outputFormat, pdfLayoutMode, ct)`.

## Architecture

| Area | Files | Role |
| --- | --- | --- |
| AFD model/parser | `Afd/`, `Afd/Models/` | Template model, JSON parsing, validation, style-key mapping, numbering/table style helpers |
| Config | `Config/` | SQLite template metadata, built-in template discovery, BibTeX parser, JSON schema resources |
| Pandoc | `Pandoc/` | Pandoc CLI wrapper, Markdown normalization, reference DOCX generation, Lua filters, OpenXML correction |
| PDF | `Pandoc/*PdfConverter.cs`, `PdfRendererDetector.cs` | Renderer detection and DOCX-to-PDF conversion |
| Diagnostics | `ConversionErrorFormatter.cs`, `ConversionDiagnostics.cs` | User-facing errors, warnings, and preprocess result records |

## AFD Templates

AFD (Academic Format Definition) separates document content from output style. Templates are JSON files embedded from `Config/TemplateSchemas/`.

Current built-in templates:

| Template file | Template id/name |
| --- | --- |
| `default-thesis.json` | 默认学术论文 |
| `course-report.json` | 课程报告 |
| `lab-report.json` | 实验报告 |
| `software-plan-report.json` | 软件计划项目书 |

`Config/Schemas/afd-template-v1.schema.json` documents the expected template shape. `ConfigManager.EnsureSeedTemplatesAsync()` registers embedded templates into the local catalog. The desktop app stores that catalog in `data/weavedoc.db`.

`AfdStyleResolver` keeps style behavior shared between reference-doc generation and final OpenXML correction, including heading numbering derived from `AfdNumbering`. `AfdTableStyle` carries table border, header fill/bold, and cell margin options.

## Markdown Preprocessing

The converter contains two related preprocessing layers:

- `PandocPipeline.PrepareMarkdownInputAsync` applies lightweight normalizers before direct Pandoc calls.
- `MarkdownPreprocessor` provides a richer preprocess result with warnings and temporary resource paths.

Current normalization covers:

- HTML tables that can safely become Markdown pipe tables.
- Markdown and HTML image references.
- Remote images downloaded into a temporary media directory with size/type checks.
- Dollar math normalization, including text cases that Pandoc otherwise misreads.
- Resource path setup so local and temporary media can be found by Pandoc.

Unsupported or lossy transformations are reported as `ConversionWarning` records.

## Pandoc And Lua Filters

`PandocPipeline` resolves Pandoc in this order:

1. `tools/pandoc/` copied to the build output.
2. `tools/pandoc/` found by walking up from `AppContext.BaseDirectory`.
3. `pandoc`/`pandoc.exe` on `PATH`.

All `.lua` files in `Pandoc/LuaFilters/` are discovered and passed to Pandoc. The filters currently handle heading markers, block style assignment, and semantic block styling.

## OpenXML Correction

`OpenXmlStyleCorrector` is the final authority for DOCX shape after Pandoc output. It writes styles into `StyleDefinitionsPart`, normalizes Pandoc block styles, applies page settings/header/footer, handles table layout rules, and applies `PdfLayoutMode` before PDF export.

PDF layout options are:

- `PdfLayoutMode.SingleColumn`
- `PdfLayoutMode.TwoColumn`

## PDF Renderers

`CompositePdfConverter` selects renderers in priority order:

1. Microsoft Word COM on Windows.
2. LibreOffice `soffice --headless`.
3. Syncfusion DocIO fallback.

`PdfRendererDetector` reports which renderers are available. `DocumentConversionEngine` records the renderer actually used in `ConversionResult.PdfConverterName`.

## Dependencies

Key package/runtime dependencies:

- .NET 10
- `DocumentFormat.OpenXml`
- `Markdig`
- `Microsoft.Data.Sqlite`
- `Syncfusion.DocIORenderer.Net.Core`
- `Syncfusion.Pdf.Net.Core`
- Linux native assets for SkiaSharp/HarfBuzz
- External Pandoc CLI
- Optional Microsoft Word or LibreOffice for higher-fidelity PDF export

## Build And Test

```bash
dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
```

Pandoc provisioning is wired through `tools/DownloadExternalTools.targets`. Set `SkipExternalToolsDownload=true` when you want to skip that automatic step.

## Minimal Usage

```csharp
var pandoc = new PandocPipeline();
var pdfConverter = new CompositePdfConverter(new PdfRendererDetector());
var config = new ConfigManager("config.db");
await config.EnsureSeedTemplatesAsync();

var engine = new DocumentConversionEngine(pandoc, pdfConverter, config);

var docx = await engine.ConvertAsync("input.md", "default-thesis", "docx");
var pdf = await engine.ConvertAsync(
    "input.md",
    "software-plan-report",
    "pdf",
    PdfLayoutMode.TwoColumn);
```

The converter test README at [../../tests/WeaveDoc.Converter.Tests/README.md](../../tests/WeaveDoc.Converter.Tests/README.md) describes the current parser, config, Pandoc, preprocessing, OpenXML, and PDF renderer coverage.
