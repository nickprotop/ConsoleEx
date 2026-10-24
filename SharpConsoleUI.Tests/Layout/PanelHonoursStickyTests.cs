// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Step 5: a <see cref="ScrollablePanelControl"/> honours <c>StickyPosition</c> on its children.
/// </summary>
/// <remarks>
/// <para>
/// Before this, sticky worked at the window root and nowhere else, and a sticky child inside a
/// panel was silently laid out as ordinary scrolling content. Four comments in <c>Flows/</c>
/// documented that as a standing workaround ("a ScrollablePanel does NOT honour StickyPosition,
/// so the bands must be window children") rather than fixing it.
/// </para>
/// <para>
/// This is a deliberate BEHAVIOUR CHANGE, not a pure addition: five panels in DemoApp gain pinned
/// bands they did not have. The tests here pin the new contract - a band holds its row while the
/// body scrolls underneath, and the body cannot scroll past its end by the band heights.
/// </para>
/// </remarks>
public class PanelHonoursStickyTests
{
	private readonly ITestOutputHelper _out;

	public PanelHonoursStickyTests(ITestOutputHelper output) => _out = output;

	private static string[] Render(Window window)
	{
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		return GoldenBuffer.Capture(window, "panel").ToText().Split('\n');
	}

	/// <summary>
	/// A panel with a pinned header, a pinned footer, and enough body rows to overflow - the
	/// canonical sticky sandwich, one level down from the window root where it already worked.
	/// </summary>
	private static (Window Window, ScrollablePanelControl Panel) Sandwich(
		ConsoleWindowSystem system, int w = 40, int h = 12, int bodyLines = 40)
	{
		var window = new Window(system) { Title = "Panel", Width = w, Height = h, Left = 0, Top = 0 };

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithScrollbar(false)
			.Build();

		panel.AddControl(Builder.Markup("HEADER").StickyTop().Build());
		for (int i = 0; i < bodyLines; i++)
			panel.AddControl(Builder.Markup($"body {i}").Build());
		panel.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());

