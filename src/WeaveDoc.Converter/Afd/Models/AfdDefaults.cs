namespace WeaveDoc.Converter.Afd.Models;

/// <summary>全局默认样式</summary>
public record AfdDefaults
{
    public string FontFamily { get; init; } = "宋体";
    public double? FontSize { get; init; }
    public double? LineSpacing { get; init; }
    public AfdPageSize? PageSize { get; init; }
    public AfdMargins? Margins { get; init; }
}

public record AfdPageSize
{
    public double Width { get; init; }
    public double Height { get; init; }
}

public record AfdMargins
{
    public double Top { get; init; }
    public double Bottom { get; init; }
    public double Left { get; init; }
    public double Right { get; init; }
}
