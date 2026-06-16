using Xunit;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Tests;

public class CitationValidatorTests
{
    private static readonly CitationValidator _validator =
        new(CitationFieldRules.ByEntryType.ToDictionary(
            kv => kv.Key, kv => kv.Value.Required, StringComparer.OrdinalIgnoreCase));

    private static LiteratureEntryRecord Entry(string key, string type, params (string, string)[] fields)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (f, v) in fields) dict[f] = v;
        return new LiteratureEntryRecord { CitationKey = key, EntryType = type, Fields = dict };
    }

    [Fact]
    public async Task Validate_AllFieldsPresent_NoIssues()
    {
        var entry = Entry("k", "article",
            ("author", "Smith"), ("title", "T"), ("journal", "J"), ("year", "2024"),
            ("volume", "1"), ("pages", "1-2"));

        var result = await _validator.ValidateAsync(new[] { "k" }, _ => Task.FromResult<LiteratureEntryRecord?>(entry));

        Assert.Empty(result.Issues);
        Assert.Equal(1, result.CheckedCount);
        Assert.Equal(1, result.ResolvedCount);
        Assert.False(result.HasBlockingErrors);
    }

    [Fact]
    public async Task Validate_UnresolvedKey_ReportedAsUnresolved()
    {
        var result = await _validator.ValidateAsync(new[] { "missing" }, _ => Task.FromResult<LiteratureEntryRecord?>(null));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(CitationIssueKind.Unresolved, issue.Kind);
        Assert.Equal("missing", issue.CitationKey);
        Assert.True(result.HasBlockingErrors);
    }

    [Fact]
    public async Task Validate_ArticleMissingVolume_ReportedAsMissingField()
    {
        var entry = Entry("k", "article",
            ("author", "Smith"), ("title", "T"), ("journal", "J"), ("year", "2024"), ("pages", "1-2"));

        var result = await _validator.ValidateAsync(new[] { "k" }, _ => Task.FromResult<LiteratureEntryRecord?>(entry));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(CitationIssueKind.MissingField, issue.Kind);
        Assert.Equal("volume", issue.FieldName);
        Assert.True(result.HasBlockingErrors); // article 缺字段是 error 级
    }

    [Fact]
    public async Task Validate_AuthorOrEditor_EitherSatisfies()
    {
        var entry = Entry("k", "book",
            ("editor", "Doe"), ("title", "T"), ("publisher", "P"), ("year", "2024"));

        var result = await _validator.ValidateAsync(new[] { "k" }, _ => Task.FromResult<LiteratureEntryRecord?>(entry));

        Assert.Empty(result.Issues); // editor 替代 author，无 issue
    }

    [Fact]
    public async Task Validate_UnknownEntryType_UsesFallbackWarningLevel()
    {
        var entry = Entry("k", "weirdtype", ("author", "A"), ("title", "T"), ("year", "2024"));

        var result = await _validator.ValidateAsync(new[] { "k" }, _ => Task.FromResult<LiteratureEntryRecord?>(entry));

        Assert.Empty(result.Issues); // 回退规则齐全
        Assert.False(result.HasBlockingErrors);
    }

    [Fact]
    public async Task Validate_MultipleKeys_AggregatesIssues()
    {
        var good = Entry("g", "book", ("author", "A"), ("title", "T"), ("publisher", "P"), ("year", "2024"));
        var repo = new Dictionary<string, LiteratureEntryRecord?>(StringComparer.OrdinalIgnoreCase) { ["g"] = good };

        var result = await _validator.ValidateAsync(
            new[] { "g", "x" },
            key => Task.FromResult(repo.TryGetValue(key, out var v) ? v : null));

        Assert.Equal(2, result.CheckedCount);
        Assert.Equal(1, result.ResolvedCount);
        Assert.True(result.HasBlockingErrors); // x 未解析
    }
}

public class CslResourceProviderTests
{
    [Fact]
    public void ExtractToTemp_ReturnsReadableCslFile()
    {
        var path = CslResourceProvider.ExtractToTemp();
        try
        {
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("<style", content);
            Assert.Contains("citation-format=\"numeric\"", content);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
