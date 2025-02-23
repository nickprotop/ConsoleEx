using ConsoleEx.Contents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx
{
	public class SystemInfoWindow
	{
		private MarkupContent? _systemInfoContent;
		private Window _window;
		public Window Window => _window;

		public SystemInfoWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Title = "System Info",
				Left = 12,
				Top = 4,
				Width = 40,
				Height = 10
			};

			consoleWindowSystem.AddWindow(_window);
		}

		public void WindowThread(Window window)
		{
			_systemInfoContent = new MarkupContent(GetSystemInfo());
			window.AddContent(_systemInfoContent);

			while (true)
			{
				_systemInfoContent.SetContent(GetSystemInfo());
				Thread.Sleep(2000);
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
