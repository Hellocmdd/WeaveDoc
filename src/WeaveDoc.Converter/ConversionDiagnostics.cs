namespace WeaveDoc.Converter;

public record ConversionWarning(string Code, string Message, string Source);

public record MarkdownPreprocessResult(
    string MarkdownPath,
    IReadOnlyList<string> ResourcePaths,
    IReadOnlyList<ConversionWarning> Warnings);

public record PandocConversionResult(
    string Stdout,
    IReadOnlyList<ConversionWarning> Warnings);
