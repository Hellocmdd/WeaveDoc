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
    public static void Build(string outputPath, AfdTemplate template)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylePart.Styles = new Styles();

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
                Width = (uint)(template.Defaults.PageSize.Width * 567),
                Height = (uint)(template.Defaults.PageSize.Height * 567)
            });
        }
        if (template.Defaults.Margins != null)
        {
            sectPr.AppendChild(new PageMargin
            {
                Top = (int)(template.Defaults.Margins.Top * 567),
                Bottom = (int)(template.Defaults.Margins.Bottom * 567),
                Left = (uint)(template.Defaults.Margins.Left * 567),
                Right = (uint)(template.Defaults.Margins.Right * 567)
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
