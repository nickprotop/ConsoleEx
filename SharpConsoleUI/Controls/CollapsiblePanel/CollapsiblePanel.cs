// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A container control that hosts a flat list of child controls beneath a clickable header.
	/// The header shows an indicator and an optional title, and the body can be expanded or
	/// collapsed. This is a DOM-layout container: it stores and exposes its children, and the
	/// layout engine paints them — the panel itself only paints its own header chrome.
	/// </summary>
	/// <remarks>
	/// This is a <see langword="partial"/> class. Header rendering, state toggling, mouse and
	/// keyboard activation, and the optional height animation are implemented in companion
	/// partials added by later tasks.
	/// </remarks>
	public partial class CollapsiblePanel : BaseControl, IContainer, IContainerControl, IControlHost, IMouseAwareControl, IInteractiveControl, IFocusableControl, IFocusableContainerWithHeader, ILogicalCursorProvider
	{
		#region Fields

		private readonly List<IWindowControl> _children = new();
		private readonly object _childrenLock = new();

		private bool _isExpanded = true;
		private string? _title;
		private CollapsibleHeaderStyle _headerStyle = CollapsibleHeaderStyle.Borderless;
		private CollapsibleAnimationMode _animationMode = CollapsibleAnimationMode.None;
		private string _expandedIcon = ControlDefaults.CollapsiblePanelExpandedIcon;
		private string _collapsedIcon = ControlDefaults.CollapsiblePanelCollapsedIcon;
		private bool _showHeaderSeparator = false;
		private HorizontalAlignment _headerAlignment = HorizontalAlignment.Left;
		private bool _collapsible = true;
		private bool _showHeader = true;

		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Color? _focusedForegroundValue;
		private Color? _focusedBackgroundValue;

		private int? _maxContentHeight;
		private Color? _borderColorValue;

		private bool _isEnabled = true;
		private bool _isDirty = true;

		// Resolved on-screen height of the body region, published by CollapsibleLayout during
		// arrange. Used by GetVisibleHeightForControl so body children report the real viewport.
		private int? _arrangedBodyRegionHeight;

		#endregion

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <summary>
		/// Gets or sets the header title text shown next to the expand/collapse indicator.
		/// </summary>
		/// <remarks>
		/// Upgraded with richer behavior in a later task; backed by <c>SetProperty</c>
		/// so changes invalidate the container.
		/// </remarks>
		public string? Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		#endregion

		#region Header geometry

		/// <summary>
		/// Gets or sets how the panel header (and, when expanded, the body frame) is drawn.
		/// Defaults to <see cref="CollapsibleHeaderStyle.Borderless"/>.
		/// </summary>
		public CollapsibleHeaderStyle HeaderStyle
		{
			get => _headerStyle;
			set => SetProperty(ref _headerStyle, value);
		}

		/// <summary>
		/// Gets or sets the indicator glyph shown in the header when the panel is expanded.
		/// When <see langword="null"/> or empty, no indicator is drawn.
		/// </summary>
		public string? ExpandedIcon
		{
			get => _expandedIcon;
			set => SetProperty(ref _expandedIcon, value ?? string.Empty);
		}

		/// <summary>
		/// Gets or sets the indicator glyph shown in the header when the panel is collapsed.
		/// When <see langword="null"/> or empty, no indicator is drawn.
		/// </summary>
		public string? CollapsedIcon
		{
			get => _collapsedIcon;
			set => SetProperty(ref _collapsedIcon, value ?? string.Empty);
		}

		/// <summary>
		/// Gets or sets whether a horizontal separator line is drawn beneath a borderless header.
		/// Has no effect on the bordered header style. Defaults to <see langword="false"/>.
		/// </summary>
		public bool ShowHeaderSeparator
		{
			get => _showHeaderSeparator;
			set => SetProperty(ref _showHeaderSeparator, value);
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the indicator + title within the header row.
		/// Defaults to <see cref="HorizontalAlignment.Left"/>.
		/// </summary>
		public HorizontalAlignment HeaderAlignment
		{
			get => _headerAlignment;
			set => SetProperty(ref _headerAlignment, value);
		}

		/// <summary>
		/// Gets or sets whether the panel can be collapsed/expanded by the user. Defaults to
		/// <see langword="true"/> (current behavior). When <see langword="false"/> the panel is
		/// locked expanded, draws no expand/collapse indicator, ignores header clicks and
		/// Enter/Space, and is not itself a Tab stop — focus passes through to its body children
		/// (the panel behaves as a plain container).
		/// </summary>
		public bool Collapsible
		{
			get => _collapsible;
			set
			{
				if (_collapsible == value)
					return; // guard: no redundant notification / invalidation (CLAUDE.md rule #5)

				_collapsible = value;
				OnPropertyChanged();

				// A non-collapsible panel is always expanded. When we force-expand here, SetExpanded
				// performs the single invalidation (and layout rebuild); otherwise invalidate once.
				if (!value && !_isExpanded)
					SetExpanded(true);
				else
					Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the header row is drawn. Defaults to <see langword="true"/>.
		/// When <see langword="false"/> and the panel is non-collapsible, the header is fully
		/// suppressed: a borderless panel shows only its body, and a bordered panel draws a clean
		/// box (titleless top border) around the body. A collapsible panel always shows its header
		/// regardless of this flag (see <see cref="Collapsible"/>), since the header is the only
		/// affordance for toggling it.
		/// </summary>
		public bool ShowHeader
		{
			get => _showHeader;
			set => SetProperty(ref _showHeader, value);
		}

		/// <summary>
		/// Effective header visibility. A collapsible panel must show its header (it is the only
		/// toggle affordance), so the header is shown whenever <see cref="ShowHeader"/> is true OR
		/// the panel is collapsible. This resolves the invalid (Collapsible=true, ShowHeader=false)
		/// combination gracefully with no exception.
		/// </summary>
		private bool EffectiveShowHeader => _showHeader || _collapsible;

		/// <summary>
		/// Gets or sets the maximum height in rows allotted to the expanded body before it scrolls.
		/// When <see langword="null"/>, the body is sized to its content. Consumed by the layout
		/// engine in a later task.
		/// </summary>
		public int? MaxContentHeight
		{
			get => _maxContentHeight;
			set => SetProperty(ref _maxContentHeight, value);
		}

		/// <summary>
		/// Gets or sets the color used for the header separator and the body border.
		/// When <see langword="null"/>, the resolved foreground color is used.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColorValue;
			set => SetProperty(ref _borderColorValue, value);
		}

		/// <summary>
		/// Gets or sets the header foreground color used when the panel has keyboard focus.
		/// When <see langword="null"/>, the color is resolved from the theme
		/// (<see cref="Themes.ITheme.CollapsibleHeaderFocusedForegroundColor"/>).
		/// </summary>
		public Color? FocusedForegroundColor
		{
			get => _focusedForegroundValue;
			set => SetProperty(ref _focusedForegroundValue, value);
		}

		/// <summary>
		/// Gets or sets the header background color used when the panel has keyboard focus.
		/// When <see langword="null"/>, the color is resolved from the theme
		/// (<see cref="Themes.ITheme.CollapsibleHeaderFocusedBackgroundColor"/>).
		/// </summary>
		public Color? FocusedBackgroundColor
		{
			get => _focusedBackgroundValue;
			set => SetProperty(ref _focusedBackgroundValue, value);
		}

		#endregion

		#region Public state

		/// <summary>Fired once each time the expanded state actually changes. Arg is the new expanded value.</summary>
		public event EventHandler<bool>? ExpandedChanged;

		/// <summary>Gets or sets whether the body is expanded (visible). Default true.</summary>
		public bool IsExpanded
		{
			get => _isExpanded;
			set => SetExpanded(value);
		}

		/// <summary>Expands the body.</summary>
		public void Expand() => SetExpanded(true);

		/// <summary>Collapses the body, leaving only the header visible.</summary>
		public void Collapse() => SetExpanded(false);

		/// <summary>Toggles between expanded and collapsed.</summary>
		public void Toggle() => SetExpanded(!_isExpanded);

		private void SetExpanded(bool value)
		{
			// A non-collapsible panel is permanently expanded; ignore any attempt to collapse it.
			if (!_collapsible)
				value = true;

			if (_isExpanded == value)
				return; // guard: no redundant event / invalidation (CLAUDE.md rule #5)

			_isExpanded = value;
			ApplyChildVisibility();

			if (_animationMode == CollapsibleAnimationMode.Height)
				StartHeightAnimation(); // Task 7 fills this in; instant fallback until then

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);

			// INPC for IsExpanded so data binding sees toggles (property setter + Toggle/Expand/Collapse all route here).
			OnPropertyChanged(nameof(IsExpanded));
			ExpandedChanged?.Invoke(this, _isExpanded);
		}

		private void ApplyChildVisibility()
		{
			lock (_childrenLock)
			{
				foreach (var c in _children)
					c.Visible = _isExpanded;
			}
		}

		// Animation hook. Implemented in Task 7 (Animations partial). No-op until then.
		partial void StartHeightAnimationCore();
		private void StartHeightAnimation() => StartHeightAnimationCore();

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set { _backgroundColorValue = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveForeground(_foregroundColorValue, Container);
			set { _foregroundColorValue = value; Container?.Invalidate(true); }
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
			Container?.Invalidate(redrawAll, callerControl);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// For a direct body child, reports the arranged body region height (the real on-screen
		/// space the layout allotted the body) so scroll-aware children derive a correct viewport.
		/// Falls back to delegating to the parent container when the region is not yet known or the
		/// control is not a direct child.
		/// </remarks>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			if (_arrangedBodyRegionHeight is int regionH && IsDirectChild(control))
				return regionH;
			return Container?.GetVisibleHeightForControl(control);
		}

		private bool IsDirectChild(IWindowControl control)
		{
			lock (_childrenLock) { return _children.Contains(control); }
		}

		/// <summary>
		/// Records the body region height resolved by <see cref="Layout.CollapsibleLayout"/> during
		/// arrange. Called by the layout engine; not part of the public control surface.
		/// </summary>
		/// <param name="height">The on-screen height in rows available to the body.</param>
		internal void SetArrangedBodyRegionHeight(int height) => _arrangedBodyRegionHeight = height;

		#endregion

		#region IControlHost / IContainerControl Implementation

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> Children
		{
			get { lock (_childrenLock) { return new List<IWindowControl>(_children); } }
		}

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			lock (_childrenLock) { return new List<IWindowControl>(_children); }
		}

		/// <summary>
		/// Gets a locked snapshot of the current child controls. Used by the layout factory
		/// (wired in a later task) to build child layout nodes.
		/// </summary>
		/// <returns>A snapshot of the children in insertion order.</returns>
		internal IReadOnlyList<IWindowControl> ContentsSnapshot() => GetChildren();

		/// <inheritdoc/>
		public void AddControl(IWindowControl control)
		{
			control.Container = this;
			control.Visible = _isExpanded;
			lock (_childrenLock)
			{
				_children.Add(control);
			}

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <inheritdoc/>
		public void RemoveControl(IWindowControl control)
		{
			bool removed;
			lock (_childrenLock)
			{
				removed = _children.Remove(control);
			}

			if (removed)
			{
				control.Container = null;
				control.Dispose();

				(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public void ClearControls()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				snapshot = new List<IWindowControl>(_children);
				_children.Clear();
			}

			foreach (var child in snapshot)
			{
				child.Container = null;
				child.Dispose();
			}

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		#endregion

		#region IInteractiveControl Implementation

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		// ProcessKey is implemented in CollapsiblePanel.Keyboard.cs (Task 6).

		#endregion

		#region Layout (IDOMPaintable)

		/// <summary>
		/// Gets the height in rows occupied by the panel header (excluding margins).
		/// For the borderless style this is the header row plus the optional separator row.
		/// For the bordered style it is the single top-border row; the box frames the body
		/// below it when expanded (completed in a later task).
		/// </summary>
		internal int HeaderHeight
		{
			get
			{
				if (_headerStyle == CollapsibleHeaderStyle.Bordered)
					return 1; // top border row; box frames the body below. When the header is
							  // hidden this row becomes a plain (titleless) top border.
							  // Borderless: a hidden header occupies no rows at all.
				if (!EffectiveShowHeader)
					return 0;
				int h = ControlDefaults.CollapsiblePanelBorderlessHeaderHeight;
				if (_showHeaderSeparator) h += ControlDefaults.CollapsiblePanelHeaderSeparatorHeight;
				return h;
			}
		}

		/// <summary>Test-only accessor for <see cref="HeaderHeight"/>.</summary>
		internal int HeaderHeightForTest => HeaderHeight;

		private string? CurrentIcon => !_collapsible ? null : (_isExpanded ? _expandedIcon : _collapsedIcon);

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = Width ?? constraints.MaxWidth;
			int height = HeaderHeight + Margin.Top + Margin.Bottom;
			// Body height is added by CollapsibleLayout (Task 5). Direct measure = header-only.
			return new LayoutSize(width, height);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Paints the borderless header (indicator + title + optional separator). The bordered
		/// style and the body children are painted by the layout partial added in a later task.
		/// </remarks>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			Color bg = ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			Color fg = ColorResolver.ResolveForeground(_foregroundColorValue, Container);

			// When the header is the active Tab stop, recolor it with the themed focus colors
			// so the user can see which panel header is focused. Body children are unaffected.
			if (HasFocus)
			{
				fg = ColorResolver.ResolveCollapsibleHeaderFocusedForeground(_focusedForegroundValue, Container);
				bg = ColorResolver.ResolveCollapsibleHeaderFocusedBackground(_focusedBackgroundValue, Container);
			}

			if (_headerStyle == CollapsibleHeaderStyle.Bordered)
			{
				PaintBorderedHeader(buffer, bounds, clipRect, fg, bg); // Task 5 implements
				return;
			}

			if (!EffectiveShowHeader)
				return; // borderless headerless panel: body is painted by the layout engine

			int headerY = bounds.Y + Margin.Top;
			int left = bounds.X + Margin.Left;
			int right = bounds.X + bounds.Width - Margin.Right;

			for (int x = left; x < right; x++)
				buffer.SetNarrowCell(x, headerY, ' ', fg, bg);

			var cells = MarkupParser.Parse(ComposeHeaderText(), fg, bg);

			int avail = Math.Max(0, right - left);
			int textWidth = Math.Min(cells.Count, avail);
			int startX = left + HorizontalOffset(textWidth, avail, _headerAlignment);

			var headerClip = new LayoutRect(left, headerY, avail, 1);
			buffer.WriteCellsClipped(startX, headerY, cells, headerClip);

			if (_showHeaderSeparator)
			{
				int sepY = headerY + 1;
				Color sep = _borderColorValue ?? fg;
				for (int x = left; x < right; x++)
					buffer.SetNarrowCell(x, sepY, '─', sep, bg);
			}
		}

		private static int HorizontalOffset(int contentWidth, int available, HorizontalAlignment align)
		{
			if (contentWidth >= available) return 0;
			return align switch
			{
				HorizontalAlignment.Center => (available - contentWidth) / 2,
				HorizontalAlignment.Right => available - contentWidth,
				_ => 0
			};
		}

		/// <summary>
		/// Composes the header text shown next to nothing (bordered) or after the indicator
		/// (borderless): the current indicator glyph followed by the title. Shared by both
		/// header styles to avoid duplication.
		/// </summary>
		private string ComposeHeaderText()
		{
			string icon = CurrentIcon ?? string.Empty;
			return string.IsNullOrEmpty(icon)
				? (_title ?? string.Empty)
				: icon + " " + (_title ?? string.Empty);
		}

		/// <summary>
		/// Maps the panel's <see cref="HorizontalAlignment"/> header alignment to the
		/// <see cref="TextJustification"/> understood by the shared border renderer.
		/// </summary>
		private static TextJustification MapAlignment(HorizontalAlignment a) => a switch
		{
			HorizontalAlignment.Center => TextJustification.Center,
			HorizontalAlignment.Right => TextJustification.Right,
			_ => TextJustification.Left
		};

		/// <summary>
		/// Paints the bordered header. When expanded, the top border (with the indicator + title
		/// embedded), the side rows and the bottom border are drawn to frame the body region; the
		/// body content itself is painted by the layout engine. When collapsed, the single header
		/// row is rendered as a flat titled horizontal rule (no corner glyphs) so the panel does
		/// not show dangling corners with no box beneath them.
		/// </summary>
		private void PaintBorderedHeader(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color fg, Color bg)
		{
			Color border = _borderColorValue ?? fg;
			var box = BoxChars.Single;

			int x = bounds.X + Margin.Left;
			int top = bounds.Y + Margin.Top;
			int width = Math.Max(2, bounds.Width - Margin.Left - Margin.Right);

			if (!_isExpanded)
			{
				// Collapsed footprint: a flat titled rule (horizontal ends, no corners).
				PanelBorderRenderer.DrawTitledRule(buffer, x, top, width, clipRect, box, border, bg,
					ComposeHeaderText(), MapAlignment(_headerAlignment));
				return;
			}

			// Expanded: full box. Top border carries the title + indicator, unless the header is
			// hidden (non-collapsible panel mode) in which case the top border is drawn titleless.
			string? topHeader = EffectiveShowHeader ? ComposeHeaderText() : null;
			PanelBorderRenderer.DrawTopBorder(buffer, x, top, width, clipRect, box, border, bg,
				topHeader, MapAlignment(_headerAlignment));

			// Frame the body: side rows from top+1 down to the row above the bottom border.
			int bottom = bounds.Y + bounds.Height - Margin.Bottom - 1;
			for (int rowY = top + 1; rowY < bottom; rowY++)
				PanelBorderRenderer.DrawBorderedRow(buffer, x, rowY, width, clipRect, box, border, bg);

			if (bottom > top)
				PanelBorderRenderer.DrawBottomBorder(buffer, x, bottom, width, clipRect, box, border, bg);
		}

		#endregion

		#region IFocusableControl Implementation

		/// <inheritdoc/>
		/// <remarks>
		/// A collapsible panel's header is a Tab focus stop so it can be toggled with Enter/Space.
		/// A non-collapsible panel is a transparent focus container (like a ColumnContainer): it is
		/// not itself a stop, so focus passes straight through to its focusable body children.
		/// </remarks>
		public bool CanReceiveFocus => _isEnabled && _collapsible;

		/// <inheritdoc/>
		public bool HasFocus => ComputeHasFocus();

		#endregion

		#region ILogicalCursorProvider Implementation

		/// <summary>
		/// Finds the focused interactive child in the panel body, mirroring how
		/// <see cref="ColumnContainer"/> locates its focused content.
		/// </summary>
		private IInteractiveControl? FindFocusedChild()
		{
			var focusManager = (this as IWindowControl).GetParentWindow()?.FocusManager;
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }
			foreach (var content in snapshot)
			{
				if (content is IInteractiveControl ic
					&& content is IFocusableControl fc
					&& (focusManager?.IsFocused(fc) ?? false))
				{
					return ic;
				}
			}
			return null;
		}

		/// <summary>
		/// Computes the focused body child's top-left offset within the panel's own coordinate
		/// space — i.e. the header rows, margins, side inset and the heights of the siblings stacked
		/// above it. The cursor a child reports is relative to that child's own origin, so the panel
		/// must add this offset before handing the position up to its container (a ColumnContainer or
		/// a self-painting ScrollablePanelControl), which composes it further. Without this, the
		/// cursor lands on the panel's header row and the terminal hides it.
		/// </summary>
		/// <remarks>
		/// The offset is derived by measuring and arranging a transient copy of the panel's own
		/// layout subtree at the panel's actual on-screen size, then reading the focused child node's
		/// bounds relative to the subtree root (Arrange computes descendant bounds relative to the
		/// arranged root when it sits at the origin). This reuses the real <see cref="CollapsibleLayout"/>
		/// so the offset can never desync from where the body is actually painted, and it works in
		/// both the direct-DOM case and the self-painted (inside ScrollablePanelControl) case where
		/// the body children have no persistent window-level layout nodes.
		/// </remarks>
		private Point? FocusedChildOffsetInPanel(IWindowControl focusedChild)
		{
			int width = ActualWidth > 0 ? ActualWidth : (Width ?? 0);
			if (width <= 0) return null;
			int height = ActualHeight > 0 ? ActualHeight : HeaderHeight + Margin.Top + Margin.Bottom;

			var subtree = LayoutNodeFactory.CreateSubtree(this);
			subtree.Measure(new LayoutConstraints(1, width, 1, height));
			subtree.Arrange(new LayoutRect(0, 0, width, height));

			var childNode = subtree.FindByControl(focusedChild);
			if (childNode == null) return null;

			// Subtree root is arranged at the origin, so the child's AbsoluteBounds is already its
			// offset within the panel.
			var ab = childNode.AbsoluteBounds;
			return new Point(ab.X, ab.Y);
		}

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			// When collapsed the body is hidden, so there is no cursor to show.
			if (!_isExpanded) return null;

			var focused = FindFocusedChild();
			if (focused is not ILogicalCursorProvider cursorProvider || focused is not IWindowControl wc)
				return null;

			var childCursor = cursorProvider.GetLogicalCursorPosition();
			if (childCursor == null) return null;

			var offset = FocusedChildOffsetInPanel(wc);
			if (offset == null) return null;

			// Translate the child-relative cursor into the panel's own content space so the parent
			// container receives a coordinate it can compose correctly.
			return new Point(childCursor.Value.X + offset.Value.X, childCursor.Value.Y + offset.Value.Y);
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			if (!_isExpanded) return;

			var focused = FindFocusedChild();
			if (focused is not ILogicalCursorProvider cursorProvider || focused is not IWindowControl wc)
				return;

			var offset = FocusedChildOffsetInPanel(wc);
			if (offset == null) return;

			// Inverse of GetLogicalCursorPosition: strip the panel-relative offset back off.
			cursorProvider.SetLogicalCursorPosition(
				new Point(position.X - offset.Value.X, position.Y - offset.Value.Y));
		}

		#endregion

		// Mouse activation (IMouseAwareControl) is implemented in CollapsiblePanel.Mouse.cs (Task 6).
	}
}
