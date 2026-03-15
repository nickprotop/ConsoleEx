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
			// Use the actual rendered width from the previous frame if available,
			// otherwise fall back to constraints. ActualWidth is set by SetActualBounds
			// in PaintDOM and reflects the true allocated width.
			int availableWidth = ActualWidth > 0 ? ActualWidth : (Width ?? constraints.MaxWidth);
			CheckAndApplyDisplayMode(availableWidth);

			SyncInternalControls();

			int width = Width ?? constraints.MaxWidth;
			int height = _grid.MeasureDOM(constraints).Height;

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

			// Check responsive mode using actual allocated width from layout.
			// If mode changes here, we invalidate to trigger re-layout on the next frame.
			CheckAndApplyDisplayMode(bounds.Width);

			// Background fill — preserve gradient if no explicit background
			var bgColor = ColorResolver.ResolveBackground(_backgroundColorValue, Container, defaultBg);
			var fgColor = ColorResolver.ResolveForeground(_foregroundColor, Container, defaultFg);
			bool preserveBg = _backgroundColorValue == null && (Container?.HasGradientBackground ?? false);

			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					var lineRect = new LayoutRect(bounds.X, y, bounds.Width, 1);
					if (preserveBg)
						buffer.FillRectPreservingBackground(lineRect, fgColor);
					else
						buffer.FillRect(lineRect, ' ', fgColor, bgColor);
				}
			}

			// Children (the grid) are painted by the DOM tree's child LayoutNodes
			_isDirty = false;
		}

		#endregion
	}
}
