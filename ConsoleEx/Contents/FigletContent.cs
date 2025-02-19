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
	public class FigletContent : IWIndowContent
	{
		private Color? _color;
		private Alignment _justify = Alignment.Left;
		private List<string>? _renderedContent;
		private string? _text;
		private int? _width;

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

		public Color? Color
		{ get => _color; set { _color = value; _renderedContent = null; Container?.Invalidate(); } }

		public IContainer? Container { get; set; }

		public string? Text
		{ get => _text; set { _text = value; _renderedContent = null; Container?.Invalidate(); } }

		public int? Width
		{ get => _width; set { _width = value; _renderedContent = null; Container?.Invalidate(); } }

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

			_renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(
				new FigletText(_text ?? string.Empty)
				{
					Color = _color ?? Container?.ForegroundColor ?? Spectre.Console.Color.White,
				}, _width ?? availableWidth ?? 50, availableHeight);

			int maxContentWidth = 0;
			foreach (var line in _renderedContent)
			{
				int length = AnsiConsoleHelper.StripAnsiStringLength(line);
				if (length > maxContentWidth) maxContentWidth = length;
			}

			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			for (int i = 0; i < _renderedContent.Count; i++)
			{
				_renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _renderedContent[i];
			}

			return _renderedContent;
		}

		public void SetColor(Color color)
		{
			_color = color;
			_renderedContent = null;
			Container?.Invalidate();
		}

		public void SetText(string text)
		{
			_text = text;
			_renderedContent = null;
			Container?.Invalidate();
		}
	}
}