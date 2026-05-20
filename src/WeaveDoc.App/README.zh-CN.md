# WeaveDoc.App

[English](README.md) | 简体中文

`WeaveDoc.App` 是当前分支的 Avalonia 桌面入口。

## 作用范围

- 持有 `Program.cs` 中的 Avalonia 启动入口
- 在 `Views/MainWindow.*` 中承载主 RAG 窗口
- 在 `ViewModels/RagTabViewModel.cs` 中维护 UI 状态
- 将同级 `src/WeaveDoc.Rag/` 目录下的 RAG 实现一并编译进当前可执行程序

## 构建与运行

```bash
dotnet build src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

## 离线评测

```bash
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj -- --eval ./docs/eval-baseline.json
```

## 运行时依赖

- `doc/` 中的本地语料
- `models/` 中的本地模型文件
- 使用辅助脚本时需要 `llama.cpp/` 子模块
- `scripts/` 中提供启动、评测和更新脚本
