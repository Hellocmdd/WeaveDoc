using Avalonia.Platform.Storage;

namespace WeaveDoc.App.Services.Documents;

public sealed class AvaloniaMarkdownFilePickerService : IMarkdownFilePickerService
{
    private readonly Func<IStorageProvider?> _getStorageProvider;

    public AvaloniaMarkdownFilePickerService(Func<IStorageProvider?> getStorageProvider)
    {
        _getStorageProvider = getStorageProvider ?? throw new ArgumentNullException(nameof(getStorageProvider));
    }

    public async Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storageProvider = _getStorageProvider();
        if (storageProvider is null)
        {
            return null;
        }

        var selected = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开 Markdown 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown 文件") { Patterns = ["*.md", "*.markdown", "*.txt"] },
                FilePickerFileTypes.All
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return selected.FirstOrDefault()?.TryGetLocalPath();
    }
}
