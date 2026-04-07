using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 本地配置管理：模板库 CRUD、配置读写
/// </summary>
public class ConfigManager
{
    public ConfigManager(string dbPath) { }

    public Task<AfdTemplate?> GetTemplateAsync(string templateId) => throw new NotImplementedException();
    public Task<List<AfdMeta>> ListTemplatesAsync() => throw new NotImplementedException();
    public Task SaveTemplateAsync(string templateId, AfdTemplate template) => throw new NotImplementedException();
    public Task DeleteTemplateAsync(string templateId) => throw new NotImplementedException();
}
