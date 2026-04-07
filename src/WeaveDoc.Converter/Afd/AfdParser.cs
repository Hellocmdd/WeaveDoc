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
        var template = ParseJson(content);
        Validate(template);
        return template;
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

    public bool Validate(AfdTemplate template)
    {
        if (template.Meta is null)
            throw new AfdParseException("模板元信息 (meta) 不能为空");

        if (string.IsNullOrWhiteSpace(template.Meta.TemplateName))
            throw new AfdParseException("模板名称 (meta.templateName) 不能为空");

        if (template.Defaults is null)
            throw new AfdParseException("默认样式 (defaults) 不能为空");

        if (template.Styles is null || template.Styles.Count == 0)
            throw new AfdParseException("样式定义 (styles) 不能为空");

        foreach (var (key, style) in template.Styles)
        {
            if (style.FontSize is <= 0)
                throw new AfdParseException($"样式 '{key}' 的 fontSize 必须 > 0");
        }

        return true;
    }
}
