namespace RagAvalonia.Services;

public sealed record RagOptions(
    int ChunkSize,
    int ChunkOverlap,
    int TopK,
    int CandidatePoolSize,
    int SparseCandidatePoolSize,
    int ContextWindowRadius,
    float MinCombinedThreshold,
    float VectorWeight,
    float Bm25Weight,
    float KeywordWeight,
    float TitleWeight,
    float JsonStructureWeight,
    float CoverageWeight,
    float NeighborWeight,
    float JsonBranchWeight,
    float DirectKeywordBonus,
    int FallbackSentenceCount,
    string EmbeddingModelFile,
    int EmbeddingGpuLayerCount,
    bool RerankerEnabled,
    string RerankerBaseUrl,
    string RerankerModel,
    int RerankerTopN,
    int RerankerTimeoutSeconds,
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
            SparseCandidatePoolSize: GetInt("RAG_SPARSE_CANDIDATE_POOL_SIZE", 48, 8, 400),
            ContextWindowRadius: GetInt("RAG_CONTEXT_WINDOW_RADIUS", 1, 0, 3),
            MinCombinedThreshold: GetFloat("RAG_MIN_COMBINED_THRESHOLD", 0.18f, 0f, 1f),
            VectorWeight: GetFloat("RAG_VECTOR_WEIGHT", 0.38f, 0f, 2f),
            Bm25Weight: GetFloat("RAG_BM25_WEIGHT", 0.2f, 0f, 2f),
            KeywordWeight: GetFloat("RAG_KEYWORD_WEIGHT", 0.18f, 0f, 2f),
            TitleWeight: GetFloat("RAG_TITLE_WEIGHT", 0.12f, 0f, 2f),
            JsonStructureWeight: GetFloat("RAG_JSON_STRUCTURE_WEIGHT", 0.1f, 0f, 2f),
            CoverageWeight: GetFloat("RAG_COVERAGE_WEIGHT", 0.08f, 0f, 2f),
            NeighborWeight: GetFloat("RAG_NEIGHBOR_WEIGHT", 0.08f, 0f, 2f),
            JsonBranchWeight: GetFloat("RAG_JSON_BRANCH_WEIGHT", 0.06f, 0f, 2f),
            DirectKeywordBonus: GetFloat("RAG_DIRECT_KEYWORD_BONUS", 0.08f, 0f, 1f),
            FallbackSentenceCount: GetInt("RAG_FALLBACK_SENTENCE_COUNT", 2, 1, 8),
            EmbeddingModelFile: GetString("RAG_EMBEDDING_MODEL_FILE", "bge-m3.gguf"),
            EmbeddingGpuLayerCount: GetInt("RAG_EMBEDDING_GPU_LAYERS", 999, 0, 999),
            RerankerEnabled: GetBool("RAG_RERANKER_ENABLED", true),
            RerankerBaseUrl: GetString("RAG_RERANKER_BASE_URL", "http://127.0.0.1:8081"),
            RerankerModel: GetString("RAG_RERANKER_MODEL", "bge-reranker-v2-m3"),
            RerankerTopN: GetInt("RAG_RERANKER_TOP_N", 8, 1, 50),
            RerankerTimeoutSeconds: GetInt("RAG_RERANKER_TIMEOUT_SECONDS", 30, 1, 300),
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

    private static bool GetBool(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" or "on" => true,
            "0" or "false" or "no" or "n" or "off" => false,
            _ => defaultValue
        };
    }
}
