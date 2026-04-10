using System.Collections.Generic;
using Xunit;
using WeaveDoc.Converter.Afd;

namespace WeaveDoc.Converter.Tests;

public class AfdStyleMapperTests
{
    [Theory]
    [InlineData("heading1", "Heading1")]
    [InlineData("heading2", "Heading2")]
    [InlineData("heading3", "Heading3")]
    [InlineData("heading4", "Heading4")]
    [InlineData("heading5", "Heading5")]
    [InlineData("heading6", "Heading6")]
    [InlineData("body", "Normal")]
    [InlineData("caption", "Caption")]
    [InlineData("footnote", "FootnoteText")]
    [InlineData("reference", "Reference")]
    [InlineData("abstract", "Abstract")]
    public void MapToOpenXmlStyleId_KnownKey_ReturnsCorrectId(string afdKey, string expectedOpenXmlId)
    {
        Assert.Equal(expectedOpenXmlId, AfdStyleMapper.MapToOpenXmlStyleId(afdKey));
    }

    [Fact]
    public void MapToOpenXmlStyleId_UnknownKey_ThrowsKeyNotFoundException()
    {
        Action act = () => AfdStyleMapper.MapToOpenXmlStyleId("nonexistent_style");
        Assert.Throws<KeyNotFoundException>(act);
    }

    [Theory]
    [InlineData("Heading1", "heading1")]
    [InlineData("Heading2", "heading2")]
    [InlineData("Heading3", "heading3")]
    [InlineData("Heading4", "heading4")]
    [InlineData("Heading5", "heading5")]
    [InlineData("Heading6", "heading6")]
    [InlineData("Normal", "body")]
    public void MapToAfdStyleKey_KnownId_ReturnsCorrectKey(string openXmlId, string expectedAfdKey)
    {
        Assert.Equal(expectedAfdKey, AfdStyleMapper.MapToAfdStyleKey(openXmlId));
    }

    [Fact]
    public void MapToAfdStyleKey_UnknownId_ReturnsNull()
    {
        Assert.Null(AfdStyleMapper.MapToAfdStyleKey("UnknownStyle"));
    }
}
