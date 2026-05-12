# WeaveDoc

[English](README.md) | 简体中文

WeaveDoc 现在是一个统一桌面工作区，用来承载模板化文档转换、模板管理，以及可选的本地 RAG 工作流。仓库级 README 现在回到整体入口文档的角色。

## 模块组成

- `src/WeaveDoc.App/`：统一 Avalonia 桌面外壳，包含 `文档转换`、`模板管理`、`RAG 问答` 三个页签
- `src/WeaveDoc.Converter/`：Markdown + AFD 模板到 DOCX/PDF 的转换管线
- `src/WeaveDoc.Rag/`：文档索引、检索、重排、聊天接入和离线评测辅助能力

## 仓库结构

- `tests/`：桌面端、转换器、RAG 三组测试项目
- `scripts/`：启动、评测和 `llama.cpp` 辅助脚本
- `docs/`：架构说明与评测基线文件
- `doc/`、`models/`、`.rag/`、`.eval/`：工作区级数据目录
- `llama.cpp/`：本地 RAG 栈依赖的上游子模块
- `WeaveDoc.slnx`：主解决方案入口

## 快速开始

```bash
git clone --recurse-submodules https://github.com/Hellocmdd/WeaveDoc.git
cd WeaveDoc
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
```

Pandoc 会在构建时自动准备：

- Windows 使用 `tools/setup-tools.ps1`
- Linux/macOS 使用 `tools/setup-tools.sh`
- 如需跳过自动下载，可设置 `SkipExternalToolsDownload=true`

如果你当前主要使用文档转换和模板管理，上面的命令就够了。只有在需要本地 RAG 问答或离线评测时，才需要继续阅读 `src/WeaveDoc.Rag/README.zh-CN.md` 中的模型和服务配置说明。

## README 分工

- 仓库级的克隆、构建、测试和模块导航保留在这里。
- RAG 专属的模型准备、`llama-server` 启动、环境变量和评测说明，已经下沉到 `src/WeaveDoc.Rag/README.zh-CN.md`。
- Converter 的实现细节和模板格式说明保留在 `src/WeaveDoc.Converter/README.md`。

这样根 README 会更像项目首页，而不是某一个子模块的运行手册。

## 测试

```bash
dotnet test WeaveDoc.slnx -nologo
```

## 文档索引

- 统一桌面说明（英文）：[src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- Converter 模块说明（中文）：[src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)
- RAG 模块说明（中文）：[src/WeaveDoc.Rag/README.zh-CN.md](src/WeaveDoc.Rag/README.zh-CN.md)
- RAG 架构说明（中文）：[docs/rag-architecture.zh-CN.md](docs/rag-architecture.zh-CN.md)
- 评测基线：[docs/eval-baseline.json](docs/eval-baseline.json)
