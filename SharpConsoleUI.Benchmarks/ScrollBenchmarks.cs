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
/// The cost of SCROLLING a long markup document: a chat transcript, a log pane, a file view.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Scrolling is the most common thing a user does to a populated view, and
/// nothing else in this suite measures it. <see cref="MarkupAppendBenchmarks"/> covers content
/// arriving, <see cref="FullFrameRenderBenchmarks"/> covers one frame of a static window, and
/// <see cref="LayoutTreeBenchmarks"/> covers tree shape. None of them move a viewport over content
/// that does not change, which is a different path: the parse cache stays warm throughout and the
/// cost is layout plus paint plus diff, not parsing.
/// </para>
/// <para>
/// <b>Why the content is mixed.</b> The lines carry CJK, emoji and combining marks alongside ASCII,
/// and mix untagged, whole-line-tagged and per-word-tagged markup, because the row writer's work
/// depends on both: wide glyphs decide clipping at the viewport edge, and tag density decides how
/// many style runs a row carries. A pure-ASCII, pure-plain document would measure the easy case and
/// report a number no real transcript will see.
/// </para>
/// <para>
/// <b>What ScrollByOneLine measures.</b> One line of movement and the frame that follows it, which
/// is what a wheel notch or an arrow key costs. <see cref="ScrollPageDown"/> is the same for a page
/// jump, where every visible row is replaced at once rather than shifted by one.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ScrollBenchmarks
{
	private const int Width = 120;
	private const int Height = 40;

	private ConsoleWindowSystem _system = null!;
	private Window _window = null!;
	private ScrollablePanelControl _panel = null!;
	private int _direction = 1;

	/// <summary>Scrollback depth: a short view, a session, a long-running pane.</summary>
	[Params(200, 2000)]
	public int Lines;

	[GlobalSetup]
	public void Setup()
	{
		_system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		_window = new Window(_system) { Width = Width, Height = Height };

		_panel = new ScrollablePanelControl { Height = Height - 4 };
		foreach (var line in BenchTrees.MixedMarkupLines(Lines))
			_panel.AddControl(new MarkupControl(new List<string> { line }));

		_window.AddControl(_panel);

		// Prime: the first frame parses everything. Scrolling must measure a warm cache, or it
		// reports the cost of first paint rather than the cost of moving.
		_window.RenderAndGetVisibleContent();
		for (int i = 0; i < 20; i++)
		{
			_panel.ScrollVerticalBy(1);
			_window.RenderAndGetVisibleContent();
		}
	}

	/// <summary>One line of movement plus the frame that follows: a wheel notch or an arrow key.</summary>
	/// <remarks>
	/// Direction reverses every call so the viewport oscillates near the middle of the document
	/// instead of walking to an end and clamping, where scrolling would become a no-op and the
	/// benchmark would quietly start measuring nothing.
	/// </remarks>
	[Benchmark]
	public int ScrollByOneLine()
	{
		_direction = -_direction;
		_panel.ScrollVerticalBy(_direction);
		return _window.RenderAndGetVisibleContent().Count;
	}

	/// <summary>A page jump: every visible row is replaced at once rather than shifted by one.</summary>
	[Benchmark]
	public int ScrollPageDown()
	{
		_direction = -_direction;
		_panel.ScrollVerticalBy(_direction * (Height - 6));
		return _window.RenderAndGetVisibleContent().Count;
	}

	/// <summary>
	/// The control: the same window re-rendered without moving. The gap between this and
	/// <see cref="ScrollByOneLine"/> is what the scroll itself costs, separate from the standing
	/// cost of producing a frame at all.
	/// </summary>
	[Benchmark(Baseline = true)]
	public int NoScrollReRender() => _window.RenderAndGetVisibleContent().Count;
}
