using Microsoft.Data.Sqlite;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 模板仓储：SQLite 存储 AFD 模板元信息和文件路径
/// </summary>
internal class TemplateRepository
{
    private readonly string _dbPath;
    private bool _initialized;

    public TemplateRepository(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS templates (
                template_id  TEXT PRIMARY KEY,
                name         TEXT NOT NULL,
                version      TEXT NOT NULL,
                author       TEXT NOT NULL,
                description  TEXT NOT NULL,
                json_path    TEXT NOT NULL,
                created_at   TEXT NOT NULL,
                updated_at   TEXT NOT NULL
            )
            """;
        await cmd.ExecuteNonQueryAsync();
        _initialized = true;
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
            await InitializeAsync();
    }

    public async Task<AfdMeta?> GetMetaAsync(string templateId)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_id, name, version, author, description, json_path
            FROM templates WHERE template_id = @id
            """;
        cmd.Parameters.AddWithValue("@id", templateId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AfdMeta
            {
                TemplateName = reader.GetString(1),
                Version = reader.GetString(2),
                Author = reader.GetString(3),
                Description = reader.GetString(4)
            };
        }

        return null;
    }

    public async Task<string?> GetJsonPathAsync(string templateId)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT json_path FROM templates WHERE template_id = @id";
        cmd.Parameters.AddWithValue("@id", templateId);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<List<AfdMeta>> GetAllMetasAsync()
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_id, name, version, author, description
            FROM templates ORDER BY name
            """;

        var metas = new List<AfdMeta>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            metas.Add(new AfdMeta
            {
                TemplateId = reader.GetString(0),
                TemplateName = reader.GetString(1),
                Version = reader.GetString(2),
                Author = reader.GetString(3),
                Description = reader.GetString(4)
            });
        }

        return metas;
    }

    public async Task UpsertAsync(string templateId, AfdMeta meta, string jsonPath)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var now = DateTime.UtcNow.ToString("o");
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO templates
                (template_id, name, version, author, description, json_path, created_at, updated_at)
            VALUES
                (@id, @name, @version, @author, @description, @jsonPath,
                 COALESCE((SELECT created_at FROM templates WHERE template_id = @id), @now), @now)
            """;
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.Parameters.AddWithValue("@name", meta.TemplateName);
        cmd.Parameters.AddWithValue("@version", meta.Version);
        cmd.Parameters.AddWithValue("@author", meta.Author);
        cmd.Parameters.AddWithValue("@description", meta.Description);
        cmd.Parameters.AddWithValue("@jsonPath", jsonPath);
        cmd.Parameters.AddWithValue("@now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string templateId)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM templates WHERE template_id = @id";
        cmd.Parameters.AddWithValue("@id", templateId);

        await cmd.ExecuteNonQueryAsync();
    }
}
