// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;

namespace ConsoleEx.Contents
{
	public class MarkupContent : IWIndowContent
	{
		private List<string> _content;
		private Alignment _justify = Alignment.Left;
		private List<string>? _renderedContent;
		private int? _width;
		private bool _wrap = true;

		public MarkupContent(List<string> lines)
		{
			_content = lines;
		}

		public int? ActualWidth
		{
			get
			{
				if (_renderedContent == null) return null;
				int maxLength = 0;
				foreach (var line in _renderedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{ get => _justify; set { _justify = value; _renderedContent = null; Container?.Invalidate(); } }

		public IContainer? Container { get; set; }

		public int? Width
		{ get => _width; set { _width = value; _renderedContent = null; Container?.Invalidate(); } }

		public bool Wrap
		{ get => _wrap; set { _wrap = value; _renderedContent = null; Container?.Invalidate(); } }

		public void Dispose()
		{
			Container = null;
		}

		public void Invalidate()
		{
			_renderedContent = null;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (_renderedContent != null) return _renderedContent;

			_renderedContent = new List<string>();

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
				_renderedContent?.AddRange(ansiLines);
			}

			for (int i = 0; i < _renderedContent?.Count; i++)
			{
				_renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _renderedContent[i];
			}

			return _renderedContent ?? new List<string>();
		}

		public void SetContent(List<string> lines)
		{
			_content = lines;
			_renderedContent = null;
			Container?.Invalidate();
		}
	}
}