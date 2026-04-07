namespace WeaveDoc.Converter.Config;

/// <summary>
/// BibTeX 文献解析器
/// </summary>
public class BibtexParser
{
    public List<BibtexEntry> Parse(string bibContent) => throw new NotImplementedException();
    public BibtexEntry? ParseSingle(string entryText) => throw new NotImplementedException();
}

public record BibtexEntry
{
    public string EntryType { get; init; } = "";     // article, book, inproceedings 等
    public string CitationKey { get; init; } = "";    // @article{this_part,
    public Dictionary<string, string> Fields { get; init; } = new();
}
