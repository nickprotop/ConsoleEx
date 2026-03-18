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
using SharpConsoleUI.Rendering;
using System.Drawing;

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
	public partial class Window : IContainer
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

		/// <summary>
		/// Gets the number of content lines from the buffer (used for scroll calculations).
		/// </summary>
		internal int ContentLineCount => _renderer?.Buffer?.Height ?? 0;

		private string _guid;
		private Color? _inactiveBorderForegroundColor;
		private Color? _inactiveTitleForegroundColor;
		internal bool _invalidated = false;
		private bool _isActive;
		internal IInteractiveControl? _lastFocusedControl;
		internal IInteractiveControl? _lastDeepFocusedControl;  // actual leaf control

		// Convenience property to access FocusStateService
		internal FocusStateService? FocusService => _windowSystem?.FocusStateService;

		// Single authority for all focus changes in this window
		internal FocusCoordinator? FocusCoord { get; private set; }
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

		internal Windows.WindowRenderer? Renderer => _renderer;

		/// <summary>
		/// Raised before the window content is painted to the character buffer.
		/// Attach a handler here to modify or initialize the buffer before normal rendering.
		/// </summary>
		public event Windows.WindowRenderer.BufferPaintDelegate? PreBufferPaint
		{
			add    { if (_renderer != null) _renderer.PreBufferPaint  += value; }
			remove { if (_renderer != null) _renderer.PreBufferPaint  -= value; }
		}

		/// <summary>
		/// Raised after the window content is painted to the character buffer.
		/// Attach a handler here to apply post-processing effects (blur, fade, overlays, etc.).
		/// </summary>
		public event Windows.WindowRenderer.BufferPaintDelegate? PostBufferPaint
		{
			add    { if (_renderer != null) _renderer.PostBufferPaint += value; }
			remove { if (_renderer != null) _renderer.PostBufferPaint -= value; }
		}

		/// <summary>
		/// Gets the current character buffer for this window, or null if not yet initialized.
		/// Useful for capturing buffer snapshots in transition effects.
		/// </summary>
		public Layout.CharacterBuffer? Buffer => _renderer?.Buffer;

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

		// Initialize event dispatcher and focus coordinator
		_eventDispatcher = new Windows.WindowEventDispatcher(this);
		FocusCoord = new Core.FocusCoordinator(this);

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

		// Initialize event dispatcher and focus coordinator
		_eventDispatcher = new Windows.WindowEventDispatcher(this);
		FocusCoord = new Core.FocusCoordinator(this);

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
		/// Occurs before a key is dispatched to the focused control.
		/// Set <see cref="KeyPressedEventArgs.Handled"/> to true to prevent controls from seeing the key.
		/// </summary>
		public event EventHandler<KeyPressedEventArgs>? PreviewKeyPressed;

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

		private GradientBackground? _backgroundGradient;

		/// <summary>
		/// Gets or sets the gradient background for this window.
		/// When set, the gradient is rendered over the solid background color before controls are painted.
		/// Set to null to disable gradient background.
		/// </summary>
		public GradientBackground? BackgroundGradient
		{
			get => _backgroundGradient;
			set
			{
				_backgroundGradient = value;
				Invalidate(true);
			}
		}

		bool IContainer.HasGradientBackground => _backgroundGradient != null;

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

		internal bool IsDragging { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the window can be moved by the user.
		/// </summary>
		public bool IsMovable { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the window can be resized by the user.
		/// </summary>
		public bool IsResizable { get; set; } = true;

		/// <summary>
		/// Gets or sets per-border movement permissions for resizing. Only meaningful when
		/// <see cref="IsResizable"/> is <c>true</c>. Defaults to <see cref="ResizeBorderDirections.All"/>.
		/// </summary>
		public ResizeBorderDirections AllowedResizeDirections { get; set; } = ResizeBorderDirections.All;

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

	internal bool RenderLock
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

		internal int OriginalHeight { get; set; }
		internal int OriginalLeft { get; set; }
		internal int OriginalTop { get; set; }
		internal int OriginalWidth { get; set; }

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
				return ContentLineCount + _topStickyHeight;
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
	}
}
