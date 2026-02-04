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

		// Modal window positioning defaults
		/// <summary>
		/// Left offset for modal window positioning relative to parent (default: 5)
		/// </summary>
		public const int ModalWindowLeftOffset = 5;

		/// <summary>
		/// Top offset for modal window positioning relative to parent (default: 3)
		/// </summary>
		public const int ModalWindowTopOffset = 3;
	}
}
