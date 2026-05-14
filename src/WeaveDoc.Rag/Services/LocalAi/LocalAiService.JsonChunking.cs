using System.Text;
using System.Text.Json;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService
{
    private IReadOnlyList<DocumentChunk> BuildChunksFromJson(JsonElement root, string sourceName, string relativePath)
    {
        var documentTitle = ExtractJsonTitle(root, Path.GetFileNameWithoutExtension(sourceName));
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        AppendJsonChunks(root, sourceName, relativePath, documentTitle, documentTitle, documentTitle, chunks, ref chunkIndex);

        if (chunks.Count > 0)
        {
            return chunks;
        }

        var fallbackText = ConvertJsonToText(root, documentTitle);
        return SplitIntoChunks(fallbackText, sourceName, relativePath);
    }

    private static string ConvertJsonToText(JsonElement root, string defaultTitle)
    {
        var builder = new StringBuilder();
        var title = ExtractJsonTitle(root, defaultTitle);
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        AppendJsonElement(builder, root, title, 0);
        return builder.ToString().Trim();
    }

    private static string ExtractJsonTitle(JsonElement root, string defaultTitle)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return defaultTitle;
        }

        foreach (var name in JsonTitleFieldCandidates)
        {
            if (TryGetPropertyByNormalizedName(root, name, out var value))
            {
                var text = JsonScalarToString(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return defaultTitle;
    }

    private void AppendJsonChunks(
        JsonElement element,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        List<DocumentChunk> chunks,
        ref int chunkIndex)
    {
        if (IsIgnoredJsonStructure(structurePath))
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendJsonObjectChunks(element, sourceName, relativePath, documentTitle, sectionTitle, structurePath, chunks, ref chunkIndex);
                break;
            case JsonValueKind.Array:
                AppendJsonArrayChunks(element, sourceName, relativePath, documentTitle, sectionTitle, structurePath, chunks, ref chunkIndex);
                break;
            default:
                AddStructuredChunk(chunks, sourceName, relativePath, documentTitle, sectionTitle, structurePath, "body", ref chunkIndex, JsonScalarToString(element));
                break;
        }
    }

    private void AppendJsonObjectChunks(
        JsonElement element,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        List<DocumentChunk> chunks,
        ref int chunkIndex)
    {
        var scalarLines = new List<string>();

        foreach (var property in element.EnumerateObject())
        {
            var childPath = CombineStructurePath(structurePath, property.Name);
            var childSection = GetLeafStructureSegment(childPath);

            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AppendJsonChunks(property.Value, sourceName, relativePath, documentTitle, childSection, childPath, chunks, ref chunkIndex);
                continue;
            }

            if (IsJsonFieldName(property.Name, JsonIgnoredFieldNames))
            {
                continue;
            }

            if (property.Name.Equals("number", StringComparison.OrdinalIgnoreCase) && IsIgnoredJsonStructure(childPath))
            {
                continue;
            }

            var value = JsonScalarToString(property.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsJsonFieldName(property.Name, JsonPriorityFieldNames) || value.Length > _options.ChunkSize / 2)
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AddStructuredChunk(
                    chunks,
                    sourceName,
                    relativePath,
                    documentTitle,
                    childSection,
                    childPath,
                    ResolveJsonContentKind(property.Name, childPath),
                    ref chunkIndex,
                    $"{property.Name}: {value}");
                continue;
            }

            var scalarLine = $"{property.Name}: {value}";
            if (ShouldFlushStructuredValues(scalarLines, scalarLine))
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
            }

            scalarLines.Add(scalarLine);
        }

        FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
    }

    private void AppendJsonArrayChunks(
        JsonElement element,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        List<DocumentChunk> chunks,
        ref int chunkIndex)
    {
        var scalarValues = new List<string>();
        var itemIndex = 1;

        foreach (var item in element.EnumerateArray())
        {
            var itemPath = CombineStructurePath(structurePath, ResolveJsonArrayItemPathSegment(item, itemIndex));
            var itemSection = ExtractJsonArrayItemSectionTitle(item, itemIndex);

            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                FlushJsonArrayScalarChunk(chunks, scalarValues, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AppendJsonChunks(item, sourceName, relativePath, documentTitle, itemSection, itemPath, chunks, ref chunkIndex);
            }
            else
            {
                var value = JsonScalarToString(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (ShouldFlushStructuredValues(scalarValues, value, bulletPrefixLength: 2))
                    {
                        FlushJsonArrayScalarChunk(chunks, scalarValues, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                    }

                    scalarValues.Add(value);
                }
            }

            itemIndex++;
        }

        FlushJsonArrayScalarChunk(chunks, scalarValues, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
    }

    private void FlushJsonScalarChunk(
        List<DocumentChunk> chunks,
        List<string> scalarLines,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        ref int chunkIndex)
    {
        if (scalarLines.Count == 0)
        {
            return;
        }

        AddStructuredChunk(
            chunks,
            sourceName,
            relativePath,
            documentTitle,
            sectionTitle,
            structurePath,
            ResolveJsonContentKind(GetLeafStructureSegment(structurePath), structurePath),
            ref chunkIndex,
            string.Join('\n', scalarLines));

        scalarLines.Clear();
    }

    private void FlushJsonArrayScalarChunk(
        List<DocumentChunk> chunks,
        List<string> scalarValues,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        ref int chunkIndex)
    {
        if (scalarValues.Count == 0)
        {
            return;
        }

        AddStructuredChunk(
            chunks,
            sourceName,
            relativePath,
            documentTitle,
            sectionTitle,
            structurePath,
            ResolveJsonContentKind(GetLeafStructureSegment(structurePath), structurePath),
            ref chunkIndex,
            string.Join('\n', scalarValues.Select(value => $"- {value}")));

        scalarValues.Clear();
    }

    private void AddStructuredChunk(
        List<DocumentChunk> chunks,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        string contentKind,
        ref int chunkIndex,
        string text)
    {
        var normalizedText = text.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        if (normalizedText.Length > _options.ChunkSize)
        {
            foreach (var oversizedPart in SplitLargeParagraph(normalizedText))
            {
                var normalizedKind = NormalizeChunkContentKind(contentKind, sectionTitle, structurePath, oversizedPart);
                chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, oversizedPart, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, normalizedKind));
            }

            return;
        }

        var kind = NormalizeChunkContentKind(contentKind, sectionTitle, structurePath, normalizedText);
        chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, normalizedText, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, kind));
    }

    private static string CombineStructurePath(string parentPath, string childSegment)
    {
        return string.IsNullOrWhiteSpace(parentPath) ? childSegment : $"{parentPath} > {childSegment}";
    }

    private static string BuildJsonArrayItemPathSegment(int itemIndex)
    {
        return $"第{itemIndex}项";
    }

    private bool ShouldFlushStructuredValues(IReadOnlyList<string> values, string nextValue, int bulletPrefixLength = 0)
    {
        if (values.Count == 0)
        {
            return false;
        }

        var currentLength = values.Sum(value => value.Length + bulletPrefixLength) + (values.Count - 1);
        var safeLimit = Math.Max(120, _options.ChunkSize - 24);
        return currentLength + nextValue.Length + bulletPrefixLength + 1 > safeLimit;
    }

    private static string ExtractJsonArrayItemSectionTitle(JsonElement item, int itemIndex)
    {
        var baseLabel = BuildJsonArrayItemPathSegment(itemIndex);
        if (item.ValueKind != JsonValueKind.Object)
        {
            return baseLabel;
        }

        if (TryExtractJsonArrayItemSemanticLabel(item, out var semanticLabel))
        {
            return semanticLabel;
        }

        foreach (var property in item.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                continue;
            }

            if (IsJsonFieldName(property.Name, JsonIgnoredFieldNames))
            {
                continue;
            }

            var value = JsonScalarToString(property.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return $"{baseLabel} {TruncateJsonSectionLabel($"{property.Name}: {value}")}";
        }

        return baseLabel;
    }

    private static string ResolveJsonArrayItemPathSegment(JsonElement item, int itemIndex)
    {
        return TryExtractJsonArrayItemSemanticLabel(item, out var semanticLabel)
            ? semanticLabel
            : BuildJsonArrayItemPathSegment(itemIndex);
    }

    private static bool TryExtractJsonArrayItemSemanticLabel(JsonElement item, out string label)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            label = string.Empty;
            return false;
        }

        string? numberLabel = null;
        if (TryGetPropertyByNormalizedName(item, "number", out var numberValue))
        {
            var numberText = JsonScalarToString(numberValue);
            if (!string.IsNullOrWhiteSpace(numberText))
            {
                numberLabel = TruncateJsonSectionLabel(numberText);
            }
        }

        foreach (var fieldName in JsonArrayItemTitleFieldCandidates)
        {
            if (!TryGetPropertyByNormalizedName(item, fieldName, out var value))
            {
                continue;
            }

            var titleLabel = JsonScalarToString(value);
            if (string.IsNullOrWhiteSpace(titleLabel))
            {
                continue;
            }

            label = string.IsNullOrWhiteSpace(numberLabel)
                ? TruncateJsonSectionLabel(titleLabel)
                : TruncateJsonSectionLabel($"{numberLabel} {titleLabel}");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(numberLabel))
        {
            label = numberLabel;
            return true;
        }

        label = string.Empty;
        return false;
    }

    private static string TruncateJsonSectionLabel(string text)
    {
        var normalized = text.Trim().Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ');
        return normalized.Length <= 48 ? normalized : normalized[..48].TrimEnd() + "...";
    }

    private static string GetLeafStructureSegment(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return "正文";
        }

        var segments = structurePath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? structurePath : segments[^1];
    }

    private static bool IsIgnoredJsonStructure(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return false;
        }

        return structurePath.Contains("参考文献", StringComparison.Ordinal)
            || structurePath.Contains("references", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMetadataStructurePath(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return false;
        }

        return structurePath.Contains("author", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("authors", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("affiliation", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("funding", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("acknowledgements", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("article_info", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReferenceStructurePath(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return false;
        }

        return structurePath.Contains("references", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("reference", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("citations", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("citation", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("参考文献", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveJsonContentKind(string fieldNameOrPathLeaf, string structurePath)
    {
        if (IsReferenceStructurePath(structurePath))
        {
            return "reference";
        }

        if (IsMetadataStructurePath(structurePath))
        {
            return "metadata";
        }

        return NormalizeChunkContentKind(GetJsonContentKind(fieldNameOrPathLeaf), fieldNameOrPathLeaf, structurePath, string.Empty);
    }

    private static string GetJsonContentKind(string fieldNameOrPathLeaf)
    {
        if (string.IsNullOrWhiteSpace(fieldNameOrPathLeaf))
        {
            return "body";
        }

        var normalized = fieldNameOrPathLeaf
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (IsJsonFieldName(normalized, JsonTitleFieldCandidates))
        {
            return "title";
        }

        if (normalized.Contains("abstract", StringComparison.OrdinalIgnoreCase) || normalized.Contains("摘要", StringComparison.OrdinalIgnoreCase))
        {
            return "abstract";
        }

        if (IsJsonFieldName(normalized, JsonSummaryFieldCandidates))
        {
            return "summary";
        }

        if (IsJsonFieldName(normalized, JsonKeywordFieldCandidates))
        {
            return "keyword";
        }

        if (normalized.Equals("content", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("workflow", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }

        if (IsJsonFieldName(normalized, JsonMetadataFieldNames))
        {
            return "metadata";
        }

        return "body";
    }

    private static string ResolveMarkdownContentKind(string sectionTitle, string structurePath, string text)
    {
        return NormalizeChunkContentKind("body", sectionTitle, structurePath, text);
    }

    private static string NormalizeChunkContentKind(string contentKind, string sectionTitle, string structurePath, string text)
    {
        var normalized = string.IsNullOrWhiteSpace(contentKind) ? "body" : contentKind.Trim().ToLowerInvariant();
        var context = $"{sectionTitle} {structurePath}".ToLowerInvariant();
        var trimmedText = text.Trim();
        var lowerText = trimmedText.ToLowerInvariant();

        if (IsReferenceStructurePath(context)
            || trimmedText.StartsWith("参考文献", StringComparison.Ordinal)
            || trimmedText.StartsWith("# 参考文献", StringComparison.Ordinal)
            || IsReferenceLikeSentence(trimmedText))
        {
            return "reference";
        }

        if (HtmlTagRegex.IsMatch(trimmedText) || MarkdownImageRegex.IsMatch(trimmedText))
        {
            return "noise";
        }

        if (context.Contains("abstract", StringComparison.OrdinalIgnoreCase)
            || context.Contains("摘要", StringComparison.OrdinalIgnoreCase)
            || lowerText.StartsWith("abstract:", StringComparison.Ordinal))
        {
            return "abstract";
        }

        if (IsMetadataStructurePath(context)
            || MetadataLineRegex.IsMatch(trimmedText)
            || trimmedText.Contains("中图分类号", StringComparison.Ordinal)
            || trimmedText.Contains("文献标识码", StringComparison.Ordinal)
            || lowerText.Contains("doi", StringComparison.Ordinal))
        {
            return "metadata";
        }

        if (context.Contains("introduction", StringComparison.OrdinalIgnoreCase)
            || context.Contains("引言", StringComparison.OrdinalIgnoreCase)
            || context.Contains("绪论", StringComparison.OrdinalIgnoreCase))
        {
            return "intro";
        }

        if (context.Contains("conclusion", StringComparison.OrdinalIgnoreCase)
            || context.Contains("结论", StringComparison.OrdinalIgnoreCase)
            || context.Contains("总结", StringComparison.OrdinalIgnoreCase)
            || context.Contains("展望", StringComparison.OrdinalIgnoreCase))
        {
            return "conclusion";
        }

        return normalized switch
        {
            "title" => "title",
            "summary" => "summary",
            "abstract" => "abstract",
            "intro" or "introduction" => "intro",
            "conclusion" => "conclusion",
            "reference" or "references" => "reference",
            "metadata" => "metadata",
            "noise" => "noise",
            "keyword" or "keywords" => "keyword",
            _ => "body"
        };
    }

    private static void AppendJsonElement(StringBuilder builder, JsonElement element, string label, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendJsonObject(builder, element, depth);
                break;
            case JsonValueKind.Array:
                AppendJsonArray(builder, element, label, depth);
                break;
            default:
                var text = JsonScalarToString(element);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text);
                    builder.AppendLine();
                }
                break;
        }
    }

    private static void AppendJsonObject(StringBuilder builder, JsonElement element, int depth)
    {
        var scalarLines = new List<string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                FlushJsonScalarLines(builder, scalarLines);
                AppendJsonHeading(builder, property.Name, depth + 1);
                AppendJsonElement(builder, property.Value, property.Name, depth + 1);
                continue;
            }

            var value = JsonScalarToString(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                scalarLines.Add($"{property.Name}: {value}");
            }
        }

        FlushJsonScalarLines(builder, scalarLines);
    }

    private static void AppendJsonArray(StringBuilder builder, JsonElement element, string label, int depth)
    {
        var index = 1;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                AppendJsonHeading(builder, $"{label}[{index}]", depth + 1);
                AppendJsonElement(builder, item, $"{label}[{index}]", depth + 1);
            }
            else
            {
                var value = JsonScalarToString(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    builder.AppendLine($"- {value}");
                }
            }

            index++;
        }

        if (index > 1)
        {
            builder.AppendLine();
        }
    }

    private static void AppendJsonHeading(StringBuilder builder, string label, int depth)
    {
        var headingDepth = Math.Min(6, depth + 1);
        builder.AppendLine($"{new string('#', headingDepth)} {label}");
        builder.AppendLine();
    }

    private static void FlushJsonScalarLines(StringBuilder builder, List<string> scalarLines)
    {
        if (scalarLines.Count == 0)
        {
            return;
        }

        foreach (var line in scalarLines)
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        scalarLines.Clear();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetPropertyByNormalizedName(JsonElement element, string propertyName, out JsonElement value)
    {
        var normalizedTarget = NormalizeJsonFieldName(propertyName);
        foreach (var property in element.EnumerateObject())
        {
            if (NormalizeJsonFieldName(property.Name).Equals(normalizedTarget, StringComparison.Ordinal))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsJsonFieldName(string value, IEnumerable<string> candidates)
    {
        var normalized = NormalizeJsonFieldName(value);
        return candidates.Any(candidate => NormalizeJsonFieldName(candidate).Equals(normalized, StringComparison.Ordinal));
    }

    private static string NormalizeJsonFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string JsonScalarToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }
}
