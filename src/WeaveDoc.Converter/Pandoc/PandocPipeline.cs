namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / PDF / AST JSON
/// </summary>
public class PandocPipeline
{
    public PandocPipeline(string? pandocPath = null) { }

    public Task<string> ToDocxAsync(string inputPath, string outputPath,
        string? referenceDoc = null, string? luaFilter = null,
        CancellationToken ct = default) => throw new NotImplementedException();

    public Task<string> ToPdfAsync(string inputPath, string outputPath,
        CancellationToken ct = default) => throw new NotImplementedException();

    public Task<string> ToAstJsonAsync(string inputPath,
        CancellationToken ct = default) => throw new NotImplementedException();
}
