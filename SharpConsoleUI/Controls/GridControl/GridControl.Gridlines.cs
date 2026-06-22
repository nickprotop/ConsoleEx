// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class GridControl
	{
		// Per-boundary gridline sets (a boundary "after index N" separates track N and N+1).
		private readonly HashSet<int> _colGridlines = new();
		private readonly HashSet<int> _rowGridlines = new();

		private bool _showColumnGridlines;
		private bool _showRowGridlines;
		private BorderStyle _gridlineStyle = BorderStyle.Single;
		private Color? _gridlineColor;

		/// <summary>Draws a vertical rule between every adjacent column. Combine with per-boundary
		/// <see cref="AddGridlineAfterColumn"/> (the two union). Enabling this bumps the column gap to at least 1.</summary>
		public bool ShowColumnGridlines { get => _showColumnGridlines; set { _showColumnGridlines = value; Container?.Invalidate(true); } }

		/// <summary>Draws a horizontal rule between every adjacent row. Bumps the row gap to at least 1.</summary>
		public bool ShowRowGridlines { get => _showRowGridlines; set { _showRowGridlines = value; Container?.Invalidate(true); } }

		/// <summary>The box-drawing style for gridlines. Default <see cref="BorderStyle.Single"/> (│ ─ ┼).</summary>
		public BorderStyle GridlineStyle { get => _gridlineStyle; set { _gridlineStyle = value; Container?.Invalidate(true); } }

		/// <summary>Explicit gridline glyph colour. Null resolves to the grid's role border colour, shaded
		/// dimmer (so a rule is lighter than a full border); set this for full border-weight colour.</summary>
		public Color? GridlineColor { get => _gridlineColor; set { _gridlineColor = value; Container?.Invalidate(true); } }

		/// <summary>Adds a vertical rule at the boundary after column <paramref name="index"/> (between it and
		/// index+1). Idempotent. Out-of-range indices are stored but never painted.</summary>
		public void AddGridlineAfterColumn(int index) { if (_colGridlines.Add(index)) Container?.Invalidate(true); }

		/// <summary>Adds a horizontal rule at the boundary after row <paramref name="index"/>.</summary>
		public void AddGridlineAfterRow(int index) { if (_rowGridlines.Add(index)) Container?.Invalidate(true); }

		/// <summary>Removes the per-boundary column gridline after <paramref name="index"/> (no effect on <see cref="ShowColumnGridlines"/>).</summary>
		public void RemoveGridlineAfterColumn(int index) { if (_colGridlines.Remove(index)) Container?.Invalidate(true); }

		/// <summary>Removes the per-boundary row gridline after <paramref name="index"/>.</summary>
		public void RemoveGridlineAfterRow(int index) { if (_rowGridlines.Remove(index)) Container?.Invalidate(true); }

		/// <summary>True if a per-boundary column gridline exists after <paramref name="index"/>.</summary>
		public bool HasGridlineAfterColumn(int index) => _colGridlines.Contains(index);

		/// <summary>True if a per-boundary row gridline exists after <paramref name="index"/>.</summary>
		public bool HasGridlineAfterRow(int index) => _rowGridlines.Contains(index);

		/// <summary>Clears all per-boundary gridlines on both axes. Does not change the Show* flags.</summary>
		public void ClearGridlines()
		{
			if (_colGridlines.Count == 0 && _rowGridlines.Count == 0) return;
			_colGridlines.Clear();
			_rowGridlines.Clear();
			Container?.Invalidate(true);
		}

		/// <summary>True when any vertical gridline should be considered for painting (grid-level or per-boundary).</summary>
		internal bool ColumnGridlinesActive => _showColumnGridlines || _colGridlines.Count > 0;

		/// <summary>True when any horizontal gridline should be considered for painting.</summary>
		internal bool RowGridlinesActive => _showRowGridlines || _rowGridlines.Count > 0;

		/// <summary>True if a vertical gridline is requested at column boundary <paramref name="n"/> (union of grid-level and per-boundary).</summary>
		private bool WantsColumnGridline(int n) => _showColumnGridlines || _colGridlines.Contains(n);

		/// <summary>True if a horizontal gridline is requested at row boundary <paramref name="n"/>.</summary>
		private bool WantsRowGridline(int n) => _showRowGridlines || _rowGridlines.Contains(n);

		/// <summary>
		/// Resolves the gridline glyph colour: an explicit <see cref="GridlineColor"/> wins (full weight),
		/// else the grid's role border colour shaded dimmer, else the grid foreground shaded dimmer. Static —
		/// gridlines do not react to focus/hover (they are permanent structure).
		/// </summary>
		private Color ResolveGridlineColor()
		{
			if (_gridlineColor.HasValue)
				return _gridlineColor.Value;

			Color? roleBorder = ColorResolver.ColorRoleBorder(ColorRole, Container, Outline, ColorRoleState.Normal, ColorRoleMode);
			Color baseColor = roleBorder ?? ForegroundColor;
			return baseColor.Shade(ControlDefaults.GridlineDimShade);
		}

		/// <summary>
		/// Paints gridlines into the track gaps. Called from PaintDOM AFTER cell chrome and BEFORE splitters.
		/// A boundary that carries a splitter is skipped here — the splitter (interactive, highlighted) owns
		/// that gap; gridlines fill every other requested boundary. Span-blocked segments are suppressed.
		/// </summary>
		internal void PaintGridlines(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
		{
			if (!ColumnGridlinesActive && !RowGridlinesActive) return;

			var (colSizes, rowSizes, colOffsets, rowOffsets, columnGap, rowGap) = LayoutAlgorithm.LastArrangeMetrics;
			if (colSizes.Length == 0 || rowSizes.Length == 0) return;

			var box = BoxChars.FromBorderStyle(_gridlineStyle);
			Color fg = ResolveGridlineColor();
			Color bg = Color.Transparent;

			// Vertical rules (between columns).
			if (ColumnGridlinesActive)
			{
				for (int n = 0; n < colSizes.Length - 1; n++)
				{
					if (!WantsColumnGridline(n)) continue;
					if (HasColumnSplitterAfter(n)) continue; // splitter wins at this boundary

					int gapX = bounds.X + GapCentreColX(colOffsets, colSizes, n, columnGap);
					int botY = bounds.Y + rowOffsets[rowSizes.Length - 1] + rowSizes[rowSizes.Length - 1];
					for (int r = 0; r < rowSizes.Length; r++)
					{
						if (ColumnBoundaryBlockedAtRow(n, r)) continue;
						int rowTop = bounds.Y + rowOffsets[r];
						int rowEndExclusive = (r < rowSizes.Length - 1)
							? bounds.Y + rowOffsets[r + 1]
							: bounds.Y + rowOffsets[r] + rowSizes[r];
						for (int y = rowTop; y < rowEndExclusive && y < botY; y++)
						{
							if (gapX < clipRect.X || gapX >= clipRect.Right || y < clipRect.Y || y >= clipRect.Bottom) continue;
							buffer.SetCell(gapX, y, new Cell(box.Vertical, fg, bg));
						}
					}
				}
			}

			// Horizontal rules (between rows).
			if (RowGridlinesActive)
			{
				for (int n = 0; n < rowSizes.Length - 1; n++)
				{
					if (!WantsRowGridline(n)) continue;
					if (HasRowSplitterAfter(n)) continue; // splitter wins

					int gapY = bounds.Y + GapCentreRowY(rowOffsets, rowSizes, n, rowGap);
					int rightX = bounds.X + colOffsets[colSizes.Length - 1] + colSizes[colSizes.Length - 1];
					for (int c = 0; c < colSizes.Length; c++)
					{
						if (RowBoundaryBlockedAtColumn(n, c)) continue;
						int colLeft = bounds.X + colOffsets[c];
						int colEndExclusive = (c < colSizes.Length - 1)
							? bounds.X + colOffsets[c + 1]
							: bounds.X + colOffsets[c] + colSizes[c];
						for (int x = colLeft; x < colEndExclusive && x < rightX; x++)
						{
							if (gapY < clipRect.Y || gapY >= clipRect.Bottom || x < clipRect.X || x >= clipRect.Right) continue;
							buffer.SetCell(x, gapY, new Cell(box.Horizontal, fg, bg));
						}
					}
				}
			}

			// Junctions (┼) where a vertical gridline crosses a horizontal gridline — only where NEITHER side
			// is a splitter boundary (a splitter crossing owns its own ╬ via PaintSplitters).
			if (ColumnGridlinesActive && RowGridlinesActive)
			{
				for (int cn = 0; cn < colSizes.Length - 1; cn++)
				{
					if (!WantsColumnGridline(cn) || HasColumnSplitterAfter(cn)) continue;
					int gapX = bounds.X + GapCentreColX(colOffsets, colSizes, cn, columnGap);
					for (int rn = 0; rn < rowSizes.Length - 1; rn++)
					{
						if (!WantsRowGridline(rn) || HasRowSplitterAfter(rn)) continue;
						int gapY = bounds.Y + GapCentreRowY(rowOffsets, rowSizes, rn, rowGap);
						if (gapX < clipRect.X || gapX >= clipRect.Right || gapY < clipRect.Y || gapY >= clipRect.Bottom) continue;
						bool up = !ColumnBoundaryBlockedAtRow(cn, rn);
						bool down = !ColumnBoundaryBlockedAtRow(cn, rn + 1);
						bool left = !RowBoundaryBlockedAtColumn(rn, cn);
						bool right = !RowBoundaryBlockedAtColumn(rn, cn + 1);
						char junction = ResolveJunctionGlyph(box, up, down, left, right);
						buffer.SetCell(gapX, gapY, new Cell(junction, fg, bg));
					}
				}
			}
		}

		/// <summary>
		/// Picks the gridline junction glyph from which of the four arms carry a line at a crossing:
		/// all four → ┼; a single missing arm → the matching tee (┬┴├┤); two missing adjacent arms → the
		/// matching corner (┌┐└┘). Fewer than two arms is degenerate (a crossing only exists where both a
		/// column and a row line are requested) and defaults to ┼.
		/// </summary>
		private static char ResolveJunctionGlyph(BoxChars box, bool up, bool down, bool left, bool right)
		{
			if (up && down && left && right) return box.Cross;
			if (!up && down && left && right) return box.TopTee;
			if (up && !down && left && right) return box.BottomTee;
			if (up && down && !left && right) return box.LeftTee;
			if (up && down && left && !right) return box.RightTee;
			if (!up && down && !left && right) return box.TopLeft;
			if (!up && down && left && !right) return box.TopRight;
			if (up && !down && !left && right) return box.BottomLeft;
			if (up && !down && left && !right) return box.BottomRight;
			return box.Cross; // degenerate (≤1 arm) — defensive
		}

		/// <summary>Test-only: the colour gridlines currently paint with.</summary>
		internal Color GetGridlineColorForTest() => ResolveGridlineColor();
	}
}
