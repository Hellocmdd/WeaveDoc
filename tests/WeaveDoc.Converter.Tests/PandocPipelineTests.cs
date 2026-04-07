using System.Text.Json;
using Xunit;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter.Tests;

public class PandocPipelineTests
{
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

    private static PandocPipeline CreatePipeline()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc-3.9.0.2", "pandoc.exe");
        return new PandocPipeline(pandocPath);
    }

    private static string CreateTempMarkdown(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pandoc-test-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task ToDocxAsync_InvalidInput_ThrowsException()
    {
        var pipeline = CreatePipeline();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.docx");

        await Assert.ThrowsAsync<Exception>(() =>
            pipeline.ToDocxAsync("/nonexistent/file.md", outputPath));
    }

    [Fact]
    public async Task ToDocxAsync_WithInput_ProducesDocx()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# 测试标题\n\n这是正文段落。\n");
        var docxPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.docx");

        try
        {
            await pipeline.ToDocxAsync(mdPath, docxPath);

            Assert.True(File.Exists(docxPath));
            Assert.True(new FileInfo(docxPath).Length > 0);
        }
        finally
        {
            File.Delete(mdPath);
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public async Task ToAstJsonAsync_ReturnsValidJson()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# 标题\n\n正文内容\n");

        try
        {
            var json = await pipeline.ToAstJsonAsync(mdPath);

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("blocks", out _));
        }
        finally
        {
            File.Delete(mdPath);
        }
    }
}
