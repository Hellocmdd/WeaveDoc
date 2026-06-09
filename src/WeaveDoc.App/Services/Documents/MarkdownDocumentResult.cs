namespace WeaveDoc.App.Services.Documents;

public sealed record MarkdownDocumentResult(
    bool Succeeded,
    string Content,
    string? FilePath,
    string DisplayName,
    string PreviewHtml,
    string? ErrorMessage)
{
    public static MarkdownDocumentResult Success(string content, string? filePath, string previewHtml)
    {
        return new MarkdownDocumentResult(
            true,
            content ?? string.Empty,
            filePath,
            GetDisplayName(filePath),
            previewHtml ?? string.Empty,
            null);
    }

    public static MarkdownDocumentResult Failure(
        string errorMessage,
        string content = "",
        string? filePath = null,
        string previewHtml = "")
    {
        return new MarkdownDocumentResult(
            false,
            content ?? string.Empty,
            filePath,
            GetDisplayName(filePath),
            previewHtml ?? string.Empty,
            string.IsNullOrWhiteSpace(errorMessage) ? "Markdown 文档操作失败。" : errorMessage);
    }

    private static string GetDisplayName(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath) ? string.Empty : Path.GetFileName(filePath);
    }
}
