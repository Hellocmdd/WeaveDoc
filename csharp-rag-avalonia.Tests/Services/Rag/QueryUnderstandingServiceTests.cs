using RagAvalonia.Services;

namespace RagAvalonia.Tests.Services.Rag;

public sealed class QueryUnderstandingServiceTests
{
    [Fact]
    public void BuildProfile_DetectsChineseProcedureQuestion()
    {
        var profile = QueryUnderstandingService.BuildProfile("STM32智能浇花系统如何控制土壤湿度？");

        Assert.Equal("procedure", profile.Intent);
        Assert.Contains(profile.FocusTerms, term => term.Contains("stm32", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(profile.FocusTerms);
    }

    [Fact]
    public void BuildProfile_ExtractsRequestedDocumentTitle()
    {
        var profile = QueryUnderstandingService.BuildProfile("《基于STM32单片机的智能浇花系统设计》这篇论文的硬件组成有哪些？");

        Assert.Equal("基于STM32单片机的智能浇花系统设计", profile.RequestedDocumentTitle);
        Assert.Equal("composition", profile.Intent);
    }

    [Fact]
    public void BuildProfile_HandlesFollowUpDetailDirective()
    {
        var profile = QueryUnderstandingService.BuildProfile("智能浇花系统如何控制土壤湿度？；补充要求：详细一点");

        Assert.True(profile.WantsDetailedAnswer);
        Assert.DoesNotContain(profile.FocusTerms, term => term.Contains("详细一点", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildProfile_RespectsEnglishMetadataAvoidance()
    {
        var profile = QueryUnderstandingService.BuildProfile("总结这篇论文，不要英文摘要和英文关键词");

        Assert.Equal("summary", profile.Intent);
        Assert.True(profile.AvoidsEnglishMetadata);
    }
}
