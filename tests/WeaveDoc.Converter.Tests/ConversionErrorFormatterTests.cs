using Xunit;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter.Tests;

public class ConversionErrorFormatterTests
{
    [Fact]
    public void ToUserMessage_PandocExitWithoutStderrAndMissingInput_ReturnsFriendlyError()
    {
        var missingMarkdownPath = Path.Combine(
            Path.GetTempPath(),
            $"missing-{Guid.NewGuid():N}.md");
        var exception = new PandocException(
            "Pandoc 转换失败，退出码 1",
            1,
            "pandoc",
            missingMarkdownPath,
            stdout: "",
            stderr: "");

        var message = ConversionErrorFormatter.ToUserMessage(
            exception,
            missingMarkdownPath,
            "docx");

        Assert.Contains("Pandoc 无法读取输入文件", message);
    }
}
