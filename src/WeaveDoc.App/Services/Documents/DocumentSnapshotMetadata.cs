namespace WeaveDoc.App.Services.Documents;

public sealed record DocumentSnapshotMetadata(
    string DocumentId,
    string OriginalPath,
    string DisplayName,
    string Platform,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DocumentSnapshotEntry> Versions);
