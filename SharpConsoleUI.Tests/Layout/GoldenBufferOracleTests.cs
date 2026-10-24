// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

// SharpConsoleUI.Tests.Controls shadows the SharpConsoleUI.Controls factory from inside this
// namespace, so the builder entry point needs an explicit alias.
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Proves the golden-buffer oracle itself before it is used to judge the root-layout rewrite.
/// </summary>
/// <remarks>
/// An oracle that cannot fail is worse than no oracle: it reports success regardless of what the
/// rewrite does. These tests sabotage a rendered window in the specific ways a sticky-to-grid
/// mapping is most likely to break it - a band one row off, a clip-rect leak, a colour change -
/// and assert the comparison catches each one.
///
/// Step 1 of docs/experiment/design.md. Written against UNMODIFIED code so the baseline is known
/// good before any layout work begins.
/// </remarks>
public class GoldenBufferOracleTests
{
	private static Window Render(ConsoleWindowSystem system, Window window)
	{
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		return window;
	}

	/// <summary>
	/// The canonical sticky shape: a sticky header, a scrolling body long enough to overflow, and
	/// a sticky footer. This is the layout the Auto/Star/Auto mapping must reproduce exactly.
	/// </summary>
	private static Window StickySandwich(ConsoleWindowSystem system, int w, int h)
	{
		var window = new Window(system) { Title = "Sandwich", Width = w, Height = h, Left = 0, Top = 0 };

		window.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());
		for (int i = 0; i < 40; i++)
			window.AddControl(Builder.Markup($"body line {i}").Build());
		window.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());

		system.AddWindow(window);
		return Render(system, window);
	}

	[Fact]
	public void TheOracle_ReportsNoDifference_ForTwoIdenticalRenders()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = StickySandwich(system, 60, 20);

		var first = GoldenBuffer.Capture(window, "first");
		var second = GoldenBuffer.Capture(window, "second");

		Assert.Equal(string.Empty, GoldenBuffer.Compare(first, second));
	}

	[Fact]
	public void TheOracle_CatchesAScrolledBody()
	{
		// The failure the rewrite is most likely to produce: the body row laid out at the wrong
		// offset while the sticky bands stay put. A text-only comparison would still see all the
		// same words, just in different places.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = StickySandwich(system, 60, 20);

		var before = GoldenBuffer.Capture(window, "unscrolled");

		// ScrollBy, not the ScrollOffset setter: the setter stores the value without invalidating
		// (Window.cs:845 vs ScrollBy at 865), so a re-render of a clean tree paints the old layout
		// and the buffers compare equal. Found by this test - see the note in design.md.
		window.ScrollBy(3);
		Render(system, window);
		var after = GoldenBuffer.Capture(window, "scrolled-by-3");

		var report = GoldenBuffer.Compare(before, after);
		Assert.False(string.IsNullOrEmpty(report),
			"a three-row scroll must register as a buffer difference");
	}

	[Fact]
	public void TheOracle_CatchesAChangedStickyBand()
	{
		// A sticky band that renders different content - the symptom of a band landing in the
		// wrong grid row, or of a clip rect letting body content paint over it.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);

		var a = StickySandwich(system, 60, 20);
		var baseline = GoldenBuffer.Capture(a, "HEADER");

		var system2 = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var b = new Window(system2) { Title = "Sandwich", Width = 60, Height = 20, Left = 0, Top = 0 };
		b.AddControl(Builder.Markup("[bold]HEADEX[/]").StickyTop().Build());   // one glyph differs
		for (int i = 0; i < 40; i++)
			b.AddControl(Builder.Markup($"body line {i}").Build());
		b.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		system2.AddWindow(b);
		Render(system2, b);

		var changed = GoldenBuffer.Capture(b, "HEADEX");

		var report = GoldenBuffer.Compare(baseline, changed);
		Assert.False(string.IsNullOrEmpty(report),
			"a single changed glyph in a sticky band must be caught");
	}

	[Fact]
	public void TheOracle_CatchesAColourOnlyChange()
	{
		// Colour-only drift is the clip-rect failure mode: the right characters in the right
		// places, painted with the wrong background because a band's clip leaked. Comparing
		// rendered text would miss this entirely, which is why the oracle compares Cells.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var a = new Window(system) { Title = "Colour", Width = 40, Height = 10, Left = 0, Top = 0 };
		a.AddControl(Builder.Markup("[red]same text[/]").StickyTop().Build());
		system.AddWindow(a);
		Render(system, a);
		var red = GoldenBuffer.Capture(a, "red");

		var system2 = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var b = new Window(system2) { Title = "Colour", Width = 40, Height = 10, Left = 0, Top = 0 };
		b.AddControl(Builder.Markup("[blue]same text[/]").StickyTop().Build());
		system2.AddWindow(b);
		Render(system2, b);
		var blue = GoldenBuffer.Capture(b, "blue");

		Assert.Equal(red.ToText(), blue.ToText());   // identical as text

		var report = GoldenBuffer.Compare(red, blue);
		Assert.False(string.IsNullOrEmpty(report),
			"identical text in a different colour must still be caught");
	}

	[Fact]
	public void TheOracle_RefusesToCaptureAnUnpaintedWindow()
	{
		// Two unpainted windows would compare equal, so an unpainted capture is a silent false
		// pass. It must throw instead.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Never painted", Width = 40, Height = 10 };

		Assert.Throws<InvalidOperationException>(() => GoldenBuffer.Capture(window, "unpainted"));
	}

	[Theory]
	[InlineData(60, 20)]   // comfortable
	[InlineData(20, 6)]    // narrow and short - allotted extent differs from painted viewport
	[InlineData(200, 50)]  // wide
	[InlineData(12, 4)]    // degenerate: bands alone nearly fill the window
	public void TheStickySandwich_CapturesAtBoundarySizes(int w, int h)
	{
		// Boundary sizes are where layout bugs hide (CLAUDE.md, and the SPC-in-grid scroll-cap
		// bug). The oracle must produce a usable capture at each, not just the comfortable one.
		var system = TestWindowSystemBuilder.CreateTestSystem(240, 60);
		var window = StickySandwich(system, w, h);

		var shot = GoldenBuffer.Capture(window, $"sandwich-{w}x{h}");

		Assert.True(shot.Width > 0 && shot.Height > 0, "capture must have real dimensions");
		Assert.Equal(string.Empty, GoldenBuffer.Compare(shot, GoldenBuffer.Capture(window, "again")));
	}
}
