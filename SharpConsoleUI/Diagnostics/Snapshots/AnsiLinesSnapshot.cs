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
/// Captures the ANSI-encoded output lines from CharacterBuffer.ToLines().
/// Used to validate ANSI generation and optimization.
/// </summary>
public class AnsiLinesSnapshot
{
	/// <summary>
	/// Gets the list of ANSI-encoded lines.
	/// </summary>
	public List<string> Lines { get; init; } = new();

	/// <summary>
	/// Gets the total number of ANSI escape sequences across all lines.
	/// </summary>
	public int TotalAnsiEscapes { get; init; }

	/// <summary>
	/// Gets the total number of visible characters (excluding ANSI codes).
	/// </summary>
	public int TotalCharacters { get; init; }

	/// <summary>
	/// Gets the timestamp when this snapshot was taken.
	/// </summary>
	public DateTime Timestamp { get; init; }

	/// <summary>
	/// Gets the frame number this snapshot represents.
	/// </summary>
	public int FrameNumber { get; init; }

	/// <summary>
	/// Gets the line at the specified index.
	/// </summary>
	public string GetLine(int y)
	{
		if (y < 0 || y >= Lines.Count)
			throw new ArgumentOutOfRangeException(nameof(y), $"Line {y} is out of range (0-{Lines.Count - 1})");

		return Lines[y];
	}

	/// <summary>
	/// Parses a line into segments of text and ANSI codes.
	/// </summary>
	public List<AnsiSegment> ParseLine(int y)
	{
		var line = GetLine(y);
		var segments = new List<AnsiSegment>();

		var regex = new Regex(@"\x1b\[[0-9;]*[a-zA-Z]");
		var matches = regex.Matches(line);

		int lastIndex = 0;
		foreach (Match match in matches)
		{
			// Add text before this ANSI code
			if (match.Index > lastIndex)
			{
				segments.Add(new AnsiSegment(
					line.Substring(lastIndex, match.Index - lastIndex),
					AnsiSegmentType.Text));
			}

			// Add the ANSI code
			segments.Add(new AnsiSegment(match.Value, AnsiSegmentType.AnsiCode));
			lastIndex = match.Index + match.Length;
		}

		// Add remaining text
		if (lastIndex < line.Length)
		{
			segments.Add(new AnsiSegment(
				line.Substring(lastIndex),
				AnsiSegmentType.Text));
		}

		return segments;
	}

	/// <summary>
	/// Counts ANSI escape sequences matching a specific pattern.
	/// </summary>
	public int CountEscapeSequences(string pattern)
	{
		var regex = new Regex(pattern);
		return Lines.Sum(line => regex.Matches(line).Count);
	}

	/// <summary>
	/// Returns all ANSI escape sequences in the output.
	/// </summary>
	public List<string> GetAllEscapeSequences()
	{
		var sequences = new List<string>();
		var regex = new Regex(@"\x1b\[[0-9;]*[a-zA-Z]");

		foreach (var line in Lines)
		{
			var matches = regex.Matches(line);
			sequences.AddRange(matches.Cast<Match>().Select(m => m.Value));
		}

		return sequences;
	}
}

/// <summary>
/// Represents a segment of a line (either text or ANSI code).
/// </summary>
public record AnsiSegment(string Content, AnsiSegmentType Type);

/// <summary>
/// Type of segment in a parsed ANSI line.
/// </summary>
public enum AnsiSegmentType
{
	/// <summary>Visible text characters.</summary>
	Text,

	/// <summary>ANSI escape sequence.</summary>
	AnsiCode
}