		window.AddControl(panel);
		system.AddWindow(window);
		return (window, panel);
	}

	[Fact]
	public void ABandHoldsItsRowWhileTheBodyScrollsUnderneath()
	{
		// The whole point: the header must not scroll away, and the footer must not be reachable
		// only by scrolling to the end.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var (window, panel) = Sandwich(system);

		var before = Render(window);
		_out.WriteLine(string.Join("\n", before));

		Assert.Contains("HEADER", before[0]);
		Assert.Contains("body 0", before[1]);
		Assert.Contains("FOOTER", before[^2]);

		panel.ScrollVerticalBy(5);
		var after = Render(window);
		_out.WriteLine("--- after scrolling 5 ---");
		_out.WriteLine(string.Join("\n", after));

		// Bands unmoved, body advanced.
		Assert.Contains("HEADER", after[0]);
		Assert.Contains("FOOTER", after[^2]);
		Assert.Contains("body 5", after[1]);
		Assert.DoesNotContain("body 0", string.Join("\n", after));
	}

	[Fact]
	public void TheBodyCannotScrollPastItsEndByTheBandHeights()
	{
		// The failure this guards: if the bands are counted as scrollable content, the maximum
		// offset is overstated by their height and the body scrolls off into blank rows.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var (window, panel) = Sandwich(system);

		Render(window);
		panel.ScrollVerticalBy(10_000);
		var atEnd = Render(window);
		_out.WriteLine(string.Join("\n", atEnd));

		Assert.Contains("HEADER", atEnd[0]);
		Assert.Contains("FOOTER", atEnd[^2]);

		// The last body line must be visible, and the row above the footer must not be blank -
		// that blank row IS the overshoot.
		Assert.Contains("body 39", string.Join("\n", atEnd));
		Assert.False(string.IsNullOrWhiteSpace(atEnd[^3]),
			"the body overscrolled: blank rows appeared above the pinned footer");
	}

	[Fact]
	public void AFillBodyChildGetsTheViewportMinusTheBands()
	{
		// A Fill child must divide what is left AFTER the bands take their rows, not the whole
		// viewport - otherwise it overlaps them or forces a phantom scroll.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Fill", Width = 30, Height = 10, Left = 0, Top = 0 };

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithScrollbar(false)
			.Build();

		panel.AddControl(Builder.Markup("TOP").StickyTop().Build());
		var filler = Builder.Markup("FILL").WithVerticalAlignment(VerticalAlignment.Fill).Build();
		panel.AddControl(filler);
		panel.AddControl(Builder.Markup("BOT").StickyBottom().Build());

		window.AddControl(panel);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.Contains("TOP", lines[0]);
		Assert.Contains("FILL", lines[1]);
		Assert.Contains("BOT", lines[^2]);

		// The Fill child occupies the gap exactly: no scrollbar-triggering overflow.
		Assert.False(panel.CanScrollDown,
			"a Fill child sized to the band-reduced viewport must not overflow it");
	}

	[Fact]
	public void TwoBandsOnTheSameEdgeKeepAuthorOrder()
	{
		// WPF reverses far-edge children; this codebase does not - a Rule declared before a help
		// line must still paint above it (ImageViewerWindow relies on exactly this).
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Two", Width = 30, Height = 10, Left = 0, Top = 0 };

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithScrollbar(false)
			.Build();

		for (int i = 0; i < 20; i++)
			panel.AddControl(Builder.Markup($"line {i}").Build());
		panel.AddControl(Builder.Markup("RULE").StickyBottom().Build());
		panel.AddControl(Builder.Markup("HELP").StickyBottom().Build());

		window.AddControl(panel);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		int rule = Array.FindIndex(lines, l => l.Contains("RULE"));
		int help = Array.FindIndex(lines, l => l.Contains("HELP"));

		Assert.True(rule >= 0 && help >= 0, "both bottom bands must be painted");
		Assert.True(rule < help, $"author order must hold: RULE (row {rule}) above HELP (row {help})");
	}

	[Fact]
	public void BandsSurviveAReRenderAndRealKeyInput()
	{
		// The "real thing" path: drive the panel through the real input pipeline, re-render, and
		// assert the pinned state SURVIVES the re-render rather than being a first-frame accident.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var (window, panel) = Sandwich(system);

		Render(window);
		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

		for (int i = 0; i < 3; i++)
		{
			system.InputStateService.EnqueueKey(
				new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false));
			system.Input.ProcessInput();
		}

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.Contains("HEADER", lines[0]);
		Assert.Contains("FOOTER", lines[^2]);
		Assert.DoesNotContain("body 0\n", string.Join("\n", lines));
	}

	[Fact]
	public void AHorizontalDockFallsThroughToTheBody()
	{
		// A panel stacks vertically, so Left/Right have no band to claim. They must behave as
		// ordinary scrolling children rather than silently vanishing.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Side", Width = 30, Height = 8, Left = 0, Top = 0 };

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithScrollbar(false)
			.Build();

		var side = Builder.Markup("SIDE").Build();
		side.Dock = SharpConsoleUI.Layout.Dock.Left;
		panel.AddControl(side);
		panel.AddControl(Builder.Markup("after").Build());

		window.AddControl(panel);
		system.AddWindow(window);

		var lines = Render(window);
		_out.WriteLine(string.Join("\n", lines));

		Assert.Contains("SIDE", lines[0]);
		Assert.Contains("after", lines[1]);
	}

	[Fact]
	public void APanelWithNoStickyChildrenIsUnchanged()
	{
		// The regression guard for every existing panel: no docked child means no band path, and
		// the layout must be exactly what it was.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Plain", Width = 30, Height = 8, Left = 0, Top = 0 };

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithScrollbar(false)
			.Build();

		for (int i = 0; i < 20; i++)
			panel.AddControl(Builder.Markup($"row {i}").Build());

		window.AddControl(panel);
		system.AddWindow(window);

		var before = Render(window);
		Assert.Contains("row 0", before[0]);

		panel.ScrollVerticalBy(4);
		var after = Render(window);

		// Everything scrolls, nothing is pinned.
		Assert.Contains("row 4", after[0]);
		Assert.DoesNotContain("row 0", string.Join("\n", after));
	}
}
