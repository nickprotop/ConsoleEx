// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
	public class Window
	{
		private readonly object _lock = new();
		private readonly List<IWIndowContent> _content = new();
		private List<string> _renderedContent = new();
		private int _scrollOffset;
		private bool _invalidated = false;
		private Task? _windowTask;
		private Thread? _windowThread;

		public delegate Task WindowThreadDelegateAsync(Window window);

		private WindowThreadDelegateAsync? _windowThreadMethodAsync;

		public delegate void WindowThreadDelegate(Window window);

		private WindowThreadDelegate? _windowThreadMethod;

		public int OriginalWidth { get; set; }
		public int OriginalHeight { get; set; }
		public int OriginalLeft { get; set; }
		public int OriginalTop { get; set; }

		public string Title { get; set; } = "Window";
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; } = 40;
		public int Height { get; set; } = 20;
		public bool IsActive { get; set; }
		public bool IsVisible { get; set; } = true;
		public bool IsContentVisible { get; set; } = true;
		public bool Maximized { get; set; }
		public bool IsDirty { get; set; } = true;
		public int ZIndex { get; set; }

		private ConsoleWindowSystem? _windowSystem;

		// Define the event for key presses
		public event EventHandler<KeyPressedEventArgs>? KeyPressed;

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegateAsync windowThreadMethod)
		{
			_windowSystem = windowSystem;
			_windowThreadMethodAsync = windowThreadMethod;
			_windowTask = Task.Run(() => _windowThreadMethodAsync(this));
		}

		public Window(ConsoleWindowSystem windowSystem)
		{
			_windowSystem = windowSystem;
		}

		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegate windowThreadMethod)
		{
			_windowSystem = windowSystem;
			_windowThreadMethod = windowThreadMethod;
			_windowThread = new Thread(() => _windowThreadMethod(this));
			_windowThread.Start();
		}

		public void Invalidate()
		{
			_invalidated = true;
			IsDirty = true;
		}

		public void Show()
		{
			IsVisible = true;
			Invalidate();
		}

		public void Close()
		{
			_windowSystem?.CloseWindow(this);
		}

		public void Hide()
		{
			IsVisible = false;
			Invalidate();
		}

		public void RemoveContent(IWIndowContent content)
		{
			lock (_lock)
			{
				if (_content.Remove(content))
				{
					content.Container = null;
					RenderContent();
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
				RenderContent();
				_scrollOffset = Math.Max(0, (_renderedContent?.Count ?? Height) - (Height - 2));
				IsDirty = true;
			}
		}

		public bool ProcessInput(ConsoleKeyInfo key)
		{
			lock (_lock)
			{
				bool contentKeyHandled = false;

				if (_content.Any(_content => _content.IsInteractive))
				{
					contentKeyHandled = _content.Last(_content => _content.IsInteractive).ProcessKey(key);
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

		public List<string> GetVisibleContent()
		{
			if (_invalidated)
			{
				RenderContent();
				_invalidated = false;
			}

			return _renderedContent?.Skip(_scrollOffset)?.Take(Height - 2)?.ToList() ?? new List<string>();
		}

		public void RenderContent()
		{
			if (!IsVisible)
			{
				return;
			}

			lock (_lock)
			{
				List<string> lines = new List<string>();

				var linesCount = 0;

				foreach (var content in _content)
				{
					var ansiLines = content.RenderContent(Width - 2, Height - 2, true);
					lines.AddRange(ansiLines);

					linesCount = lines.Count - _scrollOffset;
				}

				_renderedContent = lines;
			}
		}
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
