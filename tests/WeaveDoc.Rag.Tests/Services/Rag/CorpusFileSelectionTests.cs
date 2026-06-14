using WeaveDoc.Rag.Services;

namespace WeaveDoc.Rag.Tests.Services.Rag;

public sealed class CorpusFileSelectionTests
{
    [Theory]
    [InlineData("软件设计/界面设计/页面定义.md")]
    [InlineData("软件设计/界面设计_demo/package.json")]
    [InlineData("notes/node_modules-guide.md")]
    public void ShouldIndexCorpusFile_AcceptsRegularCorpusDocuments(string relativePath)
    {
        Assert.True(ShouldIndex(relativePath));
    }

    [Theory]
    [InlineData("QA/manual.md")]
    [InlineData("dev/root_cause_analysis.md")]
    [InlineData("logs/git_diff.txt")]
    [InlineData("task_doc/native_markdown_editor_migration_tasks.md")]
    [InlineData("软件设计/界面设计_demo/node_modules/next/dist/docs/index.md")]
    [InlineData("软件设计/界面设计_demo/.next/dev/build-manifest.json")]
    [InlineData("软件设计/界面设计_demo/dist/report.json")]
    [InlineData("软件设计/界面设计_demo/build/output.json")]
    [InlineData("软件设计/界面设计_demo/bin/debug.json")]
    [InlineData("软件设计/界面设计_demo/obj/project.assets.json")]
    [InlineData("软件设计/界面设计_demo/coverage/summary.json")]
    [InlineData("软件设计/界面设计_demo/.turbo/cache.json")]
    public void ShouldIndexCorpusFile_RejectsGeneratedOrDependencyDocuments(string relativePath)
    {
        Assert.False(ShouldIndex(relativePath));
    }

    private static bool ShouldIndex(string relativePath)
    {
        var docRoot = Path.Combine(Path.GetTempPath(), "weavedoc-rag-doc-root");
        var filePath = Path.Combine(
            [docRoot, .. relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

        return LocalAiService.ShouldIndexCorpusFile(docRoot, filePath);
    }
}
