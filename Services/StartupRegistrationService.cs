using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

namespace Peeklet.Services;

internal static class StartupRegistrationService
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "Peeklet";

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

		var command = Quote(executablePath);

		using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
			?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

		var currentValue = runKey?.GetValue(ValueName) as string;
		if (string.Equals(currentValue, command, StringComparison.Ordinal))
		{
			return;
		}

		runKey?.SetValue(ValueName, command, RegistryValueKind.String);
	}

	private static string Quote(string path)
	{
		return string.Concat('"', path, '"');
	}
}