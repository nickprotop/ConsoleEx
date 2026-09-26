// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollbarControl
	{
		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (args.Handled || !_isEnabled) return false;

			int trackLength = _orientation == ScrollbarOrientation.Vertical ? ActualHeight : ActualWidth;
			if (trackLength <= 0) return false;

			bool isWheel = args.HasFlag(Drivers.MouseFlags.WheeledUp) || args.HasFlag(Drivers.MouseFlags.WheeledDown);
			if (isWheel)
			{
				if (args.HasFlag(Drivers.MouseFlags.WheeledUp))
					SetValueFromUser(_value - _smallChange);
				else
					SetValueFromUser(_value + _smallChange);

				args.Handled = true;
				return true;
			}

			if (args.HasAnyFlag(Drivers.MouseFlags.Button1Pressed, Drivers.MouseFlags.Button1Dragged,
				Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1Clicked))
			{
				var route = _gesture.Route(args, _ => ScrollbarGestureRegion.Track);

				if (route.Phase == GesturePhase.None && args.HasFlag(Drivers.MouseFlags.Button1Clicked))
					route = new GestureRoute<ScrollbarGestureRegion>(GesturePhase.Down, ScrollbarGestureRegion.Track);

				if (route.Phase != GesturePhase.None && route.Region == ScrollbarGestureRegion.Track)
					return HandleTrackGesture(route.Phase, args, trackLength);
			}

			return false;
		}

		private bool HandleTrackGesture(GesturePhase phase, MouseEventArgs args, int trackLength)
		{
			int relativePos = _orientation == ScrollbarOrientation.Vertical ? args.Position.Y : args.Position.X;
			var metrics = TrackMetrics(trackLength);

			switch (phase)
			{
				case GesturePhase.Down:
					_thumbDragging = false;
					{
						var zone = ScrollbarInput.HitTest(metrics, relativePos);
						if (zone == ScrollbarHitZone.Thumb)
						{
							_thumbDragging = true;
							_dragStartPointerPos = relativePos;
							_dragStartThumbPos = ScrollbarGeometry.ThumbPosForOffset(metrics);
						}
						else if (zone != ScrollbarHitZone.None)
						{
							int newValue = ScrollbarInput.OffsetForZone(metrics, zone, _smallChange, LargeChange);
							SetValueFromUser(newValue);
						}
					}
					args.Handled = true;
					return true;

				case GesturePhase.Move:
					if (_thumbDragging)
					{
						int delta = relativePos - _dragStartPointerPos;
						int newValue = ScrollbarInput.OffsetForDrag(metrics, _dragStartThumbPos, delta);
						SetValueFromUser(newValue);
					}
					args.Handled = true;
					return true;

				default: // Up
					_thumbDragging = false;
					args.Handled = true;
					return true;
			}
		}

		#endregion
	}
}
