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
	/// Fluent builder for creating BarGraphControl instances.
	/// </summary>
	public class BarGraphBuilder
	{
		private readonly BarGraphControl _control;

		/// <summary>
		/// Initializes a new instance of the <see cref="BarGraphBuilder"/> class.
		/// </summary>
		public BarGraphBuilder()
		{
			_control = new BarGraphControl();
		}

		/// <summary>
		/// Sets the label text.
		/// </summary>
		public BarGraphBuilder WithLabel(string label)
		{
			_control.Label = label;
			return this;
		}

		/// <summary>
		/// Sets the fixed width for the label column in characters.
		/// When set, all labels are padded or truncated to this width, ensuring bars align vertically.
		/// Use this when displaying multiple bar graphs to align them.
		/// </summary>
		public BarGraphBuilder WithLabelWidth(int width)
		{
			_control.LabelWidth = width;
			return this;
		}

		/// <summary>
		/// Sets the current value.
		/// </summary>
		public BarGraphBuilder WithValue(double value)
		{
			_control.Value = value;
			return this;
		}

		/// <summary>
		/// Sets the maximum value (100% fill).
		/// </summary>
		public BarGraphBuilder WithMaxValue(double maxValue)
		{
			_control.MaxValue = maxValue;
			return this;
		}

		/// <summary>
		/// Sets the bar width in characters.
		/// </summary>
		public BarGraphBuilder WithBarWidth(int width)
		{
			_control.BarWidth = width;
			return this;
		}

		/// <summary>
		/// Sets the filled color.
		/// </summary>
		public BarGraphBuilder WithFilledColor(Color color)
		{
			_control.FilledColor = color;
			return this;
		}

		/// <summary>
		/// Sets the unfilled color.
		/// </summary>
		public BarGraphBuilder WithUnfilledColor(Color color)
		{
			_control.UnfilledColor = color;
			return this;
		}

		/// <summary>
		/// Sets both filled and unfilled colors.
		/// </summary>
		public BarGraphBuilder WithColors(Color filled, Color unfilled)
		{
			_control.FilledColor = filled;
			_control.UnfilledColor = unfilled;
			return this;
		}

		/// <summary>
		/// Sets the background color.
		/// </summary>
		public BarGraphBuilder WithBackgroundColor(Color color)
		{
			_control.BackgroundColor = color;
			return this;
		}

		/// <summary>
		/// Sets the foreground color for labels and values.
		/// </summary>
		public BarGraphBuilder WithForegroundColor(Color color)
		{
			_control.ForegroundColor = color;
			return this;
		}

		/// <summary>
		/// Sets whether to show the label.
		/// </summary>
		public BarGraphBuilder ShowLabel(bool show = true)
		{
			_control.ShowLabel = show;
			return this;
		}

		/// <summary>
		/// Sets whether to show the value.
		/// </summary>
		public BarGraphBuilder ShowValue(bool show = true)
		{
			_control.ShowValue = show;
			return this;
		}

		/// <summary>
		/// Sets the value format string.
		/// </summary>
		public BarGraphBuilder WithValueFormat(string format)
		{
			_control.ValueFormat = format;
			return this;
		}

		/// <summary>
		/// Sets the control name.
		/// </summary>
		public BarGraphBuilder WithName(string name)
		{
			_control.Name = name;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public BarGraphBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_control.Margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public BarGraphBuilder WithMargin(Margin margin)
		{
			_control.Margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment.
		/// </summary>
		public BarGraphBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_control.HorizontalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment.
		/// </summary>
		public BarGraphBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_control.VerticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the width.
		/// </summary>
		public BarGraphBuilder WithWidth(int width)
		{
			_control.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		public BarGraphBuilder Visible(bool visible)
		{
			_control.Visible = visible;
			return this;
		}

		/// <summary>
		/// Sets color thresholds for gradient effect.
		/// Colors apply when bar percentage meets or exceeds the threshold.
		/// </summary>
		public BarGraphBuilder WithGradient(params ColorThreshold[] thresholds)
		{
			_control.SetColorThresholds(thresholds);
			return this;
		}

		/// <summary>
		/// Sets a standard green/yellow/red gradient at 0%, 50%, and 80% thresholds.
		/// Green for 0-49%, Yellow for 50-79%, Red for 80%+.
		/// </summary>
		public BarGraphBuilder WithStandardGradient()
		{
			_control.SetColorThresholds(
				new ColorThreshold(0, Color.Green),
				new ColorThreshold(50, Color.Yellow),
				new ColorThreshold(80, Color.Red)
			);
			return this;
		}

		/// <summary>
		/// Builds the BarGraphControl instance.
		/// </summary>
		public BarGraphControl Build()
		{
			return _control;
		}
	}
}
