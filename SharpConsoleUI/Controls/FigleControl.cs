// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders text using FIGlet ASCII art fonts.
	/// Wraps the Spectre.Console FigletText component for large decorative text display.
	/// </summary>
	public class FigleControl : IWindowControl, IDOMPaintable
	{
		private Color? _color;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string? _text;
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the <see cref="FigleControl"/> class.
		/// </summary>
		public FigleControl()
		{
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				if (string.IsNullOrEmpty(_text)) return _margin.Left + _margin.Right;

				// Calculate width by rendering to get actual FIGlet dimensions
				FigletText figletText = new FigletText(_text);
				var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, _width ?? 80, null, bgColor);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + _margin.Left + _margin.Right;
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the color of the FIGlet text.
		/// </summary>
		public Color? Color
		{ get => _color; set { _color = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text to render as FIGlet ASCII art.
		/// </summary>
		public string? Text
		{ get => _text; set { _text = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			if (string.IsNullOrEmpty(_text))
				return new System.Drawing.Size(_margin.Left + _margin.Right, _margin.Top + _margin.Bottom);

			// For Figlet text, we need to render to get the size
			FigletText figletText = new FigletText(_text);
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, _width ?? 80, null, bgColor);

			int maxWidth = content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line));
			return new System.Drawing.Size(
				maxWidth + _margin.Left + _margin.Right,
				content.Count + _margin.Top + _margin.Bottom
			);
		}

		/// <summary>
		/// Sets the color of the FIGlet text.
		/// </summary>
		/// <param name="color">The color to apply to the text.</param>
		public void SetColor(Color color)
		{
			_color = color;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the text to render as FIGlet ASCII art.
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetText(string text)
		{
			_text = text;
			Container?.Invalidate(true);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			if (string.IsNullOrEmpty(_text))
			{
				return new LayoutSize(
					Math.Clamp(_margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(_margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
				);
			}

			// For Figlet text, we need to render to get the size
			FigletText figletText = new FigletText(_text);
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			int targetWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, targetWidth, null, bgColor);

			int maxWidth = content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line));
			int width = maxWidth + _margin.Left + _margin.Right;
			int height = content.Count + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = _color ?? Container?.ForegroundColor ?? defaultFg;
			int targetWidth = bounds.Width - _margin.Left - _margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			if (!string.IsNullOrEmpty(_text))
			{
				// Render the FIGlet text
				FigletText figletText = new FigletText(_text);
				figletText.Color = fgColor;

				int figletWidth = _width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, figletWidth, null, bgColor);

				int figletHeight = renderedContent.Count;
				int availableHeight = bounds.Height - _margin.Top - _margin.Bottom;

				for (int i = 0; i < Math.Min(figletHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Calculate alignment
						int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
						int alignOffset = 0;
						if (lineWidth < targetWidth)
						{
							switch (_horizontalAlignment)
							{
								case HorizontalAlignment.Center:
									alignOffset = (targetWidth - lineWidth) / 2;
									break;
								case HorizontalAlignment.Right:
									alignOffset = targetWidth - lineWidth;
									break;
							}
						}

						// Fill left alignment padding
						if (alignOffset > 0)
						{
							buffer.FillRect(new LayoutRect(startX, paintY, alignOffset, 1), ' ', fgColor, bgColor);
						}

						// Parse and write the FIGlet line
						var cells = AnsiParser.Parse(renderedContent[i], fgColor, bgColor);
						buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

						// Fill right padding
						int rightPadStart = startX + alignOffset + lineWidth;
						int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
						if (rightPadWidth > 0)
						{
							buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', fgColor, bgColor);
						}

						// Fill right margin
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after FIGlet content
				for (int y = startY + figletHeight; y < bounds.Bottom - _margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			for (int y = bounds.Bottom - _margin.Bottom; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}
		}

		#endregion
	}
}
