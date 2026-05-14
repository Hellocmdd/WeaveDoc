namespace WeaveDoc.Rag.Services;

public static class QueryUnderstandingService
{
    public static QueryProfile BuildProfile(string question)
    {
        return LocalAiQuestionAnalyzer.BuildProfile(question);
    }
}
