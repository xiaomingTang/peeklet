using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Peeklet.Models;

namespace Peeklet.Services;

public sealed class ExplorerSelectionService
{
    private const double SelectionAnchorWidth = 230;
    private const double DesktopCursorAnchorWidth = 24;
    private const double DesktopCursorAnchorHeight = 24;
    private const string DesktopListViewClassName = "SysListView32";

    public bool TryGetSelectionContext(out PreviewSelectionContext? context)
    {
        context = null;

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || !IsExplorerWindow(foregroundWindow) || IsTextInputFocused(foregroundWindow))
        {
            return false;
        }

        if (TryGetExplorerSelection(foregroundWindow, out var selectedPath, out var files) && !string.IsNullOrWhiteSpace(selectedPath))
        {
            context = BuildSelectionContext(selectedPath, files, TryGetSelectedItemRect(foregroundWindow) ?? GetWindowRect(foregroundWindow));
            return true;
        }

        if (TryGetDesktopSelection(foregroundWindow, out selectedPath, out files, out var anchorRect) && !string.IsNullOrWhiteSpace(selectedPath))
        {
            context = BuildSelectionContext(selectedPath, files, anchorRect);
            return true;
        }

        return false;
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

                    if (selectedItems is not System.Collections.IEnumerable selectedItemsEnumerable)
                    {
                        return false;
                    }

                    foreach (var item in selectedItemsEnumerable)
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

    private static bool TryGetDesktopSelection(IntPtr hwnd, out string? selectedFilePath, out List<string> files, out ScreenRect anchorRect)
    {
        selectedFilePath = null;
        files = [];
        anchorRect = default;

        var desktopView = FindDesktopViewForCursor(hwnd);
        if (desktopView is null)
        {
            return false;
        }

        var listItemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem);
        var selectedItem = desktopView.SelectedItem;

        var allItems = desktopView.ListView.FindAll(TreeScope.Children, listItemCondition);
        if (allItems.Count == 0)
        {
            allItems = desktopView.ListView.FindAll(TreeScope.Descendants, listItemCondition);
        }

        if (allItems.Count == 0)
        {
            return false;
        }

