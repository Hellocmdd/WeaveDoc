using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter;

/// <summary>
/// 端到端编排：Markdown → AFD → DOCX/PDF
/// 这是组长唯一需要调用的入口
/// </summary>
public class DocumentConversionEngine
{
    public Task<ConversionResult> ConvertAsync(
        string markdownPath,
        string templateId,
        string outputFormat,
        CancellationToken ct = default) => throw new NotImplementedException();
}

public record ConversionResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = "";
    public string Format { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
}
