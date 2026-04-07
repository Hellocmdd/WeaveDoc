namespace WeaveDoc.Converter.Afd.Models;

/// <summary>单个样式定义</summary>
public record AfdStyleDefinition
{
    public string? DisplayName { get; init; }
    public string? FontFamily { get; init; }
    public double? FontSize { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public string? Alignment { get; init; }
    public double? SpaceBefore { get; init; }
    public double? SpaceAfter { get; init; }
    public double? LineSpacing { get; init; }
    public double? FirstLineIndent { get; init; }
    public double? HangingIndent { get; init; }
}
