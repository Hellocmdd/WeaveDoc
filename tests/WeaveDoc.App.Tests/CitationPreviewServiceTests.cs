using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.Tests.Fakes;
using WeaveDoc.Converter.Config;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class CitationPreviewServiceTests
{
    [Fact]
    public async Task CreatePreviewMarkdownAsync_ReplacesCitationsWithStableNumbersAndAppendsReferences()
    {
        var repository = new FakeLiteratureRepository();
        repository.Seed(Entry(
            "smith2024",
            "article",
            ("author", "Smith"),
            ("title", "Citation Preview"),
            ("journal", "Journal of Markdown"),
            ("year", "2024"),
            ("volume", "1"),
            ("pages", "1-9")));
        repository.Seed(Entry(
            "chen2023",
            "book",
            ("author", "Chen"),
            ("title", "Writing Tools"),
            ("publisher", "Weave Press"),
            ("year", "2023")));
        var service = new CitationPreviewService(repository);

        var result = await service.CreatePreviewMarkdownAsync(
            "第一处 [@smith2024]，第二处 [@chen2023]，重复 [@smith2024]。",
            TestContext.Current.CancellationToken);

        Assert.Equal("第一处 [1]，第二处 [2]，重复 [1]。", result.Markdown.Split("\n\n## 参考文献")[0]);
        Assert.Contains("## 参考文献", result.Markdown);
        Assert.Contains("[1] Smith. Citation Preview. Journal of Markdown, 2024.", result.Markdown);
        Assert.Contains("[2] Chen. Writing Tools. Weave Press, 2023.", result.Markdown);
        Assert.True(result.HasCitations);
    }

    [Fact]
    public async Task CreatePreviewMarkdownAsync_MarksUnresolvedCitationWithoutDroppingReferenceSection()
    {
        var service = new CitationPreviewService(new FakeLiteratureRepository());

        var result = await service.CreatePreviewMarkdownAsync(
            "无法解析 [@missing2026]。",
            TestContext.Current.CancellationToken);

        Assert.Contains("无法解析 [? missing2026]。", result.Markdown);
        Assert.Contains("## 参考文献", result.Markdown);
        Assert.Contains("[?] missing2026：文献库中未找到。", result.Markdown);
    }

    [Fact]
    public async Task CreatePreviewMarkdownAsync_DoesNotReplaceCitationsInsideCode()
    {
        var repository = new FakeLiteratureRepository();
        repository.Seed(Entry(
            "real2024",
            "article",
            ("author", "A"),
            ("title", "Real"),
            ("journal", "J"),
            ("year", "2024"),
            ("volume", "1"),
            ("pages", "1")));
        var service = new CitationPreviewService(repository);

        var result = await service.CreatePreviewMarkdownAsync(
            "正文 [@real2024]\n\n```text\n代码 [@fake2024]\n```",
            TestContext.Current.CancellationToken);

        Assert.Contains("正文 [1]", result.Markdown);
        Assert.Contains("代码 [@fake2024]", result.Markdown);
        Assert.DoesNotContain("fake2024：文献库中未找到", result.Markdown);
    }

    private static LiteratureEntryRecord Entry(string key, string type, params (string Field, string Value)[] fields)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, value) in fields)
        {
            dict[field] = value;
        }

        return new LiteratureEntryRecord
        {
            CitationKey = key,
            EntryType = type,
            Title = dict.TryGetValue("title", out var title) ? title : string.Empty,
            Authors = dict.TryGetValue("author", out var author) ? author : string.Empty,
            Year = dict.TryGetValue("year", out var year) ? year : string.Empty,
            Fields = dict,
            SourceFile = "refs.bib",
            ImportedAt = DateTime.UtcNow.ToString("o")
        };
    }
}
