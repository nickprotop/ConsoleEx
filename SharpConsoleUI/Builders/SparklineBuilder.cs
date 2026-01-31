// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Fluent builder for creating SparklineControl instances.
	/// </summary>
	public class SparklineBuilder
	{
		private readonly SparklineControl _control;

		/// <summary>
		/// Initializes a new instance of the <see cref="SparklineBuilder"/> class.
		/// </summary>
		public SparklineBuilder()
		{
			_control = new SparklineControl();
		}

		/// <summary>
		/// Sets the graph height in lines.
		/// </summary>
		public SparklineBuilder WithHeight(int height)
		{
			_control.GraphHeight = height;
			return this;
		}

		/// <summary>
		/// Sets the maximum value for the graph scale.
		/// </summary>
		public SparklineBuilder WithMaxValue(double maxValue)
		{
			_control.MaxValue = maxValue;
			return this;
		}

		/// <summary>
		/// Sets the minimum value for the graph scale.
		/// </summary>
		public SparklineBuilder WithMinValue(double minValue)
		{
			_control.MinValue = minValue;
			return this;
		}

		/// <summary>
		/// Sets the bar color.
		/// </summary>
		public SparklineBuilder WithBarColor(Color color)
		{
			_control.BarColor = color;
			return this;
		}

		/// <summary>
		/// Sets the background color.
		/// </summary>
		public SparklineBuilder WithBackgroundColor(Color color)
		{
			_control.BackgroundColor = color;
			return this;
		}

		/// <summary>
		/// Sets the border style.
		/// </summary>
		public SparklineBuilder WithBorder(BorderStyle style)
		{
			_control.BorderStyle = style;
			return this;
		}

		/// <summary>
		/// Sets the border style and color.
		/// </summary>
		public SparklineBuilder WithBorder(BorderStyle style, Color color)
		{
			_control.BorderStyle = style;
			_control.BorderColor = color;
			return this;
		}

		/// <summary>
		/// Sets the border color.
		/// </summary>
		public SparklineBuilder WithBorderColor(Color color)
		{
			_control.BorderColor = color;
			return this;
		}

		/// <summary>
		/// Sets the title.
		/// </summary>
		public SparklineBuilder WithTitle(string title)
		{
			_control.Title = title;
			return this;
		}

		/// <summary>
		/// Sets the title and color.
		/// </summary>
		public SparklineBuilder WithTitle(string title, Color color)
		{
			_control.Title = title;
			_control.TitleColor = color;
			return this;
		}

		/// <summary>
		/// Sets the title color.
		/// </summary>
		public SparklineBuilder WithTitleColor(Color color)
		{
			_control.TitleColor = color;
			return this;
		}

		/// <summary>
		/// Sets the title position (Top or Bottom).
		/// </summary>
		public SparklineBuilder WithTitlePosition(TitlePosition position)
		{
			_control.TitlePosition = position;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of data points to keep.
		/// </summary>
		public SparklineBuilder WithMaxDataPoints(int maxPoints)
		{
			_control.MaxDataPoints = maxPoints;
			return this;
		}

		/// <summary>
		/// Sets the initial data points.
		/// </summary>
		public SparklineBuilder WithData(IEnumerable<double> dataPoints)
		{
			_control.SetDataPoints(dataPoints);
			return this;
		}

		/// <summary>
		/// Sets the secondary data points (for bidirectional mode).
		/// </summary>
		public SparklineBuilder WithSecondaryData(IEnumerable<double> dataPoints)
		{
			_control.SetSecondaryDataPoints(dataPoints);
			return this;
		}

		/// <summary>
		/// Sets the secondary bar color (for bidirectional mode).
		/// </summary>
		public SparklineBuilder WithSecondaryBarColor(Color color)
		{
			_control.SecondaryBarColor = color;
			return this;
		}

		/// <summary>
		/// Sets the secondary max value (for bidirectional mode).
		/// </summary>
		public SparklineBuilder WithSecondaryMaxValue(double maxValue)
		{
			_control.SecondaryMaxValue = maxValue;
			return this;
		}

		/// <summary>
		/// Sets both primary and secondary data at once (for bidirectional mode).
		/// </summary>
		public SparklineBuilder WithBidirectionalData(IEnumerable<double> primaryData, IEnumerable<double> secondaryData)
		{
			_control.SetBidirectionalData(primaryData, secondaryData);
			return this;
		}

		/// <summary>
		/// Sets the control name.
		/// </summary>
		public SparklineBuilder WithName(string name)
		{
			_control.Name = name;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public SparklineBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_control.Margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public SparklineBuilder WithMargin(Margin margin)
		{
			_control.Margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment.
		/// </summary>
		public SparklineBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_control.HorizontalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment.
		/// </summary>
		public SparklineBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_control.VerticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the width.
		/// </summary>
		public SparklineBuilder WithWidth(int width)
		{
			_control.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		public SparklineBuilder Visible(bool visible)
		{
			_control.Visible = visible;
			return this;
		}

		/// <summary>
		/// Sets the rendering mode (Block or Braille).
		/// Block uses 9-level block characters, Braille uses 5-level braille patterns.
		/// </summary>
		public SparklineBuilder WithMode(SparklineMode mode)
		{
			_control.Mode = mode;
			return this;
		}

		/// <summary>
		/// Sets the color gradient for vertical color interpolation.
		/// </summary>
		public SparklineBuilder WithGradient(ColorGradient gradient)
		{
			_control.Gradient = gradient;
			return this;
		}

		/// <summary>
		/// Sets the color gradient from a gradient specification string.
		/// Supports predefined gradients (cool, warm, spectrum, grayscale),
		/// arrow notation (blue→cyan→green), and :reverse suffix.
		/// </summary>
		public SparklineBuilder WithGradient(string gradientSpec)
		{
			_control.Gradient = ColorGradient.Parse(gradientSpec);
			return this;
		}

		/// <summary>
		/// Sets the color gradient from an array of colors.
		/// </summary>
		public SparklineBuilder WithGradient(params Color[] colors)
		{
			_control.Gradient = ColorGradient.FromColors(colors);
			return this;
		}

		/// <summary>
		/// Sets the secondary color gradient for bidirectional mode.
		/// </summary>
		public SparklineBuilder WithSecondaryGradient(ColorGradient gradient)
		{
			_control.SecondaryGradient = gradient;
			return this;
		}

		/// <summary>
		/// Sets the secondary color gradient from a gradient specification string.
		/// Supports predefined gradients (cool, warm, spectrum, grayscale),
		/// arrow notation (blue→cyan→green), and :reverse suffix.
		/// </summary>
		public SparklineBuilder WithSecondaryGradient(string gradientSpec)
		{
			_control.SecondaryGradient = ColorGradient.Parse(gradientSpec);
			return this;
		}

		/// <summary>
		/// Sets the secondary color gradient from an array of colors.
		/// </summary>
		public SparklineBuilder WithSecondaryGradient(params Color[] colors)
		{
			_control.SecondaryGradient = ColorGradient.FromColors(colors);
			return this;
		}

		/// <summary>
		/// Enables or disables the dotted baseline with optional customization.
		/// </summary>
		/// <param name="show">Whether to show the baseline.</param>
		/// <param name="baselineChar">Character to use for the baseline (default: ┈).</param>
		/// <param name="color">Color for the baseline (default: Grey50).</param>
		/// <param name="position">Position of the baseline (Top or Bottom, default: Bottom).</param>
		public SparklineBuilder WithBaseline(bool show = true, char baselineChar = '┈', Color? color = null, TitlePosition position = TitlePosition.Bottom)
		{
			_control.ShowBaseline = show;
			_control.BaselineChar = baselineChar;
			_control.BaselinePosition = position;
			if (color.HasValue)
				_control.BaselineColor = color.Value;
			return this;
		}

		/// <summary>
		/// Sets whether to show the title inline with the baseline.
		/// Only applies when TitlePosition and BaselinePosition are the same (both Top or both Bottom).
		/// Format: "Title ┈┈┈┈┈┈┈┈┈" (title followed by baseline fill).
		/// </summary>
		public SparklineBuilder WithInlineTitleBaseline(bool inline = true)
		{
			_control.InlineTitleWithBaseline = inline;
			return this;
		}

		/// <summary>
		/// Builds the SparklineControl instance.
		/// </summary>
		public SparklineControl Build()
		{
			return _control;
		}
	}
}
