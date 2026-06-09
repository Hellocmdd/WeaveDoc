namespace WeaveDoc.App.Services.Documents;

public interface IMarkdownFilePickerService
{
    Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default);
}
