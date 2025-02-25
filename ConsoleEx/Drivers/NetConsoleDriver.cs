using ConsoleEx.Helpers;
using ConsoleEx.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Drivers
{
	public enum RenderMode
	{
		Direct,
		Buffer
	}

	public class NetConsoleDriver : IConsoleDriver
	{
		private int _lastConsoleHeight;
		private int _lastConsoleWidth;
		private ConsoleWindowSystem? _consoleWindowSystem;
		private RenderMode _renderMode { get; set; } = RenderMode.Direct;
		private bool _running = false;

		public Size ScreenSize => new Size(Console.WindowWidth, Console.WindowHeight);

		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		public event EventHandler<Size>? ScreenResized;

		public void Initialize(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;

			Console.OutputEncoding = Encoding.UTF8;
		}

		public void Start()
		{
			_running = true;

			Console.CursorVisible = false;

			_lastConsoleWidth = Console.WindowWidth;
			_lastConsoleHeight = Console.WindowHeight;

			var inputTask = Task.Run(InputLoop);
			var resizeTask = Task.Run(ResizeLoop);
		}

		public void Exit()
		{
			_running = false;

			Console.Clear();

			Console.CursorVisible = true;
		}

		public void Clear()
		{
			Console.Clear();
		}

		public void WriteToConsole(int x, int y, string value)
		{
			switch (_renderMode)
			{
				case RenderMode.Direct:
					Console.SetCursorPosition(x, y);
					Console.Write(value);
					break;
			}
		}

		private void InputLoop()
		{
			while (_running == true)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true);
					KeyPressed?.Invoke(this, key);
				}
				Thread.Sleep(10);
			}
		}

		private void ResizeLoop()
		{
			while (_running == true)
			{
				if (Console.WindowWidth != _lastConsoleWidth || Console.WindowHeight != _lastConsoleHeight)
				{
					ScreenResized?.Invoke(this, ScreenSize);

					_lastConsoleWidth = Console.WindowWidth;
					_lastConsoleHeight = Console.WindowHeight;
				}
				Thread.Sleep(50);
			}
		}
	}
}
