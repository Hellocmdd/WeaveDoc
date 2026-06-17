using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.App.Services.ExportTemplates;

public enum TemplatePagePreset
{
    A4,
    A5,
    Letter,
}

public enum TemplateMarginPreset
{
    Standard,
    Narrow,
    Wide,
    Thesis,
}

public enum TemplateFirstLineIndentPreset
{
    None,
    TwoCharacters,
}

public enum TemplateHeadingPreset
{
    Academic,
    Report,
    Compact,
}

public sealed record CustomExportTemplateOptions
{
    public string TemplateName { get; init; } = "自定义模板";
    public string Description { get; init; } = "用户自定义导出模板";
    public string BaseFontFamily { get; init; } = "宋体";
    public double BaseFontSize { get; init; } = 12;
    public double LineSpacing { get; init; } = 1.5;
    public TemplatePagePreset PagePreset { get; init; } = TemplatePagePreset.A4;
    public TemplateMarginPreset MarginPreset { get; init; } = TemplateMarginPreset.Standard;
    public TemplateFirstLineIndentPreset FirstLineIndentPreset { get; init; } = TemplateFirstLineIndentPreset.TwoCharacters;
    public TemplateHeadingPreset HeadingPreset { get; init; } = TemplateHeadingPreset.Academic;
    public string CodeFontFamily { get; init; } = "Consolas";
    public double CodeFontSize { get; init; } = 10;
}

public static class CustomExportTemplateOptionsCatalog
{
    public static IReadOnlyList<string> FontFamilies { get; } =
    [
        "宋体",
        "微软雅黑",
        "黑体",
        "仿宋",
        "楷体",
        "Times New Roman",
        "Arial",
    ];

    public static IReadOnlyList<string> CodeFontFamilies { get; } =
    [
        "Consolas",
        "JetBrains Mono",
        "Courier New",
        "Microsoft YaHei UI",
    ];

    public static IReadOnlyList<double> FontSizes { get; } = [10.5, 12, 14, 16];
    public static IReadOnlyList<double> CodeFontSizes { get; } = [9, 10, 10.5, 12];
    public static IReadOnlyList<double> LineSpacings { get; } = [1.0, 1.15, 1.5, 2.0];
    public static IReadOnlyList<TemplatePagePreset> PagePresets { get; } = Enum.GetValues<TemplatePagePreset>();
    public static IReadOnlyList<TemplateMarginPreset> MarginPresets { get; } = Enum.GetValues<TemplateMarginPreset>();
    public static IReadOnlyList<TemplateFirstLineIndentPreset> FirstLineIndentPresets { get; } = Enum.GetValues<TemplateFirstLineIndentPreset>();
    public static IReadOnlyList<TemplateHeadingPreset> HeadingPresets { get; } = Enum.GetValues<TemplateHeadingPreset>();
}

