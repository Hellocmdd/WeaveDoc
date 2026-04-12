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
    /// 遍历模板样式，将 AFD 属性写入文档的样式定义（styles.xml），
    /// 并清除匹配段落中与样式定义重复的内联属性。
    /// </summary>
    public static void ApplyAfdStyles(string docxPath, AfdTemplate template)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var mainPart = doc.MainDocumentPart!;

        // Phase 1: 将 AFD 属性写入样式定义
        WriteStyleDefinitions(mainPart, template);

        // Phase 2 & 3: 清除冗余内联属性，保留用户有意的行内格式
        StripRedundantInline(mainPart.Document.Body!, template);

        mainPart.Document.Save();
    }

    /// <summary>
    /// 将 AFD 模板中的样式属性写入 StyleDefinitionsPart 中的 Style 元素
    /// </summary>
    private static void WriteStyleDefinitions(MainDocumentPart mainPart, AfdTemplate template)
    {
        var stylesPart = mainPart.StyleDefinitionsPart
            ?? mainPart.AddNewPart<StyleDefinitionsPart>();

        if (stylesPart.Styles == null)
            stylesPart.Styles = new Styles();

        var styles = stylesPart.Styles;

        foreach (var (afdKey, styleDef) in template.Styles)
        {
            var styleId = AfdStyleMapper.MapToOpenXmlStyleId(afdKey);

            // 查找或创建 Style 元素
            var style = styles.Elements<Style>()
                .FirstOrDefault(s => s.StyleId == styleId);

            if (style == null)
            {
                style = new Style
                {
                    Type = StyleValues.Paragraph,
                    StyleId = styleId
                };
                style.AppendChild(new StyleName { Val = styleDef.DisplayName ?? afdKey });
                styles.AppendChild(style);
            }

            // 清除旧的 StyleRunProperties 和 StyleParagraphProperties
            style.RemoveAllChildren<StyleRunProperties>();
            style.RemoveAllChildren<StyleParagraphProperties>();

            // 写入字符属性
            ApplyStyleRunProperties(style, styleDef);

            // 写入段落属性
            ApplyStyleParagraphProperties(style, styleDef);
        }
    }

    private static void ApplyStyleRunProperties(Style style, AfdStyleDefinition styleDef)
    {
        var rPr = new StyleRunProperties();

        if (styleDef.FontFamily != null)
            rPr.AppendChild(CreateRunFonts(styleDef.FontFamily));

        if (styleDef.FontSize != null)
        {
            var hp = ((int)(styleDef.FontSize.Value * 2)).ToString();
            rPr.AppendChild(new FontSize { Val = hp });
            rPr.AppendChild(new FontSizeComplexScript { Val = hp });
        }

        if (styleDef.Bold == true)
            rPr.AppendChild(new Bold());

        if (styleDef.Italic == true)
            rPr.AppendChild(new Italic());

        if (rPr.HasChildren)
            style.AppendChild(rPr);
    }

    private static void ApplyStyleParagraphProperties(Style style, AfdStyleDefinition styleDef)
    {
        var pPr = new StyleParagraphProperties();

        if (styleDef.Alignment != null)
            pPr.AppendChild(CreateJustification(styleDef.Alignment));

        if (styleDef.SpaceBefore != null || styleDef.SpaceAfter != null || styleDef.LineSpacing != null)
            pPr.AppendChild(CreateSpacing(styleDef));

        if (styleDef.FirstLineIndent != null || styleDef.HangingIndent != null)
            pPr.AppendChild(CreateIndentation(styleDef));

        if (pPr.HasChildren)
            style.AppendChild(pPr);
    }

    /// <summary>
    /// 清除与样式定义重复的内联属性：
    /// - 字体、字号：始终从内联中移除（已写入样式定义）
    /// - Bold：仅当样式定义中 Bold==true 时移除
    /// - Italic：仅当样式定义中 Italic==true 时移除
    /// 保留用户有意的行内格式（如正文中的加粗/斜体）
    /// </summary>
    private static void StripRedundantInline(Body body, AfdTemplate template)
    {
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var pPr = paragraph.GetFirstChild<ParagraphProperties>();
            var styleId = pPr?.ParagraphStyleId?.Val?.Value;
            if (styleId == null) continue;

            var afdKey = AfdStyleMapper.MapToAfdStyleKey(styleId);
            if (afdKey == null || !template.Styles.TryGetValue(afdKey, out var styleDef)) continue;

            // 清除段落级冗余内联属性
            if (pPr != null)
            {
                if (styleDef.Alignment != null)
                    pPr.RemoveAllChildren<Justification>();
                if (styleDef.SpaceBefore != null || styleDef.SpaceAfter != null || styleDef.LineSpacing != null)
                    pPr.RemoveAllChildren<SpacingBetweenLines>();
                if (styleDef.FirstLineIndent != null || styleDef.HangingIndent != null)
                    pPr.RemoveAllChildren<Indentation>();
            }

            // 清除 Run 级冗余内联属性
            foreach (var run in paragraph.Elements<Run>())
            {
                var rPr = run.RunProperties;
                if (rPr == null) continue;

                // 字体和字号：始终移除（已写入样式定义）
                rPr.RemoveAllChildren<RunFonts>();
                rPr.RemoveAllChildren<FontSize>();
                rPr.RemoveAllChildren<FontSizeComplexScript>();

                // Bold：仅当样式定义要求加粗时移除
                if (styleDef.Bold == true)
                    rPr.RemoveAllChildren<Bold>();

                // Italic：仅当样式定义要求斜体时移除
                if (styleDef.Italic == true)
                    rPr.RemoveAllChildren<Italic>();

                // 如果 RunProperties 变空了，移除整个元素
                if (!rPr.HasChildren)
                    rPr.Remove();
            }
        }
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
