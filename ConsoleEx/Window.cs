// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Controls;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace ConsoleEx
{
	public enum WindowMode
	{
		Normal,
		Modal
	}

	public enum WindowState
	{
		Normal,
		Minimized,
		Maximized
	}

	public class ClosingEventArgs : EventArgs
	{
		public ClosingEventArgs()
		{
		}

		public bool Allow { get; set; } = true;
	}

	public class KeyPressedEventArgs : EventArgs
	{
		public KeyPressedEventArgs(ConsoleKeyInfo keyInfo, bool allreadyHandled)
		{
			KeyInfo = keyInfo;
			AllreadyHandled = allreadyHandled;
		}

		public bool AllreadyHandled { get; private set; }

		public bool Handled { get; set; }
		public ConsoleKeyInfo KeyInfo { get; }
	}

	public class Window : IContainer
	{
		private readonly List<IWIndowControl> _content = new();
		private readonly List<IInteractiveControl> _interactiveContents = new();
		private readonly Lock _lock = new();

		private readonly Window? _parentWindow;
		private Color? _activeBorderForegroundColor;
		private Color? _activeTitleForegroundColor;
		private int _bottomStickyHeight;
		private List<string> _bottomStickyLines = new List<string>();

		// List to store interactive contents
		private List<string> _cachedContent = new();

		private Dictionary<IWIndowControl, int> _contentLeftIndex = new();
		private Dictionary<IWIndowControl, int> _contentTopRowIndex = new();
		private string _guid;
		private Color? _inactiveBorderForegroundColor;
		private Color? _inactiveTitleForegroundColor;
		private bool _invalidated = false;
		private bool _isActive;
		private IInteractiveControl? _lastFocusedControl;
		private int? _maximumHeight;
		private int? _maximumWidth;
		private int? _minimumHeight = 3;
		private int? _minimumWidth = 10;
		private WindowMode _mode = WindowMode.Normal;
		private int _scrollOffset;
		private WindowState _state;
		private object? _tag;
		private int _topStickyHeight;
		private List<string> _topStickyLines = new List<string>();
		private ConsoleWindowSystem? _windowSystem;
		private Task? _windowTask;
		private Thread? _windowThread;
		private WindowThreadDelegate? _windowThreadMethod;
		private WindowThreadDelegateAsync? _windowThreadMethodAsync;

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegateAsync windowThreadMethod, Window? parentWindow = null)
		{
			_guid = System.Guid.NewGuid().ToString();

			_parentWindow = parentWindow;
			_windowSystem = windowSystem;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			// Set position relative to parent if this is a subwindow
			SetupInitialPosition();

			_windowThreadMethodAsync = windowThreadMethod;
			_windowTask = Task.Run(() => _windowThreadMethodAsync(this));
		}

		public Window(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
		{
			_guid = System.Guid.NewGuid().ToString();

			_windowSystem = windowSystem;
			_parentWindow = parentWindow;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			// Set position relative to parent if this is a subwindow
			SetupInitialPosition();
		}

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegate windowThreadMethod, Window? parentWindow = null)
		{
			_guid = System.Guid.NewGuid().ToString();

			_windowSystem = windowSystem;
			_parentWindow = parentWindow;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			// Set position relative to parent if this is a subwindow
			SetupInitialPosition();

			_windowThreadMethod = windowThreadMethod;
			_windowThread = new Thread(() => _windowThreadMethod(this));
			_windowThread.Start();
		}

		public delegate void WindowThreadDelegate(Window window);

		// Window Thread Delegates
		public delegate Task WindowThreadDelegateAsync(Window window);

		// Events
		public event EventHandler? Activated;

		public event EventHandler? Deactivated;

		public event EventHandler<KeyPressedEventArgs>? KeyPressed;

		public event EventHandler? OnClosed;

		public event EventHandler<ClosingEventArgs>? OnCLosing;

		public event EventHandler? OnResize;

		public event EventHandler? OnShown;

		public event EventHandler<WindowStateChangedEventArgs>? StateChanged;

		public Color ActiveBorderForegroundColor
		{ get => _activeBorderForegroundColor ?? _windowSystem?.Theme.ActiveBorderForegroundColor ?? Color.White; set { _activeBorderForegroundColor = value; Invalidate(false); } }

		public Color ActiveTitleForegroundColor
		{ get => _activeTitleForegroundColor ?? _windowSystem?.Theme.ActiveTitleForegroundColor ?? Color.White; set { _activeTitleForegroundColor = value; Invalidate(false); } }

		public Color BackgroundColor { get; set; }

		public Color ForegroundColor { get; set; }

		public ConsoleWindowSystem? GetConsoleWindowSystem => _windowSystem;

		public string Guid => _guid.ToString();

		public int Height { get; set; } = 20;

		public Color InactiveBorderForegroundColor
		{ get => _inactiveBorderForegroundColor ?? _windowSystem?.Theme.InactiveBorderForegroundColor ?? Color.White; set { _inactiveBorderForegroundColor = value; Invalidate(false); } }

		public Color InactiveTitleForegroundColor
		{ get => _inactiveTitleForegroundColor ?? _windowSystem?.Theme.InactiveTitleForegroundColor ?? Color.White; set { _inactiveTitleForegroundColor = value; Invalidate(false); } }

		public bool IsClosable { get; set; } = true;

		public bool IsContentVisible { get; set; } = true;

		public bool IsDirty { get; set; } = true;

		public bool IsDragging { get; set; }

		public bool IsMovable { get; set; } = true;

		public bool IsResizable { get; set; } = true;

		public bool IsScrollable { get; set; } = true;

		public int Left { get; set; }

		public WindowMode Mode
		{
			get => _mode;
			set { _mode = value; }
		}

		public int OriginalHeight { get; set; }

		public int OriginalLeft { get; set; }

		public int OriginalTop { get; set; }

		public int OriginalWidth { get; set; }

		public Window? ParentWindow
		{
			get
			{
				return _parentWindow;
			}
		}

		public int ScrollOffset { get => _scrollOffset; set => _scrollOffset = value; }

		public WindowState State
		{
			get => _state;
			set
			{
				WindowState previous_state = _state;

				if (previous_state == value)
				{
					return;
				}

				_state = value;

				switch (value)
				{
					case WindowState.Minimized:
						Invalidate(true);
						break;

					case WindowState.Maximized:
						OriginalWidth = Width;
						OriginalHeight = Height;
						OriginalLeft = Left;
						OriginalTop = Top;
						Width = _windowSystem?.DesktopDimensions.Width ?? 80;
						Height = _windowSystem?.DesktopDimensions.Height ?? 24;
						Left = 0;
						Top = 0;
						Invalidate(true);
						break;

					case WindowState.Normal:
						if (previous_state == WindowState.Maximized)
						{
							Top = OriginalTop;
							Left = OriginalLeft;
							Width = OriginalWidth;
							Height = OriginalHeight;
							Invalidate(true);
						}
						break;
				}

				OnStateChanged(value);
			}
		}

		public object? Tag { get => _tag; set => _tag = value; }

		public string Title { get; set; } = "Window";

		public int Top { get; set; }

		public int TotalLines => _cachedContent.Count + _topStickyHeight;

		public int Width { get; set; } = 40;

		public int ZIndex { get; set; }

		public void AddContent(IWIndowControl content)
		{
			lock (_lock)
			{
				content.Container = this;
				_content.Add(content);

				_invalidated = true;

				if (content is IInteractiveControl interactiveContent)
				{
					_interactiveContents.Add(interactiveContent);

					if (_interactiveContents.Where(p => p.HasFocus).Count() == 0)
					{
						interactiveContent.HasFocus = true;
						_lastFocusedControl = interactiveContent;
					}
				}

				RenderAndGetVisibleContent();

				if (content.StickyPosition == StickyPosition.None && _interactiveContents.Where(p => p.HasFocus).Count() == 0) GoToBottom();
			}
		}

		public bool Close(bool systemCall = false)
		{
			if (IsClosable)
			{
				if (OnCLosing != null)
				{
					var args = new ClosingEventArgs();
					OnCLosing(this, args);
					if (!args.Allow)
					{
						return false;
					}
				}

				OnClosed?.Invoke(this, EventArgs.Empty);

				foreach (var content in _content)
				{
					(content as IWIndowControl).Dispose();
				}

				if (!systemCall) _windowSystem?.CloseWindow(this);

				return true;
			}

			return false;
		}

		public IWIndowControl? GetContentFromDesktopCoordinates(Point? point)
		{
			lock (_lock)
			{
				if (point == null) return null;

				// Translate the coordinates to the relative position within the window
				var relativePosition = _windowSystem?.TranslateToRelative(this, point);
				if (relativePosition == null)
				{
					return null;
				}

				return GetContentFromWindowCoordinates(relativePosition);
			}
		}

		public IWIndowControl? GetContentFromWindowCoordinates(Point? point)
		{
			lock (_lock)
			{
				if (point == null) return null;

				// Check if the coordinates are within the window bounds
				if (point?.X < 0 || point?.X >= Width || point?.Y < 0 || point?.Y >= Height)
				{
					return null;
				}

				// Calculate the content index based on the scroll offset
				int contentIndex = point?.Y ?? 0 + _scrollOffset;

				// Iterate through the content to find the one that matches the coordinates
				foreach (var content in _content)
				{
					int contentTop = _contentTopRowIndex[content];
					int contentBottom = contentTop + content.RenderContent(Width - 2, Height - 2).Count;

					if (contentTop <= contentIndex && contentIndex < contentBottom)
					{
						return content;
					}
				}

				return null;
			}
		}

		public bool GetIsActive()
		{
			return _isActive;
		}

		public void GoToBottom()
		{
			_scrollOffset = Math.Max(0, (_cachedContent?.Count ?? Height) - (Height - 2));
			Invalidate(true);
		}

		public void GoToTop()
		{
			_scrollOffset = 0;
			Invalidate(true);
		}

		public bool HasActiveInteractiveContent(out IInteractiveControl? interactiveContent)
		{
			interactiveContent = _interactiveContents.LastOrDefault(ic => ic.IsEnabled && ic.HasFocus);
			return interactiveContent != null;
		}

		public bool HasInteractiveContent(out Point cursorPosition)
		{
			if (HasActiveInteractiveContent(out var activeInteractiveContent))
			{
				(int, int)? currentCursorPosition = activeInteractiveContent!.GetCursorPosition();

				if (currentCursorPosition == null)
				{
					cursorPosition = new Point(0, 0);
					return false;
				}

				int left = currentCursorPosition?.Item1 ?? 0;
				int top = currentCursorPosition?.Item2 ?? 0;

				if (activeInteractiveContent != null && activeInteractiveContent is IWIndowControl)
				{
					IWIndowControl? content = activeInteractiveContent as IWIndowControl;

					cursorPosition = new Point(_contentLeftIndex[content!] + left + 1, _contentTopRowIndex[content!] + top + 1 - _scrollOffset);

					// Check if the cursor position is within the visible bounds
					if (cursorPosition.Y > _topStickyHeight && (activeInteractiveContent as IWIndowControl)?.StickyPosition == StickyPosition.Top)
					{
						return false;
					}

					if (cursorPosition.Y <= _topStickyHeight && (activeInteractiveContent as IWIndowControl)?.StickyPosition != StickyPosition.Top)
					{
						return false;
					}

					if (cursorPosition.Y > Height - 2 - _bottomStickyHeight) return false;

					return true;
				}
			}

			cursorPosition = new Point(0, 0);
			return false;
		}

		public void Invalidate(bool redrawAll, IWIndowControl? callerControl = null)
		{
			_invalidated = true;

			lock (_lock)
			{
				if (redrawAll)
				{
					foreach (var content in _content)
					{
						(content as IWIndowControl)?.Invalidate();
					}
				}
				else
				{
					(callerControl as IWIndowControl)?.Invalidate();
				}
			}

			IsDirty = true;
		}

		public void NotifyControlFocusLost(IInteractiveControl control)
		{
			if (control != null && _interactiveContents.Contains(control))
			{
				_lastFocusedControl = control;
			}
		}

		public bool ProcessInput(ConsoleKeyInfo key)
		{
			lock (_lock)
			{
				bool contentKeyHandled = false;
				bool windowHandled = false;

				if (HasActiveInteractiveContent(out var activeInteractiveContent))
				{
					contentKeyHandled = activeInteractiveContent!.ProcessKey(key);
				}

				// Continue with key handling only if not handled by the focused interactive content
				if (!contentKeyHandled)
				{
					if (key.Key == ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						SwitchFocus(true); // Pass true to indicate backward focus switch
						windowHandled = true;
					}
					else if (key.Key == ConsoleKey.Tab)
					{
						SwitchFocus(false); // Pass false to indicate forward focus switch
						windowHandled = true;
					}
					else
					{
						switch (key.Key)
						{
							case ConsoleKey.UpArrow:
								if (key.Modifiers != ConsoleModifiers.None) break;
								_scrollOffset = Math.Max(0, _scrollOffset - 1);
								IsDirty = true;
								windowHandled = true;
								break;

							case ConsoleKey.DownArrow:
								if (key.Modifiers != ConsoleModifiers.None) break;
								_scrollOffset = Math.Min((_cachedContent?.Count ?? Height) - (Height - 2 - _topStickyHeight), _scrollOffset + 1);
								IsDirty = true;
								windowHandled = true;
								break;
						}
					}
				}

				var handled = OnKeyPressed(key, contentKeyHandled || windowHandled);

				return (handled || contentKeyHandled || windowHandled);
			}
		}

		public void RemoveContent(IWIndowControl content)
		{
			lock (_lock)
			{
				if (_content.Remove(content))
				{
					if (content is IInteractiveControl interactiveContent)
					{
						_interactiveContents.Remove(interactiveContent);

						// If the removed content was the last focused control, clear it
						if (_lastFocusedControl == interactiveContent)
						{
							_lastFocusedControl = null;
						}

						// If the removed content had focus, switch focus to the next one
						if (interactiveContent.HasFocus && _interactiveContents.Count > 0)
						{
							_interactiveContents[0].HasFocus = true;
							_lastFocusedControl = _interactiveContents[0];
						}
					}
					_invalidated = true;
					RenderAndGetVisibleContent();
					content.Dispose();
					GoToBottom();
				}
			}
		}

		public List<string> RenderAndGetVisibleContent()
		{
			// Return empty list if window is minimized
			if (_state == WindowState.Minimized)
			{
				return new List<string>();
			}

			lock (_lock)
			{
				// Only recalculate content if it's been invalidated
				if (_invalidated)
				{
					List<string> lines = new List<string>();

					// Process top sticky content first to ensure it is always on top
					_topStickyHeight = 0;
					_topStickyLines.Clear();

					foreach (var content in _content.Where(c => c.StickyPosition == StickyPosition.Top && c.Visible == true))
					{
						// Store the top row index for the current content
						_contentTopRowIndex[content] = _topStickyHeight;
						_contentLeftIndex[content] = 0;

						// Get content's rendered lines
						var ansiLines = content.RenderContent(Width - 2, Height - 2);

						// Ensure proper formatting for each line
						for (int i = 0; i < ansiLines.Count; i++)
						{
							var line = ansiLines[i];
							ansiLines[i] = $"{line}";
						}

						_topStickyLines.AddRange(ansiLines);
						_topStickyHeight += ansiLines.Count;
					}

					// Process bottom sticky content last
					_bottomStickyHeight = 0;
					_bottomStickyLines.Clear();

					foreach (var content in _content.Where(c => c.StickyPosition == StickyPosition.Bottom && c.Visible == true))
					{
						// Track the position of sticky content
						_contentTopRowIndex[content] = lines.Count + _bottomStickyLines.Count;
						_contentLeftIndex[content] = 0;

						var ansiLines = content.RenderContent(Width - 2, Height - 2);

						for (int i = 0; i < ansiLines.Count; i++)
						{
							var line = ansiLines[i];
							ansiLines[i] = $"{line}";
						}

						_bottomStickyLines.AddRange(ansiLines);
						_bottomStickyHeight += ansiLines.Count;
					}

					// Process normal content next (non-sticky)
					foreach (var content in _content.Where(c => c.StickyPosition == StickyPosition.None && c.Visible == true))
					{
						// Store the top row index for the current content
						_contentTopRowIndex[content] = lines.Count + _topStickyHeight;
						_contentLeftIndex[content] = 0;

						// Get content's rendered lines
						var ansiLines = content.RenderContent(Width - 2, Height - 2 - _topStickyHeight - _bottomStickyHeight);

						// Ensure proper formatting for each line
						for (int i = 0; i < ansiLines.Count; i++)
						{
							var line = ansiLines[i];
							ansiLines[i] = $"{line}";
						}

						lines.AddRange(ansiLines);
					}

					// Reserve space for sticky content at the bottom
					lines.AddRange(Enumerable.Repeat(string.Empty, _bottomStickyHeight));

					_cachedContent = lines;
					_invalidated = false;
				}

				// Get visible portion based on scroll offset (accounting for window border)
				List<string> visibleContent = _cachedContent.Skip(_scrollOffset).Take(Height - 2).ToList();

				// Pad with empty lines if needed
				if (visibleContent.Count < Height - 2)
				{
					visibleContent.AddRange(Enumerable.Repeat(string.Empty, Height - 2 - visibleContent.Count));
				}

				// Replace the top placeholder lines with actual sticky content
				visibleContent.RemoveRange(visibleContent.Count - _topStickyHeight, _topStickyHeight);
				visibleContent.InsertRange(0, _topStickyLines);

				// Replace the bottom placeholder lines with actual sticky content
				visibleContent.RemoveRange(visibleContent.Count - _bottomStickyHeight, _bottomStickyHeight);
				visibleContent.AddRange(_bottomStickyLines);

				return visibleContent;
			}
		}

		public void Restore()
		{
		}

		public void SetIsActive(bool value)
		{
			if (value)
			{
				Activated?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				Deactivated?.Invoke(this, EventArgs.Empty);
			}

			_isActive = value;

			if (_lastFocusedControl != null)
			{
				_lastFocusedControl.HasFocus = value;
			}
		}

		public void SetPosition(Point point)
		{
			if (point.X < 0 || point.Y < 0) return;

			Left = point.X;
			Top = point.Y;
		}

		public void SetSize(int width, int height)
		{
			if (Width == width && Height == height)
			{
				return;
			}

			if (_minimumWidth != null && width < _minimumWidth)
			{
				width = (int)_minimumWidth;
			}

			if (_maximumWidth != null && width > _maximumWidth)
			{
				width = (int)_maximumWidth;
			}

			if (_minimumHeight != null && height < _minimumHeight)
			{
				height = (int)_minimumHeight;
			}

			if (_maximumHeight != null && height > _maximumHeight)
			{
				height = (int)_maximumHeight;
			}

			Width = width;
			Height = height;

			if (_scrollOffset > (_cachedContent?.Count ?? Height) - (Height - 2))
			{
				GoToBottom();
			}

			Invalidate(true);

			OnResize?.Invoke(this, EventArgs.Empty);
		}

		public void SwitchFocus(bool backward = false)
		{
			lock (_lock)
			{
				if (_interactiveContents.Count == 0) return;

				// Find the currently focused content
				var currentIndex = _interactiveContents.FindIndex(ic => ic.HasFocus);

				// If no control is focused but we have a last focused control, use that as a starting point
				if (currentIndex == -1 && _lastFocusedControl != null)
				{
					currentIndex = _interactiveContents.IndexOf(_lastFocusedControl);
				}

				// Remove focus from the current content if there is one
				if (currentIndex != -1)
				{
					_lastFocusedControl = _interactiveContents[currentIndex]; // Remember the last focused control
					_interactiveContents[currentIndex].HasFocus = false;
				}

				// Calculate the next index
				int nextIndex;
				if (backward)
				{
					nextIndex = (currentIndex - 1 + _interactiveContents.Count) % _interactiveContents.Count;
				}
				else
				{
					nextIndex = (currentIndex + 1) % _interactiveContents.Count;
				}

				// Set focus to the next content
				_interactiveContents[nextIndex].SetFocus(true, backward);
				_lastFocusedControl = _interactiveContents[nextIndex]; // Update last focused control

				BringIntoFocus(nextIndex);
			}
		}

		public void UnfocusCurrentControl()
		{
			if (_lastFocusedControl != null)
			{
				_lastFocusedControl.SetFocus(false, false);
			}
		}

		public void WindowIsAdded()
		{
			OnShown?.Invoke(this, EventArgs.Empty);
		}

		// Method to raise the KeyPressed event and return whether it was handled
		protected virtual bool OnKeyPressed(ConsoleKeyInfo key, bool allreadyHandled)
		{
			var handler = KeyPressed;
			if (handler != null)
			{
				var args = new KeyPressedEventArgs(key, allreadyHandled);
				handler(this, args);
				return args.Handled;
			}
			return false;
		}

		protected virtual void OnStateChanged(WindowState newState)
		{
			StateChanged?.Invoke(this, new WindowStateChangedEventArgs(newState));
		}

		private void BringIntoFocus(int nextIndex)
		{
			// Ensure the focused content is within the visible window
			var focusedContent = _interactiveContents[nextIndex] as IWIndowControl;

			if (focusedContent != null)
			{
				int contentTop = _contentTopRowIndex[focusedContent];
				int contentHeight = focusedContent.RenderContent(Width - 2, Height - 2).Count;
				int contentBottom = contentTop + contentHeight;

				if (focusedContent.StickyPosition == StickyPosition.None)
				{
					// Calculate the visible region boundaries
					int visibleTop = _scrollOffset + _topStickyHeight;
					int visibleBottom = _scrollOffset + (Height - 2 - _bottomStickyHeight);

					if (contentTop < visibleTop)
					{
						// Ensure we never set a negative scroll offset
						_scrollOffset = Math.Max(0, contentTop - _topStickyHeight);
					}
					else if (contentBottom > visibleBottom)
					{
						// Calculate how much we need to scroll to show the bottom of the content
						int newOffset = contentBottom - (Height - 2 - _bottomStickyHeight);

						// Ensure we don't scroll beyond the maximum available content
						int maxOffset = Math.Max(0, (_cachedContent?.Count ?? 0) - (Height - 2 - _topStickyHeight));
						_scrollOffset = Math.Min(newOffset, maxOffset);
					}
				}
			}

			// Invalidate the window to update the display
			Invalidate(true);
		}

		// Helper method to set up initial position for subwindows
		private void SetupInitialPosition()
		{
			// Only set position if this is a subwindow and both Left and Top are at their default values (0)
			if (_parentWindow != null && Left == 0 && Top == 0)
			{
				// Position the subwindow in the center of the parent window
				int parentCenterX = _parentWindow.Left + (_parentWindow.Width / 2);
				int parentCenterY = _parentWindow.Top + (_parentWindow.Height / 2);

				// Center this window on the parent's center
				Left = Math.Max(0, parentCenterX - (Width / 2));
				Top = Math.Max(0, parentCenterY - (Height / 2));

				// If we're a modal window, ensure we're visible and properly centered
				if (Mode == WindowMode.Modal)
				{
					// Use a smaller offset for modal windows to make them look more like dialogs
					// Ensure the window fits within the parent window bounds
					Left = Math.Max(0, _parentWindow.Left + 5);
					Top = Math.Max(0, _parentWindow.Top + 3);

					// Make sure the window isn't too large for the parent
					if (_windowSystem != null)
					{
						// Make sure the window fits on the screen
						if (Left + Width > _windowSystem.DesktopBottomRight.X)
							Left = Math.Max(0, _windowSystem.DesktopBottomRight.X - Width);

						if (Top + Height > _windowSystem.DesktopBottomRight.Y)
							Top = Math.Max(0, _windowSystem.DesktopBottomRight.Y - Height);
					}
				}
			}
		}

		public class WindowStateChangedEventArgs : EventArgs
		{
			public WindowStateChangedEventArgs(WindowState newState)
			{
				NewState = newState;
			}

			public WindowState NewState { get; }
		}
	}
}
