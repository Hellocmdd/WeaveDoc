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

    [Fact]
    public void ParseJson_ValidJson_ReturnsTemplate()
    {
        var json = """
        {
          "meta": {
            "templateName": "测试模板",
            "version": "1.0.0",
            "author": "Tester",
            "description": "单元测试用"
          },
          "defaults": {
            "fontFamily": "宋体",
            "fontSize": 12,
            "lineSpacing": 1.5
          },
          "styles": {
            "heading1": {
              "displayName": "标题 1",
              "fontFamily": "黑体",
              "fontSize": 16,
              "bold": true,
              "alignment": "center"
            }
          }
        }
        """;

        var parser = new AfdParser();
        var result = parser.ParseJson(json);

        Assert.Equal("测试模板", result.Meta.TemplateName);
        Assert.Equal("1.0.0", result.Meta.Version);
        Assert.Equal("宋体", result.Defaults.FontFamily);
        Assert.Equal(12, result.Defaults.FontSize);
        Assert.Single(result.Styles);
        Assert.True(result.Styles["heading1"].Bold);
        Assert.Equal("黑体", result.Styles["heading1"].FontFamily);
        Assert.Equal(16, result.Styles["heading1"].FontSize);
    }
}
