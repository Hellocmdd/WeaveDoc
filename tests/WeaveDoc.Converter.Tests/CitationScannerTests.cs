using Xunit;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Tests;

public class CitationScannerTests
{
    private readonly CitationScanner _scanner = new();

    [Fact]
    public void Scan_SingleCitation_ExtractsKey()
    {
        var result = _scanner.Scan("Some text [@smith2024] here.");
        Assert.Equal(new[] { "smith2024" }, result.Keys);
    }

    [Fact]
    public void Scan_MultipleCitationsInOneGroup_AllExtracted()
    {
        var result = _scanner.Scan("As shown [@a; @b; @c].");
        Assert.Equal(new[] { "a", "b", "c" }, result.Keys);
    }

    [Fact]
    public void Scan_CitationWithPrefixAndLocator_ExtractsKey()
    {
        var result = _scanner.Scan("see [see @smith2024, pp. 12-13]");
        Assert.Equal(new[] { "smith2024" }, result.Keys);
    }

    [Fact]
    public void Scan_DuplicateKeys_DedupedKeepingFirstOccurrenceOrder()
    {
        var result = _scanner.Scan("[@b] then [@a] then [@b] again [@a]");
        Assert.Equal(new[] { "b", "a" }, result.Keys);
    }

    [Fact]
    public void Scan_DuplicateKeys_RecordsEveryOccurrence()
    {
        var result = _scanner.Scan("[@b] then [@a] then [@b] again [@a]");

        Assert.Equal(new[] { "b", "a" }, result.Keys);
        Assert.Equal(new[] { "b", "a", "b", "a" }, result.Occurrences.Select(o => o.Key));
    }

    [Fact]
    public void Scan_FencedCodeBlock_Ignored()
    {
        var md = """
            Text [@real2024].

            ```
            This is code [@fake2024] and [@fake2025].
            ```

            More [@real2024].
            """;
        var result = _scanner.Scan(md);
        Assert.Equal(new[] { "real2024" }, result.Keys);
    }

    [Fact]
    public void Scan_InlineCode_Ignored()
    {
        var result = _scanner.Scan("See [@real] not `[@fake]` here.");
        Assert.Equal(new[] { "real" }, result.Keys);
    }

    [Fact]
    public void Scan_EscapedCitation_NotMatched()
    {
        var result = _scanner.Scan(@"Escaped \[@notmatched] but [@matched] ok.");
        Assert.Equal(new[] { "matched" }, result.Keys);
    }

    [Fact]
    public void Scan_NegativeCitation_ExtractsKey()
    {
        var result = _scanner.Scan("See [-@neg2024] type.");
        Assert.Equal(new[] { "neg2024" }, result.Keys);
    }

    [Fact]
    public void Scan_NoCitations_ReturnsEmpty()
    {
        var result = _scanner.Scan("Plain text with no citations.");
        Assert.Empty(result.Keys);
    }
}
