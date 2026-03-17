using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Peeklet.Services;

public sealed class GlobalKeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _hookCallback;
    private IntPtr _hookHandle;

    public event EventHandler? SpacePressed;

    public event EventHandler? LeftPressed;

    public event EventHandler? RightPressed;

    public event EventHandler? EscapePressed;

    public GlobalKeyboardHook()
    {
        _hookCallback = HookCallback;
    }

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookCallback, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install keyboard hook.");
        }
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == NativeMethods.WM_KEYDOWN)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var key = KeyInterop.KeyFromVirtualKey((int)info.vkCode);
            if (key == Key.Space && !HasModifierKeysPressed())
            {
                SpacePressed?.Invoke(this, EventArgs.Empty);
            }
            else if (key == Key.Left)
            {
                LeftPressed?.Invoke(this, EventArgs.Empty);
            }
            else if (key == Key.Right)
            {
                RightPressed?.Invoke(this, EventArgs.Empty);
            }
            else if (key == Key.Escape)
            {
                EscapePressed?.Invoke(this, EventArgs.Empty);
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private static bool HasModifierKeysPressed()
    {
        return IsKeyPressed(NativeMethods.VK_SHIFT)
            || IsKeyPressed(NativeMethods.VK_CONTROL)
            || IsKeyPressed(NativeMethods.VK_MENU)
            || IsKeyPressed(NativeMethods.VK_LWIN)
            || IsKeyPressed(NativeMethods.VK_RWIN);
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (NativeMethods.GetKeyState(virtualKey) & 0x8000) != 0;
    }
}