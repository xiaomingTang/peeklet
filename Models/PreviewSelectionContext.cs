using System;

namespace Peeklet.Models;

public sealed record PreviewSelectionContext(
    IReadOnlyList<string> Files,
    int SelectedIndex,
    ScreenRect AnchorRect,
    IntPtr SourceWindowHandle)
{
    public string SelectedFilePath => Files[SelectedIndex];

    public string? TryGetPreviousFilePath()
    {
        var index = SelectedIndex - 1;
        return index >= 0 ? Files[index] : null;
    }

    public string? TryGetNextFilePath()
    {
        var index = SelectedIndex + 1;
        return index < Files.Count ? Files[index] : null;
    }

    public PreviewSelectionContext MoveTo(string filePath)
    {
        for (var index = 0; index < Files.Count; index++)
        {
            if (string.Equals(Files[index], filePath, StringComparison.OrdinalIgnoreCase))
            {
                return this with { SelectedIndex = index };
            }
        }

        return this;
    }
}