﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Spectre.Console;
using SharpConsoleUI;

namespace SharpConsoleUI.Example
{
	public class LogWindow
	{
		private Window _window;

		public LogWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Title = "Log",
				Left = 6,
				Top = 6,
				Width = 40,
				Height = 10,
				BackgroundColor = Color.Grey35
			};

			consoleWindowSystem.AddWindow(_window);

			_window.KeyPressed += KeyPressed;
		}

		public Window Window => _window;

		public void AddLog(string log)
		{
			_window.AddControl(new MarkupControl(new List<string>() { log }) { Alignment = Alignment.Center });
		}

		public void KeyPressed(object? sender, KeyPressedEventArgs e)
		{
			if (sender == null) return;

			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				e.Handled = true;
				_window.Close();
			}
		}

		public void WindowThread(Window window)
		{
		}
	}
}