

using Spectre.Console;

namespace ConsoleEx
{
	public class Theme
	{
		public char DesktopBackroundChar { get; set; } = ' ';
		public Color DesktopBackgroundColor { get; set; } = Color.Black;
		public Color DesktopForegroundColor { get; set; } = Color.White;
		public Color WindowBackgroundColor { get; set; } = Color.Grey15;
		public Color WindowForegroundColor { get; set; } = Color.White;
		public Color InactiveBorderColor { get; set; } = Color.Grey;
		public Color ActiveBorderColor { get; set; } = Color.Green;
		public Color InactiveTitleColor { get; set; } = Color.Grey;
		public Color ActiveTitleColor { get; set; } = Color.Green;
		public Color TopBarForegroundColor { get; set; } = Color.Black;
		public Color TopBarBackgroundColor { get; set; } = Color.White;
		public Color BottomBarForegroundColor { get; set; } = Color.White;
		public Color BottomBarBackgroundColor { get; set; } = Color.Blue;

		// Buttons
		public Color ButtonBackgroundColor { get; set; } = Color.Grey39;
		public Color ButtonForegroundColor { get; set; } = Color.White;
		public Color ButtonDisabledBackgroundColor { get; set; } = Color.Grey;
		public Color ButtonDisabledForegroundColor { get; set; } = Color.White;
		public Color ButtonFocusedBackgroundColor { get; set; } = Color.Blue;
		public Color ButtonFocusedForegroundColor { get; set; } = Color.White;


		public Theme() { }

	}
}
