﻿// -----------------------------------------------------------------------
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

			consoleWindowSystem.SetActiveWindow(logWindow.Window);

			return consoleWindowSystem.Run().exitCode;
		}
	}
}
