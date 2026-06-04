# WeaveDoc.App.Tests

`WeaveDoc.App.Tests` is the headless Avalonia test project for the unified WeaveDoc desktop shell.

## Scope

| Test class | Coverage |
| --- | --- |
| `MainWindowTests` | Verifies that the unified tab shell contains the embedded `Markdown 编辑` tab |
| `TemplateTabTests` | Template grid loading, seed-template visibility, and status text |
| `ConvertTabTests` | Template loading, input validation, DOCX/PDF format toggles, PDF single/two-column selection, PDF layout handoff, DOCX conversion, and custom output names |

The project intentionally exercises UI construction and command wiring without starting local model loading or long-running RAG initialization work.

## Files

```text
WeaveDoc.App.Tests/
├── TestAppBuilder.cs
├── MainWindowTests.cs
├── TemplateTabTests.cs
├── ConvertTabTests.cs
└── WeaveDoc.App.Tests.csproj
```

## Test Stack

- xUnit v3
- Avalonia Headless
- Avalonia.Headless.XUnit
- `WeaveDoc.App`, `WeaveDoc.Converter`, and `WeaveDoc.MarkdownEditor` project references through the app project

## Run

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "MainWindowTests" -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "TemplateTabTests" -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "ConvertTabTests" -nologo
```

## Notes

- `MainWindow` initializes the RAG view model from the window `Opened` event, so tests can validate shell composition without forcing local model startup.
- `ConvertTabTests` use temporary directories and a test `ConfigManager` database, then clean them in `Dispose()`.
- PDF layout behavior is verified with an injected test converter rather than depending on a machine-specific PDF renderer.
