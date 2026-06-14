using System;
using System.Collections.Generic;
using System.Text;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace WeaveDoc.MarkdownEditor.Services;

public sealed class MarkdigMarkdownRenderService : IMarkdownRenderService
{
    private static readonly MarkdownPipeline Pipeline = CreatePipeline();

    private static MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseTaskLists()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .UseGenericAttributes();
            
        builder.Extensions.Add(new CustomMathExtension());

        return builder.Build();
    }

    private class CustomMathExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (!pipeline.BlockParsers.Contains<MathBlockParser>())
            {
                pipeline.BlockParsers.Insert(0, new MathBlockParser());
            }
            if (!pipeline.InlineParsers.Contains<RelaxedMathInlineParser>())
            {
                pipeline.InlineParsers.Insert(0, new RelaxedMathInlineParser());
            }
        }

        public void Setup(MarkdownPipeline pipeline, Markdig.Renderers.IMarkdownRenderer renderer)
        {
            // We use custom AST-to-HTML rendering, so no Markdig renderers needed here.
        }
    }

    private string _source = string.Empty;
    private int[] _lineOffsets = Array.Empty<int>();
    private StringBuilder _html = new();

    public string RenderPreviewHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        _source = markdown;
        _lineOffsets = ComputeLineOffsets(markdown);
        _html.Clear();

        var doc = Markdig.Markdown.Parse(markdown, Pipeline);
        RenderBlocks(doc);

        return _html.ToString();
    }

    private void RenderBlocks(ContainerBlock container)
    {
        foreach (var block in container)
        {
            RenderBlock(block);
        }
    }

    private void RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(heading);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(paragraph);
                break;
            case MathBlock mathBlock:
                RenderMathBlock(mathBlock);
                break;
            case CodeBlock codeBlock:
                RenderCodeBlock(codeBlock);
                break;
            case QuoteBlock quote:
                RenderQuote(quote);
                break;
            case ListBlock list:
                RenderList(list);
                break;
            case Table table:
                RenderTable(table);
                break;
            case ThematicBreakBlock:
                _html.Append($"<hr data-line=\"{block.Line + 1}\">\n");
                break;
            case HtmlBlock htmlBlock:
                // Emit raw HTML directly — Markdig already identified this as intentional raw HTML.
                // Escaping would turn <div> into &lt;div&gt; and show tags as plain text.
                _html.Append(htmlBlock.Lines.ToString());
                break;
            default:
                if (block is ContainerBlock container)
                    RenderBlocks(container);
                break;
        }
    }

    private void RenderHeading(HeadingBlock heading)
    {
        var level = heading.Level;
        _html.Append($"<h{level} data-line=\"{heading.Line + 1}\">");
        RenderInlines(heading.Inline);
        _html.Append($"</h{level}>\n");
    }

    private void RenderParagraph(ParagraphBlock paragraph)
    {
        _html.Append($"<p data-line=\"{paragraph.Line + 1}\">");
        RenderInlines(paragraph.Inline);
        _html.Append("</p>\n");
    }

    private void RenderCodeBlock(CodeBlock codeBlock)
    {
        var lang = (codeBlock as Markdig.Syntax.FencedCodeBlock)?.Info ?? string.Empty;
        _html.Append($"<pre><code class=\"language-{EscapeHtml(lang)}\" data-line=\"{codeBlock.Line + 1}\">");

        var lines = codeBlock.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines.Lines[i].Slice.ToString();
            var lineNumber = codeBlock.Line + 1 + i;
            _html.Append($"<span data-line=\"{lineNumber}\">{EscapeHtml(line)}</span>");
        }

        _html.Append("</code></pre>\n");
    }

    private void RenderQuote(QuoteBlock quote)
    {
        _html.Append($"<blockquote data-line=\"{quote.Line + 1}\">");
        RenderBlocks(quote);
        _html.Append("</blockquote>\n");
    }

    private void RenderList(ListBlock list)
    {
        var tag = list.IsOrdered ? "ol" : "ul";
        if (list.Count > 0)
        {
            _html.Append($"<{tag}>\n");
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                    RenderListItem(listItem);
            }
            _html.Append($"</{tag}>\n");
        }
    }

    private void RenderListItem(ListItemBlock listItem)
    {
        _html.Append($"<li data-line=\"{listItem.Line + 1}\">");
        foreach (var subBlock in listItem)
        {
            RenderBlock(subBlock);
        }
        _html.Append("</li>\n");
    }

    private void RenderTable(Table table)
    {
        _html.Append($"<table data-line=\"{table.Line + 1}\">\n");

        // Group consecutive header rows under <thead> and body rows under <tbody>.
        var inHead = false;
        var inBody = false;
        foreach (var row in table)
        {
            if (row is not TableRow tableRow)
                continue;

            if (tableRow.IsHeader)
            {
                if (inBody)
                {
                    _html.Append("</tbody>\n");
                    inBody = false;
                }
                if (!inHead)
                {
                    _html.Append("<thead>\n");
                    inHead = true;
                }
                RenderTableRow(tableRow, "th", table);
            }
            else
            {
                if (inHead)
                {
                    _html.Append("</thead>\n");
                    inHead = false;
                }
                if (!inBody)
                {
                    _html.Append("<tbody>\n");
                    inBody = true;
                }
                RenderTableRow(tableRow, "td", table);
            }
        }

        if (inHead)
            _html.Append("</thead>\n");
        if (inBody)
            _html.Append("</tbody>\n");
        _html.Append("</table>\n");
    }

    private void RenderTableRow(TableRow row, string cellTag, Table table)
    {
        _html.Append("<tr>\n");
        var columnIndex = 0;
        foreach (var cell in row)
        {
            if (cell is not TableCell tableCell)
                continue;

            var alignment = GetColumnAlignment(table, columnIndex);
            var style = string.IsNullOrEmpty(alignment)
                ? string.Empty
                : $" style=\"text-align:{alignment}\"";

            _html.Append($"<{cellTag}{style} data-line=\"{tableCell.Line + 1}\">");
            RenderTableCellContent(tableCell);
            _html.Append($"</{cellTag}>\n");

            columnIndex++;
        }
        _html.Append("</tr>\n");
    }

    private void RenderTableCellContent(TableCell cell)
    {
        // Markdig wraps each cell's content in a ParagraphBlock; render its inlines
        // directly (no <p> wrapper) to keep the table compact and the HTML valid.
        foreach (var block in cell)
        {
            if (block is ParagraphBlock paragraph)
                RenderInlines(paragraph.Inline);
            else
                RenderBlock(block);
        }
    }

    private static string GetColumnAlignment(Table table, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= table.ColumnDefinitions.Count)
            return string.Empty;

        return table.ColumnDefinitions[columnIndex].Alignment switch
        {
            TableColumnAlign.Left => "left",
            TableColumnAlign.Center => "center",
            TableColumnAlign.Right => "right",
            _ => string.Empty
        };
    }

    private void RenderMathBlock(MathBlock mathBlock)
    {
        var content = mathBlock.Lines.ToString();
        // HTML-escape the LaTeX content so that < > & in formulas do not break the HTML wrapper.
        // KaTeX reads via el.textContent which automatically decodes HTML entities.
        _html.Append($"<div class=\"math-display\" data-line=\"{mathBlock.Line + 1}\">{EscapeHtml(content)}</div>\n");
    }

    private void RenderInlines(ContainerInline? container)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            RenderInline(inline);
        }
    }

    private void RenderInline(Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                RenderLiteral(literal);
                break;
            case CodeInline code:
                _html.Append($"<code>{EscapeHtml(code.Content)}</code>");
                break;
            case EmphasisInline emphasis:
                RenderEmphasis(emphasis);
                break;
            case LinkInline link:
                RenderLink(link);
                break;
            case LineBreakInline lineBreak:
                _html.Append(lineBreak.IsHard ? "<br>\n" : "\n");
                break;
            case MathInline math:
                RenderMathInline(math);
                break;
            case HtmlInline htmlInline:
                _html.Append(htmlInline.Tag);
                break;
            case ContainerInline container:
                RenderInlines(container);
                break;
            default:
                RenderInlines(inline as ContainerInline);
                break;
        }
    }

    private void RenderLiteral(LiteralInline literal)
    {
        var content = literal.Content.ToString();
        if (content.Length == 0)
            return;

        // Use the start of the inline's source span as the anchor for data-pos.
        // Emit one <span> per text run (not per character) to keep DOM size small.
        // JS computes per-character offsets via Range.toString().length.
        var span = literal.Span;
        var offset = span.IsEmpty ? -1 : span.Start;

        if (offset >= 0 && offset < _source.Length)
        {
            var (line, col) = OffsetToLineCol(offset);
            _html.Append($"<span data-pos=\"{line}-{col}\">{EscapeHtml(content)}</span>");
        }
        else
        {
            _html.Append(EscapeHtml(content));
        }
    }

    private void RenderEmphasis(EmphasisInline emphasis)
    {
        var tag = emphasis.DelimiterCount switch
        {
            3 => (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_') ? "<strong><em>" : "<em>",
            2 => (emphasis.DelimiterChar == '~') ? "<del>" : "<strong>",
            _ => "<em>"
        };

        var closeTag = emphasis.DelimiterCount switch
        {
            3 => (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_') ? "</em></strong>" : "</em>",
            2 => (emphasis.DelimiterChar == '~') ? "</del>" : "</strong>",
            _ => "</em>"
        };

        _html.Append(tag);
        RenderInlines(emphasis);
        _html.Append(closeTag);
    }

    private void RenderLink(LinkInline link)
    {
        if (link.IsImage)
        {
            var alt = link.FirstChild is LiteralInline altLiteral
                ? EscapeHtml(altLiteral.Content.ToString())
                : string.Empty;
            _html.Append($"<img src=\"{EscapeHtml(link.Url ?? string.Empty)}\" alt=\"{alt}\" />");
        }
        else
        {
            _html.Append($"<a href=\"{EscapeHtml(link.Url ?? string.Empty)}\" target=\"_blank\" rel=\"noopener noreferrer\">");
            RenderInlines(link);
            _html.Append("</a>");
        }
    }

    private void RenderMathInline(MathInline mathInline)
    {
        var content = mathInline.Content.ToString();
        var (line, col) = mathInline.Span.IsEmpty ? (mathInline.Line + 1, 1) : OffsetToLineCol(mathInline.Span.Start);
        // A single-line "$$...$$" carries DelimiterCount == 2 and should render as display math,
        // matching the multi-line block form. Inline "$...$" stays inline.
        var cls = mathInline.DelimiterCount >= 2 ? "math-display" : "math-inline";
        // HTML-escape the LaTeX content so that < > & in formulas do not break the HTML wrapper.
        // KaTeX reads via el.textContent which automatically decodes HTML entities.
        _html.Append($"<span class=\"{cls}\" data-pos=\"{line}-{col}\">{EscapeHtml(content)}</span>");
    }

    private (int line, int col) OffsetToLineCol(int offset)
    {
        if (offset < 0)
            return (1, 1);

        var line = FindLine(offset);
        var col = offset - GetLineOffset(line - 1) + 1;
        return (line, col);
    }

    private int FindLine(int offset)
    {
        // Binary search for the line containing the given offset
        var lo = 0;
        var hi = _lineOffsets.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_lineOffsets[mid] <= offset)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo + 1; // 1-indexed
    }

    private int GetLineOffset(int zeroIndexedLine)
    {
        if (zeroIndexedLine < 0) return 0;
        if (zeroIndexedLine >= _lineOffsets.Length) return _source.Length;
        return _lineOffsets[zeroIndexedLine];
    }

    private static int[] ComputeLineOffsets(string text)
    {
        var offsets = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                offsets.Add(i + 1);
        }
        return offsets.ToArray();
    }

    private static string EscapeChar(char c)
    {
        return c switch
        {
            '<' => "&lt;",
            '>' => "&gt;",
            '&' => "&amp;",
            '"' => "&quot;",
            '\'' => "&#39;",
            _ => c.ToString()
        };
    }

    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(EscapeChar(c));
        }
        return sb.ToString();
    }
}