public static class CustomExportTemplateBuilder
{
    public static AfdTemplate Create(CustomExportTemplateOptions options)
    {
        var templateName = string.IsNullOrWhiteSpace(options.TemplateName)
            ? "自定义模板"
            : options.TemplateName.Trim();
        var description = string.IsNullOrWhiteSpace(options.Description)
            ? "用户自定义导出模板"
            : options.Description.Trim();
        var margins = ResolveMargins(options.MarginPreset);
        var pageSize = ResolvePageSize(options.PagePreset);
        var heading = ResolveHeadingPreset(options.HeadingPreset);
        var firstLineIndent = options.FirstLineIndentPreset == TemplateFirstLineIndentPreset.TwoCharacters
            ? options.BaseFontSize * 2
            : (double?)null;

        return new AfdTemplate
        {
            Meta = new AfdMeta
            {
                TemplateName = templateName,
                Version = "1.0.0",
                Author = "自定义",
                Description = description,
            },
            Defaults = new AfdDefaults
            {
                FontFamily = options.BaseFontFamily,
                FontSize = options.BaseFontSize,
                LineSpacing = options.LineSpacing,
                PageSize = pageSize,
                Margins = margins,
            },
            Styles = new Dictionary<string, AfdStyleDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["heading1"] = new()
                {
                    DisplayName = "标题 1",
                    FontFamily = heading.FontFamily,
                    FontSize = heading.Heading1Size,
                    Bold = true,
                    Alignment = heading.Heading1Alignment,
                    SpaceBefore = heading.Heading1SpaceBefore,
                    SpaceAfter = heading.Heading1SpaceAfter,
                    LineSpacing = options.LineSpacing,
                },
                ["heading2"] = new()
                {
                    DisplayName = "标题 2",
                    FontFamily = heading.FontFamily,
                    FontSize = heading.Heading2Size,
                    Bold = true,
                    Alignment = "left",
                    SpaceBefore = heading.Heading2SpaceBefore,
                    SpaceAfter = heading.Heading2SpaceAfter,
                    LineSpacing = options.LineSpacing,
                },
                ["heading3"] = new()
                {
                    DisplayName = "标题 3",
                    FontFamily = heading.FontFamily,
                    FontSize = heading.Heading3Size,
                    Bold = true,
                    Alignment = "left",
                    SpaceBefore = heading.Heading3SpaceBefore,
                    SpaceAfter = heading.Heading3SpaceAfter,
                    LineSpacing = options.LineSpacing,
                },
                ["heading4"] = new()
                {
                    DisplayName = "标题 4",
                    FontFamily = heading.FontFamily,
                    FontSize = options.BaseFontSize,
                    Bold = true,
                    Alignment = "left",
                    SpaceBefore = 8,
                    SpaceAfter = 4,
                    LineSpacing = options.LineSpacing,
                },
                ["heading5"] = new()
                {
                    DisplayName = "标题 5",
                    FontFamily = heading.FontFamily,
                    FontSize = options.BaseFontSize,
                    Bold = true,
                    Italic = true,
                    Alignment = "left",
                    SpaceBefore = 6,
                    SpaceAfter = 3,
                    LineSpacing = options.LineSpacing,
                },
                ["heading6"] = new()
                {
                    DisplayName = "标题 6",
                    FontFamily = heading.FontFamily,
                    FontSize = options.BaseFontSize,
                    Italic = true,
                    Alignment = "left",
                    SpaceBefore = 6,
                    SpaceAfter = 3,
                    LineSpacing = options.LineSpacing,
                },
                ["body"] = new()
                {
                    DisplayName = "正文",
                    FontFamily = options.BaseFontFamily,
                    FontSize = options.BaseFontSize,
                    FirstLineIndent = firstLineIndent,
                    LineSpacing = options.LineSpacing,
                },
                ["blockquote"] = new()
                {
                    DisplayName = "引用块",
                    FontFamily = options.BaseFontFamily,
                    FontSize = options.BaseFontSize,
                    LineSpacing = options.LineSpacing,
                },
                ["list"] = new()
                {
                    DisplayName = "列表段落",
                    FontFamily = options.BaseFontFamily,
                    FontSize = options.BaseFontSize,
                    LineSpacing = options.LineSpacing,
                },
                ["codeblock"] = new()
                {
                    DisplayName = "代码块",
                    FontFamily = options.CodeFontFamily,
                    FontSize = options.CodeFontSize,
                    LineSpacing = 1.0,
                },
            },
            HeaderFooter = new AfdHeaderFooter
            {
                Header = new AfdHeaderContent
                {
                    Text = templateName,
                    FontFamily = options.BaseFontFamily,
                    FontSize = 10.5,
                    Alignment = "center",
                },
                Footer = new AfdFooterContent
                {
                    PageNumbering = true,
                    Format = "arabic",
                    Alignment = "center",
                    StartPage = 1,
                },
            },
        };
    }

    public static string CreateTemplateId(string templateName)
    {
        var normalized = new string((templateName ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "custom-template";
        return $"custom-{normalized}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private static AfdPageSize ResolvePageSize(TemplatePagePreset preset) => preset switch
    {
        TemplatePagePreset.A5 => new AfdPageSize { Width = 148, Height = 210 },
        TemplatePagePreset.Letter => new AfdPageSize { Width = 216, Height = 279 },
        _ => new AfdPageSize { Width = 210, Height = 297 },
    };

    private static AfdMargins ResolveMargins(TemplateMarginPreset preset) => preset switch
    {
        TemplateMarginPreset.Narrow => new AfdMargins { Top = 15, Bottom = 15, Left = 18, Right = 18 },
        TemplateMarginPreset.Wide => new AfdMargins { Top = 30, Bottom = 30, Left = 35, Right = 35 },
        TemplateMarginPreset.Thesis => new AfdMargins { Top = 25, Bottom = 25, Left = 30, Right = 30 },
        _ => new AfdMargins { Top = 25, Bottom = 25, Left = 25, Right = 25 },
    };

    private static HeadingStylePreset ResolveHeadingPreset(TemplateHeadingPreset preset) => preset switch
    {
        TemplateHeadingPreset.Report => new HeadingStylePreset("黑体", 15, 13, 12, "left", 18, 12, 12, 6, 8, 4),
        TemplateHeadingPreset.Compact => new HeadingStylePreset("黑体", 14, 12, 12, "left", 12, 6, 8, 4, 6, 3),
        _ => new HeadingStylePreset("黑体", 16, 14, 13, "center", 24, 18, 18, 12, 12, 6),
    };

    private sealed record HeadingStylePreset(
        string FontFamily,
        double Heading1Size,
        double Heading2Size,
        double Heading3Size,
        string Heading1Alignment,
        double Heading1SpaceBefore,
        double Heading1SpaceAfter,
        double Heading2SpaceBefore,
        double Heading2SpaceAfter,
        double Heading3SpaceBefore,
        double Heading3SpaceAfter);
}
