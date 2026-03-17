using Peeklet.Models;

namespace Peeklet.Services;

public sealed class PreviewPlacementService
{
    private const double Gap = 12;

    public PreviewPlacement Calculate(ScreenRect anchor, ScreenRect workingArea, double preferredWidth, double preferredHeight)
    {
        var width = Math.Min(preferredWidth, workingArea.Width - 32);
        var height = Math.Min(preferredHeight, workingArea.Height - 32);

        foreach (var candidate in BuildCandidates(anchor, workingArea, width, height))
        {
            if (Fits(candidate, workingArea))
            {
                return candidate;
            }
        }

        return new PreviewPlacement(
            Left: workingArea.Left + (workingArea.Width - width) / 2,
            Top: workingArea.Top + (workingArea.Height - height) / 2,
            Width: width,
            Height: height,
            Strategy: "fallback-center");
    }

    private static IEnumerable<PreviewPlacement> BuildCandidates(ScreenRect anchor, ScreenRect workingArea, double width, double height)
    {
        var rightLeft = anchor.Right + Gap;
        var leftLeft = anchor.Left - Gap - width;
        var centerLeft = workingArea.Left + (workingArea.Width - width) / 2;

        yield return new PreviewPlacement(rightLeft, anchor.Top, width, height, "right-top");
        yield return new PreviewPlacement(rightLeft, AlignCenter(anchor, height), width, height, "right-center");
        yield return new PreviewPlacement(rightLeft, anchor.Bottom - height, width, height, "right-bottom");
        yield return new PreviewPlacement(leftLeft, anchor.Top, width, height, "left-top");
        yield return new PreviewPlacement(leftLeft, AlignCenter(anchor, height), width, height, "left-center");
        yield return new PreviewPlacement(leftLeft, anchor.Bottom - height, width, height, "left-bottom");
        yield return new PreviewPlacement(centerLeft, anchor.Top, width, height, "center-top");
        yield return new PreviewPlacement(centerLeft, AlignCenter(anchor, height), width, height, "center-center");
        yield return new PreviewPlacement(centerLeft, Math.Max(workingArea.Top, workingArea.Bottom - height), width, height, "center-bottom");
    }

    private static double AlignCenter(ScreenRect anchor, double height)
    {
        return anchor.Top + (anchor.Height - height) / 2;
    }

    private static bool Fits(PreviewPlacement placement, ScreenRect workingArea)
    {
        return placement.Left >= workingArea.Left
            && placement.Top >= workingArea.Top
            && placement.Left + placement.Width <= workingArea.Right
            && placement.Top + placement.Height <= workingArea.Bottom;
    }
}