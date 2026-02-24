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

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders a horizontal rule (divider line) with optional title text.
	/// Wraps the Spectre.Console Rule component.
	/// </summary>
	public class RuleControl : BaseControl
	{
		private Color? _color;
		private string? _title;
		private Justify _titleAlignment = Justify.Left;

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
			set
			{
				_color = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the title text displayed within the rule.
		/// </summary>
		public string? Title
		{
			get => _title;
			set
			{
				_title = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the title within the rule.
		/// </summary>
		public Justify TitleAlignment
		{
			get => _titleAlignment;
			set
			{
				_titleAlignment = value;
				Container?.Invalidate(true);
			}
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
			var ruleColor = _color ?? fgColor;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int ruleWidth = Width ?? targetWidth;
			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			// Paint the rule line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, startY, Margin.Left, 1), ' ', fgColor, bgColor);
				}

				// Render using Spectre's Rule and parse the output
				Rule rule = new Rule()
				{
					Title = string.IsNullOrEmpty(_title) ? null : _title,
					Style = new Style(ruleColor, background: bgColor),
					Justification = _titleAlignment
				};

				var ansiLine = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(rule, ruleWidth, 1, bgColor).FirstOrDefault() ?? string.Empty;
				var cells = AnsiParser.Parse(ansiLine, ruleColor, bgColor);
				buffer.WriteCellsClipped(startX, startY, cells, clipRect);

				// Fill right margin
				if (Margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), ' ', fgColor, bgColor);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, bgColor);
		}

		#endregion
	}
}
