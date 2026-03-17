using System.IO;
using Peeklet.Models;

namespace Peeklet.Services;

public sealed class PreviewRouter
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif", ".ico", ".tif", ".tiff"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".json", ".xml", ".csv", ".yml", ".yaml", ".ini", ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".go", ".rs", ".cpp", ".h", ".md"
    };

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> SvgExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svg"
    };

    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };

    public PreviewRequest BuildRequest(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var extension = fileInfo.Extension;
        var contentKind = ResolveKind(extension);

        return new PreviewRequest(
            fileInfo.FullName,
            fileInfo.Name,
            extension,
            fileInfo.Exists ? fileInfo.Length : 0,
            fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTimeOffset.UtcNow,
            contentKind);
    }

    private static PreviewContentKind ResolveKind(string extension)
    {
        if (MarkdownExtensions.Contains(extension))
        {
            return PreviewContentKind.Markdown;
        }

        if (SvgExtensions.Contains(extension))
        {
            return PreviewContentKind.Svg;
        }

        if (PdfExtensions.Contains(extension))
        {
            return PreviewContentKind.Pdf;
        }

        if (ImageExtensions.Contains(extension))
        {
            return PreviewContentKind.Image;
        }

        if (OfficeExtensions.Contains(extension))
        {
            return PreviewContentKind.Office;
        }

        if (TextExtensions.Contains(extension))
        {
            return PreviewContentKind.Text;
        }

        return PreviewContentKind.Unsupported;
    }
}