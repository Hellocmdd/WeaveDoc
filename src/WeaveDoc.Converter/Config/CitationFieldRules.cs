namespace WeaveDoc.Converter.Config;

/// <summary>GB/T 7714-2015 顺序编码制按 entry type 的必需字段规则。</summary>
public static class CitationFieldRules
{
    /// <summary>entry type → (必需字段, 缺失级别)。entry type 统一小写。</summary>
    public static readonly IReadOnlyDictionary<string, (string[] Required, bool Blocking)> ByEntryType =
        new Dictionary<string, (string[], bool)>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = (new[] { "author", "title", "journal", "year", "volume", "pages" }, true),
            ["book"] = (new[] { "author", "title", "publisher", "year" }, true),
            ["inproceedings"] = (new[] { "author", "title", "booktitle", "year", "pages" }, true),
            ["incollection"] = (new[] { "author", "title", "booktitle", "year", "pages" }, true),
            ["phdthesis"] = (new[] { "author", "title", "school", "year" }, true),
            ["mastersthesis"] = (new[] { "author", "title", "school", "year" }, true),
            ["techreport"] = (new[] { "author", "title", "institution", "year" }, true),
            ["misc"] = (new[] { "author", "title", "year" }, false),
            ["online"] = (new[] { "author", "title", "year" }, false),
        };

    /// <summary>未知 entry type 的回退规则。</summary>
    public static readonly (string[] Required, bool Blocking) Fallback =
        (new[] { "author", "title", "year" }, false);

    /// <summary>某些字段允许互替（author/editor 二选一）。</summary>
    public static bool HasAlternative(string field, IReadOnlyDictionary<string, string> fields)
    {
        if (field == "author")
            return fields.ContainsKey("editor");
        return false;
    }

    public static (string[] Required, bool Blocking) Resolve(string entryType)
    {
        return ByEntryType.TryGetValue(entryType, out var rule) ? rule : Fallback;
    }
}
