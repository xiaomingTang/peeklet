using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;

const string ProductName = "Peeklet";
const string StartupValueName = "Peeklet";
const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Peeklet";
const string MainExecutableName = "Peeklet.exe";
const string UninstallerExecutableName = "Peeklet.Uninstaller.exe";
const string ConfirmedArgument = "--confirmed";

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

var baseDirectory = AppContext.BaseDirectory;
var mainExecutablePath = Path.Combine(baseDirectory, MainExecutableName);
var uninstallerPath = Path.Combine(baseDirectory, UninstallerExecutableName);
var isConfirmed = args.Contains(ConfirmedArgument, StringComparer.OrdinalIgnoreCase);

if (!File.Exists(uninstallerPath) || !File.Exists(mainExecutablePath))
{
	MessageBox.Show(
		"当前目录不包含完整的 Peeklet 安装文件，已取消卸载。",
		"卸载 Peeklet",
		MessageBoxButtons.OK,
		MessageBoxIcon.Error);
	return;
}

if (!isConfirmed)
{
	var confirmResult = MessageBox.Show(
		$"这将关闭 {ProductName}、移除开机自启动，并删除以下目录中的所有文件：\n\n{baseDirectory}\n\n是否继续？",
		$"卸载 {ProductName}",
		MessageBoxButtons.YesNo,
		MessageBoxIcon.Warning,
		MessageBoxDefaultButton.Button2);

	if (confirmResult != DialogResult.Yes)
	{
		return;
	}
}

try
{
	RemoveStartupRegistration();
	RemoveUninstallRegistration();
	TerminateRunningProcesses(mainExecutablePath);
	ScheduleDirectoryDeletion(baseDirectory);

	MessageBox.Show(
		$"{ProductName} 已开始卸载。窗口关闭后会删除程序文件。",
		$"卸载 {ProductName}",
		MessageBoxButtons.OK,
		MessageBoxIcon.Information);
}
catch (Exception ex)
{
	MessageBox.Show(
		$"卸载失败：{ex.Message}",
		$"卸载 {ProductName}",
		MessageBoxButtons.OK,
		MessageBoxIcon.Error);
}

static void RemoveStartupRegistration()
{
	using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
	runKey?.DeleteValue(StartupValueName, throwOnMissingValue: false);
}

static void RemoveUninstallRegistration()
{
	Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
}

static void TerminateRunningProcesses(string mainExecutablePath)
{
	foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(mainExecutablePath)))
	{
		try
		{
			if (process.Id == Environment.ProcessId)
			{
				continue;
			}

			var processPath = process.MainModule?.FileName;
			if (!string.Equals(processPath, mainExecutablePath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			process.Kill(entireProcessTree: true);
			process.WaitForExit(5000);
		}
		catch
		{
			// Skip processes we cannot inspect or terminate.
		}
	}
}

static void ScheduleDirectoryDeletion(string targetDirectory)
{
	Environment.CurrentDirectory = Path.GetTempPath();

	var commandFilePath = Path.Combine(
		Path.GetTempPath(),
		$"peeklet-uninstall-{Guid.NewGuid():N}.cmd");

	var commandLines = new[]
	{
		"@echo off",
		"setlocal",
		"timeout /t 2 /nobreak >nul",
		$":retry",
		$"rmdir /s /q \"{targetDirectory}\"",
		$"if exist \"{targetDirectory}\" (",
		"  timeout /t 1 /nobreak >nul",
		"  goto retry",
		")",
		"del \"%~f0\""
	};

	File.WriteAllLines(commandFilePath, commandLines);

	Process.Start(new ProcessStartInfo
	{
		FileName = "cmd.exe",
		Arguments = $"/c \"{commandFilePath}\"",
		CreateNoWindow = true,
		UseShellExecute = false,
		WindowStyle = ProcessWindowStyle.Hidden,
		WorkingDirectory = Path.GetTempPath()
	});
}