// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class HorizontalGridControl
	{
		/// <summary>
		/// Re-entrancy guard for <see cref="Sync"/>. A splitter-drag writeback (Task 7) mutates
		/// <see cref="ColumnContainer.Width"/>, which would otherwise re-trigger a full <see cref="Sync"/>
		/// mid-drag; this flag short-circuits that feedback loop.
		/// </summary>
		private bool _syncing;

		/// <summary>
		/// HGC opts into Star-as-content sizing during MEASURE (see
		/// <see cref="IGridSource.StarTracksSelfSizeToContentInMeasure"/>): a flex (Star) column/row reports its
		/// CONTENT size as the grid's desired size, then ARRANGE distributes Star across the real allocation.
		/// This reproduces the retired HorizontalLayout's measure/arrange split: a content-tight parent (the
		/// window root for a Left/Center/Right grid, or any parent that measures unbounded) packs the grid to
		/// content; a parent that hands the grid a wider box (a ScrollablePanel, a Stretch slot) lets the flex
		/// columns fan out.
		/// </summary>
		protected override bool StarTracksSelfSizeToContentInMeasure => true;

		/// <summary>
		/// Rebuilds the underlying single-row grid from the column model. Maps each <see cref="ColumnContainer"/>
		/// to one grid column (Width&#8594;Fixed, FlexFactor&gt;0&#8594;Star, else Auto, honoring Min/Max),
		/// interleaving splitters as real fixed-width columns. Idempotent; guarded against re-entrancy.
		/// </summary>
		private void Sync()
		{
			if (_syncing) return;
			_syncing = true;
			try
			{
				List<ColumnContainer> columns;
				List<SplitterControl> splitters;
				lock (_gridLock)
				{
					columns = new List<ColumnContainer>(_columns);
					splitters = new List<SplitterControl>(_splitters);
				}

				// Clear the inherited grid state (cells + track defs) and re-stamp HGC's flush layout.
				ClearControls();
				ColumnGap = 0;
				RowGap = 0;
				RowDefinitions.Clear();
				ColumnDefinitions.Clear();

				if (columns.Count == 0)
				{
					return; // empty HGC = empty grid (GridLayout short-circuits)
				}

				// Single row. A Fill grid uses a Star row so cells get the full arranged height; otherwise the
				// row is Auto (self-sizes to the tallest cell). With the measure/arrange split above, a Star row
				// also reports content height at measure, so even a Fill grid self-sizes when measured unbounded.
				RowDefinitions.Add(VerticalAlignment == VerticalAlignment.Fill ? GridLength.Star() : GridLength.Auto());

				// Build the interleaved column list: column, [splitter], column, ...
				int gridCol = 0;
				for (int i = 0; i < columns.Count; i++)
				{
					ColumnContainer col = columns[i];

					// A column IS its track: master's HorizontalLayout always arranged the column box at the
					// full track width (it never consulted the column's own HorizontalAlignment). GridLayout,
					// by contrast, honours a child's alignment, so a Left/Top column would be arranged at its
					// content size and leave the track partly empty (its background would not reach the track
					// edge, and ActualWidth would report the content width). Stamp Stretch/Fill so the column
					// fills its cell exactly as before.
					if (col.HorizontalAlignment != HorizontalAlignment.Stretch)
					{
						col.HorizontalAlignment = HorizontalAlignment.Stretch;
					}
					if (col.VerticalAlignment != VerticalAlignment.Fill)
					{
						col.VerticalAlignment = VerticalAlignment.Fill;
					}

					ColumnDefinitions.Add(ToGridLength(col));
					Place(col, 0, gridCol);
					gridCol++;

					SplitterControl? splitterAfter = SplitterAfterColumn(i, splitters);
					if (splitterAfter != null)
					{
						ColumnDefinitions.Add(GridLength.Cells(splitterAfter.Width ?? 1));
						Place(splitterAfter, 0, gridCol);
						gridCol++;
					}
				}
			}
			finally { _syncing = false; }
		}

		/// <summary>
		/// Translates a single <see cref="ColumnContainer"/> into the grid track length that reproduces
		/// master's HorizontalLayout sizing: an explicit Width becomes a Fixed track (Min/Max clamped);
		/// a positive FlexFactor becomes a Star track; otherwise the column is content-sized (Auto). A
		/// hidden column collapses to a zero-cell track.
		/// </summary>
		/// <remarks>
		/// The FLEX (Star) branch passes <c>max: null</c>: master's HorizontalLayout does NOT apply
		/// <see cref="ColumnContainer.MaxWidth"/> as a hard cap on a flex column (only Fixed columns honour
		/// the cap there). Min/Max are kept for the Fixed branch. A flex column is ALWAYS a Star track so it
		/// distributes extra space whenever the grid is arranged wider than its content (exactly master's
		/// HorizontalLayout behaviour); the grid still self-sizes when measured unbounded via
		/// <see cref="IGridSource.StarTracksSelfSizeToContentInMeasure"/> (overridden true on HGC), so a
		/// content-tight parent (e.g. the window root for a Left-aligned grid) packs to content.
		/// </remarks>
		private static GridLength ToGridLength(ColumnContainer col)
		{
			if (!col.Visible) return GridLength.Cells(0);
			if (col.Width != null) return GridLength.Cells(col.Width.Value, col.MinWidth, col.MaxWidth);
			if (col.FlexFactor > 0) return GridLength.Star(col.FlexFactor, col.MinWidth, null);
			return GridLength.Auto(col.MinWidth, col.MaxWidth);
		}

		/// <summary>
		/// Returns the splitter (if any) whose left column is <paramref name="columnIndex"/>, using HGC's
		/// existing <c>_splitterControls</c> left-column-index map. Returns <c>null</c> when no splitter sits
		/// after that column.
		/// </summary>
		private SplitterControl? SplitterAfterColumn(int columnIndex, List<SplitterControl> splitters)
		{
			lock (_gridLock)
			{
				foreach (SplitterControl s in splitters)
				{
					if (_splitterControls.TryGetValue(s, out int leftIndex) && leftIndex == columnIndex)
					{
						return s;
					}
				}
			}
			return null;
		}
	}
}
