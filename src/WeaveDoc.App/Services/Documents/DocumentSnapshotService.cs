using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;

namespace WeaveDoc.App.Services.Documents;

public sealed class DocumentSnapshotService : IDocumentSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IWeaveDocUserDataPathProvider _pathProvider;

    public DocumentSnapshotService()
        : this(new WeaveDocUserDataPathProvider())
    {
    }

    public DocumentSnapshotService(IWeaveDocUserDataPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
    }

    public async Task<DocumentSnapshotEntry?> CreateSnapshotAsync(
        string filePath,
        SnapshotTrigger trigger,
        string? pendingContent = null,
        bool force = false,
        SnapshotRetentionPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(filePath);
        var currentContent = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (pendingContent is not null && string.Equals(currentContent, pendingContent, StringComparison.Ordinal))
        {
            return null;
        }

        var contentHash = ComputeHash(currentContent);
        var metadata = await ReadMetadataAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var versionsDirectory = Path.Combine(GetDocumentDirectory(fullPath), "versions");
        var latest = metadata.Versions
            .OrderByDescending(version => version.CreatedAt)
            .FirstOrDefault();

        if (metadata.Versions.Any(version =>
                string.Equals(version.ContentHash, contentHash, StringComparison.Ordinal)
                && File.Exists(Path.Combine(versionsDirectory, version.FileName))))
        {
            return null;
        }

        var retention = policy ?? SnapshotRetentionPolicy.Default;
        if (!force && trigger == SnapshotTrigger.AutoSave && latest is not null)
        {
            var elapsed = DateTimeOffset.UtcNow - latest.CreatedAt;
            var changedEnough = pendingContent is not null
                && Math.Abs(pendingContent.Length - latest.ContentLength) >= retention.AutoSnapshotContentChangeThreshold;
            if (elapsed < TimeSpan.FromMinutes(retention.AutoSnapshotMinIntervalMinutes) && !changedEnough)
            {
                return null;
            }
        }

        Directory.CreateDirectory(versionsDirectory);

        var snapshotId = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fffffff}-{contentHash[..8]}";
        var snapshotFileName = $"{snapshotId}{Path.GetExtension(fullPath)}";
        var snapshotPath = Path.Combine(versionsDirectory, snapshotFileName);

        await File.WriteAllTextAsync(snapshotPath, currentContent, cancellationToken).ConfigureAwait(false);
        var snapshotInfo = new FileInfo(snapshotPath);
        var originalInfo = new FileInfo(fullPath);
        var entry = new DocumentSnapshotEntry(
            snapshotId,
            snapshotFileName,
            fullPath,
            DateTimeOffset.UtcNow,
            originalInfo.Length,
            snapshotInfo.Length,
            currentContent.Length,
            contentHash,
            trigger,
            null);

        var versions = metadata.Versions
            .Where(version => version.SnapshotId != snapshotId)
            .Append(entry)
            .OrderByDescending(version => version.CreatedAt)
            .ToArray();

        metadata = metadata with
        {
            OriginalPath = fullPath,
            DisplayName = Path.GetFileName(fullPath),
            UpdatedAt = DateTimeOffset.UtcNow,
            Versions = versions
        };

        await WriteMetadataAsync(fullPath, metadata, cancellationToken).ConfigureAwait(false);
        await CleanupSnapshotsAsync(fullPath, retention, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<IReadOnlyList<DocumentSnapshotEntry>> ListSnapshotsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var metadata = await ReadMetadataAsync(filePath, cancellationToken).ConfigureAwait(false);
        var versionsDirectory = Path.Combine(GetDocumentDirectory(filePath), "versions");
        return metadata.Versions
            .Where(entry => File.Exists(Path.Combine(versionsDirectory, entry.FileName)))
            .OrderByDescending(entry => entry.CreatedAt)
            .ToArray();
    }

    public async Task<string> ReadSnapshotContentAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var entry = await FindSnapshotAsync(filePath, snapshotId, cancellationToken).ConfigureAwait(false);
        var snapshotPath = Path.Combine(GetDocumentDirectory(filePath), "versions", entry.FileName);
        return await File.ReadAllTextAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreSnapshotFileAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var content = await ReadSnapshotContentAsync(filePath, snapshotId, cancellationToken).ConfigureAwait(false);
        var fullPath = Path.GetFullPath(filePath);
        await CreateSnapshotAsync(
            fullPath,
            SnapshotTrigger.RestoreBeforeOverwrite,
            pendingContent: content,
            force: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSnapshotAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(filePath);
        var metadata = await ReadMetadataAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var versionsDirectory = Path.Combine(GetDocumentDirectory(fullPath), "versions");

        var entry = metadata.Versions.FirstOrDefault(
            version => string.Equals(version.SnapshotId, snapshotId, StringComparison.Ordinal));

        // 幂等：metadata 中无此 id 视为已删除，不抛异常（对齐 CleanupSnapshotsAsync 的容错风格）。
        if (entry is null)
        {
            return;
        }

        var snapshotPath = Path.Combine(versionsDirectory, entry.FileName);
        try
        {
            if (File.Exists(snapshotPath))
            {
                File.Delete(snapshotPath);
            }
        }
        catch
        {
            // 文件删除是 best-effort：即使删不掉（占用/权限），仍更新 metadata 避免幽灵条目。
            // 下一次 ListSnapshotsAsync 也会用 File.Exists 过滤掉文件不存在的条目。
        }

        var remaining = metadata.Versions
            .Where(version => !string.Equals(version.SnapshotId, snapshotId, StringComparison.Ordinal))
            .ToArray();

        await WriteMetadataAsync(
            fullPath,
            metadata with { UpdatedAt = DateTimeOffset.UtcNow, Versions = remaining },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CleanupSnapshotsAsync(
        string filePath,
        SnapshotRetentionPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        var retention = policy ?? SnapshotRetentionPolicy.Default;
        var fullPath = Path.GetFullPath(filePath);
        var metadata = await ReadMetadataAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var versionsDirectory = Path.Combine(GetDocumentDirectory(fullPath), "versions");
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retention.MaxRetentionDays);

        var keep = metadata.Versions
            .Where(entry => entry.CreatedAt >= cutoff)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(Math.Max(1, retention.MaxSnapshotsPerDocument))
            .ToArray();

        var keepIds = keep.Select(entry => entry.SnapshotId).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in metadata.Versions.Where(entry => !keepIds.Contains(entry.SnapshotId)))
        {
            var snapshotPath = Path.Combine(versionsDirectory, entry.FileName);
            try
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }
            }
            catch
            {
                // Cleanup is best effort; never block the active save path.
            }
        }

        await WriteMetadataAsync(
            fullPath,
            metadata with { UpdatedAt = DateTimeOffset.UtcNow, Versions = keep },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentSnapshotEntry> FindSnapshotAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListSnapshotsAsync(filePath, cancellationToken).ConfigureAwait(false);
        var entry = snapshots.FirstOrDefault(item => string.Equals(item.SnapshotId, snapshotId, StringComparison.Ordinal));
        return entry ?? throw new FileNotFoundException($"快照不存在：{snapshotId}", snapshotId);
    }

    private async Task<DocumentSnapshotMetadata> ReadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        var documentDirectory = GetDocumentDirectory(fullPath);
        var metadataPath = Path.Combine(documentDirectory, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return CreateEmptyMetadata(fullPath);
        }

        try
        {
            await using var stream = File.OpenRead(metadataPath);
            var metadata = await JsonSerializer.DeserializeAsync<DocumentSnapshotMetadata>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return metadata ?? CreateEmptyMetadata(fullPath);
        }
        catch
        {
            return CreateEmptyMetadata(fullPath);
        }
    }

    private async Task WriteMetadataAsync(
        string filePath,
        DocumentSnapshotMetadata metadata,
        CancellationToken cancellationToken)
    {
        var documentDirectory = GetDocumentDirectory(filePath);
        Directory.CreateDirectory(documentDirectory);
        var metadataPath = Path.Combine(documentDirectory, "metadata.json");
        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private DocumentSnapshotMetadata CreateEmptyMetadata(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var now = DateTimeOffset.UtcNow;
        return new DocumentSnapshotMetadata(
            GetDocumentId(fullPath),
            fullPath,
            Path.GetFileName(fullPath),
            GetPlatformName(),
            now,
            now,
            []);
    }

    private string GetDocumentDirectory(string filePath)
    {
        return Path.Combine(_pathProvider.GetSnapshotsRoot(), "documents", GetDocumentId(filePath));
    }

    private static string GetDocumentId(string filePath)
    {
        return ComputeHash(NormalizePathForHash(filePath))[..32];
    }

    private static string NormalizePathForHash(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? fullPath.ToUpperInvariant()
            : fullPath;
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        return "Unknown";
    }
}
