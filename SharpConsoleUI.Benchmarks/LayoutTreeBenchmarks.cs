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
	// NOTE: tree node count is exponential (leaves = Breadth^Depth) AND the layout engine scales
	// super-linearly with node count (measured: ~27x more nodes from (D5,B3)→(D8,B3) costs ~216x
	// more time — the per-frame CreateSubtree rebuild + redundant traversals the ScrollLayout
	// refactor targets). Depth=8 cases run multiple SECONDS and allocate GBs, too heavy for a
	// routine baseline/CI sweep, so Depth is capped at 6 and Breadth at 3 (worst case D6/B3 ≈ 729
	// leaves). The (2/4/6)×(2/3) grid still demonstrates the super-linear curve clearly. Raise the
	// caps for a one-off deep-dive into layout scaling, but keep them modest for the committed
	// baseline. (Per CLAUDE.md "no silent caps".)
	[Params(2, 4, 6)]
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
