namespace WeaveDoc.Converter.Afd.Models;

public record AfdTableStyle
{
    public string? BorderColor { get; init; }
    public double? BorderSize { get; init; }
    public string? HeaderFill { get; init; }
    public bool HeaderBold { get; init; } = true;
    public double? CellMargin { get; init; }
}
