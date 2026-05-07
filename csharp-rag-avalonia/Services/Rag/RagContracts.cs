using RagAvalonia.Models;

namespace RagAvalonia.Services;

internal sealed record QueryProfile(
    string OriginalQuestion,
    IReadOnlyList<string> FocusTerms,
    string Intent,
    bool WantsDetailedAnswer,
    bool IsFollowUpExpansion,
    string? RequestedDocumentTitle,
    bool RequestsEnglishMetadata,
    bool AvoidsEnglishMetadata,
    bool DisallowKeywordLikeLeadChunksForSummary,
    bool PreferFallbackOverUnknown,
    IReadOnlyList<string> CompareSubjects);

internal sealed record ScoredChunk(
    DocumentChunk Chunk,
    float Score,
    float SemanticScore,
    float Bm25Score,
    float KeywordScore,
    float TitleScore,
    float JsonStructureScore,
    float CoverageScore,
    float NeighborScore,
    float JsonBranchScore,
    bool HasDirectKeywordHit,
    float RequestedDocumentScore);

internal sealed record LearnedRerankResult(IReadOnlyList<ScoredChunk> RankedChunks, bool Used, string Status);

internal sealed record RetrievalResult(
    IReadOnlyList<ScoredChunk> RankedChunks,
    IReadOnlyList<DocumentChunk> ContextChunks,
    QueryProfile QueryProfile,
    int SparseCandidateCount,
    int SemanticCandidateCount,
    bool UsedSparsePrefilter,
    bool UsedLearnedReranker,
    string LearnedRerankerStatus,
    IReadOnlyList<string> TargetFilePaths,
    string PipelineMode);
