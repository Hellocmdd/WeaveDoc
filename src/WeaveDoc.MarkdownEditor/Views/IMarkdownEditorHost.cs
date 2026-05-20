namespace WeaveDoc.MarkdownEditor.Views;

public interface IMarkdownEditorHost
{
    string PreviewHtml { get; }

    void ScrollPreviewToSelection(int startLine, int startCol, int endLine, int endCol);

    void SetMonacoReady(bool ready);

    void ScrollEditorToPosition(int lineNumber, int column);

    void ScrollEditorToPositionWithRange(int lineNumber, int column, int selectionLength);

    void ClearEditorHighlight();
}
