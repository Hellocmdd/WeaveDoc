namespace WeaveDoc.App.Services.Documents;

public sealed record DocumentSnapshotEntry(
    string SnapshotId,
    string FileName,
    string OriginalPath,
    DateTimeOffset CreatedAt,
    long OriginalSizeBytes,
    long SnapshotSizeBytes,
    int ContentLength,
    string ContentHash,
    SnapshotTrigger Trigger,
    string? Note);
