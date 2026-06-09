using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.ViewModels;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class DocumentWorkspaceViewModelTests
{
    [Fact]
    public void InitialState_IsEmptyMarkdownDocument()
    {
        var viewModel = new DocumentWorkspaceViewModel(new FakeMarkdownDocumentService());

        Assert.Null(viewModel.CurrentFilePath);
        Assert.Equal("未打开 Markdown 文档", viewModel.DisplayName);
        Assert.Empty(viewModel.Content);
        Assert.Empty(viewModel.PreviewHtml);
        Assert.False(viewModel.HasDocument);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("未打开文档", viewModel.StatusText);
        Assert.DoesNotContain("# Hello WeaveDoc!", viewModel.Content);
    }

    [Fact]
    public async Task OpenAsync_WhenReadSucceeds_LoadsDocumentAndPreviewWithoutDirtyState()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 标题", "/workspace/demo.md", "<h1>标题</h1>"));
        var viewModel = new DocumentWorkspaceViewModel(service);

        var opened = await viewModel.OpenAsync("/workspace/demo.md", TestContext.Current.CancellationToken);

        Assert.True(opened);
        Assert.Equal(["/workspace/demo.md"], service.ReadPaths);
        Assert.Equal("/workspace/demo.md", viewModel.CurrentFilePath);
        Assert.Equal("demo.md", viewModel.DisplayName);
        Assert.Equal("# 标题", viewModel.Content);
        Assert.Equal("<h1>标题</h1>", viewModel.PreviewHtml);
        Assert.Empty(service.PreviewRequests);
        Assert.True(viewModel.HasDocument);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.HasError);
        Assert.Equal("已打开 demo.md", viewModel.StatusText);
    }

    [Fact]
    public async Task UpdateContent_WhenDocumentIsOpen_DoesNotRefreshPreviewAndEnablesSave()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 旧标题", "/workspace/demo.md", "<h1>旧标题</h1>"));
        service.PreviewFactory = (content, filePath) =>
            MarkdownDocumentResult.Success(content, filePath, $"<preview>{content}</preview>");
        var viewModel = new DocumentWorkspaceViewModel(service);
        await viewModel.OpenAsync("/workspace/demo.md", TestContext.Current.CancellationToken);

        viewModel.UpdateContent("# 新标题");

        Assert.Equal("# 新标题", viewModel.Content);
        Assert.Empty(service.PreviewRequests);
        Assert.Equal("<h1>旧标题</h1>", viewModel.PreviewHtml);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanSave);
        Assert.Equal("已修改 demo.md", viewModel.StatusText);
    }

    [Fact]
    public async Task ContentSetter_WhenEditorBindingWritesContent_DoesNotRefreshPreview()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 旧标题", "/workspace/demo.md", "<h1>旧标题</h1>"));
        service.PreviewFactory = (content, filePath) =>
            MarkdownDocumentResult.Success(content, filePath, $"<preview file=\"{Path.GetFileName(filePath)}\">{content}</preview>");
        var viewModel = new DocumentWorkspaceViewModel(service);
        await viewModel.OpenAsync("/workspace/demo.md", TestContext.Current.CancellationToken);

        viewModel.Content = "# 新标题";

        Assert.Empty(service.PreviewRequests);
        Assert.Equal("# 新标题", viewModel.Content);
        Assert.Equal("<h1>旧标题</h1>", viewModel.PreviewHtml);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanSave);
        Assert.Empty(service.Saves);
    }

    [Fact]
    public async Task RefreshPreview_WhenDocumentChanged_UpdatesPreviewFromCurrentContent()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 旧标题", "/workspace/demo.md", "<h1>旧标题</h1>"));
        service.PreviewFactory = (content, filePath) =>
            MarkdownDocumentResult.Success(content, filePath, $"<preview file=\"{Path.GetFileName(filePath)}\">{content}</preview>");
        var viewModel = new DocumentWorkspaceViewModel(service);
        await viewModel.OpenAsync("/workspace/demo.md", TestContext.Current.CancellationToken);
        viewModel.Content = "# 新标题";

        var refreshed = viewModel.RefreshPreview();

        Assert.True(refreshed);
        var previewRequest = Assert.Single(service.PreviewRequests);
        Assert.Equal("# 新标题", previewRequest.Content);
        Assert.Equal("/workspace/demo.md", previewRequest.FilePath);
        Assert.Equal("<preview file=\"demo.md\"># 新标题</preview>", viewModel.PreviewHtml);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task SaveAsync_WhenDirtyDocumentSaves_ResetsDirtyState()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 旧标题", "/workspace/demo.md", "<h1>旧标题</h1>"));
        service.QueueSave(MarkdownDocumentResult.Success("# 新标题", "/workspace/demo.md", "<h1>新标题</h1>"));
        var viewModel = new DocumentWorkspaceViewModel(service);
        await viewModel.OpenAsync("/workspace/demo.md", TestContext.Current.CancellationToken);
        viewModel.UpdateContent("# 新标题");

        var saved = await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.True(saved);
        var save = Assert.Single(service.Saves);
        Assert.Equal("/workspace/demo.md", save.FilePath);
        Assert.Equal("# 新标题", save.Content);
        Assert.Equal("# 新标题", viewModel.Content);
        Assert.Equal("<h1>新标题</h1>", viewModel.PreviewHtml);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.HasError);
        Assert.Equal("已保存 demo.md", viewModel.StatusText);
    }

    [Fact]
    public async Task OpenAsync_WhenReadFails_PreservesCurrentDocumentAndShowsError()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 当前", "/workspace/current.md", "<h1>当前</h1>"));
        service.QueueRead(MarkdownDocumentResult.Failure("读取失败", filePath: "/workspace/missing.md"));
        var viewModel = new DocumentWorkspaceViewModel(service);
        await viewModel.OpenAsync("/workspace/current.md", TestContext.Current.CancellationToken);
        viewModel.UpdateContent("# 已修改");

        var opened = await viewModel.OpenAsync("/workspace/missing.md", TestContext.Current.CancellationToken);

        Assert.False(opened);
        Assert.Equal("/workspace/current.md", viewModel.CurrentFilePath);
        Assert.Equal("current.md", viewModel.DisplayName);
        Assert.Equal("# 已修改", viewModel.Content);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.HasError);
        Assert.Equal("读取失败", viewModel.ErrorMessage);
        Assert.Equal("读取失败", viewModel.StatusText);
    }

    [Fact]
    public async Task SaveAsync_WhenSaveFails_PreservesDirtyDocumentAndShowsError()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 当前", "/workspace/current.md", "<h1>当前</h1>"));
        service.QueueSave(MarkdownDocumentResult.Failure(
            "保存失败",
            "# 已修改",
            "/workspace/current.md",
            "<h1>已修改</h1>"));
        var viewModel = new DocumentWorkspaceViewModel(service);
        await viewModel.OpenAsync("/workspace/current.md", TestContext.Current.CancellationToken);
        viewModel.UpdateContent("# 已修改");

        var saved = await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.False(saved);
        Assert.Equal("# 已修改", viewModel.Content);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.HasError);
        Assert.Equal("保存失败", viewModel.ErrorMessage);
        Assert.Equal("保存失败", viewModel.StatusText);
    }

    [Fact]
    public async Task AppShellViewModel_ExposesDocumentWorkspaceAndMirrorsDocumentStatus()
    {
        var service = new FakeMarkdownDocumentService();
        service.QueueRead(MarkdownDocumentResult.Success("# 标题", "/workspace/demo.md", "<h1>标题</h1>"));
        var workspace = new DocumentWorkspaceViewModel(service);
        var shell = new AppShellViewModel(workspace);
        var changedProperties = new List<string?>();
        shell.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        await shell.DocumentWorkspace.OpenAsync("/workspace/demo.md", TestContext.Current.CancellationToken);

        Assert.Same(workspace, shell.DocumentWorkspace);
        Assert.True(shell.HasDocuments);
        Assert.Equal("demo.md", shell.CurrentDocumentTitle);
        Assert.Equal("/workspace/demo.md", shell.CurrentDocumentSubtitle);
        Assert.Equal("已打开 demo.md", shell.StatusText);
        Assert.DoesNotContain("# Hello WeaveDoc!", shell.DocumentWorkspace.Content);
        Assert.Contains(nameof(AppShellViewModel.HasDocuments), changedProperties);
        Assert.Contains(nameof(AppShellViewModel.CurrentDocumentTitle), changedProperties);
        Assert.Contains(nameof(AppShellViewModel.CurrentDocumentSubtitle), changedProperties);
        Assert.Contains(nameof(AppShellViewModel.StatusText), changedProperties);
    }

    private sealed class FakeMarkdownDocumentService : IMarkdownDocumentService
    {
        private readonly Queue<MarkdownDocumentResult> _readResults = [];
        private readonly Queue<MarkdownDocumentResult> _saveResults = [];

        public List<string> ReadPaths { get; } = [];

        public List<(string FilePath, string Content)> Saves { get; } = [];

        public List<(string Content, string? FilePath)> PreviewRequests { get; } = [];

        public Func<string, string?, MarkdownDocumentResult> PreviewFactory { get; set; } =
            (content, filePath) => MarkdownDocumentResult.Success(content, filePath, $"<preview>{content}</preview>");

        public void QueueRead(MarkdownDocumentResult result)
        {
            _readResults.Enqueue(result);
        }

        public void QueueSave(MarkdownDocumentResult result)
        {
            _saveResults.Enqueue(result);
        }

        public Task<MarkdownDocumentResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadPaths.Add(filePath);
            var result = _readResults.Count == 0
                ? MarkdownDocumentResult.Failure("读取失败", filePath: filePath)
                : _readResults.Dequeue();

            return Task.FromResult(result);
        }

        public Task<MarkdownDocumentResult> SaveAsync(
            string filePath,
            string content,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Saves.Add((filePath, content));
            var result = _saveResults.Count == 0
                ? MarkdownDocumentResult.Success(content, filePath, $"<preview>{content}</preview>")
                : _saveResults.Dequeue();

            return Task.FromResult(result);
        }

        public MarkdownDocumentResult CreatePreview(string content, string? filePath = null)
        {
            PreviewRequests.Add((content, filePath));
            return PreviewFactory(content, filePath);
        }
    }
}
