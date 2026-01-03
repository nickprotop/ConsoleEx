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
using SharpConsoleUI.Core;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI
{
	/// <summary>
	/// Specifies the display mode of a window.
	/// </summary>
	public enum WindowMode
	{
		/// <summary>
		/// Normal window that can be deactivated when other windows are selected.
		/// </summary>
		Normal,
		/// <summary>
		/// Modal window that blocks input to other windows until closed.
		/// </summary>
		Modal
	}

	/// <summary>
	/// Specifies the current state of a window.
	/// </summary>
	public enum WindowState
	{
		/// <summary>
		/// Window is displayed at its normal size and position.
		/// </summary>
		Normal,
		/// <summary>
		/// Window is minimized and not visible in the content area.
		/// </summary>
		Minimized,
		/// <summary>
		/// Window is maximized to fill the entire desktop area.
		/// </summary>
		Maximized
	}

	/// <summary>
	/// Provides data for the window closing event, allowing cancellation of the close operation.
	/// </summary>
	public class ClosingEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ClosingEventArgs"/> class.
		/// </summary>
		public ClosingEventArgs()
		{
		}

		/// <summary>
		/// Gets or sets a value indicating whether the window close operation should be allowed.
		/// Set to false to cancel the close operation.
		/// </summary>
		public bool Allow { get; set; } = true;
	}

	/// <summary>
	/// Provides data for the key pressed event within a window.
	/// </summary>
	public class KeyPressedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="KeyPressedEventArgs"/> class.
		/// </summary>
		/// <param name="keyInfo">The key information for the pressed key.</param>
		/// <param name="allreadyHandled">Indicates whether the key was already handled by a control.</param>
		public KeyPressedEventArgs(ConsoleKeyInfo keyInfo, bool allreadyHandled)
		{
			KeyInfo = keyInfo;
			AllreadyHandled = allreadyHandled;
		}

		/// <summary>
		/// Gets a value indicating whether the key press was already handled by a focused control.
		/// </summary>
		public bool AllreadyHandled { get; private set; }

		/// <summary>
		/// Gets or sets a value indicating whether this key press event has been handled.
		/// </summary>
		public bool Handled { get; set; }

		/// <summary>
		/// Gets the key information for the pressed key.
		/// </summary>
		public ConsoleKeyInfo KeyInfo { get; }
	}

	/// <summary>
	/// Represents a window in the console UI system that can contain controls and handle user input.
	/// Implements <see cref="IContainer"/> for control management.
	/// </summary>
	public class Window : IContainer
	{
		private readonly List<IWindowControl> _controls = new();
		private readonly List<IInteractiveControl> _interactiveContents = new();
		private readonly object _lock = new();

		private readonly Window? _parentWindow;
		private readonly WindowLayoutManager _layoutManager;
		private Color? _activeBorderForegroundColor;
		private Color? _activeTitleForegroundColor;
		private int _bottomStickyHeight;
		private List<string> _bottomStickyLines = new List<string>();

		// Track control positions within scrollable content (startLine, lineCount)
		private readonly Dictionary<IWindowControl, (int StartLine, int LineCount)> _controlPositions = new();

		// List to store interactive contents
		private List<string> _cachedContent = new();

		private string _guid;
		private Color? _inactiveBorderForegroundColor;
		private Color? _inactiveTitleForegroundColor;
		private bool _invalidated = false;
		private bool _isActive;
		private IInteractiveControl? _lastFocusedControl;

		// Convenience property to access FocusStateService
		private FocusStateService? FocusService => _windowSystem?.FocusStateService;
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
		private CancellationTokenSource? _windowThreadCts;
		private bool _isClosing = false;
		private string? _name;

		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> class with an async background task.
		/// </summary>
		/// <param name="windowSystem">The console window system that manages this window.</param>
		/// <param name="windowThreadMethod">The async delegate to run in the background for this window.</param>
		/// <param name="parentWindow">Optional parent window for positioning and modal behavior.</param>
		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegateAsync windowThreadMethod, Window? parentWindow = null)
		{
			_guid = System.Guid.NewGuid().ToString();

			_parentWindow = parentWindow;
			_windowSystem = windowSystem;
			_layoutManager = new WindowLayoutManager(this);

			BackgroundColor = _windowSystem.Theme.WindowBackgroundColor;
			ForegroundColor = _windowSystem.Theme.WindowForegroundColor;

			// Set position relative to parent if this is a subwindow
			SetupInitialPosition();

			_windowThreadMethodAsync = windowThreadMethod;
			_windowThreadCts = new CancellationTokenSource();
			var token = _windowThreadCts.Token;
			_windowTask = Task.Run(async () =>
			{
				try
				{
					await windowThreadMethod(this, token);
				}
				catch (OperationCanceledException)
				{
					// Normal cancellation, ignore
				}
				catch (Exception ex)
				{
					// Log error to LogService if available, but don't crash
					_windowSystem?.LogService?.LogError($"Window thread error: {ex.Message}", ex, "Window");
				}
			});
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> class without a background task.
		/// </summary>
		/// <param name="windowSystem">The console window system that manages this window.</param>
		/// <param name="parentWindow">Optional parent window for positioning and modal behavior.</param>
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

		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> class with a synchronous background thread.
		/// </summary>
		/// <param name="windowSystem">The console window system that manages this window.</param>
		/// <param name="windowThreadMethod">The delegate to run on a background thread for this window.</param>
		/// <param name="parentWindow">Optional parent window for positioning and modal behavior.</param>
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

		/// <summary>
		/// Represents a synchronous method that runs on the window's background thread.
		/// </summary>
		/// <param name="window">The window instance.</param>
		public delegate void WindowThreadDelegate(Window window);

		/// <summary>
		/// Represents an asynchronous method that runs as the window's background task.
		/// </summary>
		/// <param name="window">The window instance.</param>
		/// <param name="cancellationToken">Token to signal cancellation when the window closes.</param>
		/// <returns>A task representing the async operation.</returns>
		public delegate Task WindowThreadDelegateAsync(Window window, CancellationToken cancellationToken);

		/// <summary>
		/// Occurs when the window becomes the active window.
		/// </summary>
		public event EventHandler? Activated;

		/// <summary>
		/// Occurs when the window loses active status.
		/// </summary>
		public event EventHandler? Deactivated;

		/// <summary>
		/// Occurs when a key is pressed while the window has focus.
		/// </summary>
		public event EventHandler<KeyPressedEventArgs>? KeyPressed;

		/// <summary>
		/// Occurs after the window has been closed.
		/// </summary>
		public event EventHandler? OnClosed;

		/// <summary>
		/// Occurs when the window is about to close, allowing cancellation.
		/// </summary>
		public event EventHandler<ClosingEventArgs>? OnCLosing;

		/// <summary>
		/// Occurs when the window is resized.
		/// </summary>
		public event EventHandler? OnResize;

		/// <summary>
		/// Occurs when the window is first shown.
		/// </summary>
		public event EventHandler? OnShown;

		/// <summary>
		/// Occurs when the window state changes (Normal, Minimized, Maximized).
		/// </summary>
		public event EventHandler<WindowStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Gets or sets the foreground color of the window border when active.
		/// </summary>
		public Color ActiveBorderForegroundColor
		{ get => _activeBorderForegroundColor ?? _windowSystem?.Theme.ActiveBorderForegroundColor ?? Color.White; set { _activeBorderForegroundColor = value; Invalidate(false); } }

		/// <summary>
		/// Gets or sets the foreground color of the window title when active.
		/// </summary>
		public Color ActiveTitleForegroundColor
		{ get => _activeTitleForegroundColor ?? _windowSystem?.Theme.ActiveTitleForegroundColor ?? Color.White; set { _activeTitleForegroundColor = value; Invalidate(false); } }

		/// <summary>
		/// Gets or sets the background color of the window content area.
		/// </summary>
		public Color BackgroundColor { get; set; }

		/// <summary>
		/// Gets or sets the default foreground color for window content.
		/// </summary>
		public Color ForegroundColor { get; set; }

		/// <summary>
		/// Gets the console window system that manages this window.
		/// </summary>
		public ConsoleWindowSystem? GetConsoleWindowSystem => _windowSystem;

		/// <summary>
		/// Gets the unique identifier for this window instance.
		/// </summary>
		public string Guid => _guid.ToString();

		private int _height = 20;

		/// <summary>
		/// Gets or sets the height of the window in character rows.
		/// </summary>
		public int Height
		{
			get => _height;
			set
			{
				if (_height != value)
				{
					_height = value;
					UpdateControlLayout();
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the window border when inactive.
		/// </summary>
		public Color InactiveBorderForegroundColor
		{ get => _inactiveBorderForegroundColor ?? _windowSystem?.Theme.InactiveBorderForegroundColor ?? Color.White; set { _inactiveBorderForegroundColor = value; Invalidate(false); } }

		/// <summary>
		/// Gets or sets the foreground color of the window title when inactive.
		/// </summary>
		public Color InactiveTitleForegroundColor
		{ get => _inactiveTitleForegroundColor ?? _windowSystem?.Theme.InactiveTitleForegroundColor ?? Color.White; set { _inactiveTitleForegroundColor = value; Invalidate(false); } }

		/// <summary>
		/// Gets or sets a value indicating whether the window can be closed by the user.
		/// </summary>
		public bool IsClosable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window can be maximized.
		/// </summary>
		public bool IsMaximizable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window can be minimized.
		/// </summary>
		public bool IsMinimizable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window content is visible.
		/// </summary>
		public bool IsContentVisible { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window needs to be redrawn.
		/// </summary>
		public bool IsDirty { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window is currently being dragged.
		/// </summary>
		public bool IsDragging { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the window can be moved by the user.
		/// </summary>
		public bool IsMovable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window can be resized by the user.
		/// </summary>
		public bool IsResizable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window content is scrollable.
		/// </summary>
		public bool IsScrollable { get; set; } = true;

		/// <summary>
		/// Gets or sets the left position of the window in character columns.
		/// </summary>
		public int Left { get; set; }

		/// <summary>
		/// Gets or sets the window mode (Normal or Modal).
		/// </summary>
		public WindowMode Mode
		{
			get => _mode;
			set { _mode = value; }
		}

		/// <summary>
		/// Gets or sets the original height before maximizing, used for restore.
		/// </summary>
		public int OriginalHeight { get; set; }

		/// <summary>
		/// Gets or sets the original left position before maximizing, used for restore.
		/// </summary>
		public int OriginalLeft { get; set; }

		/// <summary>
		/// Gets or sets the original top position before maximizing, used for restore.
		/// </summary>
		public int OriginalTop { get; set; }

		/// <summary>
		/// Gets or sets the original width before maximizing, used for restore.
		/// </summary>
		public int OriginalWidth { get; set; }

		/// <summary>
		/// Gets the parent window if this is a subwindow, otherwise null.
		/// </summary>
		public Window? ParentWindow
		{
			get
			{
				return _parentWindow;
			}
		}

		/// <summary>
		/// Gets or sets the vertical scroll offset for window content.
		/// </summary>
		public int ScrollOffset { get => _scrollOffset; set => _scrollOffset = value; }

		/// <summary>
		/// Gets or sets the current state of the window (Normal, Minimized, Maximized).
		/// </summary>
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

				_windowSystem?.LogService?.LogDebug($"Window state changed: {Title} ({previous_state} -> {value})", "Window");
				_state = value;

				switch (value)
				{
					case WindowState.Minimized:
						// Clear the window area before minimizing
						_windowSystem?.ClearArea(Left, Top, Width, Height);
						Invalidate(true);
						break;

					case WindowState.Maximized:
						OriginalWidth = Width;
						OriginalHeight = Height;
						OriginalLeft = Left;
						OriginalTop = Top;
						Left = 0;
						Top = 0;
						// Use centralized SetSize which handles invalidation order correctly
						SetSize(
							_windowSystem?.DesktopDimensions.Width ?? 80,
							_windowSystem?.DesktopDimensions.Height ?? 24
						);
						OnResize?.Invoke(this, EventArgs.Empty);
						break;

					case WindowState.Normal:
						if (previous_state == WindowState.Maximized)
						{
							// Clear the old maximized area before restoring
							_windowSystem?.ClearArea(Left, Top, Width, Height);

							Top = OriginalTop;
							Left = OriginalLeft;
							// Use centralized SetSize which handles invalidation order correctly
							SetSize(OriginalWidth, OriginalHeight);
							OnResize?.Invoke(this, EventArgs.Empty);
						}
						else if (previous_state == WindowState.Minimized)
						{
							// Just need to redraw - position hasn't changed
							Invalidate(true);
						}
						break;
				}

				OnStateChanged(value);
			}
		}

		/// <summary>
		/// Gets or sets an arbitrary object value that can be attached to this window.
		/// </summary>
		public object? Tag { get => _tag; set => _tag = value; }

		/// <summary>
		/// Gets or sets the title displayed in the window's title bar.
		/// </summary>
		public string Title { get; set; } = "Window";

		/// <summary>
		/// Optional unique name for finding/identifying this window.
		/// User-defined string for singleton window patterns (e.g., "LogViewer", "Settings").
		/// </summary>
		public string? Name
		{
			get => _name;
			set => _name = value;
		}

		/// <summary>
		/// Gets or sets the top position of the window in character rows.
		/// </summary>
		public int Top { get; set; }

		/// <summary>
		/// Gets the total number of content lines including sticky headers.
		/// </summary>
		public int TotalLines => _cachedContent.Count + _topStickyHeight;

		private int _width = 40;

		/// <summary>
		/// Gets or sets the width of the window in character columns.
		/// </summary>
		public int Width
		{
			get => _width;
			set
			{
				if (_width != value)
				{
					_width = value;
					UpdateControlLayout();
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the Z-order index for layering windows.
		/// </summary>
		public int ZIndex { get; set; }

		/// <inheritdoc/>
		public void AddControl(IWindowControl content)
		{
			lock (_lock)
			{
				_windowSystem?.LogService?.LogDebug($"Control added to window '{Title}': {content.GetType().Name}", "Window");

				content.Container = this;
				_controls.Add(content);

				// Register the control with the InvalidationManager for proper coordination
				InvalidationManager.Instance.RegisterControl(content);

				_invalidated = true;

				if (content is IInteractiveControl interactiveContent)
				{
					_interactiveContents.Add(interactiveContent);

					if (_interactiveContents.Where(p => p.HasFocus).Count() == 0)
					{
						interactiveContent.HasFocus = true;
						_lastFocusedControl = interactiveContent;
						// Sync with FocusStateService
						FocusService?.SetFocus(this, interactiveContent, FocusChangeReason.Programmatic);
					}
				}

				RenderAndGetVisibleContent();

				if (content.StickyPosition == StickyPosition.None && _interactiveContents.Where(p => p.HasFocus).Count() == 0) GoToBottom();
			}
		}

		/// <summary>
		/// Removes all controls from the window.
		/// </summary>
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

		/// <summary>
		/// Attempts to close the window.
		/// </summary>
		/// <param name="systemCall">True if called by the window system during cleanup.</param>
		/// <returns>True if the window was closed; false if closing was cancelled.</returns>
		public bool Close(bool systemCall = false)
		{
			// Prevent re-entrancy: Close() can be called twice when closing via button
			// (once from button click, once from CloseWindow calling Close(true))
			if (_isClosing) return true;

			if (IsClosable)
			{
				_isClosing = true;

				// Cancel async window thread - non-blocking to avoid UI freeze
				if (_windowThreadCts != null)
				{
					_windowThreadCts.Cancel();
					// Don't Wait() - it blocks the UI thread and can cause deadlock
					// The task wrapper has try/catch that handles cancellation gracefully
					// Clean up CTS in background after task completes
					var cts = _windowThreadCts;
					var task = _windowTask;
					_windowThreadCts = null;
					_windowTask = null;

					// Fire-and-forget cleanup (safe - task has exception handling)
					_ = Task.Run(async () =>
					{
						try
						{
							if (task != null)
								await task.ConfigureAwait(false);
						}
						catch { }
						finally
						{
							cts?.Dispose();
						}
					});
				}

				if (OnCLosing != null)
				{
					var args = new ClosingEventArgs();
					OnCLosing(this, args);
					if (!args.Allow)
					{
						_isClosing = false;
						return false;
					}
				}

				OnClosed?.Invoke(this, EventArgs.Empty);

				foreach (var content in _controls)
				{
					// Unregister the control from the InvalidationManager before disposing
					InvalidationManager.Instance.UnregisterControl(content as IWindowControl);
					(content as IWindowControl).Dispose();
				}

				if (!systemCall) _windowSystem?.CloseWindow(this);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Determines whether this window contains the specified control.
		/// </summary>
		/// <param name="content">The control to check for.</param>
		/// <returns>True if the control is in this window; otherwise false.</returns>
		public bool ContainsControl(IWindowControl content)
		{
			lock (_lock)
			{
				return _controls.Contains(content);
			}
		}

		/// <summary>
		/// Gets the control at the specified desktop coordinates.
		/// </summary>
		/// <param name="point">The desktop coordinates to check.</param>
		/// <returns>The control at the specified position, or null if none found.</returns>
		public IWindowControl? GetContentFromDesktopCoordinates(Point? point)
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

		/// <summary>
		/// Gets the control at the specified window-relative coordinates.
		/// </summary>
		/// <param name="point">The window-relative coordinates to check.</param>
		/// <returns>The control at the specified position, or null if none found.</returns>
		public IWindowControl? GetContentFromWindowCoordinates(Point? point)
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
				var contentPoint = new Point(point.Value.X, point.Value.Y);

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
				// Ensure layout is current before processing mouse events
				UpdateControlLayout();
				
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
					else
					{
						// Click in window content area but no control found - remove focus from current control
						if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked))
						{
							UnfocusCurrentControl();
							return true; // Event was handled (removed focus)
						}
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
		private Point GetControlRelativePosition(IWindowControl control, Point windowPosition)
		{
			// Use the new layout manager for coordinate translation
			var bounds = _layoutManager.GetOrCreateControlBounds(control);
			return bounds.WindowToControl(windowPosition);
		}

		/// <summary>
		/// Handles focus management when a control is clicked
		/// </summary>
		private void HandleControlFocusFromMouse(IWindowControl control)
		{
			// Check if control can receive focus
			if (control is Controls.IFocusableControl focusable && focusable.CanReceiveFocus)
			{
				// Remove focus from current control
				if (_lastFocusedControl != null && _lastFocusedControl != control && _lastFocusedControl is Controls.IFocusableControl currentFocused)
				{
					currentFocused.SetFocus(false, Controls.FocusReason.Mouse);
				}

				// Set focus to new control
				focusable.SetFocus(true, Controls.FocusReason.Mouse);

				// Update last focused control (check if it's also IInteractiveControl)
				if (control is IInteractiveControl interactive)
				{
					_lastFocusedControl = interactive;
					// Sync with FocusStateService
					FocusService?.SetFocus(this, interactive, FocusChangeReason.Mouse);
				}
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
						control.ActualWidth ?? availableWidth, // Use full width if not specified
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
						control.ActualWidth ?? availableWidth, // Use full width if not specified
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
						control.ActualWidth ?? availableWidth, // Use full width if not specified
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

		/// <summary>
		/// Gets a control by its index in the controls collection.
		/// </summary>
		/// <param name="index">The zero-based index of the control.</param>
		/// <returns>The control at the specified index, or null if out of range.</returns>
		public IWindowControl? GetControlByIndex(int index)
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

		/// <summary>
		/// Gets a control of type T by its tag value.
		/// </summary>
		/// <typeparam name="T">The type of control to search for.</typeparam>
		/// <param name="tag">The tag value to match.</param>
		/// <returns>The first matching control, or null if not found.</returns>
		public IWindowControl? GetControlByTag<T>(string tag) where T : IWindowControl
		{
			lock (_lock)
			{
				return _controls.FirstOrDefault(c => c is T && c.Tag?.ToString() == tag);
			}
		}

		/// <summary>
		/// Gets a copy of all controls in this window.
		/// </summary>
		/// <returns>A list containing all controls.</returns>
		public List<IWindowControl> GetControls()
		{
			lock (_lock)
			{
				return _controls.ToList(); // Return a copy to avoid external modification
			}
		}

		/// <summary>
		/// Gets all controls of the specified type.
		/// </summary>
		/// <typeparam name="T">The type of controls to retrieve.</typeparam>
		/// <returns>A list of controls of type T.</returns>
		public List<T> GetControlsByType<T>() where T : IWindowControl
		{
			lock (_lock)
			{
				return _controls.OfType<T>().ToList();
			}
		}

		/// <summary>
		/// Gets the number of controls in this window.
		/// </summary>
		/// <returns>The control count.</returns>
		public int GetControlsCount()
		{
			lock (_lock)
			{
				return _controls.Count;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this window is currently active.
		/// </summary>
		/// <returns>True if the window is active; otherwise false.</returns>
		public bool GetIsActive()
		{
			return _isActive;
		}

		/// <summary>
		/// Scrolls the window content to the bottom.
		/// </summary>
		public void GoToBottom()
		{
			_scrollOffset = Math.Max(0, (_cachedContent?.Count ?? Height) - (Height - 2));
			Invalidate(true);
		}

		/// <summary>
		/// Scrolls the window content to the top.
		/// </summary>
		public void GoToTop()
		{
			_scrollOffset = 0;
			Invalidate(true);
		}

		/// <summary>
		/// Determines whether this window has an active interactive control with focus.
		/// </summary>
		/// <param name="interactiveContent">When returning true, contains the focused interactive control.</param>
		/// <returns>True if there is a focused interactive control; otherwise false.</returns>
		public bool HasActiveInteractiveContent(out IInteractiveControl? interactiveContent)
		{
			interactiveContent = _interactiveContents.LastOrDefault(ic => ic.IsEnabled && ic.HasFocus);
			return interactiveContent != null;
		}

		/// <summary>
		/// Determines whether there is interactive content that needs cursor display.
		/// </summary>
		/// <param name="cursorPosition">When returning true, contains the cursor position in window coordinates.</param>
		/// <returns>True if cursor should be displayed; otherwise false.</returns>
		public bool HasInteractiveContent(out Point cursorPosition)
		{
			if (HasActiveInteractiveContent(out var activeInteractiveContent))
			{
				if (activeInteractiveContent is IWindowControl control)
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
		private bool IsCursorPositionVisible(Point cursorPosition, IWindowControl control)
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
				
				var controlTop = controlBounds.Y; // controlBounds.Y is already in window coordinates
				var controlBottom = controlTop + controlBounds.Height;
				
				// Control is visible if it overlaps with the visible scroll area
				return controlBottom > visibleScrollTop && controlTop < visibleScrollBottom;
			}
		}

		/// <summary>
		/// Marks the window as needing redraw and optionally invalidates all controls.
		/// </summary>
		/// <param name="redrawAll">True to invalidate all controls; false for partial invalidation.</param>
		/// <param name="callerControl">The control that initiated the invalidation, to prevent recursion.</param>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			_invalidated = true;

			lock (_lock)
			{
				if (redrawAll)
				{
					// Directly invalidate each control's cache to ensure they re-render
					// This is essential for window resize/maximize operations
					foreach (var content in _controls)
					{
						if (content is IWindowControl control && control != callerControl)
						{
							// Call Invalidate() directly to clear the control's ThreadSafeCache
							// SafeInvalidate only marks the InvalidationManager's state,
							// it doesn't clear the actual content cache
							// Skip the callerControl to prevent infinite recursion
							control.Invalidate();
						}
					}
				}
				else if (callerControl != null)
				{
					// Specific control invalidation - use SafeInvalidate for coordination
					callerControl.SafeInvalidate(InvalidationReason.ChildInvalidated);
				}
			}

			IsDirty = true;
		}

		/// <summary>
		/// Gets the actual visible height for a control within the window viewport.
		/// Accounts for window scrolling and clipping.
		/// </summary>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			lock (_lock)
			{
				// For sticky controls, they're always fully visible
				if (control.StickyPosition == StickyPosition.Top || control.StickyPosition == StickyPosition.Bottom)
				{
					// Return the control's rendered height
					var bounds = _layoutManager.GetControlBounds(control);
					return bounds?.ControlContentBounds.Height;
				}

				var availableHeight = Height - 2;
				var scrollableAreaHeight = availableHeight - _topStickyHeight - _bottomStickyHeight;

				// Try to find position for direct children
				if (_controlPositions.TryGetValue(control, out var position))
				{
					return CalculateVisibleHeight(position.StartLine, position.LineCount, scrollableAreaHeight);
				}

				// For nested controls, find the parent container that IS tracked
				// and calculate the nested control's actual position
				foreach (var kvp in _controlPositions)
				{
					var (nestedOffset, nestedHeight) = FindNestedControlPosition(kvp.Key, control);
					if (nestedHeight > 0)
					{
						// Calculate actual position: parent start + nested offset
						int actualStart = kvp.Value.StartLine + nestedOffset;
						return CalculateVisibleHeight(actualStart, nestedHeight, scrollableAreaHeight);
					}
				}

				return null;
			}
		}

		// Find a nested control's position within a parent container
		// Returns (offsetWithinParent, controlHeight) or (-1, 0) if not found
		private (int Offset, int Height) FindNestedControlPosition(IWindowControl container, IWindowControl target)
		{
			if (container is HorizontalGridControl grid)
			{
				// For HorizontalGridControl, controls are in columns side by side
				// We need to find which column contains the control and its vertical offset
				foreach (var column in grid.Columns)
				{
					var result = FindNestedControlPosition(column, target);
					if (result.Height > 0)
						return result;
				}
			}
			else if (container is ColumnContainer column)
			{
				int offset = 0;
				foreach (var child in column.Contents.Where(c => c.Visible))
				{
					if (child == target)
					{
						// Found the target - get its rendered height
						var rendered = child.RenderContent(null, null);
						return (offset, rendered.Count);
					}

					// Check if target is nested deeper
					var nestedResult = FindNestedControlPosition(child, target);
					if (nestedResult.Height > 0)
					{
						return (offset + nestedResult.Offset, nestedResult.Height);
					}

					// Add this child's height to the offset
					var childRendered = child.RenderContent(null, null);
					offset += childRendered.Count;
				}
			}
			return (-1, 0);
		}

		// Check if a container control contains the target control
		private bool ContainsControl(IWindowControl container, IWindowControl target)
		{
			if (container is HorizontalGridControl grid)
			{
				foreach (var column in grid.Columns)
				{
					foreach (var child in column.Contents)
					{
						if (child == target)
							return true;
						if (ContainsControl(child, target))
							return true;
					}
				}
			}
			else if (container is ColumnContainer column)
			{
				foreach (var child in column.Contents)
				{
					if (child == target)
						return true;
					if (ContainsControl(child, target))
						return true;
				}
			}
			return false;
		}

		// Calculate visible height given control position and viewport
		private int CalculateVisibleHeight(int controlStart, int controlHeight, int viewportHeight)
		{
			int controlEnd = controlStart + controlHeight;
			int viewportTop = _scrollOffset;
			int viewportBottom = _scrollOffset + viewportHeight;

			int visibleTop = Math.Max(controlStart, viewportTop);
			int visibleBottom = Math.Min(controlEnd, viewportBottom);

			int visibleHeight = visibleBottom - visibleTop;
			return visibleHeight > 0 ? visibleHeight : 0;
		}

		/// <summary>
		/// Notifies the window that a control has lost focus.
		/// </summary>
		/// <param name="control">The control that lost focus.</param>
		public void NotifyControlFocusLost(IInteractiveControl control)
		{
			if (control != null && _interactiveContents.Contains(control))
			{
				_lastFocusedControl = control;
			}
		}

		/// <summary>
		/// Processes keyboard input for this window.
		/// </summary>
		/// <param name="key">The key information to process.</param>
		/// <returns>True if the input was handled; otherwise false.</returns>
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

		/// <summary>
		/// Removes a control from this window and disposes it.
		/// </summary>
		/// <param name="content">The control to remove.</param>
		public void RemoveContent(IWindowControl content)
		{
			lock (_lock)
			{
				if (_controls.Remove(content))
				{
					_windowSystem?.LogService?.LogDebug($"Control removed from window '{Title}': {content.GetType().Name}", "Window");
					if (content is IInteractiveControl interactiveContent)
					{
						_interactiveContents.Remove(interactiveContent);

						// If the removed content was the last focused control, clear it
						if (_lastFocusedControl == interactiveContent)
						{
							_lastFocusedControl = null;
							FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
						}

						// If the removed content had focus, switch focus to the next one
						if (interactiveContent.HasFocus && _interactiveContents.Count > 0)
						{
							_interactiveContents[0].HasFocus = true;
							_lastFocusedControl = _interactiveContents[0];
							FocusService?.SetFocus(this, _interactiveContents[0], FocusChangeReason.Programmatic);
						}
					}
					_invalidated = true;
					RenderAndGetVisibleContent();

					// Unregister the control from the InvalidationManager
					InvalidationManager.Instance.UnregisterControl(content);
					content.Dispose();
					GoToBottom();
				}
			}
		}

		/// <summary>
		/// Renders the window content and returns the visible lines.
		/// </summary>
		/// <returns>A list of rendered content lines visible within the window viewport.</returns>
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

			// Clear and rebuild control position tracking
			_controlPositions.Clear();
			int currentScrollableOffset = 0;

			foreach (var control in scrollableControls)
			{
				var renderedLines = control.RenderContent(availableWidth, scrollableHeight);

				// Track this control's position and size
				_controlPositions[control] = (currentScrollableOffset, renderedLines.Count);

				scrollableContent.AddRange(renderedLines);
				currentScrollableOffset += renderedLines.Count;
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

		/// <summary>
		/// Maximizes the window to fill the entire desktop area.
		/// </summary>
		public void Maximize()
		{
			if (!IsMaximizable)
				return;
			State = WindowState.Maximized;
		}

		/// <summary>
		/// Minimizes the window.
		/// </summary>
		public void Minimize()
		{
			if (!IsMinimizable)
				return;
			State = WindowState.Minimized;
		}

		/// <summary>
		/// Restores the window to its normal state.
		/// </summary>
		public void Restore()
		{
			State = WindowState.Normal;
		}

		/// <summary>
		/// Sets the active state of the window.
		/// </summary>
		/// <param name="value">True to activate the window; false to deactivate.</param>
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
				// Sync with FocusStateService
				if (value)
				{
					FocusService?.SetFocus(this, _lastFocusedControl, FocusChangeReason.Programmatic);
				}
			}
		}

		/// <summary>
		/// Sets the position of the window.
		/// </summary>
		/// <param name="point">The new position with X as left and Y as top.</param>
		public void SetPosition(Point point)
		{
			if (point.X < 0 || point.Y < 0) return;

			Left = point.X;
			Top = point.Y;
		}

		/// <summary>
		/// Sets the window size with proper invalidation and constraint handling.
		/// </summary>
		/// <param name="width">The new width in character columns.</param>
		/// <param name="height">The new height in character rows.</param>
		public void SetSize(int width, int height)
		{
			if (_width == width && _height == height)
			{
				return;
			}

			// Apply constraints
			if (_minimumWidth != null && width < _minimumWidth)
				width = (int)_minimumWidth;
			if (_maximumWidth != null && width > _maximumWidth)
				width = (int)_maximumWidth;
			if (_minimumHeight != null && height < _minimumHeight)
				height = (int)_minimumHeight;
			if (_maximumHeight != null && height > _maximumHeight)
				height = (int)_maximumHeight;

			// Set backing fields directly to avoid property setters calling
			// UpdateControlLayout() before both dimensions are set
			_width = width;
			_height = height;

			// IMPORTANT: Invalidate controls FIRST so they clear their caches
			Invalidate(true);

			// Then recalculate layout with fresh rendering
			UpdateControlLayout();

			if (_scrollOffset > (_cachedContent?.Count ?? Height) - (Height - 2))
			{
				GoToBottom();
			}

			OnResize?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Switches focus to the next or previous interactive control in the window.
		/// </summary>
		/// <param name="backward">True to move focus backward; false to move forward.</param>
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
				// Use directional focus for container controls that support it
				if (_interactiveContents[nextIndex] is Controls.IDirectionalFocusControl directional)
				{
					directional.SetFocusWithDirection(true, backward);
				}
				else if (_interactiveContents[nextIndex] is Controls.IFocusableControl focusable)
				{
					focusable.SetFocus(true, Controls.FocusReason.Keyboard);
				}
				_lastFocusedControl = _interactiveContents[nextIndex]; // Update last focused control

				// Sync with FocusStateService
				FocusService?.SetFocus(this, _interactiveContents[nextIndex], FocusChangeReason.Keyboard);

				_windowSystem?.LogService?.LogTrace($"Focus switched in '{Title}': {_lastFocusedControl?.GetType().Name}", "Focus");

				BringIntoFocus(nextIndex);
			}
		}

		/// <summary>
		/// Removes focus from the currently focused control.
		/// </summary>
		public void UnfocusCurrentControl()
		{
			if (_lastFocusedControl != null && _lastFocusedControl is Controls.IFocusableControl focusable)
			{
				focusable.SetFocus(false, Controls.FocusReason.Programmatic);
				// Sync with FocusStateService
				FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
			}
		}

		/// <summary>
		/// Changes the order of a control in the rendering sequence.
		/// </summary>
		/// <param name="content">The control to reorder.</param>
		/// <param name="newIndex">The new index position for the control.</param>
		public void UpdateContentOrder(IWindowControl content, int newIndex)
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

		/// <summary>
		/// Called when the window has been added to the window system.
		/// </summary>
		public void WindowIsAdded()
		{
			OnShown?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Raises the KeyPressed event.
		/// </summary>
		/// <param name="key">The key information.</param>
		/// <param name="allreadyHandled">Indicates whether the key was already handled.</param>
		/// <returns>True if the event was handled; otherwise false.</returns>
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

		/// <summary>
		/// Raises the StateChanged event.
		/// </summary>
		/// <param name="newState">The new window state.</param>
		protected virtual void OnStateChanged(WindowState newState)
		{
			StateChanged?.Invoke(this, new WindowStateChangedEventArgs(newState));
		}

		private void BringIntoFocus(int nextIndex)
		{
			// Ensure the focused content is within the visible window
			var focusedContent = _interactiveContents[nextIndex] as IWindowControl;

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

		/// <summary>
		/// Provides data for the window state changed event.
		/// </summary>
		public class WindowStateChangedEventArgs : EventArgs
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="WindowStateChangedEventArgs"/> class.
			/// </summary>
			/// <param name="newState">The new window state.</param>
			public WindowStateChangedEventArgs(WindowState newState)
			{
				NewState = newState;
			}

			/// <summary>
			/// Gets the new window state.
			/// </summary>
			public WindowState NewState { get; }
		}
	}
}