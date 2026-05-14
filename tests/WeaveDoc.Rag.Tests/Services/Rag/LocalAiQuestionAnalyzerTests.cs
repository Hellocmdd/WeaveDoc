using WeaveDoc.Rag.Services;

namespace WeaveDoc.Rag.Tests.Services.Rag;

public sealed class LocalAiQuestionAnalyzerTests
{
    [Fact]
    public void ExtractRequestedDocumentTitle_ReturnsQuotedTitle()
    {
        var title = LocalAiQuestionAnalyzer.ExtractRequestedDocumentTitle("《基于STM32单片机的智能浇花系统设计》这篇论文的硬件组成有哪些？");

        Assert.Equal("基于STM32单片机的智能浇花系统设计", title);
    }

    [Fact]
    public void ExtractCompareSubjects_ReturnsBothSides()
    {
        var subjects = LocalAiQuestionAnalyzer.ExtractCompareSubjects("土壤湿度传感器和温湿度传感器有什么区别？");

        Assert.Equal(["土壤湿度传感器", "温湿度传感器"], subjects);
    }

    [Fact]
    public void IsFollowUpExpansionRequest_DetectsExpansionDirective()
    {
        Assert.True(LocalAiQuestionAnalyzer.IsFollowUpExpansionRequest("继续，详细一点"));
        Assert.False(LocalAiQuestionAnalyzer.IsFollowUpExpansionRequest("智能浇花系统如何控制土壤湿度？"));
    }
}
