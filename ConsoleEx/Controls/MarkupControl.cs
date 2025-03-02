// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;

namespace ConsoleEx.Controls
{
	public class MarkupControl : IWIndowControl
	{
		private List<string>? _cachedContent;
		private List<string> _content;
		private Alignment _justify = Alignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private int? _width;
		private bool _wrap = true;

		public MarkupControl(List<string> lines)
		{
			_content = lines;
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
		{ get => _justify; set { _justify = value; _cachedContent = null; Container?.Invalidate(true); } }

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

		public string Text
		{
			get => string.Join("\n", _content);
			set
			{
				_content = value.Split('\n').ToList();
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

		public bool Wrap
		{ get => _wrap; set { _wrap = value; _cachedContent = null; Container?.Invalidate(true); } }

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

			int maxContentWidth = 0;
			foreach (var line in _content)
			{
				int length = AnsiConsoleHelper.StripSpectreLength(line);
				if (length > maxContentWidth) maxContentWidth = length;
			}

			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			foreach (var line in _content)
			{
				var ansiLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{line}", _width ?? availableWidth ?? 50, availableHeight, _wrap, Container?.BackgroundColor, Container?.ForegroundColor);
				_cachedContent?.AddRange(ansiLines);
			}

			for (int i = 0; i < _cachedContent?.Count; i++)
			{
				_cachedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _cachedContent[i];
			}

			return _cachedContent ?? new List<string>();
		}

		public void SetContent(List<string> lines)
		{
			_content = lines;
			_cachedContent = null;
			Container?.Invalidate(true);
		}
	}
}