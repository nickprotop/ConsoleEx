// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Configuration
{
	/// <summary>
	/// Centralized default values and constants for control behavior.
	/// Extracted from magic numbers scattered throughout the codebase.
	/// </summary>
	public static class ControlDefaults
	{
		// Layout defaults
		/// <summary>
		/// Minimum number of visible items in lists/trees before scrolling (default: 3)
		/// </summary>
		public const int DefaultMinimumVisibleItems = 3;

		/// <summary>
		/// Default number of visible items in lists/trees (default: 10)
		/// </summary>
		public const int DefaultVisibleItems = 10;

		/// <summary>
		/// Default padding around controls (default: 1)
		/// </summary>
		public const int DefaultPadding = 1;

		/// <summary>
		/// Default border width for controls (default: 1)
		/// </summary>
		public const int DefaultBorderWidth = 1;

		// Text defaults
		/// <summary>
		/// Default padding around text content: "  text  " (default: 4 total, 2 each side)
		/// </summary>
		public const int DefaultTextPadding = 4;

		/// <summary>
		/// Default padding for window titles: "[ title ]" (default: 5 for brackets and spaces)
		/// </summary>
		public const int DefaultTitlePadding = 5;

		/// <summary>
		/// Length of ellipsis when truncating text: "..." (default: 3)
		/// </summary>
		public const int DefaultEllipsisLength = 3;

		/// <summary>
		/// Minimum width for text fields (default: 3)
		/// </summary>
		public const int DefaultMinTextWidth = 3;

		// Scrolling defaults
		/// <summary>
		/// Number of lines to scroll per arrow key press (default: 1)
		/// </summary>
		public const int DefaultScrollStep = 1;

		/// <summary>
		/// Multiplier for page up/down scrolling (default: 5x viewport height)
		/// </summary>
		public const int DefaultPageScrollMultiplier = 5;

		// Input defaults
		/// <summary>
		/// Debounce delay for rapid input events in milliseconds (default: 300ms)
		/// </summary>
		public const int DefaultDebounceMs = 300;

		/// <summary>
		/// Interval for continuous mouse button press polling in milliseconds (default: 100ms)
		/// </summary>
		public const int ContinuousPressIntervalMs = 100;

		/// <summary>
		/// Cursor blink rate for text inputs in milliseconds (default: 500ms)
		/// </summary>
		public const int DefaultBlinkRateMs = 500;

		// Tree/List defaults
		/// <summary>
		/// Indentation size for nested tree nodes (default: 2 spaces)
		/// </summary>
		public const int DefaultIndentSize = 2;

		/// <summary>
		/// Icon for expanded tree nodes (default: "▼")
		/// </summary>
		public const string DefaultExpandedIcon = "▼";

		/// <summary>
		/// Icon for collapsed tree nodes (default: "▶")
		/// </summary>
		public const string DefaultCollapsedIcon = "▶";

		/// <summary>
		/// Selection indicator prefix (default: ">")
		/// </summary>
		public const string DefaultSelectionIndicator = ">";

		// Button defaults
		/// <summary>
		/// Prefix shown before focused button text (default: ">")
		/// </summary>
		public const string DefaultFocusPrefix = ">";

		/// <summary>
		/// Suffix shown after focused button text (default: "&lt;")
		/// </summary>
		public const string DefaultFocusSuffix = "<";

		// Dialog defaults
		/// <summary>
		/// Default width for dialog windows (default: 60 characters)
		/// </summary>
		public const int DefaultDialogWidth = 60;

		/// <summary>
		/// Default height for dialog windows (default: 20 lines)
		/// </summary>
		public const int DefaultDialogHeight = 20;

		// Double-click defaults
		/// <summary>
		/// Maximum time between clicks to register as double-click in milliseconds (default: 500ms)
		/// </summary>
		public const int DefaultDoubleClickThresholdMs = 500;

		// Window defaults
		/// <summary>
		/// Minimum height for windows in character rows (default: 3)
		/// </summary>
		public const int DefaultWindowMinimumHeight = 3;

		/// <summary>
		/// Minimum width for windows in character columns (default: 10)
		/// </summary>
		public const int DefaultWindowMinimumWidth = 10;

		/// <summary>
		/// Default height for new windows (default: 20 lines)
		/// </summary>
		public const int DefaultWindowHeight = 20;

		/// <summary>
		/// Default width for new windows (default: 40 characters)
		/// </summary>
		public const int DefaultWindowWidth = 40;

		// Window lifecycle defaults
		/// <summary>
		/// Timeout for async window thread cleanup in seconds (default: 5)
		/// </summary>
		public const int AsyncCleanupTimeoutSeconds = 5;

		/// <summary>
		/// Warning threshold during grace period in seconds (default: 3)
		/// Shows countdown when remaining time falls below this
		/// </summary>
		public const int GracePeriodWarningThresholdSeconds = 3;

		/// <summary>
		/// Delay before transforming hung window to error state in milliseconds (default: 500ms)
		/// </summary>
		public const int ErrorTransformDelayMs = 500;

		// Error window layout defaults
		/// <summary>
		/// Minimum width for error windows (default: 50 characters)
		/// </summary>
		public const int MinimumErrorWindowWidth = 50;

		/// <summary>
		/// Border offset for error window sizing (default: 4)
		/// </summary>
		public const int ErrorWindowBorderOffset = 4;

		/// <summary>
		/// Spacing offset for error window sizing (default: 6)
		/// </summary>
		public const int ErrorWindowSpacingOffset = 6;

		// MultilineEditControl defaults
		/// <summary>
		/// Fallback width for multiline editor when effective width is unknown (default: 80)
		/// </summary>
		public const int DefaultEditorWidth = 80;

		/// <summary>
		/// Default viewport height for multiline editor in lines (default: 10)
		/// </summary>
		public const int DefaultEditorViewportHeight = 10;

		/// <summary>
		/// Number of lines to scroll per mouse wheel tick (default: 3)
		/// </summary>
		public const int DefaultScrollWheelLines = 3;

		/// <summary>
		/// Default tab size in spaces for multiline editor (default: 4)
		/// </summary>
		public const int DefaultTabSize = 4;

		/// <summary>
		/// Maximum allowed tab size in spaces (default: 8)
		/// </summary>
		public const int MaxTabSize = 8;

		/// <summary>
		/// Default maximum undo history depth (default: 100)
		/// </summary>
		public const int DefaultUndoLimit = 100;

		/// <summary>
		/// Character displayed for space characters when visible whitespace is enabled (middle dot U+00B7)
		/// </summary>
		public const char WhitespaceSpaceChar = '\u00B7';

		/// <summary>
		/// Number of spaces after line numbers in the gutter (default: 1)
		/// </summary>
		public const int LineNumberGutterPadding = 1;

		/// <summary>
		/// Hint text shown when the editor is focused but not in editing mode
		/// </summary>
		public const string BrowseModeHint = "Enter to edit";

		/// <summary>
		/// Hint text shown when the editor is in editing mode
		/// </summary>
		public const string EditingModeHint = "Esc to stop editing";

		// Modal window positioning defaults
		/// <summary>
		/// Left offset for modal window positioning relative to parent (default: 5)
		/// </summary>
		public const int ModalWindowLeftOffset = 5;

		/// <summary>
		/// Top offset for modal window positioning relative to parent (default: 3)
		/// </summary>
		public const int ModalWindowTopOffset = 3;

		// Terminal defaults
		/// <summary>
		/// Default number of scrollback lines retained by the terminal control (default: 1000)
		/// </summary>
		public const int DefaultTerminalScrollbackLines = 1000;

		/// <summary>
		/// Number of lines to scroll per mouse wheel tick in the terminal control (default: 3)
		/// </summary>
		public const int DefaultTerminalScrollWheelLines = 3;

		// Menu control defaults
		/// <summary>
		/// Delay in milliseconds before a submenu opens on hover (default: 150ms)
		/// </summary>
		public const int MenuSubmenuHoverDelayMs = 150;

		/// <summary>
		/// Maximum number of items visible in a dropdown before scrolling (default: 20)
		/// </summary>
		public const int MenuMaxDropdownHeight = 20;

		/// <summary>
		/// Minimum width for a dropdown menu in characters (default: 15)
		/// </summary>
		public const int MenuDropdownMinWidth = 15;

		/// <summary>
		/// Maximum width for a dropdown menu in characters (default: 50)
		/// </summary>
		public const int MenuDropdownMaxWidth = 50;

		/// <summary>
		/// Horizontal padding added to each menu bar item: " text " (default: 4, 2 each side)
		/// </summary>
		public const int MenuItemHorizontalPadding = 4;

		/// <summary>
		/// Extra padding for dropdown item width calculation including shortcut spacing (default: 10)
		/// </summary>
		public const int MenuItemDropdownPadding = 10;

		// Notification defaults
		/// <summary>
		/// Horizontal padding added to notification window width beyond message length (default: 8)
		/// </summary>
		public const int NotificationHorizontalPadding = 8;

		/// <summary>
		/// Vertical padding added to notification window height beyond message line count (default: 5)
		/// </summary>
		public const int NotificationVerticalPadding = 5;

		/// <summary>
		/// Default auto-dismiss timeout for notifications in milliseconds (default: 5000)
		/// </summary>
		public const int NotificationDefaultTimeoutMs = 5000;

		// Canvas control defaults
		/// <summary>
		/// Default canvas width in characters (default: 40).
		/// </summary>
		public const int DefaultCanvasWidth = 40;

		/// <summary>
		/// Default canvas height in characters (default: 20).
		/// </summary>
		public const int DefaultCanvasHeight = 20;

		/// <summary>
		/// Minimum allowed canvas dimension in either axis (default: 1).
		/// </summary>
		public const int MinCanvasSize = 1;

		// NavigationView defaults
		/// <summary>
		/// Default width of the NavigationView left pane in characters (default: 26).
		/// </summary>
		public const int DefaultNavigationViewPaneWidth = 26;

		/// <summary>
		/// Minimum width of the NavigationView left pane in characters (default: 10).
		/// </summary>
		public const int MinNavigationViewPaneWidth = 10;

		/// <summary>
		/// Red component of the NavigationView selected item background (default: 40).
		/// </summary>
		public const int NavigationViewSelectedBgR = 40;

		/// <summary>
		/// Green component of the NavigationView selected item background (default: 50).
		/// </summary>
		public const int NavigationViewSelectedBgG = 50;

		/// <summary>
		/// Blue component of the NavigationView selected item background (default: 80).
		/// </summary>
		public const int NavigationViewSelectedBgB = 80;

		/// <summary>
		/// Extra indent (in characters) applied to sub-items under a header (default: 2).
		/// </summary>
		public const int NavigationViewSubItemExtraIndent = 2;

		/// <summary>
		/// Indicator shown before expanded header text (default: "[-]").
		/// </summary>
		public const string NavigationViewExpandedIndicator = "[-]";

		/// <summary>
		/// Indicator shown before collapsed header text (default: "[+]").
		/// </summary>
		public const string NavigationViewCollapsedIndicator = "[+]";

		/// <summary>
		/// Number of blank lines rendered above a header for visual spacing (default: 1).
		/// Skipped for the first header in the list.
		/// </summary>
		public const int NavigationViewHeaderTopMargin = 1;

		/// <summary>
		/// Width threshold at or above which Auto display mode resolves to Expanded (default: 80).
		/// </summary>
		public const int DefaultNavigationViewExpandedThreshold = 80;

		/// <summary>
		/// Width threshold at or above which Auto display mode resolves to Compact (default: 50).
		/// Below this threshold, Auto resolves to Minimal.
		/// </summary>
		public const int DefaultNavigationViewCompactThreshold = 50;

		/// <summary>
		/// Width of the navigation pane in Compact display mode (default: 5).
		/// </summary>
		public const int DefaultNavigationViewCompactPaneWidth = 5;

		/// <summary>
		/// Duration in milliseconds for navigation pane width transition animations (default: 200).
		/// </summary>
		public const int NavigationViewTransitionDurationMs = 200;

		/// <summary>
		/// Character used as the hamburger menu icon in Compact and Minimal modes.
		/// </summary>
		public const char NavigationViewHamburgerChar = '\u2261';

		/// <summary>
		/// Width of the clickable area for the hamburger icon in the content header (default: 3).
		/// Only clicks within this many characters from the left edge open the navigation portal.
		/// </summary>
		public const int NavigationViewHamburgerClickWidth = 3;

		/// <summary>
		/// Fixed character overhead per navigation item row (leading spaces + indicator + trailing space).
		/// </summary>
		public const int NavigationViewItemOverhead = 4;

		// DatePicker defaults

		/// <summary>
		/// Number of columns in the calendar grid (days of week).
		/// </summary>
		public const int CalendarGridColumns = 7;

		/// <summary>
		/// Width of each day column in the calendar grid (3 chars: space + 2 digits).
		/// </summary>
		public const int CalendarDayColumnWidth = 3;

		/// <summary>
		/// Maximum number of week rows in the calendar grid.
		/// </summary>
		public const int CalendarGridRows = 6;

		/// <summary>
		/// Total width of the calendar portal in characters.
		/// </summary>
		public const int CalendarPortalWidth = 28;

		/// <summary>
		/// Total height of the calendar portal in rows.
		/// </summary>
		public const int CalendarPortalHeight = 10;

		/// <summary>
		/// Arrow character for navigating to the previous month.
		/// </summary>
		public const string CalendarPrevMonthArrow = "\u25C4";

		/// <summary>
		/// Arrow character for navigating to the next month.
		/// </summary>
		public const string CalendarNextMonthArrow = "\u25BA";

		/// <summary>
		/// Default date format when no culture-specific format is provided.
		/// </summary>
		public const string CalendarDefaultDateFormat = "yyyy-MM-dd";

		/// <summary>
		/// Default prompt text for DatePicker controls.
		/// </summary>
		public const string DatePickerDefaultPrompt = "Date:";

		/// <summary>
		/// Dropdown indicator character for the DatePicker calendar toggle.
		/// </summary>
		public const string DatePickerDropdownIndicator = "\u25BC";

		// LineGraph defaults

		/// <summary>
		/// Default height of the graph area in lines (default: 10).
		/// </summary>
		public const int LineGraphDefaultHeight = 10;

		/// <summary>
		/// Default maximum number of data points per series (default: 100).
		/// </summary>
		public const int LineGraphDefaultMaxDataPoints = 100;

		/// <summary>
		/// Minimum height of the graph area in lines (default: 3).
		/// </summary>
		public const int LineGraphMinHeight = 3;

		/// <summary>
		/// Padding between Y-axis labels and the graph area (default: 1).
		/// </summary>
		public const int LineGraphYAxisLabelPadding = 1;

		/// <summary>
		/// Default format string for Y-axis labels (default: "F1").
		/// </summary>
		public const string LineGraphDefaultAxisFormat = "F1";

		/// <summary>
		/// Color for empty braille/ASCII cells in line graphs.
		/// </summary>
		public static readonly Color LineGraphEmptyCellColor = Color.Grey19;

		/// <summary>
		/// Default character for reference lines.
		/// </summary>
		public const char LineGraphDefaultReferenceLineChar = '─';

		/// <summary>
		/// Default color for reference lines.
		/// </summary>
		public static readonly Color LineGraphDefaultReferenceLineColor = Color.Grey50;

		/// <summary>
		/// Character used for the legend color marker (horizontal line segment).
		/// </summary>
		public const char LineGraphLegendMarkerChar = '\u2501'; // ━

		/// <summary>
		/// Width of the legend marker including trailing space (marker char + space).
		/// </summary>
		public const int LineGraphLegendMarkerWidth = 2;

		/// <summary>
		/// Gap between legend entries in characters.
		/// </summary>
		public const int LineGraphLegendEntryGap = 2;

		/// <summary>
		/// Padding between graph edge and marker arrow/label.
		/// </summary>
		public const int LineGraphMarkerPadding = 1;

		/// <summary>
		/// Right-side marker arrow character (points left, towards graph).
		/// Uses small triangle (U+25C2) which is reliably 1-column wide.
		/// </summary>
		public const string LineGraphMarkerArrowRight = "\u25C2";

		/// <summary>
		/// Left-side marker arrow character (points right, towards graph).
		/// Uses small triangle (U+25B8) which is reliably 1-column wide.
		/// </summary>
		public const string LineGraphMarkerArrowLeft = "\u25B8";

		// Dropdown defaults

		/// <summary>
		/// Arrow indicator for a closed dropdown (points down).
		/// Uses small triangle (U+25BE) which is reliably 1-column wide across terminals.
		/// </summary>
		public const string DropdownClosedArrow = "\u25BE";

		/// <summary>
		/// Arrow indicator for an open dropdown (points up).
		/// Uses small triangle (U+25B4) which is reliably 1-column wide across terminals.
		/// </summary>
		public const string DropdownOpenArrow = "\u25B4";

		/// <summary>
		/// Scroll-up indicator for dropdown portal.
		/// Uses small triangle (U+25B4) which is reliably 1-column wide across terminals.
		/// </summary>
		public const string DropdownScrollUpArrow = "\u25B4";

		/// <summary>
		/// Scroll-down indicator for dropdown portal.
		/// Uses small triangle (U+25BE) which is reliably 1-column wide across terminals.
		/// </summary>
		public const string DropdownScrollDownArrow = "\u25BE";

		// TimePicker defaults

		/// <summary>
		/// Display width of a time segment (hour, minute, second) in characters.
		/// </summary>
		public const int TimeSegmentWidth = 2;

		/// <summary>
		/// Display width of the AM/PM segment in characters.
		/// </summary>
		public const int TimeAmPmSegmentWidth = 2;

		/// <summary>
		/// Step size for large increment/decrement operations (Page Up/Down).
		/// </summary>
		public const int TimeLargeIncrementStep = 10;

		/// <summary>
		/// Default prompt text for TimePicker controls.
		/// </summary>
		public const string TimePickerDefaultPrompt = "Time:";

		// Shared date/time defaults

		/// <summary>
		/// Timeout in milliseconds before a pending first digit is auto-committed.
		/// </summary>
		public const int SegmentPendingDigitTimeoutMs = 1500;

		// HorizontalSplitter defaults

		/// <summary>
		/// Minimum height for controls adjacent to a horizontal splitter (default: 3).
		/// </summary>
		public const int HorizontalSplitterMinControlHeight = 3;

		/// <summary>
		/// Number of rows moved per Shift+Arrow key press on horizontal splitter (default: 5).
		/// </summary>
		public const int HorizontalSplitterKeyboardJumpSize = 5;

		// Toolbar defaults

		/// <summary>
		/// Default row height for toolbar rows when no explicit height is set and
		/// all items measure as height 1 (default: 1).
		/// </summary>
		public const int DefaultToolbarRowHeight = 1;

		/// <summary>
		/// Character used for toolbar separator lines (horizontal box drawing: ─).
		/// </summary>
		public const char ToolbarLineCharacter = '\u2500';

		// StatusBar defaults

		/// <summary>
		/// Default height for the status bar in rows (default: 1).
		/// </summary>
		public const int StatusBarDefaultHeight = 1;

		/// <summary>
		/// Default spacing between status bar items in characters (default: 2).
		/// </summary>
		public const int StatusBarItemSpacing = 2;

		/// <summary>
		/// Default separator character between status bar sections (default: "|").
		/// </summary>
		public const string StatusBarSeparatorChar = "|";

		/// <summary>
		/// Default separator between shortcut and label text (default: ":").
		/// </summary>
		public const string StatusBarShortcutLabelSeparator = ":";

		// Slider defaults

		/// <summary>
		/// Default minimum value for slider controls (default: 0.0).
		/// </summary>
		public const double SliderDefaultMinValue = 0.0;

		/// <summary>
		/// Default maximum value for slider controls (default: 100.0).
		/// </summary>
		public const double SliderDefaultMaxValue = 100.0;

		/// <summary>
		/// Default step increment for slider controls (default: 1.0).
		/// </summary>
		public const double SliderDefaultStep = 1.0;

		/// <summary>
		/// Default large step increment for Page Up/Down and Shift+Arrow (default: 10.0).
		/// </summary>
		public const double SliderDefaultLargeStep = 10.0;

		/// <summary>
		/// Minimum allowed step value to prevent zero-step sliders (default: 0.001).
		/// </summary>
		public const double SliderMinStep = 0.001;

		/// <summary>
		/// Default minimum range gap between low and high thumbs on a RangeSlider (default: 0.0).
		/// </summary>
		public const double RangeSliderDefaultMinRange = 0.0;

		/// <summary>
		/// Minimum track length in characters for slider controls (default: 3).
		/// </summary>
		public const int SliderMinTrackLength = 3;

		/// <summary>
		/// Hit radius in characters around a thumb for mouse click detection (default: 1).
		/// </summary>
		public const int SliderThumbHitRadius = 1;

		/// <summary>
		/// Character used for the slider thumb indicator (U+25CF: ●).
		/// </summary>
		public const char SliderThumbChar = '\u25CF';

		/// <summary>
		/// Character used for the filled portion of a horizontal slider track (U+2501: ━).
		/// </summary>
		public const char SliderFilledTrackChar = '\u2501';

		/// <summary>
		/// Character used for the unfilled portion of a horizontal slider track (U+2500: ─).
		/// </summary>
		public const char SliderUnfilledTrackChar = '\u2500';

		/// <summary>
		/// Character used for unfilled portion of a vertical slider track (U+2502: │).
		/// </summary>
		public const char SliderVerticalTrackChar = '\u2502';

		/// <summary>
		/// Character used for filled portion of a vertical slider track (U+2503: ┃).
		/// </summary>
		public const char SliderVerticalFilledTrackChar = '\u2503';

		/// <summary>
		/// Default format string for the slider value label (default: "F0").
		/// </summary>
		public const string SliderDefaultValueFormat = "F0";

		/// <summary>
		/// Spacing in characters between the track and value/min/max labels (default: 1).
		/// </summary>
		public const int SliderLabelSpacing = 1;

		/// <summary>
		/// Left end-cap character for horizontal slider tracks (U+2502: │).
		/// </summary>
		public const char SliderHorizontalLeftCap = '\u2502';

		/// <summary>
		/// Right end-cap character for horizontal slider tracks (U+2502: │).
		/// </summary>
		public const char SliderHorizontalRightCap = '\u2502';

		/// <summary>
		/// Top end-cap character for vertical slider tracks (U+2500: ─).
		/// </summary>
		public const char SliderVerticalTopCap = '\u2500';

		/// <summary>
		/// Bottom end-cap character for vertical slider tracks (U+2500: ─).
		/// </summary>
		public const char SliderVerticalBottomCap = '\u2500';

	}
}
