using RagAvalonia.Models;
using RagAvalonia.Services;

namespace RagAvalonia.Tests.Services.Rag;

public sealed class RagPipelineHeuristicsTests
{
    [Fact]
    public void ProcedureBoost_PrefersStructuredMechanismChunkOverMarkdownFragment()
    {
        var profile = new QueryProfile(
            "这个系统如何完成自动控制",
            ["系统"],
            "procedure",
            false,
            null,
            false,
            false,
            false,
            false,
            []);

        var markdownFragment = new DocumentChunk(
            "fragment",
            "doc/example.md",
            1,
            "，系统采用多层架构并实现自动控制",
            [],
            "通用系统",
            "",
            "",
            "body");

        var structuredMechanism = new DocumentChunk(
            "structured",
            "doc/example.json",
            2,
            "根据实时输入数据计算误差，控制策略动态调节输出，并驱动执行机构形成反馈。",
            [],
            "通用系统",
            "2.3 控制策略",
            "content > 2 方法 > 2.3 控制策略",
            "body");

        var fragmentBoost = LocalAiService.ComputeTopChunkIntentBoost(markdownFragment, profile);
        var structuredBoost = LocalAiService.ComputeTopChunkIntentBoost(structuredMechanism, profile);

        Assert.True(structuredBoost > fragmentBoost + 2.0f);
    }

    [Fact]
    public void CitationNormalization_ExpandsKnownBareChunkCitation()
    {
        var chunk = new DocumentChunk(
            "method",
            "json/example.json",
            5,
            "控制策略会根据输入误差调节输出。",
            [],
            "通用系统",
            "控制策略",
            "content > method",
            "body");

        var answer = "控制阶段会根据输入误差调节输出 [c6]。";

        var normalized = LocalAiService.NormalizeGeneratedAnswerCitations(answer, [chunk], [], null);

        Assert.Contains("[json/example.json | content > method | c6]", normalized);
        Assert.DoesNotContain("[c6]", normalized);
    }

    [Fact]
    public void CitationNormalization_FlagsUnknownBareChunkCitation()
    {
        var chunk = new DocumentChunk(
            "method",
            "json/example.json",
            5,
            "控制策略会根据输入误差调节输出。",
            [],
            "通用系统",
            "控制策略",
            "content > method",
            "body");

        Assert.True(LocalAiService.ContainsUnknownBareChunkCitation("控制阶段见 [c99]。", [chunk]));
        Assert.False(LocalAiService.ContainsUnknownBareChunkCitation("控制阶段见 [c6]。", [chunk]));
    }

    [Fact]
    public void SummaryFallback_ComposesLeadAndCoreBodyEvidence()
    {
        var profile = new QueryProfile(
            "总结这份 JSON 文档主要讲了什么，不要直接抄英文摘要或关键词",
            ["json"],
            "summary",
            false,
            "通用系统设计",
            false,
            true,
            true,
            false,
            []);

        var chunks = new[]
        {
            new DocumentChunk(
                "abstract",
                "json/example.json",
                1,
                "abstract: 面向复杂场景的自动控制需求，本文设计了一套通用系统，覆盖数据采集、策略决策、执行反馈和效果验证。",
                [],
                "通用系统设计",
                "abstract",
                "abstract",
                "abstract"),
            new DocumentChunk(
                "workflow",
                "json/example.json",
                2,
                "- 初始化阶段：系统启动时配置外设接口并加载参数。- 主程序循环：周期性处理任务。",
                [],
                "通用系统设计",
                "workflow",
                "content > workflow",
                "body"),
            new DocumentChunk(
                "method",
                "json/example.json",
                3,
                "核心控制方法根据实时数据计算误差，通过算法策略动态调节输出。",
                [],
                "通用系统设计",
                "控制方法",
                "content > method",
                "body"),
            new DocumentChunk(
                "module",
                "json/example.json",
                4,
                "执行模块接收控制结果并驱动外部设备，交互模块负责显示状态和同步数据。",
                [],
                "通用系统设计",
                "执行与交互",
                "content > module",
                "body"),
            new DocumentChunk(
                "result",
                "json/example.json",
                5,
                "测试验证显示系统能够降低人工干预并提升运行稳定性。",
                [],
                "通用系统设计",
                "效果验证",
                "content > result",
                "body")
        };

        var answer = LocalAiService.BuildLocalSummaryFallbackAnswer(profile, chunks, ["json/example.json"]);

        Assert.StartsWith("《通用系统设计》主要讲的是面向复杂场景的自动控制需求", answer);
        Assert.Contains("控制方法", answer);
        Assert.Contains("执行模块", answer);
        Assert.Contains("交互模块", answer);
        Assert.Contains("效果", answer);
        Assert.DoesNotContain("主要围绕- 初始化阶段", answer);
        Assert.DoesNotContain("englishAbstract", answer, StringComparison.OrdinalIgnoreCase);
    }
}
