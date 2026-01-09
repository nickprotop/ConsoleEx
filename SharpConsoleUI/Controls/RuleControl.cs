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
	/// A control that renders a horizontal rule (divider line) with optional title text.
	/// Wraps the Spectre.Console Rule component.
	/// </summary>
	public class RuleControl : IWindowControl, IDOMPaintable
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Color? _color;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string? _title;
		private Justify _titleAlignment = Justify.Left;
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the <see cref="RuleControl"/> class.
		/// </summary>
		public RuleControl()
		{
		}

		/// <inheritdoc/>
		public int? ActualWidth => (_width ?? 80) + _margin.Left + _margin.Right;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set
			{
				_horizontalAlignment = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set
			{
				_verticalAlignment = value;
				Container?.Invalidate(true);
			}
		}

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

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public object? Tag { get; set; }

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

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
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
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// Rules are typically one line and take the available width
			int width = _width ?? 80; // Default width if not specified
			return new System.Drawing.Size(width, 1 + _margin.Top + _margin.Bottom);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = _width ?? constraints.MaxWidth;
			int height = 1 + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width + _margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = Container?.ForegroundColor ?? defaultFg;
			var ruleColor = _color ?? fgColor;

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) return;

			int ruleWidth = _width ?? targetWidth;
			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			for (int y = bounds.Y; y < startY && y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			// Paint the rule line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (_margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, startY, _margin.Left, 1), ' ', fgColor, bgColor);
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
				if (_margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, startY, _margin.Right, 1), ' ', fgColor, bgColor);
				}
			}

			// Fill bottom margin
			for (int y = startY + 1; y < bounds.Bottom; y++)
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
