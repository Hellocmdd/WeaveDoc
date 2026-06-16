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
  -> (when [@key] present) CitationScanner -> CitationValidator -> temp .bib + CSL
  -> PandocPipeline (+ LuaFilters + reference.docx + optional citeproc)
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
| Config | `Config/` | SQLite template metadata, built-in template discovery, BibTeX parser, literature repository, citation scanning/validation, JSON schema and CSL resources |
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

## BibTeX Citations

The converter closes the citation loop end to end: import a `.bib` file, cite entries in Markdown with Pandoc citation syntax, and let Pandoc `citeproc` render them according to GB/T 7714-2015 when exporting.

Pipeline integration is in `DocumentConversionEngine.ConvertAsync` as a Step 0 that only runs when the Markdown contains a citation:

```text
Markdown contains [@key]
  -> CitationScanner.Scan(markdown)             extract cited keys (deduped, in first-occurrence order)
  -> CitationValidator.ValidateAsync(...)        CON-01 field-completeness check against the library
                                                    issues become ConversionWarning records, never blocking
  -> LiteratureRepository.WriteBibliographyFileAsync   write only the cited entries to a temp .bib
  -> CslResourceProvider.ExtractToTemp()         extract the embedded GB/T 7714 CSL to a temp file
  -> PandocPipeline.ToDocxAsync(..., CitationContext)
       --bibliography <temp.bib> --csl <gbt7714.csl> --citeproc
  -> Pandoc renders superscript numbering + bibliography per GB/T 7714 numeric style
```

Components (all in `Config/`):

| Component | Role |
| --- | --- |
| `BibtexParser` | Parses `.bib` text into `BibtexEntry` records (entry type, citation key, fields). Existing parser, reused as the import kernel. |
| `LiteratureRepository` | SQLite CRUD for literature entries in `literature_entries` table (same `weavedoc.db`). Fields stored as JSON, common columns flattened for indexing; `citation_key` is the primary key with upsert semantics. |
| `CitationScanner` | Extracts Pandoc citation keys from Markdown via regex with text masking that excludes fenced/indented code blocks, inline code, and escaped `\[@key]`. |
| `CitationValidator` | CON-01 completeness check. Decoupled from the repository via a `resolver` callback so it can be unit tested without a database. Never blocks export. |
| `CitationFieldRules` | GB/T 7714-2015 required-field rules per entry type, with `author`/`editor` alternation and an unknown-type fallback. |
| `CslResourceProvider` | Extracts the embedded `Config/Csl/chinese-gb7714-2015-numeric.csl` to a temp file. |

`PandocPipeline.ToDocxAsync` accepts an optional `CitationContext` and forwards `--bibliography`/`--csl`/`--citeproc` to Pandoc only when it is non-null. `DocumentConversionEngine` takes an optional `LiteratureRepository`; when either is null the engine degrades gracefully and records a `ConversionWarning` rather than failing. Citation warnings surface in `ConversionResult.Warnings`.

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

// Optional: enables [@key] citation rendering (GB/T 7714-2015) on export.
var literature = new LiteratureRepository("config.db");
await literature.ImportAsync(new BibtexParser().Parse(File.ReadAllText("refs.bib")), "refs.bib");

var engine = new DocumentConversionEngine(pandoc, pdfConverter, config, literature);

var docx = await engine.ConvertAsync("input.md", "default-thesis", "docx");
var pdf = await engine.ConvertAsync(
    "input.md",
    "software-plan-report",
    "pdf",
    PdfLayoutMode.TwoColumn);
```

`ConversionResult.Warnings` carries any citation issues (unresolved keys, missing GB/T 7714 fields) so callers can surface them without the export itself failing.

The converter test README at [../../tests/WeaveDoc.Converter.Tests/README.md](../../tests/WeaveDoc.Converter.Tests/README.md) describes the current parser, config, Pandoc, preprocessing, OpenXML, and PDF renderer coverage.
