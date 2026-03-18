using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Peeklet.Models;
using Peeklet.Services;

namespace Peeklet;

public partial class MainWindow : Window
{
    private const double MinImageZoom = 1.0;
    private const double MaxImageZoom = 6.0;
    private const double ImageZoomStep = 0.15;

    private double _imageZoom = 1.0;
    private bool _isClosing;
    private bool _isImagePanning;
    private System.Windows.Point _imagePanStartPoint;
    private double _imagePanStartHorizontalOffset;
    private double _imagePanStartVerticalOffset;

    public bool IsClosing => _isClosing;

    public event EventHandler? CloseRequested;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ShowLoadingState(string title, string subtitle, string message)
    {
        if (_isClosing)
        {
            return;
        }

        TitleText.Text = title;
        SubtitleText.Text = subtitle;

        ResetImageZoom();
        NativePreviewHost.ClearPreview();
        BrowserPreview.Source = null;
        ImagePreview.Source = null;
        TextPreview.Text = string.Empty;

        TextHost.Visibility = Visibility.Collapsed;
        ImageHost.Visibility = Visibility.Collapsed;
        BrowserHost.Visibility = Visibility.Collapsed;
        PreviewHandlerHost.Visibility = Visibility.Collapsed;

        PlaceholderText.Text = message;
        PlaceholderHost.Visibility = Visibility.Visible;
    }

    public void ApplyPlacement(PreviewPlacement placement)
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(placement.Left),
            (int)Math.Round(placement.Top),
            (int)Math.Round(placement.Width),
            (int)Math.Round(placement.Height),
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    public async Task ShowContentAsync(PreviewContent content)
    {
        if (_isClosing)
        {
            return;
        }

        TitleText.Text = content.Title;
        SubtitleText.Text = content.Subtitle;

        NativePreviewHost.ClearPreview();
        BrowserPreview.Source = null;
        ImagePreview.Source = null;
        TextPreview.Text = string.Empty;

        TextHost.Visibility = Visibility.Collapsed;
        ImageHost.Visibility = Visibility.Collapsed;
        BrowserHost.Visibility = Visibility.Collapsed;
        PreviewHandlerHost.Visibility = Visibility.Collapsed;
        PlaceholderHost.Visibility = Visibility.Collapsed;

        switch (content.SurfaceType)
        {
            case PreviewSurfaceType.Text:
                TextPreview.Text = content.TextContent ?? string.Empty;
                TextHost.Visibility = Visibility.Visible;
                break;

            case PreviewSurfaceType.Image:
                ImagePreview.Source = CreateBitmap(content.ImagePath!);
                ResetImageZoom();
                ImageHost.Visibility = Visibility.Visible;
                QueueImageViewportUpdate();
                break;

            case PreviewSurfaceType.Browser:
                await EnsureWebViewReadyAsync();
                if (_isClosing)
                {
                    return;
                }

                BrowserPreview.Source = new Uri(content.NavigateTo!);
                BrowserHost.Visibility = Visibility.Visible;
                break;

            case PreviewSurfaceType.PreviewHandler:
                if (NativePreviewHost.TryShowPreview(content.PreviewFilePath!))
                {
                    PreviewHandlerHost.Visibility = Visibility.Visible;
                    break;
                }

                PlaceholderText.Text = "Failed to load the registered Windows Preview Handler for this file.";
                PlaceholderHost.Visibility = Visibility.Visible;
                break;

            default:
                PlaceholderText.Text = content.PlaceholderMessage ?? string.Empty;
                PlaceholderHost.Visibility = Visibility.Visible;
                break;
        }

        QueuePreviewSurfaceFocus();
    }

