// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Contents;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;
using ConsoleEx.Themes;
using ConsoleEx.Helpers;
using static ConsoleEx.Window;

namespace ConsoleEx
{
	public enum RenderMode
	{
		Direct,
		Buffer
	}

	public class ConsoleWindowSystem
	{
		private readonly ConcurrentQueue<ConsoleKeyInfo> _inputQueue = new();
		private readonly object _renderLock = new();
		private readonly ConcurrentDictionary<int, Window> _windows = new();
		private Window? _activeWindow;
		private int _exitCode;
		private string? _exitMessage;
		private int _lastConsoleHeight;
		private int _lastConsoleWidth;
		private bool _running;

		public ConsoleWindowSystem()
		{
			Console.OutputEncoding = Encoding.UTF8;
		}

		public string BottomStatus { get; set; } = "";
		public Position DesktopBottomRight => new Position(Console.WindowWidth - 1, Console.WindowHeight - 1 - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Size DesktopDimensions => new Size(Console.WindowWidth, Console.WindowHeight - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Position DesktopUpperLeft => new Position(0, string.IsNullOrEmpty(TopStatus) ? 0 : 1);

		public RenderMode RenderMode { get; set; } = RenderMode.Buffer;
		public Theme Theme { get; set; } = new Theme();
		public string TopStatus { get; set; } = "";

		public Window AddWindow(Window window)
		{
			window.ZIndex = _windows.Count > 0 ? _windows.Values.Max(w => w.ZIndex) + 1 : 0;
			_windows.TryAdd(window.ZIndex, window);
			_activeWindow ??= window;
			return window;
		}

		public void CloseWindow(Window? window)
		{
			if (window == null) return;

			_windows.TryRemove(window.ZIndex, out _);
			if (_activeWindow == window)
			{
				_activeWindow = _windows.Values.FirstOrDefault();
				if (_activeWindow != null)
				{
					SetActiveWindow(_activeWindow);
				}
			}

			FillRect(0, 0, Console.WindowWidth, Console.WindowHeight, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
			foreach (var w in _windows.Values)
			{
				w.Invalidate(true);
			}
		}

		public (int exitCode, string? exitMessage) Run()
		{
			_running = true;
			Console.CursorVisible = false;

			_lastConsoleWidth = Console.WindowWidth;
			_lastConsoleHeight = Console.WindowHeight;

			FillRect(0, 0, Console.WindowWidth, Console.WindowHeight, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			var inputTask = Task.Run(InputLoop);
			var resizeTask = Task.Run(ResizeLoop);

			while (_running)
			{
				lock (_renderLock)
				{
					ProcessInput();
					UpdateDisplay();
					UpdateCursor();
				}
				Thread.Sleep(50);
			}

			Console.Clear();
			Console.SetCursorPosition(0, 0);
			Console.WriteLine($"Console window system terminated{(string.IsNullOrEmpty(_exitMessage) ? "." : (" with message: \"" + _exitMessage + "\""))}");

			Console.CursorVisible = true;

			return (_exitCode, _exitMessage);
		}

		public void SetActiveWindow(Window window)
		{
			if (window == null)
			{
				return;
			}

			_activeWindow?.Invalidate(true);

			_activeWindow = window;
			foreach (var w in _windows.Values)
			{
				w.SetIsActive(false);
			}
			_activeWindow.SetIsActive(true);
			_activeWindow.ZIndex = _windows.Values.Max(w => w.ZIndex) + 1;

			_activeWindow.Invalidate(true);
		}

		public (int absoluteLeft, int absoluteTop) TranslateToAbsolute(Window window, Position point)
		{
			int absoluteLeft = window.Left + point.X;
			int absoluteTop = window.Top + DesktopUpperLeft.Y + point.Y;
			return (absoluteLeft, absoluteTop);
		}

		public Position TranslateToRelative(Window window, Position point)
		{
			int relativeLeft = point.X - window.Left;
			int relativeTop = point.Y - window.Top - DesktopUpperLeft.Y;
			return new Position(relativeLeft, relativeTop);
		}

		private void CycleActiveWindow()
		{
			if (_windows.Count == 0) return;

			var index = _windows.Values.ToList().IndexOf(_activeWindow ?? _windows.Values.First());
			Window? window = _windows.Values.ElementAt((index + 1) % _windows.Count);

			if (window != null)
			{
				SetActiveWindow(window);
				if (window.State == WindowState.Minimized) window.State = WindowState.Normal;
			}
		}

		private void DrawScrollbar(Window window, int y, string borderColor, char verticalBorder, string resetColor)
		{
			var scrollbarChar = '░';
			var contentHeight = window.TotalLines;
			var visibleHeight = window.Height - 2;

			if (window.IsScrollable && contentHeight > visibleHeight)
			{
				if (window.Height > 2)
				{
					var scrollPosition = (float)window.ScrollOffset / Math.Max(1, contentHeight - visibleHeight);
					var scrollbarPosition = (int)(scrollPosition * (visibleHeight - 1));
					if (y - 1 == scrollbarPosition)
					{
						scrollbarChar = '█';
					}
				}
			}
			else scrollbarChar = verticalBorder;

			WriteToConsole(window.Left + window.Width - 1, window.Top + DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{scrollbarChar}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);
		}

		private void DrawWindowBorders(Window window)
		{
			var horizontalBorder = window.GetIsActive() ? '═' : '─';
			var verticalBorder = window.GetIsActive() ? '║' : '│';
			var topLeftCorner = window.GetIsActive() ? '╔' : '┌';
			var topRightCorner = window.GetIsActive() ? '╗' : '┐';
			var bottomLeftCorner = window.GetIsActive() ? '╚' : '└';
			var bottomRightCorner = window.GetIsActive() ? '╝' : '┘';

			var borderColor = window.GetIsActive() ? $"[{Theme.ActiveBorderColor}]" : $"[{Theme.InactiveBorderColor}]";
			var titleColor = window.GetIsActive() ? $"[{Theme.ActiveTitleColor}]" : $"[{Theme.InactiveTitleColor}]";
			var resetColor = "[/]";

			var title = $"{titleColor}| {StringHelper.TrimWithEllipsis(window.Title, window.Width - 8, (window.Width - 8) / 2)} |{resetColor}";
			var titleLength = AnsiConsoleHelper.StripSpectreLength(title);
			var availableSpace = window.Width - 2 - titleLength;
			var leftPadding = 1;
			var rightPadding = availableSpace - leftPadding;

			var topBorderWidth = Math.Min(window.Width, DesktopBottomRight.X - window.Left + 1);
			WriteToConsole(window.Left, window.Top + DesktopUpperLeft.Y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{topLeftCorner}{new string(horizontalBorder, leftPadding)}{title}{new string(horizontalBorder, rightPadding)}{topRightCorner}{resetColor}", topBorderWidth, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);

			for (var y = 1; y < window.Height - 1; y++)
			{
				if (window.Top + DesktopUpperLeft.Y + y - 1 >= DesktopBottomRight.Y) break;
				WriteToConsole(window.Left, window.Top + DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);

				if (window.Left + window.Width - 2 < DesktopBottomRight.X)
				{
					WriteToConsole(window.Left + window.Width - 1, window.Top + DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);
				}

				DrawScrollbar(window, y, borderColor, verticalBorder, resetColor);
			}

			var bottomBorderWidth = Math.Min(window.Width - 2, DesktopBottomRight.X - window.Left - 1);
			WriteToConsole(window.Left, window.Top + DesktopUpperLeft.Y + window.Height - 1, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{bottomLeftCorner}{new string(horizontalBorder, bottomBorderWidth)}{bottomRightCorner}{resetColor}", window.Width, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);
		}

		private void FillRect(int left, int top, int width, int height, char character, Color? backgroundColor, Color? foregroundColor)
		{
			for (var y = 0; y < height; y++)
			{
				if (top + y > DesktopDimensions.Height) break;

				WriteToConsole(left, top + DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(character, Math.Min(width, Console.WindowWidth - left))}", Math.Min(width, Console.WindowWidth - left), 1, false, backgroundColor, foregroundColor)[0]);
			}
		}

		private HashSet<Window> GetOverlappingWindows(Window window, HashSet<Window>? visited = null)
		{
			visited ??= new HashSet<Window>();
			if (visited.Contains(window))
			{
				return visited;
			}

			visited.Add(window);

			foreach (var otherWindow in _windows.Values)
			{
				if (window != otherWindow && IsOverlapping(window, otherWindow))
				{
					GetOverlappingWindows(otherWindow, visited);
				}
			}

			return visited;
		}

		private bool HandleAltInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			if (key.Key >= ConsoleKey.D1 && key.Key <= ConsoleKey.D9)
			{
				int index = key.Key - ConsoleKey.D1;
				if (index < _windows.Count)
				{
					SetActiveWindow(_windows[index]);
					if (_windows[index].State == WindowState.Minimized) _windows[index].State = WindowState.Normal;
					handled = true;
				}
			}
			return handled;
		}

		private bool HandleMoveInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetPosition(new Position(_activeWindow?.Left ?? 0, Math.Max(0, (_activeWindow?.Top ?? 0) - 1)));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetPosition(new Position(_activeWindow?.Left ?? 0, Math.Min(DesktopBottomRight.Y - (_activeWindow?.Height ?? 0) + 1, (_activeWindow?.Top ?? 0) + 1)));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetPosition(new Position(Math.Max(DesktopUpperLeft.X, (_activeWindow?.Left ?? 0) - 1), _activeWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetPosition(new Position(Math.Min(DesktopBottomRight.X - (_activeWindow?.Width ?? 0) + 1, (_activeWindow?.Left ?? 0) + 1), _activeWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.X:
					CloseWindow(_activeWindow);
					handled = true;
					break;
			}
			return handled;
		}

		private bool HandleResizeInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetSize(_activeWindow.Width, Math.Max(1, _activeWindow.Height - 1));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetSize(_activeWindow.Width, Math.Min(DesktopDimensions.Height - _activeWindow.Top, _activeWindow.Height + 1));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetSize(Math.Max(1, _activeWindow.Width - 1), _activeWindow.Height);
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(_activeWindow);
					_activeWindow?.SetSize(Math.Min(DesktopBottomRight.X - _activeWindow.Left + 1, _activeWindow.Width + 1), _activeWindow.Height);
					handled = true;
					break;
			}
			return handled;
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

		private bool IsCompletelyCovered(Window window)
		{
			foreach (var otherWindow in _windows.Values)
			{
				if (otherWindow != window && IsOverlapping(window, otherWindow) && otherWindow.ZIndex > window.ZIndex)
				{
					if (otherWindow.Left <= window.Left && otherWindow.Top <= window.Top &&
						otherWindow.Left + otherWindow.Width >= window.Left + window.Width &&
						otherWindow.Top + otherWindow.Height >= window.Top + window.Height)
					{
						return true;
					}
				}
			}
			return false;
		}

		private bool IsOverlapping(Window window1, Window window2)
		{
			return window1.Left < window2.Left + window2.Width &&
				   window1.Left + window1.Width > window2.Left &&
				   window1.Top < window2.Top + window2.Height &&
				   window1.Top + window1.Height > window2.Top;
		}

		private bool IsWindowOutOfBounds(Window window, Position desktopTopLeftCorner, Position desktopBottomRightCorner)
		{
			return window.Left < desktopTopLeftCorner.X || (window.Top + DesktopUpperLeft.Y) < desktopTopLeftCorner.Y ||
				   window.Left >= desktopBottomRightCorner.X || window.Top + DesktopUpperLeft.Y >= desktopBottomRightCorner.Y;
		}

		private void MoveOrResizeOperation(Window? window)
		{
			if (window == null) return;

			FillRect(window.Left, window.Top, window.Width, window.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			foreach (var w in _windows.Values)
			{
				if (w != window && IsOverlapping(window, w))
				{
					w.Invalidate(true);
				}

				window.Invalidate(true);
			}
		}

		private void ProcessInput()
		{
			while (_inputQueue.TryDequeue(out var key))
			{
				if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.T)
				{
					CycleActiveWindow();
				}
				else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.Q)
				{
					_exitMessage = "user requested exit";
					_running = false;
				}
				else if (_activeWindow != null)
				{
					bool handled = false;

					if ((key.Modifiers & ConsoleModifiers.Shift) != 0 && _activeWindow.IsResizable)
					{
						handled = HandleResizeInput(key);
					}
					else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && _activeWindow.IsMovable)
					{
						handled = HandleMoveInput(key);
					}
					else if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
					{
						handled = HandleAltInput(key);
					}

					if (!handled)
					{
						handled = _activeWindow.ProcessInput(key);
					}
				}
			}
		}

		private void RenderWindow(Window window)
		{
			lock (_renderLock)
			{
				Position desktopTopLeftCorner = DesktopUpperLeft;
				Position desktopBottomRightCorner = DesktopBottomRight;

				if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
				{
					return;
				}

				FillRect(window.Left, window.Top, window.Width, window.Height, ' ', window.BackgroundColor, null);
				DrawWindowBorders(window);

				var lines = window.RenderAndGetVisibleContent();
				if (window.IsDirty)
				{
					window.IsDirty = false;
				}

				RenderWindowContent(window, lines);
			}
		}

		private void RenderWindowContent(Window window, List<string> lines)
		{
			for (var y = 0; y < lines.Count; y++)
			{
				if (window.Top + y >= DesktopBottomRight.Y) break;
				var line = lines[y];

				var maxWidth = Math.Min(window.Width - 2, DesktopBottomRight.X - window.Left - 2);
				if (AnsiConsoleHelper.StripAnsiStringLength(line) > maxWidth)
				{
					line = AnsiConsoleHelper.TruncateAnsiString(line, maxWidth);
				}

				WriteToConsole(window.Left + 1, window.Top + DesktopUpperLeft.Y + y + 1, $"{line}");
			}
		}

		private void ResizeLoop()
		{
			{
				if (Console.WindowWidth != _lastConsoleWidth || Console.WindowHeight != _lastConsoleHeight)
				{
					lock (_renderLock)
					{
						Size desktopSize = DesktopDimensions;

						foreach (var window in _windows.Values)
						{
							if (window.Left + window.Width > desktopSize.Width)
							{
								window.Left = Math.Max(0, desktopSize.Width - window.Width);
							}
							if (window.Top + window.Height > desktopSize.Height)
							{
								window.Top = Math.Max(1, desktopSize.Height - window.Height);
							}
						}

						foreach (var window in _windows.Values)
						{
							window.Invalidate(true);
						}

						_lastConsoleWidth = Console.WindowWidth;
						_lastConsoleHeight = Console.WindowHeight;

						FillRect(0, 0, Console.WindowWidth, Console.WindowHeight, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
					}
				}
				Thread.Sleep(100);
			}
		}

		private void UpdateCursor()
		{
			if (_activeWindow != null && _activeWindow.HasInteractiveContent(out var cursorPosition))
			{
				var (absoluteLeft, absoluteTop) = TranslateToAbsolute(_activeWindow, new Position(cursorPosition.X, cursorPosition.Y));

				// Check if the cursor position is within the console window boundaries
				if (absoluteLeft >= 0 && absoluteLeft < Console.WindowWidth &&
					absoluteTop >= 0 && absoluteTop < Console.WindowHeight &&
					// Check if the cursor position is within the active window's boundaries
					absoluteLeft + 1 >= _activeWindow.Left && absoluteLeft + 1 < _activeWindow.Left + _activeWindow.Width &&
					absoluteTop > _activeWindow.Top + 1 && absoluteTop < _activeWindow.Top + _activeWindow.Height)
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

		private void UpdateDisplay()
		{
			lock (_renderLock)
			{
				// Calculate the effective length of the bottom row without markup
				var topRow = TopStatus;
				var effectiveLength = AnsiConsoleHelper.StripSpectreLength(topRow);
				var paddedTopRow = topRow.PadRight(Console.WindowWidth + (topRow.Length - effectiveLength));
				WriteToConsole(0, 0, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.TopBarForegroundColor}]{paddedTopRow}[/]", Console.WindowWidth, 1, false, Theme.TopBarBackgroundColor, null)[0]);

				var windowsToRender = new HashSet<Window>();

				// Identify dirty windows and overlapping windows
				foreach (var window in _windows.Values)
				{
					if (window.IsDirty && !IsCompletelyCovered(window))
					{
						var overlappingWindows = GetOverlappingWindows(window);
						foreach (var overlappingWindow in overlappingWindows)
						{
							if (overlappingWindow.IsDirty || IsOverlapping(window, overlappingWindow))
							{
								if (overlappingWindow.IsDirty || overlappingWindow.ZIndex > window.ZIndex)
								{
									windowsToRender.Add(overlappingWindow);
								}
							}
						}
					}
				}

				// Render non-active windows based on their ZIndex
				foreach (var window in _windows.Values.OrderBy(w => w.ZIndex))
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
			string bottomRow = $"{string.Join(" | ", _windows.Values.Select((w, i) => $"[bold]Alt-{i + 1}[/] {StringHelper.TrimWithEllipsis(w.Title, 15, 7)}"))} | {BottomStatus}";

			if (AnsiConsoleHelper.StripSpectreLength(bottomRow) > Console.WindowWidth)
			{
				bottomRow = AnsiConsoleHelper.TruncateSpectre(bottomRow, Console.WindowWidth);
			}

			//add padding to the bottom row
			bottomRow += new string(' ', Console.WindowWidth - AnsiConsoleHelper.StripSpectreLength(bottomRow));

			WriteToConsole(0, Console.WindowHeight - 1, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.BottomBarForegroundColor}]{bottomRow}[/]", Console.WindowWidth, 1, false, Theme.BottomBarBackgroundColor, null)[0]);
		}

		private void WriteToConsole(int x, int y, string value)
		{
			switch (RenderMode)
			{
				case RenderMode.Direct:
					Console.SetCursorPosition(x, y);
					Console.Write(value);
					break;
			}
		}
	}
}