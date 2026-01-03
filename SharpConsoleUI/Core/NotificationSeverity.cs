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
	/// </summary>
	public class NotificationSeverity
	{
		/// <summary>
		/// Represents a danger/error notification severity with a red visual theme.
		/// </summary>
		public static readonly NotificationSeverity Danger = new NotificationSeverity(
			"Error", "❌", NotificationSeverityEnum.Danger);

		/// <summary>
		/// Represents an informational notification severity with a blue visual theme.
		/// </summary>
		public static readonly NotificationSeverity Info = new NotificationSeverity(
			"Info", "ℹ️", NotificationSeverityEnum.Info);

		/// <summary>
		/// Represents a generic notification with no specific severity.
		/// </summary>
		public static readonly NotificationSeverity None = new NotificationSeverity(
			"Notification", "", NotificationSeverityEnum.None);

		/// <summary>
		/// Represents a success notification severity with a green visual theme.
		/// </summary>
		public static readonly NotificationSeverity Success = new NotificationSeverity(
			"Success", "✔️", NotificationSeverityEnum.Success);

		/// <summary>
		/// Represents a warning notification severity with a yellow visual theme.
		/// </summary>
		public static readonly NotificationSeverity Warning = new NotificationSeverity(
			"Warning", "⚠️", NotificationSeverityEnum.Warning);

		/// <summary>
		/// Gets the severity level enum value.
		/// </summary>
		public NotificationSeverityEnum Severity;

		private NotificationSeverity(string name, string icon, NotificationSeverityEnum severity)
		{
			Name = name;
			Icon = icon;
			Severity = severity;
		}

		/// <summary>
		/// Gets the icon associated with this severity level.
		/// </summary>
		public string Icon { get; }

		/// <summary>
		/// Gets the display name for this severity level.
		/// </summary>
		public string? Name { get; }

		/// <summary>
		/// Gets a <see cref="NotificationSeverity"/> instance from a severity enum value.
		/// </summary>
		/// <param name="severity">The severity enum value.</param>
		/// <returns>The corresponding <see cref="NotificationSeverity"/> instance.</returns>
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

		/// <summary>
		/// Gets the active border foreground color for this severity level.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system to get theme colors from.</param>
		/// <returns>The foreground color for active window borders.</returns>
		public Color ActiveBorderForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Severity switch
			{
				NotificationSeverityEnum.Danger => Color.White,
				NotificationSeverityEnum.Info => Color.White,
				NotificationSeverityEnum.None => Color.White,
				NotificationSeverityEnum.Success => Color.White,
				NotificationSeverityEnum.Warning => Color.White,
				_ => throw new NotImplementedException()
			};
		}

		/// <summary>
		/// Gets the active title foreground color for this severity level.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system to get theme colors from.</param>
		/// <returns>The foreground color for active window titles.</returns>
		public Color ActiveTitleForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Severity switch
			{
				NotificationSeverityEnum.Danger => Color.White,
				NotificationSeverityEnum.Info => Color.White,
				NotificationSeverityEnum.None => Color.White,
				NotificationSeverityEnum.Success => Color.White,
				NotificationSeverityEnum.Warning => Color.White,
				_ => throw new NotImplementedException()
			};
		}

		/// <summary>
		/// Gets the inactive border foreground color for this severity level.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system to get theme colors from.</param>
		/// <returns>The foreground color for inactive window borders.</returns>
		public Color InactiveBorderForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Severity switch
			{
				NotificationSeverityEnum.Danger => Color.White,
				NotificationSeverityEnum.Info => Color.White,
				NotificationSeverityEnum.None => Color.White,
				NotificationSeverityEnum.Success => Color.White,
				NotificationSeverityEnum.Warning => Color.White,
				_ => throw new NotImplementedException()
			};
		}

		/// <summary>
		/// Gets the inactive title foreground color for this severity level.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system to get theme colors from.</param>
		/// <returns>The foreground color for inactive window titles.</returns>
		public Color InactiveTitleForegroundColor(ConsoleWindowSystem consoleWindowSystem)
		{
			return Severity switch
			{
				NotificationSeverityEnum.Danger => Color.White,
				NotificationSeverityEnum.Info => Color.White,
				NotificationSeverityEnum.None => Color.White,
				NotificationSeverityEnum.Success => Color.White,
				NotificationSeverityEnum.Warning => Color.White,
				_ => throw new NotImplementedException()
			};
		}

		/// <summary>
		/// Gets the window background color for this severity level based on the current theme.
		/// </summary>
		/// <param name="consoleWindowSystem">The console window system to get theme colors from.</param>
		/// <returns>The background color for notification windows of this severity.</returns>
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
