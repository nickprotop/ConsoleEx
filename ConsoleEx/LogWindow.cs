using ConsoleEx.Contents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx
{
	public class LogWindow
	{
		private Window _window;

		public Window Window => _window;

		public LogWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Title = "Log",
				Left = 2,
				Top = 2,
				Width = 40,
				Height = 10
			};

			consoleWindowSystem.AddWindow(_window);

			_window.KeyPressed += KeyPressed;
		}

		public void WindowThread(Window window)
		{
		}

		public void AddLog(string log)
		{
			_window.AddContent(new MarkupContent(new List<string>() { log }) { Alignment = Alignment.Center });
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
	}
}
