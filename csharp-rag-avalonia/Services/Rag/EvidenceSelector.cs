using RagAvalonia.Models;

namespace RagAvalonia.Services;

public sealed partial class LocalAiService
{
    private static bool ShouldUseActiveDocumentScope(QueryProfile queryProfile)
    {
        if (!string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle))
        {
            return false;
        }

        return queryProfile.OriginalQuestion.Contains("这篇", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("该文档", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("文档里", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("文中", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("这个系统", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("详细一点", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("具体一点", StringComparison.Ordinal);
    }

    private void RememberActiveDocumentScope(IReadOnlyList<DocumentChunk> chunks)
    {
        var paths = chunks
            .GroupBy(chunk => chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .Take(2)
            .ToArray();
        if (paths.Length > 0)
        {
            _activeDocumentFilePaths = paths;
        }
    }
}
