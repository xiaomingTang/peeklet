using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Peeklet.Services;

public enum NativeTrayIconBalloonIcon : uint
{
	None = 0,
	Info = 1,
	Warning = 2,
	Error = 3
}

public sealed class NativeTrayIcon : IDisposable
{
	private const int WmApp = 0x8000;
	private const int WmCommand = 0x0111;
	private const int WmContextMenu = 0x007B;
	private const int WmLButtonDblClk = 0x0203;
	private const int WmRButtonUp = 0x0205;
	private const int NinSelect = 0x0400;
	private const int NinKeySelect = 0x0401;
	private const uint NifMessage = 0x00000001;
	private const uint NifIcon = 0x00000002;
	private const uint NifTip = 0x00000004;
	private const uint NifInfo = 0x00000010;
	private const uint NifShowTip = 0x00000080;
	private const uint NimAdd = 0x00000000;
	private const uint NimModify = 0x00000001;
	private const uint NimDelete = 0x00000002;
	private const uint NimSetVersion = 0x00000004;
	private const uint NotifyIconVersion4 = 4;
	private const uint MfString = 0x00000000;
	private const uint MfSeparator = 0x00000800;
	private const uint TpmBottomAlign = 0x0020;
	private const uint TpmLeftAlign = 0x0000;
	private const uint TpmRightButton = 0x0002;
	private const int MenuOpenId = 1001;
	private const int MenuExitId = 1002;
	private static readonly uint TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

	private readonly Action _showWindow;
	private readonly Action _exitApplication;
	private readonly Icon _icon;
	private readonly HwndSource _window;
	private readonly uint _callbackMessage;
	private bool _disposed;

	public NativeTrayIcon(string tooltip, Icon icon, Action showWindow, Action exitApplication)
	{
		_showWindow = showWindow;
		_exitApplication = exitApplication;
		_icon = icon;
		_callbackMessage = WmApp + 1;

		var parameters = new HwndSourceParameters("PeekletTrayIcon")
		{
			Width = 0,
			Height = 0,
			WindowStyle = 0,
			ParentWindow = nint.Zero
		};

		_window = new HwndSource(parameters);
		_window.AddHook(WndProc);

		AddIcon(tooltip);
	}

	public void ShowBalloonTip(string title, string text, NativeTrayIconBalloonIcon icon, uint timeoutMilliseconds)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var data = CreateNotifyIconData();
		data.uFlags = NifInfo;
		data.uTimeoutOrVersion = timeoutMilliseconds;
		data.dwInfoFlags = (uint)icon;
		data.szInfoTitle = title;
		data.szInfo = text;
		ShellNotifyIcon(NimModify, ref data);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		var data = CreateNotifyIconData();
		ShellNotifyIcon(NimDelete, ref data);

		_window.RemoveHook(WndProc);
		_window.Dispose();
		_disposed = true;
	}

	private void AddIcon(string tooltip)
	{
		var data = CreateNotifyIconData();
		data.uFlags = NifMessage | NifIcon | NifTip | NifShowTip;
		data.szTip = tooltip;
		ShellNotifyIcon(NimAdd, ref data);
		data.uTimeoutOrVersion = NotifyIconVersion4;
		ShellNotifyIcon(NimSetVersion, ref data);
	}

	private NotifyIconData CreateNotifyIconData()
	{
		return new NotifyIconData
		{
			cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
			hWnd = _window.Handle,
			uID = 1,
			uCallbackMessage = _callbackMessage,
			hIcon = _icon.Handle
		};
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == TaskbarCreatedMessage)
		{
			AddIcon("Peeklet");
			handled = true;
			return IntPtr.Zero;
		}

		if (msg == _callbackMessage)
		{
			var notificationCode = LowWord(lParam);

			switch (notificationCode)
			{
				case WmLButtonDblClk:
				case NinSelect:
				case NinKeySelect:
					_showWindow();
					handled = true;
					break;
				case WmContextMenu:
				case WmRButtonUp:
					ShowContextMenu(hwnd);
					handled = true;
					break;
			}
		}

		if (msg == WmCommand)
		{
			switch (LowWord(wParam))
			{
				case MenuOpenId:
					_showWindow();
					handled = true;
					break;
				case MenuExitId:
					_exitApplication();
					handled = true;
					break;
			}
		}

		return IntPtr.Zero;
	}

	private static void ShowContextMenu(IntPtr hwnd)
	{
		var menu = CreatePopupMenu();
		if (menu == IntPtr.Zero)
		{
			return;
		}

		try
		{
			AppendMenu(menu, MfString, MenuOpenId, "Open");
			AppendMenu(menu, MfSeparator, 0, string.Empty);
			AppendMenu(menu, MfString, MenuExitId, "Exit");

			GetCursorPos(out var point);
			SetForegroundWindow(hwnd);
			TrackPopupMenu(menu, TpmLeftAlign | TpmBottomAlign | TpmRightButton, point.X, point.Y, 0, hwnd, IntPtr.Zero);
		}
		finally
		{
			DestroyMenu(menu);
		}
	}

	private static int LowWord(IntPtr value)
	{
		return unchecked((short)(value.ToInt64() & 0xffff));
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "Shell_NotifyIconW")]
	private static extern bool ShellNotifyIcon(uint dwMessage, ref NotifyIconData lpData);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "AppendMenuW")]
	private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string lpNewItem);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr CreatePopupMenu();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyMenu(IntPtr hMenu);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool GetCursorPos(out NativePoint lpPoint);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint RegisterWindowMessage(string lpString);

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct NotifyIconData
	{
		public uint cbSize;
		public IntPtr hWnd;
		public uint uID;
		public uint uFlags;
		public uint uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;
		public uint dwState;
		public uint dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;
		public uint uTimeoutOrVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;
		public uint dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	}
}