using WeaveDoc.App.Services.Documents;

namespace WeaveDoc.App.Tests.Fakes;

internal sealed class FakeUnsavedChangesConfirmationService : IUnsavedChangesConfirmationService
{
    private readonly Queue<UnsavedChangesDecision> _decisions = [];

    public List<string> ConfirmedDisplayNames { get; } = [];

    public void QueueDecision(UnsavedChangesDecision decision)
    {
        _decisions.Enqueue(decision);
    }

    public Task<UnsavedChangesDecision> ConfirmAsync(string displayName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConfirmedDisplayNames.Add(displayName);
        return Task.FromResult(_decisions.Count == 0 ? UnsavedChangesDecision.Cancel : _decisions.Dequeue());
    }
}
