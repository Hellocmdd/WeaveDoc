using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.App.Services.Documents;

public sealed class MarkdownDocumentService : IMarkdownDocumentService
{
    private static readonly string[] SupportedExtensions = [".md", ".markdown", ".txt"];

    private readonly MarkdownService _markdownService;

    public MarkdownDocumentService()
        : this(new MarkdownService())
    {
    }

    internal MarkdownDocumentService(MarkdownService markdownService)
    {
        _markdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
    }

    public async Task<MarkdownDocumentResult> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateMarkdownPath(filePath);
        if (validationError is not null)
        {
            return MarkdownDocumentResult.Failure(validationError, filePath: filePath);
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return MarkdownDocumentResult.Failure($"Markdown 文件不存在：{filePath}", filePath: filePath);
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return CreatePreview(content, filePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MarkdownDocumentResult.Failure($"读取 Markdown 文件失败：{ex.Message}", filePath: filePath);
        }
    }

    public async Task<MarkdownDocumentResult> SaveAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var normalizedContent = content ?? string.Empty;
        var previewHtml = CreatePreviewHtmlOrEmpty(normalizedContent);
        var validationError = ValidateMarkdownPath(filePath);
        if (validationError is not null)
        {
            return MarkdownDocumentResult.Failure(validationError, normalizedContent, filePath, previewHtml);
        }

        try
        {
            await File.WriteAllTextAsync(filePath, normalizedContent, cancellationToken).ConfigureAwait(false);
            return MarkdownDocumentResult.Success(normalizedContent, filePath, previewHtml);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MarkdownDocumentResult.Failure(
                $"保存 Markdown 文件失败：{ex.Message}",
                normalizedContent,
                filePath,
                previewHtml);
        }
    }

    public MarkdownDocumentResult CreatePreview(string content, string? filePath = null)
    {
        var normalizedContent = content ?? string.Empty;

        try
        {
            var previewHtml = CreatePreviewHtml(normalizedContent);
            return MarkdownDocumentResult.Success(normalizedContent, filePath, previewHtml);
        }
        catch (Exception ex)
        {
            return MarkdownDocumentResult.Failure(
                $"生成 Markdown 预览失败：{ex.Message}",
                normalizedContent,
                filePath);
        }
    }

    private string CreatePreviewHtml(string content)
    {
        return _markdownService.ConvertMarkdownToHtmlWithCharPositions(content);
    }

    private string CreatePreviewHtmlOrEmpty(string content)
    {
        try
        {
            return CreatePreviewHtml(content);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ValidateMarkdownPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Markdown 文件路径不能为空。";
        }

        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return $"不支持的 Markdown 文件类型：{extension}";
        }

        return null;
    }
}
