namespace RagAvalonia.Services;

internal static class QueryUnderstandingService
{
    public static QueryProfile BuildProfile(string question)
    {
        var requestedDocumentTitle = LocalAiService.ExtractRequestedDocumentTitle(question);
        var focusTerms = LocalAiService.BuildRetrievalQueryTokens(question, requestedDocumentTitle)
            .Where(LocalAiService.IsMeaningfulFocusTerm)
            .OrderByDescending(token => token.Length)
            .Take(8)
            .ToArray();

        var intent = LocalAiService.DetectIntent(question);
        var wantsDetailedAnswer = LocalAiService.WantsDetailedAnswer(question);
        return new QueryProfile(
            question,
            focusTerms,
            intent,
            wantsDetailedAnswer,
            requestedDocumentTitle,
            LocalAiService.AvoidsEnglishMetadata(question));
    }
}