    public void BringToFront()
    {
        if (_isClosing)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        Topmost = true;
        Activate();
        Focus();
        ForceActivateWindow(hwnd);
        Topmost = false;

        if (hwnd != IntPtr.Zero)
        {
            QueuePreviewSurfaceFocus();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    private static BitmapImage CreateBitmap(string imagePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(imagePath);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private async Task EnsureWebViewReadyAsync()
    {
        if (BrowserPreview.CoreWebView2 is not null)
        {
            return;
        }

        var environment = await WebViewEnvironmentProvider.GetAsync();
        await BrowserPreview.EnsureCoreWebView2Async(environment);
        var coreWebView = BrowserPreview.CoreWebView2;
        if (coreWebView is null)
        {
            return;
        }

        coreWebView.Settings.AreDefaultContextMenusEnabled = false;
        coreWebView.Settings.AreDevToolsEnabled = false;
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FocusPreviewSurface()
    {
        if (_isClosing || !IsVisible)
        {
            return;
        }

        if (PreviewHandlerHost.Visibility == Visibility.Visible && NativePreviewHost.FocusPreview())
        {
            return;
        }

        if (BrowserHost.Visibility == Visibility.Visible && BrowserPreview.Focus())
        {
            Keyboard.Focus(BrowserPreview);
            return;
        }

        if (TextHost.Visibility == Visibility.Visible && TextPreview.Focus())
        {
            Keyboard.Focus(TextPreview);
            return;
        }

        if (ImageHost.Visibility == Visibility.Visible && ImageScrollViewer.Focus())
        {
            Keyboard.Focus(ImageScrollViewer);
            return;
        }

        Keyboard.Focus(this);
    }

    private void QueuePreviewSurfaceFocus()
    {
        Dispatcher.BeginInvoke(FocusPreviewSurface, DispatcherPriority.Input);
    }

    private static void ForceActivateWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var foregroundThreadId = foregroundWindow != IntPtr.Zero
            ? NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _)
            : 0;
        var attached = false;

        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attached = NativeMethods.AttachThreadInput(foregroundThreadId, currentThreadId, true);
            }

            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.SetActiveWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(foregroundThreadId, currentThreadId, false);
            }
        }
    }

    private void ImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_imageZoom <= MinImageZoom)
        {
            UpdateImageViewport();
        }
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ImagePreview.Source is null || ImageHost.Visibility != Visibility.Visible)
        {
            return;
        }

        var mousePosition = e.GetPosition(ImageScrollViewer);
        var oldZoom = _imageZoom;
        _imageZoom = e.Delta > 0
            ? Math.Min(MaxImageZoom, _imageZoom + ImageZoomStep)
            : Math.Max(MinImageZoom, _imageZoom - ImageZoomStep);

        if (Math.Abs(_imageZoom - oldZoom) < double.Epsilon)
        {
            return;
        }

        var targetHorizontalOffset = ((mousePosition.X + ImageScrollViewer.HorizontalOffset) / oldZoom * _imageZoom) - mousePosition.X;
        var targetVerticalOffset = ((mousePosition.Y + ImageScrollViewer.VerticalOffset) / oldZoom * _imageZoom) - mousePosition.Y;

        ImageZoomTransform.ScaleX = _imageZoom;
        ImageZoomTransform.ScaleY = _imageZoom;

        if (_imageZoom <= MinImageZoom)
        {
            SetImageScrollbarsEnabled(false);
            UpdateImageViewport();
            ImageScrollViewer.UpdateLayout();
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);
            UpdateImageCursor();
            e.Handled = true;
            return;
        }

        SetImageScrollbarsEnabled(true);
        ImageScrollViewer.UpdateLayout();
        ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, targetHorizontalOffset));
        ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, targetVerticalOffset));
        UpdateImageCursor();
        e.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_imageZoom <= 1.0 || ImagePreview.Source is null)
        {
            return;
        }

        _isImagePanning = true;
        _imagePanStartPoint = e.GetPosition(ImageScrollViewer);
        _imagePanStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
        _imagePanStartVerticalOffset = ImageScrollViewer.VerticalOffset;
        ImageScrollViewer.CaptureMouse();
        ImageScrollViewer.Cursor = System.Windows.Input.Cursors.SizeAll;
        e.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isImagePanning)
        {
            return;
        }

        var currentPoint = e.GetPosition(ImageScrollViewer);
        var delta = currentPoint - _imagePanStartPoint;
        ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, _imagePanStartHorizontalOffset - delta.X));
        ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, _imagePanStartVerticalOffset - delta.Y));
        e.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isImagePanning)
        {
            return;
        }

        _isImagePanning = false;
        ImageScrollViewer.ReleaseMouseCapture();
        UpdateImageCursor();
        e.Handled = true;
    }

    private void ResetImageZoom()
    {
        _imageZoom = 1.0;
        _isImagePanning = false;
        ImageZoomTransform.ScaleX = 1.0;
        ImageZoomTransform.ScaleY = 1.0;
        SetImageScrollbarsEnabled(false);
        ImageScrollViewer.ScrollToHorizontalOffset(0);
        ImageScrollViewer.ScrollToVerticalOffset(0);
        ImageScrollViewer.ReleaseMouseCapture();
        UpdateImageCursor();
    }

    private void UpdateImageViewport()
    {
        if (ImageScrollViewer.ActualWidth <= 0 || ImageScrollViewer.ActualHeight <= 0)
        {
            return;
        }

        var imageContentHost = FindName("ImageContentHost") as Border;
        var horizontalPadding = imageContentHost?.Padding.Left + imageContentHost?.Padding.Right ?? 0;
        var verticalPadding = imageContentHost?.Padding.Top + imageContentHost?.Padding.Bottom ?? 0;

        ImagePreview.Width = Math.Max(1, ImageScrollViewer.ActualWidth - horizontalPadding);
        ImagePreview.Height = Math.Max(1, ImageScrollViewer.ActualHeight - verticalPadding);
    }

    private void UpdateImageCursor()
    {
        ImageScrollViewer.Cursor = _isImagePanning || _imageZoom > 1.0
            ? System.Windows.Input.Cursors.SizeAll
            : System.Windows.Input.Cursors.Arrow;
    }

    private void QueueImageViewportUpdate()
    {
        Dispatcher.BeginInvoke(UpdateImageViewport, DispatcherPriority.Loaded);
    }

    private void SetImageScrollbarsEnabled(bool enabled)
    {
        var visibility = enabled ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        ImageScrollViewer.HorizontalScrollBarVisibility = visibility;
        ImageScrollViewer.VerticalScrollBarVisibility = visibility;
    }
}