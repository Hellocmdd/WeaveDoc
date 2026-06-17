namespace WeaveDoc.App.Services.Documents;

public interface ICitationPreviewService
{
    Task<CitationPreviewResult> CreatePreviewMarkdownAsync(
        string markdown,
        CancellationToken cancellationToken = default);
}
