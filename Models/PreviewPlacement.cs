namespace Peeklet.Models;

public sealed record PreviewPlacement(
    double Left,
    double Top,
    double Width,
    double Height,
    string Strategy);