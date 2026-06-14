using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Afd;

/// <summary>
/// Resolves style-level behavior that must stay identical across reference.docx
/// generation and final OpenXML post-processing.
/// </summary>
public static class AfdStyleResolver
{
    public static AfdStyleDefinition ResolveEffectiveStyle(
        string afdKey,
        AfdStyleDefinition styleDef,
        AfdNumbering? numbering)
    {
        if (styleDef.Numbering != null || numbering?.HeadingNumbering == null)
            return styleDef;
        if (!TryGetHeadingLevel(afdKey, out var level))
            return styleDef;
        if (!numbering.HeadingNumbering.Levels.Any(x => x.Level == level))
            return styleDef;

        var headingInstance = numbering.Instances.Values
            .FirstOrDefault(x => string.Equals(x.Kind, "heading", StringComparison.OrdinalIgnoreCase));
        var numId = headingInstance?.NumId ?? numbering.HeadingNumbering.StartNumId;
        return styleDef with { Numbering = new AfdParagraphNumbering { NumId = numId, Level = level } };
    }

    public static bool TryGetHeadingLevel(string afdKey, out int level)
    {
        level = -1;
        const string prefix = "heading";
        if (!afdKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!int.TryParse(afdKey[prefix.Length..], out var headingNumber))
            return false;
        if (headingNumber is < 1 or > 6)
            return false;

        level = headingNumber - 1;
        return true;
    }
}
