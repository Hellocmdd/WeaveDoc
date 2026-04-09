using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// OpenXML 样式修正：将 AFD 样式规则精确应用到 .docx 文件
/// </summary>
public static class OpenXmlStyleCorrector
{
    private const double MmToTwips = 1440.0 / 25.4; // ≈ 56.693

    /// <summary>
    /// 遍历文档中所有段落，根据其 styleId 查找对应 AFD 样式，
    /// 将字体、字号等属性直接写入每个 Run 的 RunProperties。
    /// </summary>
    public static void ApplyAfdStyles(string docxPath, AfdTemplate template)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var body = doc.MainDocumentPart!.Document.Body!;

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var pPr = paragraph.GetFirstChild<ParagraphProperties>();
            var styleId = pPr?.ParagraphStyleId?.Val?.Value;
            if (styleId == null) continue;

            // 将 OpenXML styleId 反向映射为 AFD 样式键
            var afdKey = AfdStyleMapper.MapToAfdStyleKey(styleId);
            if (afdKey == null || !template.Styles.TryGetValue(afdKey, out var styleDef)) continue;

            // 应用段落属性（对齐、间距、缩进）
            ApplyParagraphProperties(paragraph, pPr!, styleDef);

            // 将字符属性写入每个 Run
            foreach (var run in paragraph.Elements<Run>())
            {
                ApplyRunProperties(run, styleDef);
            }
        }

        doc.MainDocumentPart.Document.Save();
    }

    /// <summary>
    /// 更新文档的页面尺寸和页边距
    /// </summary>
    public static void ApplyPageSettings(string docxPath, AfdDefaults defaults)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var body = doc.MainDocumentPart!.Document.Body!;
        var sectPr = body.Elements<SectionProperties>().LastOrDefault()
                      ?? body.AppendChild(new SectionProperties());

        if (defaults.PageSize != null)
        {
            var pgSz = sectPr.Elements<PageSize>().FirstOrDefault();
            if (pgSz == null)
            {
                pgSz = new PageSize();
                sectPr.AppendChild(pgSz);
            }
            pgSz.Width = (uint)(defaults.PageSize.Width * MmToTwips);
            pgSz.Height = (uint)(defaults.PageSize.Height * MmToTwips);
        }

        if (defaults.Margins != null)
        {
            var pgMar = sectPr.Elements<PageMargin>().FirstOrDefault();
            if (pgMar == null)
            {
                pgMar = new PageMargin();
                sectPr.AppendChild(pgMar);
            }
            pgMar.Top = (int)(defaults.Margins.Top * MmToTwips);
            pgMar.Bottom = (int)(defaults.Margins.Bottom * MmToTwips);
            pgMar.Left = (uint)(defaults.Margins.Left * MmToTwips);
            pgMar.Right = (uint)(defaults.Margins.Right * MmToTwips);
            pgMar.Header = (uint)(12.7 * MmToTwips); // 页眉距边缘 12.7mm
            pgMar.Footer = (uint)(12.7 * MmToTwips); // 页脚距边缘 12.7mm
        }

        doc.MainDocumentPart.Document.Save();
    }

    /// <summary>
    /// 添加页眉和页脚到文档
    /// </summary>
    public static void ApplyHeaderFooter(string docxPath, AfdHeaderFooter headerFooter)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var mainPart = doc.MainDocumentPart!;
        var body = mainPart.Document.Body!;
        var sectPr = body.Elements<SectionProperties>().LastOrDefault()
                      ?? body.AppendChild(new SectionProperties());

        // 确保 HeaderReference / FooterReference 存在
        if (headerFooter.Header != null)
        {
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var headerRef = new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) };
            sectPr.AppendChild(headerRef);

            var header = new Header(new Paragraph());
            var headerPara = header.GetFirstChild<Paragraph>()!;
            var run = headerPara.AppendChild(new Run());
            var rPr = run.AppendChild(new RunProperties());

            if (headerFooter.Header.FontFamily != null)
                rPr.AppendChild(CreateRunFonts(headerFooter.Header.FontFamily));
            if (headerFooter.Header.FontSize != null)
            {
                var hp = ((int)(headerFooter.Header.FontSize.Value * 2)).ToString();
                rPr.AppendChild(new FontSize { Val = hp });
                rPr.AppendChild(new FontSizeComplexScript { Val = hp });
            }

            run.AppendChild(new Text(headerFooter.Header.Text));

            if (headerFooter.Header.Alignment != null)
            {
                var pPr = headerPara.GetFirstChild<ParagraphProperties>()
                          ?? headerPara.AppendChild(new ParagraphProperties());
                pPr.AppendChild(CreateJustification(headerFooter.Header.Alignment));
            }

            headerPart.Header = header;
        }

        if (headerFooter.Footer != null)
        {
            var footerPart = mainPart.AddNewPart<FooterPart>();
            var footerRef = new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) };
            sectPr.AppendChild(footerRef);

            var footer = new Footer(new Paragraph());
            var footerPara = footer.GetFirstChild<Paragraph>()!;

            if (headerFooter.Footer.PageNumbering)
            {
                // 插入页码字段
                var run1 = footerPara.AppendChild(new Run());
                run1.AppendChild(new Text("第 "));
                var run2 = footerPara.AppendChild(new Run());
                var fldCharBegin = new FieldChar { FieldCharType = FieldCharValues.Begin };
                run2.AppendChild(fldCharBegin);
                var run3 = footerPara.AppendChild(new Run());
                run3.AppendChild(new FieldCode { Text = " PAGE " });
                var run4 = footerPara.AppendChild(new Run());
                run4.AppendChild(new FieldChar { FieldCharType = FieldCharValues.Separate });
                var run5 = footerPara.AppendChild(new Run());
                run5.AppendChild(new Text("1")); // 占位符，Word 渲染时会替换为实际页码
                var run6 = footerPara.AppendChild(new Run());
                run6.AppendChild(new FieldChar { FieldCharType = FieldCharValues.End });
                var run7 = footerPara.AppendChild(new Run());
                run7.AppendChild(new Text(" 页"));
            }
            else if (!string.IsNullOrEmpty(headerFooter.Footer.Format))
            {
                var run = footerPara.AppendChild(new Run());
                run.AppendChild(new Text(headerFooter.Footer.Format));
            }

            if (headerFooter.Footer.Alignment != null)
            {
                var pPr = footerPara.GetFirstChild<ParagraphProperties>()
                          ?? footerPara.AppendChild(new ParagraphProperties());
                pPr.AppendChild(CreateJustification(headerFooter.Footer.Alignment));
            }

            footerPart.Footer = footer;

            if (headerFooter.Footer.StartPage != 1)
            {
                sectPr.AppendChild(new PageNumberType { Start = headerFooter.Footer.StartPage });
            }
        }

        doc.MainDocumentPart.Document.Save();
    }

    #region Private helpers

    private static void ApplyParagraphProperties(Paragraph paragraph, ParagraphProperties pPr, AfdStyleDefinition styleDef)
    {
        if (styleDef.Alignment != null)
        {
            var justification = pPr.GetFirstChild<Justification>();
            if (justification == null)
            {
                justification = CreateJustification(styleDef.Alignment);
                pPr.AppendChild(justification);
            }
            else
            {
                justification.Val = CreateJustification(styleDef.Alignment).Val;
            }
        }

        if (styleDef.SpaceBefore != null || styleDef.SpaceAfter != null || styleDef.LineSpacing != null)
        {
            var spacing = pPr.GetFirstChild<SpacingBetweenLines>();
            if (spacing == null)
            {
                spacing = CreateSpacing(styleDef);
                pPr.AppendChild(spacing);
            }
            else
            {
                if (styleDef.SpaceBefore != null)
                    spacing.Before = ((int)(styleDef.SpaceBefore.Value * 20)).ToString();
                if (styleDef.SpaceAfter != null)
                    spacing.After = ((int)(styleDef.SpaceAfter.Value * 20)).ToString();
                if (styleDef.LineSpacing != null)
                {
                    spacing.Line = ((int)(styleDef.LineSpacing.Value * 240)).ToString();
                    spacing.LineRule = LineSpacingRuleValues.Auto;
                }
            }
        }

        if (styleDef.FirstLineIndent != null || styleDef.HangingIndent != null)
        {
            var indent = pPr.GetFirstChild<Indentation>();
            if (indent == null)
            {
                indent = CreateIndentation(styleDef);
                pPr.AppendChild(indent);
            }
            else
            {
                if (styleDef.FirstLineIndent != null)
                    indent.FirstLine = ((int)(styleDef.FirstLineIndent.Value * 20)).ToString();
                if (styleDef.HangingIndent != null)
                    indent.Hanging = ((int)(styleDef.HangingIndent.Value * 20)).ToString();
            }
        }
    }

    private static void ApplyRunProperties(Run run, AfdStyleDefinition styleDef)
    {
        var rPr = run.GetFirstChild<RunProperties>() ?? run.AppendChild(new RunProperties());

        if (styleDef.FontFamily != null)
        {
            var fonts = rPr.GetFirstChild<RunFonts>();
            if (fonts == null)
            {
                fonts = CreateRunFonts(styleDef.FontFamily);
                rPr.AppendChild(fonts);
            }
            else
            {
                fonts.Ascii = styleDef.FontFamily;
                fonts.EastAsia = styleDef.FontFamily;
                fonts.HighAnsi = styleDef.FontFamily;
            }
        }

        if (styleDef.FontSize != null)
        {
            var hp = ((int)(styleDef.FontSize.Value * 2)).ToString();
            var fontSize = rPr.GetFirstChild<FontSize>();
            if (fontSize == null)
            {
                rPr.AppendChild(new FontSize { Val = hp });
            }
            else
            {
                fontSize.Val = hp;
            }
            var fontSizeCs = rPr.GetFirstChild<FontSizeComplexScript>();
            if (fontSizeCs == null)
            {
                rPr.AppendChild(new FontSizeComplexScript { Val = hp });
            }
            else
            {
                fontSizeCs.Val = hp;
            }
        }

        if (styleDef.Bold == true && rPr.GetFirstChild<Bold>() == null)
            rPr.AppendChild(new Bold());

        if (styleDef.Italic == true && rPr.GetFirstChild<Italic>() == null)
            rPr.AppendChild(new Italic());
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

    #endregion
}
