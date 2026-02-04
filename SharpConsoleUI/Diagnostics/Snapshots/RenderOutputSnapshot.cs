// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SharpConsoleUI.Diagnostics.Snapshots;

/// <summary>
/// Captures the final console output string sent to Console.Write().
/// Used to validate actual console output and measure bytes written.
/// </summary>
public class RenderOutputSnapshot
{
	/// <summary>
	/// Gets the full console output string.
	/// </summary>
	public string FullOutput { get; init; } = string.Empty;

	/// <summary>
	/// Gets the total number of bytes written.
	/// </summary>
	public int BytesWritten { get; init; }

	/// <summary>
	/// Gets the number of cursor movement commands.
	/// </summary>
	public int CursorMoves { get; init; }

	/// <summary>
	/// Gets the timestamp when this snapshot was taken.
	/// </summary>
	public DateTime Timestamp { get; init; }

	/// <summary>
	/// Gets the frame number this snapshot represents.
	/// </summary>
	public int FrameNumber { get; init; }

	/// <summary>
	/// Extracts all ANSI escape sequences from the output.
	/// </summary>
	public List<string> GetAnsiSequences()
	{
		var regex = new Regex(@"\x1b\[[0-9;]*[a-zA-Z]");
		var matches = regex.Matches(FullOutput);
		return matches.Cast<Match>().Select(m => m.Value).ToList();
	}

	/// <summary>
	/// Extracts all cursor positioning commands (ESC[y;xH).
	/// </summary>
	public List<(int y, int x)> GetCursorPositions()
	{
		var regex = new Regex(@"\x1b\[(\d+);(\d+)H");
		var matches = regex.Matches(FullOutput);

		return matches.Cast<Match>()
			.Select(m => (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)))
			.ToList();
	}

	/// <summary>
	/// Counts occurrences of a specific ANSI pattern.
	/// </summary>
	public int CountPattern(string pattern)
	{
		var regex = new Regex(pattern);
		return regex.Matches(FullOutput).Count;
	}

	/// <summary>
	/// Gets the stripped output (ANSI codes removed).
	/// </summary>
	public string GetStrippedOutput()
	{
		var regex = new Regex(@"\x1b\[[0-9;]*[a-zA-Z]");
		return regex.Replace(FullOutput, string.Empty);
	}
}
