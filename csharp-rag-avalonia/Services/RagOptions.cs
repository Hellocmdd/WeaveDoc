namespace RagAvalonia.Services;

public sealed record RagOptions(
    int ChunkSize,
    int ChunkOverlap,
    int TopK,
    int CandidatePoolSize,
    int ContextWindowRadius,
    float MinCombinedThreshold,
    float VectorWeight,
    float Bm25Weight,
    float KeywordWeight,
    float TitleWeight,
    float CoverageWeight,
    float NeighborWeight,
    float DirectKeywordBonus,
    int FallbackSentenceCount,
    string LlamaServerBaseUrl,
    string ChatModel,
    float Temperature,
    int MaxTokens,
    int HttpTimeoutSeconds)
{
    public static RagOptions LoadFromEnvironment()
    {
        return new RagOptions(
            ChunkSize: GetInt("RAG_CHUNK_SIZE", 520, 128, 6000),
            ChunkOverlap: GetInt("RAG_CHUNK_OVERLAP", 96, 0, 1200),
            TopK: GetInt("RAG_TOP_K", 4, 1, 20),
            CandidatePoolSize: GetInt("RAG_CANDIDATE_POOL_SIZE", 12, 4, 50),
            ContextWindowRadius: GetInt("RAG_CONTEXT_WINDOW_RADIUS", 1, 0, 3),
            MinCombinedThreshold: GetFloat("RAG_MIN_COMBINED_THRESHOLD", 0.18f, 0f, 1f),
            VectorWeight: GetFloat("RAG_VECTOR_WEIGHT", 0.38f, 0f, 2f),
            Bm25Weight: GetFloat("RAG_BM25_WEIGHT", 0.2f, 0f, 2f),
            KeywordWeight: GetFloat("RAG_KEYWORD_WEIGHT", 0.18f, 0f, 2f),
            TitleWeight: GetFloat("RAG_TITLE_WEIGHT", 0.12f, 0f, 2f),
            CoverageWeight: GetFloat("RAG_COVERAGE_WEIGHT", 0.08f, 0f, 2f),
            NeighborWeight: GetFloat("RAG_NEIGHBOR_WEIGHT", 0.08f, 0f, 2f),
            DirectKeywordBonus: GetFloat("RAG_DIRECT_KEYWORD_BONUS", 0.08f, 0f, 1f),
            FallbackSentenceCount: GetInt("RAG_FALLBACK_SENTENCE_COUNT", 2, 1, 8),
            LlamaServerBaseUrl: GetString("LLAMA_SERVER_BASE_URL", "http://127.0.0.1:8080"),
            ChatModel: GetString("LLAMA_SERVER_CHAT_MODEL", "local-model"),
            Temperature: GetFloat("LLAMA_SERVER_TEMPERATURE", 0.2f, 0f, 2f),
            MaxTokens: GetInt("LLAMA_SERVER_MAX_TOKENS", 1536, 128, 8192),
                HttpTimeoutSeconds: GetInt("LLAMA_SERVER_TIMEOUT_SECONDS", 300, 10, 1800));
    }

    private static int GetInt(string name, int defaultValue, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (!int.TryParse(raw, out var value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
    }

    private static float GetFloat(string name, float defaultValue, float min, float max)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (!float.TryParse(raw, out var value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
    }

    private static string GetString(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }
}
