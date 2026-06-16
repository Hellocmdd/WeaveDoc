# WeaveDoc.Converter.Tests

`WeaveDoc.Converter.Tests` covers the AFD template system, local configuration storage, BibTeX literature and citations, Markdown preprocessing, Pandoc integration, OpenXML correction, and PDF renderer selection for `WeaveDoc.Converter`.

## Scope

| Test class | Coverage |
| --- | --- |
| `AfdParserTests` | JSON/file parsing, validation, malformed templates, built-in template parsing |
| `AfdStyleMapperTests` | AFD style key to OpenXML style id mapping and reverse lookup |
| `AfdStyleResolverTests` | Effective style resolution, especially heading numbering behavior |
| `BibtexParserTests` | BibTeX parsing, string expansion, nested braces, comments, preambles, malformed entries, `BibtexEntry` derived properties |
| `CitationScannerTests` | Pandoc `[@key]` extraction: single/grouped/prefixed/negative citations, dedup with first-occurrence order, fenced/indented code block exclusion, inline code exclusion, escaped `\[@key]` exclusion |
| `CitationValidatorTests` + `CslResourceProviderTests` | CON-01 GB/T 7714 field completeness (unresolved keys, missing fields, `author`/`editor` alternation, unknown-type fallback); embedded CSL extraction |
| `ConfigManagerTests` | Template save/get/list/delete and seed-template idempotency |
| `ConversionResultWarningsTests` | `ConversionResult.Warnings` default-empty and roundtrip |
| `LiteratureRepositoryTests` | SQLite import (upsert on duplicate key), get-all/get-by-key/find, update field, delete, `WriteBibliographyFileAsync` ordering/dedup/empty |
| `MarkdownPreprocessorTests` | HTML table conversion, remote image handling, warning emission, and code-fence safety |
| `PandocPipelineTests` | Pandoc calls, Markdown math/image/table normalization, reference DOCX generation, OpenXML style/page/header/footer correction, table layout, PDF conversion, `CitationContext`/citeproc pass-through, and `DocumentConversionEngine` behavior |
| `PdfConverterSelectionTests` | Word/LibreOffice/Syncfusion detection and renderer priority |

The Pandoc tests exercise real conversion paths. They require Pandoc to be available through the build-provisioned `tools/pandoc/` path or `PATH`.

## Test Stack

- .NET 10
- xUnit 2
- Microsoft.NET.Test.Sdk
- DocumentFormat.OpenXml
- Syncfusion DocIO renderer packages
- Project reference to `src/WeaveDoc.Converter`

## Files

```text
WeaveDoc.Converter.Tests/
├── AfdParserTests.cs
├── AfdStyleMapperTests.cs
├── AfdStyleResolverTests.cs
├── BibtexParserTests.cs
├── CitationScannerTests.cs
├── CitationValidatorTests.cs
├── ConfigManagerTests.cs
├── ConversionResultWarningsTests.cs
├── LiteratureRepositoryTests.cs
├── MarkdownPreprocessorTests.cs
├── PandocPipelineTests.cs
├── PdfConverterSelectionTests.cs
├── TestImageServer.cs
└── WeaveDoc.Converter.Tests.csproj
```

## Run

```bash
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "AfdParserTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "MarkdownPreprocessorTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "CitationScannerTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "LiteratureRepositoryTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "PandocPipelineTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "PdfConverterSelectionTests" -nologo
```

## Notes

- Config tests use temporary directories and SQLite databases.
- PDF renderer selection tests use fake renderers where possible so they do not depend on machine-specific Word/LibreOffice installation state.
- Style tests should inspect DOCX structure through OpenXML parts rather than only checking that output files exist.
- Keep fixed test counts out of this README; the project changes often, and the source files are the authority.
