// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace ConsoleEx.Themes
{
	public class Theme
	{
		public Theme()
		{ }

		public Color ActiveBorderForegroundColor { get; set; } = Color.Green;
		public Color ActiveTitleForegroundColor { get; set; } = Color.Green;
		public Color BottomBarBackgroundColor { get; set; } = Color.Blue;
		public Color BottomBarForegroundColor { get; set; } = Color.White;
		public Color ButtonBackgroundColor { get; set; } = Color.Grey39;
		public Color ButtonDisabledBackgroundColor { get; set; } = Color.Grey;
		public Color ButtonDisabledForegroundColor { get; set; } = Color.White;
		public Color ButtonFocusedBackgroundColor { get; set; } = Color.Blue;
		public Color ButtonFocusedForegroundColor { get; set; } = Color.White;
		public Color ButtonForegroundColor { get; set; } = Color.White;
		public Color ButtonSelectedBackgroundColor { get; set; } = Color.DarkBlue;
		public Color ButtonSelectedForegroundColor { get; set; } = Color.White;
		public Color DesktopBackgroundColor { get; set; } = Color.Black;
		public char DesktopBackroundChar { get; set; } = ' ';
		public Color DesktopForegroundColor { get; set; } = Color.White;
		public Color InactiveBorderForegroundColor { get; set; } = Color.Grey;
		public Color InactiveTitleForegroundColor { get; set; } = Color.Grey;
		public Color ModalBackgroundColor { get; set; } = Color.Grey19;
		public Color ModalBorderForegroundColor { get; set; } = Color.Yellow;
		public Color ModalTitleForegroundColor { get; set; } = Color.Yellow;
		public Color NotificationDangerWindowBackgroundColor { get; set; } = Color.Maroon;
		public Color NotificationInfoWindowBackgroundColor { get; set; } = Color.SteelBlue;
		public Color NotificationSuccessWindowBackgroundColor { get; set; } = Color.DarkGreen;
		public Color NotificationWarningWindowBackgroundColor { get; set; } = Color.Orange3;
		public Color NotificationWindowBackgroundColor { get; set; } = Color.Grey;
		public Color PromptInputBackgroundColor { get; set; } = Color.Grey23;
		public Color PromptInputFocusedBackgroundColor { get; set; } = Color.Grey46;
		public Color PromptInputFocusedForegroundColor { get; set; } = Color.White;
		public Color PromptInputForegroundColor { get; set; } = Color.White;
		public bool ShowModalShadow { get; set; } = true;
		public Color TextEditFocusedNotEditing { get; set; } = Color.Grey35;
		public Color TopBarBackgroundColor { get; set; } = Color.White;
		public Color TopBarForegroundColor { get; set; } = Color.Black;

		// Slightly darker than regular windows
		public bool UseDoubleLineBorderForModal { get; set; } = true;

		public Color WindowBackgroundColor { get; set; } = Color.Grey15;
		public Color WindowForegroundColor { get; set; } = Color.White;
	}
}