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

		public SpectreRenderableControl()
		{
			_contentCache = new ThreadSafeCache<List<string>>(this);
		}

		public SpectreRenderableControl(IRenderable renderable)
		{
			_contentCache = new ThreadSafeCache<List<string>>(this);
			_renderable = renderable;
		}

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

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		public IContainer? Container { get; set; }

		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

		public IRenderable? Renderable
		{ get => _renderable; set { _renderable = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(); Container?.Invalidate(true); } }

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
	}		public void Dispose()
		{
			Container = null;
		}

		public void Invalidate()
		{
			_contentCache.Invalidate();
		}

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

			return _contentCache.GetOrRender(() =>
			{
				if (_renderable == null) return new List<string> { string.Empty };

				var cachedContent = new List<string>();

				int width = _width ?? availableWidth ?? 80;

				// Convert the Spectre renderable to ANSI strings
				cachedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, width, availableHeight, BackgroundColor);

				int maxContentWidth = 0;
				foreach (var line in cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxContentWidth) maxContentWidth = length;
				}

				// Apply alignment
				int paddingLeft = 0;
				if (_alignment == Alignment.Center)
				{
					paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
				}

				for (int i = 0; i < cachedContent.Count; i++)
				{
					string leftPadding = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', paddingLeft),
						paddingLeft,
						1,
						false,
						BackgroundColor,
						null
					).FirstOrDefault() ?? string.Empty;

					cachedContent[i] = leftPadding + cachedContent[i];
				}

				// Apply margins
				ApplyMargins(ref cachedContent, maxContentWidth + paddingLeft);

				return cachedContent;
			});
		}

		public void SetRenderable(IRenderable renderable)
		{
			_renderable = renderable;
			_contentCache.Invalidate();
			Container?.Invalidate(true);
		}

		private void ApplyMargins(ref List<string> content, int contentWidth)
		{
			Color windowBackground = Container?.BackgroundColor ?? Color.Black;
			Color windowForeground = Container?.ForegroundColor ?? Color.White;

			// Add left and right margins to each line
			for (int i = 0; i < content.Count; i++)
			{
				if (_margin.Left > 0)
				{
					string leftMargin = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', _margin.Left),
						_margin.Left,
						1,
						false,
						windowBackground,
						null
					).FirstOrDefault() ?? string.Empty;

					content[i] = leftMargin + content[i];
				}

				if (_margin.Right > 0)
				{
					string rightMargin = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', _margin.Right),
						_margin.Right,
						1,
						false,
						windowBackground,
						null
					).FirstOrDefault() ?? string.Empty;

					content[i] += rightMargin;
				}
			}

			int finalWidth = contentWidth + _margin.Left + _margin.Right;

			// Add top margin
			if (_margin.Top > 0)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', finalWidth),
					finalWidth,
					1,
					false,
					windowBackground,
					windowForeground
				).FirstOrDefault() ?? string.Empty;

				content.InsertRange(0, Enumerable.Repeat(emptyLine, _margin.Top));
			}

			// Add bottom margin
			if (_margin.Bottom > 0)
			{
				string emptyLine = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', finalWidth),
					finalWidth,
					1,
					false,
					windowBackground,
					windowForeground
				).FirstOrDefault() ?? string.Empty;

				content.AddRange(Enumerable.Repeat(emptyLine, _margin.Bottom));
			}
		}
	}
}