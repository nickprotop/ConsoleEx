// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollbarControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width, height;

			if (_orientation == ScrollbarOrientation.Vertical)
			{
				width = 1 + Margin.Left + Margin.Right;
				height = (Height ?? Math.Max(1, constraints.MaxHeight - Margin.Top - Margin.Bottom)) + Margin.Top + Margin.Bottom;
			}
			else
			{
				width = (Width ?? Math.Max(1, constraints.MaxWidth - Margin.Left - Margin.Right)) + Margin.Left + Margin.Right;
				height = 1 + Margin.Top + Margin.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			var bgColor = ColorResolver.ResolveBackground(null, Container);

			int trackLength = _orientation == ScrollbarOrientation.Vertical
				? bounds.Height - Margin.Top - Margin.Bottom
				: bounds.Width - Margin.Left - Margin.Right;
			if (trackLength <= 0) return;

			int x = bounds.X + Margin.Left;
			int y = bounds.Y + Margin.Top;

			var palette = ResolvePalette(bgColor);
			var axis = _orientation == ScrollbarOrientation.Vertical ? ScrollbarAxis.Vertical : ScrollbarAxis.Horizontal;

			ScrollbarRenderer.Draw(buffer, axis, x, y, TrackMetrics(trackLength), palette, clip: clipRect);
		}

		#endregion

		#region Private Rendering Helpers

		/// <summary>The shared engine's view of this bar's geometry inputs, for the given track length.</summary>
		private ScrollbarMetrics TrackMetrics(int trackLength) =>
			new(trackLength, _maximum, _viewportLength, _value);

		private ScrollbarPalette ResolvePalette(Color background) =>
			ScrollbarPaletteResolver.Resolve(new ScrollbarPaletteRequest(
				ThumbOverride: _scrollbarThumbColor,
				TrackOverride: _scrollbarColor,
				Theme: Container?.GetConsoleWindowSystem?.Theme,
				HasFocus: _isActive,
				IsEnabled: _isEnabled,
				Background: background));

		#endregion
	}
}
