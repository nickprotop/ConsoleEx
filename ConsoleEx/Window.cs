// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Contents;
using ConsoleEx.Helpers;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace ConsoleEx
{
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
		public KeyPressedEventArgs(ConsoleKeyInfo keyInfo)
		{
			KeyInfo = keyInfo;
		}

		public bool Handled { get; set; }
		public ConsoleKeyInfo KeyInfo { get; }
	}

	public class Window : IContainer
	{
		private readonly List<IWIndowContent> _content = new();
		private readonly List<IInteractiveContent> _interactiveContents = new();
		private readonly object _lock = new();

		private Color? _activeBorderForegroundColor;
		private Color? _activeTitleForegroundColor;
		private int _bottomStickyHeight;
		private List<string> _bottomStickyLines = new List<string>();

		// List to store interactive contents
		private List<string> _cachedContent = new();

		private Dictionary<IWIndowContent, int> _contentLeftIndex = new();

		private Dictionary<IWIndowContent, int> _contentTopRowIndex = new();

		private string _guid;
		private Color? _inactiveBorderForegroundColor;
		private Color? _inactiveTitleForegroundColor;
		private bool _invalidated = false;

		private bool _isActive;
		private int? _maximumHeight;
		private int? _maximumWidth;
		private int? _minimumHeight = 3;
		private int? _minimumWidth = 10;
		private int _scrollOffset;
		private WindowState _state;
		private object? _tag;
		private ConsoleWindowSystem? _windowSystem;
		private Task? _windowTask;
		private Thread? _windowThread;
		private WindowThreadDelegate? _windowThreadMethod;
		private WindowThreadDelegateAsync? _windowThreadMethodAsync;

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegateAsync windowThreadMethod)
		{
			_guid = System.Guid.NewGuid().ToString();

			_windowSystem = windowSystem;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			_windowThreadMethodAsync = windowThreadMethod;
			_windowTask = Task.Run(() => _windowThreadMethodAsync(this));
		}

		public Window(ConsoleWindowSystem windowSystem)
		{
			_guid = System.Guid.NewGuid().ToString();

			_windowSystem = windowSystem;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;
		}

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegate windowThreadMethod)
		{
			_guid = System.Guid.NewGuid().ToString();

			_windowSystem = windowSystem;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			_windowThreadMethod = windowThreadMethod;
			_windowThread = new Thread(() => _windowThreadMethod(this));
			_windowThread.Start();
		}

		public delegate void WindowThreadDelegate(Window window);

		// Window Thread Delegates
		public delegate Task WindowThreadDelegateAsync(Window window);

		// Events
		public event EventHandler<bool>? ActivationChanged;

		public event EventHandler<KeyPressedEventArgs>? KeyPressed;

		public event EventHandler? OnClosed;

		public event EventHandler<ClosingEventArgs>? OnCLosing;

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
		public int OriginalHeight { get; set; }
		public int OriginalLeft { get; set; }
		public int OriginalTop { get; set; }
		public int OriginalWidth { get; set; }
		public int ScrollOffset => _scrollOffset;

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
		public int TotalLines => _cachedContent.Count;
		public int Width { get; set; } = 40;
		public int ZIndex { get; set; }

		public void AddContent(IWIndowContent content)
		{
			lock (_lock)
			{
				content.Container = this;
				_content.Add(content);

				_invalidated = true;

				if (content is IInteractiveContent interactiveContent)
				{
					_interactiveContents.Add(interactiveContent);

					if (_interactiveContents.Where(p => p.HasFocus).Count() == 0)
					{
						interactiveContent.HasFocus = true;
					}
				}

				RenderAndGetVisibleContent();

				if (content.StickyPosition == StickyPosition.None) GoToBottom();
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

				foreach (var content in _content)
				{
					(content as IWIndowContent).Dispose();
				}

				if (!systemCall) _windowSystem?.CloseWindow(this);
				OnClosed?.Invoke(this, EventArgs.Empty);

				return true;
			}

			return false;
		}

		public IWIndowContent? GetContentFromDesktopCoordinates(Point? point)
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

		public IWIndowContent? GetContentFromWindowCoordinates(Point? point)
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

		public bool HasActiveInteractiveContent(out IInteractiveContent? interactiveContent)
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

				if (activeInteractiveContent != null && activeInteractiveContent is IWIndowContent)
				{
					IWIndowContent? content = activeInteractiveContent as IWIndowContent;

					cursorPosition = new Point(_contentLeftIndex[content!] + left + 1, _contentTopRowIndex[content!] + top + 1 - _scrollOffset);

					// Check if the cursor position is within the visible bounds
					if (cursorPosition.Y >= 0 && cursorPosition.Y < Height - 1 - _bottomStickyHeight)
					{
						return true;
					}

					return false;
				}
			}

			cursorPosition = new Point(0, 0);
			return false;
		}

		public void Invalidate(bool redrawAll)
		{
			_invalidated = true;
			if (redrawAll)
			{
				foreach (var content in _content)
				{
					(content as IWIndowContent)?.Invalidate();
				}
			}

			IsDirty = true;
		}

		public bool ProcessInput(ConsoleKeyInfo key)
		{
			lock (_lock)
			{
				bool contentKeyHandled = false;

				if (HasActiveInteractiveContent(out var activeInteractiveContent))
				{
					contentKeyHandled = activeInteractiveContent!.ProcessKey(key);
				}

				if (contentKeyHandled)
				{
					return true;
				}

				// Raise the KeyPressed event
				var handled = OnKeyPressed(key);

				// Continue with key handling only if not handled by the user
				if (!handled)
				{
					if (key.Key == ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						SwitchFocus(true); // Pass true to indicate backward focus switch
						handled = true;
					}
					else if (key.Key == ConsoleKey.Tab)
					{
						SwitchFocus(false); // Pass false to indicate forward focus switch
						handled = true;
					}
					else
					{
						switch (key.Key)
						{
							case ConsoleKey.UpArrow:
								if (key.Modifiers != ConsoleModifiers.None) break;
								_scrollOffset = Math.Max(0, _scrollOffset - 1);
								IsDirty = true;
								handled = true;
								break;

							case ConsoleKey.DownArrow:
								if (key.Modifiers != ConsoleModifiers.None) break;
								_scrollOffset = Math.Min((_cachedContent?.Count ?? Height) - (Height - 2), _scrollOffset + 1);
								IsDirty = true;
								handled = true;
								break;
						}
					}
				}

				return handled;
			}
		}

		public void RemoveContent(IWIndowContent content)
		{
			lock (_lock)
			{
				if (_content.Remove(content))
				{
					if (content is IInteractiveContent interactiveContent)
					{
						_interactiveContents.Remove(interactiveContent);
						// If the removed content had focus, switch focus to the next one
						if (interactiveContent.HasFocus && _interactiveContents.Count > 0)
						{
							_interactiveContents[0].HasFocus = true;
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
			if (_state == WindowState.Minimized)
			{
				return new List<string>();
			}

			lock (_lock)
			{
				if (_invalidated)
				{
					List<string> lines = new List<string>();

					foreach (var content in _content.Where(c => c.StickyPosition != StickyPosition.Bottom))
					{
						// Store the top row index for the current content
						_contentTopRowIndex[content] = lines.Count;

						// Store the left index for the current content
						_contentLeftIndex[content] = 0;

						var ansiLines = content.RenderContent(Width - 2, Height - 2);

						for (int i = 0; i < ansiLines.Count; i++)
						{
							var line = ansiLines[i];
							ansiLines[i] = $"{line}";
						}

						lines.AddRange(ansiLines);
					}

					_bottomStickyHeight = 0;
					_bottomStickyLines.Clear();

					foreach (var content in _content.Where(c => c.StickyPosition == StickyPosition.Bottom))
					{
						// Store the top row index for the current content
						_contentTopRowIndex[content] = lines.Count + _bottomStickyLines.Count;

						// Store the left index for the current content
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

					// lines.AddRange(bottomStickyLines);
					lines.AddRange(Enumerable.Repeat(string.Empty, _bottomStickyHeight));

					_cachedContent = lines;
					_invalidated = false;
				}

				List<string> visibleContent = _cachedContent.Skip(_scrollOffset).Take(Height - 2).ToList();

				if (visibleContent.Count < Height - 2)
				{
					visibleContent.AddRange(Enumerable.Repeat(string.Empty, Height - 2 - visibleContent.Count));
				}

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
			ActivationChanged?.Invoke(this, value);
			_isActive = value;
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
		}

		public void SwitchFocus(bool backward = false)
		{
			lock (_lock)
			{
				if (_interactiveContents.Count == 0) return;

				// Find the currently focused content
				var currentIndex = _interactiveContents.FindIndex(ic => ic.HasFocus);

				// Remove focus from the current content
				if (currentIndex != -1)
				{
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

				BringIntoFocus(nextIndex);
			}
		}

		// Method to raise the KeyPressed event and return whether it was handled
		protected virtual bool OnKeyPressed(ConsoleKeyInfo key)
		{
			var handler = KeyPressed;
			if (handler != null)
			{
				var args = new KeyPressedEventArgs(key);
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
			var focusedContent = _interactiveContents[nextIndex] as IWIndowContent;

			if (focusedContent != null)
			{
				if (focusedContent.StickyPosition == StickyPosition.None)
				{
					int contentTop = _contentTopRowIndex[focusedContent];
					int contentBottom = contentTop + focusedContent.RenderContent(Width - 2, Height - 2).Count;

					if (contentTop < _scrollOffset)
					{
						// Scroll up to make the top of the content visible
						_scrollOffset = contentTop;
					}
					else if (contentBottom > _scrollOffset + (Height - 2 - _bottomStickyHeight))
					{
						// Scroll down to make the bottom of the content visible, considering sticky bottom height
						_scrollOffset = contentBottom - (Height - 2 - _bottomStickyHeight);
					}
				}
			}

			// Invalidate the window to update the display
			Invalidate(true);
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