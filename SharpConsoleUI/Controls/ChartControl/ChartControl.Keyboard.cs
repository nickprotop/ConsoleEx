// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		private ChartPoint? _selectedPoint;
		private ChartSeries? _selectedSeries;
		private int _selectedIndex = -1;

		/// <summary>The point currently picked, or null when nothing is.</summary>
		public ChartPoint? SelectedPoint => _selectedPoint;

		/// <summary>The series the selected point belongs to.</summary>
		public ChartSeries? SelectedSeries => _selectedSeries;

		/// <summary>Raised when the selection changes, including when it is cleared.</summary>
		public event EventHandler<ChartPointEventArgs>? SelectionChanged;

		/// <summary>Raised when the selected point is activated with Enter.</summary>
		public event EventHandler<ChartPointEventArgs>? PointActivated;

		/// <summary>
		/// Moves the selection, raising the event only when it actually changed.
		/// </summary>
		/// <remarks>
		/// The guard matters: an app that redraws on <see cref="SelectionChanged"/>
		/// would loop forever on a pointer resting over one point.
		/// </remarks>
		private void SetSelection(ChartSeries? series, int index)
		{
			ChartPoint? point = null;
			if (series != null && (uint)index < (uint)series.Points.Count)
			{
				point = series.Points[index];
			}
			else
			{
				series = null;
				index = -1;
			}

			if (_selectedSeries == series && _selectedIndex == index) return;

			_selectedSeries = series;
			_selectedIndex = index;
			_selectedPoint = point;

			Invalidate(Invalidation.Repaint);
			SelectionChanged?.Invoke(this, new ChartPointEventArgs
			{
				Point = point,
				Series = series,
			});
		}

		/// <summary>Drops the selection without firing when there was none.</summary>
		private void ClearSelection() => SetSelection(null, -1);

		/// <summary>The series the keyboard walks: the selected one, else the first visible.</summary>
		private ChartSeries? KeyboardSeries()
		{
			if (_selectedSeries != null) return _selectedSeries;

			lock (_dataLock)
			{
				return _series.FirstOrDefault(s => s.IsVisible && s.Points.Count > 0);
			}
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// Declining lets the key reach whatever else wanted it; a chart that
			// swallows everything breaks the window's own shortcuts.
			if (!_enabled) return false;

			// Zoom keys work without selection, so they are checked before the
			// selection gate rather than behind it.
			if (_enableZoom)
			{
				switch (key.Key)
				{
					case ConsoleKey.Add:
					case ConsoleKey.OemPlus:
						ZoomAbout((VisibleXMin + VisibleXMax) / 2, ControlDefaults.ChartWheelZoomFactor);
						return true;

					case ConsoleKey.Subtract:
					case ConsoleKey.OemMinus:
						ZoomAbout((VisibleXMin + VisibleXMax) / 2, 1 / ControlDefaults.ChartWheelZoomFactor);
						return true;

					case ConsoleKey.Escape when _selectedPoint == null && IsZoomed:
						ResetZoom();
						return true;
				}
			}

			if (!_enableSelection) return false;

			var series = KeyboardSeries();
			int count = series?.Points.Count ?? 0;

			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
					if (count == 0) return false;
					// Clamp rather than wrap: an axis has ends, and silently jumping
					// from the last point to the first misreads the data.
					SetSelection(series, Math.Max(0, CurrentIndexOr(0) - 1));
					return true;

				case ConsoleKey.RightArrow:
					if (count == 0) return false;
					SetSelection(series, Math.Min(count - 1, CurrentIndexOr(-1) + 1));
					return true;

				case ConsoleKey.Home:
					if (count == 0) return false;
					SetSelection(series, 0);
					return true;

				case ConsoleKey.End:
					if (count == 0) return false;
					SetSelection(series, count - 1);
					return true;

				case ConsoleKey.Enter:
					if (_selectedPoint == null) return false;
					PointActivated?.Invoke(this, new ChartPointEventArgs
					{
						Point = _selectedPoint,
						Series = _selectedSeries,
					});
					return true;

				case ConsoleKey.Escape:
					// Escape means "back out": it drops the selection, and failing
					// that it returns a zoomed chart to its full range.
					if (_selectedPoint != null) { ClearSelection(); return true; }
					if (_enableZoom && IsZoomed) { ResetZoom(); return true; }
					return false;

				default:
					return false;
			}
		}

		/// <summary>The selected index, or a fallback when nothing is selected yet.</summary>
		private int CurrentIndexOr(int fallback) => _selectedIndex >= 0 ? _selectedIndex : fallback;
	}
}
