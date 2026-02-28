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
	}
}
