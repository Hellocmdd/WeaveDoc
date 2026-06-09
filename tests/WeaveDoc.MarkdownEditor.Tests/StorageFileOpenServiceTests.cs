using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Tests;

[TestFixture]
public class StorageFileOpenServiceTests
{
    [Test]
    public async Task OpenMarkdownStreamAsync_ReadsStreamOnlyContent()
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("# Stream file"));

        var result = await StorageFileOpenService.OpenMarkdownStreamAsync(stream, "stream-only.md");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Content, Is.EqualTo("# Stream file"));
        Assert.That(result.FilePath, Is.Null);
        Assert.That(result.DisplayName, Is.EqualTo("stream-only.md"));
    }

    [Test]
    public async Task PreparePdfStreamAsync_CopiesStreamToTemporaryPdf()
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        await using var stream = new MemoryStream(bytes);

        var result = await StorageFileOpenService.PreparePdfStreamAsync(stream, "stream-only.pdf");

        try
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsTemporary, Is.True);
            Assert.That(result.DisplayName, Is.EqualTo("stream-only.pdf"));
            Assert.That(result.FilePath, Does.EndWith(".pdf"));
            Assert.That(File.Exists(result.FilePath), Is.True);
            Assert.That(await File.ReadAllBytesAsync(result.FilePath), Is.EqualTo(bytes));
        }
        finally
        {
            StorageFileOpenService.TryDeleteTemporaryFile(result.FilePath);
        }
    }
}
