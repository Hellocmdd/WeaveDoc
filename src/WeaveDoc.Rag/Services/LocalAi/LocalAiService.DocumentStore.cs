using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService
{
    private static int? TryGetIntProperty(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.String when int.TryParse(value.GetString(), out var intValue) => intValue,
            _ => null
        };
    }

    private static float? TryGetFloatProperty(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetSingle(out var floatValue) => floatValue,
            JsonValueKind.String when float.TryParse(value.GetString(), out var floatValue) => floatValue,
            _ => null
        };
    }

    private static bool IsSupportedDocumentExtension(string extension)
    {
        return SupportedDocumentExtensions.Any(candidate => extension.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldIndexCorpusFile(string docRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(docRoot, filePath).Replace('\\', '/');
        return !IgnoredCorpusRelativePrefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool FilesHaveSameContent(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        using var leftStream = File.OpenRead(leftPath);
        using var rightStream = File.OpenRead(rightPath);
        using var sha256 = SHA256.Create();
        var leftHash = sha256.ComputeHash(leftStream);
        rightStream.Position = 0;
        var rightHash = sha256.ComputeHash(rightStream);
        return leftHash.AsSpan().SequenceEqual(rightHash);
    }

    private async Task<IReadOnlyList<DocumentChunk>> LoadChunksFromDocumentAsync(string filePath, string relativePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        var raw = await ReadDocumentContentAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return SplitIntoChunks(raw, Path.GetFileName(filePath), relativePath);
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return BuildChunksFromJson(document.RootElement, Path.GetFileName(filePath), relativePath);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"JSON 文档解析失败: {Path.GetFileName(filePath)}，{exception.Message}", exception);
        }
    }

    private static async Task<string> ReadDocumentContentAsync(string filePath, CancellationToken cancellationToken)
    {
        return await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }
}
