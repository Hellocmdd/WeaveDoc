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

    [Fact]
    public void Parse_NestedBraces_ExtractsCorrectly()
    {
        var bib = """
            @article{nested,
              title = {A {Very {Nested} Title} Here}
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("A {Very {Nested} Title} Here", entries[0].Fields["title"]);
    }

    [Fact]
    public void Parse_StringAbbreviation_ExpandsValue()
    {
        var bib = """
            @string{jan = "January"}

            @article{abbrev,
              author = {Test Author},
              month = jan
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("January", entries[0].Fields["month"]);
    }

    [Fact]
    public void Parse_QuotedValues_ExtractsCorrectly()
    {
        var bib = """
            @book{book1,
              title = "A Book Title",
              publisher = "Oxford University Press"
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("A Book Title", entries[0].Fields["title"]);
        Assert.Equal("Oxford University Press", entries[0].Fields["publisher"]);
    }

    [Fact]
    public void Parse_SkipsCommentAndPreamble()
    {
        var bib = """
            @comment{Some comment}
            @preamble{Some preamble}
            @article{kept, title = {Kept}}
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("kept", entries[0].CitationKey);
    }

    [Fact]
    public void Parse_MalformedEntry_SilentlySkips()
    {
        var bib = """
            @article{no comma here}
            @article{valid, title = {Valid}}
            """;

        var entries = new BibtexParser().Parse(bib);

        // 第一个条目没有逗号，citation key 后无字段，ParseEntryBody 返回 null
        Assert.Single(entries);
        Assert.Equal("valid", entries[0].CitationKey);
    }
}
