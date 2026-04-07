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

    // --- Task 3: 无效 JSON 错误处理 ---

    [Fact]
    public void ParseJson_InvalidJson_ThrowsAfdParseException()
    {
        var parser = new AfdParser();
        Action act = () => parser.ParseJson("{ invalid json !!!");
        var ex = Record.Exception(act);

        var typed = Assert.IsType<AfdParseException>(ex);
        Assert.Contains("JSON 解析失败", typed.Message);
        Assert.NotNull(typed.InnerException);
    }

    // --- Task 4: 文件路径解析 ---

    [Fact]
    public void Parse_ValidFile_ReturnsTemplate()
    {
        var solutionRoot = FindSolutionRoot();
        var jsonPath = Path.Combine(solutionRoot,
            "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", "default-thesis.json");

        var parser = new AfdParser();
        var result = parser.Parse(jsonPath);

        Assert.Equal("默认学术论文", result.Meta.TemplateName);
        Assert.Equal("WeaveDoc", result.Meta.Author);
        Assert.Equal("宋体", result.Defaults.FontFamily);
        Assert.Equal(12, result.Defaults.FontSize);
        Assert.Equal(1.5, result.Defaults.LineSpacing);
        Assert.NotNull(result.Defaults.PageSize);
        Assert.Equal(210, result.Defaults.PageSize.Width);
        Assert.Equal(297, result.Defaults.PageSize.Height);
        Assert.True(result.Styles.ContainsKey("heading1"));
        Assert.True(result.Styles.ContainsKey("heading2"));
        Assert.True(result.Styles.ContainsKey("body"));
        Assert.Equal("黑体", result.Styles["heading1"].FontFamily);
        Assert.Equal(16, result.Styles["heading1"].FontSize);
        Assert.True(result.Styles["heading1"].Bold);
        Assert.Equal("center", result.Styles["heading1"].Alignment);
        Assert.Equal(24, result.Styles["body"].FirstLineIndent);
    }

    [Fact]
    public void Parse_NonexistentFile_ThrowsFileNotFoundException()
    {
        var parser = new AfdParser();
        Action act = () => parser.Parse("/nonexistent/path/template.json");
        Assert.Throws<FileNotFoundException>(act);
    }

    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, ".gitignore")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("无法找到解决方案根目录");
    }
}
