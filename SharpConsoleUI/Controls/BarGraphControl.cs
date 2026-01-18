// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A horizontal bar graph control for visualizing percentage-based data.
	/// Displays a filled/unfilled bar with optional label, value, and custom colors.
	/// </summary>
	public class BarGraphControl : IWindowControl, IDOMPaintable
	{
		private const int DEFAULT_BAR_WIDTH = 20;
		private const char FILLED_CHAR = '█';
		private const char EMPTY_CHAR = '░';

		private Color? _backgroundColorValue;
		private int _barWidth = DEFAULT_BAR_WIDTH;
		private IContainer? _container;
		private Color _filledColor = Color.Cyan1;
		private Color? _foregroundColorValue;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private string _label = string.Empty;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private double _maxValue = 100.0;
		private bool _showLabel = true;
		private bool _showValue = true;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private Color _unfilledColor = Color.Grey35;
		private double _value;
		private string _valueFormat = "F1";
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the <see cref="BarGraphControl"/> class.
		/// </summary>
		public BarGraphControl()
		{
		}

		/// <summary>
		/// Gets or sets the background color of the control.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the width of the bar in characters.
		/// </summary>
		public int BarWidth
		{
			get => _barWidth;
			set
			{
				_barWidth = Math.Max(1, value);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color for the filled portion of the bar.
		/// </summary>
		public Color FilledColor
		{
			get => _filledColor;
			set
			{
				_filledColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color for labels and values.
		/// When null, inherits from the container.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the label text displayed before the bar.
		/// </summary>
		public string Label
		{
			get => _label;
			set
			{
				_label = value ?? string.Empty;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum value for the bar (100% fill).
		/// </summary>
		public double MaxValue
		{
			get => _maxValue;
			set
			{
				_maxValue = Math.Max(0.01, value);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to show the label.
		/// </summary>
		public bool ShowLabel
		{
			get => _showLabel;
			set
			{
				_showLabel = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to show the value after the bar.
		/// </summary>
		public bool ShowValue
		{
			get => _showValue;
			set
			{
				_showValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the color for the unfilled portion of the bar.
		/// </summary>
		public Color UnfilledColor
		{
			get => _unfilledColor;
			set
			{
				_unfilledColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the current value to display.
		/// </summary>
		public double Value
		{
			get => _value;
			set
			{
				_value = Math.Max(0, value);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the format string for displaying the value (e.g., "F1", "F2", "0").
		/// </summary>
		public string ValueFormat
		{
			get => _valueFormat;
			set
			{
				_valueFormat = value ?? "F1";
				Container?.Invalidate(true);
			}
		}

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ActualWidth => _width;

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : (int?)null;
				if (_width != validatedValue)
				{
					_width = validatedValue;
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <inheritdoc/>
		public Size GetLogicalContentSize()
		{
			int contentWidth = CalculateContentWidth();
			return new Size(contentWidth + _margin.Left + _margin.Right, 1 + _margin.Top + _margin.Bottom);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(false);
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int contentWidth = CalculateContentWidth();
			int width = contentWidth + _margin.Left + _margin.Right;
			int height = 1 + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			// Resolve colors
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color fgColor = _foregroundColorValue ?? Container?.ForegroundColor ?? defaultFg;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int paintY = startY;

			if (paintY < clipRect.Y || paintY >= clipRect.Bottom || paintY >= bounds.Bottom)
				return;

			// Fill margins and background
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			int currentX = startX;

			// Paint label
			if (_showLabel && !string.IsNullOrEmpty(_label))
			{
				string labelText = _label + ": ";
				foreach (char c in labelText)
				{
					if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
					{
						buffer.SetCell(currentX, paintY, c, Color.Grey70, bgColor);
					}
					currentX++;
				}
			}

			// Paint bar
			double percent = Math.Clamp(_value / _maxValue, 0.0, 1.0);
			int filledChars = (int)Math.Round(percent * _barWidth);
			int unfilledChars = _barWidth - filledChars;

			// Filled portion
			for (int i = 0; i < filledChars; i++)
			{
				if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
				{
					buffer.SetCell(currentX, paintY, FILLED_CHAR, _filledColor, bgColor);
				}
				currentX++;
			}

			// Unfilled portion
			for (int i = 0; i < unfilledChars; i++)
			{
				if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
				{
					buffer.SetCell(currentX, paintY, EMPTY_CHAR, _unfilledColor, bgColor);
				}
				currentX++;
			}

			// Paint value
			if (_showValue)
			{
				currentX++; // Space before value
				string valueText = _value.ToString(_valueFormat);
				foreach (char c in valueText)
				{
					if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
					{
						buffer.SetCell(currentX, paintY, c, _filledColor, bgColor);
					}
					currentX++;
				}
			}
		}

		#endregion

		#region Private Helper Methods

		private int CalculateContentWidth()
		{
			int width = _barWidth;

			if (_showLabel && !string.IsNullOrEmpty(_label))
			{
				width += _label.Length + 2; // Label + ": "
			}

			if (_showValue)
			{
				width += 1 + _value.ToString(_valueFormat).Length; // Space + value
			}

			return width;
		}

		#endregion
	}
}
