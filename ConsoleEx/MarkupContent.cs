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
		private List<string>? _renderedContent;
		private int? _width;
		private bool _wrap = true;

		public int? Width
		{ get => _width; set { _width = value; _renderedContent = null; Container?.Invalidate(); } }

		private Alignment _justify;

		public Alignment Alignment
		{ get => _justify; set { _justify = value; _renderedContent = null; Container?.Invalidate(); } }

		public void Invalidate()
		{
			_renderedContent = null;
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

		public bool Wrap
		{ get => _wrap; set { _wrap = value; _renderedContent = null; Container?.Invalidate(); } }

		public IContainer? Container { get; set; }

		public void SetContent(List<string> lines)
		{
			_content = lines;
			_renderedContent = null;
			Container?.Invalidate();
		}

		public MarkupContent(List<string> lines)
		{
			_content = lines;
		}

		public List<string> RenderContent(int? width, int? height)
		{
			if (_renderedContent != null) return _renderedContent;

			_renderedContent = new List<string>();

			foreach (var line in _content)
			{
				var ansiLines = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(line, _width ?? (width ?? 50), height, _wrap, Container?.BackgroundColor, Container?.ForegroundColor);
				_renderedContent?.AddRange(ansiLines);
			}

			return _renderedContent ?? new List<string>();
		}

		public void Dispose()
		{
			Container = null;
		}
	}
}
