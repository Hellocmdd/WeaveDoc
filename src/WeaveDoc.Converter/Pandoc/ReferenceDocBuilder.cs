using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// 从 AfdTemplate 生成 reference.docx，供 Pandoc --reference-doc 使用
/// </summary>
public static class ReferenceDocBuilder
{
    private const double MmToTwips = 1440.0 / 25.4; // ≈ 56.693

    public static void Build(string outputPath, AfdTemplate template)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylePart.Styles = new Styles();

        // 先创建 Normal（默认段落）样式，确保 Pandoc 生成的正文段落有样式基准
        var normalStyle = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal"
        };
        normalStyle.Append(new StyleName { Val = "Normal" });
        var normalRPr = new StyleRunProperties();
        normalRPr.Append(CreateRunFonts(template.Defaults.FontFamily));
        if (template.Defaults.FontSize != null)
        {
            var hp = ((int)(template.Defaults.FontSize.Value * 2)).ToString();
            normalRPr.Append(new FontSize { Val = hp });
            normalRPr.Append(new FontSizeComplexScript { Val = hp });
        }
        normalStyle.Append(normalRPr);
        stylePart.Styles.Append(normalStyle);

        // 再创建模板中定义的各样式
        foreach (var (afdKey, styleDef) in template.Styles)
        {
            var styleId = AfdStyleMapper.MapToOpenXmlStyleId(afdKey);
            var style = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = styleId,
                CustomStyle = true
            };
            style.Append(new StyleName { Val = styleDef.DisplayName ?? afdKey });

            // 段落属性
            var pPr = new StyleParagraphProperties();
            if (styleDef.Alignment != null)
                pPr.Append(CreateJustification(styleDef.Alignment));
            if (styleDef.LineSpacing != null || styleDef.SpaceBefore != null || styleDef.SpaceAfter != null)
                pPr.Append(CreateSpacing(styleDef));
            if (styleDef.FirstLineIndent != null || styleDef.HangingIndent != null)
                pPr.Append(CreateIndentation(styleDef));
            style.Append(pPr);

            // 字符属性
            var rPr = new StyleRunProperties();
            if (styleDef.FontFamily != null)
                rPr.Append(CreateRunFonts(styleDef.FontFamily));
            if (styleDef.FontSize != null)
            {
                var hp = ((int)(styleDef.FontSize.Value * 2)).ToString();
                rPr.Append(new FontSize { Val = hp });
                rPr.Append(new FontSizeComplexScript { Val = hp });
            }
            if (styleDef.Bold == true)
                rPr.Append(new Bold());
            if (styleDef.Italic == true)
                rPr.Append(new Italic());
            style.Append(rPr);

            stylePart.Styles.Append(style);
        }

        // 页面设置
        var sectPr = mainPart.Document.Body!.AppendChild(new SectionProperties());
        if (template.Defaults.PageSize != null)
        {
            sectPr.AppendChild(new PageSize
            {
                Width = (uint)(template.Defaults.PageSize.Width * MmToTwips),
                Height = (uint)(template.Defaults.PageSize.Height * MmToTwips)
            });
        }
        if (template.Defaults.Margins != null)
        {
            sectPr.AppendChild(new PageMargin
            {
                Top = (int)(template.Defaults.Margins.Top * MmToTwips),
                Bottom = (int)(template.Defaults.Margins.Bottom * MmToTwips),
                Left = (uint)(template.Defaults.Margins.Left * MmToTwips),
                Right = (uint)(template.Defaults.Margins.Right * MmToTwips)
            });
        }

        stylePart.Styles.Save();
        mainPart.Document.Save();
    }

    private static Justification CreateJustification(string alignment) => alignment switch
    {
        "center" => new Justification { Val = JustificationValues.Center },
        "right" => new Justification { Val = JustificationValues.Right },
        "both" => new Justification { Val = JustificationValues.Both },
        _ => new Justification { Val = JustificationValues.Left }
    };

    private static SpacingBetweenLines CreateSpacing(AfdStyleDefinition def)
    {
        var spacing = new SpacingBetweenLines();
        if (def.SpaceBefore != null)
            spacing.Before = ((int)(def.SpaceBefore.Value * 20)).ToString();
        if (def.SpaceAfter != null)
            spacing.After = ((int)(def.SpaceAfter.Value * 20)).ToString();
        if (def.LineSpacing != null)
        {
            spacing.Line = ((int)(def.LineSpacing.Value * 240)).ToString();
            spacing.LineRule = LineSpacingRuleValues.Auto;
        }
        return spacing;
    }

    private static Indentation CreateIndentation(AfdStyleDefinition def)
    {
        var indent = new Indentation();
        if (def.FirstLineIndent != null)
            indent.FirstLine = ((int)(def.FirstLineIndent.Value * 20)).ToString();
        if (def.HangingIndent != null)
            indent.Hanging = ((int)(def.HangingIndent.Value * 20)).ToString();
        return indent;
    }

    private static RunFonts CreateRunFonts(string fontFamily) => new()
    {
        Ascii = fontFamily,
        EastAsia = fontFamily,
        HighAnsi = fontFamily
    };
}
