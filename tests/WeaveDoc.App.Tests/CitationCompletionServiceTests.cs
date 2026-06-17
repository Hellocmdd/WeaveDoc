using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.Tests.Fakes;
using WeaveDoc.Converter.Config;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class CitationCompletionServiceTests
{
    [Fact]
    public async Task GetSuggestionsAsync_ReturnsKeyTitleAuthorYearForMatchingEntries()
    {
        var repository = new FakeLiteratureRepository();
        repository.Seed(Entry("smith2024", "Citation UX", "Smith", "2024"));
        repository.Seed(Entry("chen2025", "Other", "Chen", "2025"));
        var service = new CitationCompletionService(repository);

        var suggestions = await service.GetSuggestionsAsync("smi", TestContext.Current.CancellationToken);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("smith2024", suggestion.CitationKey);
        Assert.Equal("Citation UX", suggestion.Title);
        Assert.Equal("Smith", suggestion.Authors);
        Assert.Equal("2024", suggestion.Year);
    }

    [Fact]
    public async Task GetSuggestionsAsync_EmptyPrefixReturnsFirstEightEntries()
    {
        var repository = new FakeLiteratureRepository();
        for (var i = 0; i < 10; i++)
        {
            repository.Seed(Entry($"key{i}", $"Title {i}", "A", "2024"));
        }

        var service = new CitationCompletionService(repository);

        var suggestions = await service.GetSuggestionsAsync(string.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(8, suggestions.Count);
        Assert.Equal("key0", suggestions[0].CitationKey);
    }

    private static LiteratureEntryRecord Entry(string key, string title, string authors, string year)
    {
        return new LiteratureEntryRecord
        {
            CitationKey = key,
            EntryType = "article",
            Title = title,
            Authors = authors,
            Year = year,
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = title,
                ["author"] = authors,
                ["year"] = year
            },
            SourceFile = "refs.bib",
            ImportedAt = DateTime.UtcNow.ToString("o")
        };
    }
}
