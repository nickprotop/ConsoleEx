// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI
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
		private readonly List<IWIndowControl> _controls = new();
		private readonly List<IInteractiveControl> _interactiveContents = new();
		private readonly object _lock = new();

		private readonly Window? _parentWindow;
		private readonly WindowLayoutManager _layoutManager;
		private Color? _activeBorderForegroundColor;
		private Color? _activeTitleForegroundColor;
		private int _bottomStickyHeight;
		private List<string> _bottomStickyLines = new List<string>();

		// List to store interactive contents
		private List<string> _cachedContent = new();

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
			_layoutManager = new WindowLayoutManager(this);

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
			_layoutManager = new WindowLayoutManager(this);

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

		public void AddControl(IWIndowControl content)
		{
			lock (_lock)
			{
				content.Container = this;
				_controls.Add(content);

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

		public void ClearControls()
		{
			lock (_lock)
			{
				foreach (var content in _controls.ToList())
				{
					RemoveContent(content);
				}
				Invalidate(true);
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

				foreach (var content in _controls)
				{
					(content as IWIndowControl).Dispose();
				}

				if (!systemCall) _windowSystem?.CloseWindow(this);

				return true;
			}

			return false;
		}

		public bool ContainsControl(IWIndowControl content)
		{
			lock (_lock)
			{
				return _controls.Contains(content);
			}
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

				// Check if the coordinates are within the window content bounds
				if (point?.X < 1 || point?.X >= Width - 1 || point?.Y < 1 || point?.Y >= Height - 1)
				{
					return null;
				}

				// Convert to content coordinates (remove border offset)
				var contentPoint = new Point(point.Value.X - 1, point.Value.Y - 1);

				// Check each control's bounds using the layout manager
				foreach (var control in _controls.Where(c => c.Visible))
				{
					var bounds = _layoutManager.GetOrCreateControlBounds(control);
					if (bounds.ControlContentBounds.Contains(contentPoint))
					{
						return control;
					}
				}

				return null;
			}
		}

		/// <summary>
		/// Handles mouse events for this window and propagates them to controls
		/// </summary>
		/// <param name="args">Mouse event arguments with window-relative coordinates</param>
		/// <returns>True if the event was handled</returns>
		public bool ProcessWindowMouseEvent(Events.MouseEventArgs args)
		{
			lock (_lock)
			{
				// Check if the click is within the window content area (not borders/title)
				if (IsClickInWindowContent(args.WindowPosition))
				{
					// Find the control at this position
					var targetControl = GetContentFromWindowCoordinates(GetContentCoordinates(args.WindowPosition));
					
					if (targetControl != null)
					{
						// Handle focus management for mouse clicks
						if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked))
						{
							HandleControlFocusFromMouse(targetControl);
						}

						// Propagate mouse event to control if it supports mouse events
						if (targetControl is Controls.IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
						{
							// Calculate control-relative coordinates
							var controlPosition = GetControlRelativePosition(targetControl, args.WindowPosition);
							var controlArgs = args.WithPosition(controlPosition);
							
							return mouseAware.ProcessMouseEvent(controlArgs);
						}
						
						// Event was handled by focus change even if control doesn't support mouse
						return true;
					}
				}
				
				return false; // Event not handled
			}
		}

		/// <summary>
		/// Checks if a window-relative position is within the content area (not title bar or borders)
		/// </summary>
		private bool IsClickInWindowContent(Point windowPosition)
		{
			// Window content starts at (1,1) to account for borders
			// Title bar is at Y=0, so content starts at Y=1
			return windowPosition.X >= 1 && windowPosition.X < Width - 1 &&
				   windowPosition.Y >= 1 && windowPosition.Y < Height - 1;
		}

		/// <summary>
		/// Converts window-relative coordinates to content coordinates (accounting for borders and scroll)
		/// </summary>
		private Point GetContentCoordinates(Point windowPosition)
		{
			// Subtract border offset and add scroll offset
			return new Point(windowPosition.X - 1, windowPosition.Y - 1 + _scrollOffset);
		}

		/// <summary>
		/// Calculates the position relative to a specific control using the new layout system
		/// </summary>
		private Point GetControlRelativePosition(IWIndowControl control, Point windowPosition)
		{
			// Use the new layout manager for coordinate translation
			var bounds = _layoutManager.GetOrCreateControlBounds(control);
			return bounds.WindowToControl(windowPosition);
		}

		/// <summary>
		/// Handles focus management when a control is clicked
		/// </summary>
		private void HandleControlFocusFromMouse(IWIndowControl control)
		{
			// Check if control can receive focus
			if (control is Controls.IFocusableControl focusable && focusable.CanReceiveFocus)
			{
				// Remove focus from current control
				if (_lastFocusedControl is IInteractiveControl currentFocused && currentFocused != focusable)
				{
					currentFocused.SetFocus(false, false);
				}
				
				// Set focus to new control
				focusable.SetFocus(true, Controls.FocusReason.Mouse);
				_lastFocusedControl = focusable as IInteractiveControl;
			}
		}

		/// <summary>
		/// Updates the layout manager with current control positions and bounds
		/// </summary>
		private void UpdateControlLayout()
		{
			lock (_lock)
			{
				var availableWidth = Width - 2; // Account for borders
				var availableHeight = Height - 2; // Account for borders
				
				var currentTopOffset = 0;
				var currentBottomOffset = 0;
				
				// Calculate sticky top controls positions
				foreach (var control in _controls.Where(c => c.StickyPosition == StickyPosition.Top && c.Visible))
				{
					var bounds = _layoutManager.GetOrCreateControlBounds(control);
					var renderedContent = control.RenderContent(availableWidth, availableHeight);
					
					bounds.ControlContentBounds = new Rectangle(
						0, // Always left-aligned 
						currentTopOffset,
						control.ActualWidth ?? (renderedContent.FirstOrDefault()?.Length ?? 0),
						renderedContent.Count
					);
					
					bounds.ViewportSize = new Size(availableWidth, availableHeight);
					bounds.HasInternalScrolling = control is MultilineEditControl;
					bounds.ScrollOffset = Point.Empty; // Sticky controls don't scroll
					bounds.IsVisible = true;
					
					currentTopOffset += renderedContent.Count;
				}
				
				// Calculate sticky bottom controls positions (working backwards)
				var bottomControls = _controls.Where(c => c.StickyPosition == StickyPosition.Bottom && c.Visible).Reverse().ToList();
				foreach (var control in bottomControls)
				{
					var bounds = _layoutManager.GetOrCreateControlBounds(control);
					var renderedContent = control.RenderContent(availableWidth, availableHeight);
					
					currentBottomOffset += renderedContent.Count;
					
					bounds.ControlContentBounds = new Rectangle(
						0, // Always left-aligned
						availableHeight - currentBottomOffset,
						control.ActualWidth ?? (renderedContent.FirstOrDefault()?.Length ?? 0),
						renderedContent.Count
					);
					
					bounds.ViewportSize = new Size(availableWidth, availableHeight);
					bounds.HasInternalScrolling = control is MultilineEditControl;
					bounds.ScrollOffset = Point.Empty; // Sticky controls don't scroll
					bounds.IsVisible = true;
				}
				
				// Calculate scrollable controls positions
				var scrollableAreaTop = currentTopOffset;
				var scrollableAreaHeight = availableHeight - currentTopOffset - currentBottomOffset;
				var currentScrollableOffset = 0;
				
				foreach (var control in _controls.Where(c => c.StickyPosition == StickyPosition.None && c.Visible))
				{
					var bounds = _layoutManager.GetOrCreateControlBounds(control);
					var renderedContent = control.RenderContent(availableWidth, scrollableAreaHeight);
					
					bounds.ControlContentBounds = new Rectangle(
						0, // Always left-aligned
						scrollableAreaTop + currentScrollableOffset - _scrollOffset,
						control.ActualWidth ?? (renderedContent.FirstOrDefault()?.Length ?? 0),
						renderedContent.Count
					);
					
					bounds.ViewportSize = new Size(availableWidth, scrollableAreaHeight);
					bounds.HasInternalScrolling = control is MultilineEditControl;
					bounds.ScrollOffset = new Point(0, _scrollOffset);
					bounds.IsVisible = true;
					
					currentScrollableOffset += renderedContent.Count;
				}
			}
		}

		public IWIndowControl? GetControlByIndex(int index)
		{
			lock (_lock)
			{
				if (index >= 0 && index < _controls.Count)
				{
					return _controls[index];
				}
				return null;
			}
		}

		public IWIndowControl? GetControlByTag<T>(string tag) where T : IWIndowControl
		{
			lock (_lock)
			{
				return _controls.FirstOrDefault(c => c is T && c.Tag?.ToString() == tag);
			}
		}

		public List<IWIndowControl> GetControls()
		{
			lock (_lock)
			{
				return _controls.ToList(); // Return a copy to avoid external modification
			}
		}

		public List<T> GetControlsByType<T>() where T : IWIndowControl
		{
			lock (_lock)
			{
				return _controls.OfType<T>().ToList();
			}
		}

		public int GetControlsCount()
		{
			lock (_lock)
			{
				return _controls.Count;
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
				if (activeInteractiveContent is IWIndowControl control)
				{
					// Use the new layout manager for coordinate translation
					var windowCursorPos = _layoutManager.TranslateLogicalCursorToWindow(control);
					if (windowCursorPos != null)
					{
						cursorPosition = windowCursorPos.Value;
						
						// Check if the cursor position is actually visible in the window
						if (IsCursorPositionVisible(cursorPosition, control))
						{
							return true;
						}
					}
				}
			}

			cursorPosition = new Point(0, 0);
			return false;
		}

		/// <summary>
		/// Checks if a cursor position is visible within the current window viewport
		/// </summary>
		/// <param name="cursorPosition">The cursor position in window coordinates</param>
		/// <param name="control">The control that owns the cursor</param>
		/// <returns>True if the cursor position is visible</returns>
		private bool IsCursorPositionVisible(Point cursorPosition, IWIndowControl control)
		{
			// Check if cursor is within the basic window content area (excluding borders)
			if (cursorPosition.X < 1 || cursorPosition.X >= Width - 1 || 
				cursorPosition.Y < 1 || cursorPosition.Y >= Height - 1)
			{
				return false;
			}

			// Get the control's bounds to understand its positioning
			var bounds = _layoutManager.GetOrCreateControlBounds(control);
			var controlBounds = bounds.ControlContentBounds;

			// For sticky controls, they're always visible in their designated areas
			if (control.StickyPosition == StickyPosition.Top)
			{
				// Top sticky controls are visible if cursor is within top sticky area
				return cursorPosition.Y >= 1 && cursorPosition.Y < 1 + _topStickyHeight;
			}
			else if (control.StickyPosition == StickyPosition.Bottom)
			{
				// Bottom sticky controls are visible if cursor is within bottom sticky area
				var bottomAreaStart = Height - 1 - _bottomStickyHeight;
				return cursorPosition.Y >= bottomAreaStart && cursorPosition.Y < Height - 1;
			}
			else
			{
				// For scrollable controls, check if cursor is within the scrollable viewport
				var scrollableAreaTop = 1 + _topStickyHeight;
				var scrollableAreaBottom = Height - 1 - _bottomStickyHeight;
				
				// Check if cursor Y is within the scrollable area bounds
				if (cursorPosition.Y < scrollableAreaTop || cursorPosition.Y >= scrollableAreaBottom)
				{
					return false;
				}

				// Check if the control itself is visible in the current scroll position
				// Control is visible if any part of it intersects with the visible scrollable area
				var visibleScrollTop = _scrollOffset;
				var visibleScrollBottom = _scrollOffset + (scrollableAreaBottom - scrollableAreaTop);
				
				var controlTop = controlBounds.Y + _scrollOffset; // Adjust for scroll offset
				var controlBottom = controlTop + controlBounds.Height;
				
				// Control is visible if it overlaps with the visible scroll area
				return controlBottom > visibleScrollTop && controlTop < visibleScrollBottom;
			}
		}

		public void Invalidate(bool redrawAll, IWIndowControl? callerControl = null)
		{
			_invalidated = true;

			lock (_lock)
			{
				if (redrawAll)
				{
					foreach (var content in _controls)
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
				if (_controls.Remove(content))
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
					UpdateControlLayout();
					RebuildContentCache();
				}

				return BuildVisibleContent();
			}
		}

		private void RebuildContentCache()
		{
			var availableWidth = Width - 2; // Account for borders
			var availableHeight = Height - 2; // Account for borders

			// Render sticky top controls
			_topStickyLines.Clear();
			_topStickyHeight = 0;
			var topStickyControls = _controls.Where(c => c.StickyPosition == StickyPosition.Top && c.Visible).ToList();
			
			foreach (var control in topStickyControls)
			{
				var renderedLines = control.RenderContent(availableWidth, availableHeight);
				_topStickyLines.AddRange(renderedLines);
				_topStickyHeight += renderedLines.Count;
			}

			// Render sticky bottom controls
			_bottomStickyLines.Clear();
			_bottomStickyHeight = 0;
			var bottomStickyControls = _controls.Where(c => c.StickyPosition == StickyPosition.Bottom && c.Visible).ToList();
			
			foreach (var control in bottomStickyControls)
			{
				var renderedLines = control.RenderContent(availableWidth, availableHeight);
				_bottomStickyLines.AddRange(renderedLines);
				_bottomStickyHeight += renderedLines.Count;
			}

			// Render scrollable content (non-sticky controls)
			var scrollableContent = new List<string>();
			var scrollableControls = _controls.Where(c => c.StickyPosition == StickyPosition.None && c.Visible).ToList();
			var scrollableHeight = availableHeight - _topStickyHeight - _bottomStickyHeight;
			
			foreach (var control in scrollableControls)
			{
				var renderedLines = control.RenderContent(availableWidth, scrollableHeight);
				scrollableContent.AddRange(renderedLines);
			}

			_cachedContent = scrollableContent;
			_invalidated = false;
		}

		private List<string> BuildVisibleContent()
		{
			var availableHeight = Height - 2; // Account for borders
			var scrollableAreaHeight = availableHeight - _topStickyHeight - _bottomStickyHeight;
			
			// Start with empty content area
			var visibleContent = new List<string>();

			// Add top sticky content (always visible at top)
			visibleContent.AddRange(_topStickyLines);

			// Add scrollable content (respecting scroll offset)
			var scrollableVisible = _cachedContent
				.Skip(_scrollOffset)
				.Take(scrollableAreaHeight);
			
			visibleContent.AddRange(scrollableVisible);

			// Pad scrollable area if needed
			var currentScrollableLines = visibleContent.Count - _topStickyHeight;
			if (currentScrollableLines < scrollableAreaHeight)
			{
				var paddingNeeded = scrollableAreaHeight - currentScrollableLines;
				visibleContent.AddRange(Enumerable.Repeat(string.Empty, paddingNeeded));
			}

			// Add bottom sticky content (always visible at bottom)
			visibleContent.AddRange(_bottomStickyLines);

			// Final padding to ensure we have exactly the right number of lines
			while (visibleContent.Count < availableHeight)
			{
				visibleContent.Add(string.Empty);
			}

			// Trim if we have too many lines
			if (visibleContent.Count > availableHeight)
			{
				visibleContent = visibleContent.Take(availableHeight).ToList();
			}

			return visibleContent;
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

		public void UpdateContentOrder(IWIndowControl content, int newIndex)
		{
			lock (_lock)
			{
				if (_controls.Contains(content) && newIndex >= 0 && newIndex < _controls.Count)
				{
					_controls.Remove(content);
					_controls.Insert(newIndex, content);
					_invalidated = true;
					Invalidate(true);
				}
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
				var bounds = _layoutManager.GetOrCreateControlBounds(focusedContent);
				var controlBounds = bounds.ControlContentBounds;
				
				int contentTop = controlBounds.Y;
				int contentHeight = controlBounds.Height;
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