// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Threading;
using System.Timers;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleEx
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var system = new ConsoleWindowSystem
			{
				TopStatus = "TOP STATUS BAR",
				BottomStatus = "BOTTOM STATUS BAR"
			};

			var window1 = new Window(system, (window) =>
			{
				window.KeyPressed += (sender, e) =>
				{
					if (e.KeyInfo.Key == ConsoleKey.Escape)
					{
						window.Close();
					}
				};
			})
			{
				Title = "Window 1",
				Left = 2,
				Top = 2,
				Width = 40,
				Height = 15
			};

			var window2 = new Window(system)
			{
				Title = "System Info",
				Left = 20,
				Top = 10,
				Width = 60,
				Height = 15
			};

			system.AddWindow(window1);
			system.AddWindow(window2);

			// Example of creating window with Figlet content and it's own thread
			system.CreateWindow("Clock", 60, 10, Console.WindowWidth - 60, 1, (window) =>
			{
				FigletContent figletContent = new FigletContent($"{DateTime.Now:HH:mm:ss}", Color.Red, Justify.Left);
				window.AddContent(figletContent);

				while (true)
				{
					figletContent.SetText($"{DateTime.Now:HH:mm:ss}");
					Thread.Sleep(1000);
				}
			});

			// Example of writing to a window from another thread
			Task.Run(() =>
			{
				for (var i = 0; i < 30; i++)
				{
					window1.AddContent(new MarkupContent(new List<string>() { $"Message [bold blue]{i}[/] from thread-Message [bold blue]{i}[/] from thread" }, true));
					Thread.Sleep(50);
				}
				system.Restore(window1);
			});

			MarkupContent systemInfoContent = new MarkupContent(GetSystemInfo(), false);
			window2.AddContent(systemInfoContent);

			// Set up a timer to update system info in window2 at regular intervals
			System.Threading.Timer _timer = new System.Threading.Timer(UpdateSystemInfo, new { Window = window2, SystemInfoContent = systemInfoContent }, 0, 5000);

			system.SetActiveWindow(window1);

			system.Run();
		}

		private static void UpdateSystemInfo(object? state)
		{
			dynamic? data = state;
			var window = data?.Window as Window;
			var systemInfoContent = data?.SystemInfoContent as MarkupContent;

			if (window != null)
			{
				systemInfoContent?.SetMarkup(GetSystemInfo());
			}
		}

		private static List<string> GetSystemInfo()
		{
			var systemInfo = new List<string>();

			// CPU usage
			systemInfo.Add($"[bold yellow]CPU Usage:[/] [bold green]{new Random().Next(0, 100)}%[/]");

			// Memory usage
			systemInfo.Add($"[bold yellow]Available Memory:[/] [bold green]{new Random().Next(0, 8000)} MB[/]");

			// Disk usage
			systemInfo.Add($"[bold yellow]Disk Free Space:[/] [bold green]{new Random().Next(1, 100)}%[/]");

			return systemInfo;
		}
	}
}
