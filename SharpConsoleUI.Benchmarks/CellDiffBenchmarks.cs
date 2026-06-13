// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using BenchmarkDotNet.Attributes;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;

namespace SharpConsoleUI.Benchmarks;

/// <summary>
/// Measures per-frame diff + ANSI generation by re-rendering a window through the headless
/// harness. NoChange re-renders an unchanged frame (dirty-tracking effectiveness — should be
/// the cheapest); ScatteredChange mutates the label's text each iteration so a small region
/// must be re-emitted.
/// NOTE: This benchmarks only the default (no-diagnostics) render path, which is what ships.
/// A per-DirtyTrackingMode comparison (Line/Cell vs default) is intentionally OMITTED because
/// the only Line/Cell-mode test builders also enable diagnostics + quality analysis, which
/// would dominate the measurement (apples-to-oranges). Revisit if diagnostics-free per-mode
/// builders are added.
/// </summary>
[MemoryDiagnoser]
public class CellDiffBenchmarks
{
	private ConsoleWindowSystem _system = null!;
	private Window _window = null!;
	private MarkupControl _label = null!;
	private int _counter;

	[GlobalSetup]
	public void Setup()
	{
		_system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		_window = new Window(_system) { Width = 80, Height = 25 };
		_label = BenchTrees.Leaf("frame 0 — the quick brown fox");
		_window.AddControl(_label);
		_window.RenderAndGetVisibleContent(); // prime the front buffer
	}

	[Benchmark(Baseline = true)]
	public int NoChange() => _window.RenderAndGetVisibleContent().Count;

	[Benchmark]
	public int ScatteredChange()
	{
		_label.SetContent(new List<string> { $"frame {_counter++} — the quick brown fox" });
		return _window.RenderAndGetVisibleContent().Count;
	}
}
