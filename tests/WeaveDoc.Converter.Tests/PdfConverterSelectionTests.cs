using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;
using Xunit;

namespace WeaveDoc.Converter.Tests;

public class PdfConverterSelectionTests
{
    [Fact]
    public void PdfRendererDetector_DetectLibreOffice_UsesExplicitPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lo-detect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sofficePath = Path.Combine(tempDir, "soffice.exe");
        File.WriteAllText(sofficePath, "");

        try
        {
            var detector = new PdfRendererDetector(
                libreOfficePathOverride: sofficePath,
                isWindowsOverride: false);

            var result = detector.DetectLibreOffice();

            Assert.True(result.IsAvailable);
            Assert.Equal(PdfRendererKind.LibreOffice, result.Kind);
            Assert.Equal(sofficePath, result.ExecutablePath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PdfRendererDetector_DetectLibreOffice_ReturnsReasonWhenMissing()
    {
        var detector = new PdfRendererDetector(
            libreOfficePathOverride: Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "soffice.exe"),
            isWindowsOverride: false);

        var result = detector.DetectLibreOffice();

        Assert.False(result.IsAvailable);
        Assert.Contains("soffice", result.UnavailableReason);
    }

    [Fact]
    public void PdfRendererDetector_DetectWord_NonWindowsDoesNotThrow()
    {
        var detector = new PdfRendererDetector(isWindowsOverride: false);

        var result = detector.DetectWord();

        Assert.False(result.IsAvailable);
        Assert.Equal(PdfRendererKind.Word, result.Kind);
        Assert.Contains("Windows", result.UnavailableReason);
    }

    [Fact]
    public void CompositePdfConverter_WordAvailable_UsesWordFirst()
    {
        var fallback = new RecordingPdfConverter("Syncfusion DocIO", "syncfusion");
        var detector = new PdfRendererDetector(
            isWindowsOverride: true,
            wordComAvailableOverride: true);
        var converter = new CompositePdfConverter(
            detector,
            fallback,
            wordFactory: () => new RecordingPdfConverter("word", "word"),
            libreOfficeFactory: _ => new RecordingPdfConverter("libreoffice", "libreoffice"));
        var outputPath = Path.Combine(Path.GetTempPath(), $"word-first-{Guid.NewGuid():N}.pdf");

        try
        {
            converter.ConvertToPdf("input.docx", outputPath);

            Assert.Equal("word", File.ReadAllText(outputPath));
            Assert.Equal("word", converter.LastUsedConverterName);
            Assert.Equal("word -> Syncfusion DocIO", converter.Name);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void CompositePdfConverter_LibreOfficeAvailable_UsesLibreOfficeBeforeSyncfusion()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lo-choice-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sofficePath = Path.Combine(tempDir, "soffice.exe");
        File.WriteAllText(sofficePath, "");
        var detector = new PdfRendererDetector(
            libreOfficePathOverride: sofficePath,
            isWindowsOverride: false);
        var converter = new CompositePdfConverter(
            detector,
            new RecordingPdfConverter("Syncfusion DocIO", "syncfusion"),
            libreOfficeFactory: _ => new RecordingPdfConverter("libreoffice", "libreoffice"));
        var outputPath = Path.Combine(Path.GetTempPath(), $"lo-first-{Guid.NewGuid():N}.pdf");

        try
        {
            converter.ConvertToPdf("input.docx", outputPath);

            Assert.Equal("libreoffice", File.ReadAllText(outputPath));
            Assert.Equal("libreoffice", converter.LastUsedConverterName);
            Assert.Equal("libreoffice -> Syncfusion DocIO", converter.Name);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CompositePdfConverter_NoExternalRenderer_UsesSyncfusionFallback()
    {
        var detector = new PdfRendererDetector(
            libreOfficePathOverride: Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "soffice.exe"),
            isWindowsOverride: false);
        var converter = new CompositePdfConverter(detector, new RecordingPdfConverter("syncfusion", "syncfusion"));
        var outputPath = Path.Combine(Path.GetTempPath(), $"syncfallback-{Guid.NewGuid():N}.pdf");

        try
        {
            converter.ConvertToPdf("input.docx", outputPath);

            Assert.Equal("syncfusion", File.ReadAllText(outputPath));
            Assert.Equal("syncfusion", converter.LastUsedConverterName);
            Assert.Equal("syncfusion", converter.Name);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_Pdf_UsesInjectedConverter()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"fake-pdf-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new ConfigManager(dbPath);
            await configManager.SaveTemplateAsync("test-tpl", CreateTestTemplate());

            var pipeline = new PandocPipeline(Path.Combine(FindSolutionRoot(), "tools", "pandoc", "pandoc.exe"));
            var converter = new RecordingPdfConverter("fake", "%PDF-1.7 fake");
            var engine = new DocumentConversionEngine(pipeline, converter, configManager);
            var mdPath = Path.Combine(Path.GetTempPath(), $"fake-pdf-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "test-tpl", "pdf");

                Assert.True(result.Success, result.ErrorMessage);
                Assert.True(File.Exists(result.OutputPath));
                Assert.Equal("%PDF-1.7 fake", File.ReadAllText(result.OutputPath));
                Assert.Equal("fake", result.PdfConverterName);
            }
            finally
            {
                File.Delete(mdPath);
                var outputPath = Path.Combine(Path.GetDirectoryName(mdPath)!, $"{Path.GetFileNameWithoutExtension(mdPath)}-测试模板.pdf");
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static Afd.Models.AfdTemplate CreateTestTemplate() => new()
    {
        Meta = new Afd.Models.AfdMeta { TemplateName = "测试模板" },
        Defaults = new Afd.Models.AfdDefaults
        {
            FontFamily = "宋体",
            FontSize = 12,
            LineSpacing = 1.5,
            PageSize = new Afd.Models.AfdPageSize { Width = 210, Height = 297 },
            Margins = new Afd.Models.AfdMargins { Top = 25, Bottom = 25, Left = 30, Right = 30 }
        },
        Styles = new Dictionary<string, Afd.Models.AfdStyleDefinition>
        {
            ["heading1"] = new()
            {
                DisplayName = "标题 1",
                FontFamily = "黑体",
                FontSize = 16,
                Bold = true,
                Alignment = "center"
            },
            ["body"] = new()
            {
                DisplayName = "正文",
                FontFamily = "宋体",
                FontSize = 12
            }
        }
    };

    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, ".gitignore")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("无法找到解决方案根目录");
    }

    private sealed class RecordingPdfConverter : IPdfConverter
    {
        private readonly string _content;
        private readonly string _name;

        public RecordingPdfConverter(string name, string content)
        {
            _name = name;
            _content = content;
        }

        public string Name => _name;

        public void ConvertToPdf(string docxPath, string pdfPath)
        {
            File.WriteAllText(pdfPath, _content);
        }
    }
}
