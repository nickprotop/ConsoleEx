// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A scrollable panel control that can host child controls with automatic scrolling support.
	/// Supports vertical and horizontal scrolling, mouse wheel, and visual scrollbars.
	/// </summary>
	public class ScrollablePanelControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer, IDOMPaintable, IDirectionalFocusControl, IContainerControl, IScrollableContainer, IFocusTrackingContainer
	{
		private readonly List<IWindowControl> _children = new();
		private int _verticalScrollOffset = 0;
		private int _horizontalScrollOffset = 0;
		private int _contentHeight = 0;
		private int _contentWidth = 0;
		private int _viewportHeight = 0;
		private int _viewportWidth = 0;
		private bool _hasFocus = false;
		private bool _isEnabled = true;
		private IInteractiveControl? _focusedChild = null;
		private IInteractiveControl? _lastInternalFocusedChild = null;
		private bool _focusFromBackward = false;

		// Click target tracking for double-click consistency
		private IWindowControl? _lastClickTarget = null;
		private DateTime _lastClickTime;
		private System.Drawing.Point _lastClickPosition;

		// Scrollbar drag state
		private bool _isScrollbarDragging = false;
		private int _scrollbarDragStartY = 0;
		private int _scrollbarDragStartOffset = 0;

		// Configurable options
		private bool _showScrollbar = true;
		private ScrollbarPosition _scrollbarPosition = ScrollbarPosition.Right;
		private ScrollMode _horizontalScrollMode = ScrollMode.None;
		private ScrollMode _verticalScrollMode = ScrollMode.Scroll;
		private bool _enableMouseWheel = true;
		private bool _autoScroll = false;

		// IWindowControl properties
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private int? _height;

		// IContainer properties
		private Color? _backgroundColorValue;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollablePanelControl"/> class.
		/// </summary>
		public ScrollablePanelControl()
		{
		}

		/// <summary>
		/// Creates a new builder for configuring a ScrollablePanelControl
		/// </summary>
		/// <returns>A new builder instance</returns>
		public static Builders.ScrollablePanelBuilder Create()
		{
			return new Builders.ScrollablePanelBuilder();
		}

		#region Events

		/// <summary>
		/// Event fired when the panel is scrolled.
		/// </summary>
		public event EventHandler<ScrollEventArgs>? Scrolled;

		/// <summary>
		/// Event fired when the control gains focus.
		/// </summary>
		public event EventHandler? GotFocus;

		/// <summary>
		/// Event fired when the control loses focus.
		/// </summary>
		public event EventHandler? LostFocus;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <summary>
		/// Event fired when the control is clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Event fired when the control is double-clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Event fired when the mouse enters the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <summary>
		/// Event fired when the mouse leaves the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Event fired when the mouse moves over the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		#endregion

		#region Configuration Properties

		/// <summary>
		/// Gets or sets whether to show the scrollbar.
		/// </summary>
		public bool ShowScrollbar
		{
			get => _showScrollbar;
			set { _showScrollbar = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the position of the scrollbar.
		/// </summary>
		public ScrollbarPosition ScrollbarPosition
		{
			get => _scrollbarPosition;
			set { _scrollbarPosition = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the horizontal scroll mode.
		/// </summary>
		public ScrollMode HorizontalScrollMode
		{
			get => _horizontalScrollMode;
			set { _horizontalScrollMode = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the vertical scroll mode.
		/// </summary>
		public ScrollMode VerticalScrollMode
		{
			get => _verticalScrollMode;
			set { _verticalScrollMode = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets whether mouse wheel scrolling is enabled.
		/// </summary>
		public bool EnableMouseWheel
		{
			get => _enableMouseWheel;
			set => _enableMouseWheel = value;
		}

		/// <summary>
		/// Gets or sets whether to automatically scroll to bottom when content is added.
		/// When enabled, scrolls to bottom on AddControl if currently at/near bottom.
		/// Disables automatically when user scrolls up, re-enables when user scrolls to bottom.
		/// </summary>
		public bool AutoScroll
		{
			get => _autoScroll;
			set => _autoScroll = value;
		}

		/// <summary>
		/// Gets the current vertical scroll offset in lines.
		/// </summary>
		public int VerticalScrollOffset => _verticalScrollOffset;

		/// <summary>
		/// Gets the current horizontal scroll offset in characters.
		/// </summary>
		public int HorizontalScrollOffset => _horizontalScrollOffset;

		/// <summary>
		/// Gets the total height of the scrollable content area in lines.
		/// Unlike <see cref="ContentHeight"/> (which returns the control's own height),
		/// this returns the height of the inner content that may extend beyond the viewport.
		/// </summary>
		public int TotalContentHeight => _contentHeight;

		/// <summary>
		/// Gets the total width of the scrollable content area in characters.
		/// Unlike <see cref="ContentWidth"/> (which returns the control's own width),
		/// this returns the width of the inner content that may extend beyond the viewport.
		/// </summary>
		public int TotalContentWidth => _contentWidth;

		/// <summary>
		/// Gets the height of the visible viewport area in lines.
		/// </summary>
		public int ViewportHeight => _viewportHeight;

		/// <summary>
		/// Gets the width of the visible viewport area in characters.
		/// </summary>
		public int ViewportWidth => _viewportWidth;

		/// <summary>
		/// Gets whether the content can be scrolled upward (vertical offset is greater than zero).
		/// </summary>
		public bool CanScrollUp => _verticalScrollOffset > 0;

		/// <summary>
		/// Gets whether the content can be scrolled downward (more content exists below the viewport).
		/// </summary>
		public bool CanScrollDown => _verticalScrollOffset < Math.Max(0, _contentHeight - _viewportHeight);

		/// <summary>
		/// Gets whether the content can be scrolled left (horizontal offset is greater than zero).
		/// </summary>
		public bool CanScrollLeft => _horizontalScrollOffset > 0;

		/// <summary>
		/// Gets whether the content can be scrolled right (more content exists beyond the viewport width).
		/// </summary>
		public bool CanScrollRight => _horizontalScrollOffset < Math.Max(0, _contentWidth - _viewportWidth);

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ContentHeight => _height;

		/// <inheritdoc/>
		public int? ContentWidth => _width;

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set { _horizontalAlignment = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set { _verticalAlignment = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set { _margin = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set { _stickyPosition = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set
			{
				if (_visible != value)
				{
					_visible = value;

					// If becoming invisible and we have focus, lose it
					if (!_visible && _hasFocus)
					{
						SetFocus(false, FocusReason.Programmatic);
					}

					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set { _width = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public int? Height
		{
			get => _height;
			set => PropertySetterHelper.SetDimensionProperty(ref _height, value, Container);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			int height = _children.Where(c => c.Visible).ToList().Sum(c => c.GetLogicalContentSize().Height);
			int width = _width ?? 80;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			foreach (var child in _children.ToList())
			{
				child.Dispose();
			}
			_children.Clear();
			Container = null;
		}

		#endregion

		#region IInteractiveControl Implementation

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				// Use SetFocus to properly handle focus changes including child delegation
				SetFocus(value, FocusReason.Programmatic);
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set { _isEnabled = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			var log = GetConsoleWindowSystem?.LogService;
			log?.LogTrace($"ScrollPanel.ProcessKey({key.Key}): _hasFocus={_hasFocus} _isEnabled={_isEnabled} _focusedChild={_focusedChild?.GetType().Name ?? "null"} _focusedChild.HasFocus={(_focusedChild as IFocusableControl)?.HasFocus}", "Focus");

			if (!_hasFocus || !_isEnabled) return false;

			// FIRST: Delegate to focused child if we have one
			if (_focusedChild != null && _focusedChild.ProcessKey(key))
			{
				return true; // Child handled it
			}

			// Handle Escape: unfocus child, enter scroll mode (panel stays focused)
			if (key.Key == ConsoleKey.Escape && _focusedChild != null)
			{
				log?.LogTrace($"ScrollPanel.ProcessKey: Escape → unfocusing child {_focusedChild.GetType().Name}, entering scroll mode", "Focus");
				_lastInternalFocusedChild = _focusedChild;
				if (_focusedChild is IFocusableControl escapeFc)
					escapeFc.SetFocus(false, FocusReason.Programmatic);
				_focusedChild = null;
				Container?.Invalidate(true);
				return true;
			}

			// Handle Escape in scroll mode (no child focused): let it propagate to unfocus panel
			if (key.Key == ConsoleKey.Escape && _focusedChild == null)
			{
				log?.LogTrace("ScrollPanel.ProcessKey: Escape in scroll mode → propagating to parent", "Focus");
				_lastInternalFocusedChild = null;
				return false; // Let parent handle (will unfocus panel)
			}

			// SECOND: Handle Tab navigation through children
			if (key.Key == ConsoleKey.Tab)
			{
				bool shiftPressed = (key.Modifiers & ConsoleModifiers.Shift) != 0;

				// Tab in scroll mode: restore last focused child
				if (_focusedChild == null && _lastInternalFocusedChild != null)
				{
					log?.LogTrace($"ScrollPanel.ProcessKey: Tab in scroll mode → restoring {_lastInternalFocusedChild.GetType().Name}", "Focus");
					_focusedChild = _lastInternalFocusedChild;
					_lastInternalFocusedChild = null;
					if (_focusedChild is IFocusableControl restoreFc)
						restoreFc.SetFocus(true, FocusReason.Keyboard);
					if (_focusedChild is IWindowControl focusedWindow)
						ScrollChildIntoView(focusedWindow);
					Container?.Invalidate(true);
					return true;
				}

				var focusableChildren = _children
					.Where(c => c.Visible && c is IFocusableControl fc && fc.CanReceiveFocus)
					.Cast<IInteractiveControl>()
					.ToList();

				if (focusableChildren.Count > 0)
				{
					int currentIndex = _focusedChild != null ? focusableChildren.IndexOf(_focusedChild) : -1;

					int newIndex;
					if (shiftPressed)
					{
						// Backward
						newIndex = currentIndex - 1;
						if (newIndex < 0)
							return false; // Let Tab propagate to parent
					}
					else
					{
						// Forward
						newIndex = currentIndex + 1;
						if (newIndex >= focusableChildren.Count)
							return false; // Let Tab propagate to parent
					}

					// Unfocus current
					if (_focusedChild is IFocusableControl currentFc)
						currentFc.SetFocus(false, FocusReason.Keyboard);

					// Focus new
					_focusedChild = focusableChildren[newIndex];
					if (_focusedChild is IFocusableControl newFc)
						newFc.SetFocus(true, FocusReason.Keyboard);

					// Scroll newly focused child into view
					if (_focusedChild is IWindowControl newlyFocusedWindow)
						ScrollChildIntoView(newlyFocusedWindow);

					Container?.Invalidate(true);
					return true;
				}
			}

			// THIRD: Handle scrolling keys (only if panel needs scrolling)
			if (NeedsScrolling())
			{
				switch (key.Key)
				{
					case ConsoleKey.UpArrow:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(-1);
							return true;
						}
						break;

					case ConsoleKey.DownArrow:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(1);
							return true;
						}
						break;

					case ConsoleKey.PageUp:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(-_viewportHeight);
							return true;
						}
						break;

					case ConsoleKey.PageDown:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(_viewportHeight);
							return true;
						}
						break;

					case ConsoleKey.Home:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalTo(0);
							return true;
						}
						break;

					case ConsoleKey.End:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							_autoScroll = true;  // Explicitly re-attach
							ScrollVerticalTo(Math.Max(0, _contentHeight - _viewportHeight));
							return true;
						}
						break;

					case ConsoleKey.LeftArrow:
						if (_horizontalScrollMode == ScrollMode.Scroll)
						{
							ScrollHorizontalBy(-1);
							return true;
						}
						break;

					case ConsoleKey.RightArrow:
						if (_horizontalScrollMode == ScrollMode.Scroll)
						{
							ScrollHorizontalBy(1);
							return true;
						}
						break;
				}
			}

			return false;
		}

		#endregion

		#region IFocusableControl Implementation

		/// <inheritdoc/>
		/// <summary>
		/// ScrollablePanel is focusable when it has anything to interact with:
		/// either scrollable content or focusable children. The panel acts as an
		/// opaque focus container — it owns its children's focus lifecycle entirely.
		/// </summary>
		public bool CanReceiveFocus
		{
			get
			{
				if (!_visible || !_isEnabled) return false;

				bool needsScrolling = NeedsScrolling();
				bool hasFocusableChildren = HasFocusableChildren();

				var result = needsScrolling || hasFocusableChildren;
				GetConsoleWindowSystem?.LogService?.LogTrace($"ScrollPanel.CanReceiveFocus: needsScrolling={needsScrolling} hasFocusableChildren={hasFocusableChildren} result={result}", "Focus");
				return result;
			}
		}

		/// <inheritdoc/>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_focusFromBackward = backward;
			SetFocus(focus, FocusReason.Keyboard);
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var log = GetConsoleWindowSystem?.LogService;
			log?.LogTrace($"ScrollPanel.SetFocus({focus}, {reason}): _hasFocus={_hasFocus} _focusedChild={_focusedChild?.GetType().Name ?? "null"}", "Focus");

			if (_hasFocus == focus) return;

			var hadFocus = _hasFocus;
			_hasFocus = focus;

			if (focus)
			{
				// Getting focus - find first/last focusable child if we have any
				var focusableChildren = _children
					.Where(c => c.Visible && c is IFocusableControl fc && fc.CanReceiveFocus)
					.ToList();

				if (focusableChildren.Any())
				{
					// Focus first or last child based on direction
					_focusedChild = _focusFromBackward
						? focusableChildren.Last() as IInteractiveControl
						: focusableChildren.First() as IInteractiveControl;

					log?.LogTrace($"ScrollPanel.SetFocus: delegating to child {_focusedChild?.GetType().Name} (backward={_focusFromBackward})", "Focus");

					if (_focusedChild is IFocusableControl fc)
					{
						if (_focusedChild is IDirectionalFocusControl dfc)
							dfc.SetFocusWithDirection(true, _focusFromBackward);
						else
							fc.SetFocus(true, reason);
					}
				}
				else
				{
					log?.LogTrace("ScrollPanel.SetFocus: no focusable children, panel focused for scrolling", "Focus");
				}

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				// Losing focus - unfocus any focused child
				if (_focusedChild != null && _focusedChild is IFocusableControl fc)
				{
					log?.LogTrace($"ScrollPanel.SetFocus(false): unfocusing child {_focusedChild.GetType().Name}", "Focus");
					fc.SetFocus(false, reason);
				}
				_focusedChild = null;
				_lastInternalFocusedChild = null;

				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			Container?.Invalidate(true);

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;  // Always want mouse events for child focus

		/// <inheritdoc/>
		public bool CanFocusWithMouse
		{
			get
			{
				var result = CanReceiveFocus;
				GetConsoleWindowSystem?.LogService?.LogTrace($"ScrollPanel.CanFocusWithMouse: {result}", "Focus");
				return result;
			}
		}

		/// <inheritdoc/>
		/// <summary>
		/// Gets the target child control for a click event, using cached target for double-click sequences.
		/// This prevents the same logical double-click from being dispatched to different children
		/// when scroll position changes between clicks.
		/// </summary>
		private IWindowControl? GetClickTargetChild(MouseEventArgs args, int contentWidth)
		{
			var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;
			var positionChanged = _lastClickPosition != args.Position;

			bool isSequentialClick =
				timeSinceLastClick < Configuration.ControlDefaults.DefaultDoubleClickThresholdMs &&
				!positionChanged;

			// Reuse cached target for double-click sequences
			if (isSequentialClick && _lastClickTarget != null)
			{
				// Verify child still exists
				if (_children.Contains(_lastClickTarget))
					return _lastClickTarget;
			}

			// New click sequence - perform fresh hit test using current scroll offset
			int contentY = args.Position.Y - _margin.Top + _verticalScrollOffset;
			int currentY = 0;

			foreach (var child in _children.Where(c => c.Visible))
			{
				int childHeight = MeasureChildHeight(child, contentWidth);

				if (contentY >= currentY && contentY < currentY + childHeight)
				{
					// Found the clicked child
					_lastClickTarget = child;
					_lastClickTime = DateTime.Now;
					_lastClickPosition = args.Position;
					return child;
				}

				currentY += childHeight;
			}

			// No child found at this position
			_lastClickTarget = null;
			_lastClickTime = DateTime.Now;
			_lastClickPosition = args.Position;
			return null;
		}

		/// <inheritdoc />
	public bool ProcessMouseEvent(MouseEventArgs args)
		{
			var log = GetConsoleWindowSystem?.LogService;

			if (args.Handled) return false;

			// Handle mouse wheel scrolling
			if (_enableMouseWheel)
			{
				bool isWheel = args.HasFlag(Drivers.MouseFlags.WheeledUp) || args.HasFlag(Drivers.MouseFlags.WheeledDown);
				if (isWheel)
				{
					// Forward to focused child first (e.g. ListControl with internal scroll).
					// This lets the child handle its own item scrolling before SPC attempts
					// to scroll the panel viewport.
					if (_focusedChild is IMouseAwareControl childMouse && childMouse.WantsMouseEvents)
					{
						if (childMouse.ProcessMouseEvent(args))
							return true;
					}

					// Fall back to SPC viewport scroll
					if (args.HasFlag(Drivers.MouseFlags.WheeledUp))
					{
						if (_verticalScrollOffset > 0)
						{
							ScrollVerticalBy(-3);
							args.Handled = true;
							return true;
						}
						return false;
					}
					else if (args.HasFlag(Drivers.MouseFlags.WheeledDown))
					{
						int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);
						if (_verticalScrollOffset < maxScroll)
						{
							ScrollVerticalBy(3);
							args.Handled = true;
							return true;
						}
						return false;
					}
				}
			}

			// Handle scrollbar drag-in-progress
			// Button1Dragged = real mouse movement; Button1Pressed = synthetic continuous-press repeats
			if (_isScrollbarDragging && args.HasAnyFlag(Drivers.MouseFlags.Button1Dragged, Drivers.MouseFlags.Button1Pressed))
			{
				var (_, sbTop, sbHeight, _, sbThumbHeight) = GetScrollbarGeometry();
				int deltaY = args.Position.Y - _scrollbarDragStartY;
				int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);
				int trackRange = Math.Max(1, sbHeight - sbThumbHeight);
				int newOffset = _scrollbarDragStartOffset + (int)(deltaY * (double)maxScroll / trackRange);
				ScrollVerticalTo(newOffset);
				args.Handled = true;
				return true;
			}

			// Handle scrollbar drag end
			if (args.HasFlag(Drivers.MouseFlags.Button1Released) && _isScrollbarDragging)
			{
				_isScrollbarDragging = false;
				args.Handled = true;
				return true;
			}

			// Handle scrollbar click/press interactions
			{
				bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
				if (needsScrollbar)
				{
					var (sbRelX, sbTop, sbHeight, sbThumbY, sbThumbHeight) = GetScrollbarGeometry();
					int viewportX = args.Position.X - _margin.Left;
					int contentWidth = _viewportWidth - 2;
					bool isOnScrollbar = viewportX >= contentWidth;

					if (isOnScrollbar && args.HasFlag(Drivers.MouseFlags.Button1Pressed))
					{
						int relY = args.Position.Y - sbTop;
						int maxScroll = Math.Max(0, _contentHeight - _viewportHeight);

						if (relY == 0 && _verticalScrollOffset > 0)
						{
							// Arrow up
							ScrollVerticalBy(-3);
						}
						else if (relY == sbHeight - 1 && _verticalScrollOffset < maxScroll)
						{
							// Arrow down
							ScrollVerticalBy(3);
						}
						else if (relY >= sbThumbY && relY < sbThumbY + sbThumbHeight)
						{
							// Thumb: start drag
							_isScrollbarDragging = true;
							_scrollbarDragStartY = args.Position.Y;
							_scrollbarDragStartOffset = _verticalScrollOffset;
						}
						else if (relY < sbThumbY)
						{
							// Track above thumb: page up
							ScrollVerticalBy(-_viewportHeight);
						}
						else
						{
							// Track below thumb: page down
							ScrollVerticalBy(_viewportHeight);
						}
						args.Handled = true;
						return true;
					}

					if (isOnScrollbar && args.HasAnyFlag(Drivers.MouseFlags.Button1Clicked,
						Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1DoubleClicked,
						Drivers.MouseFlags.Button1TripleClicked))
					{
						// Consume scrollbar click events to prevent propagation to children
						args.Handled = true;
						return true;
					}
				}
			}

			// Handle click events for child focus
			if (args.HasAnyFlag(Drivers.MouseFlags.Button1Clicked, Drivers.MouseFlags.Button1Pressed,
				Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1DoubleClicked, Drivers.MouseFlags.Button1TripleClicked,
				Drivers.MouseFlags.Button2Clicked, Drivers.MouseFlags.Button2Pressed, Drivers.MouseFlags.Button2Released,
				Drivers.MouseFlags.Button2DoubleClicked, Drivers.MouseFlags.Button2TripleClicked,
				Drivers.MouseFlags.Button3Clicked, Drivers.MouseFlags.Button3Pressed, Drivers.MouseFlags.Button3Released,
				Drivers.MouseFlags.Button3DoubleClicked, Drivers.MouseFlags.Button3TripleClicked))
			{
				log?.LogTrace($"ScrollPanel.ProcessMouseEvent: click pos={args.Position} _hasFocus={_hasFocus} _focusedChild={_focusedChild?.GetType().Name ?? "null"}", "Focus");
				// Calculate content width (accounting for scrollbar)
				int contentWidth = _viewportWidth;
				bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
				if (needsScrollbar)
					contentWidth -= 2;

				// Translate mouse position to viewport coordinates
				int viewportX = args.Position.X - _margin.Left;

				// Check if click is within content area (not in scrollbar or margins)
				if (viewportX >= 0 && viewportX < contentWidth && args.Position.Y >= _margin.Top)
				{
					// Use click target caching to ensure double-clicks go to the same child
					// even if scroll position changes between clicks
					var child = GetClickTargetChild(args, contentWidth);
					log?.LogTrace($"ScrollPanel.ProcessMouseEvent: GetClickTargetChild={child?.GetType().Name ?? "null (empty space)"}", "Focus");

					if (child != null)
					{
						// Set focus on clicked child (if focusable)
						if (child is IFocusableControl focusable && focusable.CanReceiveFocus)
						{
							// Clear focus from other children
							foreach (var otherChild in _children)
							{
								if (otherChild != child && otherChild is IFocusableControl fc && fc.HasFocus)
								{
									fc.SetFocus(false, FocusReason.Mouse);
								}
							}

							// Set focus on clicked child
							focusable.SetFocus(true, FocusReason.Mouse);
							if (child is IInteractiveControl interactive)
								_focusedChild = interactive;
							// Mark panel as focused so ProcessKey routes keys to _focusedChild.
							// Tab focus calls SetFocus(true) which sets _hasFocus; mouse click
							// skips that path, leaving _hasFocus=false without this line.
							_hasFocus = true;
							_lastInternalFocusedChild = null;
							log?.LogTrace($"ScrollPanel.ProcessMouseEvent: focused child {child.GetType().Name}, _focusedChild={_focusedChild?.GetType().Name}", "Focus");
						}

						// Forward mouse event to child (if it handles mouse events)
						if (child is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
						{
							// Calculate child-relative Y position
							// We need to recalculate the child's position in the current scroll state
							int contentY = args.Position.Y - _margin.Top + _verticalScrollOffset;
							int currentY = 0;
							foreach (var c in _children.Where(c => c.Visible))
							{
								if (c == child)
								{
									// Translate coordinates to child-relative
									var childPosition = new System.Drawing.Point(viewportX, contentY - currentY);
									var childArgs = args.WithPosition(childPosition);

									// Forward event to child
									if (mouseAware.ProcessMouseEvent(childArgs))
									{
										args.Handled = true;
										return true;
									}
									break;
								}
								currentY += MeasureChildHeight(c, contentWidth);
							}
						}
					}
					else
					{
						// Clicked on empty space or non-focusable content:
						// Unfocus all children, clear _focusedChild so arrow keys scroll
						log?.LogTrace("ScrollPanel.ProcessMouseEvent: click on empty space → unfocusing children", "Focus");
						foreach (var otherChild in _children)
						{
							if (otherChild is IFocusableControl fc && fc.HasFocus)
							{
								fc.SetFocus(false, FocusReason.Mouse);
							}
						}
						_focusedChild = null;
						_lastInternalFocusedChild = null;
						Container?.Invalidate(true);
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Measures the height of a child control using the layout pipeline.
		/// </summary>
		private int MeasureChildHeight(IWindowControl child, int availableWidth)
		{
			var childNode = LayoutNodeFactory.CreateSubtree(child);
			childNode.IsVisible = true;
			int maxH = _viewportHeight > 0 ? _viewportHeight : int.MaxValue;
			var constraints = new LayoutConstraints(1, availableWidth, 1, maxH);
			childNode.Measure(constraints);
			return childNode.DesiredSize.Height;
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set { _backgroundColorValue = value; Invalidate(true); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => _foregroundColor;
			set { _foregroundColor = value; Invalidate(true); }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		public bool IsDirty
		{
			get => _isDirty;
			set => _isDirty = value;
		}

		/// <inheritdoc/>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			_isDirty = true;
			Container?.Invalidate(redrawAll, this);
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			return _viewportHeight > 0 ? _viewportHeight : null;
		}

		#endregion

		#region Child Control Management

		/// <summary>
		/// Adds a child control to the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// For multi-threaded scenarios, queue additions and process them during paint.
		/// </summary>
		public void AddControl(IWindowControl control)
		{
			_children.Add(control);
			control.Container = this;
			// If the panel has focus but no focused child yet, focus the new control
			// if it's focusable. Restores focus routing after ClearContents.
			if (_hasFocus && _focusedChild == null &&
				control.Visible &&
				control is IFocusableControl fc && fc.CanReceiveFocus)
			{
				_focusedChild = control as IInteractiveControl;
				if (control is IDirectionalFocusControl dfc)
					dfc.SetFocusWithDirection(true, false);
				else
					fc.SetFocus(true, FocusReason.Programmatic);
			}
			Invalidate(true);
		}

		/// <summary>
		/// Removes a child control from the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// </summary>
		public void RemoveControl(IWindowControl control)
		{
			// If removing the focused child, clear focus
			if (_focusedChild == control)
			{
				if (_focusedChild is IFocusableControl fc)
					fc.SetFocus(false, FocusReason.Programmatic);
				_focusedChild = null;
			}

			// Clear remembered child if it's being removed
			if (_lastInternalFocusedChild == control as IInteractiveControl)
				_lastInternalFocusedChild = null;

			if (_children.Remove(control))
			{
				control.Container = null;

				// If we're no longer focusable (lost all children and no scrolling), lose focus
				if (_hasFocus && !CanReceiveFocus)
				{
					SetFocus(false, FocusReason.Programmatic);
				}

				Invalidate(true);
			}
		}

		/// <summary>
		/// Removes all child controls from the panel.
		/// </summary>
		public void ClearContents()
		{
			if (_focusedChild is IFocusableControl fc)
				fc.SetFocus(false, FocusReason.Programmatic);
			_focusedChild = null;
			_lastInternalFocusedChild = null;

			foreach (var child in _children)
			{
				child.Container = null;
				child.Dispose();
			}
			_children.Clear();

			Invalidate(true);
		}

		/// <summary>
		/// Gets the collection of child controls.
		/// </summary>
		public IReadOnlyList<IWindowControl> Children => _children.AsReadOnly();

		#endregion

		#region IContainerControl Implementation

		/// <summary>
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			return _children.AsReadOnly();
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				if (_focusedChild != null && _focusedChild != child && _focusedChild is IFocusableControl oldFc)
					oldFc.HasFocus = false;

				_focusedChild = child;
				_lastInternalFocusedChild = child;

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}
			else if (_focusedChild == child)
			{
				_focusedChild = null;
			}

			Container?.Invalidate(true);
		}

		#endregion

		#region IScrollableContainer Implementation

		/// <summary>
		/// Automatically scrolls to bring a child control into view when it receives focus.
		/// This is called by the focus system when a child within this panel gains focus.
		/// </summary>
		public void ScrollChildIntoView(IWindowControl child)
		{
			if (!_children.Contains(child))
				return; // Not our child

			// Calculate child's position within our content
			int childContentY = 0;
			int contentWidth = _viewportWidth;
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight)
				contentWidth -= 2;

			// Find child's Y position by measuring all children before it
			foreach (var c in _children.Where(c => c.Visible))
			{
				if (c == child)
					break;

				childContentY += MeasureChildHeight(c, contentWidth);
			}

			int childHeight = MeasureChildHeight(child, contentWidth);

			// Scroll vertically if child is outside viewport
			if (childContentY < _verticalScrollOffset)
			{
				// Child is above viewport - scroll up to show it at top
				ScrollVerticalTo(childContentY);
			}
			else if (childContentY + childHeight > _verticalScrollOffset + _viewportHeight)
			{
				// Child is below viewport - scroll down to show it at bottom
				ScrollVerticalTo(childContentY + childHeight - _viewportHeight);
			}
			// If child is already visible, don't scroll

			// Note: Horizontal scrolling not implemented for children (children typically fit width)
		}

		#endregion

		#region Scrolling Methods

		/// <summary>
		/// Scrolls the content vertically by the specified number of lines.
		/// Positive values scroll down, negative values scroll up.
		/// The offset is clamped to valid bounds automatically.
		/// </summary>
		/// <param name="lines">Number of lines to scroll (positive = down, negative = up).</param>
		public void ScrollVerticalBy(int lines)
		{
			int oldOffset = _verticalScrollOffset;
			int maxOffset = Math.Max(0, _contentHeight - _viewportHeight);
			_verticalScrollOffset = Math.Clamp(_verticalScrollOffset + lines, 0, maxOffset);

			// AutoScroll state tracking
			if (_autoScroll && lines < 0 && _verticalScrollOffset < maxOffset)
			{
				_autoScroll = false;  // Detach: user scrolled up
			}
			else if (!_autoScroll && lines > 0 && _verticalScrollOffset >= maxOffset)
			{
				_autoScroll = true;   // Re-attach: user scrolled to bottom
			}

			if (oldOffset != _verticalScrollOffset)
			{
				Invalidate(true);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Vertical, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		private void ScrollVerticalTo(int offset)
		{
			int oldOffset = _verticalScrollOffset;
			_verticalScrollOffset = Math.Clamp(offset, 0, Math.Max(0, _contentHeight - _viewportHeight));

			if (oldOffset != _verticalScrollOffset)
			{
				Invalidate(true);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Vertical, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>
		/// Scrolls the content horizontally by the specified number of characters.
		/// Positive values scroll right, negative values scroll left.
		/// The offset is clamped to valid bounds automatically.
		/// </summary>
		/// <param name="chars">Number of characters to scroll (positive = right, negative = left).</param>
		public void ScrollHorizontalBy(int chars)
		{
			int oldOffset = _horizontalScrollOffset;
			_horizontalScrollOffset = Math.Clamp(_horizontalScrollOffset + chars, 0, Math.Max(0, _contentWidth - _viewportWidth));

			if (oldOffset != _horizontalScrollOffset)
			{
				Invalidate(true);
				Scrolled?.Invoke(this, new ScrollEventArgs(ScrollDirection.Horizontal, _verticalScrollOffset, _horizontalScrollOffset));
			}
		}

		/// <summary>
		/// Scrolls to the top of the content.
		/// </summary>
		public void ScrollToTop() => ScrollVerticalTo(0);

		/// <summary>
		/// Scrolls to the bottom of the content.
		/// </summary>
		public void ScrollToBottom() => ScrollVerticalTo(Math.Max(0, _contentHeight - _viewportHeight));

		/// <summary>
		/// Scrolls to a specific position.
		/// </summary>
		public void ScrollToPosition(int vertical, int horizontal = 0)
		{
			ScrollVerticalTo(vertical);
			if (_horizontalScrollMode == ScrollMode.Scroll)
			{
				_horizontalScrollOffset = Math.Clamp(horizontal, 0, Math.Max(0, _contentWidth - _viewportWidth));
				Invalidate(true);
			}
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Calculate available width from constraints, not from stale _viewportWidth
			int width = _width ?? constraints.MaxWidth;
			int availableWidth = Math.Max(1, width - _margin.Left - _margin.Right);

			// Determine height
			int height;
			if (_height.HasValue)
			{
				// Explicit height set - use it directly
				height = _height.Value;
			}
			else
			{
				// No explicit height - calculate from content
				int contentHeight = CalculateContentHeight(availableWidth);
				height = contentHeight + _margin.Top + _margin.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(width + _margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			var bgColor = BackgroundColor;
			var fgColor = _foregroundColor;

			_viewportHeight = bounds.Height - _margin.Top - _margin.Bottom;
			_viewportWidth = bounds.Width - _margin.Left - _margin.Right;

			// Calculate content dimensions from children
			_contentHeight = CalculateContentHeight(_viewportWidth, _viewportHeight);
			_contentWidth = CalculateContentWidth();

			// AutoScroll: scroll to bottom on any repaint when enabled
			if (_autoScroll)
			{
				int maxOffset = Math.Max(0, _contentHeight - _viewportHeight);
				if (_verticalScrollOffset < maxOffset)
				{
					_verticalScrollOffset = maxOffset;
				}
			}

			// Reserve space for scrollbar(s)
			int contentWidth = _viewportWidth;
			bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
			if (needsScrollbar)
			{
				contentWidth -= 2;  // Reserve 2 columns: 1 for gap, 1 for scrollbar
			}


			// Render children with scroll offsets applied
			int currentY = -_verticalScrollOffset;

			// Get renderer for registering child bounds (needed for cursor position lookups)
			var parentWindow = this.GetParentWindow();
			var renderer = parentWindow?.Renderer;

			foreach (var child in _children.ToList())
			{
				if (!child.Visible) continue;

				// Build layout subtree (handles containers like TabControl, HorizontalGrid)
				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;

				// Measure using full layout pipeline.
				// Fill-aligned children: cap to viewport so they fill the visible area.
				// Content-sized children: measure unbounded for correct scroll positioning.
				int maxChildHeight = (_viewportHeight > 0 && child.VerticalAlignment == VerticalAlignment.Fill)
					? _viewportHeight : int.MaxValue;
				var constraints = new LayoutConstraints(1, contentWidth, 1, maxChildHeight);
				childNode.Measure(constraints);
				int childHeight = childNode.DesiredSize.Height;

				// Register child bounds for cursor position lookups (even if off-viewport)
				var childBoundsForCursor = new LayoutRect(
					bounds.X + _margin.Left,
					bounds.Y + _margin.Top + currentY,
					contentWidth,
					childHeight);
				renderer?.UpdateChildBounds(child, childBoundsForCursor);

				// Only render if in viewport
				if (currentY + childHeight > 0 && currentY < _viewportHeight)
				{
					var childBounds = new LayoutRect(
						bounds.X + _margin.Left,
						bounds.Y + _margin.Top + currentY,
						contentWidth,
						childHeight);

					// Arrange in screen coordinates (so AbsoluteBounds are correct)
					childNode.Arrange(childBounds);

					// Create clipped clipRect for child that excludes scrollbar area and clips to viewport
					var viewportRect = new LayoutRect(
						bounds.X + _margin.Left,
						bounds.Y + _margin.Top,
						needsScrollbar ? contentWidth + 1 : contentWidth, // +1 for gap if scrollbar visible
						_viewportHeight);

					var childClipRect = clipRect.Intersect(viewportRect);

					if (needsScrollbar)
					{
						// Further restrict to exclude scrollbar columns
						int maxRight = bounds.X + _margin.Left + contentWidth + 1; // +1 for gap
						childClipRect = childClipRect.Intersect(new LayoutRect(
							childClipRect.X,
							childClipRect.Y,
							Math.Min(childClipRect.Width, maxRight - childClipRect.X),
							childClipRect.Height));
					}

					// Paint through layout pipeline (headers + children properly)
					childNode.Paint(buffer, childClipRect, fgColor, bgColor);
				}

				currentY += childHeight;
			}

			// Draw vertical scrollbar if content exceeds viewport
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight)
			{
				DrawVerticalScrollbar(buffer, bounds, fgColor, bgColor);
			}

			_isDirty = false;
		}

		private (int scrollbarRelX, int scrollbarTop, int scrollbarHeight, int thumbY, int thumbHeight) GetScrollbarGeometry()
		{
			// scrollbarRelX is control-relative (offset from bounds.X)
			// For Right position: last column of the control = margin.Left + viewport + margin.Right - 1
			// This matches the old DrawVerticalScrollbar which used bounds.Right - 1 = bounds.X + bounds.Width - 1
			int scrollbarRelX = _scrollbarPosition == ScrollbarPosition.Right
				? _margin.Left + _viewportWidth + _margin.Right - 1
				: _margin.Left;
			int scrollbarTop = _margin.Top;
			int scrollbarHeight = _viewportHeight;

			double viewportRatio = (double)_viewportHeight / _contentHeight;
			int thumbHeight = Math.Max(1, (int)(scrollbarHeight * viewportRatio));
			double scrollRatio = _contentHeight > _viewportHeight
				? (double)_verticalScrollOffset / (_contentHeight - _viewportHeight)
				: 0;
			int thumbY = (int)((scrollbarHeight - thumbHeight) * scrollRatio);

			return (scrollbarRelX, scrollbarTop, scrollbarHeight, thumbY, thumbHeight);
		}

		private void DrawVerticalScrollbar(CharacterBuffer buffer, LayoutRect bounds, Color fgColor, Color bgColor)
		{
			var (scrollbarRelX, scrollbarTop, scrollbarHeight, thumbY, thumbHeight) = GetScrollbarGeometry();

			// Convert control-relative coordinates to buffer-absolute coordinates
			int scrollbarX = bounds.X + scrollbarRelX;
			int scrollbarAbsTop = bounds.Y + scrollbarTop;

			// Colors
			Color thumbColor = _hasFocus ? Color.Cyan1 : Color.Grey;
			Color trackColor = _hasFocus ? Color.Grey : Color.Grey23;

			for (int y = 0; y < scrollbarHeight; y++)
			{
				Color color;
				char ch;

				if (y >= thumbY && y < thumbY + thumbHeight)
				{
					color = thumbColor;
					ch = '█';
				}
				else
				{
					color = trackColor;
					ch = '│';
				}

				buffer.SetCell(scrollbarX, scrollbarAbsTop + y, ch, color, bgColor);
			}

			// Draw scroll indicators at top/bottom
			if (_verticalScrollOffset > 0)
			{
				buffer.SetCell(scrollbarX, scrollbarAbsTop, '▲', thumbColor, bgColor);
			}
			if (_verticalScrollOffset < _contentHeight - _viewportHeight)
			{
				buffer.SetCell(scrollbarX, scrollbarAbsTop + scrollbarHeight - 1, '▼', thumbColor, bgColor);
			}
		}

		private int CalculateContentHeight(int viewportWidth, int maxHeight = 0)
		{
			int availableWidth = viewportWidth;
			int maxH = maxHeight > 0 ? maxHeight : int.MaxValue;

			// Reserve space for scrollbar if we might need it
			// This is an approximation - we'll recalculate if needed
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll)
			{
				availableWidth = Math.Max(1, viewportWidth - 1);
			}

			int totalHeight = 0;
			foreach (var child in _children.Where(c => c.Visible).ToList())
			{
				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;
				// Fill-aligned children: measure with viewport constraint so they fill the visible area.
				// Content-sized children: measure unbounded to get natural height for scroll range.
				int childMaxH = (child.VerticalAlignment == VerticalAlignment.Fill && maxH < int.MaxValue)
					? maxH : int.MaxValue;
				var constraints = new LayoutConstraints(1, availableWidth, 1, childMaxH);
				childNode.Measure(constraints);
				totalHeight += childNode.DesiredSize.Height;
			}

			return totalHeight;
		}

		private int CalculateContentWidth()
		{
			var visibleChildren = _children.Where(c => c.Visible).ToList();
			return visibleChildren.Any() ? visibleChildren.Max(c => c.GetLogicalContentSize().Width) : 0;
		}

		/// <summary>
		/// Checks if scrolling is needed based on current or safe default dimensions.
		/// </summary>
		private bool NeedsScrolling()
		{
			// Use safe dimension checking with defaults
			int contentH = _contentHeight > 0 ? _contentHeight : CalculateContentHeightSafe();
			int viewportH = _viewportHeight > 0 ? _viewportHeight : 1;
			int contentW = _contentWidth > 0 ? _contentWidth : CalculateContentWidthSafe();
			int viewportW = _viewportWidth > 0 ? _viewportWidth : 1;

			bool needsVertical = _verticalScrollMode == ScrollMode.Scroll && contentH > viewportH;
			bool needsHorizontal = _horizontalScrollMode == ScrollMode.Scroll && contentW > viewportW;

			return needsVertical || needsHorizontal;
		}

		/// <summary>
		/// Checks if any child control can receive focus.
		/// </summary>
		private bool HasFocusableChildren()
		{
			return _children.Any(c => c.Visible && c is IFocusableControl fc && fc.CanReceiveFocus);
		}

		/// <summary>
		/// Safe version of CalculateContentHeight that doesn't rely on _viewportWidth being set.
		/// Uses a default width or last known good value.
		/// </summary>
		private int CalculateContentHeightSafe()
		{
			return CalculateContentHeight(_viewportWidth > 0 ? _viewportWidth : 100);
		}

		/// <summary>
		/// Safe version of CalculateContentWidth.
		/// </summary>
		private int CalculateContentWidthSafe()
		{
			return CalculateContentWidth();
		}

		#endregion
	}

	#region Supporting Types

	/// <summary>
	/// Scroll mode enumeration.
	/// </summary>
	public enum ScrollMode
	{
		/// <summary>No scrolling.</summary>
		None,
		/// <summary>Scrolling enabled.</summary>
		Scroll,
		/// <summary>Text wrapping (for horizontal overflow).</summary>
		Wrap
	}

	/// <summary>
	/// Scrollbar position enumeration.
	/// </summary>
	public enum ScrollbarPosition
	{
		/// <summary>Scrollbar on the left side.</summary>
		Left,
		/// <summary>Scrollbar on the right side.</summary>
		Right
	}

	/// <summary>
	/// Scroll event arguments.
	/// </summary>
	public class ScrollEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the scroll direction.
		/// </summary>
		public ScrollDirection Direction { get; }

		/// <summary>
		/// Gets the vertical scroll offset.
		/// </summary>
		public int VerticalOffset { get; }

		/// <summary>
		/// Gets the horizontal scroll offset.
		/// </summary>
		public int HorizontalOffset { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollEventArgs"/> class.
		/// </summary>
		public ScrollEventArgs(ScrollDirection direction, int verticalOffset, int horizontalOffset)
		{
			Direction = direction;
			VerticalOffset = verticalOffset;
			HorizontalOffset = horizontalOffset;
		}
	}

	/// <summary>
	/// Scroll direction enumeration.
	/// </summary>
	public enum ScrollDirection
	{
		/// <summary>Vertical scrolling.</summary>
		Vertical,
		/// <summary>Horizontal scrolling.</summary>
		Horizontal
	}

	#endregion
}
