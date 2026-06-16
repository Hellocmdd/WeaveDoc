using WeaveDoc.App.Services.Documents;

namespace WeaveDoc.App.Tests.Fakes;

internal sealed class FakeDocumentSnapshotService : IDocumentSnapshotService
{
    private readonly Queue<string> _snapshotContents = [];

    public List<CreateSnapshotRequest> CreateSnapshotRequests { get; } = [];

    public List<(string FilePath, string SnapshotId)> ReadSnapshotRequests { get; } = [];

    public List<(string FilePath, string SnapshotId)> RestoreSnapshotRequests { get; } = [];

    public List<string> ListSnapshotPaths { get; } = [];

    public IReadOnlyList<DocumentSnapshotEntry> Snapshots { get; set; } = [];

    public Exception? RestoreException { get; set; }

    public void QueueSnapshotContent(string content)
    {
        _snapshotContents.Enqueue(content);
    }

    public Task<DocumentSnapshotEntry?> CreateSnapshotAsync(
        string filePath,
        SnapshotTrigger trigger,
        string? pendingContent = null,
        bool force = false,
        SnapshotRetentionPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreateSnapshotRequests.Add(new CreateSnapshotRequest(filePath, trigger, pendingContent, force, policy));
        return Task.FromResult<DocumentSnapshotEntry?>(new DocumentSnapshotEntry(
            SnapshotId: $"snapshot-{CreateSnapshotRequests.Count}",
            FileName: $"snapshot-{CreateSnapshotRequests.Count}.md",
            OriginalPath: filePath,
            CreatedAt: DateTimeOffset.UtcNow,
            OriginalSizeBytes: 0,
            SnapshotSizeBytes: pendingContent?.Length ?? 0,
            ContentLength: pendingContent?.Length ?? 0,
            ContentHash: string.Empty,
            Trigger: trigger,
            Note: null));
    }

    public Task<IReadOnlyList<DocumentSnapshotEntry>> ListSnapshotsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ListSnapshotPaths.Add(filePath);
        return Task.FromResult(Snapshots);
    }

    public Task<string> ReadSnapshotContentAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadSnapshotRequests.Add((filePath, snapshotId));
        return Task.FromResult(_snapshotContents.Count == 0 ? string.Empty : _snapshotContents.Dequeue());
    }

    public Task RestoreSnapshotFileAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RestoreSnapshotRequests.Add((filePath, snapshotId));
        if (RestoreException is not null)
        {
            throw RestoreException;
        }

        return Task.CompletedTask;
    }

    public Task CleanupSnapshotsAsync(
        string filePath,
        SnapshotRetentionPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

internal sealed record CreateSnapshotRequest(
    string FilePath,
    SnapshotTrigger Trigger,
    string? PendingContent,
    bool Force,
    SnapshotRetentionPolicy? Policy);
