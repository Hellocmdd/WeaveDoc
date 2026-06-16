using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.Tests.Fakes;

/// <summary>
/// 内存假文献仓储，供 LiteratureViewModel 单测使用（脱离 SQLite）。
/// </summary>
internal sealed class FakeLiteratureRepository : ILiteratureRepository
{
    private readonly Dictionary<string, LiteratureEntryRecord> _store = new(StringComparer.OrdinalIgnoreCase);

    public List<string> ImportCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

    public List<(string Key, string Field, string Value)> UpdateFieldCalls { get; } = [];

    public List<string> WriteBibliographyCalls { get; } = [];

    public Exception? ImportException { get; set; }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task ImportAsync(IEnumerable<BibtexEntry> entries, string sourceFile, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ImportCalls.Add(sourceFile);
        if (ImportException is not null) throw ImportException;

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.CitationKey)) continue;
            _store[entry.CitationKey] = new LiteratureEntryRecord
            {
                CitationKey = entry.CitationKey,
                EntryType = entry.EntryType,
                Title = entry.Title,
                Authors = entry.Authors,
                Year = entry.Year,
                Fields = entry.Fields,
                SourceFile = sourceFile,
                ImportedAt = DateTime.UtcNow.ToString("o")
            };
        }
        return Task.CompletedTask;
    }

    public Task<List<LiteratureEntryRecord>> GetAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Values.OrderBy(e => e.CitationKey).ToList());
    }

    public Task<LiteratureEntryRecord?> GetByKeyAsync(string citationKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.TryGetValue(citationKey, out var e) ? e : null);
    }

    public Task<List<LiteratureEntryRecord>> FindAsync(string query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var like = query ?? string.Empty;
        var hits = _store.Values.Where(e =>
            e.Title.Contains(like, StringComparison.OrdinalIgnoreCase) ||
            e.Authors.Contains(like, StringComparison.OrdinalIgnoreCase) ||
            e.CitationKey.Contains(like, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hits.OrderBy(e => e.CitationKey).ToList());
    }

    public Task UpdateFieldAsync(string citationKey, string fieldName, string value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        UpdateFieldCalls.Add((citationKey, fieldName, value));
        if (_store.TryGetValue(citationKey, out var existing))
        {
            var fields = new Dictionary<string, string>(existing.Fields, StringComparer.OrdinalIgnoreCase) { [fieldName] = value };
            _store[citationKey] = existing with { Fields = fields };
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string citationKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        DeleteCalls.Add(citationKey);
        _store.Remove(citationKey);
        return Task.CompletedTask;
    }

    public Task WriteBibliographyFileAsync(string outputPath, IEnumerable<string> citationKeys, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        WriteBibliographyCalls.Add(outputPath);
        return Task.CompletedTask;
    }

    /// <summary>测试种子：直接塞入一条记录。</summary>
    public void Seed(LiteratureEntryRecord entry) => _store[entry.CitationKey] = entry;
}
