# WeaveDoc Converter Workstream Notes

这份说明保留“文档转换工作流”的上下文，但对应实现已经并入当前统一结构，不再存在独立的旧 UI 项目。

## 当前映射关系

- 转换核心：`src/WeaveDoc.Converter/`
- 统一桌面入口：`src/WeaveDoc.App/`
- 统一桌面 UI 测试：`tests/WeaveDoc.App.Tests/`
- 转换器测试：`tests/WeaveDoc.Converter.Tests/`
- 主解决方案：`WeaveDoc.slnx`

## 现状说明

原先独立的转换器桌面壳已经收敛进 `WeaveDoc.App`：

- `文档转换` 页签承接原转换向导
- `模板管理` 页签承接原模板管理界面
- `RAG 问答` 页签作为统一桌面中的第三页签追加进来

## 常用命令

```bash
dotnet build WeaveDoc.slnx
dotnet run --project src/WeaveDoc.App/WeaveDoc.App.csproj
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -nologo
dotnet test tests/WeaveDoc.App.Tests/WeaveDoc.App.Tests.csproj -nologo
```

## 外部工具

Pandoc 自动准备仍然保留 Windows 路径，并补齐了 Unix 路径：

- Windows：`tools/setup-tools.ps1`
- Linux/macOS：`tools/setup-tools.sh`

对应分派逻辑在 `tools/DownloadExternalTools.targets` 中统一维护。

## 参考文档

- 统一桌面说明：[src/WeaveDoc.App/README.md](src/WeaveDoc.App/README.md)
- Converter 说明：[src/WeaveDoc.Converter/README.md](src/WeaveDoc.Converter/README.md)
- App 测试说明：[tests/WeaveDoc.App.Tests/README.md](tests/WeaveDoc.App.Tests/README.md)
- Converter 测试说明：[tests/WeaveDoc.Converter.Tests/README.md](tests/WeaveDoc.Converter.Tests/README.md)
