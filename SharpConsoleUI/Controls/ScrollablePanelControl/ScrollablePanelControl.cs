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
	public partial class ScrollablePanelControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer, IDirectionalFocusControl, IContainerControl, IScrollableContainer, IFocusTrackingContainer
	{
		private readonly List<IWindowControl> _children = new();
		private readonly object _childrenLock = new();
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

		private int? _height;

		// IContainer properties
		private Color? _backgroundColorValue;
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
		/// Event fired when the control is double-clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Event fired when the control is right-clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

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

		#region Properties from IWindowControl (overrides and additional)

		/// <inheritdoc/>
		public int? ContentHeight => _height;

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <inheritdoc/>
		public override bool Visible
		{
			get => base.Visible;
			set
			{
				if (base.Visible != value)
				{
					base.Visible = value;

					// If becoming invisible and we have focus, lose it
					if (!Visible && _hasFocus)
					{
						SetFocus(false, FocusReason.Programmatic);
					}
				}
			}
		}

		/// <inheritdoc/>
		public int? Height
		{
			get => _height;
			set => PropertySetterHelper.SetDimensionProperty(ref _height, value, Container);
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }
			int height = snapshot.Where(c => c.Visible).Sum(c => c.GetLogicalContentSize().Height);
			int width = Width ?? 80;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				snapshot = new List<IWindowControl>(_children);
				_children.Clear();
			}
			foreach (var child in snapshot)
			{
				child.Dispose();
			}
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

		#region IContainerControl Implementation

		/// <summary>
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			lock (_childrenLock) { return new List<IWindowControl>(_children); }
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
