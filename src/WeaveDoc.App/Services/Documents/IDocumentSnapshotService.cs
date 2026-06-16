namespace WeaveDoc.App.Services.Documents;

public interface IDocumentSnapshotService
{
    Task<DocumentSnapshotEntry?> CreateSnapshotAsync(
        string filePath,
        SnapshotTrigger trigger,
        string? pendingContent = null,
        bool force = false,
        SnapshotRetentionPolicy? policy = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSnapshotEntry>> ListSnapshotsAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<string> ReadSnapshotContentAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task RestoreSnapshotFileAsync(
        string filePath,
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task CleanupSnapshotsAsync(
        string filePath,
        SnapshotRetentionPolicy? policy = null,
        CancellationToken cancellationToken = default);
}
