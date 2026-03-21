// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders a horizontal rule (divider line) with optional title text.
	/// Renders directly to CharacterBuffer using BoxChars.
	/// </summary>
	public class RuleControl : BaseControl
	{
		private Color? _color;
		private string? _title;
		private TextJustification _titleAlignment = TextJustification.Left;
		private BorderStyle _borderStyle = BorderStyle.Single;

		/// <summary>
		/// Initializes a new instance of the <see cref="RuleControl"/> class.
		/// </summary>
		public RuleControl()
		{
		}

		/// <inheritdoc/>
		public override int? ContentWidth => (Width ?? 80) + Margin.Left + Margin.Right;

		/// <summary>
		/// Gets or sets the color of the rule line.
		/// </summary>
		public Color? Color
		{
			get => _color;
			set => SetProperty(ref _color, value);
		}

		/// <summary>
		/// Gets or sets the title text displayed within the rule.
		/// </summary>
		public string? Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the title within the rule.
		/// </summary>
		public TextJustification TitleAlignment
		{
			get => _titleAlignment;
			set => SetProperty(ref _titleAlignment, value);
		}

		/// <summary>
		/// Gets or sets the border style for the rule line characters.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>
		/// Creates a new builder for configuring a RuleControl
		/// </summary>
		/// <returns>A new builder instance</returns>
		public static Builders.RuleBuilder Create()
		{
			return new Builders.RuleBuilder();
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = Width ?? constraints.MaxWidth;
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width + Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = Container?.ForegroundColor ?? defaultFg;
			var effectiveBg = SharpConsoleUI.Color.Transparent;
			var ruleColor = _color ?? fgColor;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int ruleWidth = Width ?? targetWidth;
			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			// Paint the rule line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), fgColor, effectiveBg);
				}

				var box = BoxChars.FromBorderStyle(_borderStyle);
				char horizChar = box.Horizontal;

				if (string.IsNullOrEmpty(_title))
				{
					// No title — fill entire line with horizontal chars
					for (int x = 0; x < ruleWidth; x++)
					{
						int px = startX + x;
						if (px >= clipRect.X && px < clipRect.Right)
						{
							var cellBg = effectiveBg;
							buffer.SetNarrowCell(px, startY, horizChar, ruleColor, cellBg);
						}
					}
				}
				else
				{
					// Parse title to get styled cells and measure visible length
					var titleCells = MarkupParser.Parse(_title, ruleColor, effectiveBg);
					int titleLen = titleCells.Count;

					// Add spaces around title: ─ Title ─
					int titleWithSpaces = titleLen + 2; // space before and after title
					int dashSpace = ruleWidth - titleWithSpaces;

					if (dashSpace < 2)
					{
						// Not enough room for dashes — just fill with horizontal chars
						for (int x = 0; x < ruleWidth; x++)
						{
							int px = startX + x;
							if (px >= clipRect.X && px < clipRect.Right)
							{
								var cellBg = effectiveBg;
								buffer.SetNarrowCell(px, startY, horizChar, ruleColor, cellBg);
							}
						}
					}
					else
					{
						int leftDashes, rightDashes;
						switch (_titleAlignment)
						{
							case TextJustification.Center:
								leftDashes = dashSpace / 2;
								rightDashes = dashSpace - leftDashes;
								break;
							case TextJustification.Right:
								leftDashes = dashSpace - 1;
								rightDashes = 1;
								break;
							default: // Left
								leftDashes = 1;
								rightDashes = dashSpace - 1;
								break;
						}

						int writeX = startX;

						// Left dashes
						for (int i = 0; i < leftDashes; i++)
						{
							if (writeX >= clipRect.X && writeX < clipRect.Right)
							{
								var cellBg = effectiveBg;
								buffer.SetNarrowCell(writeX, startY, horizChar, ruleColor, cellBg);
							}
							writeX++;
						}

						// Space before title
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = effectiveBg;
							buffer.SetNarrowCell(writeX, startY, ' ', ruleColor, cellBg);
						}
						writeX++;

						// Title cells (with their own colors from markup)
						foreach (var cell in titleCells)
						{
							if (writeX >= clipRect.X && writeX < clipRect.Right)
							{
								buffer.SetCell(writeX, startY, cell);
							}
							writeX++;
						}

						// Space after title
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = effectiveBg;
							buffer.SetNarrowCell(writeX, startY, ' ', ruleColor, cellBg);
						}
						writeX++;

						// Right dashes
						for (int i = 0; i < rightDashes; i++)
						{
							if (writeX >= clipRect.X && writeX < clipRect.Right)
							{
								var cellBg = effectiveBg;
								buffer.SetNarrowCell(writeX, startY, horizChar, ruleColor, cellBg);
							}
							writeX++;
						}
					}
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), fgColor, effectiveBg);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, effectiveBg);
		}

		#endregion
	}
}
