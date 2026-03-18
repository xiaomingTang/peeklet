using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Peeklet.Services;
using Application = System.Windows.Application;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;

namespace Peeklet;

public partial class App : Application
{
	private static readonly Uri TrayIconUri = new("pack://application:,,,/Assets/peeklet-icon.ico", UriKind.Absolute);
	private const string SingleInstanceMutexName = @"Local\Peeklet.SingleInstance";
	private const string ShowStatusWindowEventName = @"Local\Peeklet.ShowStatusWindow";

	private GlobalKeyboardHook? _keyboardHook;
	private PreviewController? _previewController;
	private NotifyIcon? _notifyIcon;
	private StatusWindow? _statusWindow;
	private Mutex? _singleInstanceMutex;
	private bool _ownsSingleInstanceMutex;
	private EventWaitHandle? _showStatusWindowEvent;
	private RegisteredWaitHandle? _showStatusWindowRegistration;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		if (!TryAcquireSingleInstance())
		{
			Shutdown();
			return;
		}

		StartupRegistrationService.EnsureRegistered();
		StartupRegistrationService.EnsureUninstallRegistered();

		_previewController = new PreviewController();
		_ = WebViewEnvironmentProvider.PreloadAsync();
		_keyboardHook = new GlobalKeyboardHook();
		_keyboardHook.SpacePressed += KeyboardHook_SpacePressed;
		_keyboardHook.LeftPressed += KeyboardHook_LeftPressed;
		_keyboardHook.RightPressed += KeyboardHook_RightPressed;
		_keyboardHook.EscapePressed += KeyboardHook_EscapePressed;
		_keyboardHook.Install();

		_notifyIcon = new NotifyIcon
		{
			Icon = LoadTrayIcon(),
			Visible = true,
			Text = "Peeklet"
		};
		_notifyIcon.DoubleClick += (_, _) => ShowStatusWindow();

		var menu = new ContextMenuStrip();
		menu.Items.Add("Open", null, (_, _) => ShowStatusWindow());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("Exit", null, (_, _) => Shutdown());
		_notifyIcon.ContextMenuStrip = menu;

		if (Debugger.IsAttached)
		{
			_notifyIcon.BalloonTipTitle = "Peeklet";
			_notifyIcon.BalloonTipText = "Debug mode is running in the tray. Select a file in Explorer and press Space.";
			_notifyIcon.ShowBalloonTip(2500);
		}

		if (!e.Args.Contains(AppLaunchArguments.Background, StringComparer.OrdinalIgnoreCase))
		{
			ShowStatusWindow();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (_statusWindow is not null && _statusWindow.IsVisible)
		{
			_statusWindow.Close();
		}

		_keyboardHook?.Dispose();
		_showStatusWindowRegistration?.Unregister(null);
		_showStatusWindowEvent?.Dispose();

		if (_singleInstanceMutex is not null)
		{
			if (_ownsSingleInstanceMutex)
			{
				_singleInstanceMutex.ReleaseMutex();
			}

			_singleInstanceMutex.Dispose();
		}

		if (_notifyIcon is not null)
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}

		base.OnExit(e);
	}

	private bool TryAcquireSingleInstance()
	{
		_showStatusWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowStatusWindowEventName);
		_singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
		_ownsSingleInstanceMutex = createdNew;

		if (createdNew)
		{
			_showStatusWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
				_showStatusWindowEvent,
				static (state, _) => ((App)state!).ShowStatusWindow(),
				this,
				Timeout.Infinite,
				executeOnlyOnce: false);
			return true;
		}

		_showStatusWindowEvent.Set();
		_singleInstanceMutex.Dispose();
		_singleInstanceMutex = null;
		return false;
	}

	private void KeyboardHook_SpacePressed(object? sender, EventArgs e)
	{
		if (_previewController is null)
		{
			return;
		}

		QueuePreviewAction(_previewController.ToggleFromExplorerSelectionAsync);
	}

	private void KeyboardHook_LeftPressed(object? sender, EventArgs e)
	{
		if (_previewController is null || !_previewController.CanHandlePreviewHotkeys())
		{
			return;
		}

		QueuePreviewAction(_previewController.ShowPreviousAsync);
	}

	private void KeyboardHook_RightPressed(object? sender, EventArgs e)
	{
		if (_previewController is null || !_previewController.CanHandlePreviewHotkeys())
		{
			return;
		}

		QueuePreviewAction(_previewController.ShowNextAsync);
	}

	private void KeyboardHook_EscapePressed(object? sender, EventArgs e)
	{
		if (_previewController is null || !_previewController.CanHandlePreviewHotkeys())
		{
			return;
		}

		_previewController.ClosePreview();
	}

	private void QueuePreviewAction(Func<Task> action)
	{
		Dispatcher.BeginInvoke(async () =>
		{
			try
			{
				await action();
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		});
	}

	private void ShowStatusWindow()
	{
		if (!Dispatcher.CheckAccess())
		{
			_ = Dispatcher.BeginInvoke(ShowStatusWindow);
			return;
		}

		if (_statusWindow is null || !_statusWindow.IsLoaded)
		{
			_statusWindow = new StatusWindow();
			_statusWindow.Closed += (_, _) => _statusWindow = null;
		}

		if (!_statusWindow.IsVisible)
		{
			_statusWindow.Show();
		}

		if (_statusWindow.WindowState == WindowState.Minimized)
		{
			_statusWindow.WindowState = WindowState.Normal;
		}

		_statusWindow.Activate();
		_statusWindow.Topmost = true;
		_statusWindow.Topmost = false;
		_statusWindow.Focus();
	}

	private static Icon LoadTrayIcon()
	{
		try
		{
			var resourceStream = GetResourceStream(TrayIconUri);
			if (resourceStream?.Stream is null)
			{
				return (Icon)SystemIcons.Application.Clone();
			}

			using var stream = resourceStream.Stream;
			using var icon = new Icon(stream);
			return (Icon)icon.Clone();
		}
		catch
		{
			return (Icon)SystemIcons.Application.Clone();
		}
	}
}