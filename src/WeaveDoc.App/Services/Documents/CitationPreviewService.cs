using System.Text;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.Services.Documents;

public sealed class CitationPreviewService : ICitationPreviewService
{
    private readonly ILiteratureRepository _repository;
    private readonly CitationScanner _scanner = new();

    public CitationPreviewService(ILiteratureRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<CitationPreviewResult> CreatePreviewMarkdownAsync(
        string markdown,
        CancellationToken cancellationToken = default)
    {
        var normalizedMarkdown = markdown ?? string.Empty;
        var scan = _scanner.Scan(normalizedMarkdown);
        if (scan.Keys.Count == 0)
        {
            return new CitationPreviewResult(normalizedMarkdown, HasCitations: false);
        }

        await _repository.InitializeAsync();

        var entriesByKey = new Dictionary<string, LiteratureEntryRecord?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in scan.Keys)
        {
            entriesByKey[key] = await _repository.GetByKeyAsync(key, cancellationToken);
        }

        var numberByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < scan.Keys.Count; i++)
        {
            numberByKey[scan.Keys[i]] = i + 1;
        }

        var body = ReplaceCitations(normalizedMarkdown, scan.Occurrences, numberByKey, entriesByKey);
        var references = BuildReferenceSection(scan.Keys, numberByKey, entriesByKey);
        var missingKeys = scan.Keys
            .Where(key => !entriesByKey.TryGetValue(key, out var entry) || entry is null)
            .ToList();
        var previewMarkdown = string.IsNullOrWhiteSpace(references)
            ? body
            : $"{body}\n\n## 参考文献\n\n{references}";

        return new CitationPreviewResult(previewMarkdown, HasCitations: true, missingKeys);
    }

    private static string ReplaceCitations(
        string markdown,
        IReadOnlyList<CitationOccurrence> occurrences,
        IReadOnlyDictionary<string, int> numberByKey,
        IReadOnlyDictionary<string, LiteratureEntryRecord?> entriesByKey)
    {
        if (occurrences.Count == 0)
        {
            return markdown;
        }

        var output = new StringBuilder(markdown.Length);
        var cursor = 0;
        foreach (var occurrence in occurrences.OrderBy(o => o.StartPosition))
        {
            var replacementStart = ResolveCitationStart(markdown, occurrence.StartPosition, cursor);
            var replacementEnd = ResolveCitationEnd(markdown, occurrence.StartPosition, occurrence.Key);

            if (replacementStart < cursor)
            {
                continue;
            }

            output.Append(markdown, cursor, replacementStart - cursor);
            var replacement = entriesByKey.TryGetValue(occurrence.Key, out var entry) && entry is not null
                ? $"[{numberByKey[occurrence.Key]}]"
                : $"[? {occurrence.Key}]";
            output.Append(replacement);

            cursor = replacementEnd;
        }

        output.Append(markdown, cursor, markdown.Length - cursor);
        return output.ToString();
    }

    private static int ResolveCitationStart(string markdown, int atPosition, int cursor)
    {
        var start = atPosition;
        if (start > cursor && markdown[start - 1] == '-')
        {
            start--;
        }

        if (start > cursor && markdown[start - 1] == '[')
        {
            start--;
        }

        return start;
    }

    private static int ResolveCitationEnd(string markdown, int atPosition, string key)
    {
        var end = atPosition + key.Length + 1;
        if (end < markdown.Length && markdown[end] == ']')
        {
            end++;
        }

        return end;
    }

    private static string BuildReferenceSection(
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, int> numberByKey,
        IReadOnlyDictionary<string, LiteratureEntryRecord?> entriesByKey)
    {
        var lines = new List<string>();
        foreach (var key in keys)
        {
            if (!entriesByKey.TryGetValue(key, out var entry) || entry is null)
            {
                lines.Add($"[?] {key}：文献库中未找到。");
                continue;
            }

            lines.Add($"[{numberByKey[key]}] {FormatReference(entry)}");
        }

        return string.Join("\n\n", lines);
    }

    private static string FormatReference(LiteratureEntryRecord entry)
    {
        var author = FirstNonEmpty(entry.Authors, GetField(entry, "editor"), "未知作者");
        var title = FirstNonEmpty(entry.Title, "未命名文献");
        var venue = FirstNonEmpty(
            GetField(entry, "journal"),
            GetField(entry, "booktitle"),
            GetField(entry, "publisher"),
            GetField(entry, "school"),
            GetField(entry, "institution"));
        var year = FirstNonEmpty(entry.Year, GetField(entry, "date"));

        var reference = $"{author}. {title}.";
        if (!string.IsNullOrWhiteSpace(venue) && !string.IsNullOrWhiteSpace(year))
        {
            return $"{reference} {venue}, {year}.";
        }

        if (!string.IsNullOrWhiteSpace(venue))
        {
            return $"{reference} {venue}.";
        }

        return string.IsNullOrWhiteSpace(year)
            ? reference
            : $"{reference} {year}.";
    }

    private static string GetField(LiteratureEntryRecord entry, string fieldName)
    {
        return entry.Fields.TryGetValue(fieldName, out var value) ? value : string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
