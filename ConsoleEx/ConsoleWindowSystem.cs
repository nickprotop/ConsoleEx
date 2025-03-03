// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using System.Collections.Concurrent;
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
		private readonly Renderer _renderer;
		private readonly object _renderLock = new();
		private readonly VisibleRegions _visibleRegions;
		private readonly ConcurrentDictionary<string, Window> _windows = new();
		private Window? _activeWindow;
		private ConcurrentQueue<bool> _blockUi = new();
		private string? _cachedBottomStatus;
		private string? _cachedTopStatus;
		private IConsoleDriver _consoleDriver;
		private int _exitCode;
		private int _idleTime = 10;
		private bool _running;
		// Initial idle time

		public ConsoleWindowSystem(RenderMode renderMode)
		{
			RenderMode = renderMode;

			// Initialize the console driver
			_consoleDriver = new NetConsoleDriver(this)
			{
				RenderMode = RenderMode
			};

			// Initialize the visible regions
			_visibleRegions = new VisibleRegions(this);

			// Initialize the renderer
			_renderer = new Renderer(this);
		}

		public ConcurrentQueue<bool> BlockUi => _blockUi;
		public string BottomStatus { get; set; } = "";

		public IConsoleDriver ConsoleDriver
		{ get { return _consoleDriver; } set { _consoleDriver = value; } }

		public Point DesktopBottomRight => new Point(_consoleDriver.ScreenSize.Width - 1, _consoleDriver.ScreenSize.Height - 1 - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Helpers.Size DesktopDimensions => new Helpers.Size(_consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height - (string.IsNullOrEmpty(TopStatus) ? 0 : 1) - (string.IsNullOrEmpty(BottomStatus) ? 0 : 1));
		public Point DesktopUpperLeft => new Point(0, string.IsNullOrEmpty(TopStatus) ? 0 : 1);
		public RenderMode RenderMode { get; set; }
		public Theme Theme { get; set; } = new Theme();
		public string TopStatus { get; set; } = "";
		public VisibleRegions VisibleRegions => _visibleRegions;
		public ConcurrentDictionary<string, Window> Windows => _windows;

		public Window AddWindow(Window window)
		{
			window.ZIndex = _windows.Count > 0 ? _windows.Values.Max(w => w.ZIndex) + 1 : 0;
			_windows.TryAdd(window.Guid, window);
			_activeWindow ??= window;

			window.WindowIsAdded();

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

			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
			foreach (var w in _windows.Values)
			{
				w.Invalidate(true);
			}
		}

		public void FlashWindow(Window? window, int flashCount = 3, int flashDuration = 200, Color? flashBackgroundColor = null)
		{
			if (window == null) return;

			var originalBackgroundColor = window.BackgroundColor;
			var flashColor = flashBackgroundColor ?? (window.BackgroundColor == Theme.ButtonBackgroundColor ? window.ForegroundColor : Theme.ButtonBackgroundColor);

			var flashTask = new Task(async () =>
			{
				for (int i = 0; i < flashCount; i++)
				{
					if (window == null) return;

					window.BackgroundColor = flashColor;
					window.Invalidate(true);
					await Task.Delay(flashDuration);

					window.BackgroundColor = originalBackgroundColor;
					window.Invalidate(true);
					await Task.Delay(flashDuration);
				}
			});

			flashTask.Start();
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

					_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

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
			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);

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

		public void SetActiveWindow(Window window)
		{
			if (window == null)
			{
				return;
			}

			if (_blockUi.Count != 0)
			{
				FlashWindow(_activeWindow);
				return;
			}

			var previousActiveWindow = _activeWindow;

			_windows.Values.FirstOrDefault(w => w.GetIsActive())?.SetIsActive(false);
			_activeWindow?.Invalidate(true);

			_activeWindow = window;
			_activeWindow.SetIsActive(true);
			_activeWindow.ZIndex = _windows.Values.Max(w => w.ZIndex) + 1;

			_activeWindow.Invalidate(true);

			// Unfocus the currently focused control of other windows
			foreach (var w in _windows.Values)
			{
				if (w != _activeWindow)
				{
					w.UnfocusCurrentControl();
				}
			}
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

		private bool AnyWindowDirty()
		{
			return _windows.Values.Any(window => window.IsDirty);
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
					FlashWindow(_activeWindow);
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
				if (otherWindow != window && _renderer.IsOverlapping(window, otherWindow) && otherWindow.ZIndex > window.ZIndex)
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
							_renderer.FillRect(left, top + height - 1, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Down:
							_renderer.FillRect(left, top, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Left:
							_renderer.FillRect(left + width - 1, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Right:
							_renderer.FillRect(left, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;
					}
					break;

				case WindowTopologyAction.Resize:
					switch (direction)
					{
						case Direction.Up:
							_renderer.FillRect(left, top + height - 1, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Down:
							_renderer.FillRect(left, top + height - 1, width, 1, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Left:
							_renderer.FillRect(left + width, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;

						case Direction.Right:
							_renderer.FillRect(left + width - 1, top, 1, height, Theme.DesktopBackroundChar, Theme.DesktopBackgroundColor, Theme.DesktopForegroundColor);
							break;
					}
					break;
			}

			// Redraw the necessary regions
			foreach (var w in _windows.Values.OrderBy(w => w.ZIndex))
			{
				if (w != window && _renderer.IsOverlapping(window, w))
				{
					var overlappingRegions = _renderer.GetOverlappingRegions(window, w);
					foreach (var region in overlappingRegions)
					{
						Rectangle redrawRegion = new();

						switch (windowTopologyAction)
						{
							case WindowTopologyAction.Move:
								switch (direction)
								{
									case Direction.Up:
										redrawRegion = new Rectangle(region.Left, region.Bottom - 1, region.Right - region.Left, 1);
										break;

									case Direction.Down:
										redrawRegion = new Rectangle(region.Left, region.Top, region.Right - region.Left, 1);
										break;

									case Direction.Left:
										redrawRegion = new Rectangle(region.Right - 1, region.Top, 1, region.Bottom - region.Top);
										break;

									case Direction.Right:
										redrawRegion = new Rectangle(region.Left, region.Top, 1, region.Bottom - region.Top);
										break;
								}
								break;

							case WindowTopologyAction.Resize:
								switch (direction)
								{
									case Direction.Up:
										redrawRegion = new Rectangle(region.Left, region.Bottom - 1, region.Right - region.Left, 1);
										break;

									case Direction.Down:
										redrawRegion = new Rectangle(region.Left, region.Top, region.Right - region.Left, 1);
										break;

									case Direction.Left:
										redrawRegion = new Rectangle(region.Right - 1, region.Top, 1, region.Bottom - top);
										break;

									case Direction.Right:
										redrawRegion = new Rectangle(region.Left, region.Top, 1, region.Bottom - region.Top);
										break;
								}
								break;
						}

						_renderer.RenderRegion(w, redrawRegion);
					}
				}
			}

			window.Invalidate(false);
		}

		private void ProcessInput()
		{
			while (_inputQueue.TryDequeue(out var key))
			{
				if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.T)
				{
					if (_blockUi.Count != 0)
					{
						FlashWindow(_activeWindow);
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
						var overlappingWindows = _renderer.GetOverlappingWindows(window);
						foreach (var overlappingWindow in overlappingWindows)
						{
							if (overlappingWindow.IsDirty || _renderer.IsOverlapping(window, overlappingWindow))
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
						_renderer.RenderWindow(window);
					}
				}

				// Check if any of the overlapping windows is overlapping the active window
				if (_activeWindow != null)
				{
					if (windowsToRender.Contains(_activeWindow))
					{
						_renderer.RenderWindow(_activeWindow);
					}
					else
					{
						var overlappingWindows = _renderer.GetOverlappingWindows(_activeWindow);

						foreach (var overlappingWindow in overlappingWindows)
						{
							if (windowsToRender.Contains(overlappingWindow))
							{
								_renderer.RenderWindow(_activeWindow);
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