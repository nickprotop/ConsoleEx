// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Maps a <see cref="LogLevel"/> to the foreground <see cref="Color"/> used to render its row
/// in a <see cref="LogViewerControl"/>. Mirrors the severity palette in <see cref="LogEntry.ToMarkup"/>.
/// </summary>
public static class LogSeverityColors
{
	/// <summary>Gets the row foreground color for the given log level.</summary>
	public static Color ForLevel(LogLevel level) => level switch
	{
		LogLevel.Trace => Color.Grey,
		LogLevel.Debug => Color.Grey,
		LogLevel.Information => Color.White,
		LogLevel.Warning => Color.Yellow,
		LogLevel.Error => Color.Red,
		LogLevel.Critical => Color.Red,
		_ => Color.White
	};
}
