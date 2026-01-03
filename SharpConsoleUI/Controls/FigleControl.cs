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

		public FigleControl()
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

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

		public Alignment Alignment
		{ get => _justify; set { _justify = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public Color? Color
		{ get => _color; set { _color = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public IContainer? Container { get; set; }

		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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

		public string? Text
		{ get => _text; set { _text = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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
			_contentCache.Dispose();
			Container = null;
		}

		public void Invalidate()
		{
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			// For Figlet text, we need to render to get the size
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

			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			var renderedContent = new List<string>();

			FigletText figletText = new FigletText(_text ?? string.Empty);
			Style style = new Style(_color ?? _color ?? Container?.ForegroundColor ?? Spectre.Console.Color.White, background: Container?.BackgroundColor ?? Spectre.Console.Color.Black);
			figletText.Color = style.Foreground;

			renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, _width ?? availableWidth ?? 50, availableHeight, Container?.BackgroundColor ?? Spectre.Console.Color.Black);

			int maxContentWidth = 0;
			foreach (var line in renderedContent)
			{
				int length = AnsiConsoleHelper.StripAnsiStringLength(line);
				if (length > maxContentWidth) maxContentWidth = length;
			}

			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			for (int i = 0; i < renderedContent.Count; i++)
			{
				renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + renderedContent[i];
			}

			return renderedContent;
		}

		public void SetColor(Color color)
		{
			_color = color;
			_contentCache.Invalidate(InvalidationReason.PropertyChanged);
			Container?.Invalidate(true);
		}

		public void SetText(string text)
		{
			_text = text;
			_contentCache.Invalidate(InvalidationReason.PropertyChanged);
			Container?.Invalidate(true);
		}
	}
}