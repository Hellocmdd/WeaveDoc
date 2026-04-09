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
    public void Parse_CourseReportJson_ReturnsValidTemplate()
    {
        var solutionRoot = FindSolutionRoot();
        var jsonPath = Path.Combine(solutionRoot,
            "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", "course-report.json");

        var parser = new AfdParser();
        var result = parser.Parse(jsonPath);

        Assert.Equal("课程报告", result.Meta.TemplateName);
        Assert.Equal("WeaveDoc", result.Meta.Author);
        Assert.Equal("宋体", result.Defaults.FontFamily);
        Assert.Equal(12, result.Defaults.FontSize);
        Assert.Equal(1.5, result.Defaults.LineSpacing);
        Assert.Equal(210, result.Defaults.PageSize!.Width);
        Assert.Equal(297, result.Defaults.PageSize.Height);
        Assert.Equal(25, result.Defaults.Margins!.Top);
        Assert.Equal(25, result.Defaults.Margins.Left);

        Assert.True(result.Styles.ContainsKey("heading1"));
        Assert.True(result.Styles.ContainsKey("heading2"));
        Assert.True(result.Styles.ContainsKey("heading3"));
        Assert.True(result.Styles.ContainsKey("body"));

        // 课程报告特有：heading1 16pt, heading2 15pt, heading3 14pt
        Assert.Equal(16, result.Styles["heading1"].FontSize);
        Assert.Equal(15, result.Styles["heading2"].FontSize);
        Assert.Equal(14, result.Styles["heading3"].FontSize);
        Assert.Equal("center", result.Styles["heading1"].Alignment);

        // 页眉页脚
        Assert.NotNull(result.HeaderFooter);
        Assert.Equal("课程报告", result.HeaderFooter.Header!.Text);
        Assert.Equal(10.5, result.HeaderFooter.Header.FontSize);
        Assert.True(result.HeaderFooter.Footer!.PageNumbering);
    }

    [Fact]
    public void Parse_LabReportJson_ReturnsValidTemplate()
    {
        var solutionRoot = FindSolutionRoot();
        var jsonPath = Path.Combine(solutionRoot,
            "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", "lab-report.json");

        var parser = new AfdParser();
        var result = parser.Parse(jsonPath);

        Assert.Equal("实验报告", result.Meta.TemplateName);
        Assert.Equal("WeaveDoc", result.Meta.Author);
        Assert.Equal("宋体", result.Defaults.FontFamily);
        Assert.Equal(12, result.Defaults.FontSize);
        Assert.Equal(1.5, result.Defaults.LineSpacing);
        Assert.Equal(210, result.Defaults.PageSize!.Width);
        Assert.Equal(297, result.Defaults.PageSize.Height);
        Assert.Equal(25.4, result.Defaults.Margins!.Top);
        Assert.Equal(31.7, result.Defaults.Margins.Left);

        Assert.True(result.Styles.ContainsKey("heading1"));
        Assert.True(result.Styles.ContainsKey("heading2"));
        Assert.True(result.Styles.ContainsKey("heading3"));
        Assert.True(result.Styles.ContainsKey("body"));

        // 实验报告特有：heading1 18pt, heading2 15pt, heading3 12pt
        Assert.Equal(18, result.Styles["heading1"].FontSize);
        Assert.Equal(15, result.Styles["heading2"].FontSize);
        Assert.Equal(12, result.Styles["heading3"].FontSize);
        Assert.Equal("center", result.Styles["heading1"].Alignment);

        // 页眉页脚
        Assert.NotNull(result.HeaderFooter);
        Assert.Equal("实验报告", result.HeaderFooter.Header!.Text);
        Assert.Equal(9, result.HeaderFooter.Header.FontSize);
        Assert.True(result.HeaderFooter.Footer!.PageNumbering);
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

    // --- Task 5: Validate 结构校验 ---

    private static AfdTemplate MakeValidTemplate() => new()
    {
        Meta = new AfdMeta { TemplateName = "测试" },
        Defaults = new AfdDefaults(),
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["body"] = new() { DisplayName = "正文" }
        }
    };

    [Fact]
    public void Validate_ValidTemplate_ReturnsTrue()
    {
        var parser = new AfdParser();
        Assert.True(parser.Validate(MakeValidTemplate()));
    }

    [Fact]
    public void Validate_NullMeta_ThrowsAfdParseException()
    {
        var template = MakeValidTemplate() with { Meta = null! };
        var parser = new AfdParser();
        Action act = () => parser.Validate(template);
        var ex = Assert.IsType<AfdParseException>(Record.Exception(act));
        Assert.Contains("meta", ex.Message);
    }

    [Fact]
    public void Validate_EmptyTemplateName_ThrowsAfdParseException()
    {
        var template = MakeValidTemplate() with { Meta = new AfdMeta { TemplateName = "" } };
        var parser = new AfdParser();
        Action act = () => parser.Validate(template);
        var ex = Assert.IsType<AfdParseException>(Record.Exception(act));
        Assert.Contains("templateName", ex.Message);
    }

    [Fact]
    public void Validate_EmptyStyles_ThrowsAfdParseException()
    {
        var template = MakeValidTemplate() with { Styles = new Dictionary<string, AfdStyleDefinition>() };
        var parser = new AfdParser();
        Action act = () => parser.Validate(template);
        var ex = Assert.IsType<AfdParseException>(Record.Exception(act));
        Assert.Contains("styles", ex.Message);
    }

    [Fact]
    public void Validate_NegativeFontSize_ThrowsAfdParseException()
    {
        var template = MakeValidTemplate() with
        {
            Styles = new Dictionary<string, AfdStyleDefinition>
            {
                ["body"] = new() { FontSize = -1 }
            }
        };
        var parser = new AfdParser();
        Action act = () => parser.Validate(template);
        var ex = Assert.IsType<AfdParseException>(Record.Exception(act));
        Assert.Contains("fontSize", ex.Message);
    }
}
