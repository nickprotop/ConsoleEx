// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class ChartControl
	{
		private readonly Helpers.MouseGestureCapture<ChartDragRegion> _drag = new();
		private int _dragStartX = -1;
		private int _dragCurrentX = -1;

		private ChartPoint? _hoveredPoint;
		private ChartSeries? _hoveredSeries;

		/// <summary>The point under the pointer, or null when nothing is near it.</summary>
		public ChartPoint? HoveredPoint => _hoveredPoint;

		/// <summary>The series the hovered point belongs to.</summary>
		public ChartSeries? HoveredSeries => _hoveredSeries;

		/// <summary>Where each point was last drawn. For tests.</summary>
		internal IReadOnlyList<(ChartSeries Series, int Index, int CellX, int CellY)> PointPositionsForTest() =>
			_pointPositions;

		/// <summary>
		/// Finds the drawn point nearest a cell, within the hover radius.
		/// </summary>
		/// <remarks>
		/// Searches the positions the plot pass recorded as it drew, so hit-testing
		/// and drawing can never disagree about where a point is. Hidden series are
		/// not in that list, so a chart with everything toggled off finds nothing
		/// rather than indexing into an empty collection.
		/// </remarks>
		private (ChartSeries Series, ChartPoint Point)? NearestPoint(int cellX, int cellY)
		{
			if (_pointPositions.Count == 0) return null;

			int bestDistance = int.MaxValue;
			ChartSeries? bestSeries = null;
			int bestIndex = -1;

			foreach (var (series, index, px, py) in _pointPositions)
			{
				if (!series.IsVisible) continue;

				int dx = px - cellX, dy = py - cellY;
				int distance = dx * dx + dy * dy;
				if (distance >= bestDistance) continue;

				bestDistance = distance;
				bestSeries = series;
				bestIndex = index;
			}

			if (bestSeries == null) return null;

			int radius = ControlDefaults.ChartHoverRadiusCells;
			if (bestDistance > radius * radius) return null;

			var points = bestSeries.Points;
			if ((uint)bestIndex >= (uint)points.Count) return null;

			return (bestSeries, points[bestIndex]);
		}

		/// <summary>Clears any hover and repaints if something changed.</summary>
		private void ClearHover()
		{
			if (_hoveredPoint == null && _hoveredSeries == null) return;

			_hoveredPoint = null;
			_hoveredSeries = null;
			Invalidate(Invalidation.Repaint);
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			ArgumentNullException.ThrowIfNull(args);

			// Nothing is enabled: decline, so the window under this chart keeps its
			// drag and whatever else wanted the event.
			if (!WantsMouseEvents) return false;

			if (_enableZoom && args.HasAnyFlag(MouseFlags.WheeledUp, MouseFlags.WheeledDown))
			{
				return HandleWheel(args);
			}

			// Most specific gesture first (CLAUDE.md rule 14). A click arrives
			// carrying Pressed AND Clicked together, so testing the press first
			// would see every click as the start of a drag and selection would
			// never fire - which is exactly what happened before this ordering.
			//
			// A drag already in progress is the one exception: it owns every event
			// until its release, because SGR resends the press on motion and that
			// resent press is not a fresh interaction.
			if (_drag.IsCapturing)
			{
				if (HandleDrag(args)) return true;
			}
			else
			{
				if (_enableSelection && args.HasFlag(MouseFlags.Button1Clicked))
				{
					return HandleClick(args);
				}

				if (_enableZoom && args.HasFlag(MouseFlags.Button1Pressed))
				{
					if (HandleDrag(args)) return true;
				}
			}

			if (_enableHover && args.HasFlag(MouseFlags.ReportMousePosition))
			{
				return HandleHover(args);
			}

			return false;
		}

		/// <summary>Zooms about the pointer.</summary>
		private bool HandleWheel(MouseEventArgs args)
		{
			double factor = args.HasFlag(MouseFlags.WheeledUp)
				? ControlDefaults.ChartWheelZoomFactor
				: 1 / ControlDefaults.ChartWheelZoomFactor;

			ZoomAbout(ColumnToValue(args.Position.X), factor);
			args.Handled = true;
			return true;
		}

		/// <summary>
		/// Rubber-bands an X range and zooms to it on release.
		/// </summary>
		/// <remarks>
		/// Through <see cref="Helpers.MouseGestureCapture{TRegion}"/> rather than
		/// hand-rolled state, so a drag that leaves the control still ends properly.
		/// A press and release in one place is a click, not a zoom - otherwise the
		/// chart becomes unusable the first time anyone clicks it.
		/// </remarks>
		private bool HandleDrag(MouseEventArgs args)
		{
			var route = _drag.Route(args, _ => ChartDragRegion.Plot);

			switch (route.Phase)
			{
				case Helpers.GesturePhase.Down:
					_dragStartX = args.Position.X;
					_dragCurrentX = _dragStartX;
					args.Handled = true;
					return true;

				case Helpers.GesturePhase.Move:
					_dragCurrentX = args.Position.X;
					Invalidate(Invalidation.Repaint);
					args.Handled = true;
					return true;

				case Helpers.GesturePhase.Up:
					int from = _dragStartX, to = args.Position.X;
					_dragStartX = _dragCurrentX = -1;
					Invalidate(Invalidation.Repaint);

					// One cell is a click. Let it fall through to selection.
					if (Math.Abs(to - from) < ControlDefaults.ChartMinDragCells) return false;

					ZoomTo(ColumnToValue(from), ColumnToValue(to));
					args.Handled = true;
					return true;

				default:
					return false;
			}
		}

		/// <summary>Maps a buffer column back to a value on the X axis.</summary>
		private double ColumnToValue(int column)
		{
			var plot = _lastPlotRect;
			if (plot.Width <= 1) return VisibleXMin;

			double fraction = Math.Clamp((double)(column - plot.X) / (plot.Width - 1), 0, 1);
			return VisibleXMin + fraction * (VisibleXMax - VisibleXMin);
		}

		/// <summary>
		/// Picks a point, or toggles a series when the click lands on the legend.
		/// </summary>
		private bool HandleClick(MouseEventArgs args)
		{
			int x = args.Position.X, y = args.Position.Y;

			// The legend is checked first: a click there is never a plot click.
			foreach (var (series, rect) in _legendRegions)
			{
				if (!rect.Contains(x, y)) continue;

				series.IsVisible = !series.IsVisible;

				// A hidden series must not stay selected or hovered - it is no longer
				// on the chart to point at.
				if (!series.IsVisible)
				{
					if (_selectedSeries == series) ClearSelection();
					if (_hoveredSeries == series) ClearHover();
				}

				Invalidate(Invalidation.Repaint);
				TakeFocus();
				args.Handled = true;
				return true;
			}

			var found = NearestPoint(x, y);
			if (found == null)
			{
				// A click on empty plot drops the selection, which is how a user
				// gets back to "nothing picked".
				if (_selectedPoint == null) return false;
				ClearSelection();
				TakeFocus();
				args.Handled = true;
				return true;
			}

			var (hitSeries, _) = found.Value;
			int index = IndexOfNearest(x, y, hitSeries);

			SetSelection(hitSeries, index);
			TakeFocus();
			args.Handled = true;
			return true;
		}

		/// <summary>The index within a series of the point nearest a cell.</summary>
		private int IndexOfNearest(int cellX, int cellY, ChartSeries series)
		{
			int best = -1, bestDistance = int.MaxValue;

			foreach (var (candidate, index, px, py) in _pointPositions)
			{
				if (candidate != series) continue;
				int dx = px - cellX, dy = py - cellY;
				int distance = dx * dx + dy * dy;
				if (distance >= bestDistance) continue;
				bestDistance = distance;
				best = index;
			}

			return best;
		}

		/// <summary>
		/// Takes the keyboard when the control acted on a press.
		/// </summary>
		/// <remarks>
		/// Focus follows the press rather than the release (CLAUDE.md rule 14), and
		/// only when selection is on - a chart that cannot be navigated has no use
		/// for the keyboard.
		/// </remarks>
		private void TakeFocus()
		{
			if (!_enableSelection) return;
			this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
		}

		/// <summary>Tracks the nearest point under the pointer.</summary>
		private bool HandleHover(MouseEventArgs args)
		{
			var found = NearestPoint(args.Position.X, args.Position.Y);

			if (found == null)
			{
				ClearHover();
				// Nothing of ours is under the pointer, so the event is not ours.
				return false;
			}

			var (series, point) = found.Value;

			// Guard before invalidating: a pointer resting on one point would
			// otherwise repaint on every motion report.
			if (_hoveredSeries == series && Nullable.Equals(_hoveredPoint, point))
			{
				args.Handled = true;
				return true;
			}

			_hoveredSeries = series;
			_hoveredPoint = point;
			Invalidate(Invalidation.Repaint);

			args.Handled = true;
			return true;
		}
	}
}
