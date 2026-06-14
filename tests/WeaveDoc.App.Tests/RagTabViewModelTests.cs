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

        vm.ChatProvider = "cloud";
        Assert.True(vm.IsCloudProviderSelected);
        Assert.Contains("云 API", vm.ActiveProviderSummary);

        // The summary must be re-raised whenever the provider (or the cloud fields it shows) change,
        // so the model-management banner stays in sync with the active backend.
        Assert.Contains(nameof(RagTabViewModel.ActiveProviderSummary), changes);

        changes.Clear();
        vm.CloudModel = "deepseek-chat";
        vm.CloudBaseUrl = "https://api.example.com/v1";
        Assert.Contains(nameof(RagTabViewModel.ActiveProviderSummary), changes);
        Assert.Contains("deepseek-chat", vm.ActiveProviderSummary);
        Assert.Contains("api.example.com", vm.ActiveProviderSummary);
    }

    private static RagTabViewModel NewViewModel() => new(new LocalAiService());
}
