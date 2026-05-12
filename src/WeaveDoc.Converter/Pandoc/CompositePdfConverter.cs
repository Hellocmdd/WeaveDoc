namespace WeaveDoc.Converter.Pandoc;

public sealed class CompositePdfConverter : IPdfConverter
{
    private readonly IReadOnlyList<IPdfConverter> _converters;

    public CompositePdfConverter(
        PdfRendererDetector detector,
        IPdfConverter? syncfusionFallback = null,
        Func<IPdfConverter>? wordFactory = null,
        Func<string, IPdfConverter>? libreOfficeFactory = null)
    {
        var detected = detector.Detect();
        var converters = new List<IPdfConverter>();
        wordFactory ??= () => new WordComPdfConverter();
        libreOfficeFactory ??= path => new LibreOfficePdfConverter(path);

        if (detected.FirstOrDefault(x => x.Kind == PdfRendererKind.Word)?.IsAvailable == true)
            converters.Add(wordFactory());

        var libreOffice = detected.FirstOrDefault(x => x.Kind == PdfRendererKind.LibreOffice && x.IsAvailable);
        if (!string.IsNullOrWhiteSpace(libreOffice?.ExecutablePath))
            converters.Add(libreOfficeFactory(libreOffice.ExecutablePath));

        converters.Add(syncfusionFallback ?? new SyncfusionPdfConverter());
        _converters = converters;
    }

    public CompositePdfConverter(IEnumerable<IPdfConverter> converters)
    {
        _converters = converters.ToList();
        if (_converters.Count == 0)
            throw new ArgumentException("至少需要一个 PDF 转换器。", nameof(converters));
    }

    public string Name => string.Join(" -> ", _converters.Select(x => x.Name));

    public string LastUsedConverterName { get; private set; } = "";

    public void ConvertToPdf(string docxPath, string pdfPath)
    {
        var failures = new List<string>();
        LastUsedConverterName = "";

        foreach (var converter in _converters)
        {
            try
            {
                converter.ConvertToPdf(docxPath, pdfPath);
                LastUsedConverterName = converter.Name;
                return;
            }
            catch (Exception ex)
            {
                failures.Add($"{converter.Name}: {ex.Message}");
                if (File.Exists(pdfPath))
                    TryDelete(pdfPath);
            }
        }

        throw new InvalidOperationException(
            "所有 PDF 转换引擎均失败：" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
