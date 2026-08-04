// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// What one arriving line costs a <see cref="MarkupControl"/> that already holds a scrollback.
/// <para>
/// <b>The defect.</b> The parse cache was keyed on a content version that every mutator bumps, so
/// appending one line invalidated the parse of the whole buffer. At 20,000 lines that measured
/// 40,002 line-parses and ~269 MB of allocation because one line arrived — 40,002 rather than 20,001
/// because the scroll-panel overflow probe measures at two widths (viewport and
/// viewport-minus-scrollbar) and the natural-width probe asks for a third, so a bumped version
/// missed at every one of them.
/// </para>
/// <para>
/// These assert the parse COUNT rather than a duration deliberately: it is exact, and it does not
/// move with machine, build or background load, so it cannot go quietly green on a fast box.
/// </para>
/// </summary>
[Collection("ParseCounter")]
public class MarkupControlAppendCostTests
{
	private static (ConsoleWindowSystem system, Window window, MarkupControl markup) Host(int lines)
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = 120, Height = 40 };
		var markup = new MarkupControl(new List<string>());
		window.AddControl(markup);
		system.AddWindow(window);

		var seed = new List<string>(lines);
		for (int i = 0; i < lines; i++)
			seed.Add($"[grey]12:34:56[/] [bold]user{i % 32}[/] said something ordinary here");
		markup.SetContent(seed);
		window.RenderAndGetVisibleContent(); // prime

		return (system, window, markup);
	}

	private static long ParsesFor(Window window, System.Action mutate)
	{
		long before = MarkupControl.TotalParseCount;
		mutate();
		window.RenderAndGetVisibleContent();
		return MarkupControl.TotalParseCount - before;
	}

	[Theory]
	[InlineData(200)]
	[InlineData(2000)]
	public void AppendingOneLine_DoesNotReparseTheBuffer(int lines)
	{
		var (system, window, markup) = Host(lines);

		long parses = ParsesFor(window, () => markup.AppendLine("[bold]newcomer[/] just arrived"));

		// One new group, parsed once per width the frame asks for. The bound is deliberately generous
		// about the number of widths and deliberately independent of `lines` — that independence is
		// the whole property under test.
		Assert.True(parses <= 8, $"appending one line to {lines} parsed {parses} lines; it must not scale with the buffer");

		System.GC.KeepAlive(system);
	}

	[Fact]
	public void AppendCost_IsIndependentOfScrollbackSize()
	{
		// The sharpest statement of the fix: the same append against a buffer 10x larger costs the
		// same. Under the old whole-content key the larger buffer cost 10x more.
		var (systemSmall, windowSmall, markupSmall) = Host(200);
		var (systemLarge, windowLarge, markupLarge) = Host(2000);

		long small = ParsesFor(windowSmall, () => markupSmall.AppendLine("[bold]newcomer[/] just arrived"));
		long large = ParsesFor(windowLarge, () => markupLarge.AppendLine("[bold]newcomer[/] just arrived"));

		Assert.Equal(small, large);

		System.GC.KeepAlive(systemSmall);
		System.GC.KeepAlive(systemLarge);
	}

	[Fact]
	public void UnchangedFrame_ParsesNothing()
	{
		// The pre-existing guarantee, kept: a repaint with no mutation is a pure cache hit.
		var (system, window, _) = Host(2000);

		long parses = ParsesFor(window, () => { });

		Assert.Equal(0, parses);
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void AppendedContent_IsActuallyRendered()
	{
		// A cache that returns stale rows would satisfy every count above, so pin the output too.
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 100, Height = 10 };
		var markup = new MarkupControl(new List<string> { "first line" });
		window.AddControl(markup);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		markup.AppendLine("second line");
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
		Assert.True(Contains(snap, "first line"), "the existing line disappeared");
		Assert.True(Contains(snap, "second line"), "the appended line was not rendered");

		System.GC.KeepAlive(system);
	}

	[Fact]
	public void EditingALine_ReparsesOnlyWhatChanged()
	{
		var (system, window, markup) = Host(2000);

		long parses = ParsesFor(window, () =>
		{
			var lines = new List<string>(markup.GetContentLinesForTest()) { };
			lines[500] = "[bold]this line was edited[/]";
			markup.SetContent(lines);
		});

		Assert.True(parses <= 8, $"editing one line of 2000 parsed {parses}");
		System.GC.KeepAlive(system);
	}

	[Fact]
	public void DynamicMarkup_StillReparsesEveryFrame()
	{
		// A spinner must not be served from the cache — it renders differently each frame. Static
		// lines beside it should still be cached, so the count stays small rather than the whole
		// buffer re-parsing.
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = 120, Height = 40 };
		var markup = new MarkupControl(new List<string>());
		window.AddControl(markup);
		system.AddWindow(window);

		var seed = new List<string>();
		for (int i = 0; i < 200; i++) seed.Add($"static line {i}");
		seed.Add("[spinner]dots[/] working");
		markup.SetContent(seed);
		window.RenderAndGetVisibleContent();

		long parses = ParsesFor(window, () => { });

		Assert.True(parses > 0, "dynamic content must re-parse rather than serve a frozen frame");
		System.GC.KeepAlive(system);
	}

	private static bool Contains(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap, string needle)
	{
		for (int y = 0; y < snap.Height; y++)
		{
			var sb = new System.Text.StringBuilder();
			for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
			if (sb.ToString().Contains(needle)) return true;
		}
		return false;
	}
}
