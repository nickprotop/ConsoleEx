// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A scrollable panel control that can host child controls with automatic scrolling support.
	/// Supports vertical and horizontal scrolling, mouse wheel, and visual scrollbars.
	/// </summary>
	public class ScrollablePanelControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer, IDOMPaintable, IDirectionalFocusControl
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
		private bool _focusFromBackward = false;

		// Configurable options
		private bool _showScrollbar = true;
		private ScrollbarPosition _scrollbarPosition = ScrollbarPosition.Right;
		private ScrollMode _horizontalScrollMode = ScrollMode.None;
		private ScrollMode _verticalScrollMode = ScrollMode.Scroll;
		private bool _enableMouseWheel = true;

		// IWindowControl properties
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// IContainer properties
		private Color _backgroundColor = Color.Black;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

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

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ActualHeight => null;  // Fill available space

		/// <inheritdoc/>
		public int? ActualWidth => _width;

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
			if (!_hasFocus || !_isEnabled) return false;

			// FIRST: Delegate to focused child if we have one
			if (_focusedChild != null && _focusedChild.ProcessKey(key))
			{
				return true; // Child handled it
			}

			// SECOND: Handle Tab navigation through children
			if (key.Key == ConsoleKey.Tab)
			{
				var focusableChildren = _children
					.Where(c => c is IFocusableControl fc && fc.CanReceiveFocus)
					.Cast<IInteractiveControl>()
					.ToList();

				if (focusableChildren.Count > 1)
				{
					int currentIndex = _focusedChild != null ? focusableChildren.IndexOf(_focusedChild) : -1;
					bool shiftPressed = (key.Modifiers & ConsoleModifiers.Shift) != 0;

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
		public bool CanReceiveFocus
		{
			get
			{
				if (!_visible) return false;

				// Can receive focus if we need scrolling OR have focusable children
				bool needsScrolling = NeedsScrolling();
				bool hasFocusableChildren = HasFocusableChildren();

				return needsScrolling || hasFocusableChildren;
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
			if (_hasFocus == focus) return;

			var hadFocus = _hasFocus;
			_hasFocus = focus;

			if (focus)
			{
				// Getting focus - find first/last focusable child if we have any
				var focusableChildren = _children
					.Where(c => c is IFocusableControl fc && fc.CanReceiveFocus)
					.ToList();

				if (focusableChildren.Any())
				{
					// Focus first or last child based on direction
					_focusedChild = _focusFromBackward
						? focusableChildren.Last() as IInteractiveControl
						: focusableChildren.First() as IInteractiveControl;

					if (_focusedChild is IFocusableControl fc)
					{
						if (_focusedChild is IDirectionalFocusControl dfc)
							dfc.SetFocusWithDirection(true, _focusFromBackward);
						else
							fc.SetFocus(true, reason);
					}
				}
				// If no focusable children, panel itself is focused (for scrolling)

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				// Losing focus - unfocus any focused child
				if (_focusedChild != null && _focusedChild is IFocusableControl fc)
				{
					fc.SetFocus(false, reason);
				}
				_focusedChild = null;

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
		public bool WantsMouseEvents => _enableMouseWheel;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => CanReceiveFocus;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_enableMouseWheel) return false;

			// Mouse wheel scrolling
			if (args.HasFlag(Drivers.MouseFlags.WheeledUp))
			{
				ScrollVerticalBy(-3);
				return true;
			}
			else if (args.HasFlag(Drivers.MouseFlags.WheeledDown))
			{
				ScrollVerticalBy(3);
				return true;
			}

			return false;
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => _backgroundColor;
			set { _backgroundColor = value; Invalidate(true); }
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
		/// </summary>
		public void AddControl(IWindowControl control)
		{
			_children.Add(control);
			control.Container = this;
			Invalidate(true);
		}

		/// <summary>
		/// Removes a child control from the panel.
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
		/// Gets the collection of child controls.
		/// </summary>
		public IReadOnlyList<IWindowControl> Children => _children.AsReadOnly();

		#endregion

		#region Scrolling Methods

		private void ScrollVerticalBy(int lines)
		{
			int oldOffset = _verticalScrollOffset;
			int maxOffset = Math.Max(0, _contentHeight - _viewportHeight);
			_verticalScrollOffset = Math.Clamp(_verticalScrollOffset + lines, 0, maxOffset);

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

		private void ScrollHorizontalBy(int chars)
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

			// Pass the actual available width to CalculateContentHeight
			int height = CalculateContentHeight(availableWidth);

			return new LayoutSize(
				Math.Clamp(width + _margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height + _margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var bgColor = _backgroundColor;
			var fgColor = _foregroundColor;

			_viewportHeight = bounds.Height - _margin.Top - _margin.Bottom;
			_viewportWidth = bounds.Width - _margin.Left - _margin.Right;

			// Calculate content dimensions from children
			_contentHeight = CalculateContentHeight(_viewportWidth);
			_contentWidth = CalculateContentWidth();

			// Reserve space for scrollbar(s)
			int contentWidth = _viewportWidth;
			bool needsScrollbar = _showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight;
			if (needsScrollbar)
			{
				contentWidth -= 2;  // Reserve 2 columns: 1 for gap, 1 for scrollbar
			}

			// Render children with scroll offsets applied
			int currentY = -_verticalScrollOffset;

			foreach (var child in _children.ToList())
			{
				if (!child.Visible) continue;

				// Calculate child height using MeasureDOM if available (to account for wrapping)
				int childHeight;
				if (child is IDOMPaintable measurable)
				{
					var constraints = new LayoutConstraints(
						MinWidth: 1,
						MaxWidth: contentWidth,
						MinHeight: 1,
						MaxHeight: int.MaxValue);
					childHeight = measurable.MeasureDOM(constraints).Height;
				}
				else
				{
					childHeight = child.GetLogicalContentSize().Height;
				}

				// Only render if in viewport
				if (currentY + childHeight > 0 && currentY < _viewportHeight)
				{
					var childBounds = new LayoutRect(
						bounds.X + _margin.Left,
						bounds.Y + _margin.Top + currentY,
						contentWidth,
						childHeight);

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

					if (child is IDOMPaintable paintable)
					{
						paintable.PaintDOM(buffer, childBounds, childClipRect, fgColor, bgColor);
					}
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

		private void DrawVerticalScrollbar(CharacterBuffer buffer, LayoutRect bounds, Color fgColor, Color bgColor)
		{
			// Calculate contentWidth (must match PaintDOM calculation!)
			int contentWidth = _viewportWidth;
			if (_contentHeight > _viewportHeight)
			{
				// Reserve 2 columns: 1 for gap, 1 for scrollbar
				contentWidth = _viewportWidth - 2;
			}

			// Determine scrollbar X position - at the last column of the panel
			// This should be bounds.Right - 1 (last valid column within panel bounds)
			int scrollbarX = _scrollbarPosition == ScrollbarPosition.Right
				? bounds.Right - 1  // Draw at last column of panel
				: bounds.X + _margin.Left;

			int scrollbarTop = bounds.Y + _margin.Top;
			int scrollbarHeight = bounds.Height - _margin.Top - _margin.Bottom;

			// Calculate scrollbar thumb position and size
			double viewportRatio = (double)_viewportHeight / _contentHeight;
			int thumbHeight = Math.Max(1, (int)(scrollbarHeight * viewportRatio));

			double scrollRatio = _contentHeight > _viewportHeight
				? (double)_verticalScrollOffset / (_contentHeight - _viewportHeight)
				: 0;
			int thumbY = (int)((scrollbarHeight - thumbHeight) * scrollRatio);

			// Draw scrollbar track and thumb
			Color thumbColor = _hasFocus ? Color.Cyan1 : Color.Grey;
			Color trackColor = _hasFocus ? Color.Grey : Color.Grey23;

			for (int y = 0; y < scrollbarHeight; y++)
			{
				Color color;
				char ch;

				if (y >= thumbY && y < thumbY + thumbHeight)
				{
					// Thumb
					color = thumbColor;
					ch = '█';
				}
				else
				{
					// Track
					color = trackColor;
					ch = '│';
				}

				buffer.SetCell(scrollbarX, scrollbarTop + y, ch, color, bgColor);
			}

			// Draw scroll indicators at top/bottom
			if (_verticalScrollOffset > 0)
			{
				buffer.SetCell(scrollbarX, scrollbarTop, '▲', thumbColor, bgColor);
			}
			if (_verticalScrollOffset < _contentHeight - _viewportHeight)
			{
				buffer.SetCell(scrollbarX, scrollbarTop + scrollbarHeight - 1, '▼', thumbColor, bgColor);
			}
		}

		private int CalculateContentHeight(int viewportWidth)
		{
			int availableWidth = viewportWidth;

			// Reserve space for scrollbar if we might need it
			// This is an approximation - we'll recalculate if needed
			if (_showScrollbar && _verticalScrollMode == ScrollMode.Scroll)
			{
				availableWidth = Math.Max(1, viewportWidth - 1);
			}

			int totalHeight = 0;
			foreach (var child in _children.Where(c => c.Visible).ToList())
			{
				// For DOM-paintable controls, use MeasureDOM to get actual rendered height (including wrapping)
				if (child is IDOMPaintable measurable)
				{
					var constraints = new LayoutConstraints(
						MinWidth: 1,
						MaxWidth: availableWidth,
						MinHeight: 1,
						MaxHeight: int.MaxValue);
					var size = measurable.MeasureDOM(constraints);
					totalHeight += size.Height;
				}
				else
				{
					// Fallback to logical size for non-DOM controls
					var logicalSize = child.GetLogicalContentSize();
					totalHeight += logicalSize.Height;
				}
			}

			return totalHeight;
		}

		private int CalculateContentWidth()
		{
			var visibleChildren = _children.Where(c => c.Visible).ToList();
			return visibleChildren.Any() ? visibleChildren.Max(c => c.ActualWidth ?? 0) : 0;
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
			return _children.Any(c => c is IFocusableControl fc && fc.CanReceiveFocus);
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
