// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		/// <summary>Which part of the chart a drag belongs to.</summary>
		/// <remarks>
		/// <see cref="Helpers.MouseGestureCapture{TRegion}"/> needs an enum, and a
		/// chart has one draggable area, but the type is what keeps a drag that
		/// started in the plot from being re-hit-tested onto something else midway.
		/// </remarks>
		private enum ChartDragRegion
		{
			/// <summary>The plot area.</summary>
			Plot,
		}

		private double? _viewXMin;
		private double? _viewXMax;

		/// <summary>True when the chart shows less than its full X range.</summary>
		public bool IsZoomed => _viewXMin.HasValue && _viewXMax.HasValue;

		/// <summary>The low end of the visible X range.</summary>
		public double VisibleXMin => _viewXMin ?? _lastDataXMin;

		/// <summary>The high end of the visible X range.</summary>
		public double VisibleXMax => _viewXMax ?? _lastDataXMax;

		/// <summary>Raised when the visible range changes.</summary>
		public event EventHandler<ChartRangeEventArgs>? ViewportChanged;

		/// <summary>The data's own X bounds, remembered so a reset has something to return to.</summary>
		private double _lastDataXMin;
		private double _lastDataXMax = 1;

		/// <summary>
		/// Shows only the given X range.
		/// </summary>
		/// <param name="xMin">One end of the range.</param>
		/// <param name="xMax">The other end; the two are ordered for you.</param>
		/// <remarks>
		/// The range is ordered and given a minimum width, because an inverted or
		/// zero-width viewport divides by zero in every mapping downstream.
		/// </remarks>
		public void ZoomTo(double xMin, double xMax)
		{
			if (double.IsNaN(xMin) || double.IsNaN(xMax)) return;

			if (xMin > xMax) (xMin, xMax) = (xMax, xMin);

			double span = xMax - xMin;
			double minimum = Math.Max(ControlDefaults.ChartMinZoomSpan,
				(_lastDataXMax - _lastDataXMin) * ControlDefaults.ChartMinZoomFraction);

			if (span < minimum)
			{
				double centre = (xMin + xMax) / 2;
				xMin = centre - minimum / 2;
				xMax = centre + minimum / 2;
			}

			SetViewport(xMin, xMax);
		}

		/// <summary>Returns to the full data range.</summary>
		public void ResetZoom() => SetViewport(null, null);

		/// <summary>Applies a viewport, firing only when it actually changed.</summary>
		private void SetViewport(double? min, double? max)
		{
			if (Nullable.Equals(_viewXMin, min) && Nullable.Equals(_viewXMax, max)) return;

			_viewXMin = min;
			_viewXMax = max;

			Invalidate(Invalidation.Repaint);
			ViewportChanged?.Invoke(this, new ChartRangeEventArgs
			{
				Min = VisibleXMin,
				Max = VisibleXMax,
				IsZoomed = IsZoomed,
			});
		}

		/// <summary>Zooms in or out about a point, keeping that point where it is.</summary>
		private void ZoomAbout(double centre, double factor)
		{
			double min = VisibleXMin, max = VisibleXMax;
			double span = (max - min) * factor;

			// Keep the cursor's data position fixed so zooming feels anchored
			// rather than recentring the chart under the pointer.
			double offset = max - min > 0 ? (centre - min) / (max - min) : 0.5;

			double newMin = centre - span * offset;
			double newMax = newMin + span;

			// Zooming out past the data is pointless; snap back to the whole range.
			if (span >= _lastDataXMax - _lastDataXMin)
			{
				ResetZoom();
				return;
			}

			ZoomTo(newMin, newMax);
		}
	}
}
