// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace ConsoleEx
{
	public class FigletContent : IWIndowContent
	{
		private string? _text;
		private Color? _color;
		private List<string>? _renderedContent;
		private Alignment _justify = Alignment.Left;

		private int? _width;

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

		public void Invalidate()
		{
			_renderedContent = null;
		}

		public int? Width { get => _width; set { _width = value; _renderedContent = null; Container?.Invalidate(); } }

		public IContainer? Container { get; set; }

		public void SetText(string text)
		{
			_text = text;
			_renderedContent = null;
			Container?.Invalidate();
		}

		public void SetColor(Color color)
		{
			_color = color;
			_renderedContent = null;
			Container?.Invalidate();
		}

		public Alignment Alignment { get => _justify; set { _justify = value; _renderedContent = null; Container?.Invalidate(); } }

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (_renderedContent != null) return _renderedContent;

			_renderedContent = new List<string>();

			_renderedContent = AnsiConsoleExtensions.ConvertSpectreRenderableToAnsi(
				new FigletText(_text ?? string.Empty)
				{
					Color = _color ?? Color.White
				}, _width ?? (availableWidth ?? 50), availableHeight, false);

			return _renderedContent;
		}

		public void Dispose()
		{
			Container = null;
		}
	}
}
