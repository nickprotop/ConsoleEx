// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;

namespace ConsoleEx.Contents
{
	public class RuleContent : IWIndowContent
	{
		private Alignment _alignment = Alignment.Left;
		private List<string>? _cachedContent;
		private Color? _color;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string? _title;
		private Justify _titleAlignment = Justify.Left;
		private int? _width;
		public int? ActualWidth => _cachedContent == null ? 0 : AnsiConsoleHelper.StripAnsiStringLength(_cachedContent?.FirstOrDefault() ?? string.Empty);

		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color? Color
		{
			get => _color;
			set
			{
				_color = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public IContainer? Container { get; set; }

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public string? Title
		{
			get => _title;
			set
			{
				_title = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Justify TitleAlignment
		{
			get => _titleAlignment;
			set
			{
				_titleAlignment = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public int? Width
		{
			get => _width;
			set
			{
				_width = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

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

			_cachedContent = new List<string>();

			int width = _width ?? availableWidth ?? 80;

			Rule rule = new Rule()
			{
				Title = string.IsNullOrEmpty(_title) ? null : _title,
				Style = new Style(_color ?? Container?.ForegroundColor ?? Spectre.Console.Color.White, background: Container?.BackgroundColor ?? Spectre.Console.Color.Black),
				Justification = _titleAlignment
			};

			_cachedContent = new List<string>() { AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(rule, width, 1).FirstOrDefault() ?? string.Empty };

			int paddingLeft = 0;
			if (_alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, AnsiConsoleHelper.StripAnsiStringLength(_cachedContent?.FirstOrDefault() ?? string.Empty));
			}

			for (int i = 0; i < _cachedContent!.Count; i++)
			{
				_cachedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _cachedContent[i];
			}

			return _cachedContent;
		}
	}
}