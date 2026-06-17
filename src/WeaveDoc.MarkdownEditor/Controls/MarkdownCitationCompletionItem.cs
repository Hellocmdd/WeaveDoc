namespace WeaveDoc.MarkdownEditor.Controls;

public sealed record MarkdownCitationCompletionItem(
    string CitationKey,
    string Title,
    string Authors,
    string Year);
