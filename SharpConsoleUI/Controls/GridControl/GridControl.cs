// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A two-dimensional layout container that arranges its children into rows and columns. Tracks are
	/// sized with <see cref="GridLength"/> definitions (fixed cells, auto-to-content, or proportional
	/// star weights), and cells are placed by <see cref="GridPlacement"/> with optional row/column
	/// spanning. The actual measuring and arranging is performed by <see cref="GridLayout"/>, which
	/// reads this control through the <see cref="IGridSource"/> seam.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Children can be placed explicitly with <see cref="Place"/>, or appended in row-major auto-flow
	/// order via <see cref="IControlHost.AddControl"/>. Auto-flow grows the row definitions as needed.
	/// </para>
	/// <para>
	/// This control does not paint its children: child controls are turned into layout nodes and
	/// painted by the DOM tree. <see cref="PaintDOM"/> only paints the grid's own background.
	/// </para>
	/// <para>
	/// <b>Threading.</b> Mutations — <see cref="Place"/>, <see cref="IControlHost.AddControl"/>,
	/// <see cref="RemoveControl"/>, <see cref="ReplaceControl"/>, <see cref="RemoveAt"/>,
	/// <see cref="ClearControls"/>, and edits to <see cref="RowDefinitions"/>/<see cref="ColumnDefinitions"/>
	/// — should be performed on the UI thread, or marshalled there via
	/// <c>ConsoleWindowSystem.EnqueueOnUIThread</c> (see CLAUDE.md rule 13). The grid and its definition
	/// lists lock internally so concurrent mutation cannot corrupt the underlying collections or a reader
	/// on the render thread, but this is a defensive guard only: it does not guarantee semantic
	/// consistency (for example, a cell placed concurrently with a column removal may land in an
	/// unexpected cell). The layout is always handed stable per-frame snapshots.
	/// </para>
	/// </remarks>
	public partial class GridControl : BaseControl, IContainer, IContainerControl, IControlHost, IColorRoleableControl, IGridSource
	{
		/// <summary>Default number of columns assumed when no column definitions are supplied.</summary>
		private const int DefaultColumnCount = 1;

		// Cell entries may hold a null Control: a content-less entry is a styled-but-empty cell (a
		// background/border applied to a cell before — or without — any content). Content-less entries
		// are painted as chrome but are NOT layout-tree children or focus stops, so OrderedCells (the
		// IGridSource seam read by the factory, measure/arrange, and focus) is content-only by
		// construction; AllOrderedCells exposes every entry for the paint pass only.
		private readonly List<(IWindowControl? Control, GridPlacement Placement)> _cells = new();
		private readonly object _cellsLock = new();
		private List<(IWindowControl Control, GridPlacement Placement)>? _orderedCellsCache;
		private List<(IWindowControl? Control, GridPlacement Placement)>? _allOrderedCellsCache;
		private GridLayout? _layoutAlgorithm;
		// Cached row-major projection of the cells' controls. Derived from _orderedCellsCache and
		// keyed on its reference identity, so it is rebuilt only when the ordered set changes. Used by
		// the hot focus paths (HasFocus / focused-child lookup, read by the render loop per repaint and
		// on every keystroke) so they iterate children WITHOUT allocating a fresh projected list each
		// call (CLAUDE.md rule 3). See OrderedChildren.
		private List<(IWindowControl Control, GridPlacement Placement)>? _orderedChildrenSource;
		private IReadOnlyList<IWindowControl>? _orderedChildrenCache;

		private readonly GridDefinitionList _rowDefinitions;
		private readonly GridDefinitionList _columnDefinitions;

		private int _rowGap;
		private int _columnGap;
		private Padding _padding = new(0);
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private bool _isDirty;

		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set => SetProperty(ref _role, value);
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set => SetProperty(ref _colorRoleMode, value);
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set => SetProperty(ref _outline, value);
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new, empty grid. Add track definitions via <see cref="RowDefinitions"/> and
		/// <see cref="ColumnDefinitions"/>, then place children with <see cref="Place"/> or auto-flow them
		/// with <see cref="IControlHost.AddControl"/>.
		/// </summary>
		public GridControl()
		{
			_rowDefinitions = new GridDefinitionList(OnDefinitionsChanged);
			_columnDefinitions = new GridDefinitionList(OnDefinitionsChanged);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the background fill colour. When unset (the default), the grid does not paint a
		/// background and its cells show through to whatever is behind it; the getter then reports
		/// <see cref="Color.Transparent"/> to satisfy <see cref="IContainer"/>.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Color.Transparent;
			set { _backgroundColorValue = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground (text) colour for the grid and its children. When unset, it
		/// resolves from the active theme.
		/// </summary>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveForeground(_foregroundColorValue, Container);
			set { _foregroundColorValue = value; OnPropertyChanged(); Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets the row track definitions, top to bottom. Add a <see cref="GridLength"/> per row, e.g.
		/// <c>grid.RowDefinitions.Add(GridLength.Star(1));</c>. Auto-flow grows this list as needed.
		/// Mutating this list at runtime (add, remove, clear, replace) rebuilds and invalidates the grid
		/// so the change is reflected on the next render.
		/// </summary>
		public IList<GridLength> RowDefinitions => _rowDefinitions;

		/// <summary>
		/// Gets the column track definitions, left to right. Add a <see cref="GridLength"/> per column,
		/// e.g. <c>grid.ColumnDefinitions.Add(GridLength.Star(1));</c>. Mutating this list at runtime
		/// (add, remove, clear, replace) rebuilds and invalidates the grid so the change is reflected on
		/// the next render.
		/// </summary>
		public IList<GridLength> ColumnDefinitions => _columnDefinitions;

		/// <summary>Gets or sets the gap, in cells, between adjacent rows. Defaults to 0.</summary>
		public int RowGap
		{
			get => _rowGap;
			set { if (SetProperty(ref _rowGap, value)) Container?.Invalidate(true); }
		}

		/// <summary>Gets or sets the gap, in cells, between adjacent columns. Defaults to 0.</summary>
		public int ColumnGap
		{
			get => _columnGap;
			set { if (SetProperty(ref _columnGap, value)) Container?.Invalidate(true); }
		}

		/// <summary>Gets or sets the grid's own inner padding. Defaults to <see cref="Padding.None"/>.</summary>
		public Padding Padding
		{
			get => _padding;
			set { if (SetProperty(ref _padding, value)) Container?.Invalidate(true); }
		}

		#endregion

		#region IGridSource explicit members

		/// <inheritdoc/>
		/// <remarks>
		/// Returns a per-frame snapshot taken under the list's internal lock so the layout sees a stable
		/// set of tracks that cannot change mid-measure/arrange, mirroring <see cref="OrderedCells"/>.
		/// </remarks>
		IReadOnlyList<GridLength> IGridSource.RowDefinitions => _rowDefinitions.Snapshot();

		/// <inheritdoc/>
		/// <remarks>
		/// Returns a per-frame snapshot taken under the list's internal lock so the layout sees a stable
		/// set of tracks that cannot change mid-measure/arrange, mirroring <see cref="OrderedCells"/>.
		/// </remarks>
		IReadOnlyList<GridLength> IGridSource.ColumnDefinitions => _columnDefinitions.Snapshot();

		/// <inheritdoc/>
		public IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> OrderedCells => BuildOrderedCells();

		#endregion

		/// <summary>
		/// Places a child control at the specified cell, with optional row and column spanning.
		/// </summary>
		/// <param name="control">The control to place.</param>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		/// <param name="rowSpan">The number of rows the cell occupies. Must be at least 1.</param>
		/// <param name="colSpan">The number of columns the cell occupies. Must be at least 1.</param>
		/// <returns>This grid, to allow fluent chaining.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown when <paramref name="row"/> or <paramref name="col"/> is negative, when
		/// <paramref name="rowSpan"/> or <paramref name="colSpan"/> is less than 1, or when track
		/// definitions exist and the start cell falls outside them.
		/// </exception>
		public GridControl Place(IWindowControl control, int row, int col, int rowSpan = 1, int colSpan = 1)
		{
			ArgumentNullException.ThrowIfNull(control);
			if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
			if (col < 0) throw new ArgumentOutOfRangeException(nameof(col));
			if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan));
			if (colSpan < 1) throw new ArgumentOutOfRangeException(nameof(colSpan));

			// When definitions exist, enforce range as a typo guard. When empty, defs may be set later.
			if (RowDefinitions.Count > 0 && row >= RowDefinitions.Count)
				throw new ArgumentOutOfRangeException(nameof(row));
			if (ColumnDefinitions.Count > 0 && col >= ColumnDefinitions.Count)
				throw new ArgumentOutOfRangeException(nameof(col));

			lock (_cellsLock)
			{
				_cells.Add((control, new GridPlacement(row, col, rowSpan, colSpan)));
				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}
			control.Container = this;
			RebuildAndInvalidate();
			return this;
		}

		/// <summary>
		/// Gets a lightweight <see cref="GridCell"/> handle addressing the cell whose top-left is
		/// <paramref name="row"/>/<paramref name="col"/>. The handle is a value type that reads and
		/// writes this grid's cell store directly; it stores no state of its own. Use it to get/set the
		/// cell's content and per-cell styling, e.g. <c>grid[0, 1].Background = Color.Blue;</c>.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		public GridCell this[int row, int col] => new(this, row, col);

		/// <summary>
		/// Returns a lightweight <see cref="GridCell"/> handle addressing the cell whose top-left is
		/// <paramref name="row"/>/<paramref name="col"/>. Equivalent to the indexer
		/// <see cref="this[int, int]"/>; provided for call sites that prefer a method.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		public GridCell Cell(int row, int col) => new(this, row, col);

		#region IControlHost Implementation

		/// <summary>
		/// Appends a control in row-major auto-flow order: it lands in the next free cell given the
		/// current column count (the number of column definitions, or 1 if none). Row definitions are
		/// grown automatically when the flow runs past the last defined row.
		/// </summary>
		/// <param name="control">The control to append.</param>
		public void AddControl(IWindowControl control)
		{
			ArgumentNullException.ThrowIfNull(control);

			int columnCount = ColumnDefinitions.Count > 0 ? ColumnDefinitions.Count : DefaultColumnCount;

			lock (_cellsLock)
			{
				var (row, col) = FindNextFreeCell(columnCount);

				// Auto-grow rows so the placement is in range.
				while (RowDefinitions.Count <= row)
					RowDefinitions.Add(GridLength.Star(1));

				_cells.Add((control, new GridPlacement(row, col)));
				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}
			control.Container = this;
			RebuildAndInvalidate();
		}

		/// <summary>
		/// Removes a child control from the grid. Other cells are left in place (no repacking).
		/// </summary>
		/// <param name="control">The control to remove.</param>
		public void RemoveControl(IWindowControl control)
		{
			bool removed;
			lock (_cellsLock)
			{
				int index = _cells.FindIndex(c => ReferenceEquals(c.Control, control));
				if (index < 0) return;
				_cells.RemoveAt(index);
				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
				removed = true;
			}

			if (removed)
				control.Container = null;
			RebuildAndInvalidate();
		}

		/// <summary>Removes all child controls from the grid.</summary>
		public void ClearControls()
		{
			List<(IWindowControl? Control, GridPlacement Placement)> snapshot;
			lock (_cellsLock)
			{
				snapshot = new List<(IWindowControl? Control, GridPlacement Placement)>(_cells);
				_cells.Clear();
				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}
			foreach (var (control, _) in snapshot)
				if (control != null)
					control.Container = null;
			RebuildAndInvalidate();
		}

		/// <summary>
		/// Replaces an existing child control with a new one, keeping the old control's cell placement
		/// (row, column, and spans). Other cells are untouched.
		/// </summary>
		/// <param name="oldControl">The control currently placed in the grid.</param>
		/// <param name="newControl">The control to put in <paramref name="oldControl"/>'s cell.</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="oldControl"/> or <paramref name="newControl"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="oldControl"/> is not currently placed in this grid.
		/// </exception>
		public void ReplaceControl(IWindowControl oldControl, IWindowControl newControl)
		{
			ArgumentNullException.ThrowIfNull(oldControl);
			ArgumentNullException.ThrowIfNull(newControl);

			GridPlacement placement;
			lock (_cellsLock)
			{
				int index = _cells.FindIndex(c => ReferenceEquals(c.Control, oldControl));
				if (index < 0)
					throw new ArgumentException("The control is not placed in this grid.", nameof(oldControl));

				placement = _cells[index].Placement;
				_cells[index] = (newControl, placement);
				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}

			oldControl.Container = null;
			newControl.Container = this;
			RebuildAndInvalidate();
		}

		/// <summary>
		/// Removes the control whose placement starts at the given cell. If no control starts at that
		/// cell, this is a no-op.
		/// </summary>
		/// <param name="row">The zero-based row index of the cell's top-left corner.</param>
		/// <param name="col">The zero-based column index of the cell's top-left corner.</param>
		public void RemoveAt(int row, int col)
		{
			IWindowControl? removed = null;
			lock (_cellsLock)
			{
				int index = _cells.FindIndex(c => c.Placement.Row == row && c.Placement.Col == col);
				if (index < 0) return;
				removed = _cells[index].Control;
				_cells.RemoveAt(index);
				_orderedCellsCache = null;
				_allOrderedCellsCache = null;
			}

			if (removed != null)
				removed.Container = null;
			RebuildAndInvalidate();
		}

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> Children
		{
			get { lock (_cellsLock) { return _cells.Where(c => c.Control != null).Select(c => c.Control!).ToList(); } }
		}

		#endregion

		#region IContainerControl Implementation

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> GetChildren() =>
			OrderedCells.Select(c => c.Control).ToList();

		#endregion

		#region Layout / Rendering

		/// <inheritdoc/>
		/// <remarks>
		/// The grid has no hard natural width before layout (column tracks resolve against available
		/// space), so this returns <c>null</c> to let the layout engine decide.
		/// </remarks>
		public override int? ContentWidth => null;

		/// <inheritdoc/>
		/// <remarks>
		/// The real measuring is performed by <see cref="GridLayout"/> once the grid is wired into the
		/// layout tree, which overrides this node's measurement. This minimal implementation returns
		/// <see cref="LayoutSize.Zero"/>.
		/// </remarks>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints) => LayoutSize.Zero;

		/// <inheritdoc/>
		/// <remarks>
		/// Cell content is painted by the children's own layout nodes in the DOM tree, not here. This
		/// method records the grid's bounds, paints the grid's own background (when
		/// <see cref="BackgroundColor"/> is set), then paints per-cell chrome (background fill and border)
		/// for every styled cell. Chrome is painted here — before the children paint — so it lands behind
		/// cell content.
		/// </remarks>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			var fgColor = ColorResolver.ResolveForeground(_foregroundColorValue, Container, defaultForeground);

			// Grid's own background is DELIBERATELY not tinted by ColorRole. Unlike PanelControl — a single
			// bordered box that fills its whole surface — the grid is a multi-cell surface whose default is
			// "no background fill, cells show through". Auto-filling the whole grid from the role would
			// break that show-through default and over-paint every cell. The role therefore drives per-cell
			// CHROME only (cell borders and the surface fill of cells that opt into chrome); see
			// PaintCellChrome. An explicit grid BackgroundColor still wins and fills as before.
			if (_backgroundColorValue != null)
			{
				var bgColor = ColorResolver.ResolveBackground(_backgroundColorValue, Container);
				for (int y = bounds.Y; y < bounds.Bottom; y++)
				{
					if (y < clipRect.Y || y >= clipRect.Bottom) continue;
					var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
					ControlRenderingHelpers.FillRect(buffer, lineRect, fgColor, bgColor);
				}
			}

			PaintCellChrome(buffer, bounds, clipRect, fgColor);
		}

		/// <summary>
		/// Paints per-cell chrome — background fill and border — for every styled cell (content-bearing and
		/// content-less styled-empty cells alike). Each cell's OUTER local rectangle is read from the shared
		/// <see cref="LayoutAlgorithm"/> (recorded during arrange), offset into absolute coordinates, and
		/// clipped before painting. Cells with neither a background nor a border are skipped.
		/// </summary>
		private void PaintCellChrome(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color fgColor)
		{
			var cells = AllOrderedCells();
			if (cells.Count == 0) return;

			GridLayout layout = LayoutAlgorithm;

			// Role-derived chrome colours, resolved once. Both return null when no role is set
			// (ColorRole.Default), so the explicit-wins fallback chains below collapse to today's behaviour.
			Color? roleBorder = ColorResolver.ColorRoleBorder(ColorRole, Container, Outline, ColorRoleState.Normal, ColorRoleMode);
			Color? roleBackground = ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, ColorRoleState.Normal, ColorRoleMode);

			// Border glyph colour: explicit grid foreground / theme foreground is the fgColor already
			// resolved by the caller; the role border (when a role is set) takes precedence over it for
			// chrome so a roled grid paints its cell borders in the role's border colour.
			Color cellBorderFg = roleBorder ?? fgColor;

			for (int i = 0; i < cells.Count; i++)
			{
				GridPlacement placement = cells[i].Placement;
				bool hasBackground = placement.Background != null;
				bool hasBorder = placement.Border != BorderStyle.None;
				if (!hasBackground && !hasBorder) continue;

				if (!layout.TryGetCellRect(placement.Row, placement.Col, out var local)) continue;

				var abs = new LayoutRect(bounds.X + local.X, bounds.Y + local.Y, local.Width, local.Height);
				if (abs.Width <= 0 || abs.Height <= 0) continue;

				// Per-cell surface fill: an explicit cell background always wins. When the cell has chrome
				// (border or background) but no explicit background AND a role is set, fill the cell interior
				// with the role surface so a roled bordered tile gets a subtle role background. This is scoped
				// to chrome cells only — plain cells keep showing through (no role fill).
				Color? cellFill = placement.Background != null
					? ColorResolver.ResolveBackground(placement.Background, Container)
					: roleBackground;

				if (cellFill.HasValue)
				{
					Color cellBg = cellFill.Value;
					for (int y = abs.Y; y < abs.Bottom; y++)
					{
						if (y < clipRect.Y || y >= clipRect.Bottom) continue;
						int rowX = Math.Max(abs.X, clipRect.X);
						int rowRight = Math.Min(abs.Right, clipRect.Right);
						if (rowRight <= rowX) continue;
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rowX, y, rowRight - rowX, 1), fgColor, cellBg);
					}
				}

				if (hasBorder)
				{
					// Border background matches the cell's fill (explicit, then role, then grid bg) so the
					// border row blends with whatever the cell interior was painted.
					var borderBg = cellFill ?? ColorResolver.ResolveBackground(_backgroundColorValue, Container);
					DrawCellBorder(buffer, abs, clipRect, placement.Border, cellBorderFg, borderBg);
				}
			}
		}

		/// <summary>
		/// Draws a one-cell-thick box border around <paramref name="rect"/> using the box-drawing characters
		/// for <paramref name="border"/>, clipped to <paramref name="clipRect"/>. Reuses the shared
		/// <see cref="Helpers.PanelBorderRenderer"/> primitives so the glyphs match panel-style chrome.
		/// </summary>
		private static void DrawCellBorder(CharacterBuffer buffer, LayoutRect rect, LayoutRect clipRect, BorderStyle border, Color borderColor, Color bgColor)
		{
			if (rect.Width < 2 || rect.Height < 2) return; // need room for at least the corners

			var box = Drawing.BoxChars.FromBorderStyle(border);

			// Top and bottom border rows.
			Helpers.PanelBorderRenderer.DrawTopBorder(buffer, rect.X, rect.Y, rect.Width, clipRect, box, borderColor, bgColor, header: null, headerAlignment: TextJustification.Left);
			Helpers.PanelBorderRenderer.DrawBottomBorder(buffer, rect.X, rect.Bottom - 1, rect.Width, clipRect, box, borderColor, bgColor);

			// Left and right verticals for the interior rows (the corners are drawn by the top/bottom rows).
			int rightX = rect.Right - 1;
			for (int y = rect.Y + 1; y < rect.Bottom - 1; y++)
			{
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;
				if (rect.X >= clipRect.X && rect.X < clipRect.Right)
					buffer.SetNarrowCell(rect.X, y, box.Vertical, borderColor, bgColor);
				if (rightX >= clipRect.X && rightX < clipRect.Right)
					buffer.SetNarrowCell(rightX, y, box.Vertical, borderColor, bgColor);
			}
		}

		#endregion

		#region IContainer Implementation

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
		public int? GetVisibleHeightForControl(IWindowControl control) =>
			ActualHeight > 0 ? ActualHeight : null;

		#endregion

		#region Container propagation

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				List<IWindowControl> snapshot;
				lock (_cellsLock) { snapshot = _cells.Where(c => c.Control != null).Select(c => c.Control!).ToList(); }
				foreach (var control in snapshot)
					control.Container = this;
			}
		}

		#endregion

		/// <summary>
		/// Drops the ordered-cells cache, forces a layout rebuild on the owning window, and invalidates.
		/// This is the common reaction to any structural change (cells added/removed/replaced, or track
		/// definitions mutated) so the change is reflected on the next render.
		/// </summary>
		private void RebuildAndInvalidate()
		{
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
		}

		/// <summary>
		/// Callback invoked by <see cref="RowDefinitions"/>/<see cref="ColumnDefinitions"/> after any
		/// mutation. Nulls the ordered-cells cache (track changes can affect auto-flow geometry) and
		/// triggers a rebuild so a live row/column change re-renders without needing another mutation.
		/// </summary>
		private void OnDefinitionsChanged()
		{
			lock (_cellsLock) { _orderedCellsCache = null; _allOrderedCellsCache = null; }
			RebuildAndInvalidate();
		}

		/// <summary>
		/// Returns the <b>content-bearing</b> cells in row-major order (sorted by row, then column),
		/// preserving insertion order for cells that share the same start cell. Content-less styled cells
		/// (a background/border on an empty cell) are excluded so the layout tree, focus traversal, and
		/// the <see cref="IGridSource.OrderedCells"/> index-correlation only ever see real children. The
		/// sorted list is cached and rebuilt only when the cell set changes, so per-frame measure/arrange
		/// reads do not re-sort.
		/// </summary>
		private List<(IWindowControl Control, GridPlacement Placement)> BuildOrderedCells()
		{
			lock (_cellsLock)
			{
				_orderedCellsCache ??= _cells
					.Where(c => c.Control != null)
					.OrderBy(c => c.Placement.Row)
					.ThenBy(c => c.Placement.Col)
					.Select(c => (c.Control!, c.Placement))
					.ToList();
				return _orderedCellsCache;
			}
		}

		/// <summary>
		/// Returns <b>every</b> cell entry — content-bearing and content-less styled cells alike — in
		/// row-major order, for the paint pass only (per-cell chrome must paint styled empty cells too).
		/// Unlike <see cref="BuildOrderedCells"/> this is never used for layout-tree correlation or focus.
		/// </summary>
		internal IReadOnlyList<(IWindowControl? Control, GridPlacement Placement)> AllOrderedCells()
		{
			lock (_cellsLock)
			{
				_allOrderedCellsCache ??= _cells
					.OrderBy(c => c.Placement.Row)
					.ThenBy(c => c.Placement.Col)
					.ToList();
				return _allOrderedCellsCache;
			}
		}

		/// <summary>
		/// Gets the shared <see cref="GridLayout"/> instance for this grid. The layout tree's grid node
		/// and <see cref="PaintDOM"/> share one instance so the paint pass can read the per-cell rectangles
		/// the arrange pass recorded (via <see cref="GridLayout.TryGetCellRect"/>). Created lazily.
		/// </summary>
		internal GridLayout LayoutAlgorithm => _layoutAlgorithm ??= new GridLayout(this);

		/// <summary>
		/// Returns a cached, row-major list of the cell controls for the hot focus paths. The projection
		/// is rebuilt only when the ordered-cell set changes (keyed on <see cref="_orderedCellsCache"/>'s
		/// reference identity), so per-frame/per-keystroke focus reads do not allocate a fresh list
		/// (CLAUDE.md rule 3). Unlike the public <see cref="GetChildren"/>, the returned list is shared
		/// and must be treated as read-only.
		/// </summary>
		internal IReadOnlyList<IWindowControl> OrderedChildren()
		{
			var ordered = BuildOrderedCells();
			lock (_cellsLock)
			{
				if (_orderedChildrenCache == null || !ReferenceEquals(_orderedChildrenSource, ordered))
				{
					_orderedChildrenSource = ordered;
					_orderedChildrenCache = ordered.Select(c => c.Control).ToList();
				}
				return _orderedChildrenCache;
			}
		}

		/// <summary>
		/// Finds the next free (unoccupied) cell scanning row-major across the given column count.
		/// A cell is occupied if any existing placement's span rectangle covers it.
		/// Callers must hold <see cref="_cellsLock"/>.
		/// </summary>
		private (int Row, int Col) FindNextFreeCell(int columnCount)
		{
			int row = 0;
			int col = 0;
			while (IsOccupied(row, col))
			{
				col++;
				if (col >= columnCount)
				{
					col = 0;
					row++;
				}
			}
			return (row, col);
		}

		/// <summary>
		/// Whether any placement's span rectangle covers the given cell. Callers must hold
		/// <see cref="_cellsLock"/>.
		/// </summary>
		private bool IsOccupied(int row, int col) =>
			_cells.Any(c =>
				row >= c.Placement.Row && row < c.Placement.Row + c.Placement.RowSpan &&
				col >= c.Placement.Col && col < c.Placement.Col + c.Placement.ColSpan);
	}
}
