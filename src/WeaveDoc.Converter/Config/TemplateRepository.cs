using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 模板仓储：SQLite 存储 AFD 模板元信息和文件路径
/// </summary>
internal class TemplateRepository
{
    public TemplateRepository(string dbPath) { }

    public Task InitializeAsync() => throw new NotImplementedException();
    public Task<AfdMeta?> GetMetaAsync(string templateId) => throw new NotImplementedException();
    public Task<List<AfdMeta>> GetAllMetasAsync() => throw new NotImplementedException();
    public Task UpsertAsync(string templateId, AfdMeta meta, string jsonPath) => throw new NotImplementedException();
    public Task DeleteAsync(string templateId) => throw new NotImplementedException();
}
