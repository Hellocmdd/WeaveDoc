using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class MarkdownRenderServiceTests
    {
        private IMarkdownRenderService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new MarkdigMarkdownRenderService();
        }

        [Test]
        public void RenderPreviewHtml_EmptyString_ReturnsEmptyString()
        {
            var result = _service.RenderPreviewHtml(string.Empty);
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void RenderPreviewHtml_Heading_ReturnsHtmlWithDataLine()
        {
            // 渲染对每个 text run 发一个 <span>（非逐字符），data-pos 锚定到该 run 起始列；
            // 前端 JS 用 Range.toString().length 计算字符级偏移。见 MarkdigMarkdownRenderService.RenderLiteral。
            var result = _service.RenderPreviewHtml("# Hello");
            Assert.That(result, Does.Contain("<h1"));
            Assert.That(result, Does.Contain("data-line=\"1\""));
            Assert.That(result, Does.Contain("data-pos=\"1-3\">Hello</span>"));
        }

        [Test]
        public void RenderPreviewHtml_Paragraph_ReturnsHtmlWithDataLine()
        {
            // 整个文本 run 共享一个 data-pos 锚点（1-1），字符级定位交由前端 Range 计算。
            var result = _service.RenderPreviewHtml("Hello world.");
            Assert.That(result, Does.Contain("<p"));
            Assert.That(result, Does.Contain("data-line=\"1\""));
            Assert.That(result, Does.Contain("data-pos=\"1-1\">Hello world.</span>"));
        }

        [Test]
        public void RenderPreviewHtml_TextCharacters_HaveDataPosSpans()
        {
            // 单个文本 run 一个 span；data-pos 指向 run 起始列。
            var result = _service.RenderPreviewHtml("abc");
            Assert.That(result, Does.Contain("data-pos=\"1-1\">abc</span>"));
        }

        [Test]
        public void RenderPreviewHtml_Bold_ReturnsStrongTag()
        {
            var result = _service.RenderPreviewHtml("**bold**");
            Assert.That(result, Does.Contain("<strong>"));
        }

        [Test]
        public void RenderPreviewHtml_Italic_ReturnsEmTag()
        {
            var result = _service.RenderPreviewHtml("*italic*");
            Assert.That(result, Does.Contain("<em>"));
        }

        [Test]
        public void RenderPreviewHtml_CodeBlock_ReturnsPreCode()
        {
            var result = _service.RenderPreviewHtml("```\ncode\n```");
            Assert.That(result, Does.Contain("<pre>"));
            Assert.That(result, Does.Contain("<code"));
        }

        [Test]
        public void RenderPreviewHtml_Link_ReturnsAnchor()
        {
            var result = _service.RenderPreviewHtml("[link](https://example.com)");
            Assert.That(result, Does.Contain("<a href=\"https://example.com\""));
        }

        [Test]
        public void RenderPreviewHtml_InlineMath_HasMathInlineClass()
        {
            var result = _service.RenderPreviewHtml("$x^2$");
            Assert.That(result, Does.Contain("class=\"math-inline\""));
        }

        [Test]
        public void RenderPreviewHtml_DisplayMath_HasMathDisplayClass()
        {
            var result = _service.RenderPreviewHtml("$$\nx^2\n$$");
            Assert.That(result, Does.Contain("class=\"math-display\""));
        }

        [Test]
        public void RenderPreviewHtml_InlineMath_ContentNotTruncated()
        {
            // Regression: the content slice used to drop its last char, so "$x^2$" yielded "x^".
            var result = _service.RenderPreviewHtml("$x^2$");
            Assert.That(result, Does.Contain(">x^2<"));
            Assert.That(result, Does.Not.Contain(">x^<"));
        }

        [Test]
        public void RenderPreviewHtml_AdjacentInlineMath_NoSpaceRequired()
        {
            // Regression: a '$' directly preceded by '$' was rejected as an opener,
            // so "$a$$b$" rendered the second formula as literal text.
            var result = _service.RenderPreviewHtml("$a$$b$");
            Assert.That(result, Does.Contain(">a<"));
            Assert.That(result, Does.Contain(">b<"));
            Assert.That(result, Does.Not.Contain("$b$"));
        }

        [Test]
        public void RenderPreviewHtml_SingleLineDisplayMath_RendersAsDisplay()
        {
            var result = _service.RenderPreviewHtml("$$x^2$$");
            Assert.That(result, Does.Contain("class=\"math-display\""));
            Assert.That(result, Does.Contain(">x^2<"));
        }

        [Test]
        public void RenderPreviewHtml_AdjacentDisplayMath_NoSpaceRequired()
        {
            var result = _service.RenderPreviewHtml("$$a$$$$b$$");
            Assert.That(result, Does.Contain(">a<"));
            Assert.That(result, Does.Contain(">b<"));
            Assert.That(Occurrences(result, "math-display"), Is.EqualTo(2));
        }

        private static int Occurrences(string haystack, string needle)
        {
            var count = 0;
            var i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += needle.Length;
            }
            return count;
        }

        [Test]
        public void RenderPreviewHtml_TaskList_RendersCheckboxItem()
        {
            var result = _service.RenderPreviewHtml("- [ ] task\n- [x] done");
            Assert.That(result, Does.Contain("<ul>"));
            Assert.That(result, Does.Contain("<li"));
        }

        [Test]
        public void RenderPreviewHtml_PipeTable_RendersTableHtml()
        {
            var markdown = "| 名称 | 数量 |\n| --- | ---: |\n| 苹果 | 3 |\n| 香蕉 | 12 |";
            var result = _service.RenderPreviewHtml(markdown);

            Assert.That(result, Does.Contain("<table"));
            Assert.That(result, Does.Contain("<thead>"));
            Assert.That(result, Does.Contain("<tbody>"));
            // Header cells use <th>, body cells use <td>
            Assert.That(result, Does.Contain("<th"));
            Assert.That(result, Does.Contain("<td"));
            Assert.That(result, Does.Contain("苹果"));
            Assert.That(result, Does.Contain("香蕉"));
            // Right-aligned column carries the alignment style
            Assert.That(result, Does.Contain("text-align:right"));
            // Cell contents must not leak as standalone <p> paragraphs
            Assert.That(result, Does.Not.Contain("<p"));
        }

        [Test]
        public void RenderPreviewHtml_MultiLine_DataLineIncrements()
        {
            var result = _service.RenderPreviewHtml("# Line1\n\nParagraph on line 3.");
            Assert.That(result, Does.Contain("data-line=\"1\""));
            Assert.That(result, Does.Contain("data-line=\"3\""));
        }

        [Test]
        public void RenderPreviewHtml_DoesNotThrow_OnMalformedInput()
        {
            Assert.DoesNotThrow(() => _service.RenderPreviewHtml("*** broken ~~~ stuff"));
        }

        [Test]
        public void RenderPreviewHtml_HtmlSpecialChars_Escaped()
        {
            var result = _service.RenderPreviewHtml("a & b");
            // The ampersand character should be HTML-escaped in the output
            Assert.That(result, Does.Contain("&amp;"));
        }

        [Test]
        public void RenderPreviewHtml_NoAvaloniaDependency()
        {
            var result = _service.RenderPreviewHtml("# No UI");
            Assert.That(result, Is.Not.Empty);
        }

        // 契约：Markdig 识别出的原始 HTML 块原样透传给预览 WebView（不转义）。
        // 本项目（大学课程作业）威胁模型不含恶意 .md 注入，故不在渲染层做 HTML 清理。
        [Test]
        public void RenderPreviewHtml_RawHtmlBlock_IsPassedThrough()
        {
            var result = _service.RenderPreviewHtml("<div onclick='alert(1)'>text</div>");
            Assert.That(result, Does.Contain("<div onclick='alert(1)'>text</div>"));
        }

        [Test]
        public void RenderPreviewHtml_ScriptTag_PassedThroughRaw()
        {
            var result = _service.RenderPreviewHtml("<script>alert(1)</script>");
            Assert.That(result, Does.Contain("<script>alert(1)</script>"));
        }
    }
}
