using Xunit;
using WeaveDoc.Converter;

namespace WeaveDoc.Converter.Tests;

public class ConversionResultWarningsTests
{
    [Fact]
    public void ConversionResult_Warnings_DefaultsToEmpty()
    {
        var result = new ConversionResult { Success = true };
        Assert.NotNull(result.Warnings);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ConversionResult_Warnings_Roundtrips()
    {
        var w = new ConversionWarning("CITATION_MISSING_FIELD", "missing volume", "smith2024");
        var result = new ConversionResult { Success = true, Warnings = new[] { w } };
        var single = Assert.Single(result.Warnings);
        Assert.Equal("CITATION_MISSING_FIELD", single.Code);
        Assert.Equal("smith2024", single.Source);
    }
}
