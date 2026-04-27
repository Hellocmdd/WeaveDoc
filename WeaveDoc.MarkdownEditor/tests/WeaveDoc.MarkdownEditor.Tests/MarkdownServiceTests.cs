using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class MarkdownServiceTests
    {
        private MarkdownService _markdownService;

        [SetUp]
        public void Setup()
        {
            _markdownService = new MarkdownService();
        }

        [Test]
        public void ConvertToHtml_ValidMarkdown_ReturnsHtml()
        {
            // Arrange
            var markdown = "# Heading\n\nThis is a paragraph.";
            var expectedHtml = "<h1>Heading</h1>\n<p>This is a paragraph.</p>";

            // Act
            var result = _markdownService.ConvertToHtml(markdown);

            // Assert
            Assert.AreEqual(expectedHtml, result);
        }

        [Test]
        public void ConvertToHtml_EmptyMarkdown_ReturnsEmptyString()
        {
            // Arrange
            var markdown = string.Empty;

            // Act
            var result = _markdownService.ConvertToHtml(markdown);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ConvertToHtml_NullMarkdown_ThrowsArgumentNullException()
        {
            // Arrange
            string markdown = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _markdownService.ConvertToHtml(markdown));
        }
    }
}