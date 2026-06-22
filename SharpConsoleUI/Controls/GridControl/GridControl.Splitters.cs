// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class GridControl
	{
		private readonly List<GridSplitter> _splitters = new();

		// ---- Public API ----

		/// <summary>
		/// Declares a draggable COLUMN boundary in the gap after column <paramref name="afterIndex"/>
		/// (between track N and N+1). If no column gap exists, a 1-cell gap is auto-inserted so the handle
		/// has somewhere to render. Idempotent: re-adding the same boundary is a no-op.
		/// </summary>
		/// <param name="afterIndex">The zero-based column index the splitter sits after.</param>
		/// <returns>This grid, to allow fluent chaining.</returns>
		public GridControl AddColumnSplitterAfter(int afterIndex)
		{
			if (!_splitters.Any(s => s.Matches(GridSplitterOrientation.Column, afterIndex)))
			{
				_splitters.Add(new GridSplitter(GridSplitterOrientation.Column, afterIndex) { OwnerGrid = this });
				Container?.Invalidate(true);
			}
			return this;
		}

		/// <summary>
		/// Declares a draggable ROW boundary in the gap after row <paramref name="afterIndex"/>
		/// (between track N and N+1). If no row gap exists, a 1-cell gap is auto-inserted so the handle
		/// has somewhere to render. Idempotent: re-adding the same boundary is a no-op.
		/// </summary>
		/// <param name="afterIndex">The zero-based row index the splitter sits after.</param>
		/// <returns>This grid, to allow fluent chaining.</returns>
		public GridControl AddRowSplitterAfter(int afterIndex)
		{
			if (!_splitters.Any(s => s.Matches(GridSplitterOrientation.Row, afterIndex)))
			{
				_splitters.Add(new GridSplitter(GridSplitterOrientation.Row, afterIndex) { OwnerGrid = this });
				Container?.Invalidate(true);
			}
			return this;
		}

		/// <summary>Returns whether a column splitter is declared after <paramref name="afterIndex"/>.</summary>
		public bool HasColumnSplitterAfter(int afterIndex) => _splitters.Any(s => s.Matches(GridSplitterOrientation.Column, afterIndex));

		/// <summary>Returns whether a row splitter is declared after <paramref name="afterIndex"/>.</summary>
		public bool HasRowSplitterAfter(int afterIndex) => _splitters.Any(s => s.Matches(GridSplitterOrientation.Row, afterIndex));

		/// <summary>Removes the column splitter after <paramref name="afterIndex"/>, if present.</summary>
		/// <returns>This grid, to allow fluent chaining.</returns>
		public GridControl RemoveColumnSplitterAfter(int afterIndex) { _splitters.RemoveAll(s => s.Matches(GridSplitterOrientation.Column, afterIndex)); Container?.Invalidate(true); return this; }

		/// <summary>Removes the row splitter after <paramref name="afterIndex"/>, if present.</summary>
		/// <returns>This grid, to allow fluent chaining.</returns>
		public GridControl RemoveRowSplitterAfter(int afterIndex) { _splitters.RemoveAll(s => s.Matches(GridSplitterOrientation.Row, afterIndex)); Container?.Invalidate(true); return this; }

		/// <summary>Removes every declared splitter from this grid.</summary>
		public void ClearSplitters() { _splitters.Clear(); Container?.Invalidate(true); }

		// ---- Keyboard nudge + real splitter focus (via FocusManager) ----

		/// <summary>Returns true if <paramref name="s"/> is the control currently focused by this grid's window.</summary>
		internal bool IsSplitterFocused(GridSplitter s)
			=> ReferenceEquals(this.GetParentWindow()?.FocusManager.FocusedControl, s);

		/// <summary>The splitter (if any) currently focused via the window's FocusManager and owned by this grid.</summary>
		private GridSplitter? FocusedSplitter
		{
			get
			{
				var s = this.GetParentWindow()?.FocusManager.FocusedControl as GridSplitter;
				return (s != null && ReferenceEquals(s.OwnerGrid, this)) ? s : null;
			}
		}

		/// <summary>Makes a column splitter the focused keyboard target (for arrow-key resize).</summary>
		public void FocusColumnSplitter(int afterIndex)
			=> FocusSplitter(_splitters.FirstOrDefault(s => s.Matches(GridSplitterOrientation.Column, afterIndex)));

		/// <summary>Makes a row splitter the focused keyboard target.</summary>
		public void FocusRowSplitter(int afterIndex)
			=> FocusSplitter(_splitters.FirstOrDefault(s => s.Matches(GridSplitterOrientation.Row, afterIndex)));

		/// <summary>Clears splitter focus (arrow keys resume normal cell navigation) when a splitter is focused.</summary>
		public void ClearActiveSplitter()
		{
			if (FocusedSplitter != null)
				this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Programmatic);
		}

		private void FocusSplitter(GridSplitter? s)
		{
			if (s == null) return;
			this.GetParentWindow()?.FocusManager.SetFocus(s, FocusReason.Keyboard);
			Container?.Invalidate(true);
		}

		/// <summary>Handle an arrow key for a SPECIFIC splitter <paramref name="s"/>. Returns true if consumed.
		/// Called directly by <see cref="GridSplitter.ProcessKey"/> (real window key path, where the splitter is
		/// the focused IInteractiveControl) and indirectly by <see cref="TryHandleSplitterKey"/> (grid ProcessKey path).</summary>
		internal bool HandleSplitterKey(GridSplitter s, System.ConsoleKeyInfo key)
		{
			int step = key.Modifiers.HasFlag(System.ConsoleModifiers.Shift)
				? ControlDefaults.GridSplitterKeyboardLargeStep
				: ControlDefaults.GridSplitterKeyboardStep;
			int delta = 0;
			if (s.Orientation == GridSplitterOrientation.Column)
			{
				if (key.Key == System.ConsoleKey.LeftArrow) delta = -step;
				else if (key.Key == System.ConsoleKey.RightArrow) delta = step;
			}
			else
			{
				if (key.Key == System.ConsoleKey.UpArrow) delta = -step;
				else if (key.Key == System.ConsoleKey.DownArrow) delta = step;
			}
			if (delta == 0) return false;
			ResizeSplitter(s, delta);
			Container?.Invalidate(true);
			return true;
		}

		/// <summary>Handle an arrow key for the focused splitter. Returns true if consumed.</summary>
		internal bool TryHandleSplitterKey(System.ConsoleKeyInfo key)
		{
			var s = FocusedSplitter;
			return s != null && HandleSplitterKey(s, key);
		}

		/// <summary>Appends every not-yet-emitted COLUMN splitter whose boundary index is &lt;= <paramref name="maxBoundary"/>
		/// to the focusable Tab-stop list, in ascending boundary order, recording each in <paramref name="emitted"/>.
		/// Used during the row-0 pass to interleave column splitters right after the cell that reaches their boundary.</summary>
		internal void AppendColumnSplittersUpTo(List<IFocusableControl> result, int maxBoundary, HashSet<int> emitted)
		{
			foreach (var s in _splitters
				.Where(s => s.Orientation == GridSplitterOrientation.Column && IsSplitterTabStop(s)
					&& s.AfterIndex <= maxBoundary && !emitted.Contains(s.AfterIndex))
				.OrderBy(s => s.AfterIndex))
			{
				result.Add(s);
				emitted.Add(s.AfterIndex);
			}
		}

		/// <summary>Appends any COLUMN splitters not already emitted during the row-0 pass (ascending order),
		/// so every valid column splitter still appears exactly once even when its boundary lies past the last
		/// row-0 cell or no row-0 cells exist.</summary>
		internal void AppendRemainingColumnSplitters(List<IFocusableControl> result, HashSet<int> emitted)
		{
			foreach (var s in _splitters
				.Where(s => s.Orientation == GridSplitterOrientation.Column && IsSplitterTabStop(s)
					&& !emitted.Contains(s.AfterIndex))
				.OrderBy(s => s.AfterIndex))
			{
				result.Add(s);
				emitted.Add(s.AfterIndex);
			}
		}

		/// <summary>Appends the ROW splitter after boundary <paramref name="afterIndex"/> (if declared, visible
		/// and laid out) to the focusable Tab-stop list — once, after that row's cells.</summary>
		internal void AppendRowSplitterAfter(List<IFocusableControl> result, int afterIndex)
		{
			var s = _splitters.FirstOrDefault(s => s.Orientation == GridSplitterOrientation.Row
				&& s.AfterIndex == afterIndex && IsSplitterTabStop(s));
			if (s != null) result.Add(s);
		}

		// A splitter is a Tab stop when visible AND it has a painted handle (non-empty Bounds), which excludes
		// out-of-range / not-yet-laid-out handles. Programmatic FocusColumnSplitter does NOT use this gate.
		private static bool IsSplitterTabStop(GridSplitter s) => s.Visible && !s.Bounds.IsEmpty;

		internal bool HasAnyColumnSplitter => _splitters.Any(s => s.Orientation == GridSplitterOrientation.Column);
		internal bool HasAnyRowSplitter => _splitters.Any(s => s.Orientation == GridSplitterOrientation.Row);

		/// <summary>The effective column gap the layout uses: bumped to at least 1 when a column splitter
		/// exists so the handle has a gap to render in; otherwise the raw <see cref="ColumnGap"/>.</summary>
		internal int EffectiveColumnGap => (HasAnyColumnSplitter || ColumnGridlinesActive) ? System.Math.Max(1, ColumnGap) : ColumnGap;

		/// <summary>The effective row gap the layout uses: bumped to at least 1 when a row splitter
		/// exists so the handle has a gap to render in; otherwise the raw <see cref="RowGap"/>.</summary>
		internal int EffectiveRowGap => (HasAnyRowSplitter || RowGridlinesActive) ? System.Math.Max(1, RowGap) : RowGap;

		// AUTO-GAP SEAM: layout reads gaps via IGridSource; return effective (auto-gapped) values there.
		// The public ColumnGap/RowGap stay the raw user-set values; the layout sees the bumped ones.
		int IGridSource.ColumnGap => EffectiveColumnGap;
		int IGridSource.RowGap => EffectiveRowGap;

		// ---- Splitter colors (grid-level; null = theme/role default) ----
		private Color? _splitterColor;
		private Color? _splitterFocusedBackground;
		private Color? _splitterFocusedForeground;
		private Color? _splitterDraggingBackground;
		private Color? _splitterDraggingForeground;

		/// <summary>Idle splitter glyph colour. Null falls back to the role border colour, then the grid foreground.</summary>
		public Color? SplitterColor { get => _splitterColor; set { _splitterColor = value; Container?.Invalidate(true); } }

		/// <summary>Background highlight when a splitter is focused or hovered. Null uses the theme button-focused background.</summary>
		public Color? SplitterFocusedBackground { get => _splitterFocusedBackground; set { _splitterFocusedBackground = value; Container?.Invalidate(true); } }

		/// <summary>Glyph colour when a splitter is focused or hovered. Null uses the theme button-focused foreground.</summary>
		public Color? SplitterFocusedForeground { get => _splitterFocusedForeground; set { _splitterFocusedForeground = value; Container?.Invalidate(true); } }

		/// <summary>Background highlight while a splitter is being dragged. Null uses the theme button-focused background.</summary>
		public Color? SplitterDraggingBackground { get => _splitterDraggingBackground; set { _splitterDraggingBackground = value; Container?.Invalidate(true); } }

		/// <summary>Glyph colour while a splitter is being dragged. Null uses the theme button-focused foreground.</summary>
		public Color? SplitterDraggingForeground { get => _splitterDraggingForeground; set { _splitterDraggingForeground = value; Container?.Invalidate(true); } }

		// Splitter colours are fully THEME-DRIVEN and follow the grid's ColorRole — nothing is hardcoded.
		// The highlight is FOREGROUND-ONLY: the BACKGROUND is the SAME in every state (idle / focused / hover /
		// dragging) — only the glyph (║ / ═) colour brightens to signal focus/drag. No background swap, no inversion.
		//   * Background (all states): the idle background — Transparent by default; the public Splitter*Background
		//     properties still let a caller opt into a fixed fill, but it does NOT change between states.
		//   * Idle foreground   : the role border colour SHADED dimmer (Shade 0.25), so the resting handle reads
		//     as a quiet line in the grid's own hue.
		//   * Focused/dragging  : the FULL-BRIGHT role border (its Focused state is already tinted brighter), giving
		//     a clear dim→bright highlight on the SAME hue. When the grid has no role, idle falls to the grid
		//     foreground and focused to the theme accent (ButtonFocusedBackgroundColor — the bright accent colour
		//     itself, visible on every theme). We deliberately do NOT use ButtonFocusedForegroundColor: on most
		//     themes that is the dark text meant to sit ON the accent fill, so as a glyph on a transparent
		//     background it was invisible (only ModernGray's bright-cyan value showed it) — the original
		//     "dark on every theme except ModernGray" bug.
		// The public Splitter* overrides always win when set (caller opt-out of the theme defaults).
		private (Color fg, Color bg) ResolveSplitterColors(GridSplitter s)
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;

			// The background is constant across states (caller may pin it via either background override —
			// they are treated as one constant fill since the background never changes between states;
			// otherwise Transparent).
			Color bg = _splitterFocusedBackground ?? _splitterDraggingBackground ?? Color.Transparent;

			if (s.IsDragging || s.HasFocus || s.IsHovered)
			{
				Color hiFg = (s.IsDragging ? _splitterDraggingForeground : _splitterFocusedForeground)
					?? ColorResolver.ColorRoleBorder(ColorRole, Container, Outline, ColorRoleState.Focused, ColorRoleMode)
					?? theme?.ButtonFocusedBackgroundColor   // the theme accent colour itself — bright on every theme
					?? ForegroundColor;
				return (hiFg, bg);
			}

			// Idle: the role border SHADED dimmer (same hue, quieter), or the grid foreground when no role.
			Color? roleBorder = ColorResolver.ColorRoleBorder(ColorRole, Container, Outline, ColorRoleState.Normal, ColorRoleMode);
			Color idleFg = _splitterColor
				?? (roleBorder.HasValue ? roleBorder.Value.Shade(ControlDefaults.GridSplitterIdleShade) : ForegroundColor);
			return (idleFg, bg);
		}

		/// <summary>True when the splitter is in a highlighted state (focused / hovered / dragging) — the glyph
		/// is drawn brighter AND bold so focus reads even when the brighten alone is subtle.</summary>
		private static bool IsSplitterHighlighted(GridSplitter s) => s.IsDragging || s.HasFocus || s.IsHovered;

		/// <summary>Writes one splitter glyph cell with the resolved colours and, when highlighted, a Bold
		/// decoration. Single writer for all three paint sites (column run, row run, crossing) — CLAUDE.md rule 1.</summary>
		private void WriteSplitterGlyph(CharacterBuffer buffer, int x, int y, char glyph, GridSplitter s, Color fg, Color bg)
		{
			var deco = IsSplitterHighlighted(s) ? TextDecoration.Bold : TextDecoration.None;
			buffer.SetCell(x, y, new Cell(glyph, fg, bg, deco));
		}

		// ---- Paint each splitter handle into its gap. Call from PaintDOM AFTER cells. ----
		internal void PaintSplitters(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
		{
			if (_splitters.Count == 0) return;
			var (colSizes, rowSizes, colOffsets, rowOffsets, _, _) = LayoutAlgorithm.LastArrangeMetrics;
			if (colSizes.Length == 0 || rowSizes.Length == 0) return;

			foreach (var s in _splitters)
			{
				if (s.Orientation == GridSplitterOrientation.Column)
					PaintColumnSplitter(buffer, bounds, clipRect, s, colSizes, rowSizes, colOffsets, rowOffsets);
				else
					PaintRowSplitter(buffer, bounds, clipRect, s, colSizes, rowSizes, colOffsets, rowOffsets);
			}

			// FIX #2: draw a junction glyph where a column splitter crosses a row splitter. Done after the
			// straight runs so it overwrites whichever line was last-writer at the crossing cell. Only draw at
			// crossings where NEITHER line was span-skipped (a cell spanning the boundary owns that cell).
			PaintSplitterCrossings(buffer, bounds, clipRect, colSizes, rowSizes, colOffsets, rowOffsets);
		}

		// FIX #3: a COLUMN splitter "after N" must NOT draw its glyph (║) at any row covered by a cell that col-spans
		// across the column-boundary N — that cell occupies the boundary and its own content/border owns it.
		private void PaintColumnSplitter(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, GridSplitter s,
			int[] colSizes, int[] rowSizes, int[] colOffsets, int[] rowOffsets)
		{
			if (s.AfterIndex < 0 || s.AfterIndex >= colSizes.Length - 1) { s.Bounds = Rectangle.Empty; return; }
			int n = s.AfterIndex;
			int gapX = bounds.X + colOffsets[n] + colSizes[n];
			int topY = bounds.Y + rowOffsets[0];
			int botY = bounds.Y + rowOffsets[rowSizes.Length - 1] + rowSizes[rowSizes.Length - 1];
			var (fg, bg) = ResolveSplitterColors(s);

			bool drewAny = false;
			for (int r = 0; r < rowSizes.Length; r++)
			{
				bool blocked = ColumnBoundaryBlockedAtRow(n, r);
				// Draw the column glyph (║) across row r's y-range, and (when not the last row) across the row gap after it.
				int rowTop = bounds.Y + rowOffsets[r];
				int rowEndExclusive = (r < rowSizes.Length - 1)
					? bounds.Y + rowOffsets[r + 1]                  // include the row gap after r
					: bounds.Y + rowOffsets[r] + rowSizes[r];        // last row: no trailing gap
				if (blocked) continue;
				drewAny = true;
				for (int y = rowTop; y < rowEndExclusive && y < botY; y++)
				{
					if (gapX < clipRect.X || gapX >= clipRect.Right || y < clipRect.Y || y >= clipRect.Bottom) continue;
					WriteSplitterGlyph(buffer, gapX, y, ControlDefaults.GridColumnSplitterGlyph, s, fg, bg);
				}
			}

			// A splitter blocked across EVERY row draws no glyph at all — it is fully hidden behind a
			// col-spanning cell. Empty its Bounds so it is neither a Tab stop (IsSplitterTabStop) nor a
			// hit-target; an invisible handle must not be focusable or draggable.
			if (!drewAny) { s.Bounds = Rectangle.Empty; return; }

			// Record CONTROL-RELATIVE bounds (offsets only, no bounds.X/Y) for hit-testing. Kept as the FULL
			// boundary span even where the line was span-skipped — the whole boundary stays draggable.
			int relColX = colOffsets[n] + colSizes[n];
			s.Bounds = new Rectangle(relColX, rowOffsets[0], 1, System.Math.Max(0, botY - topY));
		}

		// FIX #3: a ROW splitter "after N" must NOT draw its glyph (═) in any column covered by a cell that row-spans
		// across the row-boundary N.
		private void PaintRowSplitter(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, GridSplitter s,
			int[] colSizes, int[] rowSizes, int[] colOffsets, int[] rowOffsets)
		{
			if (s.AfterIndex < 0 || s.AfterIndex >= rowSizes.Length - 1) { s.Bounds = Rectangle.Empty; return; }
			int n = s.AfterIndex;
			int gapY = bounds.Y + rowOffsets[n] + rowSizes[n];
			int leftX = bounds.X + colOffsets[0];
			int rightX = bounds.X + colOffsets[colSizes.Length - 1] + colSizes[colSizes.Length - 1];
			var (fg, bg) = ResolveSplitterColors(s);

			bool drewAny = false;
			for (int c = 0; c < colSizes.Length; c++)
			{
				bool blocked = RowBoundaryBlockedAtColumn(n, c);
				int colLeft = bounds.X + colOffsets[c];
				int colEndExclusive = (c < colSizes.Length - 1)
					? bounds.X + colOffsets[c + 1]                  // include the column gap after c
					: bounds.X + colOffsets[c] + colSizes[c];        // last column: no trailing gap
				if (blocked) continue;
				drewAny = true;
				for (int x = colLeft; x < colEndExclusive && x < rightX; x++)
				{
					if (gapY < clipRect.Y || gapY >= clipRect.Bottom || x < clipRect.X || x >= clipRect.Right) continue;
					WriteSplitterGlyph(buffer, x, gapY, ControlDefaults.GridRowSplitterGlyph, s, fg, bg);
				}
			}

			// A splitter blocked across EVERY column draws no glyph at all — fully hidden behind a row-spanning
			// cell. Empty its Bounds so it is neither a Tab stop nor a hit-target.
			if (!drewAny) { s.Bounds = Rectangle.Empty; return; }

			// Record CONTROL-RELATIVE bounds (offsets only, no bounds.X/Y) — FULL boundary, draggable everywhere.
			int relRowY = rowOffsets[n] + rowSizes[n];
			s.Bounds = new Rectangle(colOffsets[0], relRowY, System.Math.Max(0, rightX - leftX), 1);
		}

		// A ROW boundary N is blocked at column c when some cell ROW-spans across N (Row<=N && Row+RowSpan-1>N) AND covers column c.
		private bool RowBoundaryBlockedAtColumn(int boundaryRow, int col)
		{
			foreach (var (_, p) in OrderedCells)
			{
				if (p.Row <= boundaryRow && p.Row + p.RowSpan - 1 > boundaryRow   // spans row-boundary N
					&& p.Col <= col && col < p.Col + p.ColSpan)                    // covers column col
					return true;
			}
			return false;
		}

		// A COLUMN boundary N is blocked at row r when some cell COL-spans across N (Col<=N && Col+ColSpan-1>N) AND covers row r.
		private bool ColumnBoundaryBlockedAtRow(int boundaryCol, int row)
		{
			foreach (var (_, p) in OrderedCells)
			{
				if (p.Col <= boundaryCol && p.Col + p.ColSpan - 1 > boundaryCol   // spans col-boundary N
					&& p.Row <= row && row < p.Row + p.RowSpan)                    // covers row row
					return true;
			}
			return false;
		}

		// FIX #2: write the '╬' junction at every (column-splitter, row-splitter) crossing that is drawn
		// (i.e. neither line was span-skipped at that crossing). Uses the row splitter's resolved colour.
		private void PaintSplitterCrossings(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			int[] colSizes, int[] rowSizes, int[] colOffsets, int[] rowOffsets)
		{
			foreach (var col in _splitters)
			{
				if (col.Orientation != GridSplitterOrientation.Column) continue;
				if (col.AfterIndex < 0 || col.AfterIndex >= colSizes.Length - 1) continue;
				int gapX = bounds.X + colOffsets[col.AfterIndex] + colSizes[col.AfterIndex];

				foreach (var row in _splitters)
				{
					if (row.Orientation != GridSplitterOrientation.Row) continue;
					if (row.AfterIndex < 0 || row.AfterIndex >= rowSizes.Length - 1) continue;
					int gapY = bounds.Y + rowOffsets[row.AfterIndex] + rowSizes[row.AfterIndex];

					// The crossing cell sits in the column gap after col.AfterIndex and the row gap after
					// row.AfterIndex. A spanning cell only ever covers track cells, never the gap rows/cols,
					// so a real crossing in the gap is never span-blocked — draw the junction.
					if (gapX < clipRect.X || gapX >= clipRect.Right || gapY < clipRect.Y || gapY >= clipRect.Bottom) continue;
					// The junction follows whichever crossing handle is highlighted (so it bolds/brightens with the
					// active drag/focus), else the row handle's idle colour.
					var active = IsSplitterHighlighted(col) ? col : row;
					var (fg, bg) = ResolveSplitterColors(active);
					WriteSplitterGlyph(buffer, gapX, gapY, ControlDefaults.GridSplitterCrossGlyph, active, fg, bg);
				}
			}
		}

		// ---- Mouse drag + capture / no-focus-leak ----

		private GridSplitter? _activeDragSplitter;
		private int _lastDragWindowCoord;

		/// <summary>
		/// Handles a mouse event for splitter drag, replicating SplitterControl's capture technique:
		/// returning <c>true</c> on Button1Pressed over a handle makes the window dispatcher capture this grid
		/// and route the whole press→drag→release here (bypassing hit-test), so a drag never bleeds focus/clicks
		/// into the cells under the cursor. While a drag is active, ALL frames are consumed without falling
		/// through to cell routing. Returns <c>false</c> only when no splitter is involved (so normal cell
		/// routing proceeds). Uses <c>args.Position</c> (control-relative) for hit-testing and
		/// <c>args.WindowPosition</c> for the resize delta (the handle moves as it resizes, so its own
		/// position is unreliable).
		/// </summary>
		internal bool TryHandleSplitterMouse(MouseEventArgs args)
		{
			// Ignore synthetic enter/leave so they don't start or perturb a drag.
			if (args.HasAnyFlag(MouseFlags.MouseEnter, MouseFlags.MouseLeave))
				return false;

			int WinCoordFor(GridSplitter s) =>
				s.Orientation == GridSplitterOrientation.Column ? args.WindowPosition.X : args.WindowPosition.Y;

			if (_activeDragSplitter != null)
			{
				if (args.HasFlag(MouseFlags.Button1Released))
				{
					_activeDragSplitter.IsDragging = false;
					_activeDragSplitter = null;
					Container?.Invalidate(true);
					args.Handled = true;
					return true;
				}
				if (args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
				{
					int current = WinCoordFor(_activeDragSplitter);
					int delta = current - _lastDragWindowCoord;
					if (delta != 0 && ResizeSplitter(_activeDragSplitter, delta))
						_lastDragWindowCoord = current;
					Container?.Invalidate(true);
					args.Handled = true;
					return true;
				}
				// Any other frame during an active drag is still consumed (no cell fall-through).
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				var hit = _splitters.FirstOrDefault(s => HandleHitTest(s, args.Position));
				if (hit != null)
				{
					_activeDragSplitter = hit;
					hit.IsDragging = true;
					// Pressing a handle focuses the splitter itself (it IS focusable now), so it becomes the
					// keyboard target and shows the focused highlight. Clicking a cell/empty later moves focus
					// off it via normal focus handling, clearing the highlight automatically.
					this.GetParentWindow()?.FocusManager.SetFocus(hit, FocusReason.Mouse);
					_lastDragWindowCoord = WinCoordFor(hit);
					Container?.Invalidate(true);
					args.Handled = true;
					return true;
				}
			}
			return false;
		}

		private static bool HandleHitTest(GridSplitter s, Point controlRelative)
		{
			if (s.Bounds.IsEmpty) return false;
			return controlRelative.X >= s.Bounds.X && controlRelative.X < s.Bounds.Right
				&& controlRelative.Y >= s.Bounds.Y && controlRelative.Y < s.Bounds.Bottom;
		}

		/// <summary>Applies a cell <paramref name="deltaCells"/> resize to the two tracks adjacent to splitter
		/// <paramref name="s"/> via <see cref="GridSplitterResize.ApplyResize"/>. Mutating a definition list
		/// entry auto-invalidates layout through the GridDefinitionList changed callback.</summary>
		internal bool ResizeSplitter(GridSplitter s, int deltaCells)
		{
			var (colSizes, rowSizes, _, _, _, _) = LayoutAlgorithm.LastArrangeMetrics;
			if (s.Orientation == GridSplitterOrientation.Column)
			{
				int i = s.AfterIndex;
				if (i < 0 || i >= ColumnDefinitions.Count - 1 || i >= colSizes.Length - 1) return false;
				var (a, b) = GridSplitterResize.ApplyResize(
					ColumnDefinitions[i], ColumnDefinitions[i + 1], deltaCells, colSizes[i], colSizes[i + 1]);
				ColumnDefinitions[i] = a;
				ColumnDefinitions[i + 1] = b;
			}
			else
			{
				int i = s.AfterIndex;
				if (i < 0 || i >= RowDefinitions.Count - 1 || i >= rowSizes.Length - 1) return false;
				var (a, b) = GridSplitterResize.ApplyResize(
					RowDefinitions[i], RowDefinitions[i + 1], deltaCells, rowSizes[i], rowSizes[i + 1]);
				RowDefinitions[i] = a;
				RowDefinitions[i + 1] = b;
			}
			return true;
		}

		/// <summary>Test-only: absolute screen X of a column splitter handle (window border + grid origin + handle).</summary>
		internal int GetColumnSplitterScreenX(int afterIndex, Window window)
		{
			var s = _splitters.FirstOrDefault(x => x.Matches(GridSplitterOrientation.Column, afterIndex));
			return s == null ? -1 : window.Left + 1 + ActualX + s.Bounds.X;
		}

		/// <summary>Test-only: absolute screen Y of a row splitter handle.</summary>
		internal int GetRowSplitterScreenY(int afterIndex, Window window)
		{
			var s = _splitters.FirstOrDefault(x => x.Matches(GridSplitterOrientation.Row, afterIndex));
			return s == null ? -1 : window.Top + 1 + ActualY + s.Bounds.Y;
		}

		internal GridSplitter GetColumnSplitterForTest(int afterIndex)
			=> _splitters.First(s => s.Matches(GridSplitterOrientation.Column, afterIndex));

		internal GridSplitter GetRowSplitterForTest(int afterIndex)
			=> _splitters.First(s => s.Matches(GridSplitterOrientation.Row, afterIndex));

		/// <summary>Test-only: the (fg, bg) the given splitter would paint with in its current state.</summary>
		internal (Color fg, Color bg) GetSplitterColorsForTest(GridSplitter s) => ResolveSplitterColors(s);

		internal int GetColumnArrangedSizeForTest(int index)
		{
			var m = LayoutAlgorithm.LastArrangeMetrics;
			return index < m.ColSizes.Length ? m.ColSizes[index] : -1;
		}

		internal int GetRowArrangedSizeForTest(int index)
		{
			var m = LayoutAlgorithm.LastArrangeMetrics;
			return index < m.RowSizes.Length ? m.RowSizes[index] : -1;
		}
	}
}
