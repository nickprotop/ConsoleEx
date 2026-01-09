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
using Spectre.Console.Rendering;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that wraps any Spectre.Console IRenderable for display within the window system.
	/// Provides a bridge between Spectre.Console's rich rendering and the SharpConsoleUI framework.
	/// </summary>
	public class SpectreRenderableControl : IWindowControl, IDOMPaintable
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private IRenderable? _renderable;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the <see cref="SpectreRenderableControl"/> class.
		/// </summary>
		public SpectreRenderableControl()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpectreRenderableControl"/> class with a renderable.
		/// </summary>
		/// <param name="renderable">The Spectre.Console renderable to display.</param>
		public SpectreRenderableControl(IRenderable renderable)
		{
			_renderable = renderable;
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				if (_renderable == null) return _margin.Left + _margin.Right;

				var bgColor = BackgroundColor;
				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, _width ?? 80, null, bgColor);

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
		/// Gets or sets the background color for rendering.
		/// Falls back to container or theme colors if not explicitly set.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets the foreground color for rendering.
		/// Falls back to theme colors if not explicitly set.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the Spectre.Console renderable to display.
		/// </summary>
		public IRenderable? Renderable
		{ get => _renderable; set { _renderable = value; Container?.Invalidate(true); } }

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
			if (_renderable == null)
				return new System.Drawing.Size(_margin.Left + _margin.Right, _margin.Top + _margin.Bottom);

			var bgColor = BackgroundColor;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, _width ?? 80, null, bgColor);

			int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
			return new System.Drawing.Size(
				maxWidth + _margin.Left + _margin.Right,
				content.Count + _margin.Top + _margin.Bottom
			);
		}

		/// <summary>
		/// Sets the Spectre.Console renderable to display.
		/// </summary>
		/// <param name="renderable">The renderable to display.</param>
		public void SetRenderable(IRenderable renderable)
		{
			_renderable = renderable;
			Container?.Invalidate(true);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
        public LayoutSize MeasureDOM(LayoutConstraints constraints)
        {
            if (_renderable == null)
            {
                return new LayoutSize(
                    Math.Clamp(_margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
                    Math.Clamp(_margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
                );
            }

            var bgColor = BackgroundColor;
            int targetWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;

            var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, targetWidth, null, bgColor);

            int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
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
			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;
			int targetWidth = bounds.Width - _margin.Left - _margin.Right;

			if (targetWidth <= 0) return;

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

			if (_renderable != null)
			{
				int renderWidth = _width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, renderWidth, null, bgColor);

				int contentHeight = renderedContent.Count;
				int availableHeight = bounds.Height - _margin.Top - _margin.Bottom;

				for (int i = 0; i < Math.Min(contentHeight, availableHeight); i++)
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

						// Parse and write the content line
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

				// Fill any remaining height after content
				for (int y = startY + contentHeight; y < bounds.Bottom - _margin.Bottom; y++)
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
