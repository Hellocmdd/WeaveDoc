using System;
using System.IO;
using WeaveDoc.MarkdownEditor.Services;

class Program {
    static void Main() {
        try {
            var text = File.ReadAllText("../tests/test_doc/markdown/test_latex.md");
            var renderer = new MarkdigMarkdownRenderService();
            var html = renderer.RenderPreviewHtml(text);
            Console.WriteLine("Rendered HTML length: " + (html == null ? "NULL" : html.Length.ToString()));
            if (html != null) {
                Console.WriteLine("Snippet: " + html.Substring(0, Math.Min(200, html.Length)));
            }
        } catch (Exception ex) {
            Console.WriteLine("Exception: " + ex);
        }
    }
}
