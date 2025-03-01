// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;
using ConsoleEx.Themes;
using ConsoleEx.Helpers;
using static ConsoleEx.Window;
using ConsoleEx.Drivers;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace ConsoleEx
{
	public enum Direction
	{
		Up,
		Down,
		Left,
		Right
	}

	public enum WindowTopologyAction
	{
		Resize,
		Move
	}

	public class ConsoleWindowSystem
	{
		private readonly ConcurrentQueue<ConsoleKeyInfo> _inputQueue = new();
		private readonly object _renderLock = new();
		private readonly VisibleRegions _visibleRegions;
		private readonly ConcurrentDictionary<string, Window> _windows = new();
		private Window? _activeWindow;
		private ConcurrentQueue<bool> _blockUi = new();
		private string? _cachedBottomStatus;
		private string? _cachedTopStatus;
		private IConsoleDriver _consoleDriver;
		private int _exitCode;
		private bool _running;
		private int _idleTime = 10; // Initial idle time

		public ConsoleWindowSystem()
		{
			// Initialize the console driver
			_consoleDriver = new NetConsoleDriver(this)
			{
				RenderMode = RenderMode
			};

			// Initialize the visible regions
			_visibleRegions = new VisibleRegions(this);
		}

		public ConcurrentQueue<bool> BlockUi => _blockUi;

		public string BottomStatus { get; set; } = "";

		public IConsoleDriver ConsoleDriver
		{ get { return _consoleDriver; } set { _consoleDriver = value; } }

		public Point DesktopBottomRight => new Point(_consoleDriver.ScreenSize.Width - 1, _consoleDriver.ScreenSize.Height - 1 - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Helpers.Size DesktopDimensions => new Helpers.Size(_consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Point DesktopUpperLeft => new Point(0, string.IsNullOrEmpty(TopStatus) ? 0 : 1);

		public RenderMode RenderMode { get; set; } = RenderMode.Direct;
		public Theme Theme { get; set; } = new Theme();
		public string TopStatus { get; set; } = "";

		public Window AddWindow(Window window)
		{
			window.ZIndex = _windows.Count > 0 ? _windows.Values.Max(w => w.ZIndex) + 1 : 0;
			_windows.TryAdd(window.Guid, window);
			_activeWindow ??= window;
			return window;
		}

		public void CloseWindow(Window? window)
		{
			if (window == null) return;
			if (!_windows.ContainsKey(window.Guid)) return;

			if (window.Close(systemCall: true) == false) return;

			_windows.TryRemove(window.Guid, out _);

			if (_activeWindow == window)
			{
				_activeWindow = _windows.FirstOrDefault().Value;
				if (_activeWindow != null)
				{
					SetActiveWindow(_activeWindow);
				}
			}

			FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
			foreach (var w in _windows.Values)
			{
				w.Invalidate(true);
			}
		}

		public int Run()
		{
			_running = true;

			// Subscribe to the console driver events
			_consoleDriver.KeyPressed += (sender, key) =>
			{
				_inputQueue.Enqueue(key);
			};

			_consoleDriver.ScreenResized += (sender, size) =>
			{
				lock (_renderLock)
				{
					Helpers.Size desktopSize = DesktopDimensions;

					_consoleDriver.Clear();

					FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

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

						window.Invalidate(true);
					}

					_cachedBottomStatus = null;
					_cachedTopStatus = null;
				}
			};

			_consoleDriver.MouseEvent += HandleMouseEvent;

			// Start the console driver
			_consoleDriver.Start();

			// Initialize the console window system with background color and character
			FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

			// Main loop
			while (_running)
			{
				ProcessInput();
				UpdateDisplay();
				UpdateCursor();

				// Adjust idle time based on workload
				if (_inputQueue.IsEmpty && !AnyWindowDirty())
				{
					_idleTime = Math.Min(_idleTime + 10, 100); // Increase idle time up to 100ms
				}
				else
				{
					_idleTime = 10; // Reset idle time when there is work to do
				}

				Thread.Sleep(_idleTime);
			}

			_consoleDriver.Stop();

			return _exitCode;
		}

		private bool AnyWindowDirty()
		{
			return _windows.Values.Any(window => window.IsDirty);
		}

		public void SetActiveWindow(Window window)
		{
			if (window == null)
			{
				return;
			}

			if (_blockUi.Count != 0)
			{
				return;
			}

			_windows.Values.FirstOrDefault(w => w.GetIsActive())?.SetIsActive(false);
			_activeWindow?.Invalidate(true);

			_activeWindow = window;
			_activeWindow.SetIsActive(true);
			_activeWindow.ZIndex = _windows.Values.Max(w => w.ZIndex) + 1;

			_activeWindow.Invalidate(true);
		}

		public (int absoluteLeft, int absoluteTop) TranslateToAbsolute(Window window, Point point)
		{
			int absoluteLeft = window.Left + point.X;
			int absoluteTop = window.Top + DesktopUpperLeft.Y + point.Y;
			return (absoluteLeft, absoluteTop);
		}

		public Point TranslateToRelative(Window window, Point? point)
		{
			if (point == null) return new Point(0, 0);

			int relativeLeft = (point?.X ?? 0) - window.Left;
			int relativeTop = (point?.Y ?? 0) - window.Top - DesktopUpperLeft.Y;
			return new Point(relativeLeft, relativeTop);
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

			_consoleDriver.WriteToConsole(window.Left + window.Width - 1, window.Top + DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{scrollbarChar}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0]);
		}

		private void DrawWindowBorders(Window window)
		{
			var horizontalBorder = window.GetIsActive() ? '═' : '─';
			var verticalBorder = window.GetIsActive() ? '║' : '│';
			var topLeftCorner = window.GetIsActive() ? '╔' : '┌';
			var topRightCorner = window.GetIsActive() ? '╗' : '┐';
			var bottomLeftCorner = window.GetIsActive() ? '╚' : '└';
			var bottomRightCorner = window.GetIsActive() ? '╝' : '┘';

			var borderColor = window.GetIsActive() ? $"[{window.ActiveBorderForegroundColor}]" : $"[{window.InactiveBorderForegroundColor}]";
			var titleColor = window.GetIsActive() ? $"[{window.ActiveTitleForegroundColor}]" : $"[{window.InactiveTitleForegroundColor}]";
			var resetColor = "[/]";

			if (window.IsDragging)
			{
				borderColor = "[red]"; // Change the border color to red when dragging
			}

			var title = $"{titleColor}| {StringHelper.TrimWithEllipsis(window.Title, window.Width - 8, (window.Width - 8) / 2)} |{resetColor}";
			var titleLength = AnsiConsoleHelper.StripSpectreLength(title);
			var availableSpace = window.Width - 2 - titleLength;
			var leftPadding = 1;
			var rightPadding = availableSpace - leftPadding;

			var topBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{topLeftCorner}{new string(horizontalBorder, leftPadding)}{title}{new string(horizontalBorder, rightPadding)}{topRightCorner}{resetColor}", Math.Min(window.Width, DesktopBottomRight.X - window.Left), 1, false, window.BackgroundColor, window.ForegroundColor)[0];
			var bottomBorder = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{bottomLeftCorner}{new string(horizontalBorder, window.Width - 2)}{bottomRightCorner}{resetColor}", window.Width, 1, false, window.BackgroundColor, window.ForegroundColor)[0];

			// Get all windows that potentially overlap with this window
			var overlappingWindows = _windows.Values
				.Where(w => w != window && w.ZIndex > window.ZIndex && IsOverlapping(window, w))
				.OrderBy(w => w.ZIndex)
				.ToList();

			// Calculate visible regions
			var visibleRegions = _visibleRegions.CalculateVisibleRegions(window, overlappingWindows);

			var contentHeight = window.TotalLines;
			var visibleHeight = window.Height - 2;

			var scrollbarVisible = window.IsScrollable && contentHeight > visibleHeight;
			var verticalBorderAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{borderColor}{verticalBorder}{resetColor}", 1, 1, false, window.BackgroundColor, window.ForegroundColor)[0];

			foreach (var region in visibleRegions ?? [])
			{
				if (region.Top == window.Top)
				{
					_consoleDriver.WriteToConsole(region.Left, region.Top + DesktopUpperLeft.Y, AnsiConsoleHelper.SubstringAnsi(topBorder, region.Left - window.Left, region.Width));
				}

				if (region.Top + region.Height == window.Top + window.Height)
				{
					_consoleDriver.WriteToConsole(region.Left, window.Top + window.Height, AnsiConsoleHelper.SubstringAnsi(bottomBorder, region.Left - window.Left, region.Width));
				}
			}

			for (var y = 1; y < window.Height - 1; y++)
			{
				if (window.Top + DesktopUpperLeft.Y + y - 1 >= DesktopBottomRight.Y) break;

				foreach (var region in visibleRegions ?? [])
				{
					if (window.Top + y >= region.Top && window.Top + y < region.Top + region.Height)
					{
						bool isLeftBorderVisible = window.Left >= region.Left && window.Left < region.Left + region.Width;
						bool isRightBorderVisible = window.Left + window.Width > region.Left && window.Left + window.Width < region.Left + region.Width + 1;

						if (isLeftBorderVisible)
						{
							_consoleDriver.WriteToConsole(window.Left, window.Top + DesktopUpperLeft.Y + y, verticalBorderAnsi);
						}

						if (isRightBorderVisible)
						{
							if (scrollbarVisible)
							{
								DrawScrollbar(window, y, borderColor, verticalBorder, resetColor);
							}
							else
							{
								_consoleDriver.WriteToConsole(window.Left + window.Width - 1, window.Top + DesktopUpperLeft.Y + y, verticalBorderAnsi);
							}
						}
					}
				}
			}
		}

		private void FillRect(int left, int top, int width, int height, char character, Color? backgroundColor, Color? foregroundColor)
		{
			for (var y = 0; y < height; y++)
			{
				if (top + y > DesktopDimensions.Height) break;

				_consoleDriver.WriteToConsole(left, top + DesktopUpperLeft.Y + y, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(character, Math.Min(width, _consoleDriver.ScreenSize.Width - left))}", Math.Min(width, _consoleDriver.ScreenSize.Width - left), 1, false, backgroundColor, foregroundColor)[0]);
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

		private Window? GetWindowAtPoint(Point point)
		{
			List<Window> windows = _windows.Values
				.Where(window =>
					point.X >= window.Left &&
					point.X < window.Left + window.Width &&
					point.Y - DesktopUpperLeft.Y > window.Top &&
					point.Y - DesktopUpperLeft.Y <= window.Top + window.Height)
				.OrderByDescending(window => window.ZIndex).ToList();

			if (windows.Any(w => w.Guid == _activeWindow?.Guid))
			{
				return _activeWindow;
			}
			else return windows.LastOrDefault();
		}

		private bool HandleAltInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			if (key.KeyChar >= (char)ConsoleKey.D1 && key.KeyChar <= (char)ConsoleKey.D9)
			{
				if (_blockUi.Count != 0)
				{
					return false;
				}
				int index = key.KeyChar - (char)ConsoleKey.D1;
				if (index < _windows.Count)
				{
					var newActiveWindow = _windows.Values.ElementAt(index);

					SetActiveWindow(newActiveWindow);
					if (newActiveWindow.State == WindowState.Minimized) newActiveWindow.State = WindowState.Normal;
					handled = true;
				}
			}
			return handled;
		}

		private void HandleMouseEvent(object sender, List<MouseFlags> flags, Point point)
		{
			if (flags.Contains(MouseFlags.Button1Clicked))
			{
				// Get window at the clicked point
				var window = GetWindowAtPoint(point);
				if (window != null && window != _activeWindow)
				{
					// Activate the window if it is not already active
					SetActiveWindow(window);
				}
			}
		}

		private bool HandleMoveInput(ConsoleKeyInfo key)
		{
			bool handled = false;
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Up);
					_activeWindow?.SetPosition(new Point(_activeWindow?.Left ?? 0, Math.Max(0, (_activeWindow?.Top ?? 0) - 1)));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Down);
					_activeWindow?.SetPosition(new Point(_activeWindow?.Left ?? 0, Math.Min(DesktopBottomRight.Y - (_activeWindow?.Height ?? 0) + 1, (_activeWindow?.Top ?? 0) + 1)));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Left);
					_activeWindow?.SetPosition(new Point(Math.Max(DesktopUpperLeft.X, (_activeWindow?.Left ?? 0) - 1), _activeWindow?.Top ?? 0));
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Right);
					_activeWindow?.SetPosition(new Point(Math.Min(DesktopBottomRight.X - (_activeWindow?.Width ?? 0) + 1, (_activeWindow?.Left ?? 0) + 1), _activeWindow?.Top ?? 0));
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
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Resize, Direction.Up);
					_activeWindow?.SetSize(_activeWindow.Width, Math.Max(1, _activeWindow.Height - 1));
					handled = true;
					break;

				case ConsoleKey.DownArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Resize, Direction.Down);
					_activeWindow?.SetSize(_activeWindow.Width, Math.Min(DesktopDimensions.Height - _activeWindow.Top, _activeWindow.Height + 1));
					handled = true;
					break;

				case ConsoleKey.LeftArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Left);
					_activeWindow?.SetSize(Math.Max(1, _activeWindow.Width - 1), _activeWindow.Height);
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					MoveOrResizeOperation(_activeWindow, WindowTopologyAction.Move, Direction.Right);
					_activeWindow?.SetSize(Math.Min(DesktopBottomRight.X - _activeWindow.Left + 1, _activeWindow.Width + 1), _activeWindow.Height);
					handled = true;
					break;
			}
			return handled;
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

		private bool IsWindowOutOfBounds(Window window, Point desktopTopLeftCorner, Point desktopBottomRightCorner)
		{
			return window.Left < desktopTopLeftCorner.X || (window.Top + DesktopUpperLeft.Y) < desktopTopLeftCorner.Y ||
				   window.Left >= desktopBottomRightCorner.X || window.Top + DesktopUpperLeft.Y >= desktopBottomRightCorner.Y;
		}

		private void MoveOrResizeOperation(Window? window, WindowTopologyAction windowTopologyAction, Direction direction)
		{
			if (window == null) return;

			int left = window.Left;
			int top = window.Top;
			int width = window.Width;
			int height = window.Height;

			switch (windowTopologyAction)
			{
				case WindowTopologyAction.Move:
					switch (direction)
					{
						case Direction.Up:
							FillRect(left, top + height - 1, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Down:
							FillRect(left, top, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Left:
							FillRect(left + width - 1, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Right:
							FillRect(left, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;
					}
					break;

				case WindowTopologyAction.Resize:
					switch (direction)
					{
						case Direction.Up:
							FillRect(left, top + height - 1, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Down:
							FillRect(left, top + height - 1, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Left:
							FillRect(left + width, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Right:
							FillRect(left + width - 1, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;
					}
					break;
			}

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
					if (_blockUi.Count != 0)
					{
						return;
					}
					CycleActiveWindow();
				}
				else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.Q)
				{
					_exitCode = 0;
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

		private void RenderVisibleWindowContent(Window window, List<string> lines, List<Rectangle> visibleRegions)
		{
			var screenWidth = _consoleDriver.ScreenSize.Width;
			var screenHeight = _consoleDriver.ScreenSize.Height;
			var windowLeft = window.Left;
			var windowTop = window.Top;
			var windowWidth = window.Width;
			var desktopUpperLeftY = DesktopUpperLeft.Y;

			for (var y = 0; y < lines.Count; y++)
			{
				// Skip if this line is outside the desktop area
				if (windowTop + y >= DesktopBottomRight.Y) break;

				// Get the current line
				var line = lines[y];

				// Check if this line is in any visible region
				foreach (var region in visibleRegions)
				{
					// Check if this line falls within the current region's vertical bounds
					if (window.Top + y + 1 >= region.Top && window.Top + y + 1 < region.Top + region.Height)
					{
						// Calculate content boundaries within the window
						int contentLeft = Math.Max(windowLeft + 1, region.Left);
						int contentRight = Math.Min(windowLeft + windowWidth - 1, region.Left + region.Width);
						int contentWidth = contentRight - contentLeft;

						if (contentWidth <= 0) continue;

						// Calculate the portion of the line to render
						int startOffset = contentLeft - (windowLeft + 1);
						startOffset = Math.Max(0, startOffset);

						// Get the substring of the line to render
						string visiblePortion = AnsiConsoleHelper.SubstringAnsi(line, startOffset, contentWidth);

						// Write the visible portion to the console
						_consoleDriver.WriteToConsole(contentLeft, windowTop + desktopUpperLeftY + y + 1, visiblePortion);
					}
				}
			}
		}

		private void RenderWindow(Window window)
		{
			lock (_renderLock)
			{
				Point desktopTopLeftCorner = DesktopUpperLeft;
				Point desktopBottomRightCorner = DesktopBottomRight;

				if (IsWindowOutOfBounds(window, desktopTopLeftCorner, desktopBottomRightCorner))
				{
					return;
				}

				// Get all windows that potentially overlap with this window
				var overlappingWindows = _windows.Values
					.Where(w => w != window && w.ZIndex > window.ZIndex && IsOverlapping(window, w))
					.OrderBy(w => w.ZIndex)
					.ToList();

				// Calculate visible regions
				var visibleRegions = _visibleRegions.CalculateVisibleRegions(window, overlappingWindows);

				if (!visibleRegions.Any())
				{
					// Window is completely covered - no need to render
					window.IsDirty = false;
					return;
				}

				// Fill the background only for the visible regions
				foreach (var region in visibleRegions)
				{
					FillRect(region.Left, region.Top, region.Width, region.Height, ' ', window.BackgroundColor, null);
				}

				// Draw window borders - these might be partially hidden but the drawing functions
				// will handle clipping against screen boundaries
				DrawWindowBorders(window);

				var lines = window.RenderAndGetVisibleContent();
				window.IsDirty = false;

				// Render content only for visible parts
				RenderVisibleWindowContent(window, lines, visibleRegions);
			}
		}

		private void UpdateCursor()
		{
			if (_activeWindow != null && _activeWindow.HasInteractiveContent(out var cursorPosition))
			{
				var (absoluteLeft, absoluteTop) = TranslateToAbsolute(_activeWindow, new Point(cursorPosition.X, cursorPosition.Y));

				// Check if the cursor position is within the console window boundaries
				if (absoluteLeft >= 0 && absoluteLeft < _consoleDriver.ScreenSize.Width &&
					absoluteTop >= 0 && absoluteTop < _consoleDriver.ScreenSize.Height &&
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
				if (TopStatus != _cachedTopStatus)
				{
					var topRow = TopStatus;

					var effectiveLength = AnsiConsoleHelper.StripSpectreLength(topRow);
					var paddedTopRow = topRow.PadRight(_consoleDriver.ScreenSize.Width + (topRow.Length - effectiveLength));
					_consoleDriver.WriteToConsole(0, 0, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.TopBarForegroundColor}]{paddedTopRow}[/]", _consoleDriver.ScreenSize.Width, 1, false, Theme.TopBarBackgroundColor, null)[0]);

					_cachedTopStatus = TopStatus;
				}

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

			string bottomRow = $"{string.Join(" | ", _windows.Values.Select((w, i) => $"[bold]Alt-{i + 1}[/] {StringHelper.TrimWithEllipsis(w.Title, 15, 7)}"))} | {BottomStatus}";

			// Display the list of window titles in the bottom row
			if (AnsiConsoleHelper.StripSpectreLength(bottomRow) > _consoleDriver.ScreenSize.Width)
			{
				bottomRow = AnsiConsoleHelper.TruncateSpectre(bottomRow, _consoleDriver.ScreenSize.Width);
			}

			bottomRow += new string(' ', _consoleDriver.ScreenSize.Width - AnsiConsoleHelper.StripSpectreLength(bottomRow));

			if (_cachedBottomStatus != bottomRow)
			{   //add padding to the bottom row
				_consoleDriver.WriteToConsole(0, _consoleDriver.ScreenSize.Height - 1, AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"[{Theme.BottomBarForegroundColor}]{bottomRow}[/]", _consoleDriver.ScreenSize.Width, 1, false, Theme.BottomBarBackgroundColor, null)[0]);

				_cachedBottomStatus = bottomRow;
			}

			_consoleDriver.Flush();
		}
	}
}
