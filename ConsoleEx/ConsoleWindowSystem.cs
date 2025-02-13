// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using static ConsoleEx.Window;

namespace ConsoleEx
{
	public class ConsoleWindowSystem
	{
		private readonly List<Window> _windows = new();
		private Window? _activeWindow = null;
		private bool _running;
		private readonly ConcurrentQueue<ConsoleKeyInfo> _inputQueue = new();
		private int _lastConsoleWidth;
		private int _lastConsoleHeight;

		public string TopStatus { get; set; } = "";
		public string BottomStatus { get; set; } = "";

		public Window AddWindow(Window window)
		{
			lock (_windows)
			{
				window.ZIndex = _windows.Count > 0 ? _windows.Max(w => w.ZIndex) + 1 : 0;
				_windows.Add(window);
				_activeWindow ??= window;
			}
			return window;
		}

		public void SetActiveWindow(Window window)
		{
			if (window == null || !_windows.Contains(window))
			{
				return;
			}

			lock (_windows)
			{
				_activeWindow?.Invalidate();

				_activeWindow = window;
				_windows.ForEach(w => w.IsActive = false);
				_activeWindow.IsActive = true;
				_activeWindow.ZIndex = _windows.Max(w => w.ZIndex) + 1;

				_activeWindow.Invalidate();
			}
		}

		public void Run()
		{
			_running = true;
			Console.CursorVisible = false;

			_lastConsoleWidth = Console.WindowWidth;
			_lastConsoleHeight = Console.WindowHeight;

			var inputThread = new Thread(InputLoop) { IsBackground = true };
			inputThread.Start();

			var resizeThread = new Thread(ResizeLoop) { IsBackground = true };
			resizeThread.Start();

			while (_running)
			{
				ProcessInput();
				UpdateDisplay();
				UpdateCursor();
				Thread.Sleep(50);
			}

			Console.CursorVisible = true;
		}

		public (int Left, int Top) DesktopUpperLeft
		{
			get
			{
				int left = 0;
				int top = !string.IsNullOrEmpty(TopStatus) ? 1 : 0; // Start below the top status if it exists
				return (left, top);
			}
		}

		public (int Right, int Bottom) DesktopBottomRight
		{
			get
			{
				int right = Console.WindowWidth - 1;
				int bottom = Console.WindowHeight - 1;

				if (!string.IsNullOrEmpty(TopStatus))
				{
					bottom -= 1; // Subtract one row for the top status
				}

				if (!string.IsNullOrEmpty(BottomStatus))
				{
					bottom -= 1; // Subtract one row for the bottom status
				}

				return (right, bottom);
			}
		}

		public (int Width, int Height) DesktopDimensions
		{
			get
			{
				int width = Console.WindowWidth;
				int height = Console.WindowHeight;

				if (!string.IsNullOrEmpty(TopStatus))
				{
					height -= 1; // Subtract one row for the top status
				}

				if (!string.IsNullOrEmpty(BottomStatus))
				{
					height -= 1; // Subtract one row for the bottom status
				}

				return (width, height);
			}
		}

		private void UpdateCursor()
		{
			if (_activeWindow != null && _activeWindow.HasInteractiveContent(out var cursorPosition))
			{
				var (absoluteLeft, absoluteTop) = TranslateToAbsolute(_activeWindow, cursorPosition.Left, cursorPosition.Top);

				// Check if the cursor position is within the console window boundaries
				if (absoluteLeft >= 0 && absoluteLeft < Console.WindowWidth &&
					absoluteTop >= 0 && absoluteTop < Console.WindowHeight &&
					// Check if the cursor position is within the active window's boundaries
					absoluteLeft + 1 >= _activeWindow.Left && absoluteLeft + 1 < _activeWindow.Left + _activeWindow.Width &&
					absoluteTop + 1 >= _activeWindow.Top && absoluteTop + 1 < _activeWindow.Top + _activeWindow.Height)
				{
					Console.CursorVisible = true;
					Console.SetCursorPosition(absoluteLeft, absoluteTop);
				}
				else
				{
					Console.CursorVisible = false;
				}
			}
			else
			{
				Console.CursorVisible = false;
			}
		}

