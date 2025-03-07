// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleEx.Controls
{
	public class SpectreRenderableControl : IWIndowControl
	{
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColor;
		private List<string>? _cachedContent;
		private Color? _foregroundColor;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private IRenderable? _renderable;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		public SpectreRenderableControl()
		{
		}

		public SpectreRenderableControl(IRenderable renderable)
		{
			_renderable = renderable;
		}

		public int? ActualWidth
		{
			get
			{
				if (_cachedContent == null) return null;
				int maxLength = 0;
				foreach (var line in _cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _cachedContent = null; Container?.Invalidate(true); } }

		public Color? BackgroundColor
		{ get => _backgroundColor; set { _backgroundColor = value; _cachedContent = null; Container?.Invalidate(true); } }

		public IContainer? Container { get; set; }

		public Color? ForegroundColor
		{ get => _foregroundColor; set { _foregroundColor = value; _cachedContent = null; Container?.Invalidate(true); } }

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public IRenderable? Renderable
		{ get => _renderable; set { _renderable = value; _cachedContent = null; Container?.Invalidate(true); } }

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
		{ get => _visible; set { _visible = value; _cachedContent = null; Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

		public void Dispose()
		{
			Container = null;
		}

		public void Invalidate()
		{
			_cachedContent = null;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (_cachedContent != null) return _cachedContent;
			if (_renderable == null) return new List<string> { string.Empty };

			_cachedContent = new List<string>();

			int width = _width ?? availableWidth ?? 80;

			// Convert the Spectre renderable to ANSI strings
			_cachedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, width, availableHeight, Container?.BackgroundColor ?? Spectre.Console.Color.Black);

			int maxContentWidth = 0;
			foreach (var line in _cachedContent)
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

			for (int i = 0; i < _cachedContent.Count; i++)
			{
				string leftPadding = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', paddingLeft),
					paddingLeft,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault() ?? string.Empty;

				_cachedContent[i] = leftPadding + _cachedContent[i];
			}

			// Apply margins
			ApplyMargins(ref _cachedContent, maxContentWidth + paddingLeft);

			return _cachedContent;
		}

		public void SetRenderable(IRenderable renderable)
		{
			_renderable = renderable;
			_cachedContent = null;
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