# WeaveDoc.App.Tests

`WeaveDoc.App.Tests` is the headless UI test project for the unified Avalonia desktop shell.

## Coverage

The project currently contains 8 tests:

- `TemplateTabTests`: template grid loading, seed-template visibility, and status text
- `ConvertTabTests`: template loading, validation behavior, format-toggle presence, and DOCX end-to-end conversion
- `RagTabTests`: smoke coverage that verifies the control tree without triggering `InitializeAsync()`

## Files

```text
WeaveDoc.App.Tests/
├── TestAppBuilder.cs
├── TemplateTabTests.cs
├── ConvertTabTests.cs
├── RagTabTests.cs
└── WeaveDoc.App.Tests.csproj
```

## Why `RagTab` Is Only Smoke-Tested Here

`MainWindow` initializes the RAG tab only after the window `Opened` event. That separation is intentional:

- normal UI construction stays cheap
- headless tests do not accidentally load local models
- `RagTab` can still be validated for control-tree and binding smoke coverage

## Run

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "TemplateTabTests" -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "ConvertTabTests" -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj --filter "RagTabTests" -nologo
```
