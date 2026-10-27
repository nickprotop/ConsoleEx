// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Shows a grid of cells that something else painted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A window onto a surface, which may be larger than the window in either
	/// direction. The surface is already cells: this control does no parsing,
	/// no wrapping and no measuring of text, so it does not care how the cells
	/// arrived - a debugger reading another application over a socket, a
	/// recorded session played back, a capture loaded from a file.
	/// </para>
	/// <para>
	/// Cells are blitted whole, so colour, decorations, combining marks and the
	/// continuation cell of a wide glyph all survive. Losing that last one is
	/// how a remote screen goes crooked: the glyph paints in one column and
	/// everything after it shifts left.
	/// </para>
	/// </remarks>
	public class SurfaceViewControl : BaseControl
	{
		private IReadOnlyList<IReadOnlyList<Cell>> _rows = [];
		private int _scrollX;
		private int _scrollY;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;

		/// <summary>The colour behind the surface, where the surface does not reach.</summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		/// <summary>The colour of the padding around the surface.</summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;
			set => SetProperty(ref _foregroundColorValue, value);
		}

		/// <summary>Columns in the surface, which is its widest row.</summary>
		public int SurfaceWidth { get; private set; }

		/// <summary>Rows in the surface.</summary>
		public int SurfaceHeight => _rows.Count;

		/// <inheritdoc/>
		/// <remarks>
		/// The surface's own width, so a scrolling parent can offer a viewport
		/// narrower than the content and know there is more to the right.
		/// </remarks>
		public override int? ContentWidth => SurfaceWidth;

		/// <summary>
		/// The first surface column shown, clamped to the surface.
		/// </summary>
		/// <remarks>
		/// Clamped rather than rejected: scrolling past the end shows the end.
		/// A pane that scrolled into blankness would read as the application
		/// having died.
		/// </remarks>
		public int ScrollX
		{
			get => _scrollX;
			set => SetProperty(ref _scrollX, value, v => Math.Clamp(v, 0, Math.Max(0, SurfaceWidth - 1)));
		}

		/// <summary>The first surface row shown, clamped to the surface.</summary>
		public int ScrollY
		{
			get => _scrollY;
			set => SetProperty(ref _scrollY, value, v => Math.Clamp(v, 0, Math.Max(0, SurfaceHeight - 1)));
		}

		/// <summary>
		/// Replaces what is shown.
		/// </summary>
		/// <param name="rows">The surface, one list of cells per row.</param>
		/// <remarks>
		/// Rows need not be the same length; the surface is as wide as its
		/// widest. Scroll positions are re-clamped, so a surface that shrinks
		/// cannot leave the view looking at nothing.
		/// </remarks>
		public void SetSurface(IReadOnlyList<IReadOnlyList<Cell>> rows)
		{
			_rows = rows ?? [];

			int widest = 0;
			foreach (var row in _rows) widest = Math.Max(widest, row.Count);
			SurfaceWidth = widest;

			_scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, SurfaceWidth - 1));
			_scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, SurfaceHeight - 1));

			Invalidate(Invalidation.Repaint);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// The surface's extent against the viewport's, both axes, because a
		/// view onto someone else's screen is almost always smaller than what
		/// it is showing and "is there more to the right" is the first question
		/// anyone asks of it. The vertical axis uses the shared scroll names;
		/// the horizontal one needs its own, since the shared pair is single-axis.
		/// </remarks>
		public override void DescribeState(IStateWriter w)
		{
			ArgumentNullException.ThrowIfNull(w);
			base.DescribeState(w);

			w.Scroll(ScrollY, ActualHeight, SurfaceHeight);
			w.Write("scrollOffsetX", ScrollX);
			w.Write("contentExtentX", SurfaceWidth);
		}

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// The surface's own size is the natural size, so a view in a
			// scrolling parent asks for the whole thing and a stretched one
			// takes what it is offered.
			int contentW = HorizontalAlignment == Layout.HorizontalAlignment.Stretch
				? constraints.MaxWidth - Margin.Left - Margin.Right
				: SurfaceWidth;

			int contentH = VerticalAlignment == Layout.VerticalAlignment.Fill
				? constraints.MaxHeight - Margin.Top - Margin.Bottom
				: SurfaceHeight;

			return new LayoutSize(
				Math.Clamp(contentW + Margin.Left + Margin.Right,
					constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(contentH + Margin.Top + Margin.Bottom,
					constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			Color fg = ForegroundColor;
			Color bg = BackgroundColor;

			int contentX = bounds.X + Margin.Left;
			int contentY = bounds.Y + Margin.Top;
			int availW = bounds.Width - Margin.Left - Margin.Right;
			int availH = bounds.Height - Margin.Top - Margin.Bottom;

			if (availW <= 0 || availH <= 0) return;

			for (int row = 0; row < availH; row++)
			{
				int y = contentY + row;
				if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom) continue;

				int sourceRow = _scrollY + row;
				var cells = sourceRow >= 0 && sourceRow < _rows.Count ? _rows[sourceRow] : null;

				for (int col = 0; col < availW; col++)
				{
					int x = contentX + col;
					if (x < clipRect.X || x >= clipRect.Right || x >= bounds.Right) continue;

					int sourceCol = _scrollX + col;

					// Past the surface is background, not a repeat of what came
					// before: the view is a window onto a grid, not a tile.
					if (cells == null || sourceCol < 0 || sourceCol >= cells.Count)
					{
						buffer.SetNarrowCell(x, y, ' ', fg, bg);
						continue;
					}

					// The whole cell, so the flags survive - SetNarrowCell here
					// would drop IsWideContinuation and Combiners.
					buffer.SetCell(x, y, cells[sourceCol]);
				}
			}
		}
	}
}
