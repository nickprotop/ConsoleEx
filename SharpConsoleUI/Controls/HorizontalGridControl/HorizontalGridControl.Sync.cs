// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Extensions;
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
		/// to one grid column (Width&#8594;Fixed; FlexFactor&gt;0&#8594;Star with its Min floored at the column's
		/// content width; FlexFactor==0&#8594;Auto, honoring Min/Max), interleaving splitters as real fixed-width
		/// columns. Idempotent; guarded against re-entrancy.
		/// </summary>
		// Signature of the column model as last stamped into the grid. Sync() rebuilds the grid tracks/cells
		// only when this changes — a child-content Relayout propagating up (typing, cursor blink, syntax
		// repaint) leaves the column model identical, so it must NOT tear down and re-Place every column each
		// frame (the flicker / "recreates columns every frame" regression). Structural changes (add/remove
		// column or splitter, a column's Width/FlexFactor/Visible toggle, an alignment change) shift the
		// signature and trigger a real rebuild.
		private string? _lastSyncSignature;

		// Test-observable count of ACTUAL grid rebuilds (signature-miss), not skipped Sync() calls.
		internal int SyncRebuildCount { get; private set; }

		private string ComputeModelSignature(List<ColumnContainer> columns, List<SplitterControl> splitters)
		{
			var sb = new System.Text.StringBuilder();
			sb.Append(VerticalAlignment == VerticalAlignment.Fill ? 'F' : '_').Append('|');
			foreach (var c in columns)
			{
				sb.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c))
				  .Append(':').Append(c.Visible ? '1' : '0')
				  .Append(':').Append(c.Width?.ToString() ?? "_")
				  .Append(':').Append(c.MinWidth?.ToString() ?? "_")
				  .Append(':').Append(c.MaxWidth?.ToString() ?? "_")
				  .Append(':').Append(c.FlexFactor.ToString("R"))
				  // The SET of a column's child controls is structure Sync() Places into the grid: a control
				  // added to or removed from a column must re-Place so the grid re-measures. A child whose
				  // CONTENT changes in place (same instance, new text) leaves this signature unchanged, so
				  // per-frame content updates still skip the rebuild (the flicker fix).
				  .Append(':').Append(c.Contents.Count).Append('#');
				foreach (var content in c.Contents)
					sb.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(content)).Append(',');
				sb.Append(';');
			}
			sb.Append('|');
			foreach (var s in splitters)
			{
				sb.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s))
				  .Append(':').Append(GetSplitterLeftColumnIndex(s))
				  .Append(':').Append(s.Width?.ToString() ?? "_")
				  .Append(';');
			}
			return sb.ToString();
		}

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

				// Skip the structural rebuild when the column model is unchanged since the last stamp: a
				// child-content Relayout (typing, cursor blink, syntax repaint) propagating up must NOT tear
				// down and re-Place every column. Each ClearControls/RowDefinitions.Clear/ColumnDefinitions.Add/
				// Place internally calls RebuildAndInvalidate -> ForceRebuildLayout (a full WINDOW layout-tree
				// rebuild); running the whole teardown every frame issued dozens of window rebuilds per frame
				// (the flicker / "recreates columns each frame" regression). On skip we still force ONE window
				// layout rebuild so the propagating Relayout re-measures cleanly (the grid caches layout nodes
				// that a Relayout must refresh) — one rebuild, not the teardown storm.
				string signature = ComputeModelSignature(columns, splitters);
				if (signature == _lastSyncSignature)
				{
					return;
				}
				_lastSyncSignature = signature;
				SyncRebuildCount++;

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
		/// master's HorizontalLayout sizing. An explicit Width becomes a Fixed track (Min/Max clamped); a
		/// hidden column collapses to a zero-cell track. A positive FlexFactor becomes a Star track whose Min
		/// is floored at the column's content width, so it fans out only into the surplus above content. A
		/// FlexFactor==0 column is Auto (pure content).
		/// </summary>
		/// <remarks>
		/// The Star branch passes <c>max: null</c>: master's HorizontalLayout does NOT apply
		/// <see cref="ColumnContainer.MaxWidth"/> as a hard cap on a flex column (only Fixed columns honour
		/// the cap there). Min/Max are kept for the Fixed branch. A flex column is ALWAYS a Star track so its
		/// arrange fans out into whatever extent the grid is given; the grid's measured DesiredSize stays
		/// content-packed (never the bounded parent extent) via
		/// <see cref="IGridSource.StarTracksSelfSizeToContentInMeasure"/>, mirroring HGC's own <c>MeasureDOM</c>.
		/// A content-tight parent (window root, Left/Center/Right grid) then packs the cluster; a wider parent
		/// (Stretch slot, ScrollablePanel viewport) lets the flex columns fan out.
		/// </remarks>
		private static GridLength ToGridLength(ColumnContainer col)
		{
			if (!col.Visible) return GridLength.Cells(0);
			if (col.Width != null) return GridLength.Cells(col.Width.Value, col.MinWidth, col.MaxWidth);
			if (col.FlexFactor > 0)
			{
				// A flex column is ALWAYS a Star track — its arrange behaviour is uniform: it fans out into
				// whatever extent the grid is arranged at (exactly master's HorizontalLayout, whose flex arrange
				// does not consult alignment). The only alignment-dependent quantity is the extent the PARENT
				// hands the grid: a Left/Center/Right grid at the window root is arranged at its content-packed
				// DesiredSize (WindowContentLayout), so the Star floors sum to the content total and the columns
				// PACK; a Stretch grid, or a grid inside a ScrollablePanel viewport, is arranged wider and the
				// Star surplus fans out by FlexFactor. That the grid's measured DesiredSize is content-packed
				// (never the bounded parent extent) is guaranteed by StarTracksSelfSizeToContentInMeasure, which
				// floors Star at content during BOTH bounded and unbounded measure — mirroring HGC's MeasureDOM.
				//
				// The Star Min is floored at the column's CONTENT width, reproducing HGC's arrange formula
				// "baseWidth (content) + share of the surplus by FlexFactor": the floor forms baseWidth and
				// GridTrackSizer splits the remainder by weight. The floor is read from the column's logical
				// content width (the same quantity HGC's MeasureDOM sums); it does not depend on any Star
				// division, so it is a stable fixed point across re-renders (no measure/arrange feedback loop).
				// Passing max:null matches HGC, which does NOT cap a flex column at MaxWidth.
				int contentFloor = col.GetContentWidth() ?? 0;
				int? min = col.MinWidth.HasValue ? Math.Max(col.MinWidth.Value, contentFloor) : contentFloor;
				return GridLength.Star(col.FlexFactor, min, null);
			}
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
