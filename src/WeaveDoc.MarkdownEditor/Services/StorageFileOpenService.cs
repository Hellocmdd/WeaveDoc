using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace WeaveDoc.MarkdownEditor.Services;

public sealed record MarkdownFileOpenResult(
    bool Succeeded,
    string Content,
    string? FilePath,
    string DisplayName,
    string? ErrorMessage)
{
    public static MarkdownFileOpenResult Success(string content, string? filePath, string displayName)
    {
        return new MarkdownFileOpenResult(true, content ?? string.Empty, filePath, displayName, null);
    }

    public static MarkdownFileOpenResult Failure(string errorMessage, string? filePath = null)
    {
        return new MarkdownFileOpenResult(false, string.Empty, filePath, string.Empty, errorMessage);
    }
}

public sealed record PdfFileOpenResult(
    bool Succeeded,
    string FilePath,
    string DisplayName,
    bool IsTemporary,
    string? ErrorMessage)
{
    public static PdfFileOpenResult Success(string filePath, string displayName, bool isTemporary)
    {
        return new PdfFileOpenResult(true, filePath, displayName, isTemporary, null);
    }

    public static PdfFileOpenResult Failure(string errorMessage)
    {
        return new PdfFileOpenResult(false, string.Empty, string.Empty, false, errorMessage);
    }
}

public static class StorageFileOpenService
{
    private static readonly string PdfCacheDirectory = Path.Combine(
        Path.GetTempPath(),
        "WeaveDoc.MarkdownEditor",
        "pdf-cache");

    public static async Task<MarkdownFileOpenResult> OpenMarkdownAsync(
        IStorageFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return MarkdownFileOpenResult.Failure("未选择 Markdown 文件。");
        }

        var displayName = string.IsNullOrWhiteSpace(file.Name) ? "未命名 Markdown" : file.Name;
        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            return await OpenMarkdownPathAsync(localPath, displayName, cancellationToken).ConfigureAwait(true);
        }

        try
        {
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            return await OpenMarkdownStreamAsync(stream, displayName, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MarkdownFileOpenResult.Failure($"读取 Markdown 文件失败：{ex.Message}");
        }
    }

    public static async Task<MarkdownFileOpenResult> OpenMarkdownPathAsync(
        string? filePath,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return MarkdownFileOpenResult.Failure("Markdown 文件路径不能为空。");
        }

        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(filePath)
            : displayName;

        try
        {
            if (!File.Exists(filePath))
            {
                return MarkdownFileOpenResult.Failure($"Markdown 文件不存在：{filePath}", filePath);
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(true);
            return MarkdownFileOpenResult.Success(content, filePath, resolvedDisplayName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MarkdownFileOpenResult.Failure($"读取 Markdown 文件失败：{ex.Message}", filePath);
        }
    }

    public static async Task<MarkdownFileOpenResult> OpenMarkdownStreamAsync(
        Stream stream,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(true);
            return MarkdownFileOpenResult.Success(content, null, displayName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MarkdownFileOpenResult.Failure($"读取 Markdown 文件失败：{ex.Message}");
        }
    }

    public static async Task<PdfFileOpenResult> PreparePdfAsync(
        IStorageFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return PdfFileOpenResult.Failure("未选择 PDF 文件。");
        }

        var displayName = string.IsNullOrWhiteSpace(file.Name) ? "未命名 PDF" : file.Name;
        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
        {
            return PdfFileOpenResult.Success(localPath, displayName, isTemporary: false);
        }

        try
        {
            Directory.CreateDirectory(PdfCacheDirectory);
            await using var input = await file.OpenReadAsync().ConfigureAwait(true);
            return await PreparePdfStreamAsync(input, displayName, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PdfFileOpenResult.Failure($"准备 PDF 文件失败：{ex.Message}");
        }
    }

    public static async Task<PdfFileOpenResult> PreparePdfStreamAsync(
        Stream input,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(PdfCacheDirectory);
            var extension = Path.GetExtension(displayName);
            if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".pdf";
            }

            var tempPath = Path.Combine(PdfCacheDirectory, $"{Guid.NewGuid():N}{extension}");
            await using var output = File.Create(tempPath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(true);
            return PdfFileOpenResult.Success(tempPath, displayName, isTemporary: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PdfFileOpenResult.Failure($"准备 PDF 文件失败：{ex.Message}");
        }
    }

    public static void TryDeleteTemporaryFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath)
                && Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(PdfCacheDirectory), StringComparison.Ordinal))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }
}
