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
using Spectre.Console.Rendering;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that wraps any Spectre.Console IRenderable for display within the window system.
	/// Provides a bridge between Spectre.Console's rich rendering and the SharpConsoleUI framework.
	/// </summary>
	public class SpectreRenderableControl : IWindowControl
	{
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColorValue;
		private readonly ThreadSafeCache<List<string>> _contentCache;
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
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpectreRenderableControl"/> class with a renderable.
		/// </summary>
		/// <param name="renderable">The Spectre.Console renderable to display.</param>
		public SpectreRenderableControl(IRenderable renderable)
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
			_renderable = renderable;
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				var content = _contentCache.Content;
				if (content == null || content.Count == 0) return null;
				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		/// <inheritdoc/>
		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
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
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the Spectre.Console renderable to display.
		/// </summary>
		public IRenderable? Renderable
		{ get => _renderable; set { _renderable = value; _contentCache.Invalidate(InvalidationReason.ContentChanged); Container?.Invalidate(true); } }

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
			// For Spectre renderables, we need to render to get the actual size
			if (_renderable == null) return new System.Drawing.Size(0, 0);

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

		/// <summary>
		/// Sets the Spectre.Console renderable to display.
		/// </summary>
		/// <param name="renderable">The renderable to display.</param>
		public void SetRenderable(IRenderable renderable)
		{
			_renderable = renderable;
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
			Container?.Invalidate(true);
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			if (_renderable == null) return new List<string> { string.Empty };

			var bgColor = BackgroundColor;
			int targetWidth = availableWidth ?? 80;
			int renderWidth = _width ?? targetWidth;

			// Convert the Spectre renderable to ANSI strings
			var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, renderWidth, availableHeight, bgColor);

			// Apply alignment padding
			for (int i = 0; i < renderedContent.Count; i++)
			{
				int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
				if (lineWidth < targetWidth)
				{
					int totalPadding = targetWidth - lineWidth;
					switch (_alignment)
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
	}
}
