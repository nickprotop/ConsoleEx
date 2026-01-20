// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Fluent builder for creating ProgressBarControl instances.
	/// </summary>
	public class ProgressBarBuilder
	{
		private readonly ProgressBarControl _control;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgressBarBuilder"/> class.
		/// </summary>
		public ProgressBarBuilder()
		{
			_control = new ProgressBarControl();
		}

		#region Value Methods

		/// <summary>
		/// Sets the current value.
		/// </summary>
		public ProgressBarBuilder WithValue(double value)
		{
			_control.Value = value;
			return this;
		}

		/// <summary>
		/// Sets the maximum value (100% fill).
		/// </summary>
		public ProgressBarBuilder WithMaxValue(double maxValue)
		{
			_control.MaxValue = maxValue;
			return this;
		}

		/// <summary>
		/// Sets the progress as a percentage (sets MaxValue=100 and Value).
		/// </summary>
		public ProgressBarBuilder WithPercentage(double percentage)
		{
			_control.MaxValue = 100;
			_control.Value = percentage;
			return this;
		}

		#endregion

		#region Mode Methods

		/// <summary>
		/// Enables or disables indeterminate (pulsing) mode.
		/// </summary>
		public ProgressBarBuilder Indeterminate(bool indeterminate = true)
		{
			_control.IsIndeterminate = indeterminate;
			return this;
		}

		/// <summary>
		/// Sets the animation interval in milliseconds for indeterminate mode.
		/// </summary>
		public ProgressBarBuilder WithAnimationInterval(int milliseconds)
		{
			_control.AnimationInterval = milliseconds;
			return this;
		}

		/// <summary>
		/// Sets the width of the pulse segment in indeterminate mode.
		/// </summary>
		public ProgressBarBuilder WithPulseWidth(int width)
		{
			_control.PulseWidth = width;
			return this;
		}

		#endregion

		#region Header Methods

		/// <summary>
		/// Sets the header text and enables header display.
		/// </summary>
		public ProgressBarBuilder WithHeader(string header)
		{
			_control.Header = header;
			_control.ShowHeader = true;
			return this;
		}

		/// <summary>
		/// Sets whether to show the header.
		/// </summary>
		public ProgressBarBuilder ShowHeader(bool show = true)
		{
			_control.ShowHeader = show;
			return this;
		}

		#endregion

		#region Appearance Methods

		/// <summary>
		/// Sets the bar width in characters.
		/// </summary>
		public ProgressBarBuilder WithBarWidth(int width)
		{
			_control.BarWidth = width;
			return this;
		}

		/// <summary>
		/// Sets the bar to stretch to fill available width.
		/// </summary>
		public ProgressBarBuilder Stretch()
		{
			_control.BarWidth = null;
			_control.HorizontalAlignment = HorizontalAlignment.Stretch;
			return this;
		}

		/// <summary>
		/// Sets whether to show the percentage text.
		/// </summary>
		public ProgressBarBuilder ShowPercentage(bool show = true)
		{
			_control.ShowPercentage = show;
			return this;
		}

		/// <summary>
		/// Sets the filled color.
		/// </summary>
		public ProgressBarBuilder WithFilledColor(Color color)
		{
			_control.FilledColor = color;
			return this;
		}

		/// <summary>
		/// Sets the unfilled color.
		/// </summary>
		public ProgressBarBuilder WithUnfilledColor(Color color)
		{
			_control.UnfilledColor = color;
			return this;
		}

		/// <summary>
		/// Sets both filled and unfilled colors.
		/// </summary>
		public ProgressBarBuilder WithColors(Color filled, Color unfilled)
		{
			_control.FilledColor = filled;
			_control.UnfilledColor = unfilled;
			return this;
		}

		/// <summary>
		/// Sets the percentage text color.
		/// </summary>
		public ProgressBarBuilder WithPercentageColor(Color color)
		{
			_control.PercentageColor = color;
			return this;
		}

		/// <summary>
		/// Sets the background color.
		/// </summary>
		public ProgressBarBuilder WithBackgroundColor(Color color)
		{
			_control.BackgroundColor = color;
			return this;
		}

		#endregion

		#region Standard Control Methods

		/// <summary>
		/// Sets the control name.
		/// </summary>
		public ProgressBarBuilder WithName(string name)
		{
			_control.Name = name;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public ProgressBarBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_control.Margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public ProgressBarBuilder WithMargin(Margin margin)
		{
			_control.Margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment.
		/// </summary>
		public ProgressBarBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_control.HorizontalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment.
		/// </summary>
		public ProgressBarBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_control.VerticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the width.
		/// </summary>
		public ProgressBarBuilder WithWidth(int width)
		{
			_control.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		public ProgressBarBuilder Visible(bool visible)
		{
			_control.Visible = visible;
			return this;
		}

		/// <summary>
		/// Sets an arbitrary tag object.
		/// </summary>
		public ProgressBarBuilder WithTag(object tag)
		{
			_control.Tag = tag;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the top during scrolling.
		/// </summary>
		public ProgressBarBuilder StickyTop()
		{
			_control.StickyPosition = StickyPosition.Top;
			return this;
		}

		/// <summary>
		/// Makes the control stick to the bottom during scrolling.
		/// </summary>
		public ProgressBarBuilder StickyBottom()
		{
			_control.StickyPosition = StickyPosition.Bottom;
			return this;
		}

		#endregion

		/// <summary>
		/// Builds the ProgressBarControl instance.
		/// </summary>
		public ProgressBarControl Build()
		{
			return _control;
		}
	}
}
