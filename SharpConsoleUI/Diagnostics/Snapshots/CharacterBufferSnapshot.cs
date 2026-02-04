// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using System.Text;

namespace SharpConsoleUI.Diagnostics.Snapshots;

/// <summary>
/// Captures the state of a CharacterBuffer at a specific point in time.
/// Used for validation, debugging, and regression testing.
/// </summary>
public class CharacterBufferSnapshot
{
	/// <summary>
	/// Gets the width of the captured buffer.
	/// </summary>
	public int Width { get; init; }

	/// <summary>
	/// Gets the height of the captured buffer.
	/// </summary>
	public int Height { get; init; }

	/// <summary>
	/// Gets the deep copy of the cell array.
	/// </summary>
	public Cell[,] Cells { get; init; } = null!;

	/// <summary>
	/// Gets the list of dirty cells at the time of capture.
	/// </summary>
	public List<CellChange> DirtyCells { get; init; } = new();

	/// <summary>
	/// Gets the dirty region bounds.
	/// </summary>
	public LayoutRect DirtyRegion { get; init; }

	/// <summary>
	/// Gets the timestamp when this snapshot was taken.
	/// </summary>
	public DateTime Timestamp { get; init; }

	/// <summary>
	/// Gets the frame number this snapshot represents.
	/// </summary>
	public int FrameNumber { get; init; }

	/// <summary>
	/// Gets the cell at the specified position.
	/// </summary>
	public Cell GetCell(int x, int y)
	{
		if (x < 0 || x >= Width || y < 0 || y >= Height)
			throw new ArgumentOutOfRangeException($"Position ({x}, {y}) is out of bounds for buffer {Width}x{Height}");

		return Cells[x, y];
	}

	/// <summary>
	/// Validates that all cells in the specified region match the given predicate.
	/// </summary>
	public bool ValidateRegion(LayoutRect region, Func<Cell, bool> predicate)
	{
		for (int y = region.Y; y < region.Bottom && y < Height; y++)
		{
			for (int x = region.X; x < region.Right && x < Width; x++)
			{
				if (!predicate(Cells[x, y]))
					return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Counts the number of cells matching the given predicate.
	/// </summary>
	public int CountCellsMatching(Func<Cell, bool> predicate)
	{
		int count = 0;
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (predicate(Cells[x, y]))
					count++;
			}
		}
		return count;
	}

	/// <summary>
	/// Returns a debug string representation of the buffer.
	/// </summary>
	public string ToDebugString()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"CharacterBuffer Snapshot - Frame {FrameNumber} ({Width}x{Height})");
		sb.AppendLine($"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
		sb.AppendLine($"Dirty Region: {DirtyRegion}");
		sb.AppendLine($"Dirty Cells: {DirtyCells.Count}");
		return sb.ToString();
	}

	/// <summary>
	/// Converts the buffer to ASCII art for visualization.
	/// </summary>
	public string ToAsciiArt()
	{
		var sb = new StringBuilder();
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				sb.Append(Cells[x, y].Character);
			}
			sb.AppendLine();
		}
		return sb.ToString();
	}

	/// <summary>
	/// Compares this snapshot with another and returns the differences.
	/// </summary>
	public List<CellChange> GetDifferences(CharacterBufferSnapshot other)
	{
		if (other.Width != Width || other.Height != Height)
			throw new ArgumentException("Snapshots must have the same dimensions");

		var differences = new List<CellChange>();
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				var thisCell = Cells[x, y];
				var otherCell = other.Cells[x, y];

				if (!thisCell.VisuallyEquals(otherCell))
				{
					differences.Add(new CellChange(x, y, thisCell));
				}
			}
		}
		return differences;
	}
}
