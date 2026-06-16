namespace WeaveDoc.App.Services.Documents;

public sealed record SnapshotRetentionPolicy(
    int MaxSnapshotsPerDocument = 50,
    int MaxRetentionDays = 30,
    int AutoSnapshotMinIntervalMinutes = 5,
    int AutoSnapshotContentChangeThreshold = 500)
{
    public static SnapshotRetentionPolicy Default { get; } = new();
}
