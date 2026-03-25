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
	public partial class RangeSliderControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !HasFocus)
				return false;

			bool isVertical = _orientation == SliderOrientation.Vertical;

			// Tab switches active thumb: Low→High is handled internally,
			// High→next control (or Shift+Tab: High→Low internally, Low→previous control)
			if (key.Key == ConsoleKey.Tab && !key.Modifiers.HasFlag(ConsoleModifiers.Control))
			{
				bool backward = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
				if (!backward && _activeThumb == ActiveThumb.Low)
				{
					// Forward Tab on Low thumb → switch to High thumb
					ActiveThumb = ActiveThumb.High;
					Container?.Invalidate(true);
					return true;
				}
				else if (backward && _activeThumb == ActiveThumb.High)
				{
					// Shift+Tab on High thumb → switch to Low thumb
					ActiveThumb = ActiveThumb.Low;
					Container?.Invalidate(true);
					return true;
				}
				// Otherwise let Tab/Shift+Tab pass through to move focus to next/previous control
				return false;
			}

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
					if (_activeThumb == ActiveThumb.Low)
						LowValue = _minValue;
					else
						HighValue = _lowValue + _minRange;
					return true;

				case ConsoleKey.End:
					if (_activeThumb == ActiveThumb.Low)
						LowValue = _highValue - _minRange;
					else
						HighValue = _maxValue;
					return true;

				default:
					return false;
			}

			if (handled && Math.Abs(delta) > 0)
			{
				if (_activeThumb == ActiveThumb.Low)
					LowValue = _lowValue + delta;
				else
					HighValue = _highValue + delta;
			}

			return handled;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled) return false;

			if (args.HasAnyFlag(MouseFlags.MouseEnter, MouseFlags.MouseLeave))
				return false;

			bool isVertical = _orientation == SliderOrientation.Vertical;

			// Handle drag-in-progress: use absolute position for smooth tracking
			if (_isMouseDragging && args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				SetActiveThumbFromMousePosition(args, isVertical);
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

			// Handle press to start drag or jump
			if (args.HasFlag(MouseFlags.Button1Pressed) && !_isMouseDragging)
			{
				if (!HasFocus)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				int trackStart = GetTrackStart();
				int trackLength = GetTrackLength();
				int clickPos = isVertical ? args.Position.Y : args.Position.X;
				int trackPos = clickPos - trackStart;

				if (trackLength > 0 && trackPos >= 0 && trackPos < trackLength)
				{
					int lowThumbPos = SliderRenderingHelper.ValueToPosition(_lowValue, _minValue, _maxValue, trackLength);
					int highThumbPos = SliderRenderingHelper.ValueToPosition(_highValue, _minValue, _maxValue, trackLength);

					if (isVertical)
					{
						lowThumbPos = trackLength - 1 - lowThumbPos;
						highThumbPos = trackLength - 1 - highThumbPos;
					}

					int distToLow = Math.Abs(trackPos - lowThumbPos);
					int distToHigh = Math.Abs(trackPos - highThumbPos);

					// If thumbs overlap, prefer high thumb
					if (distToLow <= ControlDefaults.SliderThumbHitRadius && distToHigh <= ControlDefaults.SliderThumbHitRadius)
					{
						_activeThumb = ActiveThumb.High;
					}
					else if (distToLow <= ControlDefaults.SliderThumbHitRadius)
					{
						_activeThumb = ActiveThumb.Low;
					}
					else if (distToHigh <= ControlDefaults.SliderThumbHitRadius)
					{
						_activeThumb = ActiveThumb.High;
					}
					else
					{
						// Click outside both thumbs - jump nearest
						double newValue = isVertical
							? SliderRenderingHelper.PositionToValue(trackLength - 1 - trackPos, _minValue, _maxValue, trackLength)
							: SliderRenderingHelper.PositionToValue(trackPos, _minValue, _maxValue, trackLength);
						double snapped = SliderRenderingHelper.SnapToStep(newValue, _minValue, _maxValue, _step);

						if (distToLow < distToHigh)
						{
							_activeThumb = ActiveThumb.Low;
							LowValue = snapped;
						}
						else
						{
							_activeThumb = ActiveThumb.High;
							HighValue = snapped;
						}
					}

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
		/// Sets the active thumb's value based on absolute mouse position for smooth tracking.
		/// </summary>
		private void SetActiveThumbFromMousePosition(MouseEventArgs args, bool isVertical)
		{
			int trackStart = GetTrackStartAbsolute();
			int trackLength = GetTrackLength();
			if (trackLength <= 0) return;

			int mousePos = isVertical ? args.WindowPosition.Y : args.WindowPosition.X;
			int trackPos = Math.Clamp(mousePos - trackStart, 0, trackLength - 1);

			double newValue;
			if (isVertical)
				newValue = SliderRenderingHelper.PositionToValue(trackLength - 1 - trackPos, _minValue, _maxValue, trackLength);
			else
				newValue = SliderRenderingHelper.PositionToValue(trackPos, _minValue, _maxValue, trackLength);

			double snapped = SliderRenderingHelper.SnapToStep(newValue, _minValue, _maxValue, _step);

			if (_activeThumb == ActiveThumb.Low)
				LowValue = snapped;
			else
				HighValue = snapped;
		}

		/// <summary>
		/// Gets the absolute (window-relative) track start position.
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
				if (_showMinMaxLabels) start += 1;
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
				if (_showMinMaxLabels) start += 1;
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
					available -= FormatValue(_maxValue).Length * 2 + 1 + ControlDefaults.SliderLabelSpacing;
				}
				return Math.Max(ControlDefaults.SliderMinTrackLength, available);
			}
			else
			{
				int available = _lastLayoutBounds.Height - Margin.Top - Margin.Bottom;
				if (_showMinMaxLabels) available -= 2;
				return Math.Max(ControlDefaults.SliderMinTrackLength, available);
			}
		}
	}
}
