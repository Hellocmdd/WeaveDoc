# Bug 修复 + 测试补充 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 2 个阻塞 Bug 并补充 5 个测试，使 Converter 库核心管道达到可信赖状态。

**Architecture:** 三处文件变更——根项目 csproj 排除子目录、DCE 修复 PDF 路径、测试文件补充 5 个新测试。所有修改互不依赖，可按任意顺序实施。

**Tech Stack:** .NET 10, xUnit, DocumentFormat.OpenXml, Pandoc CLI

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `WeaveDoc.csproj` | 修改 | 排除 `src/` 和 `tests/` 子目录的 *.cs 文件 |
| `src/WeaveDoc.Converter/DocumentConversionEngine.cs` | 修改 | 修复 PDF 输出路径 Bug |
| `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs` | 修改 | 补充 5 个新测试 |

---

### Task 1: 修复 WeaveDoc.csproj 构建错误（Bug 1）

**Files:**
- Modify: `WeaveDoc.csproj`

- [ ] **Step 1: 确认当前构建失败**

Run: `dotnet build WeaveDoc.csproj 2>&1 | tail -5`
Expected: 大量编译错误（引用 xUnit 等缺失依赖）

- [ ] **Step 2: 添加排除规则**

在 `WeaveDoc.csproj` 的 `</PropertyGroup>` 之后、`</Project>` 之前插入：

```xml
  <ItemGroup>
    <Compile Remove="src\**\*.cs" />
    <Compile Remove="tests\**\*.cs" />
  </ItemGroup>
```

完整文件应为：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="src\**\*.cs" />
    <Compile Remove="tests\**\*.cs" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: 验证构建通过**

Run: `dotnet build WeaveDoc.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: 验证解决方案整体构建通过**

Run: `dotnet build`
Expected: 所有项目构建成功，0 Error(s)

- [ ] **Step 5: 提交**

```bash
git add WeaveDoc.csproj
git commit -m "fix: exclude src/ and tests/ from root WeaveDoc.csproj"
```

---

### Task 2: 修复 DocumentConversionEngine PDF 路径 Bug（Bug 2）

**Files:**
- Modify: `src/WeaveDoc.Converter/DocumentConversionEngine.cs:64-67`

- [ ] **Step 1: 修改 PDF 输出分支**

将 `DocumentConversionEngine.cs` 第 64-67 行：

```csharp
            else if (outputFormat == "pdf")
            {
                await _pandoc.ToPdfAsync(rawDocxPath, outputPath, ct);
            }
```

替换为：

```csharp
            else if (outputFormat == "pdf")
            {
                await _pandoc.ToPdfAsync(markdownPath, outputPath, ct);
            }
```

**仅将 `rawDocxPath` 改为 `markdownPath`**。`rawDocxPath` 是已转换为 DOCX 的文件，而 `ToPdfAsync` 使用 `-f markdown` 格式读取输入，必须传入原始 Markdown 文件。PDF 不经过 OpenXmlStyleCorrector 修正（OpenXML 修正仅适用于 DOCX 格式）。

- [ ] **Step 2: 验证构建通过**

Run: `dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`
Expected: `Build succeeded.`

- [ ] **Step 3: 提交**

```bash
git add src/WeaveDoc.Converter/DocumentConversionEngine.cs
git commit -m "fix: use markdownPath for PDF output in DocumentConversionEngine"
```

---

### Task 3: 测试 DocumentConversionEngine_ConvertAsync_Docx（测试 1）

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 添加测试方法**

在 `PandocPipelineTests.cs` 文件末尾 `}` 之前（`FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx` 测试之后）添加以下测试：

```csharp
    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_Docx()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-test-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new Config.ConfigManager(dbPath);
            var template = CreateTestTemplate();
            await configManager.SaveTemplateAsync("test-tpl", template);

            var pipeline = new PandocPipeline(pandocPath);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "test-tpl", "docx");

                Assert.True(result.Success, $"转换失败: {result.ErrorMessage}");
                Assert.True(File.Exists(result.OutputPath), "输出文件不存在");

                using var doc = WordprocessingDocument.Open(result.OutputPath, false);
                Assert.NotNull(doc.MainDocumentPart);
                Assert.NotNull(doc.MainDocumentPart.Document.Body);
            }
            finally
            {
                File.Delete(mdPath);
                if (File.Exists(Path.ChangeExtension(mdPath, "docx")))
                    File.Delete(Path.ChangeExtension(mdPath, "docx"));
            }
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "DocumentConversionEngine_ConvertAsync_Docx" --verbosity normal`
Expected: `Passed!`

- [ ] **Step 3: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "test: add DocumentConversionEngine DOCX end-to-end test"
```

---

### Task 4: 测试 DocumentConversionEngine 错误路径（测试 2 + 3）

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 添加 MissingTemplate 测试**

在 `DocumentConversionEngine_ConvertAsync_Docx` 测试之后添加：

```csharp
    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_MissingTemplate()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-missing-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new Config.ConfigManager(dbPath);
            var pipeline = new PandocPipeline(pandocPath);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-missing-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "nonexistent-tpl", "docx");

                Assert.False(result.Success);
                Assert.Contains("nonexistent-tpl", result.ErrorMessage);
            }
            finally
            {
                File.Delete(mdPath);
            }
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
```

- [ ] **Step 2: 添加 UnsupportedFormat 测试**

紧接其后添加：

