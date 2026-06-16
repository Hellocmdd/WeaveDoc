namespace WeaveDoc.Converter.Config;

/// <summary>
/// 文献条目对外 DTO（Repository 读出后暴露给 UI / Scanner / Validator）。
/// Fields 是完整字段字典（单一事实来源），其余为拍平索引列。
/// </summary>
public record LiteratureEntryRecord
{
    public string CitationKey { get; init; } = "";
    public string EntryType { get; init; } = "";
    public string Title { get; init; } = "";
    public string Authors { get; init; } = "";
    public string Year { get; init; } = "";
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string SourceFile { get; init; } = "";
    public string ImportedAt { get; init; } = "";
}
