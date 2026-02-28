// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Specifies the severity level for notifications.
	/// </summary>
	public enum NotificationSeverityEnum
	{
		/// <summary>Indicates a dangerous or error condition.</summary>
		Danger,
		/// <summary>Indicates an informational message.</summary>
		Info,
		/// <summary>Indicates no specific severity.</summary>
		None,
		/// <summary>Indicates a successful operation.</summary>
		Success,
		/// <summary>Indicates a warning condition.</summary>
		Warning
	}

	/// <summary>
	/// Represents a notification severity level with associated visual properties such as colors and icons.
	/// Uses single-cell-width Unicode characters for reliable console rendering.
	/// </summary>
	public class NotificationSeverity
	{
		/// <summary>Danger/error severity with red theme. Icon: ✘ (U+2718).</summary>
		public static readonly NotificationSeverity Danger = new NotificationSeverity(
			"Error", "\u2718", NotificationSeverityEnum.Danger);

		/// <summary>Informational severity with blue theme. Icon: ● (U+25CF).</summary>
		public static readonly NotificationSeverity Info = new NotificationSeverity(
			"Info", "\u25cf", NotificationSeverityEnum.Info);

		/// <summary>Generic notification with no specific severity.</summary>
		public static readonly NotificationSeverity None = new NotificationSeverity(
			"Notification", "", NotificationSeverityEnum.None);

		/// <summary>Success severity with green theme. Icon: ✔ (U+2714).</summary>
		public static readonly NotificationSeverity Success = new NotificationSeverity(
			"Success", "\u2714", NotificationSeverityEnum.Success);

		/// <summary>Warning severity with yellow theme. Icon: ▲ (U+25B2).</summary>
		public static readonly NotificationSeverity Warning = new NotificationSeverity(
			"Warning", "\u25b2", NotificationSeverityEnum.Warning);

		/// <summary>Gets the severity level enum value.</summary>
		public NotificationSeverityEnum Severity { get; }

		private NotificationSeverity(string name, string icon, NotificationSeverityEnum severity)
		{
			Name = name;
			Icon = icon;
			Severity = severity;
		}

		/// <summary>Gets the icon character for this severity level.</summary>
		public string Icon { get; }

		/// <summary>Gets the display name for this severity level.</summary>
		public string? Name { get; }

		/// <summary>
		/// Gets a <see cref="NotificationSeverity"/> instance from a severity enum value.
		/// </summary>
		/// <exception cref="NotImplementedException">Thrown if an unknown severity value is provided.</exception>
		public static NotificationSeverity FromSeverity(NotificationSeverityEnum severity)
		{
			return severity switch
			{
				NotificationSeverityEnum.Danger => Danger,
				NotificationSeverityEnum.Info => Info,
				NotificationSeverityEnum.None => None,
				NotificationSeverityEnum.Success => Success,
				NotificationSeverityEnum.Warning => Warning,
				_ => throw new NotImplementedException()
			};
		}

		/// <summary>Gets the active border foreground color (always white).</summary>
		public Color ActiveBorderForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Color.White;
		}

		/// <summary>Gets the active title foreground color (always white).</summary>
		public Color ActiveTitleForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Color.White;
		}

		/// <summary>Gets the inactive border foreground color (always white).</summary>
		public Color InactiveBorderForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Color.White;
		}

		/// <summary>Gets the inactive title foreground color (always white).</summary>
		public Color InactiveTitleForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Color.White;
		}

		/// <summary>
		/// Gets the window background color for this severity level based on the current theme.
		/// </summary>
		public Color WindowBackgroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Severity switch
			{
				NotificationSeverityEnum.Danger => consoleWindowSystem.Theme.NotificationDangerWindowBackgroundColor,
				NotificationSeverityEnum.Info => consoleWindowSystem.Theme.NotificationInfoWindowBackgroundColor,
				NotificationSeverityEnum.None => consoleWindowSystem.Theme.NotificationWindowBackgroundColor,
				NotificationSeverityEnum.Success => consoleWindowSystem.Theme.NotificationSuccessWindowBackgroundColor,
				NotificationSeverityEnum.Warning => consoleWindowSystem.Theme.NotificationWarningWindowBackgroundColor,
				_ => throw new NotImplementedException()
			};
		}
	}
}
