using ConsoleEx.Contents;
using ConsoleEx.Themes;
using Spectre.Console;
using System.Threading;

namespace ConsoleEx.Services
{
	public enum NotificationSeverityEnum
	{
		Danger,
		Info,
		None,
		Success,
		Warning
	}

	public class NotificationSeverity
	{
		public static readonly NotificationSeverity Danger = new NotificationSeverity(
			"Error", "❌", NotificationSeverityEnum.Danger);

		public static readonly NotificationSeverity Info = new NotificationSeverity(
			"Info", "ℹ️", NotificationSeverityEnum.Info);

		public static readonly NotificationSeverity None = new NotificationSeverity(
			"Notification", "", NotificationSeverityEnum.None);

		public static readonly NotificationSeverity Success = new NotificationSeverity(
			"Success", "✔️", NotificationSeverityEnum.Success);

		public static readonly NotificationSeverity Warning = new NotificationSeverity(
			"Warning", "⚠️", NotificationSeverityEnum.Warning);

		public NotificationSeverityEnum Severity;

		private NotificationSeverity(string name, string icon, NotificationSeverityEnum severity)
		{
			Name = name;
			Icon = icon;
			Severity = severity;
		}

		public string Icon { get; }

		public string? Name { get; }

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

		public Color BackgroundColor(ConsoleWindowSystem consoleWindowSystem)
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