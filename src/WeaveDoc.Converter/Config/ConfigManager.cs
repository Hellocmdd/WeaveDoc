using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 本地配置管理：模板库 CRUD、配置读写
/// </summary>
public class ConfigManager
{
    private readonly TemplateRepository _repository;
    private readonly AfdParser _parser;
    private readonly string _templatesDir;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public ConfigManager(string dbPath)
    {
        _repository = new TemplateRepository(dbPath);
        _parser = new AfdParser();
        _templatesDir = Path.Combine(
            Path.GetDirectoryName(dbPath) ?? ".", "templates");
    }

    public async Task<AfdTemplate?> GetTemplateAsync(string templateId)
    {
        var jsonPath = await _repository.GetJsonPathAsync(templateId);
        if (jsonPath == null || !File.Exists(jsonPath))
            return null;

        return _parser.Parse(jsonPath);
    }

    public async Task<List<AfdMeta>> ListTemplatesAsync()
    {
        return await _repository.GetAllMetasAsync();
    }

    public async Task SaveTemplateAsync(string templateId, AfdTemplate template)
    {
        Directory.CreateDirectory(_templatesDir);

        var jsonPath = Path.Combine(_templatesDir, $"{templateId}.json");
        var json = JsonSerializer.Serialize(template, _jsonOptions);
        await File.WriteAllTextAsync(jsonPath, json);

        await _repository.UpsertAsync(templateId, template.Meta, jsonPath);
    }

    public async Task DeleteTemplateAsync(string templateId)
    {
        var jsonPath = await _repository.GetJsonPathAsync(templateId);

        await _repository.DeleteAsync(templateId);

        if (jsonPath != null && File.Exists(jsonPath))
            File.Delete(jsonPath);
    }

    /// <summary>
    /// 将嵌入资源中的内置 AFD 模板种子到数据库。已存在的模板不会被覆盖。
    /// </summary>
    public async Task EnsureSeedTemplatesAsync()
    {
        const string prefix = "WeaveDoc.Converter.Config.TemplateSchemas.";
        const string suffix = ".json";

        var assembly = typeof(ConfigManager).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix) && name.EndsWith(suffix));

        foreach (var resourceName in resourceNames)
        {
            var templateId = resourceName[prefix.Length..^suffix.Length];

            var existingPath = await _repository.GetJsonPathAsync(templateId);
            if (existingPath != null)
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var template = _parser.ParseJson(json);
            await SaveTemplateAsync(templateId, template);
        }
    }
}
