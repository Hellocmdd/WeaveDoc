using System.Runtime.InteropServices;

namespace WeaveDoc.App.Services.Documents;

public sealed class WeaveDocUserDataPathProvider : IWeaveDocUserDataPathProvider
{
    public string GetSnapshotsRoot()
    {
        return Path.Combine(GetUserDataRoot(), "snapshots");
    }

    private static string GetUserDataRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 快照属于机器相关的缓存型数据，使用 Local 而非 Roaming，
            // 避免在域控环境中随漫游配置文件同步。
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "WeaveDoc")
                : Path.Combine(localAppData, "WeaveDoc");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                "WeaveDoc");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "WeaveDoc")
            : Path.Combine(xdgDataHome, "WeaveDoc");
    }
}
