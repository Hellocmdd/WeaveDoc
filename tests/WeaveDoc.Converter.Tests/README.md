# WeaveDoc.Converter.Tests

`WeaveDoc.Converter.Tests` covers the AFD template system, local configuration storage, Markdown preprocessing, Pandoc integration, OpenXML correction, and PDF renderer selection for `WeaveDoc.Converter`.

## Scope

| Test class | Coverage |
| --- | --- |
| `AfdParserTests` | JSON/file parsing, validation, malformed templates, built-in template parsing |
| `AfdStyleMapperTests` | AFD style key to OpenXML style id mapping and reverse lookup |
| `AfdStyleResolverTests` | Effective style resolution, especially heading numbering behavior |
| `BibtexParserTests` | BibTeX parsing, string expansion, nested braces, comments, preambles, malformed entries |
| `ConfigManagerTests` | Template save/get/list/delete and seed-template idempotency |
| `MarkdownPreprocessorTests` | HTML table conversion, remote image handling, warning emission, and code-fence safety |
| `PandocPipelineTests` | Pandoc calls, Markdown math/image/table normalization, reference DOCX generation, OpenXML style/page/header/footer correction, table layout, PDF conversion, and `DocumentConversionEngine` behavior |
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
├── ConfigManagerTests.cs
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
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "PandocPipelineTests" -nologo
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "PdfConverterSelectionTests" -nologo
```

## Notes

- Config tests use temporary directories and SQLite databases.
- PDF renderer selection tests use fake renderers where possible so they do not depend on machine-specific Word/LibreOffice installation state.
- Style tests should inspect DOCX structure through OpenXML parts rather than only checking that output files exist.
- Keep fixed test counts out of this README; the project changes often, and the source files are the authority.
