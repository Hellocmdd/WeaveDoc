using System.Text.Json;
using System.Text.Json.Serialization;
using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.Tests.Fakes;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class DocumentSnapshotServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task RestoreSnapshotFileAsync_CreatesRestoreSnapshotBeforeOverwritingCurrentFile()
    {
        var root = CreateTestRoot();
        try
        {
            var snapshotsRoot = Path.Combine(root, "snapshots-root");
            var documentPath = Path.Combine(root, "docs", "paper draft.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            await File.WriteAllTextAsync(documentPath, "# v1", TestContext.Current.CancellationToken);
            var service = new DocumentSnapshotService(new FakeWeaveDocUserDataPathProvider(snapshotsRoot));

            var original = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.ManualSave,
                pendingContent: "# v2",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(original);
            await File.WriteAllTextAsync(documentPath, "# v2", TestContext.Current.CancellationToken);

            await service.RestoreSnapshotFileAsync(
                documentPath,
                original!.SnapshotId,
                TestContext.Current.CancellationToken);

            Assert.Equal("# v1", await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken));
            Assert.StartsWith(snapshotsRoot, GetMetadataPath(snapshotsRoot), StringComparison.OrdinalIgnoreCase);
            var snapshots = await service.ListSnapshotsAsync(documentPath, TestContext.Current.CancellationToken);
            var restoreSnapshot = snapshots.Single(snapshot => snapshot.Trigger == SnapshotTrigger.RestoreBeforeOverwrite);
            var protectedContent = await service.ReadSnapshotContentAsync(
                documentPath,
                restoreSnapshot.SnapshotId,
                TestContext.Current.CancellationToken);
            Assert.Equal("# v2", protectedContent);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task RestoreSnapshotFileAsync_WhenCurrentContentAlreadyExistsInHistory_DoesNotCreateDuplicateProtectionSnapshots()
    {
        var root = CreateTestRoot();
        try
        {
            var snapshotsRoot = Path.Combine(root, "snapshots-root");
            var documentPath = Path.Combine(root, "docs", "paper.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            var service = new DocumentSnapshotService(new FakeWeaveDocUserDataPathProvider(snapshotsRoot));

            await File.WriteAllTextAsync(documentPath, "# v1", TestContext.Current.CancellationToken);
            var v1 = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.ManualSave,
                pendingContent: "# v2",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(documentPath, "# v2", TestContext.Current.CancellationToken);
            var v2 = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.ManualSave,
                pendingContent: "# v3",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(v1);
            Assert.NotNull(v2);

            await service.RestoreSnapshotFileAsync(documentPath, v1!.SnapshotId, TestContext.Current.CancellationToken);
            await service.RestoreSnapshotFileAsync(documentPath, v2!.SnapshotId, TestContext.Current.CancellationToken);
            await service.RestoreSnapshotFileAsync(documentPath, v1.SnapshotId, TestContext.Current.CancellationToken);

            var snapshots = await service.ListSnapshotsAsync(documentPath, TestContext.Current.CancellationToken);

            Assert.Equal(2, snapshots.Count);
            Assert.DoesNotContain(snapshots, snapshot => snapshot.Trigger == SnapshotTrigger.RestoreBeforeOverwrite);
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task ListSnapshotsAsync_UsesProviderRootAndOrdersByCreatedAtDescendingStably()
    {
        var root = CreateTestRoot();
        try
        {
            var snapshotsRoot = Path.Combine(root, "xdg-data", "WeaveDoc", "snapshots");
            var documentPath = Path.Combine(root, "linux-ci", "nested path", "case-sensitive.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            var service = new DocumentSnapshotService(new FakeWeaveDocUserDataPathProvider(snapshotsRoot));

            await File.WriteAllTextAsync(documentPath, "oldest", TestContext.Current.CancellationToken);
            var oldest = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.ManualSave,
                pendingContent: "same-a",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(documentPath, "same-a", TestContext.Current.CancellationToken);
            var sameA = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.ManualSave,
                pendingContent: "same-b",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(documentPath, "same-b", TestContext.Current.CancellationToken);
            var sameB = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.AutoSave,
                pendingContent: "newest",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(documentPath, "newest", TestContext.Current.CancellationToken);
            var newest = await service.CreateSnapshotAsync(
                documentPath,
                SnapshotTrigger.AutoSave,
                pendingContent: "next",
                force: true,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(oldest);
            Assert.NotNull(sameA);
            Assert.NotNull(sameB);
            Assert.NotNull(newest);

            var sameCreatedAt = new DateTimeOffset(2026, 6, 16, 8, 30, 0, TimeSpan.Zero);
            var metadataPath = GetMetadataPath(snapshotsRoot);
            var metadata = await ReadMetadataAsync(metadataPath);
            var rewritten = metadata with
            {
                Versions =
                [
                    oldest! with { CreatedAt = sameCreatedAt.AddMinutes(-10) },
                    sameB! with { CreatedAt = sameCreatedAt },
                    sameA! with { CreatedAt = sameCreatedAt },
                    newest! with { CreatedAt = sameCreatedAt.AddMinutes(10) }
                ]
            };
            await WriteMetadataAsync(metadataPath, rewritten);

            var listed = await service.ListSnapshotsAsync(documentPath, TestContext.Current.CancellationToken);

            Assert.StartsWith(snapshotsRoot, metadataPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                [newest!.SnapshotId, sameB!.SnapshotId, sameA!.SnapshotId, oldest!.SnapshotId],
                listed.Select(snapshot => snapshot.SnapshotId).ToArray());
        }
        finally
        {
            DeleteTestRoot(root);
        }
    }

    private static string CreateTestRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "SnapshotServiceTests",
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetMetadataPath(string snapshotsRoot)
    {
        var documentsRoot = Path.Combine(snapshotsRoot, "documents");
        var documentDirectory = Assert.Single(Directory.GetDirectories(documentsRoot));
        return Path.Combine(documentDirectory, "metadata.json");
    }

    private static async Task<DocumentSnapshotMetadata> ReadMetadataAsync(string metadataPath)
    {
        await using var stream = File.OpenRead(metadataPath);
        var metadata = await JsonSerializer.DeserializeAsync<DocumentSnapshotMetadata>(
            stream,
            JsonOptions,
            TestContext.Current.CancellationToken);
        return Assert.IsType<DocumentSnapshotMetadata>(metadata);
    }

    private static async Task WriteMetadataAsync(string metadataPath, DocumentSnapshotMetadata metadata)
    {
        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(
            stream,
            metadata,
            JsonOptions,
            TestContext.Current.CancellationToken);
    }

    private static void DeleteTestRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
