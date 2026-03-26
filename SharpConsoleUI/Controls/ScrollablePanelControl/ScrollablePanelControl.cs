// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A scrollable panel control that can host child controls with automatic scrolling support.
	/// Supports vertical and horizontal scrolling, mouse wheel, and visual scrollbars.
	/// </summary>
	public partial class ScrollablePanelControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer, IContainerControl, IScrollableContainer, IFocusScope
	{
		private readonly List<IWindowControl> _children = new();
		private readonly object _childrenLock = new();
		private int _verticalScrollOffset = 0;
		private int _horizontalScrollOffset = 0;
		private int _contentHeight = 0;
		private int _contentWidth = 0;
		private int _viewportHeight = 0;
		private int _viewportWidth = 0;
		private bool _isEnabled = true;
		private IInteractiveControl? _lastInternalFocusedChild = null;
		// When true, GetInitialFocus returns 'this' so SetFocus(panel) enters scroll mode directly.
		private bool _enterScrollModeOnNextInitialFocus = false;

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

		// Border and padding
		private BorderStyle _borderStyle = BorderStyle.None;
		private Color? _borderColor;
		private Padding _padding = new Padding(0, 0, 0, 0);
		private string? _header;
		private TextJustification _headerAlignment = TextJustification.Left;

		private int? _height;

		// IContainer properties
		private Color? _backgroundColorValue;
		private Color _foregroundColor = Color.White;
		private bool _isDirty = true;

		// Set when ScrollChildIntoView is called before viewport is ready;
		// PaintDOM defers the scroll until first valid render.
		private bool _pendingScrollToFocused = false;

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
			set => SetProperty(ref _showScrollbar, value);
		}

		/// <summary>
		/// Gets or sets the position of the scrollbar.
		/// </summary>
		public ScrollbarPosition ScrollbarPosition
		{
			get => _scrollbarPosition;
			set => SetProperty(ref _scrollbarPosition, value);
		}

		/// <summary>
		/// Gets or sets the horizontal scroll mode.
		/// </summary>
		public ScrollMode HorizontalScrollMode
		{
			get => _horizontalScrollMode;
			set => SetProperty(ref _horizontalScrollMode, value);
		}

		/// <summary>
		/// Gets or sets the vertical scroll mode.
		/// </summary>
		public ScrollMode VerticalScrollMode
		{
			get => _verticalScrollMode;
			set => SetProperty(ref _verticalScrollMode, value);
		}

		/// <summary>
		/// Gets or sets whether mouse wheel scrolling is enabled.
		/// </summary>
		public bool EnableMouseWheel
		{
			get => _enableMouseWheel;
			set { _enableMouseWheel = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets whether to automatically scroll to bottom when content is added.
		/// When enabled, scrolls to bottom on AddControl if currently at/near bottom.
		/// Disables automatically when user scrolls up, re-enables when user scrolls to bottom.
		/// </summary>
		public bool AutoScroll
		{
			get => _autoScroll;
			set { _autoScroll = value; OnPropertyChanged(); }
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

		#region Border & Padding Properties

		/// <summary>
		/// Gets or sets the border style for the panel.
		/// Default is None (no border).
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>
		/// Gets or sets the border color.
		/// When null, uses the foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColor;
			set => SetProperty(ref _borderColor, value);
		}

		/// <summary>
		/// Gets or sets the padding inside the border.
		/// </summary>
		public Padding Padding
		{
			get => _padding;
			set => SetProperty(ref _padding, value);
		}

		/// <summary>
		/// Gets or sets the header text displayed in the top border.
		/// </summary>
		public string? Header
		{
			get => _header;
			set => SetProperty(ref _header, value);
		}

		/// <summary>
		/// Gets or sets the alignment of the header text.
		/// </summary>
		public TextJustification HeaderAlignment
		{
			get => _headerAlignment;
			set => SetProperty(ref _headerAlignment, value);
		}

		// Computed helpers for border insets
		private int BorderWidth => _borderStyle == BorderStyle.None ? 0 : 2;
		private int BorderHeight => _borderStyle == BorderStyle.None ? 0 : 2;
		private int ContentInsetLeft => (_borderStyle != BorderStyle.None ? 1 : 0) + _padding.Left;
		private int ContentInsetTop => (_borderStyle != BorderStyle.None ? 1 : 0) + _padding.Top;

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
					// (FocusManager will handle this automatically)
				}
			}
		}

		/// <inheritdoc/>
		public override int? Height
		{
			get => _height;
			set => SetProperty(ref _height, value, v => v.HasValue ? Math.Max(0, v.Value) : v);
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
			// For containers, HasFocus means "this container or a descendant is focused"
			// (i.e., is in the focus path). This preserves rendering/keyboard-routing semantics.
			get => ComputeIsInFocusPath();
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <summary>
		/// Gets the currently focused child using FocusManager.
		/// Returns the direct child that is focused or contains the focused control.
		/// Uses FocusPath for ancestry detection to correctly handle transparent containers
		/// like ColumnContainer that skip their HGrid parent in the Container chain.
		/// </summary>
		private IInteractiveControl? GetFocusedChildFromCoordinator()
		{
			var window = (this as IWindowControl).GetParentWindow();
			if (window == null) return null;
			var focused = window.FocusManager.FocusedControl;
			if (focused == null) return null;

			// FocusPath is built by FocusManager using ResolveParentWindowControl which correctly
			// includes HGrid even though ColumnContainer.Container skips it.
			var focusPath = window.FocusManager.FocusPath;

			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			foreach (var child in snapshot)
			{
				if (ReferenceEquals(child, focused))
					return focused as IInteractiveControl;
				// Check if child is an ancestor of focused via the FocusPath
				if (focusPath.Contains(child, ReferenceEqualityComparer.Instance))
					return child as IInteractiveControl;
			}
			return null;
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Color.Transparent;
			set { _backgroundColorValue = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => _foregroundColor;
			set { _foregroundColor = value; OnPropertyChanged(); Invalidate(true); }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		public bool IsDirty
		{
			get => _isDirty;
			set { _isDirty = value; OnPropertyChanged(); }
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

		#region IFocusScope Implementation

		/// <inheritdoc/>
		public IFocusableControl? SavedFocus { get; set; }

		/// <inheritdoc/>
		public IFocusableControl? GetInitialFocus(bool backward)
		{
			// Self-sentinel: return 'this' so FocusManager focuses panel directly (scroll mode)
			if (_enterScrollModeOnNextInitialFocus)
			{
				_enterScrollModeOnNextInitialFocus = false;
				return this;
			}
			// SavedFocus is only used for forward entry (resume from where focus left off).
			// Backward entry always goes to the last child for correct Shift+Tab behavior.
			// However, discard SavedFocus if using it would skip a nested IFocusScope child
			// (e.g. a ToolbarControl) that appears before the saved control in Tab order.
			if (!backward && SavedFocus != null)
			{
				var saved = SavedFocus;
				SavedFocus = null;
				// Self-sentinel: NavigationView sets SavedFocus = this to request scroll mode.
				// Always honour it — WouldSkipNestedScope doesn't apply to self-references.
				if (ReferenceEquals(saved, this) || !WouldSkipNestedScope(saved))
					return saved;
			}
			SavedFocus = null;
			var children = GetFocusableChildren();
			return backward ? children.LastOrDefault() : children.FirstOrDefault();
		}

		/// <inheritdoc/>
		public IFocusableControl? GetNextFocus(IFocusableControl current, bool backward)
		{
			var children = GetFocusableChildren();
			var index = children.FindIndex(c => ReferenceEquals(c, current));
			if (index < 0) return GetInitialFocus(backward);
			var nextIndex = backward ? index - 1 : index + 1;
			return (nextIndex >= 0 && nextIndex < children.Count) ? children[nextIndex] : null;
		}

		private List<IFocusableControl> GetFocusableChildren()
		{
			var result = new List<IFocusableControl>();
			foreach (var child in GetChildren())
			{
				if (!child.Visible) continue;
				CollectFocusableChild(child, result);
			}
			return result;
		}

		/// <summary>
		/// Collects focusable Tab stops from a child control, handling
		/// <see cref="IFocusableContainerWithHeader"/> (header + active-tab children),
		/// <see cref="IFocusScope"/> (opaque single stop), and leaf controls.
		/// </summary>
		private static void CollectFocusableChild(IWindowControl child, List<IFocusableControl> result)
		{
			if (!child.Visible) return;

			// IFocusableContainerWithHeader (e.g. TabControl): header is a Tab stop,
			// then active-tab children are recursively included immediately after.
			if (child is IFocusableContainerWithHeader)
			{
				if (child is IFocusableControl headerFc && headerFc.CanReceiveFocus)
					result.Add(headerFc);
				if (child is IContainerControl headerContainer)
					foreach (var grandchild in headerContainer.GetChildren())
						CollectFocusableChild(grandchild, result);
				return;
			}

			// IFocusScope (e.g. nested HGrid): opaque single stop
			if (child is IFocusScope && child is IFocusableControl scopeFc
				&& child is IContainerControl scopeContainer)
			{
				if (scopeFc.CanReceiveFocus || HasAnyFocusableDescendant(scopeContainer))
				{
					result.Add(scopeFc);
					return;
				}
			}

			// Leaf focusable control
			if (child is IFocusableControl f && f.CanReceiveFocus)
			{
				result.Add(f);
				return;
			}

			// Transparent container: recurse into children
			if (child is IContainerControl container)
				foreach (var grandchild in container.GetChildren())
					CollectFocusableChild(grandchild, result);
		}

		/// <summary>
		/// Returns true if using <paramref name="saved"/> as the initial focus would skip
		/// a nested <see cref="IFocusScope"/> child that appears earlier in Tab order.
		/// </summary>
		private bool WouldSkipNestedScope(IFocusableControl saved)
		{
			var children = GetFocusableChildren();
			foreach (var child in children)
			{
				if (ReferenceEquals(child, saved))
					return false; // reached saved before any scope — safe
				if (child is IFocusScope)
					return true; // a scope appears before saved — would be skipped
			}
			return true; // saved not found in children (stale) — discard it
		}

		/// <summary>
		/// Returns true if the container has at least one focusable descendant (direct or nested).
		/// </summary>
		private static bool HasAnyFocusableDescendant(IContainerControl container)
		{
			foreach (var child in container.GetChildren())
			{
				if (!child.Visible) continue;
				if (child is IFocusableControl f && f.CanReceiveFocus)
					return true;
				if (child is IContainerControl nested && HasAnyFocusableDescendant(nested))
					return true;
			}
			return false;
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
