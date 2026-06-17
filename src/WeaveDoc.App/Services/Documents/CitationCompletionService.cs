using WeaveDoc.Converter.Config;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.App.Services.Documents;

public sealed class CitationCompletionService
{
    private const int DefaultLimit = 8;

    private readonly ILiteratureRepository _repository;

    public CitationCompletionService(ILiteratureRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<MarkdownCitationCompletionItem>> GetSuggestionsAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync();

        var normalizedPrefix = prefix?.Trim() ?? string.Empty;
        var entries = string.IsNullOrWhiteSpace(normalizedPrefix)
            ? await _repository.GetAllAsync(cancellationToken)
            : await _repository.FindAsync(normalizedPrefix, cancellationToken);

        return entries
            .OrderByDescending(entry => entry.CitationKey.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            .ThenBy(entry => entry.CitationKey, StringComparer.OrdinalIgnoreCase)
            .Take(DefaultLimit)
            .Select(ToCompletionItem)
            .ToList();
    }

    private static MarkdownCitationCompletionItem ToCompletionItem(LiteratureEntryRecord entry)
    {
        return new MarkdownCitationCompletionItem(
            entry.CitationKey,
            entry.Title,
            entry.Authors,
            entry.Year);
    }
}
