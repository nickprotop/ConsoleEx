// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Plugins.DeveloperTools;

/// <summary>
/// Dark theme optimized for developer tools with green terminal-inspired accents.
/// Features high contrast for readability during extended coding sessions.
/// </summary>
public class DevDarkTheme : ITheme
{
	/// <inheritdoc />
	public string Name => "DevDark";

	/// <inheritdoc />
	public string Description => "Dark developer theme with green terminal-inspired accents";

	/// <inheritdoc />
	public Color ActiveBorderForegroundColor { get; set; } = Color.Green;

	/// <inheritdoc />
	public Color ActiveTitleForegroundColor { get; set; } = Color.Green;

	/// <inheritdoc />
	public Color BottomBarBackgroundColor { get; set; } = Color.Grey7;

	/// <inheritdoc />
	public Color BottomBarForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public Color ButtonBackgroundColor { get; set; } = Color.Grey15;

	/// <inheritdoc />
	public Color ButtonDisabledBackgroundColor { get; set; } = Color.Grey11;

	/// <inheritdoc />
	public Color ButtonDisabledForegroundColor { get; set; } = Color.Grey42;

	/// <inheritdoc />
	public Color ButtonFocusedBackgroundColor { get; set; } = Color.Grey23;

	/// <inheritdoc />
	public Color ButtonFocusedForegroundColor { get; set; } = Color.Green;

	/// <inheritdoc />
	public Color ButtonForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public Color ButtonSelectedBackgroundColor { get; set; } = Color.Grey19;

	/// <inheritdoc />
	public Color ButtonSelectedForegroundColor { get; set; } = Color.White;

	/// <inheritdoc />
	public Color ListUnfocusedHighlightBackgroundColor { get; set; } = Color.Grey27;

	/// <inheritdoc />
	public Color ListUnfocusedHighlightForegroundColor { get; set; } = Color.Grey93;

	/// <inheritdoc />
	public Color? ListHoverBackgroundColor { get; set; } = Color.Grey19;

	/// <inheritdoc />
	public Color? ListHoverForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public Color DesktopBackgroundColor { get; set; } = Color.Grey7;

	/// <inheritdoc />
	public char DesktopBackroundChar { get; set; } = ' ';

	/// <inheritdoc />
	public Color DesktopForegroundColor { get; set; } = Color.Grey35;

	/// <inheritdoc />
	public Color InactiveBorderForegroundColor { get; set; } = Color.Grey42;

	/// <inheritdoc />
	public Color InactiveTitleForegroundColor { get; set; } = Color.Grey58;

	/// <inheritdoc />
	public Color ModalBackgroundColor { get; set; } = Color.Grey11;

	/// <inheritdoc />
	public Color ModalBorderForegroundColor { get; set; } = Color.Green;

	/// <inheritdoc />
	public Color ModalTitleForegroundColor { get; set; } = Color.Green;

	/// <inheritdoc />
	public Color NotificationDangerWindowBackgroundColor { get; set; } = Color.DarkRed;

	/// <inheritdoc />
	public Color NotificationInfoWindowBackgroundColor { get; set; } = Color.DarkSlateGray3;

	/// <inheritdoc />
	public Color NotificationSuccessWindowBackgroundColor { get; set; } = Color.DarkGreen;

	/// <inheritdoc />
	public Color NotificationWarningWindowBackgroundColor { get; set; } = Color.DarkOrange;

	/// <inheritdoc />
	public Color NotificationWindowBackgroundColor { get; set; } = Color.Grey11;

	/// <inheritdoc />
	public Color PromptInputBackgroundColor { get; set; } = Color.Grey15;

	/// <inheritdoc />
	public Color PromptInputFocusedBackgroundColor { get; set; } = Color.Grey19;

	/// <inheritdoc />
	public Color PromptInputFocusedForegroundColor { get; set; } = Color.White;

	/// <inheritdoc />
	public Color PromptInputForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public bool ShowModalShadow { get; set; } = true;

	/// <inheritdoc />
	public Color TextEditFocusedNotEditing { get; set; } = Color.Grey19;

	/// <inheritdoc />
	public Color TopBarBackgroundColor { get; set; } = Color.Grey7;

	/// <inheritdoc />
	public Color TopBarForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public bool UseDoubleLineBorderForModal { get; set; } = true;

	/// <inheritdoc />
	public Color WindowBackgroundColor { get; set; } = Color.Grey11;

	/// <inheritdoc />
	public Color WindowForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public Color? ToolbarBackgroundColor { get; set; } = null;

	/// <inheritdoc />
	public Color? ToolbarForegroundColor { get; set; } = null;

	/// <inheritdoc />
	public Color? SeparatorForegroundColor { get; set; } = Color.Grey19;

	/// <inheritdoc />
	public Color? MenuBarBackgroundColor { get; set; } = null;

	/// <inheritdoc />
	public Color? MenuBarForegroundColor { get; set; } = null;

	/// <inheritdoc />
	public Color MenuBarHighlightBackgroundColor { get; set; } = Color.Grey19;

	/// <inheritdoc />
	public Color MenuBarHighlightForegroundColor { get; set; } = Color.Green;

	/// <inheritdoc />
	public Color MenuDropdownBackgroundColor { get; set; } = Color.Grey11;

	/// <inheritdoc />
	public Color MenuDropdownForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public Color MenuDropdownHighlightBackgroundColor { get; set; } = Color.Grey27;

	/// <inheritdoc />
	public Color MenuDropdownHighlightForegroundColor { get; set; } = Color.White;

	/// <inheritdoc />
	public Color DropdownBackgroundColor { get; set; } = Color.Grey11;

	/// <inheritdoc />
	public Color DropdownForegroundColor { get; set; } = Color.Grey78;

	/// <inheritdoc />
	public Color DropdownHighlightBackgroundColor { get; set; } = Color.Grey27;

	/// <inheritdoc />
	public Color DropdownHighlightForegroundColor { get; set; } = Color.White;

	/// <inheritdoc />
	public Color ModalFlashColor { get; set; } = Color.Grey27;
}
