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
	public class RuleControl : IWindowControl
	{
		private Alignment _alignment = Alignment.Left;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private Color? _color;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string? _title;
		private Justify _titleAlignment = Justify.Left;
		private bool _visible = true;
		private int? _width;

		public RuleControl()
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		public int? ActualWidth => _contentCache.Content == null ? 0 : AnsiConsoleHelper.StripAnsiStringLength(_contentCache.Content?.FirstOrDefault() ?? string.Empty);

		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Color? Color
		{
			get => _color;
			set
			{
				_color = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

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

		public string? Title
		{
			get => _title;
			set
			{
				_title = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Justify TitleAlignment
		{
			get => _titleAlignment;
			set
			{
				_titleAlignment = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

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
			// Rules are typically one line and take the available width
			int width = _width ?? 80; // Default width if not specified
			return new System.Drawing.Size(width, 1);
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

			int width = _width ?? availableWidth ?? 80;

			Rule rule = new Rule()
			{
				Title = string.IsNullOrEmpty(_title) ? null : _title,
				Style = new Style(_color ?? Container?.ForegroundColor ?? Spectre.Console.Color.White, background: Container?.BackgroundColor ?? Spectre.Console.Color.Black),
				Justification = _titleAlignment
			};

			renderedContent = new List<string>() { AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(rule, width, 1, Container?.BackgroundColor ?? Spectre.Console.Color.Black).FirstOrDefault() ?? string.Empty };

			int paddingLeft = 0;
			if (_alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, AnsiConsoleHelper.StripAnsiStringLength(renderedContent?.FirstOrDefault() ?? string.Empty));
			}

			for (int i = 0; i < renderedContent.Count; i++)
			{
				renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + renderedContent[i];
			}

			return renderedContent;
		}
	}
}