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
	public partial class NavigationView
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int marginH = Margin.Left + Margin.Right;
			int marginV = Margin.Top + Margin.Bottom;

			// Use the actual rendered width from the previous frame if available,
			// otherwise fall back to constraints. ActualWidth is set by SetActualBounds
			// in PaintDOM and reflects the true allocated width.
			// Subtract margins so responsive mode checks use content-area width.
			int availableWidth = (ActualWidth > 0 ? ActualWidth : (Width ?? constraints.MaxWidth)) - marginH;
			CheckAndApplyDisplayMode(availableWidth);

			SyncInternalControls();

			var adjustedConstraints = constraints.SubtractWidth(marginH).SubtractHeight(marginV);
			int width = Width ?? constraints.MaxWidth;
			int height = _grid.MeasureDOM(adjustedConstraints).Height + marginV;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			int targetHeight = bounds.Height - Margin.Top - Margin.Bottom;

			// Check responsive mode using margin-adjusted content width.
			// If mode changes here, we invalidate to trigger re-layout on the next frame.
			CheckAndApplyDisplayMode(targetWidth);

			// Background fill — preserve gradient if no explicit background
			var bgColor = ColorResolver.ResolveBackground(_backgroundColorValue, Container, defaultBg);
			var fgColor = ColorResolver.ResolveForeground(_foregroundColor, Container, defaultFg);
			var effectiveBg = _backgroundColorValue == null ? Color.Transparent : bgColor;

			for (int y = startY; y < startY + targetHeight; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					var lineRect = new LayoutRect(startX, y, targetWidth, 1);
					buffer.FillRect(lineRect, ' ', fgColor, effectiveBg);
				}
			}

			// Children (the grid) are painted by the DOM tree's child LayoutNodes
			_isDirty = false;
		}

		#endregion
	}
}
