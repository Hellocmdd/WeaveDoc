using System.Reflection;
using System.Runtime.InteropServices;

namespace WeaveDoc.Converter.Pandoc;

public sealed class WordComPdfConverter : IPdfConverter
{
    private const int WdExportFormatPdf = 17;
    private const int WdDoNotSaveChanges = 0;

    public string Name => "Microsoft Word";

    public void ConvertToPdf(string docxPath, string pdfPath)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Word COM 自动化只支持 Windows。");

        var outputDir = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        var wordType = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException("未检测到可用的 Word.Application COM 注册信息。");

        object? word = null;
        object? documents = null;
        object? document = null;

        try
        {
            word = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("无法启动 Microsoft Word。");
            SetProperty(word, "Visible", false);
            SetProperty(word, "DisplayAlerts", 0);

            documents = GetProperty(word, "Documents");
            if (documents == null)
                throw new InvalidOperationException("无法访问 Word Documents 集合。");

            document = Invoke(documents, "Open", docxPath, false, true);
            if (document == null)
                throw new InvalidOperationException("Word 无法打开 DOCX 文档。");

            Invoke(document, "ExportAsFixedFormat", pdfPath, WdExportFormatPdf);
        }
        finally
        {
            if (document != null)
            {
                try { Invoke(document, "Close", WdDoNotSaveChanges); } catch { }
                ReleaseComObject(document);
            }

            if (documents != null)
                ReleaseComObject(documents);

            if (word != null)
            {
                try { Invoke(word, "Quit", WdDoNotSaveChanges); } catch { }
                ReleaseComObject(word);
            }
        }
    }

    private static object? GetProperty(object target, string name) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);

    private static void SetProperty(object target, string name, object value) =>
        target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, [value]);

    private static object? Invoke(object target, string name, params object?[] args) =>
        target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

    private static void ReleaseComObject(object target)
    {
        if (Marshal.IsComObject(target))
        {
#pragma warning disable CA1416
            Marshal.FinalReleaseComObject(target);
#pragma warning restore CA1416
        }
    }
}
