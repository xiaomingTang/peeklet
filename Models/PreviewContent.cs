namespace Peeklet.Models;

public sealed record PreviewContent(
    PreviewSurfaceType SurfaceType,
    string Title,
    string Subtitle,
    string? TextContent = null,
    string? NavigateTo = null,
    string? PreviewFilePath = null,
    string? ImagePath = null,
    string? PlaceholderMessage = null);