using System.Runtime.InteropServices;

namespace WeaveDoc.Converter.Pandoc;

public sealed class PdfRendererDetector
{
    private static readonly string[] LibreOfficePaths =
    [
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    ];

    private static readonly string[] WordPaths =
    [
        @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE",
        @"C:\Program Files (x86)\Microsoft Office\root\Office16\WINWORD.EXE"
    ];

    private readonly string? _libreOfficePathOverride;
    private readonly bool? _isWindowsOverride;
    private readonly bool? _wordComAvailableOverride;

    public PdfRendererDetector(
        string? libreOfficePathOverride = null,
        bool? isWindowsOverride = null,
        bool? wordComAvailableOverride = null)
    {
        _libreOfficePathOverride = libreOfficePathOverride;
        _isWindowsOverride = isWindowsOverride;
        _wordComAvailableOverride = wordComAvailableOverride;
    }

    public IReadOnlyList<PdfRendererInfo> Detect()
    {
        return
        [
            DetectWord(),
            DetectLibreOffice(),
            new PdfRendererInfo(PdfRendererKind.Syncfusion, "Syncfusion DocIO", true)
        ];
    }

    public PdfRendererInfo DetectWord()
    {
        if (!IsWindows())
        {
            return new PdfRendererInfo(
                PdfRendererKind.Word,
                "Microsoft Word",
                false,
                UnavailableReason: "当前系统不是 Windows，无法使用 Word COM 自动化。");
        }

        if (_wordComAvailableOverride.HasValue)
        {
            return _wordComAvailableOverride.Value
                ? new PdfRendererInfo(PdfRendererKind.Word, "Microsoft Word", true)
                : new PdfRendererInfo(
                    PdfRendererKind.Word,
                    "Microsoft Word",
                    false,
                    UnavailableReason: "Word COM 测试覆盖为不可用。");
        }

        try
        {
#pragma warning disable CA1416
            var wordType = Type.GetTypeFromProgID("Word.Application");
#pragma warning restore CA1416
            if (wordType != null)
            {
                return new PdfRendererInfo(PdfRendererKind.Word, "Microsoft Word", true);
            }
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            return new PdfRendererInfo(
                PdfRendererKind.Word,
                "Microsoft Word",
                false,
                UnavailableReason: $"Word COM 不可用：{ex.Message}");
        }

        var wordPath = WordPaths.FirstOrDefault(File.Exists);
        return wordPath == null
            ? new PdfRendererInfo(
                PdfRendererKind.Word,
                "Microsoft Word",
                false,
                UnavailableReason: "未检测到 Word COM 注册信息或常见安装路径。")
            : new PdfRendererInfo(
                PdfRendererKind.Word,
                "Microsoft Word",
                false,
                wordPath,
                "检测到 WINWORD.EXE，但未检测到可用的 Word COM 注册信息。");
    }

    public PdfRendererInfo DetectLibreOffice()
    {
        var path = ResolveLibreOfficePath();
        return path == null
            ? new PdfRendererInfo(
                PdfRendererKind.LibreOffice,
                "LibreOffice",
                false,
                UnavailableReason: "未在常见安装路径或 PATH 中检测到 soffice.exe。")
            : new PdfRendererInfo(PdfRendererKind.LibreOffice, "LibreOffice", true, path);
    }

    private string? ResolveLibreOfficePath()
    {
        if (!string.IsNullOrWhiteSpace(_libreOfficePathOverride))
            return File.Exists(_libreOfficePathOverride) ? _libreOfficePathOverride : null;

        var knownPath = LibreOfficePaths.FirstOrDefault(File.Exists);
        if (knownPath != null)
            return knownPath;

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), OperatingSystem.IsWindows() ? "soffice.exe" : "soffice");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private bool IsWindows() =>
        _isWindowsOverride ?? RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
