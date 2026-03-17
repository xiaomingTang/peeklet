using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Peeklet.Interop;
using Peeklet.Services;

namespace Peeklet.Controls;

public sealed class PreviewHandlerHostControl : HwndHost
{
    private IntPtr _hostHwnd;
    private IPreviewHandler? _previewHandler;
    private string? _pendingFilePath;

    public bool TryShowPreview(string filePath)
    {
        _pendingFilePath = filePath;

        if (_hostHwnd == IntPtr.Zero)
        {
            return true;
        }

        return TryLoadPreview(filePath);
    }

    public void ClearPreview()
    {
        UnloadPreviewHandler();
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hostHwnd = NativeMethods.CreateWindowEx(
            0,
            "Static",
            string.Empty,
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS,
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(ActualHeight)),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hostHwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create preview host window.");
        }

        if (!string.IsNullOrWhiteSpace(_pendingFilePath))
        {
            TryLoadPreview(_pendingFilePath);
        }

        return new HandleRef(this, _hostHwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        UnloadPreviewHandler();

        if (hwnd.Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(hwnd.Handle);
        }

        _hostHwnd = IntPtr.Zero;
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);

        if (_hostHwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.MoveWindow(
            _hostHwnd,
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(rcBoundingBox.Width)),
            Math.Max(1, (int)Math.Ceiling(rcBoundingBox.Height)),
            true);

        if (_previewHandler is not null)
        {
            var rect = CreateRect(rcBoundingBox);
            _previewHandler.SetRect(ref rect);
        }
    }

    private bool TryLoadPreview(string filePath)
    {
        if (!PreviewHandlerRegistry.TryGetHandlerClsid(filePath, out var clsid))
        {
            return false;
        }

        UnloadPreviewHandler();

        object? previewObject = null;

        try
        {
            var handlerType = Type.GetTypeFromCLSID(clsid, throwOnError: true);
            previewObject = Activator.CreateInstance(handlerType!);

            if (previewObject is not IPreviewHandler previewHandler || previewObject is not IInitializeWithFile initializeWithFile)
            {
                if (previewObject is not null)
                {
                    Marshal.FinalReleaseComObject(previewObject);
                }

                return false;
            }

            initializeWithFile.Initialize(filePath, 0);
            var rect = CreateRect(new Rect(0, 0, ActualWidth, ActualHeight));
            previewHandler.SetWindow(_hostHwnd, ref rect);
            previewHandler.DoPreview();
            _previewHandler = previewHandler;
            return true;
        }
        catch
        {
            if (previewObject is not null)
            {
                Marshal.FinalReleaseComObject(previewObject);
            }

            return false;
        }
    }

    private void UnloadPreviewHandler()
    {
        if (_previewHandler is null)
        {
            return;
        }

        try
        {
            _previewHandler.Unload();
        }
        catch
        {
        }

        Marshal.FinalReleaseComObject(_previewHandler);
        _previewHandler = null;
    }

    private static NativeRect CreateRect(Rect bounds)
    {
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));

        return new NativeRect
        {
            Left = 0,
            Top = 0,
            Right = width,
            Bottom = height
        };
    }
}