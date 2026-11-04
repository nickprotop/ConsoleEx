// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Benchmarks;

/// <summary>
/// Builds synthetic control trees and representative windows for the benchmark suite.
/// All construction here is invoked from [GlobalSetup] so it is never timed.
/// </summary>
internal static class BenchTrees
{
	/// <summary>A single markup label, the leaf used to populate trees.</summary>
	public static MarkupControl Leaf(string text) => new(new List<string> { text });

	/// <summary>
	/// Builds a balanced container tree of the given depth and per-node breadth.
	/// Each non-leaf level is a ScrollablePanelControl holding <paramref name="breadth"/>
	/// children; the deepest level holds markup leaves. Returns the root control.
	/// </summary>
	public static IWindowControl BuildTree(int depth, int breadth)
	{
		if (depth <= 0)
			return Leaf("leaf");

		// ScrollablePanelControl: neutral linear container, parameterless ctor + AddControl.
		// (ColumnContainer has no parameterless ctor, so it can't be used here.)
		var container = new ScrollablePanelControl();
		for (int i = 0; i < breadth; i++)
			container.AddControl(BuildTree(depth - 1, breadth));
		return container;
	}

	/// <summary>
	/// A representative "real" screen: a header label, a stack of rows, and a nested container —
	/// used by the full-frame render benchmark.
	/// </summary>
	public static IReadOnlyList<IWindowControl> RepresentativeContent()
	{
		var controls = new List<IWindowControl>
		{
			Leaf("[bold]Dashboard[/]"),
			Leaf("[dim]status: ok[/]"),
		};

		var grid = new ScrollablePanelControl();
		for (int row = 0; row < 12; row++)
			grid.AddControl(Leaf($"row {row,2}: [green]value-{row}[/] | [blue]link[/]"));
		controls.Add(grid);

		return controls;
	}

	/// <summary>
	/// Markup lines shaped like a real transcript or log: a deterministic mix of plain, whole-line
	/// tagged and per-word tagged text, carrying CJK, emoji and combining marks alongside ASCII.
	/// </summary>
	/// <remarks>
	/// The mix matters to anything that measures painting. Wide glyphs decide what happens at a
	/// clip edge and tag density decides how many style runs a row carries, so a pure-ASCII,
	/// pure-plain document measures the easy case and reports a number no real view will see. The
	/// seed is fixed so every run and every revision sees byte-identical content.
	/// </remarks>
	public static IReadOnlyList<string> MixedMarkupLines(int count)
	{
		string[] palette = ["red", "green", "blue", "yellow", "magenta", "cyan", "grey70"];
		string[] styles = ["", "bold", "italic", "underline", "dim"];
		string[] bodies =
		[
			"The compositor double-buffers the screen and diffs the cells before flushing.",
			"Streaming tokens arrive one by one and the cache keeps the parse count flat.",
			"café naïve été αβγδε",
			"你好世界  \U0001F527",
			"日本語テキスト  →  \U0001F680",
			"A longer plain line that exercises the parser through unmarked text for a while.",
		];

		var rng = new Random(20261104);
		var lines = new List<string>(count);

		for (int i = 0; i < count; i++)
		{
			string body = bodies[rng.Next(bodies.Length)];
			string prefix = $"[dim]{i + 1,5}[/] ";
			int dec = rng.Next(10);

			if (dec < 3)
			{
				// ~30% plain: the parser walks unmarked text.
				lines.Add(prefix + Escape(body));
			}
			else if (dec < 8)
			{
				// ~50% one tag around the whole line: the common case.
				string style = styles[rng.Next(styles.Length)];
				string color = palette[rng.Next(palette.Length)];
				string tag = style.Length == 0 ? color : $"{style} {color}";
				lines.Add($"{prefix}[{tag}]{Escape(body)}[/]");
			}
			else
			{
				// ~20% a tag per word: the heaviest shape, many style runs in one row.
				var sb = new System.Text.StringBuilder(prefix);
				var words = body.Split(' ');
				for (int w = 0; w < words.Length; w++)
				{
					if (w > 0) sb.Append(' ');
					sb.Append('[').Append(palette[rng.Next(palette.Length)]).Append(']')
						.Append(Escape(words[w])).Append("[/]");
				}
				lines.Add(sb.ToString());
			}
		}

		return lines;
	}

	private static string Escape(string s) => s.Replace("[", "[[").Replace("]", "]]");
}
