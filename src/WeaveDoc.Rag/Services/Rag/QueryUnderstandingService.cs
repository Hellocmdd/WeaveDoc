namespace WeaveDoc.Rag.Services;

public static class QueryUnderstandingService
{
    public static QueryProfile BuildProfile(string question)
    {
        var requestedDocumentTitle = LocalAiService.ExtractRequestedDocumentTitle(question);
        var focusTerms = LocalAiService.BuildRetrievalQueryTokens(question, requestedDocumentTitle)
            .Where(LocalAiService.IsMeaningfulFocusTerm)
            .OrderBy(token => token.Length)
            .ThenByDescending(token => token.Any(char.IsAsciiLetterOrDigit))
            .Take(8)
            .ToArray();

        var intent = LocalAiService.DetectIntent(question);
        var wantsDetailedAnswer = LocalAiService.WantsDetailedAnswer(question);
        return new QueryProfile(
            question,
            focusTerms,
            intent,
            wantsDetailedAnswer,
            LocalAiService.IsFollowUpExpansionRequest(question),
            requestedDocumentTitle,
            LocalAiService.RequestsEnglishMetadata(question),
            LocalAiService.AvoidsEnglishMetadata(question),
            LocalAiService.DisallowKeywordLikeLeadChunksForSummary(question),
            LocalAiService.PreferFallbackOverUnknown(question),
            LocalAiService.ExtractCompareSubjects(question));
    }
}
