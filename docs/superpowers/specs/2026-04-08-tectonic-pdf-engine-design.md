# Tectonic PDF 引擎集成设计

**日期**: 2026-04-08
**状态**: 已批准
**范围**: `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs`

## 背景

当前 PDF 转换使用 XeLaTeX 引擎，要求用户安装 4GB+ 的 TeX Live/MiKTeX。Tectonic 是基于 XeTeX 的轻量替代（~30MB），排版质量等同 XeLaTeX，但无需手动安装 LaTeX 发行版。

## 方案

### 1. 本地捆绑 Tectonic

- 下载 Tectonic Windows x64 到 `tools/tectonic/tectonic.exe`
- 与 Pandoc 同级目录结构：`tools/pandoc-3.9.0.2/`、`tools/tectonic/`
- 提供下载脚本 `tools/download-tectonic.ps1`
- 二进制文件通过 `.gitignore` 排除

### 2. PandocPipeline 代码修改

**`PandocPipeline.cs`**:
- 新增 `_tectonicDir` 字段，默认 `tools/tectonic/`
- `ToPdfAsync`: `--pdf-engine xelatex` → `--pdf-engine tectonic`
- 保留 `-V CJKmainfont=宋体` 参数（Tectonic 支持）

### 3. PATH 注入

Pandoc 的 `--pdf-engine tectonic` 要求 `tectonic` 在 PATH 中。解决方案：

- 在 `RunAsync` 的 `ProcessStartInfo.EnvironmentVariables["PATH"]` 中注入 `_tectonicDir`
- 仅对 Pandoc 子进程生效，不影响系统全局 PATH

### 4. 不改动的部分

- `ToDocxAsync` 和 `ToAstJsonAsync` 不受影响
- `DocumentConversionEngine` 调用层不需要改动
- 现有测试不需要修改（单元测试 mock 了 PandocPipeline）

## 文件变更清单

| 文件 | 操作 |
|------|------|
| `tools/tectonic/tectonic.exe` | 新增（二进制） |
| `tools/download-tectonic.ps1` | 新增（下载脚本） |
| `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs` | 修改 |
| `.gitignore` | 修改（追加 `tools/tectonic/`） |
