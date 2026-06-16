using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 文献库仓储：SQLite 存储 BibTeX 条目。字段整存 JSON，常用项拍平。
/// 与 TemplateRepository 同库（weavedoc.db），citation_key 主键 upsert。
/// </summary>
public class LiteratureRepository : ILiteratureRepository
{
    private readonly string _dbPath;
    private bool _initialized;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public LiteratureRepository(string dbPath)
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
            CREATE TABLE IF NOT EXISTS literature_entries (
                citation_key   TEXT PRIMARY KEY,
                entry_type     TEXT NOT NULL,
                title          TEXT,
                authors        TEXT,
                year           TEXT,
                fields_json    TEXT NOT NULL,
                source_file    TEXT,
                imported_at    TEXT NOT NULL
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

    /// <summary>导入条目（重复 key 时 upsert 刷新）。</summary>
    public async Task ImportAsync(IEnumerable<BibtexEntry> entries, string sourceFile, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var now = DateTime.UtcNow.ToString("o");

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.CitationKey))
                continue;

            var fieldsJson = JsonSerializer.Serialize(entry.Fields, _jsonOptions);
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO literature_entries
                    (citation_key, entry_type, title, authors, year, fields_json, source_file, imported_at)
                VALUES
                    (@key, @type, @title, @authors, @year, @fieldsJson, @source,
                     COALESCE((SELECT imported_at FROM literature_entries WHERE citation_key = @key), @now))
                """;
            cmd.Parameters.AddWithValue("@key", entry.CitationKey);
            cmd.Parameters.AddWithValue("@type", entry.EntryType);
            cmd.Parameters.AddWithValue("@title", entry.Title);
            cmd.Parameters.AddWithValue("@authors", entry.Authors);
            cmd.Parameters.AddWithValue("@year", entry.Year);
            cmd.Parameters.AddWithValue("@fieldsJson", fieldsJson);
            cmd.Parameters.AddWithValue("@source", sourceFile);
            cmd.Parameters.AddWithValue("@now", now);

            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<List<LiteratureEntryRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        return await QueryAsync("SELECT citation_key, entry_type, title, authors, year, fields_json, source_file, imported_at FROM literature_entries ORDER BY citation_key", null, ct);
    }

    public async Task<LiteratureEntryRecord?> GetByKeyAsync(string citationKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT citation_key, entry_type, title, authors, year, fields_json, source_file, imported_at FROM literature_entries WHERE citation_key = @key";
        cmd.Parameters.AddWithValue("@key", citationKey);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return MapReader(reader);
        return null;
    }

    public async Task<List<LiteratureEntryRecord>> FindAsync(string query, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var like = $"%{query}%";
        return await QueryAsync(
            "SELECT citation_key, entry_type, title, authors, year, fields_json, source_file, imported_at FROM literature_entries WHERE title LIKE @q OR authors LIKE @q OR citation_key LIKE @q ORDER BY citation_key",
            cmd => cmd.Parameters.AddWithValue("@q", like), ct);
    }

    public async Task UpdateFieldAsync(string citationKey, string fieldName, string value, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var existing = await GetByKeyAsync(citationKey, ct);
        if (existing == null) return;

        var fields = new Dictionary<string, string>(existing.Fields, StringComparer.OrdinalIgnoreCase);
        fields[fieldName] = value;
        var fieldsJson = JsonSerializer.Serialize(fields, _jsonOptions);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE literature_entries SET fields_json = @f WHERE citation_key = @key";
        cmd.Parameters.AddWithValue("@f", fieldsJson);
        cmd.Parameters.AddWithValue("@key", citationKey);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string citationKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM literature_entries WHERE citation_key = @key";
        cmd.Parameters.AddWithValue("@key", citationKey);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>把命中的条目按给定 key 顺序序列化为合法 .bib 文件，交给 Pandoc。</summary>
    public async Task WriteBibliographyFileAsync(string outputPath, IEnumerable<string> citationKeys, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var orderedKeys = citationKeys.ToList();
        if (orderedKeys.Count == 0)
        {
            await File.WriteAllTextAsync(outputPath, "", ct);
            return;
        }

        // 一次性查全部 key，再按入参顺序输出
        var byKey = new Dictionary<string, LiteratureEntryRecord?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in orderedKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            byKey[key] = await GetByKeyAsync(key, ct);

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new System.Text.StringBuilder();
        foreach (var key in orderedKeys)
        {
            if (!emitted.Add(key))
                continue; // 同一 key 多次出现只输出一次（Bibliography 去重）
            var entry = byKey.TryGetValue(key, out var v) ? v : null;
            if (entry == null) continue;

            sb.Append('@').Append(entry.EntryType).Append('{').Append(entry.CitationKey).AppendLine(",");
            foreach (var field in entry.Fields)
            {
                sb.Append("    ").Append(field.Key).Append(" = {").Append(field.Value).AppendLine("},");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    private async Task<List<LiteratureEntryRecord>> QueryAsync(string sql, Action<SqliteCommand>? bind, CancellationToken ct)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind?.Invoke(cmd);

        var list = new List<LiteratureEntryRecord>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(MapReader(reader));
        return list;
    }

    private static LiteratureEntryRecord MapReader(SqliteDataReader reader)
    {
        var fieldsJson = reader.GetString(5);
        var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(fieldsJson) ?? new();
        return new LiteratureEntryRecord
        {
            CitationKey = reader.GetString(0),
            EntryType = reader.GetString(1),
            Title = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Authors = reader.IsDBNull(3) ? "" : reader.GetString(3),
            Year = reader.IsDBNull(4) ? "" : reader.GetString(4),
            Fields = fields,
            SourceFile = reader.IsDBNull(6) ? "" : reader.GetString(6),
            ImportedAt = reader.GetString(7)
        };
    }
}
