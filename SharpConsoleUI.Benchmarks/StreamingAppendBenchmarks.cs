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
/// A model reply arriving token by token into the LAST line of a transcript.
/// </summary>
/// <remarks>
/// <para>
/// <b>How this differs from <see cref="MarkupAppendBenchmarks"/>.</b> That one adds a whole new
/// line, which is what a log pane does. A streaming reply instead rewrites the line it is already
/// on, once per token, and only commits a new line at a paragraph break. The buffer therefore
/// changes many times per visible line rather than once, and every one of those changes bumps the
/// content version the parse cache is keyed on.
/// </para>
/// <para>
/// <b>Why it is worth its own measurement.</b> This is the shape of every agent transcript, and the
/// per-token cost is what decides whether a long session stays smooth. The number to watch is not
/// the absolute time but how it moves with <see cref="Scrollback"/>: a cost that grows with the
/// history above the cursor means a session gets slower the longer it runs, which a user
/// experiences as the UI degrading as they work.
/// </para>
/// <para>
/// <c>InvocationCount=1</c> because each operation mutates the control it measures; without a reset
/// to a known state in <see cref="IterationSetup"/> the line would grow without bound across
/// invocations and the numbers would describe nothing.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
public class StreamingAppendBenchmarks
{
	private const int TokensPerBurst = 10;

	private ConsoleWindowSystem _system = null!;
	private Window _window = null!;
	private MarkupControl _markup = null!;
	private List<string> _seed = null!;

	/// <summary>Transcript history above the streaming line: a new session, then a long one.</summary>
	[Params(200, 2000)]
	public int Scrollback;

	[GlobalSetup]
	public void Setup()
	{
		_system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		_window = new Window(_system) { Width = 120, Height = 40 };
		_markup = new MarkupControl(new List<string>());
		_window.AddControl(_markup);

		_seed = new List<string>(BenchTrees.MixedMarkupLines(Scrollback));
	}

	[IterationSetup]
	public void IterationSetup()
	{
		// Reset to exactly Scrollback lines plus one empty line to stream into, and prime the cache
		// so the measured work is the streaming, never the initial parse.
		var content = new List<string>(_seed) { "[bold]Assistant[/] " };
		_markup.SetContent(content);
		_window.RenderAndGetVisibleContent();
	}

	/// <summary>An unchanged frame. The control: what a frame costs when nothing arrived.</summary>
	[Benchmark(Baseline = true)]
	public int NoOpRepaint() => _window.RenderAndGetVisibleContent().Count;

	/// <summary>
	/// Ten tokens arriving one at a time, each rewriting the last line and repainting, which is what
	/// a reply actually does to the screen.
	/// </summary>
	[Benchmark]
	public int StreamTokenBurst()
	{
		int frames = 0;
		var line = new System.Text.StringBuilder("[bold]Assistant[/] ");

		for (int t = 0; t < TokensPerBurst; t++)
		{
			line.Append("token ");
			var content = new List<string>(_seed) { line.ToString() };
			_markup.SetContent(content);
			frames += _window.RenderAndGetVisibleContent().Count;
		}

		return frames;
	}

	/// <summary>
	/// The machine-independent form: how many logical lines are re-parsed to put ten tokens on
	/// screen. A count, not a duration, so it does not move with machine, build or noise.
	/// </summary>
	[Benchmark]
	public long ParsesPerTokenBurst()
	{
		long before = MarkupControl.TotalParseCount;
		var line = new System.Text.StringBuilder("[bold]Assistant[/] ");

		for (int t = 0; t < TokensPerBurst; t++)
		{
			line.Append("token ");
			var content = new List<string>(_seed) { line.ToString() };
			_markup.SetContent(content);
			_window.RenderAndGetVisibleContent();
		}

		return MarkupControl.TotalParseCount - before;
	}
}
