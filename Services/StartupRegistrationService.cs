using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

namespace Peeklet.Services;

internal static class StartupRegistrationService
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Peeklet";
	private const string ValueName = "Peeklet";
	private const string ProductName = "Peeklet";
	private const string Publisher = "Peeklet";
	private const string Version = "1.0.0";
	private const string UninstallerFileName = "Peeklet.Uninstaller.exe";

	public static void EnsureRegistered()
	{
		if (Debugger.IsAttached)
		{
			return;
		}

		var processPath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(processPath))
		{
			return;
		}

		var executablePath = processPath;
		if (!File.Exists(executablePath))
		{
			return;
		}

		var command = $"{Quote(executablePath)} {AppLaunchArguments.Background}";

		using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
			?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

		var currentValue = runKey?.GetValue(ValueName) as string;
		if (string.Equals(currentValue, command, StringComparison.Ordinal))
		{
			return;
		}

		runKey?.SetValue(ValueName, command, RegistryValueKind.String);
	}

	public static void EnsureUninstallRegistered()
	{
		if (Debugger.IsAttached)
		{
			return;
		}

		var processPath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(processPath))
		{
			return;
		}

		var executablePath = processPath;
		if (!File.Exists(executablePath))
		{
			return;
		}

		var installDirectory = Path.GetDirectoryName(executablePath);
		if (string.IsNullOrWhiteSpace(installDirectory))
		{
			return;
		}

		var uninstallerPath = Path.Combine(installDirectory, UninstallerFileName);
		if (!File.Exists(uninstallerPath))
		{
			return;
		}

		using var uninstallKey = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, writable: true);
		if (uninstallKey is null)
		{
			return;
		}

		var uninstallCommand = Quote(uninstallerPath);
		uninstallKey.SetValue("DisplayName", ProductName, RegistryValueKind.String);
		uninstallKey.SetValue("Publisher", Publisher, RegistryValueKind.String);
		uninstallKey.SetValue("DisplayVersion", Version, RegistryValueKind.String);
		uninstallKey.SetValue("DisplayIcon", executablePath, RegistryValueKind.String);
		uninstallKey.SetValue("InstallLocation", installDirectory, RegistryValueKind.String);
		uninstallKey.SetValue("UninstallString", uninstallCommand, RegistryValueKind.String);
		uninstallKey.SetValue("QuietUninstallString", uninstallCommand, RegistryValueKind.String);
		uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
		uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
	}

	public static void RemoveRegistration()
	{
		using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
		runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
	}

	public static void RemoveUninstallRegistration()
	{
		Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
	}

	private static string Quote(string path)
	{
		return string.Concat('"', path, '"');
	}
}