using System.Text.Json;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Afd;

/// <summary>
/// AFD 样式解析器：将 JSON 模板文件解析为 AfdTemplate 对象
/// </summary>
public class AfdParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public AfdTemplate Parse(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"AFD 模板文件未找到: {jsonPath}", jsonPath);

        var content = File.ReadAllText(jsonPath);
        return ParseJson(content);
    }

    public AfdTemplate ParseJson(string jsonContent)
    {
        try
        {
            var template = JsonSerializer.Deserialize<AfdTemplate>(jsonContent, _options)
                ?? throw new AfdParseException("JSON 反序列化结果为 null");
            return template;
        }
        catch (JsonException ex)
        {
            throw new AfdParseException($"JSON 解析失败: {ex.Message}", ex);
        }
    }

    public bool Validate(AfdTemplate template) => throw new NotImplementedException();
}
