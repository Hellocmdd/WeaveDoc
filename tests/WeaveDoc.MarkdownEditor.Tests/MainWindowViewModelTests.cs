using NUnit.Framework;
using WeaveDoc.MarkdownEditor.ViewModels;

namespace WeaveDoc.MarkdownEditor.Tests;

[TestFixture]
public class MainWindowViewModelTests
{
    [Test]
    public async Task OpenFile_WhenMarkdownExists_UpdatesContentPathAndStatus()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(filePath, "# Loaded\n\nBody");

        try
        {
            var viewModel = new MainWindowViewModel();

            var result = await viewModel.OpenFile(filePath);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(viewModel.EditorContent, Is.EqualTo("# Loaded\n\nBody"));
            Assert.That(viewModel.CurrentFilePath, Is.EqualTo(filePath));
            Assert.That(viewModel.DisplayName, Is.EqualTo(Path.GetFileName(filePath)));
            Assert.That(viewModel.StatusText, Does.Contain("已打开"));
            Assert.That(viewModel.IsStatusError, Is.False);
            Assert.That(viewModel.PreviewHtml, Is.Empty);

            viewModel.RefreshPreview();

            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1 data-line=\"1\">"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task OpenFile_WhenMarkdownDoesNotExist_PreservesExistingContentAndReportsFailure()
    {
        var viewModel = new MainWindowViewModel
        {
            EditorContent = "# Keep me",
            CurrentFilePath = "/tmp/keep.md",
            DisplayName = "keep.md"
        };

        var result = await viewModel.OpenFile(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(viewModel.EditorContent, Is.EqualTo("# Keep me"));
        Assert.That(viewModel.CurrentFilePath, Is.EqualTo("/tmp/keep.md"));
        Assert.That(viewModel.DisplayName, Is.EqualTo("keep.md"));
        Assert.That(viewModel.StatusText, Does.Contain("不存在"));
        Assert.That(viewModel.IsStatusError, Is.True);
    }

    [Test]
    public void EditorContentSetter_DoesNotRegeneratePreviewOrWriteDebugOutput()
    {
        var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);
            var viewModel = new MainWindowViewModel();
            var initialPreview = viewModel.PreviewHtml;

            viewModel.EditorContent = "# Edited";

            Assert.That(viewModel.EditorContent, Is.EqualTo("# Edited"));
            Assert.That(viewModel.PreviewHtml, Is.EqualTo(initialPreview));
            Assert.That(output.ToString(), Is.Empty);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Test]
    public void RefreshPreview_UpdatesPreviewOnDemand()
    {
        var viewModel = new MainWindowViewModel
        {
            EditorContent = "# Edited"
        };

        viewModel.RefreshPreview();

        Assert.That(viewModel.PreviewHtml, Does.Contain("<h1 data-line=\"1\">"));
        Assert.That(viewModel.PreviewHtml, Does.Contain("data-pos=\"1-3\""));
    }
}
