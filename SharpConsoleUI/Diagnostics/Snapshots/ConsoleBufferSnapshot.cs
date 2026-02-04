// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics.Snapshots;

/// <summary>
/// Captures the state of the ConsoleBuffer's front and back buffers.
/// Used to validate double-buffering and dirty cell detection.
/// </summary>
public class ConsoleBufferSnapshot
{
	/// <summary>
	/// Gets the front buffer (currently displayed).
	/// </summary>
	public ConsoleCell[,] FrontBuffer { get; init; } = null!;

	/// <summary>
	/// Gets the back buffer (to be rendered).
	/// </summary>
	public ConsoleCell[,] BackBuffer { get; init; } = null!;

	/// <summary>
	/// Gets the list of dirty cell positions.
	/// </summary>
	public List<(int x, int y)> DirtyCells { get; init; } = new();

	/// <summary>
	/// Gets the width of the buffer.
	/// </summary>
	public int Width { get; init; }

	/// <summary>
	/// Gets the height of the buffer.
	/// </summary>
	public int Height { get; init; }

	/// <summary>
	/// Gets the timestamp when this snapshot was taken.
	/// </summary>
	public DateTime Timestamp { get; init; }

	/// <summary>
	/// Gets the frame number this snapshot represents.
	/// </summary>
	public int FrameNumber { get; init; }

	/// <summary>
	/// Gets the number of dirty cells.
	/// </summary>
	public int GetDirtyCount() => DirtyCells.Count;

	/// <summary>
	/// Checks if a cell at the specified position is dirty.
	/// </summary>
	public bool IsCellDirty(int x, int y) => DirtyCells.Contains((x, y));

	/// <summary>
	/// Gets the front buffer cell at the specified position.
	/// </summary>
	public ConsoleCell GetFront(int x, int y)
	{
		if (x < 0 || x >= Width || y < 0 || y >= Height)
			throw new ArgumentOutOfRangeException($"Position ({x}, {y}) is out of bounds for buffer {Width}x{Height}");

		return FrontBuffer[x, y];
	}

	/// <summary>
	/// Gets the back buffer cell at the specified position.
	/// </summary>
	public ConsoleCell GetBack(int x, int y)
	{
		if (x < 0 || x >= Width || y < 0 || y >= Height)
			throw new ArgumentOutOfRangeException($"Position ({x}, {y}) is out of bounds for buffer {Width}x{Height}");

		return BackBuffer[x, y];
	}

	/// <summary>
	/// Compares front and back buffers and returns cells that differ.
	/// </summary>
	public List<(int x, int y, ConsoleCell front, ConsoleCell back)> GetDifferences()
	{
		var differences = new List<(int, int, ConsoleCell, ConsoleCell)>();

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				var front = FrontBuffer[x, y];
				var back = BackBuffer[x, y];

				if (!front.Equals(back))
				{
					differences.Add((x, y, front, back));
				}
			}
		}

		return differences;
	}
}

/// <summary>
/// Represents a cell in the console buffer (character + ANSI escape sequence).
/// </summary>
public struct ConsoleCell : IEquatable<ConsoleCell>
{
	/// <summary>
	/// Gets or sets the character.
	/// </summary>
	public char Character { get; set; }

	/// <summary>
	/// Gets or sets the ANSI escape sequence for this cell.
	/// </summary>
	public string AnsiEscape { get; set; }

	/// <summary>
	/// Creates a new console cell.
	/// </summary>
	public ConsoleCell(char character, string ansiEscape)
	{
		Character = character;
		AnsiEscape = ansiEscape ?? string.Empty;
	}

	/// <summary>Determines equality.</summary>
	public bool Equals(ConsoleCell other) =>
		Character == other.Character && AnsiEscape == other.AnsiEscape;

	/// <summary>Determines equality with object.</summary>
	public override bool Equals(object? obj) => obj is ConsoleCell other && Equals(other);

	/// <summary>Gets hash code.</summary>
	public override int GetHashCode() => HashCode.Combine(Character, AnsiEscape);

	/// <summary>Equality operator.</summary>
	public static bool operator ==(ConsoleCell left, ConsoleCell right) => left.Equals(right);

	/// <summary>Inequality operator.</summary>
	public static bool operator !=(ConsoleCell left, ConsoleCell right) => !left.Equals(right);

	/// <summary>String representation.</summary>
	public override string ToString() =>
		$"ConsoleCell('{(Character == ' ' ? "SP" : Character)}', ANSI: {(string.IsNullOrEmpty(AnsiEscape) ? "none" : AnsiEscape)})";
}
