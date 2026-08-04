// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;

namespace SharpConsoleUI.Benchmarks;

/// <summary>
/// The cost of ONE line arriving in a <see cref="MarkupControl"/> that already holds a scrollback of
/// N lines — the shape every append-heavy view has (chat transcript, log pane, terminal output).
/// <para>
/// <b>What this measures and why.</b> <c>MarkupControl</c>'s parse cache is keyed on a content
/// version that every mutator bumps, so appending one line invalidates the parse of the whole
/// buffer. <c>NoOpRepaint</c> is the control: an unchanged frame is a pure cache hit and should be
/// flat in N. <c>AppendOneLine</c> is the same repaint with a single line added first. The gap
/// between them is the re-parse, and it is what scales with N rather than with what arrived.
/// </para>
/// <para>
/// <c>ParsesPerAppend</c> reports the same thing as a count rather than a duration, via the public
/// <see cref="MarkupControl.TotalParseCount"/> diagnostic. It is the honest number: it does not move
/// with machine, build or noise, and it shows the multiplier is 2N — the scroll-panel overflow probe
/// measures children at two widths per layout pass, so a bumped version misses at both before the
/// paint hits.
/// </para>
/// <para>
/// InvocationCount=1 because the benchmark mutates the control it measures: each operation must run
/// against a buffer reset to exactly N lines by <see cref="IterationSetup"/>, or N would drift
/// upward across invocations and the numbers would describe nothing.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
public class MarkupAppendBenchmarks
{
	[Params(1000, 5000, 20000)]
	public int Lines;

	private ConsoleWindowSystem _system = null!;
	private Window _window = null!;
	private MarkupControl _markup = null!;
	private List<string> _seed = null!;

	[GlobalSetup]
	public void Setup()
	{
		_system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		_window = new Window(_system) { Width = 120, Height = 40 };
		_markup = new MarkupControl(new List<string>());
		_window.AddControl(_markup);

		// Representative scrollback: a little markup per line, not a bare word and not a stress case.
		_seed = new List<string>(Lines);
		for (int i = 0; i < Lines; i++)
			_seed.Add($"[grey]12:34:56[/] [bold]user{i % 32}[/] said something ordinary here");
	}

	[IterationSetup]
	public void IterationSetup()
	{
		// Reset to exactly N lines and prime the cache, so the measured operation is the append and
		// the repaint that follows it — never the initial parse.
		_markup.SetContent(new List<string>(_seed));
		_window.RenderAndGetVisibleContent();
	}

	/// <summary>An unchanged frame: pure cache hit. The control for the comparison.</summary>
	[Benchmark(Baseline = true)]
	public int NoOpRepaint() => _window.RenderAndGetVisibleContent().Count;

	/// <summary>One line arrives, then the frame is produced.</summary>
	[Benchmark]
	public int AppendOneLine()
	{
		_markup.AppendLine("[grey]12:34:57[/] [bold]newcomer[/] just arrived");
		return _window.RenderAndGetVisibleContent().Count;
	}

	/// <summary>
	/// The machine-independent statement of the same defect: how many logical lines get re-parsed
	/// because one was appended. Returns a count, not a duration.
	/// </summary>
	[Benchmark]
	public long ParsesPerAppend()
	{
		long before = MarkupControl.TotalParseCount;
		_markup.AppendLine("[grey]12:34:57[/] [bold]newcomer[/] just arrived");
		_window.RenderAndGetVisibleContent();
		return MarkupControl.TotalParseCount - before;
	}
}
