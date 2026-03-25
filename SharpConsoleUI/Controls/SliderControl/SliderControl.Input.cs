// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class SliderControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !HasFocus)
				return false;

			bool isVertical = _orientation == SliderOrientation.Vertical;
			double delta = 0;
			bool handled = false;

			switch (key.Key)
			{
				case ConsoleKey.RightArrow when !isVertical:
				case ConsoleKey.UpArrow when isVertical:
					delta = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? _largeStep : _step;
					handled = true;
					break;

				case ConsoleKey.LeftArrow when !isVertical:
				case ConsoleKey.DownArrow when isVertical:
					delta = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -_largeStep : -_step;
					handled = true;
					break;

				case ConsoleKey.UpArrow when !isVertical:
				case ConsoleKey.RightArrow when isVertical:
					// Non-primary axis arrows are not handled
					return false;

				case ConsoleKey.DownArrow when !isVertical:
				case ConsoleKey.LeftArrow when isVertical:
					return false;

				case ConsoleKey.PageUp:
					delta = _largeStep;
					handled = true;
					break;

				case ConsoleKey.PageDown:
					delta = -_largeStep;
					handled = true;
					break;

				case ConsoleKey.Home:
					Value = _minValue;
					return true;

				case ConsoleKey.End:
					Value = _maxValue;
					return true;

				default:
					return false;
			}

			if (handled && Math.Abs(delta) > 0)
			{
				Value = _value + delta;
			}

			return handled;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled) return false;

			// Ignore synthetic enter/leave events
			if (args.HasAnyFlag(MouseFlags.MouseEnter, MouseFlags.MouseLeave))
				return false;

			bool isVertical = _orientation == SliderOrientation.Vertical;

			// Handle drag-in-progress: use absolute position for smooth tracking
			if (_isMouseDragging && args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				SetValueFromMousePosition(args, isVertical);
				args.Handled = true;
				return true;
			}

			// Handle drag end
			if (args.HasFlag(MouseFlags.Button1Released) && _isMouseDragging)
			{
				_isMouseDragging = false;
				_isDragging = false;
				Container?.Invalidate(true);
				args.Handled = true;
				return true;
			}

			// Handle press to start drag or jump to position
			if (args.HasFlag(MouseFlags.Button1Pressed) && !_isMouseDragging)
			{
				if (!HasFocus)
				{
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				}

				// Calculate position on track
				int trackStart = GetTrackStart();
				int trackLength = GetTrackLength();
				int clickPos = isVertical ? args.Position.Y : args.Position.X;
				int trackPos = clickPos - trackStart;

				if (trackLength > 0 && trackPos >= 0 && trackPos < trackLength)
				{
					// Check if click is near the thumb
					int thumbPos = SliderRenderingHelper.ValueToPosition(_value, _minValue, _maxValue, trackLength);
					int distance = Math.Abs(trackPos - thumbPos);

					if (distance > ControlDefaults.SliderThumbHitRadius)
					{
						// Jump to clicked position
						SetValueFromTrackPosition(trackPos, trackLength, isVertical);
					}

					// Start drag (whether on thumb or after jump)
					_isMouseDragging = true;
					_isDragging = true;
				}

				args.Handled = true;
				Container?.Invalidate(true);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Sets the value based on the current absolute mouse position on the track.
		/// Uses absolute positioning instead of delta-based for smooth, responsive tracking.
		/// </summary>
		private void SetValueFromMousePosition(MouseEventArgs args, bool isVertical)
		{
			int trackStart = GetTrackStartAbsolute();
			int trackLength = GetTrackLength();
			if (trackLength <= 0) return;

			// Use window-relative position for absolute coordinate
			int mousePos = isVertical ? args.WindowPosition.Y : args.WindowPosition.X;
			int trackPos = Math.Clamp(mousePos - trackStart, 0, trackLength - 1);

			SetValueFromTrackPosition(trackPos, trackLength, isVertical);
		}

		/// <summary>
		/// Converts a track position to a value and sets it.
		/// </summary>
		private void SetValueFromTrackPosition(int trackPos, int trackLength, bool isVertical)
		{
			double newValue;
			if (isVertical)
			{
				newValue = SliderRenderingHelper.PositionToValue(trackLength - 1 - trackPos, _minValue, _maxValue, trackLength);
			}
			else
			{
				newValue = SliderRenderingHelper.PositionToValue(trackPos, _minValue, _maxValue, trackLength);
			}
			Value = SliderRenderingHelper.SnapToStep(newValue, _minValue, _maxValue, _step);
		}

		/// <summary>
		/// Gets the absolute (window-relative) X or Y of the track start,
		/// using the last layout bounds for the absolute offset.
		/// </summary>
		private int GetTrackStartAbsolute()
		{
			if (_orientation == SliderOrientation.Horizontal)
			{
				int start = _lastLayoutBounds.X + Margin.Left;
				if (_showMinMaxLabels)
					start += FormatValue(_minValue).Length + ControlDefaults.SliderLabelSpacing;
				return start;
			}
			else
			{
				int start = _lastLayoutBounds.Y + Margin.Top;
				if (_showMinMaxLabels)
					start += 1;
				return start;
			}
		}

		private int GetTrackStart()
		{
			if (_orientation == SliderOrientation.Horizontal)
			{
				int start = Margin.Left;
				if (_showMinMaxLabels)
					start += FormatValue(_minValue).Length + ControlDefaults.SliderLabelSpacing;
				return start;
			}
			else
			{
				int start = Margin.Top;
				if (_showMinMaxLabels)
					start += FormatValue(_maxValue).Length > 0 ? 1 : 0; // max label row
				return start;
			}
		}

		private int GetTrackLength()
		{
			if (_lastLayoutBounds.Width <= 0 && _lastLayoutBounds.Height <= 0)
				return ControlDefaults.SliderMinTrackLength;

			if (_orientation == SliderOrientation.Horizontal)
			{
				int available = _lastLayoutBounds.Width - Margin.Left - Margin.Right;
				if (_showMinMaxLabels)
				{
					available -= FormatValue(_minValue).Length + FormatValue(_maxValue).Length +
						ControlDefaults.SliderLabelSpacing * 2;
				}
				if (_showValueLabel)
				{
					available -= FormatValue(_maxValue).Length + ControlDefaults.SliderLabelSpacing;
				}
				return Math.Max(ControlDefaults.SliderMinTrackLength, available);
			}
			else
			{
				int available = _lastLayoutBounds.Height - Margin.Top - Margin.Bottom;
				if (_showMinMaxLabels)
					available -= 2; // min and max label rows
				return Math.Max(ControlDefaults.SliderMinTrackLength, available);
			}
		}
	}
}
