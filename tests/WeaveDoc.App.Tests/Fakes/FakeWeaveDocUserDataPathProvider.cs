using WeaveDoc.App.Services.Documents;

namespace WeaveDoc.App.Tests.Fakes;

internal sealed class FakeWeaveDocUserDataPathProvider(string snapshotsRoot) : IWeaveDocUserDataPathProvider
{
    public string SnapshotsRoot { get; } = snapshotsRoot;

    public string GetSnapshotsRoot() => SnapshotsRoot;
}
