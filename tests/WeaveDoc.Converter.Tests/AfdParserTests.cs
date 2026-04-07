using Xunit;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Tests;

public class AfdParserTests
{
    [Fact]
    public void AfdParseException_CanBeThrownAndCaught()
    {
        Action act = () => throw new AfdParseException("test error");
        var ex = Record.Exception(act);

        Assert.IsType<AfdParseException>(ex);
        Assert.Equal("test error", ex.Message);
    }

    [Fact]
    public void AfdParseException_CanWrapInnerException()
    {
        var inner = new InvalidOperationException("inner");
        Action act = () => throw new AfdParseException("outer", inner);
        var ex = Record.Exception(act);

        var typed = Assert.IsType<AfdParseException>(ex);
        Assert.Equal("outer", typed.Message);
        Assert.Same(inner, typed.InnerException);
    }
}
