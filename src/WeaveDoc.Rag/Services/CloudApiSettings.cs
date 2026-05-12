using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace WeaveDoc.Rag.Services;

public sealed class CloudApiSettings : INotifyPropertyChanged
{
    private string _chatProvider = "llama_server";
    private string _cloudBaseUrl = "https://api.deepseek.com";
    private string _cloudApiKey = "";
    private string _cloudModel = "deepseek-v4-pro";
    private bool _cloudEnableThinking;
    private string _cloudReasoningEffort = "medium";

    public string ChatProvider
    {
        get => _chatProvider;
        set
        {
            var normalized = value.ToLowerInvariant() switch
            {
                "deepseek" => "cloud",
                "cloud" or "llama_server" => value.ToLowerInvariant(),
                _ => value
            };
            if (SetProperty(ref _chatProvider, normalized))
            {
                OnPropertyChanged(nameof(IsCloudProviderSelected));
                OnPropertyChanged(nameof(EffectiveProviderLabel));
            }
        }
    }

    public string CloudBaseUrl
    {
        get => _cloudBaseUrl;
        set => SetProperty(ref _cloudBaseUrl, value);
    }

    public string CloudApiKey
    {
        get => _cloudApiKey;
        set => SetProperty(ref _cloudApiKey, value);
    }

    public string CloudModel
    {
        get => _cloudModel;
        set => SetProperty(ref _cloudModel, value);
    }

    public bool CloudEnableThinking
    {
        get => _cloudEnableThinking;
        set => SetProperty(ref _cloudEnableThinking, value);
    }

    public string CloudReasoningEffort
    {
        get => _cloudReasoningEffort;
        set => SetProperty(ref _cloudReasoningEffort, value);
    }

    public bool IsCloudProviderSelected => _chatProvider == "cloud";

    public string EffectiveProviderLabel
    {
        get
        {
            if (_chatProvider != "cloud")
                return "llama-server";

            try
            {
                var host = new Uri(_cloudBaseUrl).Host;
                return string.IsNullOrWhiteSpace(host) ? "cloud API" : $"cloud API ({host})";
            }
            catch
            {
                return "cloud API";
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static CloudApiSettings Load()
    {
        var settings = new CloudApiSettings();
        settings.ChatProvider = EnvOrDefault("RAG_CHAT_PROVIDER", "CLOUD_CHAT_PROVIDER", "llama_server");
        settings.CloudBaseUrl = EnvOrDefault("CLOUD_BASE_URL", "DEEPSEEK_BASE_URL", "https://api.deepseek.com");
        settings.CloudApiKey = EnvOrDefault("CLOUD_API_KEY", "DEEPSEEK_API_KEY", "");
        settings.CloudModel = EnvOrDefault("CLOUD_MODEL", "DEEPSEEK_MODEL", "deepseek-v4-pro");
        settings.CloudEnableThinking = EnvBool("CLOUD_ENABLE_THINKING", "DEEPSEEK_ENABLE_THINKING", false);
        settings.CloudReasoningEffort = EnvOrDefault("CLOUD_REASONING_EFFORT", "DEEPSEEK_REASONING_EFFORT", "medium");

        var filePath = GetSettingsFilePath();
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var saved = JsonSerializer.Deserialize<PersistedFields>(json);
                if (saved is not null)
                {
                    if (!string.IsNullOrWhiteSpace(saved.ChatProvider))
                        settings.ChatProvider = saved.ChatProvider;
                    if (!string.IsNullOrWhiteSpace(saved.CloudBaseUrl))
                        settings.CloudBaseUrl = saved.CloudBaseUrl;
                    if (!string.IsNullOrWhiteSpace(saved.CloudApiKey))
                        settings.CloudApiKey = saved.CloudApiKey;
                    if (!string.IsNullOrWhiteSpace(saved.CloudModel))
                        settings.CloudModel = saved.CloudModel;
                    settings.CloudEnableThinking = saved.CloudEnableThinking;
                    if (!string.IsNullOrWhiteSpace(saved.CloudReasoningEffort))
                        settings.CloudReasoningEffort = saved.CloudReasoningEffort;
                }
            }
            catch
            {
                // 文件损坏则忽略，使用环境变量值
            }
        }

        return settings;
    }

    public void Save()
    {
        var filePath = GetSettingsFilePath();
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var data = new PersistedFields(
            _chatProvider,
            _cloudBaseUrl,
            _cloudApiKey,
            _cloudModel,
            _cloudEnableThinking,
            _cloudReasoningEffort);

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string GetSettingsFilePath()
    {
        var root = WorkspacePaths.FindWorkspaceRoot();
        return Path.Combine(root, ".rag", "cloud-settings.json");
    }

    private static string EnvOrDefault(string primary, string fallback, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(primary);
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        value = Environment.GetEnvironmentVariable(fallback);
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        return defaultValue;
    }

    private static bool EnvBool(string primary, string fallback, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(primary);
        if (string.IsNullOrWhiteSpace(raw))
            raw = Environment.GetEnvironmentVariable(fallback);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" or "on" => true,
            "0" or "false" or "no" or "n" or "off" => false,
            _ => defaultValue
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record PersistedFields(
        string ChatProvider,
        string CloudBaseUrl,
        string CloudApiKey,
        string CloudModel,
        bool CloudEnableThinking,
        string CloudReasoningEffort);
}
