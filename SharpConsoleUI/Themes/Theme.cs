// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Default theme implementation for SharpConsoleUI providing customizable visual appearance
	/// for all UI elements including windows, buttons, dialogs, and notifications.
	/// </summary>
	public class Theme : ITheme
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Theme"/> class with default color values.
		/// </summary>
		public Theme()
		{ }

		/// <inheritdoc />
		public virtual string Name => "Default";

		/// <inheritdoc />
		public virtual string Description => "Default SharpConsoleUI theme";

		/// <summary>
		/// Gets or sets the foreground color for the border of active (focused) windows.
		/// </summary>
		public Color ActiveBorderForegroundColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the foreground color for the title text of active (focused) windows.
		/// </summary>
		public Color ActiveTitleForegroundColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the background color for the bottom status bar of the console window system.
		/// </summary>
		public Color BottomBarBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for text displayed in the bottom status bar.
		/// </summary>
		public Color BottomBarForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for buttons in their default (unfocused, unselected) state.
		/// </summary>
		public Color ButtonBackgroundColor { get; set; } = Color.Grey39;

		/// <summary>
		/// Gets or sets the background color for buttons when they are disabled and cannot be interacted with.
		/// </summary>
		public Color ButtonDisabledBackgroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the foreground color for button text when the button is disabled.
		/// </summary>
		public Color ButtonDisabledForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for buttons when they have keyboard focus.
		/// </summary>
		public Color ButtonFocusedBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for button text when the button has keyboard focus.
		/// </summary>
		public Color ButtonFocusedForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for button text in the default (unfocused, unselected) state.
		/// </summary>
		public Color ButtonForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for buttons when they are selected or pressed.
		/// </summary>
		public Color ButtonSelectedBackgroundColor { get; set; } = Color.DarkBlue;

		/// <summary>
		/// Gets or sets the foreground color for button text when the button is selected or pressed.
		/// </summary>
		public Color ButtonSelectedForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for the desktop area behind all windows.
		/// </summary>
		public Color DesktopBackgroundColor { get; set; } = Color.Black;

		/// <summary>
		/// Gets or sets the character used to fill the desktop background area.
		/// </summary>
		public char DesktopBackroundChar { get; set; } = ' ';

		/// <summary>
		/// Gets or sets the foreground color for the desktop background character pattern.
		/// </summary>
		public Color DesktopForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for the border of inactive (unfocused) windows.
		/// </summary>
		public Color InactiveBorderForegroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the foreground color for the title text of inactive (unfocused) windows.
		/// </summary>
		public Color InactiveTitleForegroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the background color for modal dialog windows. Typically slightly darker than regular windows.
		/// </summary>
		public Color ModalBackgroundColor { get; set; } = Color.Grey19;

		/// <summary>
		/// Gets or sets the foreground color for the border of modal dialog windows.
		/// </summary>
		public Color ModalBorderForegroundColor { get; set; } = Color.Yellow;

		/// <summary>
		/// Gets or sets the foreground color for the title text of modal dialog windows.
		/// </summary>
		public Color ModalTitleForegroundColor { get; set; } = Color.Yellow;

		/// <summary>
		/// Gets or sets the background color for danger/error notification windows.
		/// </summary>
		public Color NotificationDangerWindowBackgroundColor { get; set; } = Color.Maroon;

		/// <summary>
		/// Gets or sets the background color for informational notification windows.
		/// </summary>
		public Color NotificationInfoWindowBackgroundColor { get; set; } = Color.SteelBlue;

		/// <summary>
		/// Gets or sets the background color for success notification windows.
		/// </summary>
		public Color NotificationSuccessWindowBackgroundColor { get; set; } = Color.DarkGreen;

		/// <summary>
		/// Gets or sets the background color for warning notification windows.
		/// </summary>
		public Color NotificationWarningWindowBackgroundColor { get; set; } = Color.Orange3;

		/// <summary>
		/// Gets or sets the background color for generic notification windows without a specific type.
		/// </summary>
		public Color NotificationWindowBackgroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the background color for prompt input fields in their default state.
		/// </summary>
		public Color PromptInputBackgroundColor { get; set; } = Color.Grey23;

		/// <summary>
		/// Gets or sets the background color for prompt input fields when they have keyboard focus.
		/// </summary>
		public Color PromptInputFocusedBackgroundColor { get; set; } = Color.Grey46;

		/// <summary>
		/// Gets or sets the foreground color for text in prompt input fields when focused.
		/// </summary>
		public Color PromptInputFocusedForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for text in prompt input fields in their default state.
		/// </summary>
		public Color PromptInputForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets a value indicating whether modal windows should display a drop shadow effect.
		/// </summary>
		public bool ShowModalShadow { get; set; } = true;

		/// <summary>
		/// Gets or sets the background color for text edit controls when focused but not in editing mode.
		/// </summary>
		public Color TextEditFocusedNotEditing { get; set; } = Color.Grey35;

		/// <summary>
		/// Gets or sets the background color for the top application bar of the console window system.
		/// </summary>
		public Color TopBarBackgroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for text displayed in the top application bar.
		/// </summary>
		public Color TopBarForegroundColor { get; set; } = Color.Black;

		/// <summary>
		/// Gets or sets a value indicating whether modal windows should use double-line border characters
		/// instead of single-line borders for visual distinction.
		/// </summary>
		public bool UseDoubleLineBorderForModal { get; set; } = true;

		/// <summary>
		/// Gets or sets the default background color for standard window content areas.
		/// </summary>
		public Color WindowBackgroundColor { get; set; } = Color.Grey15;

		/// <summary>
		/// Gets or sets the default foreground color for text in standard window content areas.
		/// </summary>
		public Color WindowForegroundColor { get; set; } = Color.White;
	}
}
