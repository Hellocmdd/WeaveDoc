namespace WeaveDoc.Converter.Afd;

/// <summary>
/// AFD 样式键 → OpenXML styleId 双向映射
/// </summary>
public static class AfdStyleMapper
{
    private static readonly Dictionary<string, string> _afdToOpenXml = new()
    {
        ["heading1"] = "Heading1",
        ["heading2"] = "Heading2",
        ["heading3"] = "Heading3",
        ["heading4"] = "Heading4",
        ["heading5"] = "Heading5",
        ["heading6"] = "Heading6",
        ["body"]     = "Normal",
        ["caption"]  = "Caption",
        ["footnote"] = "FootnoteText",
        ["reference"] = "Reference",
        ["abstract"] = "Abstract"
    };

    /// <summary>
    /// 将 AFD 样式键映射为 OpenXML styleId
    /// </summary>
    public static string MapToOpenXmlStyleId(string afdStyleKey)
    {
        return _afdToOpenXml.TryGetValue(afdStyleKey, out var openXmlId)
            ? openXmlId
            : throw new KeyNotFoundException(
                $"未找到 AFD 样式键 '{afdStyleKey}' 对应的 OpenXML styleId");
    }

    /// <summary>
    /// 将 OpenXML styleId 反向映射为 AFD 样式键
    /// </summary>
    public static string? MapToAfdStyleKey(string openXmlStyleId)
    {
        return _afdToOpenXml
            .FirstOrDefault(kvp => kvp.Value == openXmlStyleId)
            .Key;
    }
}
