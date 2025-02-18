using Spectre.Console;

namespace ConsoleEx
{
	public enum WindowState
	{
		Normal,
		Minimized,
		Maximized
	}

	public class WindowStateChangedEventArgs : EventArgs
	{
		public WindowState NewState { get; }

		public WindowStateChangedEventArgs(WindowState newState)
		{
			NewState = newState;
		}
	}

	public class Window : IContainer
	{
		private readonly object _lock = new();
		private readonly List<IWIndowContent> _content = new();
		private readonly List<IInteractiveContent> _interactiveContents = new(); // List to store interactive contents
		private List<string> _renderedContent = new();
		private int _scrollOffset;
		private bool _invalidated = false;
		private Task? _windowTask;
		private Thread? _windowThread;
		private ConsoleWindowSystem? _windowSystem;
		private bool _isActive;
		private WindowState _state;

		private Dictionary<IWIndowContent, int> _contentTopRowIndex = new();
		private Dictionary<IWIndowContent, int> _contentLeftIndex = new();

		// Window Thread Delegates
		public delegate Task WindowThreadDelegateAsync(Window window);
		private WindowThreadDelegateAsync? _windowThreadMethodAsync;
		public delegate void WindowThreadDelegate(Window window);
		private WindowThreadDelegate? _windowThreadMethod;

		// Events
		public event EventHandler<bool>? ActivationChanged;
		public event EventHandler<WindowStateChangedEventArgs>? StateChanged;
		public event EventHandler<KeyPressedEventArgs>? KeyPressed;

		public int OriginalWidth { get; set; }
		public int OriginalHeight { get; set; }
		public int OriginalLeft { get; set; }
		public int OriginalTop { get; set; }

		public string Title { get; set; } = "Window";
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; } = 40;
		public int Height { get; set; } = 20;
		public bool IsContentVisible { get; set; } = true;
		public bool IsDirty { get; set; } = true;
		public int ZIndex { get; set; }
		public bool IsResizable { get; set; } = true;
		public bool IsMovable { get; set; } = true;
		public bool IsScrollable { get; set; } = true;
		public int ScrollOffset => _scrollOffset;
		public Color BackgroundColor { get; set; } = Color.Black;
		public Color ForegroundColor { get; set; } = Color.White;
		public int TotalLines => _renderedContent.Count;

		public bool GetIsActive()
		{
			return _isActive;
		}

		public void SetIsActive(bool value)
		{
			ActivationChanged?.Invoke(this, value);
			_isActive = value;
		}

		protected virtual void OnStateChanged(WindowState newState)
		{
			StateChanged?.Invoke(this, new WindowStateChangedEventArgs(newState));
		}

		private void ApplyWindowOptions(WindowOptions windowOptions)
		{
			Title = windowOptions.Title;
			Top = windowOptions.Top;
			Left = windowOptions.Left;
			Width = windowOptions.Width;
			Height = windowOptions.Height;
			IsResizable = windowOptions.IsResizable;
			IsMovable = windowOptions.IsMoveable;
			BackgroundColor = windowOptions.BackgroundColor;
			ForegroundColor = windowOptions.ForegroundColor;
			_state = windowOptions.WindowState;
		}

		public Window(ConsoleWindowSystem windowSystem, WindowOptions options, WindowThreadDelegateAsync windowThreadMethod)
		{
			_windowSystem = windowSystem;
			ApplyWindowOptions(options);
			_windowThreadMethodAsync = windowThreadMethod;
			_windowTask = Task.Run(() => _windowThreadMethodAsync(this));
		}

		public Window(ConsoleWindowSystem windowSystem)
		{
			_windowSystem = windowSystem;
		}

		public Window(ConsoleWindowSystem windowSystem, WindowOptions options, WindowThreadDelegate windowThreadMethod)
		{
			_windowSystem = windowSystem;
			ApplyWindowOptions(options);
			_windowThreadMethod = windowThreadMethod;
			_windowThread = new Thread(() => _windowThreadMethod(this));
			_windowThread.Start();
		}

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

		public void Restore()
		{
		}

		public Window(ConsoleWindowSystem windowSystem, WindowOptions options)
		{
			_windowSystem = windowSystem;
			ApplyWindowOptions(options);
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

		public void Close()
		{
			foreach (var content in _content)
			{
				(content as IWIndowContent).Dispose();
			}

			_windowSystem?.CloseWindow(this);
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
					RenderWindowContent();
					content.Dispose();
					_scrollOffset = Math.Max(0, (_renderedContent?.Count ?? Height) - (Height - 2));
					IsDirty = true;
				}
			}
		}

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
				RenderWindowContent();
				_scrollOffset = Math.Max(0, (_renderedContent?.Count ?? Height) - (Height - 2));
				IsDirty = true;
			}
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

				// Invalidate the window to update the display
				Invalidate();
			}
		}

		public bool HasActiveInteractiveContent(out IInteractiveContent? interactiveContent)
		{
			interactiveContent = _interactiveContents.LastOrDefault(ic => ic.IsEnabled && ic.HasFocus);
			return interactiveContent != null;
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
							_scrollOffset = Math.Min((_renderedContent?.Count ?? Height) - (Height - 2), _scrollOffset + 1);
							IsDirty = true;
							handled = true;
							break;
					}
				}

				return handled;
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

		public List<string> GetWindowContent()
		{
			if (_invalidated)
			{
				RenderWindowContent();
				_invalidated = false;
			}

			return _renderedContent?.Skip(_scrollOffset)?.Take(Height - 2)?.ToList() ?? new List<string>();
		}

		private void RenderWindowContent()
		{
			if (_state == WindowState.Minimized)
			{
				return;
			}

			lock (_lock)
			{
				List<string> lines = new List<string>();

				foreach (var content in _content)
				{
					// Store the top row index for the current content
					_contentTopRowIndex[content] = lines.Count;

					// Store the left index for the current content
					_contentLeftIndex[content] = 0;

					var ansiLines = content.RenderContent(Width - 2, Height - 2);

					int contentWidth = content.ActualWidth ?? Width - 2;
					int paddingLeft = 0;
					if (content.Alignment == Alignment.Center)
					{
						// make the left padding to round to the less nearing integer
						paddingLeft = (Width - 2 - contentWidth) / 2;
						if (paddingLeft < 0) paddingLeft = 0;
					}

					for (int i = 0; i < ansiLines.Count; i++)
					{
						var line = ansiLines[i];
						ansiLines[i] = $"{AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(new string(' ', paddingLeft), paddingLeft, 1, false, BackgroundColor, null).FirstOrDefault()}{line}";
					}

					lines.AddRange(ansiLines);
				}

				_renderedContent = lines;
			}
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

				if (activeInteractiveContent != null && activeInteractiveContent is IWIndowContent)
				{
					cursorPosition = (_contentLeftIndex[activeInteractiveContent as IWIndowContent] + (int)currentCursorPosition?.Item1 + 1, _contentTopRowIndex[activeInteractiveContent as IWIndowContent] + (int)currentCursorPosition?.Item2 + 1 - _scrollOffset);
					return true;
				}
			}

			cursorPosition = (0, 0);
			return false;
		}

		// Custom EventArgs class to indicate whether the event was handled
		public class KeyPressedEventArgs : EventArgs
		{
			public ConsoleKeyInfo KeyInfo { get; }
			public bool Handled { get; set; }

			public KeyPressedEventArgs(ConsoleKeyInfo keyInfo)
			{
				KeyInfo = keyInfo;
			}
		}
	}
}
