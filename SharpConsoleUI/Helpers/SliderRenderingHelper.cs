// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Shared rendering and math utilities for SliderControl and RangeSliderControl.
	/// Eliminates code duplication between the two slider implementations.
	/// </summary>
	public static class SliderRenderingHelper
	{
		/// <summary>
		/// Converts a value in [min, max] to a track position in [0, trackLength - 1].
		/// </summary>
		/// <param name="value">The current value.</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
		/// <param name="trackLength">The length of the track in characters.</param>
		/// <returns>The position on the track (0-based).</returns>
		public static int ValueToPosition(double value, double min, double max, int trackLength)
		{
			if (trackLength <= 0 || max <= min)
				return 0;

			double ratio = Math.Clamp((value - min) / (max - min), 0.0, 1.0);
			return (int)Math.Round(ratio * (trackLength - 1));
		}

		/// <summary>
		/// Converts a track position in [0, trackLength - 1] to a value in [min, max].
		/// </summary>
		/// <param name="position">The position on the track (0-based).</param>
		/// <param name="min">The minimum value.</param>
		/// <param name="max">The maximum value.</param>
		/// <param name="trackLength">The length of the track in characters.</param>
		/// <returns>The value corresponding to the position.</returns>
		public static double PositionToValue(int position, double min, double max, int trackLength)
		{
			if (trackLength <= 1)
				return min;

			double ratio = Math.Clamp((double)position / (trackLength - 1), 0.0, 1.0);
			return min + ratio * (max - min);
		}

		/// <summary>
		/// Snaps a value to the nearest step increment from the minimum value.
		/// </summary>
		/// <param name="value">The raw value to snap.</param>
		/// <param name="min">The minimum value (step base).</param>
		/// <param name="max">The maximum value.</param>
		/// <param name="step">The step increment.</param>
		/// <returns>The snapped value, clamped to [min, max].</returns>
		public static double SnapToStep(double value, double min, double max, double step)
		{
			if (step < ControlDefaults.SliderMinStep)
				step = ControlDefaults.SliderMinStep;

			double snapped = min + Math.Round((value - min) / step) * step;
			return Math.Clamp(snapped, min, max);
		}

		/// <summary>
		/// Paints a horizontal segment of track characters.
		/// </summary>
		/// <param name="buffer">The character buffer to render to.</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="bounds">The control bounds for clipping.</param>
		/// <param name="x">Starting X position.</param>
		/// <param name="y">Y position.</param>
		/// <param name="length">Number of characters to paint.</param>
		/// <param name="trackChar">The character to use.</param>
		/// <param name="color">The foreground color.</param>
		/// <param name="bgColor">The background color.</param>
		public static void PaintHorizontalTrackSegment(
			CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int x, int y, int length, char trackChar, Color color, Color bgColor)
		{
			for (int i = 0; i < length; i++)
			{
				int paintX = x + i;
				if (paintX >= clipRect.X && paintX < clipRect.Right && paintX < bounds.Right)
				{
					buffer.SetNarrowCell(paintX, y, trackChar, color, bgColor);
				}
			}
		}

		/// <summary>
		/// Paints a vertical segment of track characters.
		/// </summary>
		/// <param name="buffer">The character buffer to render to.</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="bounds">The control bounds for clipping.</param>
		/// <param name="x">X position.</param>
		/// <param name="y">Starting Y position.</param>
		/// <param name="length">Number of characters to paint.</param>
		/// <param name="trackChar">The character to use.</param>
		/// <param name="color">The foreground color.</param>
		/// <param name="bgColor">The background color.</param>
		public static void PaintVerticalTrackSegment(
			CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int x, int y, int length, char trackChar, Color color, Color bgColor)
		{
			for (int i = 0; i < length; i++)
			{
				int paintY = y + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					if (x >= clipRect.X && x < clipRect.Right && x < bounds.Right)
					{
						buffer.SetNarrowCell(x, paintY, trackChar, color, bgColor);
					}
				}
			}
		}
	}
}
