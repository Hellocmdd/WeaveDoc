namespace WeaveDoc.App.Services.Documents;

public sealed record CitationPreviewResult
{
    public CitationPreviewResult(
        string Markdown,
        bool HasCitations,
        IReadOnlyList<string>? missingKeys = null)
    {
        this.Markdown = Markdown;
        this.HasCitations = HasCitations;
        MissingKeys = missingKeys ?? [];
    }

    public string Markdown { get; init; }

    public bool HasCitations { get; init; }

    public IReadOnlyList<string> MissingKeys { get; init; }
}
