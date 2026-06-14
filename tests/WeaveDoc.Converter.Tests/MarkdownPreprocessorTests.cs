using WeaveDoc.Converter.Pandoc;
using Xunit;

namespace WeaveDoc.Converter.Tests;

public class MarkdownPreprocessorTests
{
    [Fact]
    public async Task PreprocessAsync_ConvertsSimpleHtmlTable_ToPipeTable()
    {
        var tempDir = CreateTempDir();
        var mdPath = Path.Combine(tempDir, "input.md");
        await File.WriteAllTextAsync(mdPath, """
        <table>
          <tr><th>姓名</th><th>分数</th></tr>
          <tr><td>张三</td><td>95</td></tr>
        </table>
        """);

        try
        {
            var result = await new MarkdownPreprocessor().PreprocessAsync(mdPath, Path.Combine(tempDir, "out"));
            var content = await File.ReadAllTextAsync(result.MarkdownPath);

            Assert.Contains("| 姓名 | 分数 |", content);
            Assert.Contains("| --- | --- |", content);
            Assert.Contains("| 张三 | 95 |", content);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    [Fact]
    public async Task PreprocessAsync_DoesNotTouchTableInsideCodeFence()
    {
        var tempDir = CreateTempDir();
        var mdPath = Path.Combine(tempDir, "input.md");
        await File.WriteAllTextAsync(mdPath, """
        ```html
        <table><tr><td>代码</td></tr></table>
        ```
        """);

        try
        {
            var result = await new MarkdownPreprocessor().PreprocessAsync(mdPath, Path.Combine(tempDir, "out"));
            var content = await File.ReadAllTextAsync(result.MarkdownPath);

            Assert.Contains("<table><tr><td>代码</td></tr></table>", content);
            Assert.DoesNotContain("| 代码 |", content);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    [Fact]
    public async Task PreprocessAsync_ComplexHtmlTable_ReturnsWarningAndKeepsOriginal()
    {
        var tempDir = CreateTempDir();
        var mdPath = Path.Combine(tempDir, "input.md");
        await File.WriteAllTextAsync(mdPath, """
        <table>
          <tr><td rowspan="2">姓名</td><td>分数</td></tr>
          <tr><td>95</td></tr>
        </table>
        """);

        try
        {
            var result = await new MarkdownPreprocessor().PreprocessAsync(mdPath, Path.Combine(tempDir, "out"));
            var content = await File.ReadAllTextAsync(result.MarkdownPath);

            Assert.Contains("rowspan", content);
            Assert.Contains(result.Warnings, warning => warning.Code == "html-table.unsupported");
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    [Fact]
    public async Task PreprocessAsync_RemoteMarkdownImage_DownloadsAndRewritesLocalPath()
    {
        await using var server = TestImageServer.StartPng();
        var tempDir = CreateTempDir();
        var mdPath = Path.Combine(tempDir, "input.md");
        await File.WriteAllTextAsync(mdPath, $"![远程图]({server.Url})");

        try
        {
            var result = await new MarkdownPreprocessor().PreprocessAsync(mdPath, Path.Combine(tempDir, "out"));
            var content = await File.ReadAllTextAsync(result.MarkdownPath);

            Assert.Contains("![远程图](", content);
            Assert.Contains("remote-media", content);
            Assert.Contains(result.ResourcePaths, path => path.EndsWith("remote-media"));
            Assert.True(Directory.GetFiles(Path.Combine(tempDir, "out", "remote-media"), "*.png").Length == 1);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    [Fact]
    public async Task PreprocessAsync_RemoteMarkdownImageFailure_DowngradesToAltTextAndWarns()
    {
        await using var server = TestImageServer.StartPng(statusCode: 404);
        var tempDir = CreateTempDir();
        var mdPath = Path.Combine(tempDir, "input.md");
        await File.WriteAllTextAsync(mdPath, $"![失败图]({server.Url})");

        try
        {
            var result = await new MarkdownPreprocessor().PreprocessAsync(mdPath, Path.Combine(tempDir, "out"));
            var content = await File.ReadAllTextAsync(result.MarkdownPath);

            Assert.Contains("失败图", content);
            Assert.DoesNotContain("![失败图]", content);
            Assert.Contains(result.Warnings, warning => warning.Code == "remote-image.download-failed");
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    [Fact]
    public async Task PreprocessAsync_RemoteHtmlImage_DownloadsAndRewritesAsMarkdownImage()
    {
        await using var server = TestImageServer.StartPng();
        var tempDir = CreateTempDir();
        var mdPath = Path.Combine(tempDir, "input.md");
        await File.WriteAllTextAsync(mdPath, $"""<img src="{server.Url}" alt="HTML 图">""");

        try
        {
            var result = await new MarkdownPreprocessor().PreprocessAsync(mdPath, Path.Combine(tempDir, "out"));
            var content = await File.ReadAllTextAsync(result.MarkdownPath);

            Assert.Contains("![HTML 图](", content);
            Assert.Contains("remote-media", content);
            Assert.Contains(result.ResourcePaths, path => path.EndsWith("remote-media"));
            Assert.Empty(result.Warnings);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"preprocess-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
