// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that displays rich text content using Spectre.Console markup syntax.
	/// Supports text alignment, margins, word wrapping, and sticky positioning.
	/// </summary>
	public class MarkupControl : IWindowControl
	{
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private List<string> _content;
		private Alignment _justify = Alignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private bool _wrap = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="MarkupControl"/> class with the specified lines of text.
		/// </summary>
		/// <param name="lines">The lines of text to display, supporting Spectre.Console markup syntax.</param>
		public MarkupControl(List<string> lines)
		{
			_content = lines;
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		/// <summary>
		/// Gets the actual rendered width of the control based on cached content.
		/// </summary>
		/// <returns>The maximum line width in characters, or null if content has not been rendered.</returns>
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

		/// <summary>
		/// Gets or sets the text alignment within the control.
		/// </summary>
		public Alignment Alignment
		{ get => _justify; set { _justify = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets the margin around the control content.
		/// </summary>
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
		/// Gets or sets the text content as a single string with newline separators.
		/// </summary>
		public string Text
		{
			get => string.Join("\n", _content);
			set
			{
				_content = value.Split('\n').ToList();
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the fixed width of the control. When null, the control uses available width.
		/// </summary>
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

		/// <summary>
		/// Gets or sets whether text should wrap to multiple lines when exceeding available width.
		/// </summary>
		public bool Wrap
		{ get => _wrap; set { _wrap = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public void Dispose()
		{
			_contentCache.Dispose();
			Container = null;
		}

		/// <summary>
		/// Invalidates the cached content, forcing a re-render on the next draw.
		/// </summary>
		public void Invalidate()
		{
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// Calculate the natural size based on content
			int maxWidth = 0;
			foreach (var line in _content)
			{
				int length = AnsiConsoleHelper.StripSpectreLength(line);
				if (length > maxWidth) maxWidth = length;
			}
			return new System.Drawing.Size(maxWidth, _content.Count);
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
			int targetWidth = _width ?? availableWidth ?? 50;
			Spectre.Console.Color bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;

			// Calculate content width for alignment
			int maxContentWidth = 0;
			foreach (var line in _content)
			{
				int length = AnsiConsoleHelper.StripSpectreLength(line);
				if (length > maxContentWidth) maxContentWidth = length;
			}

			// For centered/right alignment, render at content width then pad
			// For left/stretch alignment, render at target width
			int renderWidth = (Alignment == Alignment.Center || Alignment == Alignment.Right)
				? Math.Min(maxContentWidth, targetWidth)
				: targetWidth;

			foreach (var line in _content)
			{
				// Use _wrap to control whether multiple lines are allowed in output
				var ansiLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{line}", renderWidth, availableHeight, _wrap, Container?.BackgroundColor, Container?.ForegroundColor);
				renderedContent.AddRange(ansiLines);
			}

			// Apply alignment padding to reach targetWidth
			for (int i = 0; i < renderedContent.Count; i++)
			{
				int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
				if (lineWidth < targetWidth)
				{
					int totalPadding = targetWidth - lineWidth;

					switch (Alignment)
					{
						case Alignment.Center:
							int leftPad = totalPadding / 2;
							int rightPad = totalPadding - leftPad;
							renderedContent[i] = AnsiConsoleHelper.AnsiEmptySpace(leftPad, bgColor)
								+ renderedContent[i]
								+ AnsiConsoleHelper.AnsiEmptySpace(rightPad, bgColor);
							break;
						case Alignment.Right:
							renderedContent[i] = AnsiConsoleHelper.AnsiEmptySpace(totalPadding, bgColor)
								+ renderedContent[i];
							break;
						default: // Left or Stretch
							renderedContent[i] = renderedContent[i]
								+ AnsiConsoleHelper.AnsiEmptySpace(totalPadding, bgColor);
							break;
					}
				}
				else if (lineWidth > targetWidth)
				{
					// Truncate if line is too wide
					renderedContent[i] = AnsiConsoleHelper.SubstringAnsi(renderedContent[i], 0, targetWidth);
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
		/// Sets the content of the control to the specified lines of text.
		/// </summary>
		/// <param name="lines">The lines of text to display, supporting Spectre.Console markup syntax.</param>
		public void SetContent(List<string> lines)
		{
			_content = lines;
			_contentCache.Invalidate(InvalidationReason.PropertyChanged);
			Container?.Invalidate(true);
		}
	}
}
