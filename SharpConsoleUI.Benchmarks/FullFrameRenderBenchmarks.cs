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
/// End-to-end frame production for a representative populated window via the headless harness.
/// NoOpReRender re-renders an unchanged frame (dirty-tracking effectiveness — should be cheap);
/// FullRebuild forces a full dirty render each iteration (measure + arrange + paint + diff).
/// </summary>
[MemoryDiagnoser]
public class FullFrameRenderBenchmarks
{
	private ConsoleWindowSystem _system = null!;
	private Window _window = null!;

	[GlobalSetup]
	public void Setup()
	{
		_system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		_window = new Window(_system) { Width = 120, Height = 40 };
		foreach (var c in BenchTrees.RepresentativeContent())
			_window.AddControl(c);
		_window.RenderAndGetVisibleContent(); // prime
	}

	[Benchmark]
	public int NoOpReRender() => _window.RenderAndGetVisibleContent().Count;

	[Benchmark]
	public int FullRebuild()
	{
		_window.Invalidate(Invalidation.Relayout); // force a full dirty render
		return _window.RenderAndGetVisibleContent().Count;
	}
}
