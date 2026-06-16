using Xunit;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Tests;

public class LiteratureRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LiteratureRepository _repo;

    public LiteratureRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"weavedoc-lit-{Guid.NewGuid():N}.db");
        _repo = new LiteratureRepository(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task ImportAsync_StoresEntries_RoundtripViaGetAll()
    {
        var entries = new[]
        {
            new BibtexEntry
            {
                EntryType = "article",
                CitationKey = "smith2024",
                Fields = new() { ["title"] = "A Study", ["author"] = "Smith", ["year"] = "2024", ["journal"] = "Nature" }
            }
        };

        await _repo.ImportAsync(entries, "refs.bib");

        var all = await _repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("smith2024", all[0].CitationKey);
        Assert.Equal("article", all[0].EntryType);
        Assert.Equal("A Study", all[0].Title);
        Assert.Equal("Smith", all[0].Authors);
        Assert.Equal("2024", all[0].Year);
        Assert.Equal("refs.bib", all[0].SourceFile);
        Assert.Equal("A Study", all[0].Fields["title"]); // 完整字段字典也回读
    }

    [Fact]
    public async Task ImportAsync_DuplicateKey_UpsertsRatherThanDuplicate()
    {
        var v1 = new BibtexEntry { EntryType = "article", CitationKey = "k", Fields = new() { ["title"] = "Old", ["year"] = "2023" } };
        var v2 = new BibtexEntry { EntryType = "article", CitationKey = "k", Fields = new() { ["title"] = "New", ["year"] = "2024" } };

        await _repo.ImportAsync(new[] { v1 }, "a.bib");
        await _repo.ImportAsync(new[] { v2 }, "b.bib");

        var all = await _repo.GetAllAsync();
        Assert.Single(all); // 不重复
        Assert.Equal("New", all[0].Title);
        Assert.Equal("2024", all[0].Year);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsEntry_OrNull()
    {
        await _repo.ImportAsync(new[]
        {
            new BibtexEntry { EntryType = "book", CitationKey = "jones2023", Fields = new() { ["title"] = "X" } }
        }, "refs.bib");

        var hit = await _repo.GetByKeyAsync("jones2023");
        var miss = await _repo.GetByKeyAsync("nope");

        Assert.NotNull(hit);
        Assert.Equal("X", hit!.Title);
        Assert.Null(miss);
    }

    [Fact]
    public async Task FindAsync_MatchesByTitleAuthorKey()
    {
        await _repo.ImportAsync(new[]
        {
            new BibtexEntry { EntryType = "article", CitationKey = "smith2024", Fields = new() { ["title"] = "Neural Nets", ["author"] = "Smith" } },
            new BibtexEntry { EntryType = "book", CitationKey = "jones2023", Fields = new() { ["title"] = "Trees", ["author"] = "Jones" } }
        }, "refs.bib");

        var byTitle = await _repo.FindAsync("neural");
        var byAuthor = await _repo.FindAsync("jones");
        var byKey = await _repo.FindAsync("smith");

        Assert.Single(byTitle);
        Assert.Equal("smith2024", byTitle[0].CitationKey);
        Assert.Single(byAuthor);
        Assert.Single(byKey);
    }

    [Fact]
    public async Task UpdateFieldAsync_PersistsNewField()
    {
        await _repo.ImportAsync(new[]
        {
            new BibtexEntry { EntryType = "article", CitationKey = "k", Fields = new() { ["title"] = "T" } }
        }, "refs.bib");

        await _repo.UpdateFieldAsync("k", "volume", "42");

        var entry = await _repo.GetByKeyAsync("k");
        Assert.Equal("42", entry!.Fields["volume"]);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        await _repo.ImportAsync(new[]
        {
            new BibtexEntry { EntryType = "article", CitationKey = "k", Fields = new() { ["title"] = "T" } }
        }, "refs.bib");

        await _repo.DeleteAsync("k");

        Assert.Empty(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task WriteBibliographyFileAsync_OutputsCitedKeysInOrder()
    {
        await _repo.ImportAsync(new[]
        {
            new BibtexEntry { EntryType = "article", CitationKey = "b", Fields = new() { ["title"] = "B" } },
            new BibtexEntry { EntryType = "book", CitationKey = "a", Fields = new() { ["title"] = "A" } }
        }, "refs.bib");

        var outPath = Path.Combine(Path.GetTempPath(), $"cited-{Guid.NewGuid():N}.bib");
        try
        {
            await _repo.WriteBibliographyFileAsync(outPath, new[] { "a", "b", "a" });

            var content = await File.ReadAllTextAsync(outPath);
            // 按入参顺序输出，去重（第二个 a 不重复，但 Bibliography 输出本就该去重——校验 a 在 b 前）
            var aPos = content.IndexOf("@book{a,", StringComparison.Ordinal);
            var bPos = content.IndexOf("@article{b,", StringComparison.Ordinal);
            Assert.True(aPos >= 0 && bPos >= 0 && aPos < bPos);
            Assert.Contains("title = {A}", content);
        }
        finally
        {
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public async Task WriteBibliographyFileAsync_EmptyKeys_ProducesEmptyFile()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}.bib");
        try
        {
            await _repo.WriteBibliographyFileAsync(outPath, Array.Empty<string>());
            Assert.Equal("", await File.ReadAllTextAsync(outPath));
        }
        finally
        {
            try { File.Delete(outPath); } catch { }
        }
    }
}
