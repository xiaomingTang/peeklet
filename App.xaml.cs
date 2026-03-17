using System.Drawing;
using System.Diagnostics;
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

	private GlobalKeyboardHook? _keyboardHook;
	private PreviewController? _previewController;
	private NotifyIcon? _notifyIcon;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		StartupRegistrationService.EnsureRegistered();

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

		var menu = new ContextMenuStrip();
		menu.Items.Add("Open Peeklet", null, async (_, _) => await _previewController.ShowFromExplorerSelectionAsync());
		menu.Items.Add("Exit", null, (_, _) => Shutdown());
		_notifyIcon.ContextMenuStrip = menu;
		_notifyIcon.DoubleClick += async (_, _) => await _previewController.ShowFromExplorerSelectionAsync();

		if (Debugger.IsAttached)
		{
			_notifyIcon.BalloonTipTitle = "Peeklet";
			_notifyIcon.BalloonTipText = "Debug mode is running in the tray. Select a file in Explorer and press Space.";
			_notifyIcon.ShowBalloonTip(2500);
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_keyboardHook?.Dispose();

		if (_notifyIcon is not null)
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}

		base.OnExit(e);
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