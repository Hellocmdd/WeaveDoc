using Xunit;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Tests;

public class BibtexParserTests
{
    [Fact]
    public void Parse_BasicArticle_ExtractsFields()
    {
        var bib = """
            @article{smith2024,
              author = {John Smith and Jane Doe},
              title = {A Study on AI},
              journal = {Nature},
              year = {2024},
              volume = {10},
              pages = {1--20}
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        var entry = entries[0];
        Assert.Equal("article", entry.EntryType);
        Assert.Equal("smith2024", entry.CitationKey);
        Assert.Equal("John Smith and Jane Doe", entry.Fields["author"]);
        Assert.Equal("A Study on AI", entry.Fields["title"]);
        Assert.Equal("2024", entry.Fields["year"]);
    }

    [Fact]
    public void Parse_MultipleEntries_ReturnsAll()
    {
        var bib = """
            @article{first, title = {First}}
            @book{second, title = {Second}}
            @inproceedings{third, title = {Third}}
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Equal(3, entries.Count);
        Assert.Equal("first", entries[0].CitationKey);
        Assert.Equal("second", entries[1].CitationKey);
        Assert.Equal("third", entries[2].CitationKey);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        var entries = new BibtexParser().Parse("");
        Assert.Empty(entries);
    }

    [Fact]
    public void ParseSingle_ReturnsFirstEntry()
    {
        var bib = """
            @article{single, title = {Only One}}
            """;

        var entry = new BibtexParser().ParseSingle(bib);

        Assert.NotNull(entry);
        Assert.Equal("single", entry.CitationKey);
    }

    [Fact]
    public void ParseSingle_NoEntry_ReturnsNull()
    {
        var entry = new BibtexParser().ParseSingle("no entries here");
        Assert.Null(entry);
    }
}
