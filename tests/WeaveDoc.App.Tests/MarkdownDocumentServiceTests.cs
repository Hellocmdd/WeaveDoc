using WeaveDoc.App.Services.Documents;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class MarkdownDocumentServiceTests
{
    [Fact]
    public async Task ReadAsync_MarkdownFile_ReturnsContentPathDisplayNameAndPreview()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, "demo.md");
            var content = "# 标题\n\n正文";
            var cancellationToken = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            var service = new MarkdownDocumentService();

            var result = await service.ReadAsync(filePath, cancellationToken);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(content, result.Content);
            Assert.Equal(filePath, result.FilePath);
            Assert.Equal("demo.md", result.DisplayName);
            Assert.Contains("<h1 data-line=\"1\">", result.PreviewHtml);
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Theory]
    [InlineData("demo.markdown")]
    [InlineData("notes.txt")]
    public async Task ReadAsync_AcceptsSupportedMarkdownDocumentExtensions(string fileName)
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, fileName);
            var cancellationToken = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(filePath, "正文", cancellationToken);
            var service = new MarkdownDocumentService();

            var result = await service.ReadAsync(filePath, cancellationToken);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(filePath, result.FilePath);
            Assert.Equal(fileName, result.DisplayName);
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesContentToCurrentPath()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, "saved.md");
            var content = "# Saved\n\nBody";
            var service = new MarkdownDocumentService();
            var cancellationToken = TestContext.Current.CancellationToken;

            var result = await service.SaveAsync(filePath, content, cancellationToken);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(content, result.Content);
            Assert.Equal(content, await File.ReadAllTextAsync(filePath, cancellationToken));
            Assert.Equal(filePath, result.FilePath);
            Assert.Equal("saved.md", result.DisplayName);
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void CreatePreview_HeadingKeepsStructureLineAndCharacterPositions()
    {
        var service = new MarkdownDocumentService();

        var result = service.CreatePreview("# 标题");

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("<h1 data-line=\"1\">", result.PreviewHtml);
        Assert.Contains("data-pos=\"1-3\"", result.PreviewHtml);
        Assert.Contains("标", result.PreviewHtml);
        Assert.Contains("题", result.PreviewHtml);
    }

    [Fact]
    public void CreatePreview_LaTeXMarkdownKeepsMathMarkers()
    {
        var service = new MarkdownDocumentService();
        var markdown = "行内 $x+1$\n\n$$\ny=x^2\n$$";

        var result = service.CreatePreview(markdown);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Contains("math-inline", result.PreviewHtml);
        Assert.Contains("math-display", result.PreviewHtml);
    }

    [Fact]
    public async Task ReadAsync_MissingFileReturnsDisplayableFailure()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDirectory, "missing.md");
            var service = new MarkdownDocumentService();

            var result = await service.ReadAsync(filePath, TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(filePath, result.FilePath);
            Assert.Equal("missing.md", result.DisplayName);
            Assert.Empty(result.Content);
            Assert.Empty(result.PreviewHtml);
            Assert.Contains("不存在", result.ErrorMessage);
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenWriteFailsPreservesContentPathAndPreview()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var blockedPath = Path.Combine(tempDirectory, "blocked.md");
            Directory.CreateDirectory(blockedPath);
            var content = "# 当前内容";
            var service = new MarkdownDocumentService();

            var result = await service.SaveAsync(blockedPath, content, TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(content, result.Content);
            Assert.Equal(blockedPath, result.FilePath);
            Assert.Equal("blocked.md", result.DisplayName);
            Assert.Contains("保存", result.ErrorMessage);
            Assert.Contains("<h1 data-line=\"1\">", result.PreviewHtml);
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "weavedoc-markdown-document-service-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
