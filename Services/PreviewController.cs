using System.Windows;
using Peeklet.Models;

namespace Peeklet.Services;

public sealed class PreviewController
{
    private const double DefaultPreviewWidth = 1200;
    private const double DefaultPreviewHeight = 900;

    private readonly ExplorerSelectionService _selectionService = new();
    private readonly PreviewRouter _previewRouter = new();
    private readonly PreviewContentLoader _contentLoader = new();
    private readonly PreviewPlacementService _placementService = new();
    private MainWindow? _window;
    private PreviewSelectionContext? _selectionContext;
    private int _showOperationId;

    public async Task ToggleFromExplorerSelectionAsync()
    {
        if (_window is { IsVisible: true })
        {
            if (!IsPreviewForeground() && !_selectionService.CanTriggerPreview())
            {
                return;
            }

            ClosePreview();
            return;
        }

        await ShowFromExplorerSelectionAsync();
    }

    public bool CanHandlePreviewHotkeys()
    {
        return _window is { IsVisible: true } && IsPreviewForeground();
    }

    public async Task ShowFromExplorerSelectionAsync()
    {
        if (!_selectionService.TryGetSelectionContext(out var selectionContext) || selectionContext is null)
        {
            return;
        }

        _selectionContext = selectionContext;
        await ShowFileAsync(selectionContext.SelectedFilePath, selectionContext.AnchorRect);
    }

    public async Task ShowNextAsync()
    {
        if (_selectionContext is null)
        {
            return;
        }

        var nextFile = _selectionContext.TryGetNextFilePath();
        if (string.IsNullOrWhiteSpace(nextFile))
        {
            return;
        }

        _selectionContext = _selectionContext.MoveTo(nextFile);
        await ShowFileAsync(nextFile, _selectionContext.AnchorRect);
    }

    public async Task ShowPreviousAsync()
    {
        if (_selectionContext is null)
        {
            return;
        }

        var previousFile = _selectionContext.TryGetPreviousFilePath();
        if (string.IsNullOrWhiteSpace(previousFile))
        {
            return;
        }

        _selectionContext = _selectionContext.MoveTo(previousFile);
        await ShowFileAsync(previousFile, _selectionContext.AnchorRect);
    }

    public void ClosePreview()
    {
        if (_window is null)
        {
            return;
        }

        _showOperationId++;
        var window = _window;
        _window = null;
        _selectionContext = null;
        if (!window.IsClosing)
        {
            window.Close();
        }
    }

    private async Task ShowFileAsync(string filePath, ScreenRect anchorRect)
    {
        var request = _previewRouter.BuildRequest(filePath);
        var workingArea = GetWorkingArea(anchorRect);
        var placement = _placementService.Calculate(anchorRect, workingArea, DefaultPreviewWidth, DefaultPreviewHeight);

        if (_window is null || !_window.IsLoaded)
        {
            _window = new MainWindow();
            _window.Closed += (_, _) => _window = null;
            _window.CloseRequested += (_, _) => ClosePreview();
        }

        var window = _window;
        var operationId = ++_showOperationId;

        window.ApplyPlacement(placement);
        window.ShowLoadingState(request.FileName, FormatLoadingSubtitle(request), "Loading preview...");
        window.BringToFront();

        var content = await _contentLoader.LoadAsync(request, CancellationToken.None);
        if (_window != window || window.IsClosing || operationId != _showOperationId)
        {
            return;
        }

        await window.ShowContentAsync(content);
        if (_window != window || window.IsClosing || operationId != _showOperationId)
        {
            return;
        }

        window.BringToFront();
    }

    private static string FormatLoadingSubtitle(PreviewRequest request)
    {
        return $"{request.Extension.ToLowerInvariant()}  {request.FileSize / 1024.0:F1} KB  {request.LastWriteTime.LocalDateTime:yyyy-MM-dd HH:mm}";
    }

    private bool IsPreviewForeground()
    {
        if (_window is null)
        {
            return false;
        }

        var windowHandle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        return windowHandle != IntPtr.Zero && NativeMethods.GetForegroundWindow() == windowHandle;
    }

    private static ScreenRect GetWorkingArea(ScreenRect anchorRect)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            if (anchorRect.Left >= screen.WorkingArea.Left
                && anchorRect.Left <= screen.WorkingArea.Right
                && anchorRect.Top >= screen.WorkingArea.Top
                && anchorRect.Top <= screen.WorkingArea.Bottom)
            {
                return new ScreenRect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
            }
        }

        return new ScreenRect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
    }
}