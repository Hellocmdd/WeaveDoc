using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter;

/// <summary>
/// 端到端编排：Markdown → AFD → DOCX/PDF
/// 这是组长唯一需要调用的入口
/// </summary>
public class DocumentConversionEngine
{
    private readonly PandocPipeline _pandoc;
    private readonly IPdfConverter _pdfConverter;
    private readonly ConfigManager _configManager;

    public DocumentConversionEngine(PandocPipeline pandoc, IPdfConverter pdfConverter, ConfigManager configManager)
    {
        _pandoc = pandoc;
        _pdfConverter = pdfConverter;
        _configManager = configManager;
    }

    public async Task<ConversionResult> ConvertAsync(
        string markdownPath,
        string templateId,
        string outputFormat,
        PdfLayoutMode pdfLayoutMode = PdfLayoutMode.SingleColumn,
        CancellationToken ct = default)
    {
        var template = await _configManager.GetTemplateAsync(templateId);
        if (template == null)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = $"模板 '{templateId}' 不存在"
            };
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"weavedoc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Step 1: 生成 reference.docx
            var refDocPath = Path.Combine(tempDir, "reference.docx");
            ReferenceDocBuilder.Build(refDocPath, template);

            // Step 2: Pandoc 转换
            var rawDocxPath = Path.Combine(tempDir, "raw.docx");
            await _pandoc.ToDocxAsync(markdownPath, rawDocxPath, refDocPath, ct: ct);

            // Step 3: OpenXML 样式精确修正
            OpenXmlStyleCorrector.ApplyAfdStyles(rawDocxPath, template);
            OpenXmlStyleCorrector.ApplyPageSettings(rawDocxPath, template.Defaults);

            if (template.HeaderFooter != null)
                OpenXmlStyleCorrector.ApplyHeaderFooter(rawDocxPath, template.HeaderFooter);

            // Step 4: 输出
            var ext = outputFormat.ToLowerInvariant();
            var outputPath = Path.Combine(
                Path.GetDirectoryName(markdownPath) ?? "",
                $"{Path.GetFileNameWithoutExtension(markdownPath)}-{template.Meta.TemplateName}.{ext}");
            if (string.Equals(outputFormat, "docx", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(rawDocxPath, outputPath, overwrite: true);
            }
            else if (string.Equals(outputFormat, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                OpenXmlStyleCorrector.ApplyPdfLayout(rawDocxPath, pdfLayoutMode, _pdfConverter);
                _pdfConverter.ConvertToPdf(rawDocxPath, outputPath);
            }
            else
            {
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = $"不支持的输出格式: {outputFormat}"
                };
            }

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                Format = outputFormat.ToLowerInvariant(),
                PdfConverterName = string.Equals(outputFormat, "pdf", StringComparison.OrdinalIgnoreCase)
                    ? ResolvePdfConverterName()
                    : ""
            };
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = ConversionErrorFormatter.ToUserMessage(ex, markdownPath, outputFormat),
                TechnicalDetails = ex.ToString()
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private string ResolvePdfConverterName()
    {
        if (_pdfConverter is CompositePdfConverter composite
            && !string.IsNullOrWhiteSpace(composite.LastUsedConverterName))
        {
            return composite.LastUsedConverterName;
        }

        return _pdfConverter.Name;
    }
}

public record ConversionResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = "";
    public string Format { get; init; } = "";
    public string PdfConverterName { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
    public string TechnicalDetails { get; init; } = "";
}
