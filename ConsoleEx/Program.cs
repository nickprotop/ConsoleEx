// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Contents;
using Spectre.Console;
using ConsoleEx.Themes;

namespace ConsoleEx
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			var consoleWindowSystem = new ConsoleWindowSystem
			{
				TopStatus = "ConsoleEx example application",
				BottomStatus = "Ctrl-Q Quit",
				RenderMode = RenderMode.Direct,
				Theme = new Theme()
				{
					DesktopBackroundChar = '.',
					DesktopBackgroundColor = Color.Black,
					DesktopForegroundColor = Color.Grey,
				}
			};

			var window1 = new Window(consoleWindowSystem,
			(window) =>
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
				Height = 10
			};

			consoleWindowSystem.AddWindow(window1);

			var systemInfoWindow = new SystemInfoWindow(consoleWindowSystem);
			var userInfoWindow = new UserInfoWindow(consoleWindowSystem);

			// Example of creating window with Figlet content and it's own thread
			consoleWindowSystem.AddWindow(new Window(consoleWindowSystem, (window) =>
			{
				FigletContent figletContent = new FigletContent()
				{
					Text = $"{DateTime.Now:HH:mm:ss}",
					Alignment = Alignment.Center,
					Color = Color.Green
				};
				window.AddContent(figletContent);

				while (true)
				{
					figletContent.SetText($"{DateTime.Now:HH:mm:ss}");
					Thread.Sleep(1000);
				}
			})
			{
				Title = "Clock",
				Left = Console.WindowWidth - 70,
				Top = 1,
				Width = 70,
				Height = 10,
				BackgroundColor = Color.Black
			});

			// Example of writing to a window from another thread
			Task.Run(() =>
			{
				for (var i = 0; i < 30; i++)
				{
					window1.AddContent(new MarkupContent(new List<string>() { $"Message [blue]{i}[/] from thread-Message [blue]{i}[/] from thread" }) { Alignment = Alignment.Center });
					Thread.Sleep(50);
				}
			});

			consoleWindowSystem.SetActiveWindow(window1);

			return consoleWindowSystem.Run().exitCode;
		}
	}
}