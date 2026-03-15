// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Fluent builder for creating <see cref="RangeSliderControl"/> instances.
	/// </summary>
	public sealed class RangeSliderBuilder : IControlBuilder<RangeSliderControl>
	{
		private readonly RangeSliderControl _control;

		/// <summary>
		/// Initializes a new instance of the <see cref="RangeSliderBuilder"/> class.
		/// </summary>
		public RangeSliderBuilder()
		{
			_control = new RangeSliderControl();
		}

		#region Value Methods

		/// <summary>
		/// Sets the low value.
		/// </summary>
		public RangeSliderBuilder WithLowValue(double value)
		{
			_control.LowValue = value;
			return this;
		}

		/// <summary>
		/// Sets the high value.
		/// </summary>
		public RangeSliderBuilder WithHighValue(double value)
		{
			_control.HighValue = value;
			return this;
		}

		/// <summary>
		/// Sets both low and high values.
		/// </summary>
		public RangeSliderBuilder WithValues(double low, double high)
		{
			_control.LowValue = low;
			_control.HighValue = high;
			return this;
		}

		/// <summary>
		/// Sets the min and max range.
		/// </summary>
		public RangeSliderBuilder WithRange(double min, double max)
		{
			_control.MinValue = min;
			_control.MaxValue = max;
			return this;
		}

		/// <summary>
		/// Sets the minimum required gap between low and high values.
		/// </summary>
		public RangeSliderBuilder WithMinRange(double minRange)
		{
			_control.MinRange = minRange;
			return this;
		}

		/// <summary>
		/// Sets the step increment.
		/// </summary>
		public RangeSliderBuilder WithStep(double step)
		{
			_control.Step = step;
			return this;
		}

		/// <summary>
		/// Sets the large step increment.
		/// </summary>
		public RangeSliderBuilder WithLargeStep(double largeStep)
		{
			_control.LargeStep = largeStep;
			return this;
		}

		#endregion

		#region Orientation Methods

		/// <summary>
		/// Sets the slider to horizontal orientation.
		/// </summary>
		public RangeSliderBuilder Horizontal()
		{
			_control.Orientation = SliderOrientation.Horizontal;
			return this;
		}

		/// <summary>
		/// Sets the slider to vertical orientation.
		/// </summary>
		public RangeSliderBuilder Vertical()
		{
			_control.Orientation = SliderOrientation.Vertical;
			return this;
		}

		#endregion

		#region Display Methods

		/// <summary>
		/// Enables showing the range value label.
		/// </summary>
		public RangeSliderBuilder ShowValueLabel(bool show = true)
		{
			_control.ShowValueLabel = show;
			return this;
		}

		/// <summary>
		/// Enables showing min and max labels at the track ends.
		/// </summary>
		public RangeSliderBuilder ShowMinMaxLabels(bool show = true)
		{
			_control.ShowMinMaxLabels = show;
			return this;
		}

		/// <summary>
		/// Sets the format string for value labels.
		/// </summary>
		public RangeSliderBuilder WithValueFormat(string format)
		{
			_control.ValueLabelFormat = format;
			return this;
		}

		#endregion

		#region Color Methods

		/// <summary>
		/// Sets the unfilled track color.
		/// </summary>
		public RangeSliderBuilder WithTrackColor(Color color)
		{
			_control.TrackColor = color;
			return this;
		}

		/// <summary>
		/// Sets the filled track color (between thumbs).
		/// </summary>
		public RangeSliderBuilder WithFilledTrackColor(Color color)
		{
			_control.FilledTrackColor = color;
			return this;
		}

		/// <summary>
		/// Sets the inactive thumb color.
		/// </summary>
		public RangeSliderBuilder WithThumbColor(Color color)
		{
			_control.ThumbColor = color;
			return this;
		}

		/// <summary>
		/// Sets the focused/active thumb color.
		/// </summary>
		public RangeSliderBuilder WithFocusedThumbColor(Color color)
		{
			_control.FocusedThumbColor = color;
			return this;
		}

		/// <summary>
		/// Sets the background color.
		/// </summary>
		public RangeSliderBuilder WithBackgroundColor(Color color)
		{
			_control.BackgroundColor = color;
			return this;
		}

		#endregion

		#region Event Methods

		/// <summary>
		/// Registers a handler for low value changes.
		/// </summary>
		public RangeSliderBuilder OnLowValueChanged(EventHandler<double> handler)
		{
			_control.LowValueChanged += handler;
			return this;
		}

		/// <summary>
		/// Registers a handler for high value changes.
		/// </summary>
		public RangeSliderBuilder OnHighValueChanged(EventHandler<double> handler)
		{
			_control.HighValueChanged += handler;
			return this;
		}

		/// <summary>
		/// Registers a handler for range changes (fires with both low and high values).
		/// </summary>
		public RangeSliderBuilder OnRangeChanged(EventHandler<(double Low, double High)> handler)
		{
			_control.RangeChanged += handler;
			return this;
		}

		#endregion

		#region Standard Control Methods

		/// <summary>
		/// Sets the control name.
		/// </summary>
		public RangeSliderBuilder WithName(string name)
		{
			_control.Name = name;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public RangeSliderBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_control.Margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public RangeSliderBuilder WithMargin(Margin margin)
		{
			_control.Margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment.
		/// </summary>
		public RangeSliderBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_control.HorizontalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment.
		/// </summary>
		public RangeSliderBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_control.VerticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the width.
		/// </summary>
		public RangeSliderBuilder WithWidth(int width)
		{
			_control.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the height (useful for vertical sliders).
		/// </summary>
		public RangeSliderBuilder WithHeight(int height)
		{
			_control.Height = height;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		public RangeSliderBuilder Visible(bool visible)
		{
			_control.Visible = visible;
			return this;
		}

		/// <summary>
		/// Sets the control to stretch to fill available width.
		/// </summary>
		public RangeSliderBuilder Stretch()
		{
			_control.HorizontalAlignment = HorizontalAlignment.Stretch;
			return this;
		}

		/// <summary>
		/// Sets vertical alignment to Fill (useful for vertical sliders in a container).
		/// </summary>
		public RangeSliderBuilder Fill()
		{
			_control.VerticalAlignment = VerticalAlignment.Fill;
			return this;
		}

		/// <summary>
		/// Sets an arbitrary tag object.
		/// </summary>
		public RangeSliderBuilder WithTag(object tag)
		{
			_control.Tag = tag;
			return this;
		}

		#endregion

		/// <summary>
		/// Builds the RangeSliderControl instance.
		/// </summary>
		public RangeSliderControl Build()
		{
			BindingHelper.ApplyDeferredBindings(this, _control);
			return _control;
		}
	}
}
