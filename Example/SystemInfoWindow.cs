// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI;

namespace SharpConsoleUI.Example
{
	public class SystemInfoWindow
	{
		private MarkupControl? _systemInfoContent;
		private Window _window;

		public SystemInfoWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Title = "System Info",
				Left = 8,
				Top = 8,
				Width = 40,
				Height = 10
			};

			consoleWindowSystem.AddWindow(_window);
		}

		public Window Window => _window;

		public async void WindowThread(Window window)
		{
			_systemInfoContent = new MarkupControl(GetSystemInfo());
			window.AddContent(_systemInfoContent);

			while (true)
			{
				_systemInfoContent.SetContent(GetSystemInfo());
				await Task.Delay(3000);
			}
		}

		private static List<string> GetSystemInfo()
		{
			var systemInfo = new List<string>();

			// CPU usage
			systemInfo.Add($"[yellow]CPU Usage:[/] [green]{new Random().Next(0, 100)}%[/]");

			// Memory usage
			systemInfo.Add($"[yellow]Available Memory:[/] [green]{new Random().Next(0, 8000)} MB[/]");

			// Disk usage
			systemInfo.Add($"[yellow]Disk Free Space:[/] [green]{new Random().Next(1, 100)}%[/]");

			return systemInfo;
		}
	}
}