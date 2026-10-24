// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// An exact capture of a window's painted character buffer, and the comparison used as the
/// oracle for the pluggable-root-layout experiment.
/// </summary>
/// <remarks>
/// The experiment reimplements <c>StickyPosition</c> as Auto/Star/Auto grid rows. The claim that
/// existing applications cannot tell the difference is only worth as much as the evidence behind
/// it, so the evidence is the painted cells themselves: same window, same size, old root layout
/// versus new, compared cell by cell including colour and decoration.
///
/// Comparing rendered text alone would hide exactly the failures that matter here - a sticky band
/// painted one row off still reads correctly as text, and a clip-rect regression shows up as a
/// colour bleed rather than a character change. <see cref="Cell"/> is <c>IEquatable</c>, so the
/// strict comparison costs nothing.
/// </remarks>
public sealed class GoldenBuffer
{
	private readonly Cell[,] _cells;

	private GoldenBuffer(string label, int width, int height, Cell[,] cells)
	{
		Label = label;
		Width = width;
		Height = height;
		_cells = cells;
	}

	/// <summary>A human-readable name for the captured scenario, used in failure messages.</summary>
	public string Label { get; }

	public int Width { get; }

	public int Height { get; }

	/// <summary>
	/// Captures the window's current painted buffer. The window must already have been rendered;
	/// a window that has never painted has no buffer and throws rather than silently capturing
	/// an empty grid that would compare equal to another empty grid.
	/// </summary>
	public static GoldenBuffer Capture(Window window, string label)
	{
		var buffer = window.Renderer?.Buffer
			?? throw new InvalidOperationException(
				$"Window '{label}' has no painted buffer. Render it before capturing, otherwise " +
				"two unpainted captures compare equal and the comparison proves nothing.");

		var cells = new Cell[buffer.Height, buffer.Width];
		for (int y = 0; y < buffer.Height; y++)
			for (int x = 0; x < buffer.Width; x++)
				cells[y, x] = buffer.GetCell(x, y);

		return new GoldenBuffer(label, buffer.Width, buffer.Height, cells);
	}

	public Cell this[int x, int y] => _cells[y, x];

	/// <summary>
	/// Compares two captures cell by cell. Returns an empty string when they are identical;
	/// otherwise a report naming every differing cell (capped), with the two renderings side by
	/// side so the shape of the drift is visible rather than just its coordinates.
	/// </summary>
	public static string Compare(GoldenBuffer expected, GoldenBuffer actual, int maxReported = 20)
	{
		if (expected.Width != actual.Width || expected.Height != actual.Height)
		{
			return $"Buffer size differs: {expected.Label} is {expected.Width}x{expected.Height}, " +
				$"{actual.Label} is {actual.Width}x{actual.Height}.";
		}

		var diffs = new List<string>();
		int total = 0;

		for (int y = 0; y < expected.Height; y++)
		{
			for (int x = 0; x < expected.Width; x++)
			{
				var e = expected[x, y];
				var a = actual[x, y];
				if (e.Equals(a))
					continue;

				total++;
				if (diffs.Count < maxReported)
					diffs.Add($"  ({x,3},{y,3})  expected {Describe(e)}  actual {Describe(a)}");
			}
		}

		if (total == 0)
			return string.Empty;

		var sb = new StringBuilder();
		sb.AppendLine($"{total} cell(s) differ between '{expected.Label}' and '{actual.Label}':");
		foreach (var d in diffs)
			sb.AppendLine(d);
		if (total > diffs.Count)
			sb.AppendLine($"  ... and {total - diffs.Count} more");

		sb.AppendLine();
		sb.AppendLine($"--- {expected.Label} ---");
		sb.AppendLine(expected.ToText());
		sb.AppendLine($"--- {actual.Label} ---");
		sb.AppendLine(actual.ToText());

		return sb.ToString();
	}

	/// <summary>
	/// The buffer's characters as plain text, one line per row. For failure messages and for
	/// eyeballing a scenario while writing it - never as the comparison itself, which must stay
	/// cell-exact.
	/// </summary>
	public string ToText()
	{
		var sb = new StringBuilder();
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				var c = _cells[y, x];
				if (c.IsWideContinuation)
					continue;
				var r = c.Character;
				sb.Append(r.Value == 0 ? ' ' : r.ToString());
			}
			sb.AppendLine();
		}
		return sb.ToString();
	}

	private static string Describe(Cell c)
	{
		string ch = c.Character.Value == 0 ? "\\0" : c.Character.ToString();
		string cont = c.IsWideContinuation ? "+cont" : "";
		string dec = c.Decorations == default ? "" : $" {c.Decorations}";
		return $"'{ch}' {c.Foreground}/{c.Background}{dec}{cont}";
	}
}