        var desktopEntries = GetDesktopEntries();
        if (desktopEntries.Count == 0)
        {
            return false;
        }

        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < allItems.Count; index++)
        {
            var item = allItems[index];
            if (!TryResolveDesktopItemPath(item.Current.Name, desktopEntries, usedPaths, out var resolvedPath))
            {
                continue;
            }

            files.Add(resolvedPath);
            if (ReferenceEquals(item, selectedItem))
            {
                selectedFilePath = resolvedPath;
            }
        }

        if (string.IsNullOrWhiteSpace(selectedFilePath))
        {
            var selectedName = selectedItem.Current.Name;
            var selectedUsedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TryResolveDesktopItemPath(selectedName, desktopEntries, selectedUsedPaths, out selectedFilePath) || string.IsNullOrWhiteSpace(selectedFilePath))
            {
                return false;
            }

            if (!files.Contains(selectedFilePath, StringComparer.OrdinalIgnoreCase))
            {
                files.Insert(0, selectedFilePath);
            }
        }

        if (NativeMethods.GetCursorPos(out var cursor))
        {
            anchorRect = BuildDesktopAnchorRect(cursor, hwnd);
        }
        else
        {
            var rect = selectedItem.Current.BoundingRectangle;
            anchorRect = rect.Width > 0 && rect.Height > 0
                ? new ScreenRect(rect.Left, rect.Top, Math.Min(SelectionAnchorWidth, rect.Width), rect.Height)
                : GetWindowRect(hwnd);
        }

        return files.Count > 0;
    }

    private static ScreenRect BuildDesktopAnchorRect(NativeMethods.Point cursor, IntPtr hwnd)
    {
        var anchorLeft = cursor.X - (DesktopCursorAnchorWidth / 2);
        var anchorTop = cursor.Y - (DesktopCursorAnchorHeight / 2);

        var monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new NativeMethods.MonitorInfo
            {
                cbSize = Marshal.SizeOf<NativeMethods.MonitorInfo>()
            };

            if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
            {
                anchorLeft = Math.Clamp(anchorLeft, monitorInfo.rcWork.Left, monitorInfo.rcWork.Right - DesktopCursorAnchorWidth);
                anchorTop = Math.Clamp(anchorTop, monitorInfo.rcWork.Top, monitorInfo.rcWork.Bottom - DesktopCursorAnchorHeight);
            }
        }

        return new ScreenRect(anchorLeft, anchorTop, DesktopCursorAnchorWidth, DesktopCursorAnchorHeight);
    }

    private static PreviewSelectionContext BuildSelectionContext(string selectedPath, List<string> files, ScreenRect anchorRect)
    {
        var selectedIndex = files.FindIndex(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0)
        {
            files.Insert(0, selectedPath);
            selectedIndex = 0;
        }

        return new PreviewSelectionContext(files, selectedIndex, anchorRect);
    }

    private static DesktopViewCandidate? FindDesktopViewForCursor(IntPtr hwnd)
    {
        try
        {
            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return null;
            }

            var classNameCondition = new PropertyCondition(AutomationElement.ClassNameProperty, DesktopListViewClassName);
            var listViews = AutomationElement.RootElement.FindAll(TreeScope.Descendants, classNameCondition);
            DesktopViewCandidate? bestCandidate = null;

            for (var index = 0; index < listViews.Count; index++)
            {
                var listView = listViews[index];
                var candidate = TryBuildDesktopViewCandidate(listView, cursor);
                if (candidate is null)
                {
                    continue;
                }

                if (bestCandidate is null || candidate.CursorDistance < bestCandidate.CursorDistance)
                {
                    bestCandidate = candidate;
                    if (candidate.CursorDistance == 0)
                    {
                        break;
                    }
                }
            }

            return bestCandidate;
        }
        catch
        {
            return null;
        }
    }

    private static DesktopViewCandidate? TryBuildDesktopViewCandidate(AutomationElement listView, NativeMethods.Point cursor)
    {
        var listItemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem);
        var selectedCondition = new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true);
        var combinedCondition = new AndCondition(listItemCondition, selectedCondition);

        var selectedItems = listView.FindAll(TreeScope.Children, combinedCondition);
        if (selectedItems.Count == 0)
        {
            selectedItems = listView.FindAll(TreeScope.Descendants, combinedCondition);
        }

        if (selectedItems.Count != 1)
        {
            return null;
        }

        var selectedItem = selectedItems[0];
        var rect = selectedItem.Current.BoundingRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        return new DesktopViewCandidate(listView, selectedItem, GetDistanceToRect(cursor, rect.Left, rect.Top, rect.Right, rect.Bottom));
    }

    private static double GetDistanceToRect(NativeMethods.Point point, double left, double top, double right, double bottom)
    {
        var clampedX = Math.Clamp(point.X, left, right);
        var clampedY = Math.Clamp(point.Y, top, bottom);
        var deltaX = point.X - clampedX;
        var deltaY = point.Y - clampedY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static List<DesktopEntry> GetDesktopEntries()
    {
        var entries = new List<DesktopEntry>();
        AddDesktopEntries(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), entries);
        AddDesktopEntries(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), entries);
        return entries;
    }

    private static void AddDesktopEntries(string desktopPath, List<DesktopEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(desktopPath) || !Directory.Exists(desktopPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(desktopPath))
        {
            entries.Add(new DesktopEntry(filePath));
        }
    }

    private static bool TryResolveDesktopItemPath(string itemName, IReadOnlyList<DesktopEntry> entries, ISet<string> usedPaths, out string resolvedPath)
    {
        foreach (var entry in entries)
        {
            if (usedPaths.Contains(entry.Path) || !entry.Matches(itemName))
            {
                continue;
            }

            usedPaths.Add(entry.Path);
            resolvedPath = entry.Path;
            return true;
        }

        resolvedPath = string.Empty;
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

    private sealed record DesktopEntry(string Path)
    {
        private string FileName { get; } = System.IO.Path.GetFileName(Path);
        private string DisplayName { get; } = System.IO.Path.GetFileNameWithoutExtension(Path);

        public bool Matches(string itemName)
        {
            return string.Equals(itemName, FileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(itemName, DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record DesktopViewCandidate(AutomationElement ListView, AutomationElement SelectedItem, double CursorDistance);
}