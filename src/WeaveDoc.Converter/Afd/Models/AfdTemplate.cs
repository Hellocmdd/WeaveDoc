namespace WeaveDoc.Converter.Afd.Models;

/// <summary>AFD 模板完整定义</summary>
public record AfdTemplate
{
    public AfdMeta Meta { get; init; } = new();
    public AfdDefaults Defaults { get; init; } = new();
    public Dictionary<string, AfdStyleDefinition> Styles { get; init; } = new();
    public AfdHeaderFooter? HeaderFooter { get; init; }
    public AfdNumbering? Numbering { get; init; }
}
