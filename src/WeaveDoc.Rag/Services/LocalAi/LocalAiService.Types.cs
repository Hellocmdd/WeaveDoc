using System.Text.Json;
using System.Text.Json.Serialization;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService
{
    private sealed record IndexedChunk(DocumentChunk Chunk, float[] Embedding, IReadOnlyDictionary<string, int> TokenFrequency, int TokenCount);

    private sealed record SparseRetrievalComponent(
        IndexedChunk Indexed,
        float Bm25Raw,
        float KeywordScore,
        float TitleScore,
        float JsonStructureScore,
        float NoisePenalty,
        bool HasDirectKeywordHit,
        float Bm25Score = 0f,
        float SparseScore = 0f);

    public sealed record AddDocumentResult(string StoredPath, bool ImportedNewFile, string StatusMessage);

    public sealed record RetrievalChunkSnapshot(
        string Citation,
        string FilePath,
        string SectionTitle,
        string StructurePath,
        string ContentKind,
        string Text);

    private sealed record RerankRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("documents")] IReadOnlyList<string> Documents,
        [property: JsonPropertyName("top_n")] int TopN);

    private sealed record RerankScore(int Index, float Score);

    private sealed class EmbeddingCache
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly Dictionary<string, float[]> _entries;

        private EmbeddingCache(Dictionary<string, float[]> entries)
        {
            _entries = entries;
        }

        public static async Task<EmbeddingCache> LoadAsync(string cachePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(cachePath))
            {
                return new EmbeddingCache(new Dictionary<string, float[]>(StringComparer.Ordinal));
            }

            try
            {
                await using var stream = File.OpenRead(cachePath);
                var payload = await JsonSerializer.DeserializeAsync<EmbeddingCachePayload>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                if (payload?.Entries is null)
                {
                    return new EmbeddingCache(new Dictionary<string, float[]>(StringComparer.Ordinal));
                }

                return new EmbeddingCache(new Dictionary<string, float[]>(payload.Entries, StringComparer.Ordinal));
            }
            catch (JsonException)
            {
                return new EmbeddingCache(new Dictionary<string, float[]>(StringComparer.Ordinal));
            }
        }

        public bool TryGet(string key, out float[] embedding)
        {
            if (_entries.TryGetValue(key, out var cached))
            {
                embedding = cached;
                return true;
            }

            embedding = Array.Empty<float>();
            return false;
        }

        public void Set(string key, float[] embedding)
        {
            _entries[key] = embedding;
        }

        public bool PruneExcept(IReadOnlySet<string> activeKeys)
        {
            var removed = false;
            foreach (var key in _entries.Keys.Except(activeKeys).ToArray())
            {
                _entries.Remove(key);
                removed = true;
            }

            return removed;
        }

        public async Task SaveAsync(string cachePath, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(cachePath);
            var payload = new EmbeddingCachePayload(_entries);
            await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record EmbeddingCachePayload(Dictionary<string, float[]> Entries);
}
