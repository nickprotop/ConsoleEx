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
using ConsoleEx.Services;

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

			try
			{
				bool quit = false;
				int exitCode = 0;

				consoleWindowSystem.SetActiveWindow(logWindow.Window);

				// Run the console window system in a separate thread on the background
				Task.Run(() =>
				{
					exitCode = consoleWindowSystem.Run().exitCode;
					quit = true;
				});

				// Show a notification message and block UI
				Notifications.ShowNotification(consoleWindowSystem, "Notification", "Welcome to ConsoleEx example application\nPress Ctrl-Q to quit", NotificationSeverity.Info, true, 0);

				// Example of writing to a window from another thread
				Task.Run(() =>
				{
					for (var i = 0; i < 30; i++)
					{
						logWindow.AddLog($"{DateTime.Now.ToString("g")}: Message [blue]{i}[/] from main thread. Output status: [yellow]{i * i}[/] from thread");
						Thread.Sleep(500);
					}
				});

				// Wait for the console window system to finish
				while (!quit) { }

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