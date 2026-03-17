// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders
{
	/// <summary>
	/// Fluent builder for creating LineGraphControl instances.
	/// </summary>
	public class LineGraphBuilder : IControlBuilder<LineGraphControl>
	{
		private readonly LineGraphControl _control;
		private readonly List<(string name, Color color, ColorGradient? gradient)> _pendingSeries = new();
		private readonly Dictionary<string, IEnumerable<double>> _pendingData = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="LineGraphBuilder"/> class.
		/// </summary>
		public LineGraphBuilder()
		{
			_control = new LineGraphControl();
		}

		/// <summary>
		/// Sets the rendering mode (Braille or Ascii).
		/// </summary>
		public LineGraphBuilder WithMode(LineGraphMode mode)
		{
			_control.Mode = mode;
			return this;
		}

		/// <summary>
		/// Sets the graph height in lines.
		/// </summary>
		public LineGraphBuilder WithHeight(int height)
		{
			_control.GraphHeight = height;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of data points per series.
		/// </summary>
		public LineGraphBuilder WithMaxDataPoints(int maxPoints)
		{
			_control.MaxDataPoints = maxPoints;
			return this;
		}

		/// <summary>
		/// Enables auto-fitting max data points to match the rendered width.
		/// </summary>
		public LineGraphBuilder WithAutoFitDataPoints(bool autoFit = true)
		{
			_control.AutoFitDataPoints = autoFit;
			return this;
		}

		/// <summary>
		/// Sets the fixed minimum Y-axis value.
		/// </summary>
		public LineGraphBuilder WithMinValue(double minValue)
		{
			_control.MinValue = minValue;
			return this;
		}

		/// <summary>
		/// Sets the fixed maximum Y-axis value.
		/// </summary>
		public LineGraphBuilder WithMaxValue(double maxValue)
		{
			_control.MaxValue = maxValue;
			return this;
		}

		/// <summary>
		/// Sets the title.
		/// </summary>
		public LineGraphBuilder WithTitle(string title)
		{
			_control.Title = title;
			return this;
		}

		/// <summary>
		/// Sets the title and color.
		/// </summary>
		public LineGraphBuilder WithTitle(string title, Color color)
		{
			_control.Title = title;
			_control.TitleColor = color;
			return this;
		}

		/// <summary>
		/// Sets the title position.
		/// </summary>
		public LineGraphBuilder WithTitlePosition(TitlePosition position)
		{
			_control.TitlePosition = position;
			return this;
		}

		/// <summary>
		/// Enables or disables Y-axis labels with optional format string.
		/// </summary>
		public LineGraphBuilder WithYAxisLabels(bool show = true, string? format = null)
		{
			_control.ShowYAxisLabels = show;
			if (format != null)
				_control.AxisLabelFormat = format;
			return this;
		}

		/// <summary>
		/// Sets the color of Y-axis labels.
		/// </summary>
		public LineGraphBuilder WithAxisLabelColor(Color color)
		{
			_control.AxisLabelColor = color;
			return this;
		}

		/// <summary>
		/// Enables or disables the baseline with optional customization.
		/// </summary>
		/// <param name="show">Whether to show the baseline.</param>
		/// <param name="baselineChar">Character to use for the baseline.</param>
		/// <param name="color">Color for the baseline.</param>
		/// <param name="position">Position of the baseline.</param>
		public LineGraphBuilder WithBaseline(bool show = true, char baselineChar = '┈', Color? color = null, TitlePosition position = TitlePosition.Bottom)
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
		/// </summary>
		public LineGraphBuilder WithInlineTitleBaseline(bool inline = true)
		{
			_control.InlineTitleWithBaseline = inline;
			return this;
		}

		/// <summary>
		/// Sets the border style.
		/// </summary>
		public LineGraphBuilder WithBorder(BorderStyle style)
		{
			_control.BorderStyle = style;
			return this;
		}

		/// <summary>
		/// Sets the border style and color.
		/// </summary>
		public LineGraphBuilder WithBorder(BorderStyle style, Color color)
		{
			_control.BorderStyle = style;
			_control.BorderColor = color;
			return this;
		}

		/// <summary>
		/// Sets the background color.
		/// </summary>
		public LineGraphBuilder WithBackgroundColor(Color color)
		{
			_control.BackgroundColor = color;
			return this;
		}

		/// <summary>
		/// Adds a named data series.
		/// </summary>
		/// <param name="name">Series name.</param>
		/// <param name="color">Line color.</param>
		/// <param name="gradient">Optional color gradient.</param>
		public LineGraphBuilder AddSeries(string name, Color color, ColorGradient? gradient = null)
		{
			_pendingSeries.Add((name, color, gradient));
			return this;
		}

		/// <summary>
		/// Adds a named data series with a gradient specification string.
		/// </summary>
		/// <param name="name">Series name.</param>
		/// <param name="color">Line color.</param>
		/// <param name="gradientSpec">Gradient specification (e.g., "cool", "blue→cyan").</param>
		public LineGraphBuilder AddSeries(string name, Color color, string gradientSpec)
		{
			_pendingSeries.Add((name, color, ColorGradient.Parse(gradientSpec)));
			return this;
		}

		/// <summary>
		/// Sets data for a named series.
		/// </summary>
		/// <param name="seriesName">The series name.</param>
		/// <param name="data">The data points.</param>
		public LineGraphBuilder WithData(string seriesName, IEnumerable<double> data)
		{
			_pendingData[seriesName] = data;
			return this;
		}

		/// <summary>
		/// Sets data for a single-series graph (auto-creates default series).
		/// </summary>
		/// <param name="data">The data points.</param>
		public LineGraphBuilder WithData(IEnumerable<double> data)
		{
			_pendingData["default"] = data;
			return this;
		}

		/// <summary>
		/// Sets the control name.
		/// </summary>
		public LineGraphBuilder WithName(string name)
		{
			_control.Name = name;
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public LineGraphBuilder WithMargin(int left, int top, int right, int bottom)
		{
			_control.Margin = new Margin(left, top, right, bottom);
			return this;
		}

		/// <summary>
		/// Sets the margin.
		/// </summary>
		public LineGraphBuilder WithMargin(Margin margin)
		{
			_control.Margin = margin;
			return this;
		}

		/// <summary>
		/// Sets the horizontal alignment.
		/// </summary>
		public LineGraphBuilder WithAlignment(HorizontalAlignment alignment)
		{
			_control.HorizontalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the vertical alignment.
		/// </summary>
		public LineGraphBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{
			_control.VerticalAlignment = alignment;
			return this;
		}

		/// <summary>
		/// Sets the width.
		/// </summary>
		public LineGraphBuilder WithWidth(int width)
		{
			_control.Width = width;
			return this;
		}

		/// <summary>
		/// Sets the visibility.
		/// </summary>
		public LineGraphBuilder Visible(bool visible)
		{
			_control.Visible = visible;
			return this;
		}

		/// <summary>
		/// Sets alignment to stretch to fill available width.
		/// </summary>
		public LineGraphBuilder Stretch()
		{
			_control.HorizontalAlignment = HorizontalAlignment.Stretch;
			return this;
		}

		/// <summary>
		/// Adds a horizontal reference line at the specified data value.
		/// </summary>
		public LineGraphBuilder AddReferenceLine(double value, Color color, char lineChar = '─', string? label = null, LabelPosition labelPosition = LabelPosition.None)
		{
			_control.AddReferenceLine(value, color, lineChar, label, labelPosition);
			return this;
		}

		/// <summary>
		/// Adds a value marker at the specified data value.
		/// </summary>
		public LineGraphBuilder AddValueMarker(double value, string label, Color arrowColor, Color labelColor, MarkerSide side = MarkerSide.Right)
		{
			_control.AddValueMarker(value, label, arrowColor, labelColor, side);
			return this;
		}

		/// <summary>
		/// Enables high/low labels with the specified colors and side.
		/// </summary>
		public LineGraphBuilder WithHighLowLabels(bool show = true, Color? highColor = null, Color? lowColor = null, MarkerSide side = MarkerSide.Right)
		{
			_control.ShowHighLowLabels = show;
			if (highColor.HasValue) _control.HighLabelColor = highColor.Value;
			if (lowColor.HasValue) _control.LowLabelColor = lowColor.Value;
			_control.HighLowLabelSide = side;
			return this;
		}

		/// <summary>
		/// Builds the LineGraphControl instance.
		/// </summary>
		public LineGraphControl Build()
		{
			// Create pending series
			foreach (var (name, color, gradient) in _pendingSeries)
			{
				_control.AddSeries(name, color, gradient);
			}

			// Apply pending data — if no series added and we have default data, auto-create
			foreach (var (seriesName, data) in _pendingData)
			{
				if (seriesName == "default" && _control.Series.All(s => s.Name != "default"))
				{
					_control.SetDataPoints(data);
				}
				else
				{
					_control.SetDataPoints(seriesName, data);
				}
			}

			BindingHelper.ApplyDeferredBindings(this, _control);
			return _control;
		}
	}
}
