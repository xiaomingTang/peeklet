using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Peeklet.Models;

namespace Peeklet.Services;

public sealed class ExplorerSelectionService
{
    private const double SelectionAnchorWidth = 230;

    public bool TryGetSelectionContext(out PreviewSelectionContext? context)
    {
        context = null;

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || !IsExplorerWindow(foregroundWindow) || IsTextInputFocused(foregroundWindow))
        {
            return false;
        }

        if (!TryGetExplorerSelection(foregroundWindow, out var selectedPath, out var files) || string.IsNullOrWhiteSpace(selectedPath))
        {
            return false;
        }

        var selectedIndex = files.FindIndex(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0)
        {
            files.Insert(0, selectedPath);
            selectedIndex = 0;
        }

        context = new PreviewSelectionContext(files, selectedIndex, TryGetSelectedItemRect(foregroundWindow) ?? GetWindowRect(foregroundWindow));
        return true;
    }

    public bool CanTriggerPreview()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        return foregroundWindow != IntPtr.Zero
            && IsExplorerWindow(foregroundWindow)
            && !IsTextInputFocused(foregroundWindow);
    }

    public bool TryGetSelectedFile(out string? filePath, out ScreenRect anchorRect)
    {
        if (TryGetSelectionContext(out var context) && context is not null)
        {
            filePath = context.SelectedFilePath;
            anchorRect = context.AnchorRect;
            return true;
        }

        filePath = null;
        anchorRect = default;
        return false;
    }

    private static bool IsExplorerWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetExplorerSelection(IntPtr hwnd, out string? selectedFilePath, out List<string> files)
    {
        selectedFilePath = null;
        files = [];

        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
        {
            return false;
        }

        dynamic? shell = null;

        try
        {
            shell = Activator.CreateInstance(shellType);
            var windows = shell?.Windows();
            if (windows is null)
            {
                return false;
            }

            foreach (var window in windows)
            {
                try
                {
                    if ((IntPtr)(int)window.HWND != hwnd)
                    {
                        continue;
                    }

                    var document = window.Document;
                    var selectedItems = document?.SelectedItems();
                    if (selectedItems is null || selectedItems.Count != 1)
                    {
                        return false;
                    }

                    foreach (var item in selectedItems)
                    {
                        dynamic selectedItem = item;
                        if (selectedItem?.Path is string path)
                        {
                            selectedFilePath = path;
                        }

                        break;
                    }

                    if (string.IsNullOrWhiteSpace(selectedFilePath) || !File.Exists(selectedFilePath))
                    {
                        return false;
                    }

                    var folder = document?.Folder;
                    var folderItems = folder?.Items();
                    if (folderItems is not null)
                    {
                        foreach (var item in folderItems)
                        {
                            var path = item.Path as string;
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                files.Add(path);
                            }
                        }
                    }

                    if (files.Count == 0)
                    {
                        files.Add(selectedFilePath);
                    }

                    return true;
                }
                catch
                {
                    continue;
                }
            }
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        return false;
    }

    private static ScreenRect? TryGetSelectedItemRect(IntPtr hwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root is null)
            {
                return null;
            }

            var selectedCondition = new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true);
            var listItemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem);
            var combined = new AndCondition(listItemCondition, selectedCondition);
            var selectedItem = root.FindFirst(TreeScope.Descendants, combined);
            if (selectedItem is null)
            {
                return null;
            }

            var rect = selectedItem.Current.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return null;
            }

            return new ScreenRect(rect.Left, rect.Top, Math.Min(SelectionAnchorWidth, rect.Width), rect.Height);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTextInputFocused(IntPtr hwnd)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null)
            {
                return false;
            }

            if (focused.Current.NativeWindowHandle != (int)hwnd)
            {
                var parentWindow = TreeWalker.ControlViewWalker.GetParent(focused);
                while (parentWindow is not null)
                {
                    if (parentWindow.Current.NativeWindowHandle == (int)hwnd)
                    {
                        break;
                    }

                    parentWindow = TreeWalker.ControlViewWalker.GetParent(parentWindow);
                }

                if (parentWindow is null)
                {
                    return false;
                }
            }

            var controlType = focused.Current.ControlType;
            return controlType == ControlType.Edit || controlType == ControlType.Document;
        }
        catch
        {
            return false;
        }
    }

    private static ScreenRect GetWindowRect(IntPtr hwnd)
    {
        NativeMethods.GetWindowRect(hwnd, out var rect);
        return new ScreenRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}