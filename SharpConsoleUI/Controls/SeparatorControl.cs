// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A simple vertical separator control for visually dividing UI elements.
	/// Unlike <see cref="SplitterControl"/>, this is non-interactive and non-focusable.
	/// Uses a single vertical line character for a subtle appearance.
	/// </summary>
	public class SeparatorControl : BaseControl
	{
		private const char DEFAULT_CHARACTER = '│';

		private Color? _backgroundColorValue;
		private char _character = DEFAULT_CHARACTER;
		private IContainer? _container;
		private Color? _foregroundColorValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="SeparatorControl"/> class.
		/// </summary>
		public SeparatorControl()
		{
			VerticalAlignment = VerticalAlignment.Fill;
		}

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <summary>
		/// Gets or sets the background color of the separator.
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
		/// Gets or sets the character used to draw the separator.
		/// Defaults to '│' (single vertical line).
		/// </summary>
		public char Character
		{
			get => _character;
			set
			{
				_character = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the separator.
		/// When null, uses theme's SeparatorForegroundColor, then falls back to container color.
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

		/// <inheritdoc/>
		public override int? Width
		{
			get => base.Width;
			set
			{
				// Allow null (use measured width) or explicit positive value
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : (int?)null;
				if (base.Width != validatedValue)
				{
					base.Width = validatedValue;
				}
			}
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			return new System.Drawing.Size(Width ?? 1, 1);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int separatorWidth = Width ?? 1;
			int width = separatorWidth + Margin.Left + Margin.Right;
			// Report minimal height during measurement.
			// The separator will be given full height during arrangement if VerticalAlignment is Fill.
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// Resolve colors with fallback chain:
			// Control color -> Theme color -> Container color -> Default
			var theme = Container?.GetConsoleWindowSystem?.Theme;

			Color fgColor = _foregroundColorValue
				?? theme?.SeparatorForegroundColor
				?? Container?.ForegroundColor
				?? defaultFg;

			Color bgColor = _backgroundColorValue
				?? theme?.ToolbarBackgroundColor
				?? Container?.BackgroundColor
				?? defaultBg;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int separatorHeight = bounds.Height - Margin.Top - Margin.Bottom;

			// Fill margins with container background color
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, windowBackground);

			// Paint the separator lines
			for (int y = 0; y < separatorHeight; y++)
			{
				int paintY = startY + y;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					// Fill left margin
					if (Margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, paintY, Margin.Left, 1), ' ', fgColor, windowBackground);
					}

					// Paint separator character
					if (startX >= clipRect.X && startX < clipRect.Right)
					{
						buffer.SetCell(startX, paintY, _character, fgColor, bgColor);
					}

					// Fill right margin
					if (Margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(startX + 1, paintY, Margin.Right, 1), ' ', fgColor, windowBackground);
					}
				}
			}

			// Fill bottom margin
			for (int y = startY + separatorHeight; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, windowBackground);
				}
			}
		}

		#endregion
	}
}
