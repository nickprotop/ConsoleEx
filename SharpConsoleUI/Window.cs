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
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Core;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI
{
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
	/// Specifies the visual style of a window's border.
	/// </summary>
	public enum BorderStyle
	{
		/// <summary>
		/// Double-line border characters (╔═╗║╚╝ when active, ┌─┐│└┘ when inactive).
		/// This is the default and traditional border style.
		/// </summary>
		DoubleLine,

		/// <summary>
		/// Single-line border characters (┌─┐│└┘).
		/// </summary>
		Single,

		/// <summary>
		/// Rounded border characters (╭─╮│╰╯).
		/// </summary>
		Rounded,

		/// <summary>
		/// No visible border. Border areas render as spaces with window background color.
		/// Layout dimensions unchanged - border space still exists but is invisible.
		/// </summary>
		None
	}

	/// <summary>
	/// Provides data for the window closing event, allowing cancellation of the close operation.
	/// </summary>
	public class ClosingEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ClosingEventArgs"/> class.
		/// </summary>
		/// <param name="force">Whether this is a forced close that cannot be cancelled.</param>
		public ClosingEventArgs(bool force = false)
		{
			Force = force;
		}

		/// <summary>
		/// Gets a value indicating whether this is a forced close.
		/// When true, the close cannot be cancelled (Allow is ignored).
		/// Handlers can use this to perform cleanup knowing the window will close.
		/// </summary>
		public bool Force { get; }

		/// <summary>
		/// Gets or sets a value indicating whether the window close operation should be allowed.
		/// Set to false to cancel the close operation. Ignored when <see cref="Force"/> is true.
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
		/// <param name="alreadyHandled">Indicates whether the key was already handled by a control.</param>
		public KeyPressedEventArgs(ConsoleKeyInfo keyInfo, bool alreadyHandled)
		{
			KeyInfo = keyInfo;
			AlreadyHandled = alreadyHandled;
		}

		/// <summary>
		/// Gets a value indicating whether the key press was already handled by a focused control.
		/// </summary>
		public bool AlreadyHandled { get; private set; }

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
		internal readonly List<IWindowControl> _controls = new();
		internal readonly List<IInteractiveControl> _interactiveContents = new();
		internal readonly object _lock = new();

		private static int _nextCreationOrder = 0;
		private static readonly object _creationOrderLock = new();
		private readonly int _creationOrder;

		private readonly Window? _parentWindow;
		internal readonly WindowLayoutManager _layoutManager;
	private readonly Windows.WindowContentManager _contentManager;
		private Color? _activeBorderForegroundColor;
		private Color? _activeTitleForegroundColor;
		internal int _bottomStickyHeight;
		private List<string> _bottomStickyLines = new List<string>();

		// Track control positions within scrollable content (startLine, lineCount)
		private readonly Dictionary<IWindowControl, (int StartLine, int LineCount)> _controlPositions = new();

		// List to store interactive contents
		internal List<string> _cachedContent = new();

		private string _guid;
		private Color? _inactiveBorderForegroundColor;
		private Color? _inactiveTitleForegroundColor;
		internal bool _invalidated = false;
		private bool _isActive;
		internal IInteractiveControl? _lastFocusedControl;

		// Convenience property to access FocusStateService
		internal FocusStateService? FocusService => _windowSystem?.FocusStateService;
		private int? _minimumHeight = Configuration.ControlDefaults.DefaultWindowMinimumHeight;
		private int? _minimumWidth = Configuration.ControlDefaults.DefaultWindowMinimumWidth;
		private bool _isModal = false;
		internal int _scrollOffset;
		private WindowState _state;
		private object? _tag;
		internal int _topStickyHeight;
		private List<string> _topStickyLines = new List<string>();
		internal ConsoleWindowSystem? _windowSystem;
		private Task? _windowTask;
		private WindowThreadDelegateAsync? _windowThreadMethodAsync;
		private CancellationTokenSource? _windowThreadCts;
		private BorderStyle _borderStyle = BorderStyle.DoubleLine;
		private bool _showTitle = true;
		private bool _showCloseButton = true;
		internal volatile bool _isClosing = false;
		private string? _name;
		private TimeSpan _asyncThreadCleanupTimeout = TimeSpan.FromSeconds(Configuration.ControlDefaults.AsyncCleanupTimeoutSeconds);
		private int _left;
		private int _top;

		// DOM-based layout system (delegated to WindowRenderer)
		internal Windows.WindowRenderer? _renderer;

		/// <summary>
		/// Gets the window's renderer, providing access to rendering internals.
		/// </summary>
		/// <remarks>
		/// Exposes the renderer for advanced scenarios like custom buffer effects,
		/// transitions, and compositor-style manipulations. Use the PostBufferPaint event
		/// on the renderer to safely manipulate the character buffer after painting.
		/// </remarks>
		public Windows.WindowRenderer? Renderer => _renderer;

		// Border rendering (delegated to BorderRenderer)
		private Windows.BorderRenderer? _borderRenderer;

		// Event dispatching (delegated to WindowEventDispatcher)
		private Windows.WindowEventDispatcher? _eventDispatcher;

		/// <summary>
		/// Initializes a new instance of the <see cref="Window"/> class with an async background task.
		/// </summary>
		/// <param name="windowSystem">The console window system that manages this window.</param>
		/// <param name="windowThreadMethod">The async delegate to run in the background for this window.</param>
		/// <param name="parentWindow">Optional parent window for positioning and modal behavior.</param>
		public Window(ConsoleWindowSystem windowSystem, WindowThreadDelegateAsync windowThreadMethod, Window? parentWindow = null)
		{
			_guid = System.Guid.NewGuid().ToString();

			lock (_creationOrderLock)
			{
				_creationOrder = _nextCreationOrder++;
			}

			_parentWindow = parentWindow;
			_windowSystem = windowSystem;
			_layoutManager = new WindowLayoutManager(this);

		// Initialize renderer for DOM-based layout
		_renderer = new Windows.WindowRenderer(
			this,
			_windowSystem?.LogService);

		// Initialize content manager for control collection management
		_contentManager = new Windows.WindowContentManager(
			() => Title,
			_windowSystem?.LogService,
			() => Invalidate(true),
			() => _renderer?.InvalidateDOM());

		// Initialize border renderer
		_borderRenderer = new Windows.BorderRenderer(
			this,
			() => _windowSystem?.ConsoleDriver!,
			() => _windowSystem?.DesktopUpperLeft ?? Point.Empty,
			() => _windowSystem?.DesktopBottomRight ?? Point.Empty);

		// Initialize event dispatcher
		_eventDispatcher = new Windows.WindowEventDispatcher(this);

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

			lock (_creationOrderLock)
			{
				_creationOrder = _nextCreationOrder++;
			}

			_windowSystem = windowSystem;
			_parentWindow = parentWindow;
			_layoutManager = new WindowLayoutManager(this);

		// Initialize renderer for DOM-based layout
		_renderer = new Windows.WindowRenderer(
			this,
			_windowSystem?.LogService);

		// Initialize content manager for control collection management
		_contentManager = new Windows.WindowContentManager(
			() => Title,
			_windowSystem?.LogService,
			() => Invalidate(true),
			() => _renderer?.InvalidateDOM());

		// Initialize border renderer
		_borderRenderer = new Windows.BorderRenderer(
			this,
			() => _windowSystem?.ConsoleDriver!,
			() => _windowSystem?.DesktopUpperLeft ?? Point.Empty,
			() => _windowSystem?.DesktopBottomRight ?? Point.Empty);

		// Initialize event dispatcher
		_eventDispatcher = new Windows.WindowEventDispatcher(this);

			// Set position relative to parent if this is a subwindow
			SetupInitialPosition();
		}

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
		public event EventHandler<ClosingEventArgs>? OnClosing;

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
		/// Occurs when a mouse click happens on empty space (no control at that position).
		/// Used by specialized windows (e.g., OverlayWindow) to detect clicks outside content.
		/// </summary>
		public event EventHandler<Events.MouseEventArgs>? UnhandledMouseClick;

		/// <summary>
		/// Raises the UnhandledMouseClick event. Internal helper for WindowEventDispatcher.
		/// </summary>
		internal void RaiseUnhandledMouseClick(Events.MouseEventArgs args)
		{
			UnhandledMouseClick?.Invoke(this, args);
		}

		/// <summary>
		/// Gets or sets the foreground color of the window border when active.
		/// </summary>
		public Color ActiveBorderForegroundColor
		{ get => _activeBorderForegroundColor ?? _windowSystem?.Theme.ActiveBorderForegroundColor ?? Color.White; set { _activeBorderForegroundColor = value; InvalidateBorderCache(); Invalidate(false); } }

		/// <summary>
		/// Gets or sets the foreground color of the window title when active.
		/// </summary>
		public Color ActiveTitleForegroundColor
		{ get => _activeTitleForegroundColor ?? _windowSystem?.Theme.ActiveTitleForegroundColor ?? Color.White; set { _activeTitleForegroundColor = value; InvalidateBorderCache(); Invalidate(false); } }

		private Color? _backgroundColor;
		private Color? _foregroundColor;

		/// <summary>
		/// Gets or sets the background color of the window content area.
		/// If not set, uses the theme's WindowBackgroundColor (or ModalBackgroundColor for modals).
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColor ?? (IsModal
				? _windowSystem?.Theme.ModalBackgroundColor
				: _windowSystem?.Theme.WindowBackgroundColor) ?? Color.Black;
			set
			{
				_backgroundColor = value;
				InvalidateBorderCache();
				Invalidate(false);
			}
		}

		/// <summary>
		/// Gets or sets the default foreground color for window content.
		/// If not set, uses the theme's WindowForegroundColor.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColor ?? _windowSystem?.Theme.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColor = value;
				InvalidateBorderCache();
				Invalidate(false);
			}
		}

		/// <summary>
		/// Gets the console window system that manages this window.
		/// </summary>
		public ConsoleWindowSystem? GetConsoleWindowSystem => _windowSystem;

		/// <summary>
		/// Gets the unique identifier for this window instance.
		/// </summary>
		public string Guid => _guid.ToString();

		/// <summary>
		/// Gets the creation order of this window. Windows are numbered sequentially as they're created.
		/// This provides a stable ordering for features like Alt+number shortcuts.
		/// </summary>
		public int CreationOrder => _creationOrder;

		private int _height = Configuration.ControlDefaults.DefaultWindowHeight;

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
					InvalidateBorderCache();
					// Layout will be updated lazily on next event
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the window border when inactive.
		/// </summary>
		public Color InactiveBorderForegroundColor
		{ get => _inactiveBorderForegroundColor ?? _windowSystem?.Theme.InactiveBorderForegroundColor ?? Color.White; set { _inactiveBorderForegroundColor = value; InvalidateBorderCache(); Invalidate(false); } }

		/// <summary>
		/// Gets or sets the foreground color of the window title when inactive.
		/// </summary>
		public Color InactiveTitleForegroundColor
		{ get => _inactiveTitleForegroundColor ?? _windowSystem?.Theme.InactiveTitleForegroundColor ?? Color.White; set { _inactiveTitleForegroundColor = value; InvalidateBorderCache(); Invalidate(false); } }

		/// <summary>
		/// Gets or sets a value indicating whether the window can be closed by the user.
		/// </summary>
		public bool IsClosable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the close button [X] is shown in the title bar.
		/// This only affects the visual display - use IsClosable to prevent closing entirely.
		/// </summary>
		public bool ShowCloseButton
		{
			get => _showCloseButton;
			set
			{
				if (_showCloseButton != value)
				{
					_showCloseButton = value;
					Invalidate(false);
				}
			}
		}

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
		/// Thread-safe flag indicating whether the window needs to be redrawn (1 = true, 0 = false).
		/// </summary>
		private int _isDirtyFlag = 1; // 1 = true initially

		/// <summary>
		/// Gets or sets a value indicating whether the window needs to be redrawn.
		/// When true, the window will be rendered on the next frame update.
		/// Thread-safe using Interlocked operations.
		/// </summary>
		public bool IsDirty
		{
			get => Interlocked.CompareExchange(ref _isDirtyFlag, 0, 0) == 1;
			set => Interlocked.Exchange(ref _isDirtyFlag, value ? 1 : 0);
		}

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
		/// Gets or sets a value indicating whether this window always renders on top of normal windows.
		/// AlwaysOnTop windows render after normal windows regardless of ZIndex.
		/// </summary>
		public bool AlwaysOnTop { get; set; } = false;

	private bool _renderLock = false;

	/// <summary>
	/// Gets or sets a value indicating whether rendering to screen is locked.
	/// When true, the window performs all internal work (measure, layout, paint to buffer)
	/// but does not output to the render pipeline. Useful for batching multiple updates
	/// to appear atomically. When unlocked, automatically invalidates to trigger render.
	/// </summary>
	public bool RenderLock
	{
		get => _renderLock;
		set
		{
			if (_renderLock != value)
			{
				_renderLock = value;

				// When unlocking, force re-render to show accumulated changes
				if (!_renderLock)
				{
					IsDirty = true;
				}
			}
		}
	}

		/// <summary>
		/// Gets or sets the left position of the window in character columns.
		/// When set, automatically invalidates exposed regions for proper rendering.
		/// </summary>
		public int Left
		{
			get => _left;
			set
			{
				if (_left == value) return;

				// Use WindowPositioningManager for proper invalidation when system is initialized
				if (_windowSystem?.Positioning != null)
				{
					_windowSystem.Positioning.MoveWindowTo(this, value, _top);
				}
				else
				{
					// Fallback for initialization (before windowSystem is set)
					_left = value;
				}
			}
		}

		/// <summary>
		/// Internal setter for Left that bypasses invalidation logic (used by WindowPositioningManager).
		/// </summary>
		internal void SetLeftDirect(int value)
		{
			_left = value;
		}

		/// <summary>
		/// Gets or sets whether this window is modal (blocks input to other windows).
		/// </summary>
		public bool IsModal
		{
			get => _isModal;
			set { _isModal = value; }
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
		public int ScrollOffset
		{
			get => _renderer?.ScrollOffset ?? _scrollOffset;
			set
			{
				if (_renderer != null)
					_renderer.ScrollOffset = value;
				else
					_scrollOffset = value;
			}
		}

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
						_windowSystem?.Renderer?.ClearArea(Left, Top, Width, Height, _windowSystem.Theme, _windowSystem.Windows);
						Invalidate(true);
						break;

					case WindowState.Maximized:
						OriginalWidth = Width;
						OriginalHeight = Height;
						OriginalLeft = Left;
						OriginalTop = Top;
						// Position window at desktop origin (0,0 in desktop coordinates)
						// Desktop coordinates are automatically offset by DesktopUpperLeft during rendering
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
							_windowSystem?.Renderer?.ClearArea(Left, Top, Width, Height, _windowSystem.Theme, _windowSystem.Windows);

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
		private string _title = "Window";

		/// <summary>
		/// Gets or sets the title displayed in the window's title bar.
		/// </summary>
		public string Title
		{
			get => _title;
			set
			{
				if (_title != value)
				{
					_title = value;
					InvalidateBorderCache();
					Invalidate(false);
				}
			}
		}

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
		/// Gets or sets the timeout for async window thread cleanup.
		/// If thread doesn't respond to cancellation within this time, window transforms to error state.
		/// Default is 5 seconds.
		/// </summary>
		public TimeSpan AsyncThreadCleanupTimeout
		{
			get => _asyncThreadCleanupTimeout;
			set => _asyncThreadCleanupTimeout = value;
		}

		/// <summary>
		/// Gets or sets the top position of the window in character rows.
		/// When set, automatically invalidates exposed regions for proper rendering.
		/// </summary>
		public int Top
		{
			get => _top;
			set
			{
				if (_top == value) return;

				// Use WindowPositioningManager for proper invalidation when system is initialized
				if (_windowSystem?.Positioning != null)
				{
					_windowSystem.Positioning.MoveWindowTo(this, _left, value);
				}
				else
				{
					// Fallback for initialization (before windowSystem is set)
					_top = value;
				}
			}
		}

		/// <summary>
		/// Internal setter for Top that bypasses invalidation logic (used by WindowPositioningManager).
		/// </summary>
		internal void SetTopDirect(int value)
		{
			_top = value;
		}

		/// <summary>
		/// Gets the total number of content lines including sticky headers.
		/// </summary>
		public int TotalLines
		{
			get
			{
				if (_renderer != null)
				{
					// DOM mode: return total scrollable content height
					return _renderer.ScrollableContentHeight;
				}
				return _cachedContent.Count + _topStickyHeight;
			}
		}

		private int _width = Configuration.ControlDefaults.DefaultWindowWidth;

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
					InvalidateBorderCache();
					// Layout will be updated lazily on next event
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the Z-order index for layering windows.
		/// </summary>
		public int ZIndex { get; set; }

		/// <summary>
		/// Gets or sets the border style for this window.
		/// When set to BorderStyle.None, border areas render as spaces with window background color
		/// while preserving layout dimensions.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set
			{
				if (_borderStyle != value)
				{
					_borderStyle = value;
					InvalidateBorderCache();
					Invalidate(false);
				}
			}
		}

		/// <summary>
		/// Gets or sets whether the window title is displayed in the title bar.
		/// When false, only the border is shown without the title text.
		/// </summary>
		public bool ShowTitle
		{
			get => _showTitle;
			set
			{
				if (_showTitle != value)
				{
					_showTitle = value;
					Invalidate(false);
				}
			}
		}

	/// <inheritdoc/>
	public void AddControl(IWindowControl content)
	{
		lock (_lock)
		{
			// Delegate to content manager for core collection management
			_contentManager.AddControl(_controls, _interactiveContents, content, this);

			// Handle focus logic for interactive controls
			if (content is IInteractiveControl interactiveContent)
			{
				if (!_interactiveContents.Any(p => p.HasFocus))
				{
					interactiveContent.HasFocus = true;
					_lastFocusedControl = interactiveContent;
					// Sync with FocusStateService
					FocusService?.SetFocus(this, interactiveContent, FocusChangeReason.Programmatic);
				}
			}

			// Trigger re-render
			RenderAndGetVisibleContent();

			// Auto-scroll to bottom for non-sticky controls if nothing is focused
			if (content.StickyPosition == StickyPosition.None && !_interactiveContents.Any(p => p.HasFocus))
				GoToBottom();
		}
	}
	/// <summary>
	/// Removes all controls from the window.
	/// </summary>
	public void ClearControls()
	{
		lock (_lock)
		{
			// Dispose all controls first
			foreach (var content in _controls.ToList())
			{
				content.Dispose();
			}

			// Clear focus tracking
			_lastFocusedControl = null;
			FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);

			// Delegate to content manager for core clearing
			_contentManager.ClearControls(_controls, _interactiveContents);
		}
	}

	/// <summary>
	/// Gets a flattened list of all focusable controls by recursively traversing IContainerControl children.
		/// Focusable containers are "opaque" — they are added to the list but their children are NOT
		/// included (the container handles internal Tab navigation for its children).
		/// Non-focusable containers are "transparent" — they are skipped but their children are included.
		/// </summary>
		internal List<IInteractiveControl> GetAllFocusableControlsFlattened()
		{
			var result = new List<IInteractiveControl>();

			void RecursiveAdd(IWindowControl control)
			{
				// If this is a container, check if it's focusable
				if (control is Controls.IContainerControl container)
				{
					bool isFocusable = false;
					if (control is Controls.IFocusableControl fc)
					{
						isFocusable = fc.CanReceiveFocus;
					}
					else if (control is IInteractiveControl ic)
					{
						isFocusable = ic.IsEnabled;
					}

					// Focusable container: add it and STOP (it handles internal Tab)
					if (isFocusable && control is IInteractiveControl interactiveContainer)
					{
						result.Add(interactiveContainer);
						return; // Don't recurse — container is opaque
					}

					// Non-focusable container: transparent — recurse into children
					foreach (var child in container.GetChildren())
					{
						RecursiveAdd(child);
					}
				}
				// Leaf control - check if it's interactive/focusable
				else if (control is IInteractiveControl interactive)
				{
					if (control is Controls.IFocusableControl fc)
					{
						if (fc.CanReceiveFocus)
						{
							result.Add(interactive);
						}
					}
					else
					{
						if (interactive.IsEnabled)
						{
							result.Add(interactive);
						}
					}
				}
			}

			// Start from top-level controls
			foreach (var control in _controls.Where(c => c.Visible))
			{
				RecursiveAdd(control);
			}

			return result;
		}

	/// <summary>
	/// Checks if the window can be closed by firing the OnClosing event.
	/// Does not modify any state - only queries whether close is allowed.
	/// </summary>
	/// <param name="force">If true, bypasses IsClosable check and ignores Allow from OnClosing.
		/// The OnClosing event is still fired so handlers can perform pre-close work.</param>
		/// <returns>True if the window can be closed; false if close was cancelled (only when force=false).</returns>
		public bool TryClose(bool force = false)
		{
			// Already closing - allow it to proceed
			if (_isClosing) return true;

			// Check IsClosable (unless forced)
			if (!force && !IsClosable) return false;

			// Fire OnClosing event
			if (OnClosing != null)
			{
				var args = new ClosingEventArgs(force);
				OnClosing(this, args);

				// Only respect Allow if not forced
				if (!force && !args.Allow)
				{
					return false;  // Close cancelled by handler
				}
			}

			return true;
		}

		/// <summary>
		/// Attempts to close the window.
		/// If the window is in a system, delegates to CloseWindow() for proper cleanup.
		/// </summary>
		/// <param name="force">If true, forces the window to close even if IsClosable is false or OnClosing cancels.</param>
		/// <returns>True if the window was closed or close was initiated; false if closing was cancelled.</returns>
		public bool Close(bool force = false)
		{
			// Prevent re-entrancy
			if (_isClosing) return true;

			// Handle async thread cleanup with timeout (must happen before CloseWindow removes from system)
			if (_windowThreadCts != null && _windowTask != null)
			{
				// Check if close is allowed first
				if (!TryClose(force))
				{
					return false;  // Close cancelled - nothing changed
				}

				// Commit to closing
				_isClosing = true;

				_windowThreadCts.Cancel();

				var cts = _windowThreadCts;
				var task = _windowTask;
				_windowThreadCts = null;
				_windowTask = null;

				// Start grace period with visual feedback
				// When done, it will call CloseWindow() to remove from system
				BeginGracePeriodClose(task, cts);
				return true; // Close initiated (not completed yet)
			}

			// No async thread - delegate to CloseWindow if window is in a system
			if (_windowSystem != null)
			{
				bool closedBySystem = _windowSystem.CloseWindow(this, force: force);
				if (closedBySystem)
					return true;
				// Window wasn't registered in system - fall through to orphan handling
			}

			// Orphan window (not in a system OR system couldn't close it) - handle locally
			if (!TryClose(force))
			{
				return false;  // Close cancelled - nothing changed
			}

			_isClosing = true;
			CompleteClose();
			return true;
		}

		/// <summary>
		/// Begins the grace period for window thread cleanup with visual feedback.
		/// </summary>
		private void BeginGracePeriodClose(Task windowTask, CancellationTokenSource cts)
		{
			Windows.WindowLifecycleHelper.BeginGracePeriodClose(this, windowTask, cts);
		}

		/// <summary>
		/// Completes the window close operation by disposing controls and firing OnClosed event.
		/// Called by ConsoleWindowSystem after removing the window from collections.
		/// </summary>
		internal void CompleteClose()
		{
			OnClosed?.Invoke(this, EventArgs.Empty);

			foreach (var content in _controls.ToList())
			{
				InvalidationManager.Instance.UnregisterControl(content as IWindowControl);
				(content as IWindowControl)?.Dispose();
			}
		}

		/// <summary>
		/// Transforms this window into an error boundary showing hung thread information.
		/// </summary>
		private void TransformToErrorWindow(IWindowControl? statusControl)
		{
			Windows.WindowLifecycleHelper.TransformToErrorWindow(this, statusControl);
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
				if (_windowSystem == null) return null;

				// Translate the coordinates to the relative position within the window
				var relativePosition = GeometryHelpers.TranslateToRelative(this, point, _windowSystem.DesktopUpperLeft.Y);

				return _eventDispatcher?.GetControlAtPosition(relativePosition);
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
		/// Finds a control by name, searching recursively through all containers.
		/// </summary>
		/// <typeparam name="T">The type of control to find.</typeparam>
		/// <param name="name">The name of the control to find.</param>
		/// <returns>The control if found, otherwise null.</returns>
		public T? FindControl<T>(string name) where T : class, IWindowControl
		{
			lock (_lock)
			{
				return FindControlRecursive(_controls, name) as T;
			}
		}

		/// <summary>
		/// Finds a control by name, searching recursively through all containers.
		/// </summary>
		/// <param name="name">The name of the control to find.</param>
		/// <returns>The control if found, otherwise null.</returns>
		public IWindowControl? FindControl(string name)
		{
			lock (_lock)
			{
				return FindControlRecursive(_controls, name);
			}
		}

		/// <summary>
		/// Gets all named controls as a dictionary for batch access.
		/// </summary>
		/// <returns>A dictionary mapping control names to controls.</returns>
		public IReadOnlyDictionary<string, IWindowControl> GetNamedControls()
		{
			var result = new Dictionary<string, IWindowControl>();
			lock (_lock)
			{
				CollectNamedControls(_controls, result);
			}
			return result;
		}

		private static IWindowControl? FindControlRecursive(IEnumerable<IWindowControl> controls, string name)
		{
			foreach (var control in controls)
			{
				if (control.Name == name)
					return control;

				// Search nested containers
				var nested = GetNestedControls(control);
				if (nested != null)
				{
					var found = FindControlRecursive(nested, name);
					if (found != null)
						return found;
				}
			}
			return null;
		}

		private static void CollectNamedControls(IEnumerable<IWindowControl> controls, Dictionary<string, IWindowControl> result)
		{
			foreach (var control in controls)
			{
				if (!string.IsNullOrEmpty(control.Name) && !result.ContainsKey(control.Name))
				{
					result[control.Name] = control;
				}

				// Collect from nested containers
				var nested = GetNestedControls(control);
				if (nested != null)
				{
					CollectNamedControls(nested, result);
				}
			}
		}

		private static IEnumerable<IWindowControl>? GetNestedControls(IWindowControl control)
		{
			return control switch
			{
				ToolbarControl toolbar => toolbar.Items,
				HorizontalGridControl grid => grid.Columns.SelectMany(c => c.Contents),
				ColumnContainer column => column.Contents,
			ScrollablePanelControl panel => panel.Children,
				_ => null
			};
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
			_scrollOffset = Math.Max(0, (_cachedContent?.Count ?? 0) - (Height - 2));
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
		/// Scrolls the window to ensure the specified control is visible in the viewport.
		/// If the control is already fully visible, no scrolling occurs.
		/// Handles both partially visible and completely off-screen controls.
		/// </summary>
		/// <param name="control">The control to scroll into view</param>
		public void ScrollToControl(IWindowControl control)
		{
			if (_layoutManager == null || _renderer == null) return;

			try
			{
				// CRITICAL: Force layout update to get fresh widget positions
				// Without this, we get stale cached bounds from before the previous scroll
				Invalidate(true);

				var bounds = _layoutManager.GetOrCreateControlBounds(control);
				if (bounds == null) return;

				var controlBounds = bounds.ControlContentBounds;
				int contentTop = controlBounds.Y;
				int contentHeight = controlBounds.Height;
				int contentBottom = contentTop + contentHeight;

				int windowHeight = Height;
				int currentScrollOffset = ScrollOffset;

				// IMPORTANT: Control bounds Y values are RELATIVE to scrollOffset, not absolute!
				// When scrollOffset=0, widget at absolute Y=50 has relative Y=50
				// When scrollOffset=50, same widget has relative Y=0
				int visibleTop = 0;
				int visibleBottom = windowHeight - 2;  // -2 for window borders

				// Check if widget is not fully visible
				bool topCutOff = contentTop < visibleTop;
				bool bottomCutOff = contentBottom > visibleBottom;

				if (topCutOff)
				{
					// Widget top is cut off - scroll up to align top with viewport top
					int absoluteY = currentScrollOffset + contentTop;
					ScrollOffset = Math.Max(0, absoluteY);
					Invalidate(true);
				}
				else if (bottomCutOff)
				{
					// Widget bottom is cut off - scroll down to show widget
					int absoluteTopY = currentScrollOffset + contentTop;
					int newOffset = Math.Min(absoluteTopY, _renderer.MaxScrollOffset);
					ScrollOffset = Math.Max(0, newOffset);
					Invalidate(true);
				}

				// If neither topCutOff nor bottomCutOff, widget is already fully visible - no scroll needed
			}
			catch
			{
				// If layout access fails, widget may be off-screen but won't crash
			}
		}

		/// <summary>
		/// Finds the deepest focused control by recursively checking containers
		/// </summary>
		internal IWindowControl? FindDeepestFocusedControl(IInteractiveControl control)
		{
			// Search all container children recursively — HasFocus does NOT propagate
			// upward through containers, so we must traverse into every container to find
			// the deepest leaf control that actually has focus.
			if (control is Controls.IContainerControl container)
			{
				foreach (var child in container.GetChildren())
				{
					if (child is IInteractiveControl interactive)
					{
						var result = FindDeepestFocusedControl(interactive);
						if (result != null)
							return result;
					}
				}
			}

			return control.HasFocus ? control as IWindowControl : null;
		}

		/// <summary>
		/// Checks if a cursor position is visible within the current window viewport
		/// </summary>
		/// <param name="cursorPosition">The cursor position in window coordinates</param>
		/// <param name="control">The control that owns the cursor</param>
		/// <returns>True if the cursor position is visible</returns>
		internal bool IsCursorPositionVisible(Point cursorPosition, IWindowControl control)
		{
			// Get the control's bounds to understand its positioning
			var bounds = _layoutManager.GetOrCreateControlBounds(control);
			if (bounds == null) return false;
			var controlBounds = bounds.ControlContentBounds;

			// For nested controls, ControlContentBounds is never populated (only top-level controls get it).
			// Fall back to the DOM node's AbsoluteBounds which tracks all controls including nested ones.
			if (controlBounds.Width == 0 && controlBounds.Height == 0)
			{
				var node = _renderer?.GetLayoutNode(control);
				if (node == null) return false;
				var ab = node.AbsoluteBounds;
				controlBounds = new Rectangle(ab.X, ab.Y, ab.Width, ab.Height);
			}

			// Convert cursor position from window coordinates to window content coordinates
			// Window coordinates have border at (0,0), content starts at (1,1)
			// Window content coordinates (used by ControlContentBounds) have content at (0,0)
			var cursorInContentCoords = new Point(cursorPosition.X - 1, cursorPosition.Y - 1);

			// Check if cursor is within the control's actual content bounds
			if (cursorInContentCoords.X < controlBounds.X ||
				cursorInContentCoords.X >= controlBounds.X + controlBounds.Width ||
				cursorInContentCoords.Y < controlBounds.Y ||
				cursorInContentCoords.Y >= controlBounds.Y + controlBounds.Height)
			{
				return false;
			}


			// For sticky controls, the cursor is visible if it's within the window bounds
			// (bounds check already passed above)
			if (control.StickyPosition == StickyPosition.Top || control.StickyPosition == StickyPosition.Bottom)
			{
				// Sticky controls are always visible if within window bounds
				var result = cursorPosition.X >= 1 && cursorPosition.X < Width - 1 &&
							 cursorPosition.Y >= 1 && cursorPosition.Y < Height - 1;
				return result;
			}
			else
			{
				// For scrollable (non-sticky) controls, check if cursor is within window viewport
				var scrollableAreaTop = 1;
				var scrollableAreaBottom = Height - 1;


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
				var result = controlBottom > visibleScrollTop && controlTop < visibleScrollBottom;
				return result;
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
					// Invalidate measurements without rebuilding the tree
					// This preserves runtime state like splitter positions
					_renderer?.InvalidateDOMLayout();
				}
				else if (callerControl != null)
				{
					// Specific control invalidation
					var node = _renderer?.GetLayoutNode(callerControl);
					if (node != null)
					{
						node.InvalidateMeasure();
					}
					else
					{
						// Fallback: invalidate entire tree
						_renderer?.InvalidateDOMLayout();
					}
				}
			}

			IsDirty = true;
		}

		/// <summary>
		/// Invalidates cached border strings, forcing them to be regenerated on next render.
		/// Called when properties affecting border rendering change (width, height, title, border style, active state).
		/// </summary>
		internal void InvalidateBorderCache()
		{
			_borderRenderer?.InvalidateCache();
		}

		/// <summary>
		/// Gets the BorderRenderer instance for this window.
		/// </summary>
		internal Windows.BorderRenderer? BorderRenderer => _borderRenderer;

		/// <summary>
		/// Gets the WindowEventDispatcher instance for this window.
		/// </summary>
		internal Windows.WindowEventDispatcher? EventDispatcher => _eventDispatcher;

		/// <summary>
		/// Gets or sets the cached top border string (exposed for Renderer.cs access).
		/// </summary>
		internal string? _cachedTopBorder
		{
			get => _borderRenderer?._cachedTopBorder;
			set { if (_borderRenderer != null) _borderRenderer._cachedTopBorder = value; }
		}

		/// <summary>
		/// Gets or sets the cached bottom border string (exposed for Renderer.cs access).
		/// </summary>
		internal string? _cachedBottomBorder
		{
			get => _borderRenderer?._cachedBottomBorder;
			set { if (_borderRenderer != null) _borderRenderer._cachedBottomBorder = value; }
		}

		/// <summary>
		/// Gets or sets the cached vertical border string (exposed for Renderer.cs access).
		/// </summary>
		internal string? _cachedVerticalBorder
		{
			get => _borderRenderer?._cachedVerticalBorder;
			set { if (_borderRenderer != null) _borderRenderer._cachedVerticalBorder = value; }
		}

		/// <summary>
		/// Gets or sets the cached border width (exposed for Renderer.cs access).
		/// </summary>
		internal int _cachedBorderWidth
		{
			get => _borderRenderer?._cachedBorderWidth ?? -1;
			set { if (_borderRenderer != null) _borderRenderer._cachedBorderWidth = value; }
		}

		/// <summary>
		/// Gets or sets the cached border active state (exposed for Renderer.cs access).
		/// </summary>
		internal bool _cachedBorderIsActive
		{
			get => _borderRenderer?._cachedBorderIsActive ?? false;
			set { if (_borderRenderer != null) _borderRenderer._cachedBorderIsActive = value; }
		}

		/// <summary>
		/// Forces a complete rebuild of the DOM tree. Use this when the control hierarchy changes
		/// structurally (e.g., adding/removing columns in a grid) rather than just property changes.
		/// This is more expensive than Invalidate() but necessary for structural changes.
		/// </summary>
		public void ForceRebuildLayout()
		{
			lock (_lock)
			{
				_renderer?.InvalidateDOM(); // Force rebuild on next render
			}
			IsDirty = true;
			_invalidated = true;
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
						// Found the target - get its size using DOM
						var size = GetControlHeight(child);
						return (offset, size);
					}

					// Check if target is nested deeper
					var nestedResult = FindNestedControlPosition(child, target);
					if (nestedResult.Height > 0)
					{
						return (offset + nestedResult.Offset, nestedResult.Height);
					}

					// Add this child's height to the offset
					offset += GetControlHeight(child);
				}
			}
			return (-1, 0);
		}

		// Get control height using DOM or MeasureDOM
		private int GetControlHeight(IWindowControl control)
		{
			// Try to get from DOM node first
			var node = _renderer?.GetLayoutNode(control);
			if (node != null)
			{
				return node.AbsoluteBounds.Height;
			}

			// Fallback to MeasureDOM
			if (control is IDOMPaintable paintable)
			{
				var size = paintable.MeasureDOM(new LayoutConstraints(0, Width - 2, 0, Height - 2));
				return size.Height;
			}

			// Last resort
			return control.GetLogicalContentSize().Height;
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
		/// Called by controls when they gain focus (via SetFocus).
		/// Updates Window's focus tracking to keep _lastFocusedControl in sync.
		/// </summary>
		/// <param name="control">The control that gained focus.</param>
		public void NotifyControlGainedFocus(IInteractiveControl control)
		{
			bool isTopLevel = control != null && _interactiveContents.Contains(control);
			_windowSystem?.LogService?.LogTrace($"NotifyControlGainedFocus: {control?.GetType().Name} isTopLevel={isTopLevel} (only top-level updates _lastFocusedControl)", "Focus");
			if (isTopLevel)
			{
				_lastFocusedControl = control;
				FocusService?.SetFocus(this, control!, FocusChangeReason.Programmatic);
			}
		}

		/// <summary>
		/// Called by controls when they lose focus (via SetFocus).
		/// Updates Window's focus tracking to keep _lastFocusedControl in sync.
		/// </summary>
		/// <param name="control">The control that lost focus.</param>
		public void NotifyControlLostFocus(IInteractiveControl control)
		{
			bool isTracked = control != null && _lastFocusedControl == control;
			_windowSystem?.LogService?.LogTrace($"NotifyControlLostFocus: {control?.GetType().Name} isTracked={isTracked} _lastFocused={_lastFocusedControl?.GetType().Name ?? "null"}", "Focus");

			// Clear tracking if this was the last focused control AND it actually lost focus
			// (not just a child inside it losing focus while the container maintains focus)
			if (isTracked && control is Controls.IFocusableControl focusable && !focusable.HasFocus)
			{
				_lastFocusedControl = null;
				FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
			}
		}

		/// <summary>
		/// Sets focus to the specified control in this window.
		/// This is the recommended way to programmatically change focus, as it properly
		/// updates Window's internal focus tracking and unfocuses the previously focused control.
		/// </summary>
		/// <param name="control">The control to focus, or null to clear focus entirely.</param>
		public void FocusControl(IInteractiveControl? control)
		{
			// Unfocus currently focused control
			if (_lastFocusedControl != null && _lastFocusedControl is Controls.IFocusableControl currentFocusable)
			{
				currentFocusable.SetFocus(false, Controls.FocusReason.Programmatic);
			}

			// Focus new control
			if (control != null && control is Controls.IFocusableControl newFocusable && newFocusable.CanReceiveFocus)
			{
				// SetFocus() will call NotifyParentWindowOfFocusChange(), which handles tracking
				newFocusable.SetFocus(true, Controls.FocusReason.Programmatic);
				// Notification system now handles _lastFocusedControl and FocusService updates
			}
			else
			{
				_lastFocusedControl = null;
				FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
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
			// Handle focus logic before removing
			if (content is IInteractiveControl interactiveControl)
			{
				// If the removed content was the last focused control, clear it
				if (_lastFocusedControl == interactiveControl)
				{
					_lastFocusedControl = null;
					FocusService?.ClearControlFocus(FocusChangeReason.Programmatic);
				}

				// If the removed content had focus, switch focus to the next one
				if (interactiveControl.HasFocus && _interactiveContents.Count > 1)
				{
					// Find next interactive control (after removal)
					var nextControl = _interactiveContents.FirstOrDefault(ic => ic != interactiveControl);
					if (nextControl != null)
					{
						nextControl.HasFocus = true;
						_lastFocusedControl = nextControl;
						FocusService?.SetFocus(this, nextControl, FocusChangeReason.Programmatic);
					}
				}
			}

			// Delegate to content manager for core removal
			if (_contentManager.RemoveControl(_controls, _interactiveContents, content))
			{
				// Dispose the control
				content.Dispose();

				// Trigger re-render
				RenderAndGetVisibleContent();

				// Auto-scroll to bottom
				GoToBottom();
			}
		}
	}

		/// <summary>
		/// Renders the window content and returns the visible lines.
		/// </summary>
		/// <param name="visibleRegions">Optional list of screen-space rectangles representing visible portions of the window.
		/// If provided, only these regions will be painted (optimization to avoid painting occluded areas).</param>
		/// <returns>A list of rendered content lines visible within the window viewport.</returns>
		public List<string> RenderAndGetVisibleContent(List<Rectangle>? visibleRegions = null)
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
					// Layout will be updated lazily on next event
					RebuildContentCache(visibleRegions);

					// Check if visibleRegions is null or empty (window not in rendering pipeline yet)
					bool isInRenderingPipeline = visibleRegions != null && visibleRegions.Count > 0;

					// If we rendered without visible regions (during window creation),
					// keep _invalidated=true so we re-render when actually in the pipeline
					if (!isInRenderingPipeline)
					{
						_invalidated = true;
					}
				}

				return BuildVisibleContent();
			}
		}

		private void RebuildContentCache(List<Rectangle>? visibleRegions = null)
		{
			var availableWidth = Width - 2; // Account for borders
			var availableHeight = Height - 2; // Account for borders

			// Always use DOM-based layout
			RebuildContentCacheDOM(availableWidth, availableHeight, visibleRegions);
		}

		private List<string> BuildVisibleContent()
		{
			var availableHeight = Height - 2; // Account for borders

			// DOM mode: _cachedContent already contains the viewport-sized content
			var result = _cachedContent?.Take(availableHeight).ToList() ?? new List<string>();
			while (result.Count < availableHeight)
			{
				result.Add(string.Empty);
			}
			return result;
		}

		#region DOM-Based Layout System

		/// <summary>
		/// Gets the root layout node for the window's DOM tree.
		/// Used internally for layout traversal (e.g., collecting control bounds for overlay rendering).
		/// </summary>
		internal LayoutNode? GetRootLayoutNode() => _renderer?.RootLayoutNode;

		/// <summary>
		/// Gets the LayoutNode associated with a control.
		/// </summary>
		/// <param name="control">The control to look up.</param>
		/// <returns>The LayoutNode for the control, or null if not found.</returns>
		public LayoutNode? GetLayoutNode(IWindowControl control)
		{
			return _renderer?.GetLayoutNode(control);
		}

		/// <summary>
		/// Creates a portal overlay for the specified control.
		/// Portal content renders on top of all normal content with no parent clipping.
		/// Portals are useful for dropdowns, tooltips, context menus, and other overlay content.
		/// </summary>
		/// <param name="ownerControl">The control creating the portal.</param>
		/// <param name="portalContent">The content to render as an overlay.</param>
		/// <returns>The portal LayoutNode for later removal, or null if owner not found.</returns>
		public LayoutNode? CreatePortal(IWindowControl ownerControl, IWindowControl portalContent)
		{
			var portalNode = _renderer?.CreatePortal(ownerControl, portalContent);
			if (portalNode != null)
			{
				Invalidate(false);
			}
			return portalNode;
		}

		/// <summary>
		/// Removes a portal overlay created by CreatePortal().
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal.</param>
		/// <param name="portalNode">The portal LayoutNode returned by CreatePortal().</param>
		public void RemovePortal(IWindowControl ownerControl, LayoutNode portalNode)
		{
			_renderer?.RemovePortal(ownerControl, portalNode);
			Invalidate(false);
		}

		/// <summary>
		/// Gets the root layout node for this window.
		/// </summary>
		public LayoutNode? RootLayoutNode => _renderer?.RootLayoutNode;

		/// <summary>
		/// Gets whether DOM-based layout is enabled.
		/// DOM layout is always enabled and is the only rendering path.
		/// </summary>
		[Obsolete("DOM layout is now always enabled. This property will be removed.")]
		public bool UseDOMLayout => true;

		/// <summary>
		/// Rebuilds the DOM tree from the current controls.
		/// </summary>
		internal void RebuildDOMTree()
		{
			var contentWidth = Width - 2;
			var contentHeight = Height - 2;
			_renderer?.RebuildDOMTree(_controls, contentWidth, contentHeight);
		}


		/// <summary>
		/// Performs the measure and arrange passes on the DOM tree.
		/// </summary>
		private void PerformDOMLayout()
		{
			var contentWidth = Width - 2;
			var contentHeight = Height - 2;
			_renderer?.PerformDOMLayout(contentWidth, contentHeight);
		}

		/// <summary>
		/// Paints the DOM tree to the character buffer.
		/// </summary>
		/// <param name="clipRect">The clipping rectangle in window-space coordinates. Only content within this rect will be painted.</param>
		private void PaintDOM(LayoutRect clipRect)
		{
			_renderer?.PaintDOM(clipRect, BackgroundColor);
		}

		/// <summary>
		/// Invalidates the DOM layout, triggering a re-measure and re-arrange.
		/// </summary>
		private void InvalidateDOMLayout()
		{
			_renderer?.InvalidateDOMLayout();
		}


		/// <summary>
		/// Rebuilds the content cache using DOM-based layout.
		/// Converts the CharacterBuffer output to line-based format for compatibility.
		/// </summary>
		private void RebuildContentCacheDOM(int availableWidth, int availableHeight, List<Rectangle>? visibleRegions = null)
		{
			if (_renderer == null)
			{
				_cachedContent = new List<string>();
				_invalidated = false;
				return;
			}

			// Delegate to renderer for complete rendering pipeline
			_cachedContent = _renderer.RebuildContentCacheDOM(
				_controls,
				availableWidth,
				availableHeight,
				visibleRegions,
				Left,
				Top,
				ShowTitle,
				ForegroundColor,
				BackgroundColor);

			// Clear sticky tracking (DOM handles sticky internally)
			_topStickyLines.Clear();
			_topStickyHeight = 0;
			_bottomStickyLines.Clear();
			_bottomStickyHeight = 0;
			_controlPositions.Clear();

			_invalidated = false;
		}

		#endregion

		/// <summary>
		/// Maximizes the window to fill the entire desktop area.
		/// </summary>
		/// <param name="force">
		/// If true, bypasses the <see cref="IsMaximizable"/> check and forces maximization.
		/// Default is false, which respects the <see cref="IsMaximizable"/> property.
		/// Use force=true for programmatic maximization that should override user preferences.
		/// </param>
		/// <remarks>
		/// When force is false (default), the method will silently return if IsMaximizable is false.
		/// This maintains backward compatibility with existing code.
		/// </remarks>
		public void Maximize(bool force = false)
		{
			if (!force && !IsMaximizable)
				return;
			State = WindowState.Maximized;
		}

		/// <summary>
		/// Minimizes the window.
		/// </summary>
		/// <param name="force">
		/// If true, bypasses the <see cref="IsMinimizable"/> check and forces minimization.
		/// Default is false, which respects the <see cref="IsMinimizable"/> property.
		/// Use force=true for programmatic minimization (e.g., UAC-style dialogs) that
		/// should override user preferences.
		/// </param>
		/// <remarks>
		/// When force is false (default), the method will silently return if IsMinimizable is false.
		/// This maintains backward compatibility with existing code.
		/// </remarks>
		public void Minimize(bool force = false)
		{
			if (!force && !IsMinimizable)
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
		InvalidateBorderCache();

			// Invalidate window to redraw border with new active/inactive colors
			Invalidate(false);  // Border-only invalidation (redrawAll=false)

			if (_lastFocusedControl != null)
			{
				_lastFocusedControl.HasFocus = value;
				// Sync with FocusStateService
				if (value)
				{
					FocusService?.SetFocus(this, _lastFocusedControl, FocusChangeReason.WindowActivation);
				}
				else
				{
					FocusService?.ClearFocus(this, FocusChangeReason.WindowActivation);
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

			// Use public setters which go through WindowPositioningManager for proper invalidation
			Left = point.X;
			Top = point.Y;
		}

		/// <summary>
		/// Internal method to set position directly without triggering invalidation logic.
		/// Used by WindowPositioningManager to avoid recursion.
		/// Note: Negative coordinates are allowed - rendering pipeline handles them safely.
		/// </summary>
		internal void SetPositionDirect(Point point)
		{
			_left = point.X;
			_top = point.Y;
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
			if (_minimumHeight != null && height < _minimumHeight)
				height = (int)_minimumHeight;

			// Set backing fields directly to avoid property setters calling
			// UpdateControlLayout() before both dimensions are set
			_width = width;
			_height = height;

			// IMPORTANT: Invalidate controls FIRST so they clear their caches
			Invalidate(true);

			// Layout will be updated lazily on next event

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
			_eventDispatcher?.SwitchFocus(backward);
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
		/// <param name="alreadyHandled">Indicates whether the key was already handled.</param>
		/// <returns>True if the event was handled; otherwise false.</returns>
		protected internal virtual bool OnKeyPressed(ConsoleKeyInfo key, bool alreadyHandled)
		{
			var handler = KeyPressed;
			if (handler != null)
			{
				var args = new KeyPressedEventArgs(key, alreadyHandled);
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
				if (IsModal)
				{
					// Use a smaller offset for modal windows to make them look more like dialogs
					// Ensure the window fits within the parent window bounds
					Left = Math.Max(0, _parentWindow.Left + Configuration.ControlDefaults.ModalWindowLeftOffset);
					Top = Math.Max(0, _parentWindow.Top + Configuration.ControlDefaults.ModalWindowTopOffset);

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
