namespace WeaveDoc.Converter.Afd.Models;

public record AfdNumbering
{
    public AfdHeadingNumbering? HeadingNumbering { get; init; }
    public AfdListStyle? ListStyle { get; init; }
    public Dictionary<string, AfdNumberingInstance> Instances { get; init; } = new();
}

public record AfdHeadingNumbering
{
    public string Format { get; init; } = "decimal";
    public string Separator { get; init; } = ".";
    public int StartNumId { get; init; }
    public List<AfdNumberLevel> Levels { get; init; } = new();
}

public record AfdNumberLevel
{
    public int Level { get; init; }
    public string Format { get; init; } = "";
    public string Suffix { get; init; } = "";
}

public record AfdListStyle
{
    public string Bullet { get; init; } = "●";
    public string OrderedFormat { get; init; } = "1,2,3";
}

public record AfdNumberingInstance
{
    public string Kind { get; init; } = "";
    public int NumId { get; init; }
}

public record AfdParagraphNumbering
{
    public int NumId { get; init; }
    public int Level { get; init; }
}
