namespace WeaveDoc.Converter.Config;

public enum CitationIssueKind { Unresolved, MissingField }

public record ValidationIssue(string CitationKey, CitationIssueKind Kind, string FieldName, string Message);

public record ValidationResult(
    IReadOnlyList<ValidationIssue> Issues,
    int CheckedCount,
    int ResolvedCount,
    bool HasBlockingErrors);

/// <summary>
/// CON-01 著录完整性校验：对照文献库检查被引用 key 的必需字段。
/// 不直接依赖 Repository，接收 resolver 回调（便于单测）。不阻断导出。
/// </summary>
public class CitationValidator
{
    private readonly IReadOnlyDictionary<string, string[]> _fieldRules;

    /// <param name="fieldRules">entry type → 必需字段列表（key 大小写不敏感）。</param>
    public CitationValidator(IReadOnlyDictionary<string, string[]> fieldRules)
    {
        _fieldRules = new Dictionary<string, string[]>(fieldRules, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ValidationResult> ValidateAsync(
        IReadOnlyList<string> citedKeys,
        Func<string, Task<LiteratureEntryRecord?>> resolver,
        CancellationToken ct = default)
    {
        var issues = new List<ValidationIssue>();
        var blocking = false;
        var resolved = 0;

        foreach (var key in citedKeys)
        {
            var entry = await resolver(key);
            if (entry == null)
            {
                issues.Add(new ValidationIssue(key, CitationIssueKind.Unresolved, "", $"引用 '{key}' 在文献库中未找到"));
                blocking = true;
                continue;
            }
            resolved++;

            var (required, isBlocking) = ResolveRule(entry.EntryType);
            foreach (var field in required)
            {
                if (entry.Fields.ContainsKey(field))
                    continue;
                if (CitationFieldRules.HasAlternative(field, entry.Fields))
                    continue;

                var level = isBlocking ? "error" : "warning";
                issues.Add(new ValidationIssue(
                    key, CitationIssueKind.MissingField, field,
                    $"'{key}'（{entry.EntryType}）缺少字段 '{field}'（GB/T 7714 著录项，{level}）"));
                if (isBlocking) blocking = true;
            }
        }

        return new ValidationResult(issues, citedKeys.Count, resolved, blocking);
    }

    private (string[] Required, bool Blocking) ResolveRule(string entryType)
    {
        if (_fieldRules.TryGetValue(entryType, out var required))
        {
            var blocking = CitationFieldRules.ByEntryType.TryGetValue(entryType, out var builtIn) && builtIn.Blocking;
            return (required, blocking);
        }
        return CitationFieldRules.Fallback;
    }
}
