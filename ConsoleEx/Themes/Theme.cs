using Spectre.Console;

namespace ConsoleEx.Themes
{
	public class Theme
	{
		public Theme()
		{ }

		public Color ActiveBorderColor { get; set; } = Color.Green;
		public Color ActiveTitleColor { get; set; } = Color.Green;
		public Color BottomBarBackgroundColor { get; set; } = Color.Blue;
		public Color BottomBarForegroundColor { get; set; } = Color.White;
		public Color ButtonBackgroundColor { get; set; } = Color.Grey39;
		public Color ButtonDisabledBackgroundColor { get; set; } = Color.Grey;
		public Color ButtonDisabledForegroundColor { get; set; } = Color.White;
		public Color ButtonFocusedBackgroundColor { get; set; } = Color.Blue;
		public Color ButtonFocusedForegroundColor { get; set; } = Color.White;
		public Color ButtonForegroundColor { get; set; } = Color.White;
		public Color DesktopBackgroundColor { get; set; } = Color.Black;
		public char DesktopBackroundChar { get; set; } = ' ';
		public Color DesktopForegroundColor { get; set; } = Color.White;
		public Color InactiveBorderColor { get; set; } = Color.Grey;
		public Color InactiveTitleColor { get; set; } = Color.Grey;
		public Color TopBarBackgroundColor { get; set; } = Color.White;
		public Color TopBarForegroundColor { get; set; } = Color.Black;
		public Color WindowBackgroundColor { get; set; } = Color.Grey15;
		public Color WindowForegroundColor { get; set; } = Color.White;
		public Color PromptInputFocusedBackgroundColor { get; set; } = Color.Grey46;
		public Color PromptInputBackgroundColor { get; set; } = Color.Grey23;
		public Color PromptInputFocusedForegroundColor { get; set; } = Color.White;
		public Color PromptInputForegroundColor { get; set; } = Color.White;
	}
}
