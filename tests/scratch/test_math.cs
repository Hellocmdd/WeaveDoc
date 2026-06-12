using System;
using Markdig;
using WeaveDoc.MarkdownEditor.Services;

class Program {
    static void Main() {
        var html = new MarkdigMarkdownRenderService().RenderPreviewHtml("abc$x=1$def");
        Console.WriteLine(html);
    }
}
