// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
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
					int length = AnsiConsoleExtensions.StripAnsiStringLength(line);
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

			foreach (var line in _content)
			{
				var ansiLines = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(line, _width ?? (availableWidth ?? 50), availableHeight, _wrap, Container?.BackgroundColor, Container?.ForegroundColor);
				_renderedContent?.AddRange(ansiLines);
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