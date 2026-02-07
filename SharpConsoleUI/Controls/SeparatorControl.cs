// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Size = System.Drawing.Size;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A simple vertical separator control for visually dividing UI elements.
	/// Unlike <see cref="SplitterControl"/>, this is non-interactive and non-focusable.
	/// Uses a single vertical line character for a subtle appearance.
	/// </summary>
	public class SeparatorControl : IWindowControl, IDOMPaintable
	{
		private const char DEFAULT_CHARACTER = '│';

		private Color? _backgroundColorValue;
		private char _character = DEFAULT_CHARACTER;
		private IContainer? _container;
		private Color? _foregroundColorValue;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Fill;
		private bool _visible = true;
		private int? _width = null;  // null = use measured width (includes margins)

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="SeparatorControl"/> class.
		/// </summary>
		public SeparatorControl()
		{
		}

		/// <inheritdoc/>
		public int? ContentWidth => _width;

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

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
		public IContainer? Container
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
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

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
				// Allow null (use measured width) or explicit positive value
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
			return new Size(_width ?? 1, 1);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(false);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int separatorWidth = _width ?? 1;
			int width = separatorWidth + _margin.Left + _margin.Right;
			// Report minimal height during measurement.
			// The separator will be given full height during arrangement if VerticalAlignment is Fill.
			int height = 1 + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

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

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int separatorHeight = bounds.Height - _margin.Top - _margin.Bottom;

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
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, windowBackground);
					}

					// Paint separator character
					if (startX >= clipRect.X && startX < clipRect.Right)
					{
						buffer.SetCell(startX, paintY, _character, fgColor, bgColor);
					}

					// Fill right margin
					if (_margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(startX + 1, paintY, _margin.Right, 1), ' ', fgColor, windowBackground);
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
