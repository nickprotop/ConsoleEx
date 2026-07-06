// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class GridBackedHGrid
	{
		/// <summary>
		/// Content-summing measure for callers that invoke <c>GridBackedHGrid.MeasureDOM</c> DIRECTLY
		/// as a sizing helper — most notably <see cref="NavigationView"/>, which calls
		/// <c>_grid.MeasureDOM(...).Height</c> to size its content area. This is NOT the render-path measure:
		/// when the grid is wired into the layout tree it has a <see cref="GridLayout"/> and children, so
		/// <see cref="LayoutNode.Measure"/> uses <c>GridLayout.MeasureChildren</c> and never calls this method
		/// (it is only reached for a Layout-less/childless node). Returning the column content sum here
		/// preserves the natural-size contract those direct callers relied on under the old HorizontalLayout
		/// path, while the actual rendering still flows through <see cref="GridLayout"/>.
		/// </summary>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			List<ColumnContainer> measureColumns;
			List<SplitterControl> measureSplitters;
			Dictionary<IInteractiveControl, int> measureSplitterControls;
			lock (_gridLock)
			{
				measureColumns = new List<ColumnContainer>(_columns);
				measureSplitters = new List<SplitterControl>(_splitters);
				measureSplitterControls = new Dictionary<IInteractiveControl, int>(_splitterControls);
			}

			int totalWidth = Margin.Left + Margin.Right;
			int maxHeight = 0;

			// Measure each column and sum actual widths (skip hidden ones).
			for (int i = 0; i < measureColumns.Count; i++)
			{
				var column = measureColumns[i];
				if (!column.Visible) continue;

				int columnWidth;
				int columnHeight = 0;

				if (column.Width.HasValue)
				{
					// Column has explicit width - use it, but still measure for height.
					columnWidth = column.Width.Value;
					if (column is IDOMPaintable paintable)
					{
						var childSize = paintable.MeasureDOM(
							LayoutConstraints.Loose(columnWidth, constraints.MaxHeight));
						columnHeight = childSize.Height;
					}
					else
					{
						columnHeight = column.GetLogicalContentSize().Height;
					}
				}
				else if (column is IDOMPaintable paintable)
				{
					// Measure with loose constraints to get natural content size.
					var childSize = paintable.MeasureDOM(
						LayoutConstraints.Loose(constraints.MaxWidth, constraints.MaxHeight));
					columnWidth = childSize.Width;
					columnHeight = childSize.Height;
				}
				else
				{
					var size = column.GetLogicalContentSize();
					columnWidth = size.Width;
					columnHeight = size.Height;
				}

				totalWidth += columnWidth;
				maxHeight = System.Math.Max(maxHeight, columnHeight);

				// Add splitter width if present after this column.
				var splitter = measureSplitters.FirstOrDefault(s => measureSplitterControls[s] == i);
				if (splitter != null && splitter.Visible)
				{
					totalWidth += splitter.Width ?? 1;
				}
			}

			return new LayoutSize(
				System.Math.Clamp(totalWidth, constraints.MinWidth, constraints.MaxWidth),
				System.Math.Clamp(maxHeight + Margin.Top + Margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
			);
		}
	}
}
