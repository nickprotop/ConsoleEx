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
}
