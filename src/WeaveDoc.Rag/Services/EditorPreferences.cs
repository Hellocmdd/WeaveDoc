using System.IO;
using System.Text.Json;

namespace WeaveDoc.Rag.Services;

/// <summary>
/// 编辑器偏好持久化。当前仅承载 Markdown 编辑器的「自动换行」开关。
/// 仿 <see cref="CloudApiSettings"/> 模式：单例 <see cref="Current"/>，读写
/// <c>.rag/editor-preferences.json</c>，损坏文件容错回退默认值。
/// </summary>
public sealed class EditorPreferences
{
    private static readonly Lazy<EditorPreferences> CurrentInstance =
        new(() => Load(GetDefaultFilePath()));

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    private EditorPreferences(string filePath, bool wordWrapEnabled)
    {
        _filePath = filePath;
        WordWrapEnabled = wordWrapEnabled;
    }

    public static EditorPreferences Current => CurrentInstance.Value;

    public bool WordWrapEnabled { get; private set; }

    /// <summary>从指定路径加载；文件缺失或损坏时返回默认值（换行关闭）。</summary>
    public static EditorPreferences Load(string filePath)
    {
        if (!File.Exists(filePath))
            return new EditorPreferences(filePath, false);

        try
        {
            var json = File.ReadAllText(filePath);
            var saved = JsonSerializer.Deserialize<PersistedFields>(json);
            return new EditorPreferences(filePath, saved?.WordWrapEnabled ?? false);
        }
        catch
        {
            // 文件损坏则忽略，使用默认值。
            return new EditorPreferences(filePath, false);
        }
    }

    /// <summary>设置换行偏好并立即写盘。</summary>
    public void SetWordWrap(bool value)
    {
        if (WordWrapEnabled == value)
            return;

        WordWrapEnabled = value;
        Save();
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var data = new PersistedFields(WordWrapEnabled);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private static string GetDefaultFilePath()
        => Path.Combine(WorkspacePaths.FindWorkspaceRoot(), ".rag", "editor-preferences.json");

    private sealed record PersistedFields(bool WordWrapEnabled);
}
