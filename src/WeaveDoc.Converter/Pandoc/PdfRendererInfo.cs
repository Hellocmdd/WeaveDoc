namespace WeaveDoc.Converter.Pandoc;

public enum PdfRendererKind
{
    Word,
    LibreOffice,
    Syncfusion
}

public sealed record PdfRendererInfo(
    PdfRendererKind Kind,
    string DisplayName,
    bool IsAvailable,
    string? ExecutablePath = null,
    string? UnavailableReason = null);
