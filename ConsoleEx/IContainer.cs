using Spectre.Console;

namespace ConsoleEx
{
	public interface IContainer
	{
		public Color BackgroundColor { get; set; }
		public Color ForegroundColor { get; set; }

		public ConsoleWindowSystem? GetConsoleWindowSystem { get; }

		public bool IsDirty { get; set; }

		public void Invalidate(bool redrawAll);
	}
}
