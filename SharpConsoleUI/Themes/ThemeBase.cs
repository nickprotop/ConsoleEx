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
	/// Abstract base for all themes: declares every <see cref="ITheme"/> member as a settable virtual
	/// property with neutral/transparent "blank canvas" defaults. Concrete themes derive from this and
	/// set the values they want; a member left unset is transparent/neutral, never a hidden inherited value.
	/// </summary>
	public abstract class ThemeBase : ITheme
	{
		// --- Identity / metadata ---

		/// <inheritdoc/>
		public virtual string Name { get; set; } = "Custom";

		/// <inheritdoc/>
		public virtual string Description { get; set; } = string.Empty;

		/// <inheritdoc/>
		public virtual ThemeMode Mode { get; set; } = ThemeMode.Dark;

		// --- Non-color members ---

		/// <inheritdoc/>
		public virtual char DesktopBackgroundChar { get; set; } = ' ';

		/// <inheritdoc/>
		public virtual GradientBackground? DesktopBackgroundGradient { get; set; }

		/// <inheritdoc/>
		public virtual bool ShowModalShadow { get; set; } = true;

		/// <inheritdoc/>
		public virtual bool UseDoubleLineBorderForModal { get; set; } = false;

		// --- Window / border / title colors ---

		/// <inheritdoc/>
		public virtual Color? ActiveBorderForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ActiveTitleForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? PrimaryColor { get; set; }
		/// <inheritdoc/>
		public virtual Color? SecondaryColor { get; set; }
		/// <inheritdoc/>
		public virtual Color? TertiaryColor { get; set; }
		/// <inheritdoc/>
		public virtual Color? InfoColor { get; set; }
		/// <inheritdoc/>
		public virtual Color? SuccessColor { get; set; }
		/// <inheritdoc/>
		public virtual Color? WarningColor { get; set; }
		/// <inheritdoc/>
		public virtual Color? DangerColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? InactiveBorderForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? InactiveTitleForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color WindowBackgroundColor { get; set; } = Color.Transparent;

		/// <inheritdoc/>
		public virtual Color WindowForegroundColor { get; set; } = Color.White;

		// --- Top / bottom bars ---

		/// <inheritdoc/>
		public virtual Color? TopBarBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TopBarForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? BottomBarBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? BottomBarForegroundColor { get; set; }

		// --- Buttons ---

		/// <inheritdoc/>
		public virtual Color? ButtonBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonSelectedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonSelectedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonDisabledBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ButtonDisabledForegroundColor { get; set; }

		// --- Modal windows ---

		/// <inheritdoc/>
		public virtual Color? ModalBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ModalBorderForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ModalTitleForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ModalFlashColor { get; set; }

		// --- Notifications ---

		/// <inheritdoc/>
		public virtual Color? NotificationWindowBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? NotificationInfoWindowBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? NotificationSuccessWindowBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? NotificationWarningWindowBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? NotificationDangerWindowBackgroundColor { get; set; }

		// --- Desktop ---

		/// <inheritdoc/>
		public virtual Color? DesktopBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DesktopForegroundColor { get; set; }

		// --- Prompt input ---

		/// <inheritdoc/>
		public virtual Color? PromptInputBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? PromptInputForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? PromptInputFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? PromptInputFocusedForegroundColor { get; set; }

		// --- Text edit ---

		/// <inheritdoc/>
		public virtual Color? TextEditFocusedNotEditing { get; set; }

		// --- Scrollbars (general) ---

		/// <inheritdoc/>
		public virtual Color? ScrollbarThumbColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ScrollbarThumbUnfocusedColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ScrollbarTrackColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ScrollbarTrackUnfocusedColor { get; set; }

		// --- Lists ---

		/// <inheritdoc/>
		public virtual Color? ListUnfocusedHighlightBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListUnfocusedHighlightForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListHoverBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListHoverForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListSelectedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListSelectedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListDisabledForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ListDisabledBackgroundColor { get; set; }

		// --- Menu bar ---

		/// <inheritdoc/>
		public virtual Color? MenuBarBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? MenuBarForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? MenuBarHighlightBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? MenuBarHighlightForegroundColor { get; set; }

		// --- Menu dropdowns ---

		/// <inheritdoc/>
		public virtual Color? MenuDropdownBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? MenuDropdownForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? MenuDropdownHighlightBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? MenuDropdownHighlightForegroundColor { get; set; }

		// --- Dropdown control ---

		/// <inheritdoc/>
		public virtual Color? DropdownBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownHighlightBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownHighlightForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownDisabledForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DropdownDisabledBackgroundColor { get; set; }

		// --- Progress bar ---

		/// <inheritdoc/>
		public virtual Color? ProgressBarFilledColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ProgressBarUnfilledColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ProgressBarPercentageColor { get; set; }

		// --- Tables ---

		/// <inheritdoc/>
		public virtual Color? TableBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableBorderColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableHeaderBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableHeaderForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableSelectionBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableSelectionForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableHoverBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableHoverForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableUnfocusedSelectionBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableUnfocusedSelectionForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableScrollbarThumbColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TableScrollbarTrackColor { get; set; }

		// --- Tab headers ---

		/// <inheritdoc/>
		public virtual Color? TabHeaderActiveBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderActiveForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderDisabledForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderDisabledBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderActiveFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderActiveFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabHeaderFocusedForegroundColor { get; set; }

		// --- Tab content ---

		/// <inheritdoc/>
		public virtual Color? TabContentBorderColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TabContentBackgroundColor { get; set; }

		// --- Collapsible panel header ---

		/// <inheritdoc/>
		public virtual Color? CollapsibleHeaderFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CollapsibleHeaderFocusedBackgroundColor { get; set; }

		// --- Toolbar / separator ---

		/// <inheritdoc/>
		public virtual Color? ToolbarBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? ToolbarForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? SeparatorForegroundColor { get; set; }

		// --- DatePicker ---

		/// <inheritdoc/>
		public virtual Color? DatePickerBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerSegmentBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerSegmentForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerDisabledForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerDisabledBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerCalendarTodayColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerCalendarSelectedColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? DatePickerCalendarHeaderColor { get; set; }

		// --- TimePicker ---

		/// <inheritdoc/>
		public virtual Color? TimePickerBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TimePickerForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TimePickerFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TimePickerFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TimePickerSegmentBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TimePickerSegmentForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TimePickerDisabledForegroundColor { get; set; }

		// --- StatusBar ---

		/// <inheritdoc/>
		public virtual Color? StatusBarBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? StatusBarForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? StatusBarShortcutForegroundColor { get; set; }

		// --- Slider ---

		/// <inheritdoc/>
		public virtual Color? SliderTrackColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? SliderFilledTrackColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? SliderThumbColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? SliderFocusedThumbColor { get; set; }

		// --- Checkbox ---

		/// <inheritdoc/>
		public virtual Color? CheckboxBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CheckboxFocusedBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CheckboxDisabledBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CheckboxForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CheckboxFocusedForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CheckboxDisabledForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? CheckboxCheckmarkColor { get; set; }

		// --- Tree ---

		/// <inheritdoc/>
		public virtual Color? TreeBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TreeSelectionBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? TreeUnfocusedSelectionBackgroundColor { get; set; }

		// --- Graphs ---

		/// <inheritdoc/>
		public virtual Color? LineGraphBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? BarGraphBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? SparklineBackgroundColor { get; set; }

		// --- Html ---

		/// <inheritdoc/>
		public virtual Color? HtmlForegroundColor { get; set; }

		// --- Start menu ---

		/// <inheritdoc/>
		public virtual Color? StartMenuHeaderBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? StartMenuHeaderForegroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? StartMenuSectionHeaderBackgroundColor { get; set; }

		/// <inheritdoc/>
		public virtual Color? StartMenuInfoStripForegroundColor { get; set; }
	}
}
