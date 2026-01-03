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
	/// A control that renders a horizontal rule (divider line) with optional title text.
	/// Wraps the Spectre.Console Rule component.
	/// </summary>
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

		/// <summary>
		/// Initializes a new instance of the <see cref="RuleControl"/> class.
		/// </summary>
		public RuleControl()
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		/// <inheritdoc/>
		public int? ActualWidth => _contentCache.Content == null ? 0 : AnsiConsoleHelper.StripAnsiStringLength(_contentCache.Content?.FirstOrDefault() ?? string.Empty);

		/// <inheritdoc/>
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

		/// <summary>
		/// Gets or sets the color of the rule line.
		/// </summary>
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
		/// Gets or sets the title text displayed within the rule.
		/// </summary>
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

		/// <summary>
		/// Gets or sets the horizontal alignment of the title within the rule.
		/// </summary>
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
			// Rules are typically one line and take the available width
			int width = _width ?? 80; // Default width if not specified
			return new System.Drawing.Size(width, 1);
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

			int ruleWidth = _width ?? targetWidth;

			Rule rule = new Rule()
			{
				Title = string.IsNullOrEmpty(_title) ? null : _title,
				Style = new Style(_color ?? fgColor, background: bgColor),
				Justification = _titleAlignment
			};

			renderedContent = new List<string>() { AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(rule, ruleWidth, 1, bgColor).FirstOrDefault() ?? string.Empty };

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
