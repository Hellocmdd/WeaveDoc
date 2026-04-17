namespace RagAvalonia.Models;

public sealed record DocumentChunk(
    string Source,
    string FilePath,
    int Index,
    string Text,
    float[] Embedding,
    string DocumentTitle = "",
    string SectionTitle = "");
