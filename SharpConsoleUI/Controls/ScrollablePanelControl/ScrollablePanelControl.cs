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
	public partial class ScrollablePanelControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IContainer, IContainerControl, IControlHost, IScrollableContainer, IFocusScope, ILogicalCursorProvider
	{
		private readonly List<IWindowControl> _children = new();
		private readonly object _childrenLock = new();
		private int _verticalScrollOffset = 0;
		private int _horizontalScrollOffset = 0;
		private int _contentHeight = 0;
		private int _contentWidth = 0;
		private int _viewportHeight = 0;
		private int _viewportWidth = 0;

		// Per-pass cache for ResolveContentMetrics. ScrollLayout calls ResolveContentMetrics up to
		// three times within a single Measure->Arrange->Paint pass (MeasureChildren, ArrangeChildren,
		// and once per child via GetPaintClipRect), each running the O(children) CalculateContentWidth/
		// CalculateContentHeight traversal (CLAUDE.md rules 3 & 11). When the same outerBounds is passed
		// again, the metrics are deterministic (same bounds + same children => identical field values),
		// so we restore the cached field values + clamped offsets and return the cached rect instead of
		// re-traversing. The cache is keyed on the input bounds; a different bounds (or a content/scroll
		// change that recomputes the fields via PaintDOM/scrolling) recomputes and refreshes the cache.
		// NOTE: the scroll OFFSETS are deliberately NOT cached. ResolveContentMetrics only CLAMPS them
		// (cheap, idempotent), while other paths (AutoScroll in PaintDOM, scroll input) set them
		// independently without going through Invalidate; caching/restoring them would clobber those
		// updates. Only the expensive viewport/content measurement and the returned rect are cached.
		private bool _metricsCacheValid = false;
		private LayoutRect _metricsCacheBounds;
		private LayoutRect _metricsCacheResult;
		private int _metricsCacheViewportHeight;
		private int _metricsCacheViewportWidth;
		private int _metricsCacheContentHeight;
		private int _metricsCacheContentWidth;

		// When ScrollLayout drives the measure, the panel's children ALREADY EXIST as persistent
		// LayoutNodes in the ScrollLayout node's Children. This resolver maps a child control to its
		// persistent node so the content-height/Fill math (ComputeChildHeight/ComputeFillMetrics) can
		// MEASURE that node directly instead of rebuilding a throwaway subtree via
		// LayoutNodeFactory.CreateSubtree on every call. Rebuilding recursively re-creates the entire
		// nested subtree per call, which for nested SPCs is multiplicative; measuring the persistent
		// node reuses the existing subtree. The resolver is set ONLY for the span of ScrollLayout's
		// measure/arrange and is null otherwise, so detached/unit-test callers (no persistent tree)
		// transparently fall back to the CreateSubtree path. See CLAUDE.md rules 3 & 11.
		private Func<IWindowControl, LayoutNode?>? _childNodeResolver;
		private bool _isEnabled = true;
		private IInteractiveControl? _lastInternalFocusedChild = null;
		// When true, GetInitialFocus returns 'this' so SetFocus(panel) enters scroll mode directly.
		private bool _enterScrollModeOnNextInitialFocus = false;

		// Click target tracking for double-click consistency
		private IWindowControl? _lastClickTarget = null;
		private DateTime _lastClickTime;
		private System.Drawing.Point _lastClickPosition;

		// Scrollbar drag state (vertical)
		private bool _isScrollbarDragging = false;
		private int _scrollbarDragStartY = 0;
		private int _scrollbarDragStartThumbPos = 0;
		// Scrollbar drag state (horizontal)
		private bool _isHScrollbarDragging = false;
		private int _scrollbarDragStartX = 0;
		private int _hScrollbarDragStartThumbPos = 0;
		// Child mouse capture: prevents drag stealing between sibling controls
		private IWindowControl? _mouseCaptureChild;

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

		// Set when ScrollToBottom() is called before the viewport has been laid out;
		// PaintDOM completes the scroll once content/viewport metrics are known.
		private bool _pendingScrollToBottom = false;

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
		/// Gets or sets whether arrow / page / home-end keys scroll the viewport.
		/// When <c>false</c>, those keys are left unhandled by <see cref="ProcessKey"/> so they
		/// can bubble to an owning control that wants them for its own navigation (e.g. the
		/// <see cref="NavigationView"/> nav pane, which moves the selected item on arrows and
		/// relies on focus-driven auto-scroll to keep it visible). Mouse-wheel scrolling and the
		/// scrollbar are unaffected. Defaults to <c>true</c>.
		/// </summary>
		public bool ArrowKeyScrolling { get; set; } = true;

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
		/// Gets whether the content can be scrolled right (more content exists beyond the visible
		/// content width). The visible width excludes the vertical scrollbar columns, so the last
		/// columns of content remain reachable even when a vertical scrollbar is present.
		/// </summary>
		public bool CanScrollRight => _horizontalScrollOffset < MaxHorizontalScrollOffset;

		/// <summary>
		/// Gets whether a vertical scrollbar is currently shown: scrollbars are enabled, the vertical
		/// scroll mode is <see cref="ScrollMode.Scroll"/>, and content overflows the (horizontal-
		/// scrollbar-reduced) viewport height.
		/// </summary>
		public bool HasVerticalScrollbar => NeedsVerticalScrollbar;

		/// <summary>
		/// Gets whether a horizontal scrollbar is currently shown: scrollbars are enabled, the
		/// horizontal scroll mode is <see cref="ScrollMode.Scroll"/>, and content overflows the
		/// (vertical-scrollbar-reduced) viewport width.
		/// </summary>
		public bool HasHorizontalScrollbar => NeedsHorizontalScrollbar;

		#endregion

		#region Scrollbar Reservation (single source of truth)

		// Reserved space: a vertical scrollbar uses 2 columns (1 gap + 1 bar); a horizontal
		// scrollbar uses 1 row. Centralised so paint, hit-testing, clamping and geometry agree.
		internal const int VerticalScrollbarColumns = 2;
		internal const int HorizontalScrollbarRows = 1;

		// The two scrollbars are mutually dependent: a vertical bar steals columns (which can push
		// content past the width) and a horizontal bar steals a row (which can push content past
		// the height). We break the cycle with a non-mutual first pass, then resolve once.
		private bool RawNeedsVerticalScrollbar =>
			_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _viewportHeight > 0
			&& _contentHeight > _viewportHeight;

		private bool RawNeedsHorizontalScrollbar =>
			_showScrollbar && _horizontalScrollMode == ScrollMode.Scroll && _viewportWidth > 0
			&& _contentWidth > _viewportWidth;

		private bool NeedsVerticalScrollbar
		{
			get
			{
				if (!_showScrollbar || _verticalScrollMode != ScrollMode.Scroll || _viewportHeight <= 0)
					return false;
				int effHeight = _viewportHeight - (RawNeedsHorizontalScrollbar ? HorizontalScrollbarRows : 0);
				return _contentHeight > Math.Max(0, effHeight);
			}
		}

		private bool NeedsHorizontalScrollbar
		{
			get
			{
				if (!_showScrollbar || _horizontalScrollMode != ScrollMode.Scroll || _viewportWidth <= 0)
					return false;
				int effWidth = _viewportWidth - (RawNeedsVerticalScrollbar ? VerticalScrollbarColumns : 0);
				return _contentWidth > Math.Max(0, effWidth);
			}
		}

		/// <summary>
		/// The visible content width (viewport minus the vertical scrollbar columns, when shown).
		/// This is the width children are painted into and the basis for horizontal scroll extent.
		/// </summary>
		private int VisibleContentWidth =>
			Math.Max(1, _viewportWidth - (NeedsVerticalScrollbar ? VerticalScrollbarColumns : 0));

		/// <summary>
		/// The content viewport height (viewport minus the horizontal scrollbar row, when shown).
		/// </summary>
		private int VisibleContentHeight =>
			Math.Max(1, _viewportHeight - (NeedsHorizontalScrollbar ? HorizontalScrollbarRows : 0));

		/// <summary>
		/// The maximum horizontal scroll offset: total content width minus the visible content
		/// width. Computed against <see cref="VisibleContentWidth"/> so the last content column is
		/// always reachable even when a vertical scrollbar steals columns (Bug A).
		/// </summary>
		private int MaxHorizontalScrollOffset => Math.Max(0, _contentWidth - VisibleContentWidth);

		#endregion

		#region Layout Participation Accessors (for ScrollLayout)

		// These internal members expose exactly what ScrollLayout needs to read so it can
		// MEASURE/ARRANGE the panel's children as a real layout-tree participant. They mirror the
		// metrics PaintDOM computes; ScrollLayout calls ResolveContentMetrics to derive the content
		// region for a given outer bounds, then reads ScrollOffset / chrome state from these getters.
		//
		// IMPORTANT: as of this task SPC still SELF-PAINTS (LayoutNodeFactory.ResolveLayout returns
		// (null, null) for it), so these accessors do not change any existing behavior. The fields
		// they populate (_viewportWidth/_viewportHeight/_contentWidth/_contentHeight) are recomputed
		// from scratch on every PaintDOM call, so a prior ResolveContentMetrics call cannot leak
		// stale state into a self-paint.

		/// <summary>The current vertical scroll offset (content rows hidden above the viewport).</summary>
		internal int VerticalScrollOffsetInternal => _verticalScrollOffset;

		/// <summary>The current horizontal scroll offset (content columns hidden left of the viewport).</summary>
		internal int HorizontalScrollOffsetInternal => _horizontalScrollOffset;

		/// <summary>
		/// The visible content width children are arranged into, AFTER reserving the vertical
		/// scrollbar columns. Valid after <see cref="ResolveContentMetrics"/> has run for the frame.
		/// </summary>
		internal int ContentViewportWidth => VisibleContentWidth;

		/// <summary>
		/// The visible content height (viewport rows), AFTER reserving the horizontal scrollbar row.
		/// Valid after <see cref="ResolveContentMetrics"/> has run for the frame.
		/// </summary>
		internal int ContentViewportHeight => VisibleContentHeight;

		/// <summary>True when a vertical scrollbar is currently reserved/shown.</summary>
		internal bool VerticalScrollbarActive => NeedsVerticalScrollbar;

		/// <summary>True when a horizontal scrollbar is currently reserved/shown.</summary>
		internal bool HorizontalScrollbarActive => NeedsHorizontalScrollbar;

		/// <summary>The total measured content height (may exceed the content viewport height).</summary>
		internal int TotalContentHeightInternal => _contentHeight;

		/// <summary>The total measured content width (may exceed the content viewport width).</summary>
		internal int TotalContentWidthInternal => _contentWidth;

		/// <summary>Left inset (border + padding) of the content area within the panel's outer box.</summary>
		internal int ContentInsetLeftInternal => ContentInsetLeft;

		/// <summary>Top inset (border + padding) of the content area within the panel's outer box.</summary>
		internal int ContentInsetTopInternal => ContentInsetTop;

		/// <summary>True when horizontal scrolling is enabled (children laid out at full content width).</summary>
		internal bool HorizontalScrollEnabledInternal => _horizontalScrollMode == ScrollMode.Scroll;

		/// <summary>
		/// The content region (in coordinates relative to the supplied <paramref name="outerBounds"/>)
		/// that children are arranged into, and populates the viewport/content fields the Fill helpers
		/// (<see cref="ComputeFillMetrics"/>/<see cref="ComputeChildHeight"/>) and the scrollbar
		/// predicates read. This is the EXACT prelude PaintDOM runs (border + padding removed, content
		/// width/height measured, offsets clamped) factored out so ScrollLayout produces a byte-identical
		/// layout. Returns the content origin + the visible content size (scrollbar chrome reserved).
		/// </summary>
		/// <param name="outerBounds">The panel's full arranged bounds.</param>
		/// <returns>
		/// A rect whose X/Y are the content origin relative to <paramref name="outerBounds"/> and whose
		/// Width/Height are the visible content viewport (scrollbar chrome excluded).
		/// </returns>
		/// <param name="clampOffsets">
		/// Whether the persisted scroll offsets (<c>_verticalScrollOffset</c>/<c>_horizontalScrollOffset</c>)
		/// may be clamped down to the viewport-derived maximum. This MUST be <c>true</c> only when
		/// <paramref name="outerBounds"/> is the REAL on-screen box (arrange/paint), and <c>false</c> for a
		/// MEASURE pass driven by an effectively-unbounded constraint — there the panel auto-sizes to its
		/// content, so the derived viewport equals the content extent and the clamp would collapse the
		/// offset to 0, silently wiping the user's scroll position on every re-layout.
		/// </param>
		internal LayoutRect ResolveContentMetrics(LayoutRect outerBounds, bool clampOffsets = true)
		{
			// A genuinely huge box (a near-int.MaxValue measure box) is also unbounded and must never clamp,
			// regardless of the caller's flag — belt-and-suspenders for direct/legacy callers.
			bool clampVertical = clampOffsets && outerBounds.Height < LayoutConstraints.UnboundedThreshold;
			bool clampHorizontal = clampOffsets && outerBounds.Width < LayoutConstraints.UnboundedThreshold;

			// Per-pass cache: if called again with the SAME bounds, restore the previously computed
			// MEASUREMENT field values (deterministic for identical bounds + children), skipping the
			// O(children) CalculateContentWidth/CalculateContentHeight traversal. Offsets are re-clamped
			// below (not cached) so AutoScroll / scroll-input updates are honored.
			if (_metricsCacheValid && _metricsCacheBounds.Equals(outerBounds))
			{
				_viewportHeight = _metricsCacheViewportHeight;
				_viewportWidth = _metricsCacheViewportWidth;
				_contentHeight = _metricsCacheContentHeight;
				_contentWidth = _metricsCacheContentWidth;

				// Only clamp the persisted scroll offset against a REAL, bounded viewport (see clampOffsets).
				if (clampVertical)
				{
					int cachedContentViewportHeight = VisibleContentHeight;
					int cachedMaxScrollOffset = Math.Max(0, _contentHeight - cachedContentViewportHeight);
					if (_verticalScrollOffset > cachedMaxScrollOffset)
						_verticalScrollOffset = cachedMaxScrollOffset;
				}
				if (clampHorizontal)
				{
					if (_horizontalScrollOffset > MaxHorizontalScrollOffset)
						_horizontalScrollOffset = MaxHorizontalScrollOffset;
				}

				return _metricsCacheResult;
			}

			int targetWidth = outerBounds.Width - Margin.Left - Margin.Right;
			int targetHeight = outerBounds.Height - Margin.Top - Margin.Bottom;

			// Inner content box (border + padding removed). Mirrors PaintDOM exactly.
			_viewportHeight = targetHeight - BorderHeight - _padding.Top - _padding.Bottom;
			_viewportWidth = targetWidth - BorderWidth - _padding.Left - _padding.Right;

			_contentWidth = CalculateContentWidth();

			int contentViewportHeight = VisibleContentHeight;
			_contentHeight = CalculateContentHeight(_viewportWidth, contentViewportHeight);

			// Clamp scroll offsets to valid bounds (same as PaintDOM) — but ONLY against a REAL, bounded
			// viewport (see clampOffsets). During a MEASURE pass the outer box is content-sized (auto-size),
			// so the derived viewport equals the content extent and maxOffset collapses to 0; clamping then
			// would wipe the user's scroll position. The offset clamp belongs to arrange/paint (true box).
			if (clampVertical)
			{
				int maxScrollOffset = Math.Max(0, _contentHeight - contentViewportHeight);
				if (_verticalScrollOffset > maxScrollOffset)
					_verticalScrollOffset = maxScrollOffset;
			}
			if (clampHorizontal)
			{
				if (_horizontalScrollOffset > MaxHorizontalScrollOffset)
					_horizontalScrollOffset = MaxHorizontalScrollOffset;
			}

			// Content origin relative to outerBounds (PaintDOM: startX = bounds.X + Margin.Left,
			// contentOriginX = startX + ContentInsetLeft; here we return the relative offset).
			int contentOriginXRel = Margin.Left + ContentInsetLeft;
			int contentOriginYRel = Margin.Top + ContentInsetTop;

			var result = new LayoutRect(contentOriginXRel, contentOriginYRel, VisibleContentWidth, contentViewportHeight);

			// Refresh the per-pass cache with this bounds + the resulting field state.
			_metricsCacheValid = true;
			_metricsCacheBounds = outerBounds;
			_metricsCacheResult = result;
			_metricsCacheViewportHeight = _viewportHeight;
			_metricsCacheViewportWidth = _viewportWidth;
			_metricsCacheContentHeight = _contentHeight;
			_metricsCacheContentWidth = _contentWidth;

			return result;
		}

		/// <summary>
		/// Re-derives the panel's runtime viewport/content metrics from its ARRANGED layout-node
		/// bounds (the on-screen box the engine gave it), so scrollability decisions are correct even
		/// when the panel's last <see cref="ResolveContentMetrics"/> ran during a MEASURE pass (with
		/// the unbounded full-content bounds) and the subsequent paint was culled — which otherwise
		/// leaves <c>_viewportHeight</c> equal to the content height and makes the panel wrongly
		/// believe it cannot scroll. Mouse-wheel handling calls this before deciding whether to scroll.
		/// </summary>
		/// <remarks>
		/// No-op when the panel is not part of a built layout tree (e.g. detached/unit-test direct
		/// calls), preserving the existing behavior of those paths. The per-pass cache is dropped first
		/// so the metrics are recomputed from the arranged bounds rather than served from a stale entry.
		/// </remarks>
		internal void SyncMetricsFromArrangedBounds()
		{
			var node = this.GetParentWindow()?.Renderer?.GetLayoutNode(this);
			if (node == null)
				return;

			var arranged = node.AbsoluteBounds;
			if (arranged.Width <= 0 || arranged.Height <= 0)
				return;

			// Resolve from a position-independent outer box of the arranged size; ResolveContentMetrics
			// only uses Width/Height (origins are relative), so X/Y are irrelevant here.
			_metricsCacheValid = false;
			ResolveContentMetrics(new LayoutRect(0, 0, arranged.Width, arranged.Height));
		}

		/// <summary>
		/// Registers (or clears) the resolver that maps a child control to its persistent layout node
		/// for the duration of a ScrollLayout-driven measure/arrange pass. While set, the content-height
		/// and Fill math reuse the already-built child subtrees instead of rebuilding throwaway subtrees
		/// per call. Pass <c>null</c> to restore the detached fallback. Returns the previously registered
		/// resolver so callers can restore it (supporting nested SPCs whose passes overlap).
		/// </summary>
		internal Func<IWindowControl, LayoutNode?>? SetChildNodeResolver(Func<IWindowControl, LayoutNode?>? resolver)
		{
			var previous = _childNodeResolver;
			_childNodeResolver = resolver;
			return previous;
		}

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

		/// <summary>
		/// The content width children are measured/positioned at, accounting for the scrollbar.
		/// </summary>
		private int CursorContentWidth() => VisibleContentWidth;

		/// <summary>
		/// Locates the focused child's vertical slot via the shared layout. Unlike the child's own
		/// <c>ActualY</c> — which the paint pass only refreshes for in-viewport children and so goes
		/// stale once a child scrolls out of view — this is always current. Returns the child's
		/// content-space top, or null if it is not a visible child.
		/// </summary>
		private int? FocusedChildContentTop(IWindowControl child)
		{
			foreach (var slot in GetVisibleChildLayout(CursorContentWidth()))
			{
				if (slot.Control == child)
					return slot.Top;
			}
			return null;
		}

		/// <inheritdoc/>
		public System.Drawing.Point? GetLogicalCursorPosition()
		{
			var focused = GetFocusedChildFromCoordinator();
			if (focused is ILogicalCursorProvider cursorProvider && focused is IWindowControl wc)
			{
				var childPos = cursorProvider.GetLogicalCursorPosition();
				int? childTop = FocusedChildContentTop(wc);
				if (childPos.HasValue && childTop.HasValue)
				{
					// Panel-relative SCREEN row of the cursor (content-Y minus scroll). Derived from
					// the shared layout, not the child's ActualY, so it stays correct when the child
					// is scrolled out of view.
					int panelRelativeY = childPos.Value.Y + childTop.Value - _verticalScrollOffset;

					// Hide the cursor when its row is outside the panel viewport — the focused child
					// has been scrolled out of view. (The window-level visibility check is unaware of
					// the panel's internal scroll, so the panel must report this itself.)
					if (_viewportHeight > 0 && (panelRelativeY < 0 || panelRelativeY >= VisibleContentHeight))
						return null;

					// Apply the horizontal scroll offset so the cursor tracks a horizontally-scrolled child.
					return new System.Drawing.Point(
						childPos.Value.X + ContentInsetLeft - _horizontalScrollOffset,
						panelRelativeY + ContentInsetTop);
				}
			}
			return null;
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(System.Drawing.Point position)
		{
			var focused = GetFocusedChildFromCoordinator();
			if (focused is ILogicalCursorProvider cursorProvider && focused is IWindowControl wc)
			{
				int? childTop = FocusedChildContentTop(wc);
				if (childTop.HasValue)
				{
					// Inverse of GetLogicalCursorPosition.
					var childPos = new System.Drawing.Point(
						position.X - ContentInsetLeft + _horizontalScrollOffset,
						position.Y - childTop.Value + _verticalScrollOffset - ContentInsetTop);
					cursorProvider.SetLogicalCursorPosition(childPos);
				}
			}
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
			// Any invalidation (content/scroll/structural change) may alter the metrics; drop the
			// per-pass ResolveContentMetrics cache so the next pass recomputes from scratch.
			_metricsCacheValid = false;
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
