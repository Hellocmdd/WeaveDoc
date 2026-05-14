using WeaveDoc.Rag.Models;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.Rag.Tests.Services.Rag;

public sealed class LocalAiFallbackAnswerBuilderTests
{
    [Fact]
    public void BuildSummaryFallbackAnswer_PreservesCoreSummaryAndStableCitations()
    {
        var profile = new QueryProfile(
            "总结这份 JSON 文档主要讲了什么，不要直接抄英文摘要或关键词",
            ["json"],
            "summary",
            false,
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

        var answer = LocalAiFallbackAnswerBuilder.BuildSummaryFallbackAnswer(profile, chunks, ["json/example.json"]);

        Assert.StartsWith("《通用系统设计》主要讲的是面向复杂场景的自动控制需求", answer);
        Assert.Contains("[json/example.json | abstract | c2]", answer);
        Assert.Contains("控制方法", answer);
        Assert.Contains("执行模块", answer);
        Assert.Contains("效果", answer);
    }

    [Fact]
    public void NormalizeGeneratedAnswerCitations_AddsStableCitationPerLine()
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

        var normalized = LocalAiService.NormalizeGeneratedAnswerCitations(
            "控制阶段会根据输入误差调节输出。",
            [chunk],
            ["json/example.json"],
            null);

        Assert.Contains("[json/example.json | content > method | c6]", normalized);
    }
}
