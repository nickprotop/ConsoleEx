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
/// Defines the interface for SharpConsoleUI themes that control the visual appearance of all UI elements.
/// Implement this interface to create custom themes for the console window system.
/// </summary>
public interface ITheme
{
    /// <summary>
    /// Gets the unique name of the theme used for identification and selection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable description of the theme explaining its visual style.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the foreground color for the border of active (focused) windows.
    /// </summary>
    Color ActiveBorderForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the title text of active (focused) windows.
    /// </summary>
    Color ActiveTitleForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the bottom status bar of the console window system.
    /// </summary>
    Color BottomBarBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for text displayed in the bottom status bar.
    /// </summary>
    Color BottomBarForegroundColor { get; }

    /// <summary>
    /// Gets the background color for buttons in their default (unfocused, unselected) state.
    /// </summary>
    Color ButtonBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for buttons when they are disabled and cannot be interacted with.
    /// </summary>
    Color ButtonDisabledBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for button text when the button is disabled.
    /// </summary>
    Color ButtonDisabledForegroundColor { get; }

    /// <summary>
    /// Gets the background color for buttons when they have keyboard focus.
    /// </summary>
    Color ButtonFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for button text when the button has keyboard focus.
    /// </summary>
    Color ButtonFocusedForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for button text in the default (unfocused, unselected) state.
    /// </summary>
    Color ButtonForegroundColor { get; }

    /// <summary>
    /// Gets the background color for buttons when they are selected or pressed.
    /// </summary>
    Color ButtonSelectedBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for button text when the button is selected or pressed.
    /// </summary>
    Color ButtonSelectedForegroundColor { get; }

    /// <summary>
    /// Gets the background color for list items when highlighted but the control is unfocused.
    /// </summary>
    Color ListUnfocusedHighlightBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for list items when highlighted but the control is unfocused.
    /// </summary>
    Color ListUnfocusedHighlightForegroundColor { get; }

    /// <summary>
    /// Gets the background color for list items when hovered by the mouse.
    /// If null, falls back to highlight color.
    /// </summary>
    Color? ListHoverBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for list items when hovered by the mouse.
    /// If null, falls back to highlight color.
    /// </summary>
    Color? ListHoverForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the desktop area behind all windows.
    /// </summary>
    Color DesktopBackgroundColor { get; }

    /// <summary>
    /// Gets the character used to fill the desktop background area.
    /// </summary>
    char DesktopBackroundChar { get; }

    /// <summary>
    /// Gets the foreground color for the desktop background character pattern.
    /// </summary>
    Color DesktopForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the border of inactive (unfocused) windows.
    /// </summary>
    Color InactiveBorderForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the title text of inactive (unfocused) windows.
    /// </summary>
    Color InactiveTitleForegroundColor { get; }

    /// <summary>
    /// Gets the background color for modal dialog windows.
    /// </summary>
    Color ModalBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the border of modal dialog windows.
    /// </summary>
    Color ModalBorderForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the title text of modal dialog windows.
    /// </summary>
    Color ModalTitleForegroundColor { get; }

    /// <summary>
    /// Gets the background color for danger/error notification windows.
    /// </summary>
    Color NotificationDangerWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for informational notification windows.
    /// </summary>
    Color NotificationInfoWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for success notification windows.
    /// </summary>
    Color NotificationSuccessWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for warning notification windows.
    /// </summary>
    Color NotificationWarningWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for generic notification windows without a specific type.
    /// </summary>
    Color NotificationWindowBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for prompt input fields in their default state.
    /// </summary>
    Color PromptInputBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for prompt input fields when they have keyboard focus.
    /// </summary>
    Color PromptInputFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for text in prompt input fields when focused.
    /// </summary>
    Color PromptInputFocusedForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for text in prompt input fields in their default state.
    /// </summary>
    Color PromptInputForegroundColor { get; }

    /// <summary>
    /// Gets a value indicating whether modal windows should display a drop shadow effect.
    /// </summary>
    bool ShowModalShadow { get; }

    /// <summary>
    /// Gets the background color for text edit controls when focused but not in editing mode.
    /// </summary>
    Color TextEditFocusedNotEditing { get; }

    /// <summary>
    /// Gets the background color for the top application bar of the console window system.
    /// </summary>
    Color TopBarBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for text displayed in the top application bar.
    /// </summary>
    Color TopBarForegroundColor { get; }

    /// <summary>
    /// Gets a value indicating whether modal windows should use double-line border characters.
    /// </summary>
    bool UseDoubleLineBorderForModal { get; }

    /// <summary>
    /// Gets the default background color for standard window content areas.
    /// </summary>
    Color WindowBackgroundColor { get; }

    /// <summary>
    /// Gets the default foreground color for text in standard window content areas.
    /// </summary>
    Color WindowForegroundColor { get; }

    /// <summary>
    /// Gets the background color for toolbar controls. Null means transparent (inherit from container).
    /// </summary>
    Color? ToolbarBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for toolbar controls. Null means transparent (inherit from container).
    /// </summary>
    Color? ToolbarForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for separator controls. Null means transparent (inherit from container).
    /// </summary>
    Color? SeparatorForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the menu bar (top-level items). Null means inherit from container.
    /// </summary>
    Color? MenuBarBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the menu bar (top-level items). Null means inherit from container.
    /// </summary>
    Color? MenuBarForegroundColor { get; }

    /// <summary>
    /// Gets the background color for highlighted menu bar items.
    /// </summary>
    Color MenuBarHighlightBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for highlighted menu bar items.
    /// </summary>
    Color MenuBarHighlightForegroundColor { get; }

    /// <summary>
    /// Gets the background color for menu dropdowns.
    /// </summary>
    Color MenuDropdownBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for menu dropdown items.
    /// </summary>
    Color MenuDropdownForegroundColor { get; }

    /// <summary>
    /// Gets the background color for highlighted menu dropdown items.
    /// </summary>
    Color MenuDropdownHighlightBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for highlighted menu dropdown items.
    /// </summary>
    Color MenuDropdownHighlightForegroundColor { get; }

    /// <summary>
    /// Gets the background color for dropdown control lists.
    /// </summary>
    Color DropdownBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for dropdown control list items.
    /// </summary>
    Color DropdownForegroundColor { get; }

    /// <summary>
    /// Gets the background color for highlighted/selected dropdown items.
    /// </summary>
    Color DropdownHighlightBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for highlighted/selected dropdown items.
    /// </summary>
    Color DropdownHighlightForegroundColor { get; }

}
