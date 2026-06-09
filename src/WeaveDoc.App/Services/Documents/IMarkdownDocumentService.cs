namespace WeaveDoc.App.Services.Documents;

public interface IMarkdownDocumentService
{
    Task<MarkdownDocumentResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    Task<MarkdownDocumentResult> SaveAsync(string filePath, string content, CancellationToken cancellationToken = default);

    MarkdownDocumentResult CreatePreview(string content, string? filePath = null);
}