```csharp
    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_UnsupportedFormat()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-unsup-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new Config.ConfigManager(dbPath);
            var template = CreateTestTemplate();
            await configManager.SaveTemplateAsync("test-tpl", template);

            var pipeline = new PandocPipeline(pandocPath);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-unsup-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "test-tpl", "html");

                Assert.False(result.Success);
                Assert.Contains("html", result.ErrorMessage);
            }
            finally
            {
                File.Delete(mdPath);
            }
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
```

- [ ] **Step 3: 运行新测试验证通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "DocumentConversionEngine_ConvertAsync_MissingTemplate|DocumentConversionEngine_ConvertAsync_UnsupportedFormat" --verbosity normal`
Expected: 2 Passed, 0 Failed

- [ ] **Step 4: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "test: add DCE error-path tests for missing template and unsupported format"
```

---

### Task 5: 测试 OpenXmlStyleCorrector_ApplyHeaderFooter（测试 4）

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 添加 ApplyHeaderFooter 测试**

在之前的测试之后添加：

```csharp
    [Fact]
    public void OpenXmlStyleCorrector_ApplyHeaderFooter()
    {
        var template = CreateTestTemplate();
        var docxPath = Path.Combine(Path.GetTempPath(), $"hf-test-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(docxPath, template);

            var headerFooter = new AfdHeaderFooter
            {
                Header = new AfdHeaderContent
                {
                    Text = "测试页眉",
                    FontFamily = "宋体",
                    FontSize = 9,
                    Alignment = "center"
                },
                Footer = new AfdFooterContent
                {
                    PageNumbering = true,
                    Alignment = "center",
                    StartPage = 1
                }
            };

            OpenXmlStyleCorrector.ApplyHeaderFooter(docxPath, headerFooter);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var mainPart = doc.MainDocumentPart!;
            var sectPr = mainPart.Document.Body!.Elements<SectionProperties>().Last();

            // 验证 HeaderPart 存在且包含指定文本
            var headerRefs = sectPr.Elements<HeaderReference>().ToList();
            Assert.Single(headerRefs);

            var headerId = headerRefs[0].Id!.Value!;
            var headerPart = (HeaderPart)mainPart.GetPartById(headerId);
            Assert.NotNull(headerPart.Header);

            var headerPara = headerPart.Header.Elements<Paragraph>().First();
            var headerRun = headerPara.Elements<Run>().First();
            Assert.Equal("测试页眉", headerRun.GetFirstChild<Text>()?.Text);

            // 验证字体
            var rPr = headerRun.RunProperties;
            Assert.NotNull(rPr);
            var fonts = rPr.Elements<RunFonts>().First();
            Assert.Equal("宋体", fonts.EastAsia?.Value);
            var fontSize = rPr.Elements<FontSize>().First();
            Assert.Equal("18", fontSize.Val?.Value); // 9pt = 18 half-points

            // 验证 FooterPart 存在且包含 PAGE 字段
            var footerRefs = sectPr.Elements<FooterReference>().ToList();
            Assert.Single(footerRefs);

            var footerId = footerRefs[0].Id!.Value!;
            var footerPart = (FooterPart)mainPart.GetPartById(footerId);
            Assert.NotNull(footerPart.Footer);

            var footerParas = footerPart.Footer.Elements<Paragraph>().ToList();
            Assert.NotEmpty(footerParas);
            var footerPara = footerParas.First();
            var fieldCodes = footerPara.Elements<Run>()
                .SelectMany(r => r.Elements<FieldCode>())
                .ToList();
            Assert.Contains(fieldCodes, fc => fc.Text?.Contains("PAGE") == true);
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "OpenXmlStyleCorrector_ApplyHeaderFooter" --verbosity normal`
Expected: `Passed!`

- [ ] **Step 3: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "test: add ApplyHeaderFooter test for header text and footer PAGE field"
```

---

### Task 6: 测试 OpenXmlStyleCorrector_ApplyHeaderFooter_StartPage（测试 5）

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 添加 StartPage 测试**

在 `OpenXmlStyleCorrector_ApplyHeaderFooter` 测试之后添加：

```csharp
    [Fact]
    public void OpenXmlStyleCorrector_ApplyHeaderFooter_StartPage()
    {
        var template = CreateTestTemplate();
        var docxPath = Path.Combine(Path.GetTempPath(), $"hf-start-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(docxPath, template);

            var headerFooter = new AfdHeaderFooter
            {
                Footer = new AfdFooterContent
                {
                    PageNumbering = true,
                    Alignment = "center",
                    StartPage = 3
                }
            };

            OpenXmlStyleCorrector.ApplyHeaderFooter(docxPath, headerFooter);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var sectPr = doc.MainDocumentPart!.Document.Body!.Elements<SectionProperties>().Last();

            var pgNumType = sectPr.Elements<PageNumberType>().FirstOrDefault();
            Assert.NotNull(pgNumType);
            Assert.Equal(3, pgNumType.Start?.Value);
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "OpenXmlStyleCorrector_ApplyHeaderFooter_StartPage" --verbosity normal`
Expected: `Passed!`

- [ ] **Step 3: 运行全部测试**

Run: `dotnet test --verbosity normal`
Expected: 52 Passed, 0 Failed（原 47 + 新 5）

- [ ] **Step 4: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "test: add ApplyHeaderFooter StartPage numbering test"
```

---

## 最终验证

- [ ] **Step 1: 解决方案级别构建**

Run: `dotnet build`
Expected: 0 Error(s)

- [ ] **Step 2: 全量测试**

Run: `dotnet test --verbosity normal`
Expected: 52 total tests, all passed
