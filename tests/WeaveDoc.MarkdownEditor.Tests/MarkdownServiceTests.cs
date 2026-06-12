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
            var result = _service.RenderPreviewHtml("# Hello");
            Assert.That(result, Does.Contain("<h1"));
            Assert.That(result, Does.Contain("data-line=\"1\""));
            Assert.That(result, Does.Contain("data-pos=\"1-3\">H</span>"));
        }

        [Test]
        public void RenderPreviewHtml_Paragraph_ReturnsHtmlWithDataLine()
        {
            var result = _service.RenderPreviewHtml("Hello world.");
            Assert.That(result, Does.Contain("<p"));
            Assert.That(result, Does.Contain("data-line=\"1\""));
            Assert.That(result, Does.Contain("data-pos=\"1-1\">H</span>"));
            Assert.That(result, Does.Contain("data-pos=\"1-12\">.</span>"));
        }

        [Test]
        public void RenderPreviewHtml_TextCharacters_HaveDataPosSpans()
        {
            var result = _service.RenderPreviewHtml("abc");
            Assert.That(result, Does.Contain("data-pos=\"1-1\""));
            Assert.That(result, Does.Contain("data-pos=\"1-2\""));
            Assert.That(result, Does.Contain("data-pos=\"1-3\""));
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
        public void RenderPreviewHtml_TaskList_RendersCheckboxItem()
        {
            var result = _service.RenderPreviewHtml("- [ ] task\n- [x] done");
            Assert.That(result, Does.Contain("<ul>"));
            Assert.That(result, Does.Contain("<li"));
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

        [Test]
        public void RenderPreviewHtml_RawHtmlBlock_IsEscaped()
        {
            var result = _service.RenderPreviewHtml("<div onclick='alert(1)'>text</div>");
            Assert.That(result, Does.Not.Contain("<div onclick"));
            Assert.That(result, Does.Contain("&lt;"));
            Assert.That(result, Does.Contain("&gt;"));
        }

        [Test]
        public void RenderPreviewHtml_ScriptTag_NotExecutable()
        {
            var result = _service.RenderPreviewHtml("<script>alert(1)</script>");
            Assert.That(result, Does.Not.Contain("<script>"));
            Assert.That(result, Does.Not.Contain("</script>"));
        }
    }
}
