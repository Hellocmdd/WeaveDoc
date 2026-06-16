namespace WeaveDoc.Converter.Config;

/// <summary>
/// 文献库仓储抽象。允许 App 层用假实现进行单测（脱离 SQLite）。
/// </summary>
public interface ILiteratureRepository
{
    Task InitializeAsync();
    Task ImportAsync(IEnumerable<BibtexEntry> entries, string sourceFile, CancellationToken ct = default);
    Task<List<LiteratureEntryRecord>> GetAllAsync(CancellationToken ct = default);
    Task<LiteratureEntryRecord?> GetByKeyAsync(string citationKey, CancellationToken ct = default);
    Task<List<LiteratureEntryRecord>> FindAsync(string query, CancellationToken ct = default);
    Task UpdateFieldAsync(string citationKey, string fieldName, string value, CancellationToken ct = default);
    Task DeleteAsync(string citationKey, CancellationToken ct = default);
    Task WriteBibliographyFileAsync(string outputPath, IEnumerable<string> citationKeys, CancellationToken ct = default);
}
