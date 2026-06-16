// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------


using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Classic Windows-style theme implementation with bright blue and green accents.
	/// Provides traditional Windows UI aesthetic with high-contrast colors.
	/// </summary>
	public class ClassicTheme : ThemeBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ClassicTheme"/> class with classic color values.
		/// </summary>
		public ClassicTheme()
		{ }

		/// <inheritdoc />
		public override string Name { get; set; } = "Classic";

		/// <inheritdoc />
		public override string Description { get; set; } = "Classic Windows-style theme with bright blue and green accents";

		/// <summary>Gets or sets the theme's declared light/dark mode. Classic is declared a light theme.</summary>
		public override ThemeMode Mode { get; set; } = ThemeMode.Light;

		/// <summary>
		/// Gets or sets the foreground color for the border of active (focused) windows.
		/// </summary>
		public override Color ActiveBorderForegroundColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the foreground color for the title text of active (focused) windows.
		/// </summary>
		public override Color ActiveTitleForegroundColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the background color for the bottom status bar of the console window system.
		/// </summary>
		public override Color BottomBarBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for text displayed in the bottom status bar.
		/// </summary>
		public override Color BottomBarForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for buttons in their default (unfocused, unselected) state.
		/// </summary>
		public override Color? ButtonBackgroundColor { get; set; } = Color.Grey39;

		/// <summary>
		/// Gets or sets the background color for buttons when they are disabled and cannot be interacted with.
		/// </summary>
		public override Color? ButtonDisabledBackgroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the foreground color for button text when the button is disabled.
		/// </summary>
		public override Color ButtonDisabledForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for buttons when they have keyboard focus.
		/// </summary>
		public override Color? ButtonFocusedBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for button text when the button has keyboard focus.
		/// </summary>
		public override Color ButtonFocusedForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for button text in the default (unfocused, unselected) state.
		/// </summary>
		public override Color ButtonForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for buttons when they are selected or pressed.
		/// </summary>
		public override Color? ButtonSelectedBackgroundColor { get; set; } = Color.DarkBlue;

		/// <summary>
		/// Gets or sets the foreground color for button text when the button is selected or pressed.
		/// </summary>
		public override Color ButtonSelectedForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for list items when highlighted but the control is unfocused.
		/// </summary>
		public override Color? ListUnfocusedHighlightBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the foreground color for list items when highlighted but the control is unfocused.
		/// </summary>
		public override Color ListUnfocusedHighlightForegroundColor { get; set; } = Color.Silver;

		/// <summary>
		/// Gets or sets the background color for list items when hovered by the mouse.
		/// If null, falls back to highlight color.
		/// </summary>
		public override Color? ListHoverBackgroundColor { get; set; } = null;

		/// <summary>
		/// Gets or sets the foreground color for list items when hovered by the mouse.
		/// If null, falls back to highlight color.
		/// </summary>
		public override Color? ListHoverForegroundColor { get; set; } = null;

		/// <summary>
		/// Gets or sets the background color for the desktop area behind all windows.
		/// </summary>
		public override Color DesktopBackgroundColor { get; set; } = Color.Grey15;

		/// <summary>
		/// Gets or sets the character used to fill the desktop background area.
		/// </summary>
		public override char DesktopBackgroundChar { get; set; } = ' ';

		/// <summary>
		/// Gets or sets the foreground color for the desktop background character pattern.
		/// </summary>
		public override Color DesktopForegroundColor { get; set; } = Color.Grey23;

		/// <inheritdoc />
		public override GradientBackground? DesktopBackgroundGradient { get; set; }

		/// <summary>
		/// Gets or sets the foreground color for the border of inactive (unfocused) windows.
		/// </summary>
		public override Color InactiveBorderForegroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the foreground color for the title text of inactive (unfocused) windows.
		/// </summary>
		public override Color InactiveTitleForegroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the background color for modal dialog windows. Uses darker blue to distinguish from regular windows.
		/// </summary>
		public override Color ModalBackgroundColor { get; set; } = Color.DarkBlue;

		/// <summary>
		/// Gets or sets the foreground color for the border of modal dialog windows.
		/// </summary>
		public override Color ModalBorderForegroundColor { get; set; } = Color.Yellow;

		/// <summary>
		/// Gets or sets the foreground color for the title text of modal dialog windows.
		/// </summary>
		public override Color ModalTitleForegroundColor { get; set; } = Color.Yellow;

		/// <summary>
		/// Gets or sets the background color for danger/error notification windows.
		/// </summary>
		public override Color NotificationDangerWindowBackgroundColor { get; set; } = Color.Maroon;

		/// <summary>
		/// Gets or sets the background color for informational notification windows.
		/// </summary>
		public override Color NotificationInfoWindowBackgroundColor { get; set; } = Color.SteelBlue;

		/// <summary>
		/// Gets or sets the background color for success notification windows.
		/// </summary>
		public override Color NotificationSuccessWindowBackgroundColor { get; set; } = Color.DarkGreen;

		/// <summary>
		/// Gets or sets the background color for warning notification windows.
		/// </summary>
		public override Color NotificationWarningWindowBackgroundColor { get; set; } = Color.Orange3;

		/// <summary>
		/// Gets or sets the background color for generic notification windows without a specific type.
		/// </summary>
		public override Color NotificationWindowBackgroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the background color for prompt input fields in their default state.
		/// </summary>
		public override Color? PromptInputBackgroundColor { get; set; } = Color.Grey23;

		/// <summary>
		/// Gets or sets the background color for prompt input fields when they have keyboard focus.
		/// </summary>
		public override Color? PromptInputFocusedBackgroundColor { get; set; } = Color.Grey46;

		/// <summary>
		/// Gets or sets the foreground color for text in prompt input fields when focused.
		/// </summary>
		public override Color PromptInputFocusedForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for text in prompt input fields in their default state.
		/// </summary>
		public override Color PromptInputForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets a value indicating whether modal windows should display a drop shadow effect.
		/// </summary>
		public override bool ShowModalShadow { get; set; } = true;

		/// <summary>
		/// Gets or sets the background color for text edit controls when focused but not in editing mode.
		/// </summary>
		public override Color TextEditFocusedNotEditing { get; set; } = Color.Grey35;

		/// <summary>
		/// Gets or sets the background color for the top application bar of the console window system.
		/// </summary>
		public override Color TopBarBackgroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for text displayed in the top application bar.
		/// </summary>
		public override Color TopBarForegroundColor { get; set; } = Color.Black;

		/// <summary>
		/// Gets or sets a value indicating whether modal windows should use double-line border characters
		/// instead of single-line borders for visual distinction.
		/// </summary>
		public override bool UseDoubleLineBorderForModal { get; set; } = true;

		/// <summary>
		/// Gets or sets the default background color for standard window content areas.
		/// </summary>
		public override Color WindowBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the default foreground color for text in standard window content areas.
		/// </summary>
		public override Color WindowForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for toolbar controls.
		/// Null means transparent (inherit from container).
		/// </summary>
		public override Color? ToolbarBackgroundColor { get; set; } = null;

		/// <summary>
		/// Gets or sets the foreground color for toolbar controls.
		/// Null means transparent (inherit from container).
		/// </summary>
		public override Color? ToolbarForegroundColor { get; set; } = null;

		/// <summary>
		/// Gets or sets the foreground color for separator controls.
		/// Null means transparent (inherit from container).
		/// </summary>
		public override Color? SeparatorForegroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the background color for the menu bar (top-level items).
		/// Null means inherit from container.
		/// </summary>
		public override Color? MenuBarBackgroundColor { get; set; } = null;

		/// <summary>
		/// Gets or sets the foreground color for the menu bar (top-level items).
		/// Null means inherit from container.
		/// </summary>
		public override Color? MenuBarForegroundColor { get; set; } = null;

		/// <summary>
		/// Gets or sets the background color for highlighted menu bar items.
		/// </summary>
		public override Color? MenuBarHighlightBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for highlighted menu bar items.
		/// </summary>
		public override Color MenuBarHighlightForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for menu dropdowns.
		/// </summary>
		public override Color? MenuDropdownBackgroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the foreground color for menu dropdown items.
		/// </summary>
		public override Color MenuDropdownForegroundColor { get; set; } = Color.Black;

		/// <summary>
		/// Gets or sets the background color for highlighted menu dropdown items.
		/// </summary>
		public override Color? MenuDropdownHighlightBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for highlighted menu dropdown items.
		/// </summary>
		public override Color MenuDropdownHighlightForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for dropdown control lists.
		/// </summary>
		public override Color? DropdownBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the foreground color for dropdown control list items.
		/// </summary>
		public override Color DropdownForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for highlighted/selected dropdown items.
		/// </summary>
		public override Color? DropdownHighlightBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for highlighted/selected dropdown items.
		/// </summary>
		public override Color DropdownHighlightForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color used when flashing modal windows to draw user attention.
		/// </summary>
		public override Color ModalFlashColor { get; set; } = Color.Yellow;

		/// <summary>
		/// Gets or sets the color for the filled portion of progress bars.
		/// </summary>
		public override Color ProgressBarFilledColor { get; set; } = Color.Cyan1;

		/// <summary>
		/// Gets or sets the color for the unfilled portion of progress bars.
		/// </summary>
		public override Color ProgressBarUnfilledColor { get; set; } = Color.Grey35;

		/// <summary>
		/// Gets or sets the color for the percentage text display on progress bars.
		/// </summary>
		public override Color ProgressBarPercentageColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for table controls.
		/// </summary>
		public override Color? TableBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the foreground color for table controls.
		/// </summary>
		public override Color TableForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the border color for table controls.
		/// Null means falls back to active window border color.
		/// </summary>
		public override Color? TableBorderColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the background color for table headers.
		/// </summary>
		public override Color? TableHeaderBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for table headers.
		/// </summary>
		public override Color TableHeaderForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for selected rows in the table when focused.
		/// </summary>
		public override Color? TableSelectionBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for selected rows in the table when focused.
		/// </summary>
		public override Color TableSelectionForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for hovered rows in the table.
		/// </summary>
		public override Color? TableHoverBackgroundColor { get; set; } = Color.DarkBlue;

		/// <summary>
		/// Gets or sets the foreground color for hovered rows in the table.
		/// </summary>
		public override Color TableHoverForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for selected rows in the table when unfocused.
		/// </summary>
		public override Color? TableUnfocusedSelectionBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the foreground color for selected rows in the table when unfocused.
		/// </summary>
		public override Color TableUnfocusedSelectionForegroundColor { get; set; } = Color.Silver;

		/// <summary>
		/// Gets or sets the color for the table scrollbar thumb.
		/// </summary>
		public override Color TableScrollbarThumbColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the color for the table scrollbar track.
		/// </summary>
		public override Color TableScrollbarTrackColor { get; set; } = Color.Grey;

		/// <summary>Gets or sets the general scrollbar thumb color when the control is focused.</summary>
		public override Color ScrollbarThumbColor { get; set; } = Color.Green;

		/// <summary>Gets or sets the general scrollbar thumb color when the control is unfocused.</summary>
		public override Color ScrollbarThumbUnfocusedColor { get; set; } = Color.Grey;

		/// <summary>Gets or sets the general scrollbar track color when the control is focused.</summary>
		public override Color ScrollbarTrackColor { get; set; } = Color.Grey23;

		/// <summary>Gets or sets the general scrollbar track color when the control is unfocused.</summary>
		public override Color ScrollbarTrackUnfocusedColor { get; set; } = Color.Grey23;

		/// <summary>Gets or sets the collapsible-panel header foreground color when focused.</summary>
		public override Color CollapsibleHeaderFocusedForegroundColor { get; set; } = Color.Green;

		/// <summary>Gets or sets the collapsible-panel header background color when focused.</summary>
		public override Color? CollapsibleHeaderFocusedBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the background color for the active (selected) tab header.
		/// </summary>
		public override Color? TabHeaderActiveBackgroundColor { get; set; } = Color.Blue;

		/// <summary>
		/// Gets or sets the foreground color for the active (selected) tab header.
		/// </summary>
		public override Color TabHeaderActiveForegroundColor { get; set; } = Color.White;

		/// <summary>
		/// Gets or sets the background color for inactive tab headers.
		/// </summary>
		public override Color? TabHeaderBackgroundColor { get; set; } = Color.Navy;

		/// <summary>
		/// Gets or sets the foreground color for inactive tab headers.
		/// </summary>
		public override Color TabHeaderForegroundColor { get; set; } = Color.Silver;

		/// <summary>
		/// Gets or sets the foreground color for disabled tab headers.
		/// </summary>
		public override Color TabHeaderDisabledForegroundColor { get; set; } = Color.Grey;

		/// <summary>
		/// Gets or sets the background color for disabled tab headers.
		/// </summary>
		public override Color? TabHeaderDisabledBackgroundColor { get; set; } = Color.Navy;

		/// <summary>Active tab background when the header has keyboard focus.</summary>
		public override Color? TabHeaderActiveFocusedBackgroundColor { get; set; } = Color.Cyan1;

		/// <summary>Active tab foreground when the header has keyboard focus.</summary>
		public override Color TabHeaderActiveFocusedForegroundColor { get; set; } = Color.Black;

		/// <summary>Inactive tab background when the header has keyboard focus.</summary>
		public override Color? TabHeaderFocusedBackgroundColor { get; set; } = Color.Navy;

		/// <summary>Inactive tab foreground when the header has keyboard focus.</summary>
		public override Color TabHeaderFocusedForegroundColor { get; set; } = Color.Silver;

		/// <summary>
		/// Gets or sets the border color for the TabControl content area.
		/// </summary>
		public override Color? TabContentBorderColor { get; set; } = Color.Green;

		/// <summary>
		/// Gets or sets the background color for the TabControl content area.
		/// </summary>
		public override Color? TabContentBackgroundColor { get; set; } = null;

		/// <inheritdoc/>
		public override Color? DatePickerBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? DatePickerForegroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? DatePickerFocusedBackgroundColor { get; set; } = Color.Blue;
		/// <inheritdoc/>
		public override Color? DatePickerFocusedForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? DatePickerSegmentBackgroundColor { get; set; } = Color.DarkBlue;
		/// <inheritdoc/>
		public override Color? DatePickerSegmentForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? DatePickerDisabledForegroundColor { get; set; } = Color.Grey;
		/// <inheritdoc/>
		public override Color? DatePickerCalendarTodayColor { get; set; } = Color.Cyan1;
		/// <inheritdoc/>
		public override Color? DatePickerCalendarSelectedColor { get; set; } = Color.Blue;
		/// <inheritdoc/>
		public override Color? DatePickerCalendarHeaderColor { get; set; } = Color.Yellow;

		/// <inheritdoc/>
		public override Color? TimePickerBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? TimePickerForegroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? TimePickerFocusedBackgroundColor { get; set; } = Color.Blue;
		/// <inheritdoc/>
		public override Color? TimePickerFocusedForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? TimePickerSegmentBackgroundColor { get; set; } = Color.DarkBlue;
		/// <inheritdoc/>
		public override Color? TimePickerSegmentForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? TimePickerDisabledForegroundColor { get; set; } = Color.Grey;

		/// <inheritdoc/>
		public override Color? StatusBarBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? StatusBarForegroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? StatusBarShortcutForegroundColor { get; set; } = null;

		/// <inheritdoc/>
		public override Color? SliderTrackColor { get; set; } = Color.Grey35;
		/// <inheritdoc/>
		public override Color? SliderFilledTrackColor { get; set; } = Color.Cyan1;
		/// <inheritdoc/>
		public override Color? SliderThumbColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? SliderFocusedThumbColor { get; set; } = Color.Yellow;

		/// <inheritdoc/>
		public override Color? CheckboxBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? CheckboxFocusedBackgroundColor { get; set; } = Color.Blue;
		/// <inheritdoc/>
		public override Color? CheckboxDisabledBackgroundColor { get; set; } = Color.Grey;

		/// <inheritdoc/>
		public override Color? ListBackgroundColor { get; set; } = Color.Navy;

		/// <inheritdoc/>
		public override Color? TreeBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? TreeSelectionBackgroundColor { get; set; } = Color.Blue;
		/// <inheritdoc/>
		public override Color? TreeUnfocusedSelectionBackgroundColor { get; set; } = Color.Navy;
		/// <inheritdoc/>
		public override Color? LineGraphBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? BarGraphBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? SparklineBackgroundColor { get; set; } = null;

		// Per-control colors (pinned to the Button* values these controls borrowed previously)

		/// <inheritdoc/>
		public override Color DropdownFocusedForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? DropdownFocusedBackgroundColor { get; set; } = new Color(0, 0, 255);
		/// <inheritdoc/>
		public override Color DropdownDisabledForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? DropdownDisabledBackgroundColor { get; set; } = Color.Grey;

		/// <inheritdoc/>
		public override Color ListForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color ListFocusedForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color ListSelectedForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? ListSelectedBackgroundColor { get; set; } = Color.Navy;
		/// <inheritdoc/>
		public override Color ListDisabledForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color? ListDisabledBackgroundColor { get; set; } = Color.Grey;

		/// <inheritdoc/>
		public override Color CheckboxForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color CheckboxFocusedForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color CheckboxDisabledForegroundColor { get; set; } = Color.White;
		/// <inheritdoc/>
		public override Color CheckboxCheckmarkColor { get; set; } = Color.White;

		/// <inheritdoc/>
		public override Color? DatePickerDisabledBackgroundColor { get; set; } = Color.Grey;

		/// <inheritdoc/>
		public override Color HtmlForegroundColor { get; set; } = Color.White;

		// Start menu theming

		/// <inheritdoc/>
		public override Color? StartMenuHeaderBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? StartMenuHeaderForegroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? StartMenuSectionHeaderBackgroundColor { get; set; } = null;
		/// <inheritdoc/>
		public override Color? StartMenuInfoStripForegroundColor { get; set; } = Color.Grey;

	}
}
