using System.Diagnostics;

namespace WeaveDoc.Converter.Pandoc;

public sealed class LibreOfficePdfConverter : IPdfConverter
{
    private readonly string _sofficePath;

    public LibreOfficePdfConverter(string sofficePath)
    {
        if (string.IsNullOrWhiteSpace(sofficePath))
            throw new ArgumentException("LibreOffice 路径不能为空。", nameof(sofficePath));

        _sofficePath = sofficePath;
    }

    public string Name => "LibreOffice";

    public void ConvertToPdf(string docxPath, string pdfPath)
    {
        var outputDir = Path.GetDirectoryName(pdfPath);
        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = Directory.GetCurrentDirectory();

        Directory.CreateDirectory(outputDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = _sofficePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDir);
        startInfo.ArgumentList.Add(docxPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 LibreOffice。");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"LibreOffice 转 PDF 失败，退出码 {process.ExitCode}。{FirstNonEmpty(stderr, stdout)}");
        }

        var generatedPath = Path.Combine(outputDir, Path.ChangeExtension(Path.GetFileName(docxPath), ".pdf"));
        if (!File.Exists(generatedPath))
            throw new FileNotFoundException($"LibreOffice 未生成 PDF 文件。{FirstNonEmpty(stderr, stdout)}", generatedPath);

        if (!string.Equals(generatedPath, pdfPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(pdfPath))
                File.Delete(pdfPath);
            File.Move(generatedPath, pdfPath);
        }
    }

    private static string FirstNonEmpty(string stderr, string stdout)
    {
        var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return string.IsNullOrWhiteSpace(detail) ? "" : $" 输出：{detail.Trim()}";
    }
}
