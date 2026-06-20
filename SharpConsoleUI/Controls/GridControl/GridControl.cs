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
	/// </remarks>
	public partial class GridControl : BaseControl, IContainer, IContainerControl, IControlHost, IColorRoleableControl, IGridSource
	{
		/// <summary>Default number of columns assumed when no column definitions are supplied.</summary>
		private const int DefaultColumnCount = 1;

		private readonly List<(IWindowControl Control, GridPlacement Placement)> _cells = new();
		private readonly object _cellsLock = new();
		private List<(IWindowControl Control, GridPlacement Placement)>? _orderedCellsCache;

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
		/// </summary>
		public List<GridLength> RowDefinitions { get; } = new();

		/// <summary>
		/// Gets the column track definitions, left to right. Add a <see cref="GridLength"/> per column,
		/// e.g. <c>grid.ColumnDefinitions.Add(GridLength.Star(1));</c>.
		/// </summary>
		public List<GridLength> ColumnDefinitions { get; } = new();

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
		IReadOnlyList<GridLength> IGridSource.RowDefinitions => RowDefinitions;

		/// <inheritdoc/>
		IReadOnlyList<GridLength> IGridSource.ColumnDefinitions => ColumnDefinitions;

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
			}
			control.Container = this;
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
			return this;
		}

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
			}
			control.Container = this;
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
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
				removed = true;
			}

			if (removed)
				control.Container = null;
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
		}

		/// <summary>Removes all child controls from the grid.</summary>
		public void ClearControls()
		{
			List<(IWindowControl Control, GridPlacement Placement)> snapshot;
			lock (_cellsLock)
			{
				snapshot = new List<(IWindowControl Control, GridPlacement Placement)>(_cells);
				_cells.Clear();
				_orderedCellsCache = null;
			}
			foreach (var (control, _) in snapshot)
				control.Container = null;
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
		}

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> Children
		{
			get { lock (_cellsLock) { return _cells.Select(c => c.Control).ToList(); } }
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
		/// Children are painted by their own layout nodes in the DOM tree, not here. This method only
		/// records the grid's bounds and paints its own background (when <see cref="BackgroundColor"/>
		/// is set).
		/// </remarks>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			// No background requested — let cells show through to whatever is behind the grid.
			if (_backgroundColorValue == null)
				return;

			var bgColor = ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			var fgColor = ColorResolver.ResolveForeground(_foregroundColorValue, Container, defaultForeground);

			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;
				var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
				ControlRenderingHelpers.FillRect(buffer, lineRect, fgColor, bgColor);
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
				lock (_cellsLock) { snapshot = _cells.Select(c => c.Control).ToList(); }
				foreach (var control in snapshot)
					control.Container = this;
			}
		}

		#endregion

		/// <summary>
		/// Returns the cells in row-major order (sorted by row, then column), preserving insertion
		/// order for cells that share the same start cell. The sorted list is cached and rebuilt only
		/// when the cell set changes, so per-frame measure/arrange reads do not re-sort.
		/// </summary>
		private List<(IWindowControl Control, GridPlacement Placement)> BuildOrderedCells()
		{
			lock (_cellsLock)
			{
				_orderedCellsCache ??= _cells
					.OrderBy(c => c.Placement.Row)
					.ThenBy(c => c.Placement.Col)
					.ToList();
				return _orderedCellsCache;
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
