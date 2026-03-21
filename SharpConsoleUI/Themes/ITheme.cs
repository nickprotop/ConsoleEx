// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------


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
    char DesktopBackgroundChar { get; }

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

    /// <summary>
    /// Gets the background color used when flashing modal windows to draw user attention.
    /// </summary>
    Color ModalFlashColor { get; }

    /// <summary>
    /// Gets the color for the filled portion of progress bars.
    /// </summary>
    Color ProgressBarFilledColor { get; }

    /// <summary>
    /// Gets the color for the unfilled portion of progress bars.
    /// </summary>
    Color ProgressBarUnfilledColor { get; }

    /// <summary>
    /// Gets the color for the percentage text display on progress bars.
    /// </summary>
    Color ProgressBarPercentageColor { get; }

    /// <summary>
    /// Gets the background color for table controls.
    /// </summary>
    Color TableBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for table controls.
    /// </summary>
    Color TableForegroundColor { get; }

    /// <summary>
    /// Gets the border color for table controls.
    /// Null means falls back to active window border color.
    /// </summary>
    Color? TableBorderColor { get; }

    /// <summary>
    /// Gets the background color for table headers.
    /// </summary>
    Color TableHeaderBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for table headers.
    /// </summary>
    Color TableHeaderForegroundColor { get; }

    /// <summary>
    /// Gets the background color for selected rows in the table when focused.
    /// </summary>
    Color TableSelectionBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for selected rows in the table when focused.
    /// </summary>
    Color TableSelectionForegroundColor { get; }

    /// <summary>
    /// Gets the background color for hovered rows in the table.
    /// </summary>
    Color TableHoverBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for hovered rows in the table.
    /// </summary>
    Color TableHoverForegroundColor { get; }

    /// <summary>
    /// Gets the background color for selected rows in the table when unfocused.
    /// </summary>
    Color TableUnfocusedSelectionBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for selected rows in the table when unfocused.
    /// </summary>
    Color TableUnfocusedSelectionForegroundColor { get; }

    /// <summary>
    /// Gets the color for the table scrollbar thumb.
    /// </summary>
    Color TableScrollbarThumbColor { get; }

    /// <summary>
    /// Gets the color for the table scrollbar track.
    /// </summary>
    Color TableScrollbarTrackColor { get; }

    /// <summary>
    /// Gets the background color for the active (selected) tab header.
    /// </summary>
    Color TabHeaderActiveBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the active (selected) tab header.
    /// </summary>
    Color TabHeaderActiveForegroundColor { get; }

    /// <summary>
    /// Gets the background color for inactive tab headers.
    /// </summary>
    Color TabHeaderBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for inactive tab headers.
    /// </summary>
    Color TabHeaderForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for disabled tab headers.
    /// </summary>
    Color TabHeaderDisabledForegroundColor { get; }

    /// <summary>
    /// Gets the background color for disabled tab headers.
    /// </summary>
    Color TabHeaderDisabledBackgroundColor { get; }

    /// <summary>
    /// Gets the border color for the TabControl content area. Null means use active window border color.
    /// </summary>
    Color? TabContentBorderColor { get; }

    /// <summary>
    /// Gets the background color for the TabControl content area. Null means inherit from container.
    /// </summary>
    Color? TabContentBackgroundColor { get; }

    // DatePicker theme colors

    /// <summary>
    /// Gets the background color for the date picker control.
    /// </summary>
    Color? DatePickerBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground (text) color for the date picker control.
    /// </summary>
    Color? DatePickerForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the date picker when focused.
    /// </summary>
    Color? DatePickerFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the date picker when focused.
    /// </summary>
    Color? DatePickerFocusedForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the active date segment.
    /// </summary>
    Color? DatePickerSegmentBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the active date segment.
    /// </summary>
    Color? DatePickerSegmentForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for disabled date segments.
    /// </summary>
    Color? DatePickerDisabledForegroundColor { get; }

    /// <summary>
    /// Gets the highlight color for today's date in the calendar popup.
    /// </summary>
    Color? DatePickerCalendarTodayColor { get; }

    /// <summary>
    /// Gets the highlight color for the selected date in the calendar popup.
    /// </summary>
    Color? DatePickerCalendarSelectedColor { get; }

    /// <summary>
    /// Gets the color for the calendar popup header text.
    /// </summary>
    Color? DatePickerCalendarHeaderColor { get; }

    // TimePicker theme colors

    /// <summary>
    /// Gets the background color for the time picker control.
    /// </summary>
    Color? TimePickerBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground (text) color for the time picker control.
    /// </summary>
    Color? TimePickerForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the time picker when focused.
    /// </summary>
    Color? TimePickerFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the time picker when focused.
    /// </summary>
    Color? TimePickerFocusedForegroundColor { get; }

    /// <summary>
    /// Gets the background color for the active time segment.
    /// </summary>
    Color? TimePickerSegmentBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for the active time segment.
    /// </summary>
    Color? TimePickerSegmentForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for disabled time segments.
    /// </summary>
    Color? TimePickerDisabledForegroundColor { get; }

    // StatusBar theme colors

    /// <summary>
    /// Gets the background color for status bar controls. Null means inherit from container.
    /// </summary>
    Color? StatusBarBackgroundColor { get; }

    /// <summary>
    /// Gets the foreground color for status bar controls. Null means inherit from container.
    /// </summary>
    Color? StatusBarForegroundColor { get; }

    /// <summary>
    /// Gets the foreground color for shortcut key hints in status bar controls. Null means use Cyan1.
    /// </summary>
    Color? StatusBarShortcutForegroundColor { get; }

    // Slider theme colors

    /// <summary>
    /// Gets the color for the unfilled track portion of slider controls.
    /// Null means use default (Grey35 classic, Grey23 modern).
    /// </summary>
    Color? SliderTrackColor { get; }

    /// <summary>
    /// Gets the color for the filled track portion of slider controls.
    /// Null means use default (Cyan1).
    /// </summary>
    Color? SliderFilledTrackColor { get; }

    /// <summary>
    /// Gets the color for the slider thumb indicator.
    /// Null means use default (White classic, Grey93 modern).
    /// </summary>
    Color? SliderThumbColor { get; }

    /// <summary>
    /// Gets the color for the slider thumb indicator when focused.
    /// Null means use default (Yellow).
    /// </summary>
    Color? SliderFocusedThumbColor { get; }

    // Checkbox background colors

    /// <summary>
    /// Gets the background color for checkboxes in their default state. Null means transparent.
    /// </summary>
    Color? CheckboxBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for checkboxes when they have keyboard focus. Null means transparent.
    /// </summary>
    Color? CheckboxFocusedBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for checkboxes when they are disabled. Null means transparent.
    /// </summary>
    Color? CheckboxDisabledBackgroundColor { get; }

    // List background color

    /// <summary>
    /// Gets the background color for list controls. Null means transparent.
    /// </summary>
    Color? ListBackgroundColor { get; }

    // Tree background colors

    /// <summary>
    /// Gets the background color for tree controls. Null means transparent.
    /// </summary>
    Color? TreeBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for selected tree nodes when focused. Null means transparent.
    /// </summary>
    Color? TreeSelectionBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for selected tree nodes when unfocused. Null means transparent.
    /// </summary>
    Color? TreeUnfocusedSelectionBackgroundColor { get; }

    // Graph background colors

    /// <summary>
    /// Gets the background color for line graph controls. Null means transparent.
    /// </summary>
    Color? LineGraphBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for bar graph controls. Null means transparent.
    /// </summary>
    Color? BarGraphBackgroundColor { get; }

    /// <summary>
    /// Gets the background color for sparkline controls. Null means transparent.
    /// </summary>
    Color? SparklineBackgroundColor { get; }

}
