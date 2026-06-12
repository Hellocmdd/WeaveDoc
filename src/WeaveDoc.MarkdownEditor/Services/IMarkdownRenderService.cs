namespace WeaveDoc.MarkdownEditor.Services;

public interface IMarkdownRenderService
{
    string RenderPreviewHtml(string markdown);
}
