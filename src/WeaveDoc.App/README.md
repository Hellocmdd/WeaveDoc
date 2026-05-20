# WeaveDoc.App

English | [简体中文](README.zh-CN.md)

`WeaveDoc.App` is the Avalonia desktop entry for the current branch.

## Scope

- owns the Avalonia startup entry in `Program.cs`
- hosts the main RAG window in `Views/MainWindow.*`
- keeps the UI state in `ViewModels/RagTabViewModel.cs`
- compiles the RAG implementation from the sibling `src/WeaveDoc.Rag/` folder into the same executable

## Build And Run

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

## Evaluation

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

## Runtime Expectations

- local corpus under `doc/`
- local model files under `models/`
- `llama.cpp/` available as a submodule when using the helper scripts
- helper scripts in `scripts/` for launch, evaluation, and submodule refresh
