using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TestApp
{
    internal sealed class RagStore
    {
        private readonly string _dbPath;

        public RagStore(string dbPath)
        {
            _dbPath = dbPath;
        }

        public void EnsureSchema()
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS rag_chunks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source TEXT NOT NULL,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding BLOB NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_rag_chunks_source ON rag_chunks(source);
";
            cmd.ExecuteNonQuery();
        }

        public int CountChunks()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM rag_chunks";
            var val = cmd.ExecuteScalar();
            return Convert.ToInt32(val);
        }

        public List<RagSourceSummary> ListSourceSummaries()
        {
            var list = new List<RagSourceSummary>();

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT source, COUNT(*) AS chunk_count, MAX(created_at) AS last_updated_at
FROM rag_chunks
GROUP BY source
ORDER BY source ASC
";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new RagSourceSummary
                {
                    Source = reader.GetString(0),
                    ChunkCount = reader.GetInt32(1),
                    LastUpdatedAt = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                });
            }

            return list;
        }

        public void ClearAll()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM rag_chunks";
            cmd.ExecuteNonQuery();
        }

        public void InsertChunk(string source, int chunkIndex, string content, float[] embedding)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO rag_chunks(source, chunk_index, content, embedding)
VALUES ($source, $chunkIndex, $content, $embedding)
";
            cmd.Parameters.AddWithValue("$source", source);
            cmd.Parameters.AddWithValue("$chunkIndex", chunkIndex);
            cmd.Parameters.AddWithValue("$content", content);
            cmd.Parameters.AddWithValue("$embedding", ToBytes(embedding));
            cmd.ExecuteNonQuery();
        }

        public RagChunkRecord? GetChunkById(long id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, source, chunk_index, content, embedding, created_at
FROM rag_chunks
WHERE id = $id
LIMIT 1
";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new RagChunkRecord
            {
                Id = reader.GetInt64(0),
                Source = reader.GetString(1),
                ChunkIndex = reader.GetInt32(2),
                Content = reader.GetString(3),
                Embedding = FromBytes((byte[])reader[4]),
                CreatedAt = reader.GetString(5)
            };
        }

        public List<RagChunkRecord> ListChunksBySource(string source)
        {
            var list = new List<RagChunkRecord>();

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, source, chunk_index, content, embedding, created_at
FROM rag_chunks
WHERE source = $source
ORDER BY chunk_index ASC, id ASC
";
            cmd.Parameters.AddWithValue("$source", source);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new RagChunkRecord
                {
                    Id = reader.GetInt64(0),
                    Source = reader.GetString(1),
                    ChunkIndex = reader.GetInt32(2),
                    Content = reader.GetString(3),
                    Embedding = FromBytes((byte[])reader[4]),
                    CreatedAt = reader.GetString(5)
                });
            }

            return list;
        }

        public int UpdateChunk(long id, string content, float[] embedding)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
UPDATE rag_chunks
SET content = $content,
    embedding = $embedding
WHERE id = $id
";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$content", content);
            cmd.Parameters.AddWithValue("$embedding", ToBytes(embedding));
            return cmd.ExecuteNonQuery();
        }

        public int DeleteChunkById(long id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM rag_chunks WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteNonQuery();
        }

        public int DeleteChunksBySource(string source)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM rag_chunks WHERE source = $source";
            cmd.Parameters.AddWithValue("$source", source);
            return cmd.ExecuteNonQuery();
        }

        public List<RagHit> SearchByCosine(float[] queryEmbedding, int topK)
        {
            var q = NormalizeL2(queryEmbedding);
            var hits = new List<RagHit>();

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, source, chunk_index, content, embedding FROM rag_chunks";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var source = reader.GetString(1);
                var chunkIndex = reader.GetInt32(2);
                var content = reader.GetString(3);
                var embBytes = (byte[])reader[4];
                var emb = FromBytes(embBytes);
                var score = Cosine(q, NormalizeL2(emb));

                hits.Add(new RagHit
                {
                    Id = id,
                    Source = source,
                    ChunkIndex = chunkIndex,
                    Content = content,
                    Score = score
                });
            }

            return hits
                .OrderByDescending(x => x.Score)
                .Take(Math.Max(1, topK))
                .ToList();
        }

        public static List<string> ChunkText(string text, int chunkSize = 700, int overlap = 120)
        {
            var clean = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (clean.Length == 0) return new List<string>();
            if (clean.Length <= chunkSize) return new List<string> { clean };

            var chunks = new List<string>();
            var start = 0;
            while (start < clean.Length)
            {
                var end = Math.Min(start + chunkSize, clean.Length);
                var candidate = clean.Substring(start, end - start).Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    chunks.Add(candidate);
                }

                if (end >= clean.Length) break;
                start = Math.Max(end - overlap, start + 1);
            }

            return chunks;
        }

        public static IEnumerable<(string source, string content)> EnumerateKnowledgeFiles(string docDirectory, string rootDirectory)
        {
            if (Directory.Exists(docDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(docDirectory, "*", SearchOption.AllDirectories))
                {
                    if (!IsTextLikeFile(file)) continue;
                    var text = SafeReadText(file);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    yield return (Path.GetRelativePath(rootDirectory, file), text);
                }
            }

            var readme = Path.Combine(rootDirectory, "README.md");
            if (File.Exists(readme))
            {
                var text = SafeReadText(readme);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return (Path.GetRelativePath(rootDirectory, readme), text);
                }
            }
        }

        private static bool IsTextLikeFile(string file)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            return ext == ".txt" || ext == ".md" || ext == ".markdown" || ext == ".csv" || ext == ".json";
        }

        private static string SafeReadText(string path)
        {
            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static byte[] ToBytes(float[] values)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static float[] FromBytes(byte[] bytes)
        {
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private static float[] NormalizeL2(float[] vector)
        {
            var norm = 0.0;
            for (var i = 0; i < vector.Length; i++)
            {
                norm += vector[i] * vector[i];
            }

            norm = Math.Sqrt(norm);
            if (norm <= 1e-12) return vector;

            var output = new float[vector.Length];
            for (var i = 0; i < vector.Length; i++)
            {
                output[i] = (float)(vector[i] / norm);
            }

            return output;
        }

        private static float Cosine(float[] a, float[] b)
        {
            if (a.Length == 0 || b.Length == 0) return -1f;
            var n = Math.Min(a.Length, b.Length);
            double dot = 0;
            for (var i = 0; i < n; i++)
            {
                dot += a[i] * b[i];
            }

            return (float)dot;
        }
    }

    internal sealed class RagHit
    {
        public long Id { get; set; }
        public string Source { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public float Score { get; set; }
    }

    internal sealed class RagChunkRecord
    {
        public long Id { get; set; }
        public string Source { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string CreatedAt { get; set; } = string.Empty;
    }

    internal sealed class RagSourceSummary
    {
        public string Source { get; set; } = string.Empty;
        public int ChunkCount { get; set; }
        public string LastUpdatedAt { get; set; } = string.Empty;
    }
}
