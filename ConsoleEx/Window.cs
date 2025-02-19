﻿using ConsoleEx.Contents;
using Spectre.Console;

namespace ConsoleEx
{
	public enum WindowState
	{
		Normal,
		Minimized,
		Maximized
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

		private int _bottomStickyHeight;
		private List<string> _bottomStickyLines = new List<string>();

		// List to store interactive contents
		private List<string> _cachedContent = new();

		private Dictionary<IWIndowContent, int> _contentLeftIndex = new();

		private Dictionary<IWIndowContent, int> _contentTopRowIndex = new();

		private bool _invalidated = false;

		private bool _isActive;
		private int _scrollOffset;
		private WindowState _state;
		private ConsoleWindowSystem? _windowSystem;
		private Task? _windowTask;
		private Thread? _windowThread;
		private WindowThreadDelegate? _windowThreadMethod;

		private WindowThreadDelegateAsync? _windowThreadMethodAsync;

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegateAsync windowThreadMethod)
		{
			_windowSystem = windowSystem;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			_windowThreadMethodAsync = windowThreadMethod;
			_windowTask = Task.Run(() => _windowThreadMethodAsync(this));
		}

		public Window(ConsoleWindowSystem windowSystem)
		{
			_windowSystem = windowSystem;

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;
		}

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegate windowThreadMethod)
		{
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

		public event EventHandler<WindowStateChangedEventArgs>? StateChanged;

		public Color BackgroundColor { get; set; }
		public Color ForegroundColor { get; set; }
		public ConsoleWindowSystem? GetConsoleWindowSystem => _windowSystem;
		public int Height { get; set; } = 20;
		public bool IsClosable { get; set; } = true;
		public bool IsContentVisible { get; set; } = true;
		public bool IsDirty { get; set; } = true;
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
						Invalidate();
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
						Invalidate();
						break;

					case WindowState.Normal:
						if (previous_state == WindowState.Maximized)
						{
							Top = OriginalTop;
							Left = OriginalLeft;
							Width = OriginalWidth;
							Height = OriginalHeight;
							Invalidate();
						}
						break;
				}

				OnStateChanged(value);
			}
		}

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
				if (content is IInteractiveContent interactiveContent)
				{
					_interactiveContents.Add(interactiveContent);
					// Set focus to the first interactive content if none has focus
					if (_interactiveContents.Count == 1)
					{
						interactiveContent.HasFocus = true;
					}
				}
				RenderAndGetVisibleContent();
				GoToBottom();
			}
		}

		public void Close()
		{
			if (IsClosable)
			{
				foreach (var content in _content)
				{
					(content as IWIndowContent).Dispose();
				}

				_windowSystem?.CloseWindow(this);
			}
		}

		public bool GetIsActive()
		{
			return _isActive;
		}

		public void GoToBottom()
		{
			_scrollOffset = Math.Max(0, (_cachedContent?.Count ?? Height) - (Height - 2));
			Invalidate();
		}

		public void GoToTop()
		{
			_scrollOffset = 0;
			Invalidate();
		}

		public bool HasActiveInteractiveContent(out IInteractiveContent? interactiveContent)
		{
			interactiveContent = _interactiveContents.LastOrDefault(ic => ic.IsEnabled && ic.HasFocus);
			return interactiveContent != null;
		}

		public bool HasInteractiveContent(out (int Left, int Top) cursorPosition)
		{
			if (HasActiveInteractiveContent(out var activeInteractiveContent))
			{
				(int, int)? currentCursorPosition = activeInteractiveContent!.GetCursorPosition();

				if (currentCursorPosition == null)
				{
					cursorPosition = (0, 0);
					return false;
				}

				int left = currentCursorPosition?.Item1 ?? 0;
				int top = currentCursorPosition?.Item2 ?? 0;

				if (activeInteractiveContent != null && activeInteractiveContent is IWIndowContent)
				{
					IWIndowContent? content = activeInteractiveContent as IWIndowContent;

					cursorPosition = (_contentLeftIndex[content!] + left + 1, _contentTopRowIndex[content!] + top + 1 - _scrollOffset);
					return true;
				}
			}

			cursorPosition = (0, 0);
			return false;
		}

		public void Invalidate()
		{
			_invalidated = true;
			foreach (var content in _content)
			{
				(content as IWIndowContent)?.Invalidate();
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
					switch (key.Key)
					{
						case ConsoleKey.Tab:
							SwitchFocus();
							handled = true;
							break;

						case ConsoleKey.UpArrow:
							_scrollOffset = Math.Max(0, _scrollOffset - 1);
							IsDirty = true;
							handled = true;
							break;

						case ConsoleKey.DownArrow:
							_scrollOffset = Math.Min((_cachedContent?.Count ?? Height) - (Height - 2), _scrollOffset + 1);
							IsDirty = true;
							handled = true;
							break;
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

		public void SetSize(int width, int height)
		{
			Width = width;
			Height = height;

			if (_scrollOffset > (_cachedContent?.Count ?? Height) - (Height - 2))
			{
				GoToBottom();
			}

			Invalidate();
		}

		public void SwitchFocus()
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
				var nextIndex = (currentIndex + 1) % _interactiveContents.Count;

				// Set focus to the next content
				_interactiveContents[nextIndex].HasFocus = true;

				// Ensure the focused content is within the visible window
				var focusedContent = _interactiveContents[nextIndex] as IWIndowContent;
				if (focusedContent != null)
				{
					int contentTop = _contentTopRowIndex[focusedContent];
					int contentBottom = contentTop + focusedContent.RenderContent(Width - 2, Height - 2).Count;

					if (contentTop < _scrollOffset)
					{
						// Scroll up to make the top of the content visible
						_scrollOffset = contentTop;
					}
					else if (contentBottom > _scrollOffset + (Height - 2))
					{
						// Scroll down to make the bottom of the content visible
						_scrollOffset = contentBottom - (Height - 2);
					}
				}

				// Invalidate the window to update the display
				Invalidate();
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
