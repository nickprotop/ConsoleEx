// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using BenchmarkDotNet.Attributes;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Benchmarks;

/// <summary>
/// Characterizes the layout engine: build a balanced control tree once, then measure the cost
/// of Measure / Arrange / Paint as a function of tree depth × breadth. The tree is built in
/// [GlobalSetup] so construction is never timed. CreateSubtree IS inside the measured region
/// because the real renderer rebuilds layout subtrees per frame — this reflects real per-frame
/// cost. Provides the before/after baseline for the roadmapped ScrollLayout refactor.
/// </summary>
[MemoryDiagnoser]
public class LayoutTreeBenchmarks
{
	// NOTE: the tree is built from NESTED ScrollablePanelControls (see BenchTrees.BuildTree) — each
	// non-leaf level is an SPC holding Breadth children. Since the ScrollLayout refactor, SPCs are
	// REAL layout-tree participants (no longer leaf-stopped self-painters), so every level now runs
	// the full nested-scroll-panel Measure/Arrange/Paint path: each SPC measures its children, which
	// are themselves SPCs that measure THEIR children, recursively. Combined with the exponential node
	// count (leaves = Breadth^Depth), deep nesting makes this a pathological synthetic case (worst
	// case at Depth=6 ran for MINUTES). Depth is therefore capped at 4 and Breadth at 3 (worst case
	// D4/B3 ≈ 81 leaves) to keep the benchmark tractable for a routine baseline/CI sweep — real UIs do
	// not nest scroll panels this deeply. The (2/3/4)×(2/3) grid still demonstrates the depth-scaling
	// curve clearly. Raise the caps for a one-off deep-dive, but keep them modest for the committed
	// baseline. (Per CLAUDE.md "no silent caps".)
	[Params(2, 3, 4)]
	public int Depth;

	[Params(2, 3)]
	public int Breadth;

	private const int Width = 120;
	private const int Height = 40;

	private IWindowControl _root = null!;
	private CharacterBuffer _buffer = null!;

	[GlobalSetup]
	public void Setup()
	{
		_root = BenchTrees.BuildTree(Depth, Breadth);
		_buffer = new CharacterBuffer(Width, Height);
	}

	[Benchmark]
	public LayoutSize Measure()
	{
		var node = LayoutNodeFactory.CreateSubtree(_root);
		return node.Measure(LayoutConstraints.Fixed(Width, Height));
	}

	[Benchmark]
	public void MeasureArrange()
	{
		var node = LayoutNodeFactory.CreateSubtree(_root);
		node.Measure(LayoutConstraints.Fixed(Width, Height));
		node.Arrange(new LayoutRect(0, 0, Width, Height));
	}

	[Benchmark]
	public void MeasureArrangePaint()
	{
		var node = LayoutNodeFactory.CreateSubtree(_root);
		node.Measure(LayoutConstraints.Fixed(Width, Height));
		node.Arrange(new LayoutRect(0, 0, Width, Height));
		node.Paint(_buffer, new LayoutRect(0, 0, Width, Height));
	}
}
