# WeaveDoc.Converter.Tests

`WeaveDoc.Converter.Tests` covers the converter library from small parser behavior through Pandoc/OpenXML integration and PDF renderer selection.

## Test Stack

| Dependency | Purpose |
| --- | --- |
| xUnit 2 | Test framework |
| Microsoft.NET.Test.Sdk | Test runner |
| DocumentFormat.OpenXml | DOCX structure and style assertions |
| Microsoft.Data.Sqlite | Temporary template database coverage through the converter project |
| Syncfusion DocIO/PDF | PDF fallback conversion coverage |

## Test Classes

| Class | Focus |
| --- | --- |
| `AfdParserTests` | JSON parsing, file parsing, template validation, built-in template parsing, and `AfdParseException` behavior |
| `AfdStyleMapperTests` | AFD style key to OpenXML styleId mapping and reverse lookup |
| `ConfigManagerTests` | Template CRUD, seed template import, overwrite behavior, and idempotency |
| `BibtexParserTests` | Article/book/proceedings entries, multiple entries, nested braces, quoted values, `@string`, comments, preambles, and malformed input tolerance |
| `PandocPipelineTests` | Pandoc CLI, Markdown normalization, `reference.docx`, OpenXML style correction, page/header/footer settings, table layout, DOCX/PDF conversion, and full pipeline behavior |
| `PdfConverterSelectionTests` | Word/LibreOffice/Syncfusion detection, fallback order, injected converter usage, and PDF layout handoff |

## What The Integration Tests Validate

- Markdown files are converted into valid DOCX packages.
- AFD styles are written into `styles.xml` instead of relying only on inline formatting.
- Redundant inline font/size formatting is removed while intentional inline emphasis remains.
- Page size, margins, headers, footers, start page numbers, and table layout are applied through OpenXML.
- HTML tables become Word tables, and HTML images become Pandoc image nodes.
- Math normalization handles spaced dollar math, circled-number notation, and numeric tilde ranges.
- PDF conversion uses the expected renderer priority and supports single-column or two-column layout before export.

## Run

```bash
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "AfdParserTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "PandocPipelineTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "PdfConverterSelectionTests" -nologo
```

## Test Data And Isolation

- `ConfigManagerTests` create temporary SQLite databases and clean them after each test.
- Pandoc integration tests use the converter project setup, which can prepare Pandoc through `tools/DownloadExternalTools.targets`.
- PDF renderer selection tests use fake converters where possible, so they do not depend on the host machine having Word or LibreOffice installed.
- End-to-end tests exercise the built-in templates: `default-thesis`, `course-report`, and `lab-report`.
