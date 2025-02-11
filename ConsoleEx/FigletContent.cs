using Spectre.Console;

namespace ConsoleEx
{
	public class FigletContent : IWIndowContent
	{
		private string? _text;
		private Color? _color;
		private Justify? _justify;
		private List<string> _renderedContent;

		public Window? Container { get; set; }

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

		public void SetJustify(Justify justify)
		{
			_justify = justify;
			Container?.Invalidate();
		}

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

		public List<string> RenderContent(int? width, int? height, bool overflow)
		{
			_renderedContent.Clear();

			_renderedContent = AnsiConsoleExtensions.ConvertRenderableToAnsi(
				new FigletText(_text ?? string.Empty)
				{
					Color = _color ?? Color.White,
					Justification = _justify ?? Justify.Left
				}, width, height, false);

			return _renderedContent;
		}

		public List<string> RenderContent(int? width, int? height)
		{
			return RenderContent(width, height, false);
		}
	}
}