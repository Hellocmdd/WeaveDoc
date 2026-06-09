using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.Tests.Fakes;
using Xunit;

namespace WeaveDoc.App.Tests;

public class MarkdownDocumentContractsTests
{
    [Fact]
    public void UnsavedChangesDecision_DefinesExpectedBranches()
    {
        var names = Enum.GetNames<UnsavedChangesDecision>();

        Assert.Equal(["Save", "Discard", "Cancel"], names);
    }

    [Fact]
    public void Success_KeepsDocumentDataAndDerivesDisplayName()
    {
        var result = MarkdownDocumentResult.Success(
            "# 标题",
            "/workspace/demo.md",
            "<h1 data-line=\"1\">标题</h1>");

        Assert.True(result.Succeeded);
        Assert.Equal("# 标题", result.Content);
        Assert.Equal("/workspace/demo.md", result.FilePath);
        Assert.Equal("demo.md", result.DisplayName);
        Assert.Equal("<h1 data-line=\"1\">标题</h1>", result.PreviewHtml);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_KeepsDisplayableErrorWithoutThrowing()
    {
        var result = MarkdownDocumentResult.Failure(
            "读取 Markdown 文件失败。",
            "# 当前内容",
            "/workspace/current.md",
            "<h1 data-line=\"1\">当前内容</h1>");

        Assert.False(result.Succeeded);
        Assert.Equal("# 当前内容", result.Content);
        Assert.Equal("/workspace/current.md", result.FilePath);
        Assert.Equal("current.md", result.DisplayName);
        Assert.Equal("<h1 data-line=\"1\">当前内容</h1>", result.PreviewHtml);
        Assert.Equal("读取 Markdown 文件失败。", result.ErrorMessage);
    }

    [Fact]
    public async Task FakeMarkdownFilePickerService_CanReturnPathAndCancel()
    {
        var fake = new FakeMarkdownFilePickerService();
        fake.QueueResult("/workspace/demo.md");
        fake.QueueResult(null);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal("/workspace/demo.md", await fake.PickMarkdownFileAsync(cancellationToken));
        Assert.Null(await fake.PickMarkdownFileAsync(cancellationToken));
    }

    [Fact]
    public async Task FakeUnsavedChangesConfirmationService_CoversAllDecisionBranches()
    {
        var fake = new FakeUnsavedChangesConfirmationService();
        fake.QueueDecision(UnsavedChangesDecision.Save);
        fake.QueueDecision(UnsavedChangesDecision.Discard);
        fake.QueueDecision(UnsavedChangesDecision.Cancel);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(UnsavedChangesDecision.Save, await fake.ConfirmAsync("first.md", cancellationToken));
        Assert.Equal(UnsavedChangesDecision.Discard, await fake.ConfirmAsync("second.md", cancellationToken));
        Assert.Equal(UnsavedChangesDecision.Cancel, await fake.ConfirmAsync("third.md", cancellationToken));
        Assert.Equal(["first.md", "second.md", "third.md"], fake.ConfirmedDisplayNames);
    }

    [Fact]
    public async Task AvaloniaMarkdownFilePickerService_ReturnsNullWhenStorageProviderIsUnavailable()
    {
        var service = new AvaloniaMarkdownFilePickerService(() => null);

        Assert.Null(await service.PickMarkdownFileAsync(TestContext.Current.CancellationToken));
    }
}
