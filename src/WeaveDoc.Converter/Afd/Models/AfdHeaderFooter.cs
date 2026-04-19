namespace WeaveDoc.Converter.Afd.Models;

public record AfdHeaderFooter
{
    public AfdHeaderContent? Header { get; init; }
    public AfdFooterContent? Footer { get; init; }
}

public record AfdHeaderContent
{
    public string Text { get; init; } = "";
    public string? FontFamily { get; init; }
    public double? FontSize { get; init; }
    public string? Alignment { get; init; }
}

public record AfdFooterContent
{
    public bool PageNumbering { get; init; }
    public string Format { get; init; } = "arabic";
    public string? Alignment { get; init; }
    public int StartPage { get; init; } = 1;
}
