using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Office;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// 使用 Syncfusion DocIO 将 DOCX 转为 PDF，保留所有 OpenXML 样式
/// </summary>
public class SyncfusionPdfConverter
{
    private static readonly IReadOnlyDictionary<string, string> FontAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["宋体"] = "SimSun",
            ["新宋体"] = "NSimSun",
            ["黑体"] = "SimHei",
            ["楷体"] = "KaiTi",
            ["楷体_GB2312"] = "KaiTi",
            ["仿宋"] = "FangSong",
            ["仿宋_GB2312"] = "FangSong",
            ["隶书"] = "LiSu",
            ["幼圆"] = "YouYuan",
            ["微软雅黑"] = "Microsoft YaHei",
            ["等线"] = "DengXian",
            ["华文宋体"] = "STSong",
            ["华文细黑"] = "STXihei",
            ["华文楷体"] = "STKaiti",
            ["华文仿宋"] = "STFangsong"
        };

    public void ConvertToPdf(string docxPath, string pdfPath)
    {
        using var wordDoc = new WordDocument(docxPath);
        ConfigureFontSubstitution(wordDoc);

        using var renderer = new DocIORenderer
        {
            Settings = new DocIORendererSettings
            {
                AutoDetectComplexScript = true,
                EmbedFonts = true,
                EmbedCompleteFonts = true
            }
        };

        using var pdfDoc = renderer.ConvertToPDF(wordDoc);
        pdfDoc.Save(pdfPath);
    }

    private static void ConfigureFontSubstitution(WordDocument wordDoc)
    {
        wordDoc.FontSettings.FallbackFonts.InitializeDefault();
        wordDoc.FontSettings.FallbackFonts.Add(
            ScriptType.Chinese,
            "SimSun, NSimSun, SimHei, Microsoft YaHei, DengXian, MingLiU");

        wordDoc.FontSettings.SubstituteFont += HandleSubstituteFont;
    }

    private static void HandleSubstituteFont(object? sender, SubstituteFontEventArgs args)
    {
        var originalFontName = args.OriginalFontName;
        if (string.IsNullOrWhiteSpace(originalFontName))
            return;

        var normalizedFontName = NormalizeFontFamilyName(originalFontName);
        args.AlternateFontName = normalizedFontName;
    }

    private static string NormalizeFontFamilyName(string fontFamily)
    {
        if (FontAliases.TryGetValue(fontFamily, out var normalized))
            return normalized;

        return fontFamily;
    }
}
