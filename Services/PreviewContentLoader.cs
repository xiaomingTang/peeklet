using System.IO;
using System.Text;
using Markdig;
using Peeklet.Models;

namespace Peeklet.Services;

public sealed class PreviewContentLoader
{
    private const int MaxTextLength = 512 * 1024;

    public async Task<PreviewContent> LoadAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        return request.ContentKind switch
        {
            PreviewContentKind.Image => BuildImageContent(request),
            PreviewContentKind.Text => await BuildTextContentAsync(request, cancellationToken),
            PreviewContentKind.Markdown => await BuildMarkdownContentAsync(request, cancellationToken),
            PreviewContentKind.Pdf => await BuildPdfContentAsync(request, cancellationToken),
            PreviewContentKind.Svg => BuildBrowserContent(request, "SVG preview"),
            PreviewContentKind.Office => BuildOfficeContent(request),
            _ => BuildUnsupported(request)
        };
    }

    private static PreviewContent BuildImageContent(PreviewRequest request)
    {
        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Image,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            ImagePath: request.FilePath);
    }

    private static async Task<PreviewContent> BuildTextContentAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(request.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var buffer = new char[MaxTextLength];
        var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        var text = new string(buffer, 0, read);

        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Text,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            TextContent: text);
    }

    private static async Task<PreviewContent> BuildMarkdownContentAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        var markdown = await File.ReadAllTextAsync(request.FilePath, cancellationToken);
        var htmlBody = Markdown.ToHtml(markdown);
                var html = $@"
<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"" />
        <style>
            :root {{ color-scheme: light; }}
            body {{ font-family: Segoe UI, sans-serif; margin: 32px; color: #1f2937; background: #f7f7f5; }}
            img {{ max-width: 100%; height: auto; }}
            pre {{ background: #111827; color: #f3f4f6; padding: 16px; overflow: auto; border-radius: 12px; }}
            code {{ font-family: Cascadia Code, Consolas, monospace; }}
            blockquote {{ border-left: 4px solid #d1d5db; padding-left: 16px; color: #4b5563; }}
            table {{ border-collapse: collapse; width: 100%; }}
            th, td {{ border: 1px solid #d1d5db; padding: 8px; }}
        </style>
    </head>
    <body>{htmlBody}</body>
</html>";

        var tempFile = Path.Combine(Path.GetTempPath(), $"preview-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tempFile, html, cancellationToken);

        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Browser,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            NavigateTo: tempFile);
    }

    private static PreviewContent BuildBrowserContent(PreviewRequest request, string title)
    {
        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Browser,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            NavigateTo: request.FilePath,
            PlaceholderMessage: title);
    }

    private static async Task<PreviewContent> BuildPdfContentAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        if (PreviewHandlerRegistry.TryGetHandlerClsid(request.FilePath, out _))
        {
            return BuildPreviewHandlerContent(request, "System PDF preview");
        }

        var pdfUri = new Uri(request.FilePath).AbsoluteUri;
        var html = $@"
<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"" />
        <style>
            html, body {{ margin: 0; height: 100%; overflow: hidden; background: #0f1115; }}
            embed, iframe {{ border: 0; width: 100%; height: 100%; display: block; }}
        </style>
    </head>
    <body>
        <embed src=""{pdfUri}#toolbar=0&navpanes=0&scrollbar=0"" type=""application/pdf"" />
    </body>
</html>";

        var tempFile = Path.Combine(Path.GetTempPath(), $"preview-pdf-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tempFile, html, cancellationToken);

        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Browser,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            NavigateTo: tempFile,
            PlaceholderMessage: "PDF preview");
    }

    private static PreviewContent BuildOfficeContent(PreviewRequest request)
    {
        if (PreviewHandlerRegistry.TryGetHandlerClsid(request.FilePath, out _))
        {
            return BuildPreviewHandlerContent(request, "System Office preview");
        }

        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Placeholder,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            PlaceholderMessage: "No Windows Preview Handler is registered for this Office file type on this machine.");
    }

    private static PreviewContent BuildPreviewHandlerContent(PreviewRequest request, string subtitlePrefix)
    {
        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.PreviewHandler,
            Title: request.FileName,
            Subtitle: $"{subtitlePrefix}  {FormatSubtitle(request)}",
            PreviewFilePath: request.FilePath);
    }

    private static PreviewContent BuildUnsupported(PreviewRequest request)
    {
        return new PreviewContent(
            SurfaceType: PreviewSurfaceType.Placeholder,
            Title: request.FileName,
            Subtitle: FormatSubtitle(request),
            PlaceholderMessage: "No previewer is registered for this file type yet.");
    }

    private static string FormatSubtitle(PreviewRequest request)
    {
        return $"{request.Extension.ToLowerInvariant()}  {request.FileSize / 1024.0:F1} KB  {request.LastWriteTime.LocalDateTime:yyyy-MM-dd HH:mm}";
    }
}