// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Themes;

/// <summary>
/// Interface for SharpConsoleUI themes
/// </summary>
public interface ITheme
{
    /// <summary>
    /// Gets the theme name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the theme description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the active border foreground color
    /// </summary>
    Color ActiveBorderForegroundColor { get; }

    /// <summary>
    /// Gets the active title foreground color
    /// </summary>
    Color ActiveTitleForegroundColor { get; }

    /// <summary>
    /// Gets the bottom bar background color
    /// </summary>
    Color BottomBarBackgroundColor { get; }

    /// <summary>
    /// Gets the bottom bar foreground color
    /// </summary>
    Color BottomBarForegroundColor { get; }

    /// <summary>
    /// Gets the button background color
    /// </summary>
    Color ButtonBackgroundColor { get; }

    /// <summary>
    /// Gets the button disabled background color
    /// </summary>
    Color ButtonDisabledBackgroundColor { get; }

    /// <summary>
    /// Gets the button disabled foreground color
    /// </summary>
    Color ButtonDisabledForegroundColor { get; }

    /// <summary>
    /// Gets the button focused background color
    /// </summary>
    Color ButtonFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the button focused foreground color
    /// </summary>
    Color ButtonFocusedForegroundColor { get; }

    /// <summary>
    /// Gets the button foreground color
    /// </summary>
    Color ButtonForegroundColor { get; }

    /// <summary>
    /// Gets the button selected background color
    /// </summary>
    Color ButtonSelectedBackgroundColor { get; }

    /// <summary>
    /// Gets the button selected foreground color
    /// </summary>
    Color ButtonSelectedForegroundColor { get; }

    /// <summary>
    /// Gets the desktop background color
    /// </summary>
    Color DesktopBackgroundColor { get; }

    /// <summary>
    /// Gets the desktop background character
    /// </summary>
    char DesktopBackroundChar { get; }

    /// <summary>
    /// Gets the desktop foreground color
    /// </summary>
    Color DesktopForegroundColor { get; }

    /// <summary>
    /// Gets the inactive border foreground color
    /// </summary>
    Color InactiveBorderForegroundColor { get; }

    /// <summary>
    /// Gets the inactive title foreground color
    /// </summary>
    Color InactiveTitleForegroundColor { get; }

    /// <summary>
    /// Gets the modal background color
    /// </summary>
    Color ModalBackgroundColor { get; }

    /// <summary>
    /// Gets the modal border foreground color
    /// </summary>
    Color ModalBorderForegroundColor { get; }

    /// <summary>
    /// Gets the modal title foreground color
    /// </summary>
    Color ModalTitleForegroundColor { get; }

    /// <summary>
    /// Gets the notification danger window background color
    /// </summary>
    Color NotificationDangerWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the notification info window background color
    /// </summary>
    Color NotificationInfoWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the notification success window background color
    /// </summary>
    Color NotificationSuccessWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the notification warning window background color
    /// </summary>
    Color NotificationWarningWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the notification window background color
    /// </summary>
    Color NotificationWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the prompt input background color
    /// </summary>
    Color PromptInputBackgroundColor { get; }

    /// <summary>
    /// Gets the prompt input focused background color
    /// </summary>
    Color PromptInputFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the prompt input focused foreground color
    /// </summary>
    Color PromptInputFocusedForegroundColor { get; }

    /// <summary>
    /// Gets the prompt input foreground color
    /// </summary>
    Color PromptInputForegroundColor { get; }

    /// <summary>
    /// Gets whether to show modal shadow
    /// </summary>
    bool ShowModalShadow { get; }

    /// <summary>
    /// Gets the text edit focused not editing color
    /// </summary>
    Color TextEditFocusedNotEditing { get; }

    /// <summary>
    /// Gets the top bar background color
    /// </summary>
    Color TopBarBackgroundColor { get; }

    /// <summary>
    /// Gets the top bar foreground color
    /// </summary>
    Color TopBarForegroundColor { get; }

    /// <summary>
    /// Gets whether to use double line border for modal windows
    /// </summary>
    bool UseDoubleLineBorderForModal { get; }

    /// <summary>
    /// Gets the window background color
    /// </summary>
    Color WindowBackgroundColor { get; }

    /// <summary>
    /// Gets the window foreground color
    /// </summary>
    Color WindowForegroundColor { get; }
}