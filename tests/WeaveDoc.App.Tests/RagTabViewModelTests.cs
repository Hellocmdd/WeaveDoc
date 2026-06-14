using WeaveDoc.App.ViewModels;
using WeaveDoc.Rag.Models;
using WeaveDoc.Rag.Services;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class RagTabViewModelTests
{
    [Fact]
    public void Fresh_ViewModel_HasEmptyTurnsAndSendDisabled()
    {
        var vm = NewViewModel();

        Assert.Empty(vm.Turns);
        Assert.True(vm.HasNoTurns);
        Assert.False(vm.HasTurns);
        Assert.False(vm.IsSendEnabled);         // empty input
        Assert.False(vm.IsActionButtonEnabled); // neither busy nor has text
        Assert.Equal("发送", vm.SendButtonText);
        Assert.Empty(vm.LastRankedChunks);
        Assert.False(vm.HasRankedChunks);
        Assert.Equal("知识库待初始化，应用启动后会自动准备。", vm.StatusText);
    }

    [Fact]
    public void InputText_FlipsSendEnabled()
    {
        var vm = NewViewModel();

        vm.InputText = "Transformer 是什么？";

        Assert.True(vm.IsSendEnabled);
        Assert.True(vm.IsActionButtonEnabled);

        vm.InputText = "   ";

        Assert.False(vm.IsSendEnabled);
    }

    [Fact]
    public void ChatProvider_TogglesCloudAndLocalFlags()
    {
        var vm = NewViewModel();
        // Normalize to a known provider first — CloudApiSettings.Load() may read a persisted file.
        vm.ChatProvider = "llama_server";

        Assert.True(vm.IsLocalProviderSelected);
        Assert.False(vm.IsCloudProviderSelected);

        vm.ChatProvider = "cloud";

        Assert.True(vm.IsCloudProviderSelected);
        Assert.False(vm.IsLocalProviderSelected);

        vm.ChatProvider = "llama_server";

        Assert.True(vm.IsLocalProviderSelected);
        Assert.False(vm.IsCloudProviderSelected);
    }

    [Fact]
    public void ChatBackendScopeSummary_StatesCloudOnlyChangesChatLlm()
    {
        var vm = NewViewModel();

        vm.ChatProvider = "cloud";

        Assert.Contains("只接管 Chat LLM", vm.ChatBackendScopeSummary);
        Assert.Contains("Embedding", vm.ChatBackendScopeSummary);
        Assert.Contains("reranker", vm.ChatBackendScopeSummary);
        Assert.Contains("本地", vm.ChatBackendScopeSummary);
    }

    [Fact]
    public void SelectedLocalLlamaModel_TogglesDeleteAvailability()
    {
        var vm = NewViewModel();
        var model = new LocalLlamaModelItem("chat.gguf", "可用", "1.0 GB", "/tmp/chat.gguf");

        Assert.False(vm.CanDeleteSelectedLocalLlamaModel);

        vm.SelectedLocalLlamaModel = model;

        Assert.True(vm.CanDeleteSelectedLocalLlamaModel);

        vm.SelectedLocalLlamaModel = null;

        Assert.False(vm.CanDeleteSelectedLocalLlamaModel);
    }

    [Fact]
    public void UnloadModels_ResetsUiState()
    {
        var vm = NewViewModel();
        vm.Turns.Add(new ChatTurn("用户", "hi", true));

        vm.UnloadModels();

        Assert.Equal("模型已卸载。", vm.StatusText);
        Assert.Empty(vm.CorpusFiles);
        Assert.Empty(vm.LastRankedChunks);
        Assert.Equal("模型已卸载，尚未执行检索。", vm.RetrievalDebugText);
    }

    [Fact]
    public void ClearConversation_EmptiesTurnsAndResetsSnapshots()
    {
        var vm = NewViewModel();
        vm.Turns.Add(new ChatTurn("用户", "hi", true));
        vm.Turns.Add(new ChatTurn("助手", "hello", false));

        vm.ClearConversation();

        Assert.Empty(vm.Turns);
        Assert.True(vm.HasNoTurns);
        Assert.Empty(vm.LastRankedChunks);
        Assert.Equal("会话已清空。", vm.StatusText);
    }

    [Fact]
    public void Turns_CollectionChange_RaisesHasTurns()
    {
        var vm = NewViewModel();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.Turns.Add(new ChatTurn("用户", "q", true));

        Assert.Contains(nameof(RagTabViewModel.HasTurns), changes);
        Assert.Contains(nameof(RagTabViewModel.HasNoTurns), changes);
        Assert.True(vm.HasTurns);
    }

    [Fact]
    public void ActiveProviderSummary_ReflectsAndTracksProvider()
    {
        var vm = NewViewModel();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.ChatProvider = "llama_server";
        Assert.True(vm.IsLocalProviderSelected);
        Assert.Contains("本地", vm.ActiveProviderSummary);
        Assert.Contains("模型: 本地", vm.ProviderBadgeText);

        vm.ChatProvider = "cloud";
        Assert.True(vm.IsCloudProviderSelected);
        Assert.Contains("云 API", vm.ActiveProviderSummary);
        Assert.Contains("模型: 云端", vm.ProviderBadgeText);

        // The summary must be re-raised whenever the provider (or the cloud fields it shows) change,
        // so the model-management banner stays in sync with the active backend.
        Assert.Contains(nameof(RagTabViewModel.ActiveProviderSummary), changes);
        Assert.Contains(nameof(RagTabViewModel.ProviderBadgeText), changes);

        changes.Clear();
        vm.CloudModel = "deepseek-chat";
        vm.CloudBaseUrl = "https://api.example.com/v1";
        Assert.Contains(nameof(RagTabViewModel.ActiveProviderSummary), changes);
        Assert.Contains(nameof(RagTabViewModel.ProviderBadgeText), changes);
        Assert.Contains("deepseek-chat", vm.ActiveProviderSummary);
        Assert.Contains("deepseek-chat", vm.ProviderBadgeText);
        Assert.Contains("api.example.com", vm.ActiveProviderSummary);
        Assert.Contains("api.example.com", vm.ProviderBadgeToolTip);
    }

    [Fact]
    public void ProviderSettings_UpdateInjectedAiServiceImmediately()
    {
        using var service = new LocalAiService();
        var vm = new RagTabViewModel(service);

        vm.ChatProvider = "llama_server";
        var localModel = service.LlamaServerModel;
        var localEndpoint = service.LlamaServerEndpoint;

        vm.ChatProvider = "cloud";
        vm.CloudModel = "deepseek-chat";
        vm.CloudBaseUrl = "https://api.example.com/v1";

        Assert.Equal("deepseek-chat", service.LlamaServerModel);
        Assert.Equal("https://api.example.com/v1", service.LlamaServerEndpoint);

        vm.ChatProvider = "llama_server";

        Assert.Equal(localModel, service.LlamaServerModel);
        Assert.Equal(localEndpoint, service.LlamaServerEndpoint);
    }

    private static RagTabViewModel NewViewModel() => new(new LocalAiService());
}
