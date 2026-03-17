namespace Peeklet.Models;

public sealed record PreviewRequest(
    string FilePath,
    string FileName,
    string Extension,
    long FileSize,
    DateTimeOffset LastWriteTime,
    PreviewContentKind ContentKind);