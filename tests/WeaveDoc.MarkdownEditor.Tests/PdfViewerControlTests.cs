using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class PdfViewerControlTests
    {
        [Test]
        public void BuildViewerUrl_LoadsViewerShellWithEmptyFileParameter()
        {
            var url = PdfViewerControl.BuildViewerUrl(64311);

            Assert.That(url, Is.EqualTo("http://localhost:64311/pdfjs-5.7.284-dist/web/viewer.html?file=#disableworker=true"));
            Assert.That(url, Does.Not.Contain("/pdf/current"));
            Assert.That(url, Does.Contain("disableworker=true"));
            Assert.That(url, Does.Not.Contain("D%3A"));
            Assert.That(url, Does.Not.Contain("\\"));
        }

        [Test]
        public void BuildPdfJsCompatibilityScript_PolyfillsUrlParseBeforeViewerRuns()
        {
            var script = PdfViewerControl.BuildPdfJsCompatibilityScript();

            Assert.That(script, Does.Contain("URL.parse"));
            Assert.That(script, Does.Contain("new URL(url, base)"));
            Assert.That(script, Does.Contain("Promise.try"));
            Assert.That(script, Does.Contain("Uint8Array.prototype.toHex"));
            Assert.That(script, Does.Contain("Map.prototype.getOrInsertComputed"));
            Assert.That(script, Does.Contain("return null;"));
        }

        [Test]
        public void BuildPdfWorkerCompatibilityPrefix_PolyfillsPromiseTryInWorkerContext()
        {
            var prefix = PdfViewerControl.BuildPdfWorkerCompatibilityPrefix();

            Assert.That(prefix, Does.Contain("Promise.try"));
            Assert.That(prefix, Does.Contain("callback"));
            Assert.That(prefix, Does.Contain("...args"));
            Assert.That(prefix, Does.Contain("Uint8Array.prototype.toHex"));
            Assert.That(prefix, Does.Contain("Map.prototype.getOrInsertComputed"));
        }

        [Test]
        public void BuildPdfOpenScript_OpensCurrentPdfAfterViewerInitialization()
        {
            var script = PdfViewerControl.BuildPdfOpenScript();

            Assert.That(script, Does.Contain("PDFViewerApplication"));
            Assert.That(script, Does.Contain("setTimeout"));
            Assert.That(script, Does.Contain("postMessage"));
            Assert.That(script, Does.Contain("documentloaded"));
            Assert.That(script, Does.Contain("pagerendered"));
            Assert.That(script, Does.Contain("textlayerrendered"));
            Assert.That(script, Does.Contain("fetch(url"));
            Assert.That(script, Does.Contain("new Uint8Array"));
            Assert.That(script, Does.Contain("/pdf/current"));
        }

        [Test]
        public void BuildPdfOpenScript_ForcesPdfJsTextLayerSelectable()
        {
            var script = PdfViewerControl.BuildPdfOpenScript();

            Assert.That(script, Does.Contain("enableTextSelection"));
            Assert.That(script, Does.Contain("weavedoc-pdf-text-selection-style"));
            Assert.That(script, Does.Contain("user-select: text !important"));
            Assert.That(script, Does.Contain("pointer-events: auto !important"));
            Assert.That(script, Does.Contain("cursorSelectTool"));
            Assert.That(script, Does.Contain(".textLayer span"));
        }
    }
}
