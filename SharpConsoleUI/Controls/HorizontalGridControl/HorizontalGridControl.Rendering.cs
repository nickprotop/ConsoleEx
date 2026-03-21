// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class HorizontalGridControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		/// <remarks>
		/// This method measures content-based size (sum of actual child sizes), consistent with
		/// how HorizontalLayout.MeasureChildren works in the DOM system. Space distribution
		/// happens during arrangement, not measurement.
		/// </remarks>
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

            // Measure each column and sum actual widths (skip hidden ones)
            for (int i = 0; i < measureColumns.Count; i++)
            {
                var column = measureColumns[i];
                if (!column.Visible) continue;

                int columnWidth;
                int columnHeight = 0;

                if (column.Width.HasValue)
                {
                    // Column has explicit width - use it, but still measure for height
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
                    // Measure with loose constraints to get natural content size
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
                maxHeight = Math.Max(maxHeight, columnHeight);

                // Add splitter width if present after this column
                var splitter = measureSplitters.FirstOrDefault(s => measureSplitterControls[s] == i);
                if (splitter != null && splitter.Visible)
                {
                    totalWidth += splitter.Width ?? 1;
                }
            }

            return new LayoutSize(
                Math.Clamp(totalWidth, constraints.MinWidth, constraints.MaxWidth),
                Math.Clamp(maxHeight + Margin.Top + Margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
            );
        }


		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// NOTE: Container controls should NOT paint their children here.
			// Children (columns, splitters) are painted by the DOM tree's child LayoutNodes.
			// This method only paints the container's own content (background, margins).

			var bgColor = ColorResolver.ResolveBackground(BackgroundColor, Container, defaultBg);
			var fgColor = ColorResolver.ResolveForeground(ForegroundColor, Container, defaultFg);
			var effectiveBg = (BackgroundColor == null && Container?.HasGradientBackground == true)
				? Color.Transparent : bgColor;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			// Fill top margin strip
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);
			// Fill bottom margin strip
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + targetHeight, fgColor, effectiveBg);

			// Fill content rows (with left/right margin strips per row)
			for (int y = startY; y < startY + targetHeight; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillHorizontalMargins(buffer, bounds, clipRect, y, startX, targetWidth, fgColor, effectiveBg);
					var lineRect = new LayoutRect(startX, y, targetWidth, 1);
					ControlRenderingHelpers.FillRect(buffer, lineRect, fgColor, effectiveBg);
				}
			}
		}

		#endregion
	}
}
