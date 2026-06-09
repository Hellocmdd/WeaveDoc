using WeaveDoc.App.Services.Documents;

namespace WeaveDoc.App.Tests.Fakes;

internal sealed class FakeMarkdownFilePickerService : IMarkdownFilePickerService
{
    private readonly Queue<string?> _results = [];

    public void QueueResult(string? filePath)
    {
        _results.Enqueue(filePath);
    }

    public Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_results.Count == 0 ? null : _results.Dequeue());
    }
}
