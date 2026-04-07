namespace WeaveDoc.Converter.Afd.Models;

/// <summary>模板元信息</summary>
public record AfdMeta
{
    public string TemplateName { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    public string Description { get; init; } = "";
}
