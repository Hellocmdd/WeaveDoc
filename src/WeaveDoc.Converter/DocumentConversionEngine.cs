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
    private readonly LiteratureRepository? _literatureRepository;
    private readonly CitationScanner _citationScanner = new();

    public DocumentConversionEngine(
        PandocPipeline pandoc,
        IPdfConverter pdfConverter,
        ConfigManager configManager,
        LiteratureRepository? literatureRepository = null)
    {
        _pandoc = pandoc;
        _pdfConverter = pdfConverter;
        _configManager = configManager;
        _literatureRepository = literatureRepository;
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

        // 提到 try 之外，便于 catch/finally 访问
        var warnings = new List<ConversionWarning>();
        string? extractedCslPath = null;

        try
        {
            // Step 0: 引用处理（仅当 markdown 含 [@key]）
            CitationContext? citationContext = null;
            try
            {
                var markdown = await File.ReadAllTextAsync(markdownPath, ct);
                var scan = _citationScanner.Scan(markdown);
                if (scan.Keys.Count > 0)
                {
                    if (_literatureRepository == null)
                    {
                        warnings.Add(new ConversionWarning(
                            "CITATION_REPO_UNAVAILABLE",
                            "文档含引用 [@key] 但文献库未加载，引用将无法渲染为文献表",
                            Path.GetFileName(markdownPath)));
                    }
                    else
                    {
                        var validator = new CitationValidator(CitationFieldRules.ByEntryType
                            .ToDictionary(kv => kv.Key, kv => kv.Value.Required, StringComparer.OrdinalIgnoreCase));
                        var validation = await validator.ValidateAsync(
                            scan.Keys, key => _literatureRepository.GetByKeyAsync(key, ct), ct);

                        foreach (var issue in validation.Issues)
                        {
                            var code = issue.Kind == CitationIssueKind.Unresolved
                                ? "CITATION_UNRESOLVED"
                                : "CITATION_MISSING_FIELD";
                            warnings.Add(new ConversionWarning(code, issue.Message, issue.CitationKey));
                        }

                        var bibPath = Path.Combine(tempDir, "cited.bib");
                        await _literatureRepository.WriteBibliographyFileAsync(bibPath, scan.Keys, ct);
                        extractedCslPath = CslResourceProvider.ExtractToTemp();
                        citationContext = new CitationContext(bibPath, extractedCslPath);
                    }
                }
            }
            catch (Exception citeEx)
            {
                // 引用处理失败不阻断导出，降级为无引用渲染
                warnings.Add(new ConversionWarning(
                    "CITATION_PIPELINE_ERROR",
                    $"引用处理失败，已降级为普通导出：{citeEx.Message}",
                    Path.GetFileName(markdownPath)));
            }

            // Step 1: 生成 reference.docx
            var refDocPath = Path.Combine(tempDir, "reference.docx");
            ReferenceDocBuilder.Build(refDocPath, template);

            // Step 2: Pandoc 转换（传入引用上下文）
            var rawDocxPath = Path.Combine(tempDir, "raw.docx");
            await _pandoc.ToDocxAsync(markdownPath, rawDocxPath, refDocPath, citations: citationContext, ct: ct);

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
                    ErrorMessage = $"不支持的输出格式: {outputFormat}",
                    Warnings = warnings
                };
            }

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                Format = outputFormat.ToLowerInvariant(),
                PdfConverterName = string.Equals(outputFormat, "pdf", StringComparison.OrdinalIgnoreCase)
                    ? ResolvePdfConverterName()
                    : "",
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = ConversionErrorFormatter.ToUserMessage(ex, markdownPath, outputFormat),
                TechnicalDetails = ex.ToString(),
                Warnings = warnings
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
            if (extractedCslPath != null)
            {
                try { File.Delete(extractedCslPath); } catch { }
            }
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
    public IReadOnlyList<ConversionWarning> Warnings { get; init; } = Array.Empty<ConversionWarning>();
}
