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
using System.Reflection.Metadata.Ecma335;

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

			var logWindow = new LogWindow(consoleWindowSystem);
			var systemInfoWindow = new SystemInfoWindow(consoleWindowSystem);
			var userInfoWindow = new UserInfoWindow(consoleWindowSystem);
			var clockWindow = new ClockWindow(consoleWindowSystem);

			// Example of writing to a window from another thread
			Task.Run(() =>
			{
				for (var i = 0; i < 30; i++)
				{
					logWindow.AddLog($"{DateTime.Now.ToString("g")}: Message [blue]{i}[/] from main thread. Output status: [yellow]{i * i}[/] from thread");
					Thread.Sleep(500);
				}
			});

			try
			{
				bool quit = false;
				int exitCode = 0;

				consoleWindowSystem.SetActiveWindow(logWindow.Window);

				Task.Run(() =>
				{
					exitCode = consoleWindowSystem.Run().exitCode;
					quit = true;
				});

				consoleWindowSystem.ShowNotification("Notification", "Welcome to ConsoleEx example application\nPress Ctrl-Q to quit");

				while (!quit)
				{
				}

				return exitCode;
			}
			catch (Exception ex)
			{
				Console.Clear();
				AnsiConsole.WriteException(ex);
				Console.WriteLine(string.Empty);
				Console.CursorVisible = true;
				return 1;
			}
		}
	}
}
