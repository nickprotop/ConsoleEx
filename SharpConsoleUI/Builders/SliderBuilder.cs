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
	/// Fluent builder for creating <see cref="SliderControl"/> instances.
	/// </summary>
	public sealed class SliderBuilder : IControlBuilder<SliderControl>
	{
		private readonly SliderControl _control;

		/// <summary>
		/// Initializes a new instance of the <see cref="SliderBuilder"/> class.
		/// </summary>
		public SliderBuilder()
		{
			_control = new SliderControl();
		}

		#region Value Methods

		/// <summary>
		/// Sets the current value.
		/// </summary>
		public SliderBuilder WithValue(double value)
		{
			_control.Value = value;
			return this;
		}

		/// <summary>
		/// Sets the min and max range.
		/// </summary>
		public SliderBuilder WithRange(double min, double max)
		{
			_control.MinValue = min;
			_control.MaxValue = max;
			return this;
		}

		/// <summary>
		/// Sets the step increment.
		/// </summary>
		public SliderBuilder WithStep(double step)
		{
			_control.Step = step;
			return this;
		}

		/// <summary>
		/// Sets the large step increment (Page Up/Down, Shift+Arrow).
		/// </summary>
		public SliderBuilder WithLargeStep(double largeStep)
		{
			_control.LargeStep = largeStep;
			return this;
		}

		#endregion

		#region Orientation Methods

		/// <summary>
		/// Sets the slider to horizontal orientation.
		/// </summary>
		public SliderBuilder Horizontal()
		{
			_control.Orientation = SliderOrientation.Horizontal;
			return this;
		}

		/// <summary>
		/// Sets the slider to vertical orientation.
		/// </summary>
		public SliderBuilder Vertical()
		{
			_control.Orientation = SliderOrientation.Vertical;
			return this;
		}

		#endregion

		#region Display Methods

		/// <summary>
		/// Enables showing the current value label.
		/// </summary>
		public SliderBuilder ShowValueLabel(bool show = true)
		{
			_control.ShowValueLabel = show;
			return this;
		}

		/// <summary>
		/// Enables showing min and max labels at the track ends.
		/// </summary>
		public SliderBuilder ShowMinMaxLabels(bool show = true)
		{
			_control.ShowMinMaxLabels = show;
			return this;
		}

		/// <summary>
		/// Sets the format string for value labels.
		/// </summary>
		public SliderBuilder WithValueFormat(string format)
		{
			_control.ValueLabelFormat = format;
			return this;
		}

		#endregion

		#region Color Methods

		/// <summary>
		/// Sets the unfilled track color.
		/// </summary>
		public SliderBuilder WithTrackColor(Color color)
		{
			_control.TrackColor = color;
			return this;
		}

		/// <summary>
		/// Sets the filled track color.
		/// </summary>
		public SliderBuilder WithFilledTrackColor(Color color)
		{
			_control.FilledTrackColor = color;
			return this;
		}

		/// <summary>
		/// Sets the thumb color.
		/// </summary>
		public SliderBuilder WithThumbColor(Color color)
		{
			_control.ThumbColor = color;
			return this;
		}

		/// <summary>
		/// Sets the focused thumb color.
		/// </summary>
		public SliderBuilder WithFocusedThumbColor(Color color)
		{
			_control.FocusedThumbColor = color;
			return this;
		}

		/// <summary>
		/// Sets the background color.
		/// </summary>
		public SliderBuilder WithBackgroundColor(Color color)
		{
			_control.BackgroundColor = color;
			return this;
		}

		#endregion

		#region Event Methods

		/// <summary>
		/// Registers a handler for value changes.
		/// </summary>
		public SliderBuilder OnValueChanged(EventHandler<double> handler)
		{
			_control.ValueChanged += handler;
			return this;
		}

		#endregion

		#region Standard Control Methods

		/// <summary>
		/// Sets the control name.
		/// </summary>
		public SliderBuilder WithName(string name)
		{
			_control.Name = name;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public SliderBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_control.Margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public SliderBuilder WithMargin(Margin margin)
		{
			_control.Margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment.
		/// </summary>
		public SliderBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_control.HorizontalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment.
		/// </summary>
		public SliderBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_control.VerticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the width.
		/// </summary>
		public SliderBuilder WithWidth(int width)
		{
			_control.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the height (useful for vertical sliders).
		/// </summary>
		public SliderBuilder WithHeight(int height)
		{
			_control.Height = height;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		public SliderBuilder Visible(bool visible)
		{
			_control.Visible = visible;
			return this;
		}

		/// <summary>
		/// Sets the control to stretch to fill available width.
		/// </summary>
		public SliderBuilder Stretch()
		{
			_control.HorizontalAlignment = HorizontalAlignment.Stretch;
			return this;
		}

		/// <summary>
		/// Sets vertical alignment to Fill (useful for vertical sliders in a container).
		/// </summary>
		public SliderBuilder Fill()
		{
			_control.VerticalAlignment = VerticalAlignment.Fill;
			return this;
		}

		/// <summary>
		/// Sets an arbitrary tag object.
		/// </summary>
		public SliderBuilder WithTag(object tag)
		{
			_control.Tag = tag;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the top during scrolling.
		/// </summary>
		public SliderBuilder StickyTop()
		{
			_control.StickyPosition = StickyPosition.Top;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the bottom during scrolling.
		/// </summary>
		public SliderBuilder StickyBottom()
		{
			_control.StickyPosition = StickyPosition.Bottom;
			return this;
		}

		#endregion

		/// <summary>
		/// Builds the SliderControl instance.
		/// </summary>
		public SliderControl Build()
		{
			BindingHelper.ApplyDeferredBindings(this, _control);
			return _control;
		}
	}
}
