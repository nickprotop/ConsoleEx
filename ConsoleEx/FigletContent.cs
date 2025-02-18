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
		private List<string> _renderedContent;

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

		public int? Width
		{ get => _width; set { _width = value; Container?.Invalidate(); } }

		public IContainer? Container { get; set; }

		public void SetText(string text)
		{
			_text = text;
			Container?.Invalidate();
		}

		public void SetColor(Color color)
		{
			_color = color;
			Container?.Invalidate();
		}

		private Justify _justify;
		public Justify Justify
		{ get => _justify; set { _justify = value; Container?.Invalidate(); } }

		public string Guid { get; } = new Guid().ToString();

		public FigletContent(string text, Color color, Justify justify, out string guid)
		{
			guid = Guid;
			_text = text;
			_color = color;
			_justify = justify;
			_renderedContent = new List<string>();
		}

		public FigletContent(string text, Color color, Justify justify)
		{
			_text = text;
			_color = color;
			_justify = justify;
			_renderedContent = new List<string>();
		}

		public List<string> RenderContent(int? width, int? height)
		{
			_renderedContent.Clear();

			_renderedContent = AnsiConsoleExtensions.ConvertSpectreRenderableToAnsi(
				new FigletText(_text ?? string.Empty)
				{
					Color = _color ?? Color.White
				}, _width ?? (width ?? 50), height, false);

			return _renderedContent;
		}

		public void Dispose()
		{
			Container = null;
		}
	}
}
