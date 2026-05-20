using System.ComponentModel;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter;

internal static class ConversionErrorFormatter
{
    public static string ToUserMessage(Exception ex, string markdownPath, string outputFormat)
    {
        return ex switch
        {
            OperationCanceledException => "转换已取消。",
            FileNotFoundException fileEx => $"找不到文件：{fileEx.FileName ?? markdownPath}",
            DirectoryNotFoundException => "输入文件或输出目录所在路径不存在，请检查路径是否已被移动或删除。",
            UnauthorizedAccessException => "没有足够权限读取输入文件或写入输出文件，请检查文件占用和目录权限。",
            IOException ioEx when IsFileInUse(ioEx) => "文件正在被其他程序占用，请关闭 Word、PDF 阅读器或同步软件后重试。",
            IOException => "读写文件时失败，请检查输出目录权限、磁盘空间以及目标文件是否正在被占用。",
            Win32Exception win32Ex when IsPandocStartFailure(win32Ex)
                => "无法启动 Pandoc，请先构建项目以下载 tools/pandoc，或确认 pandoc.exe 可用。",
            InvalidOperationException invalidEx when invalidEx.Message.Contains("Pandoc", StringComparison.OrdinalIgnoreCase)
                => "无法启动 Pandoc，请先构建项目以下载 tools/pandoc，或确认 pandoc.exe 可用。",
            PandocException pandocEx => FormatPandocError(pandocEx),
            _ => $"转换为 {outputFormat.ToUpperInvariant()} 时发生未知错误：{ex.Message}"
        };
    }

    private static string FormatPandocError(PandocException ex)
    {
        var stderr = ex.Stderr ?? "";
        if (stderr.Contains("withBinaryFile", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("No such file", StringComparison.OrdinalIgnoreCase))
        {
            return "Pandoc 无法读取输入文件，请确认 Markdown 文件路径有效。";
        }

        if (stderr.Contains("Could not fetch resource", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("HttpExceptionRequest", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("ConnectionFailure", StringComparison.OrdinalIgnoreCase))
        {
            return "Pandoc 无法获取文档中的远程图片或资源。请检查网络连接，或先把远程图片下载成本地文件后再转换。";
        }

        if (stderr.Contains("Could not find image", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("image", StringComparison.OrdinalIgnoreCase) && stderr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Pandoc 找不到文档引用的图片文件，请检查 Markdown 中的图片路径。";
        }

        if (stderr.Contains("reference-doc", StringComparison.OrdinalIgnoreCase))
            return "Pandoc 无法读取 reference.docx 模板，请检查 AFD 模板生成结果。";

        var detail = FirstMeaningfulLine(stderr);
        return string.IsNullOrWhiteSpace(detail)
            ? $"Pandoc 转换失败，退出码 {ex.ExitCode}。"
            : $"Pandoc 转换失败：{detail}";
    }

    private static string FirstMeaningfulLine(string text)
    {
        return text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? "";
    }

    private static bool IsFileInUse(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex.HResult is sharingViolation or lockViolation;
    }

    private static bool IsPandocStartFailure(Win32Exception ex) =>
        ex.Message.Contains("系统找不到指定的文件", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("The system cannot find the file", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("No such file", StringComparison.OrdinalIgnoreCase);
}
