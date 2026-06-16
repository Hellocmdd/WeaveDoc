using System.Reflection;

namespace WeaveDoc.Converter.Config;

/// <summary>把嵌入的 GB/T 7714 CSL 解出到临时文件，供 Pandoc citeproc 使用。</summary>
public static class CslResourceProvider
{
    private const string ResourceName = "WeaveDoc.Converter.Config.Csl.chinese-gb7714-2015-numeric.csl";

    /// <summary>解出 CSL 到临时文件，返回路径。调用方负责删除。</summary>
    public static string ExtractToTemp()
    {
        var assembly = typeof(CslResourceProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"嵌入资源 {ResourceName} 未找到");
        using var reader = new StreamReader(stream);

        var csl = reader.ReadToEnd();
        var path = Path.Combine(Path.GetTempPath(), $"weavedoc-gbt7714-{Guid.NewGuid():N}.csl");
        File.WriteAllText(path, csl);
        return path;
    }
}