		private void InputLoop()
		{
			while (_running)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true);
					_inputQueue.Enqueue(key);
				}
				Thread.Sleep(10);
			}
		}

		public void ShowWindow(Window window)
		{
			lock (window)
			{
				window.Show();
				window.IsVisible = true;
				window.Invalidate();
				Console.Clear();
				_windows.ForEach(w => w.Invalidate());
			}
		}

		public void HideWindow(Window window)
		{
			lock (window)
			{
				window.Hide();
				window.IsVisible = false;
				window.Invalidate();
				Console.Clear();
				_windows.ForEach(w => w.Invalidate());
			}
		}

		public void CloseWindow(Window window)
		{
			if (window == null) return;

			lock (_windows)
			{
				_windows.Remove(window);
				if (_activeWindow == window)
				{
					_activeWindow = _windows.FirstOrDefault();
					if (_activeWindow != null)
					{
						SetActiveWindow(_activeWindow);
					}
				}
				Console.Clear();
				_windows.ForEach(w => w.Invalidate());
			}
		}

		public Window CreateWindow(string title, WindowOptions options)
		{
			var window = new Window(this, options);

			AddWindow(window);
			_activeWindow = window;
			return window;
		}

		public Window CreateWindow(string title, WindowOptions options, IWIndowContent content)
		{
			var window = new Window(this, options);

			window.AddContent(content);
			AddWindow(window);
			_activeWindow = window;
			return window;
		}

		public Window CreateWindow(WindowOptions options, WindowThreadDelegateAsync windowThreadMethod)
		{
			var window = new Window(this, options, windowThreadMethod);

			AddWindow(window);
			_activeWindow = window;
			return window;
		}

		public Window CreateWindow(WindowOptions options, IWIndowContent content, WindowThreadDelegateAsync windowThreadMethod)
		{
			var window = new Window(this, options, windowThreadMethod);

			window.AddContent(content);
			AddWindow(window);
			_activeWindow = window;
			return window;
		}

		public Window CreateWindow(WindowOptions options, WindowThreadDelegate windowThreadMethod)
		{
			var window = new Window(this, options, windowThreadMethod);

			AddWindow(window);
			_activeWindow = window;
			return window;
		}

		public Window CreateWindow(WindowOptions options, IWIndowContent content, WindowThreadDelegate windowThreadMethod)
		{
			var window = new Window(this, options, windowThreadMethod);

			window.AddContent(content);
			AddWindow(window);
			_activeWindow = window;
			return window;
		}

		private void ResizeLoop()
		{
			while (_running)
			{
				var (desktopWidth, desktopHeight) = DesktopDimensions;

				if (Console.WindowWidth != _lastConsoleWidth || Console.WindowHeight != _lastConsoleHeight)
				{
					lock (_windows)
					{
						foreach (var window in _windows)
						{
							// Ensure the window is within the new width boundaries
							if (window.Left + window.Width > desktopWidth)
							{
								window.Left = Math.Max(0, desktopWidth - window.Width);
							}

							// Ensure the window is within the new height boundaries
							if (window.Top + window.Height > desktopHeight)
							{
								window.Top = Math.Max(1, desktopHeight - window.Height);
							}
						}
					}

					Console.Clear();
					_windows.ForEach(w => w.Invalidate());
					_lastConsoleWidth = Console.WindowWidth;
					_lastConsoleHeight = Console.WindowHeight;
				}
				Thread.Sleep(100);
			}
		}

		private void ProcessInput()
		{
			while (_inputQueue.TryDequeue(out var key))
			{
				lock (_windows)
				{
					if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.T)
					{
						CycleActiveWindow();
					}
					else if (_activeWindow != null)
					{
						bool handled = false;
						var (desktopWidth, desktopHeight) = DesktopDimensions;
						var (desktopLeft, desktopTop) = DesktopUpperLeft;
						var (desktopRight, desktopBottom) = DesktopBottomRight;

						if ((key.Modifiers & ConsoleModifiers.Shift) != 0 && _activeWindow.IsResizable)
						{
							// Handle resizing
							FillWindowBackground(_activeWindow);
							switch (key.Key)
							{
								case ConsoleKey.UpArrow:
									_activeWindow.Height = Math.Max(1, _activeWindow.Height - 1);
									handled = true;
									break;

								case ConsoleKey.DownArrow:
									_activeWindow.Height = Math.Min(desktopHeight - _activeWindow.Top, _activeWindow.Height + 1);
									handled = true;
									break;

								case ConsoleKey.LeftArrow:
									_activeWindow.Width = Math.Max(1, _activeWindow.Width - 1);
									handled = true;
									break;

								case ConsoleKey.RightArrow:
									_activeWindow.Width = Math.Min(desktopWidth - _activeWindow.Left, _activeWindow.Width + 1);
									handled = true;
									break;
							}
						}
						else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && _activeWindow.IsMovable)
						{
							// Handle moving
							FillWindowBackground(_activeWindow);
							switch (key.Key)
							{
								case ConsoleKey.UpArrow:
									_activeWindow.Top = Math.Max(0, _activeWindow.Top - 1);
									handled = true;
									break;

								case ConsoleKey.DownArrow:
									_activeWindow.Top = Math.Min(desktopBottom - _activeWindow.Height + 1, _activeWindow.Top + 1);
									handled = true;
									break;

								case ConsoleKey.LeftArrow:
									_activeWindow.Left = Math.Max(desktopLeft, _activeWindow.Left - 1);
									handled = true;
									break;

								case ConsoleKey.RightArrow:
									_activeWindow.Left = Math.Min(desktopRight - _activeWindow.Width + 1, _activeWindow.Left + 1);
									handled = true;
									break;

								case ConsoleKey.X:
									CloseWindow(_activeWindow);
									handled = true;
									break;
							}
						}
						else if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
						{
							// Handle activating windows with Alt + index
							if (key.Key >= ConsoleKey.D1 && key.Key <= ConsoleKey.D9)
							{
								int index = key.Key - ConsoleKey.D1;
								if (index < _windows.Count)
								{
									SetActiveWindow(_windows[index]);
									handled = true;
								}
							}
						}

						if (!handled)
						{
							handled = _activeWindow.ProcessInput(key);
						}

						if (handled)
						{
							_windows.ForEach(w => w.Invalidate());
						}
					}
				}
			}
		}


		private void FillWindowBackground(Window window)
		{
			// Fill the entire area of the window, including borders, with background
			for (var y = 0; y < window.Height; y++)
			{
				if (window.Top + y > DesktopDimensions.Height) break;
				Console.SetCursorPosition(window.Left, window.Top + DesktopUpperLeft.Top + y);
				Console.Write(new string(' ', Math.Min(window.Width, Console.WindowWidth - window.Left)));
			}
		}

		private void CycleActiveWindow()
		{
			if (_windows.Count == 0) return;

			var index = _windows.IndexOf(_activeWindow ?? _windows.First());
			Window? window = _windows[(index + 1) % _windows.Count];

			if (window != null)
			{
				SetActiveWindow(window);
			}
		}

		private void RenderWindow(Window window)
		{
			lock (window)
			{
				var (desktopLeft, desktopTop) = DesktopUpperLeft;
				var (desktopRight, desktopBottom) = DesktopBottomRight;

				// Check if the window is out of screen boundaries
				if (window.Left < desktopLeft || (window.Top + DesktopUpperLeft.Top ) < desktopTop ||
					window.Left >= desktopRight || window.Top + DesktopUpperLeft.Top >= desktopBottom)
				{
					return; // Do not render the window if it is out of screen boundaries
				}

				// Fill the window area with background
				FillWindowBackground(window);

				// Define border characters
				var horizontalBorder = window.IsActive ? '═' : '─';
				var verticalBorder = window.IsActive ? '║' : '│';
				var topLeftCorner = window.IsActive ? '╔' : '┌';
				var topRightCorner = window.IsActive ? '╗' : '┐';
				var bottomLeftCorner = window.IsActive ? '╚' : '└';
				var bottomRightCorner = window.IsActive ? '╝' : '┘';

				// Define border color
				var borderColor = window.IsActive ? "[green]" : "[grey]";
				var titleColor = window.IsActive ? "[bold yellow]" : "[grey]";
				var resetColor = "[/]";

				// Draw top border with title
				var title = $"| {titleColor}{window.Title}{resetColor} |";
				var titleLength = AnsiConsoleExtensions.CalculateEffectiveLength(title);
				var availableSpace = window.Width - 2 - titleLength;
				var leftPadding = 1;
				var rightPadding = availableSpace - leftPadding;

				Console.SetCursorPosition(window.Left + DesktopUpperLeft.Left, window.Top + DesktopUpperLeft.Top);
				Console.Write(AnsiConsoleExtensions.ConvertMarkupToAnsi($"{borderColor}{topLeftCorner}{new string(horizontalBorder, leftPadding)}{title}{new string(horizontalBorder, rightPadding)}{topRightCorner}{resetColor}", window.Width, 1, false)[0]);

				// Draw sides
				for (var y = 1; y < window.Height - 1; y++)
				{
					if (window.Top + DesktopUpperLeft.Top + y - 1 >= desktopBottom) break; // Stop rendering if it reaches the bottom of the console
					Console.SetCursorPosition(window.Left, window.Top + DesktopUpperLeft.Top + y);
					Console.Write(AnsiConsoleExtensions.ConvertMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false)[0]);
					if (window.Left + window.Width - 2 < desktopRight)
					{
						Console.SetCursorPosition(window.Left + window.Width - 1, window.Top + DesktopUpperLeft.Top + y);
						Console.Write(AnsiConsoleExtensions.ConvertMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false)[0]);
					}
				}

				// Draw bottom border
				Console.SetCursorPosition(window.Left, window.Top + DesktopUpperLeft.Top + window.Height - 1);
				var bottomBorderWidth = Math.Min(window.Width - 2, desktopRight - window.Left - 1);
				Console.Write(AnsiConsoleExtensions.ConvertMarkupToAnsi($"{borderColor}{bottomLeftCorner}{new string(horizontalBorder, bottomBorderWidth)}{bottomRightCorner}{resetColor}", window.Width, 1, false)[0]);

				// Render the window content
				var lines = window.GetVisibleContent();
				if (window.IsDirty)
				{
					window.IsDirty = false;
				}

				for (var y = 0; y < lines.Count; y++)
				{
					if (window.Top + y >= desktopBottom) break; // Stop rendering if it reaches the bottom of the console
					var line = lines[y];

					// Truncate the line if it exceeds the console's width
					var maxWidth = Math.Min(window.Width - 2, desktopRight - window.Left - 2);
					if (AnsiConsoleExtensions.GetTrueLengthOfAnsi(line) > maxWidth)
					{
						line = AnsiConsoleExtensions.TruncateAnsiString(line, maxWidth);
					}

					Console.SetCursorPosition(window.Left + 1, window.Top + DesktopUpperLeft.Top + y + 1);
					Console.Write(line);
				}
			}
		}

		private void UpdateDisplay()
		{
			// Calculate the effective length of the bottom row without markup
			var topRow = TopStatus;
			var effectiveLength = AnsiConsoleExtensions.CalculateEffectiveLength(topRow);
			var paddedTopRow = topRow.PadRight(Console.WindowWidth + (topRow.Length - effectiveLength));
			Console.SetCursorPosition(0, 0);
			Console.Write(AnsiConsoleExtensions.ConvertMarkupToAnsi($"[black on white]{paddedTopRow}[/]", Console.WindowWidth, 1, false)[0]);

			lock (_windows)
			{
				var windowsToRender = new HashSet<Window>();

				// Identify dirty windows and overlapping windows
				foreach (var window in _windows)
				{
					if (window.IsDirty)
					{
						var overlappingWindows = GetOverlappingWindows(window);
						foreach (var overlappingWindow in overlappingWindows)
						{
							if (overlappingWindow.IsDirty || IsOverlapping(window, overlappingWindow))
							{
								windowsToRender.Add(overlappingWindow);
							}
						}
					}
				}

				// Render non-active windows based on their ZIndex
				foreach (var window in _windows.OrderBy(w => w.ZIndex))
				{
					if (window != _activeWindow && windowsToRender.Contains(window))
					{
						RenderWindow(window);
					}
				}

				// Check if any of the overlapping windows is overlapping the active window
				if (_activeWindow != null)
				{
					if (windowsToRender.Contains(_activeWindow))
					{
						RenderWindow(_activeWindow);
					}
					else
					{
						var overlappingWindows = GetOverlappingWindows(_activeWindow);

						foreach (var overlappingWindow in overlappingWindows)
						{
							if (windowsToRender.Contains(overlappingWindow))
							{
								RenderWindow(_activeWindow);
							}
						}
					}
				}
			}

			// Display the list of window titles in the bottom row
			var windowTitles = _windows.Select((w, i) => $"[bold]Alt-{i + 1}[/] {w.Title}");
			var bottomRow = string.Join(" | ", windowTitles);
			bottomRow += $" | {BottomStatus}";

			// Calculate the effective length of the bottom row without markup
			effectiveLength = AnsiConsoleExtensions.CalculateEffectiveLength(bottomRow);
			var paddedBottomRow = bottomRow.PadRight(Console.WindowWidth + (bottomRow.Length - effectiveLength));

			Console.SetCursorPosition(0, Console.WindowHeight - 1);
			Console.Write(AnsiConsoleExtensions.ConvertMarkupToAnsi($"[white on blue]{paddedBottomRow}[/]", Console.WindowWidth, 1, false)[0]);
		}


		private HashSet<Window> GetOverlappingWindows(Window window, HashSet<Window>? visited = null)
		{
			visited ??= new HashSet<Window>();
			if (visited.Contains(window))
			{
				return visited;
			}

			visited.Add(window);

			foreach (var otherWindow in _windows)
			{
				if (window != otherWindow && IsOverlapping(window, otherWindow))
				{
					GetOverlappingWindows(otherWindow, visited);
				}
			}

			return visited;
		}

		private bool IsOverlapping(Window window1, Window window2)
		{
			return window1.Left < window2.Left + window2.Width &&
				   window1.Left + window1.Width > window2.Left &&
				   window1.Top < window2.Top + window2.Height &&
				   window1.Top + window1.Height > window2.Top;
		}

		public (int absoluteLeft, int absoluteTop) TranslateToAbsolute(Window window, int relativeLeft, int relativeTop)
		{
			int absoluteLeft = window.Left + relativeLeft;
			int absoluteTop = window.Top + DesktopUpperLeft.Top + relativeTop;
			return (absoluteLeft, absoluteTop);
		}

		public (int relativeLeft, int relativeTop) TranslateToRelative(Window window, int absoluteLeft, int absoluteTop)
		{
			int relativeLeft = absoluteLeft - window.Left;
			int relativeTop = absoluteTop - window.Top - DesktopUpperLeft.Top;
			return (relativeLeft, relativeTop);
		}
	}
}
