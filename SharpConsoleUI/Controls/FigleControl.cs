// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders text using FIGlet ASCII art fonts.
	/// Wraps the Spectre.Console FigletText component for large decorative text display.
	/// </summary>
	public class FigleControl : IWindowControl
	{
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private Color? _color;
		private Alignment _justify = Alignment.Left;
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				if (_contentCache.Content == null) return null;
				int maxLength = 0;
				foreach (var line in _contentCache.Content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		/// <inheritdoc/>
		public Alignment Alignment
		{ get => _justify; set { _justify = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the color of the FIGlet text.
		/// </summary>
		public Color? Color
		{ get => _color; set { _color = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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
		/// Gets or sets the text to render as FIGlet ASCII art.
		/// </summary>
		public string? Text
		{ get => _text; set { _text = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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
					_contentCache.Invalidate(InvalidationReason.SizeChanged);
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			_contentCache.Dispose();
			Container = null;
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// For Figlet text, we need to render to get the size
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)),
				content.Count
			);
		}

		/// <inheritdoc/>
		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			var layoutService = Container?.GetConsoleWindowSystem?.LayoutStateService;

			// Smart invalidation: check if re-render is needed due to size change
			if (layoutService == null || layoutService.NeedsRerender(this, availableWidth, availableHeight))
			{
				// Dimensions changed - invalidate cache
				_contentCache.Invalidate(InvalidationReason.SizeChanged);
			}
			else
			{
				// Dimensions unchanged - return cached content if available
				var cached = _contentCache.Content;
				if (cached != null) return cached;
			}

			// Update available space tracking
			layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			var renderedContent = new List<string>();
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			var fgColor = Container?.ForegroundColor ?? Spectre.Console.Color.White;
			int targetWidth = availableWidth ?? 80;

			FigletText figletText = new FigletText(_text ?? string.Empty);
			figletText.Color = _color ?? fgColor;

			renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, _width ?? targetWidth, availableHeight, bgColor);

			// Apply alignment padding
			for (int i = 0; i < renderedContent.Count; i++)
			{
				int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
				if (lineWidth < targetWidth)
				{
					int totalPadding = targetWidth - lineWidth;
					switch (_justify)
					{
						case Alignment.Center:
							int leftPad = totalPadding / 2;
							int rightPad = totalPadding - leftPad;
							renderedContent[i] = AnsiConsoleHelper.AnsiEmptySpace(leftPad, bgColor) + renderedContent[i] + AnsiConsoleHelper.AnsiEmptySpace(rightPad, bgColor);
							break;
						case Alignment.Right:
							renderedContent[i] = AnsiConsoleHelper.AnsiEmptySpace(totalPadding, bgColor) + renderedContent[i];
							break;
						default: // Left or Stretch
							renderedContent[i] = renderedContent[i] + AnsiConsoleHelper.AnsiEmptySpace(totalPadding, bgColor);
							break;
					}
				}

				// Apply left margin
				if (_margin.Left > 0)
				{
					renderedContent[i] = AnsiConsoleHelper.AnsiEmptySpace(_margin.Left, bgColor) + renderedContent[i];
				}

				// Apply right margin
				if (_margin.Right > 0)
				{
					renderedContent[i] = renderedContent[i] + AnsiConsoleHelper.AnsiEmptySpace(_margin.Right, bgColor);
				}
			}

			// Add top margin
			if (_margin.Top > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent.FirstOrDefault() ?? string.Empty);
				for (int i = 0; i < _margin.Top; i++)
				{
					renderedContent.Insert(0, AnsiConsoleHelper.AnsiEmptySpace(finalWidth, bgColor));
				}
			}

			// Add bottom margin
			if (_margin.Bottom > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent.FirstOrDefault() ?? string.Empty);
				for (int i = 0; i < _margin.Bottom; i++)
				{
					renderedContent.Add(AnsiConsoleHelper.AnsiEmptySpace(finalWidth, bgColor));
				}
			}

			return renderedContent;
		}

		/// <summary>
		/// Sets the color of the FIGlet text.
		/// </summary>
		/// <param name="color">The color to apply to the text.</param>
		public void SetColor(Color color)
		{
			_color = color;
			_contentCache.Invalidate(InvalidationReason.PropertyChanged);
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the text to render as FIGlet ASCII art.
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetText(string text)
		{
			_text = text;
			_contentCache.Invalidate(InvalidationReason.PropertyChanged);
			Container?.Invalidate(true);
		}
	}
}
