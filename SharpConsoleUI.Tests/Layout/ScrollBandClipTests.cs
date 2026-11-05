// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// A pinned band inside a <see cref="ScrollablePanelControl"/> owns its strip: scrolling body
/// content must never paint into it, however far the body has scrolled.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these exist.</b> <c>ScrollLayout.GetPaintClipRect</c> insets the body's clip rectangle
/// by the pinned band rows. It runs once per child during the paint walk, so it cannot afford to
/// work out those rows itself: doing so by scanning the siblings is O(children) per child, which
/// is O(children^2) per frame and was 66% of a scroll at 2,000 children. The band totals are
/// therefore computed once by the arrange pass and read here.
/// </para>
/// <para>
/// <b>What that makes fragile.</b> Cached layout state is only correct while the thing it caches
/// cannot change between the write and the read. These tests pin the cases where it could: a band
/// appearing, a band being hidden, and a band changing height, each followed by a re-render whose
/// output must agree with a panel that never had a stale value to begin with. They assert the
/// PAINTED result rather than the cached field, because a correct-looking field that never reaches
/// the clip rectangle is still a bug.
/// </para>
/// </remarks>
public class ScrollBandClipTests
{
	private const int WindowWidth = 60;
	private const int WindowHeight = 16;
	private const int BodyRows = 200;

	private sealed class Harness
	{
		public required ConsoleWindowSystem System { get; init; }
		public required Window Window { get; init; }
		public required ScrollablePanelControl Panel { get; init; }
		public required MarkupControl Header { get; init; }
		public required MarkupControl Footer { get; init; }

		public List<string> Render() => Window.RenderAndGetVisibleContent();
	}

	private static Harness Build(bool headerVisible = true, bool footerVisible = true)
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = WindowWidth, Height = WindowHeight };
		var panel = new ScrollablePanelControl { Height = WindowHeight - 4 };

		var header = new MarkupControl(new List<string> { "HEADER" })
		{
			Dock = Dock.Top,
			Visible = headerVisible,
		};
		var footer = new MarkupControl(new List<string> { "FOOTER" })
		{
			Dock = Dock.Bottom,
			Visible = footerVisible,
		};

		panel.AddControl(header);
		panel.AddControl(footer);
		for (int i = 0; i < BodyRows; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"body {i:D4}" }));

		window.AddControl(panel);
		system.AddWindow(window);

		return new Harness
		{
			System = system,
			Window = window,
			Panel = panel,
			Header = header,
			Footer = footer,
		};
	}

	/// <summary>Scrolls to the far end, where the body is longest and most likely to overrun a band.</summary>
	private static void ScrollToEnd(Harness h)
	{
		h.Render();
		for (int i = 0; i < BodyRows + 20; i++)
		{
			h.Panel.ScrollVerticalBy(1);
			h.Render();
		}
	}

	private static bool Shows(List<string> lines, string text)
		=> lines.Exists(l => l.Contains(text, StringComparison.Ordinal));

	/// <summary>
	/// The baseline: with the body scrolled as far as it goes, both bands are still on screen. If
	/// the clip inset were lost, body rows would paint over them and the text would be gone.
	/// </summary>
	[Fact]
	public void BandsSurviveScrollingTheBodyToItsEnd()
	{
		var h = Build();
		ScrollToEnd(h);

		var lines = h.Render();
		Assert.True(Shows(lines, "HEADER"), "the pinned top band was painted over by body content");
		Assert.True(Shows(lines, "FOOTER"), "the pinned bottom band was painted over by body content");
	}

	/// <summary>
	/// A band HIDDEN after the panel has been painted with it. The strip it held must be released to
	/// the body: a cached inset that still reserved those rows would leave a gap no content reaches.
	/// </summary>
	[Fact]
	public void HidingABandReleasesItsRowsToTheBody()
	{
		var h = Build();
		ScrollToEnd(h);

		int before = h.Render().FindAll(l => l.Contains("body ", StringComparison.Ordinal)).Count;

		h.Header.Visible = false;
		h.Footer.Visible = false;
		var after = h.Render();

		Assert.False(Shows(after, "HEADER"), "a hidden band is still being painted");
		Assert.False(Shows(after, "FOOTER"), "a hidden band is still being painted");

		int now = after.FindAll(l => l.Contains("body ", StringComparison.Ordinal)).Count;
		Assert.True(now > before,
			$"hiding both bands should give their rows to the body, but body rows went {before} -> {now}");
	}

	/// <summary>
	/// A band SHOWN after the panel has been painted without it. It must take its strip back, and
	/// the body must stop painting there.
	/// </summary>
	[Fact]
	public void ShowingABandTakesItsRowsBackFromTheBody()
	{
		var h = Build(headerVisible: false, footerVisible: false);
		ScrollToEnd(h);

		int before = h.Render().FindAll(l => l.Contains("body ", StringComparison.Ordinal)).Count;

		h.Header.Visible = true;
		h.Footer.Visible = true;
		var after = h.Render();

		Assert.True(Shows(after, "HEADER"), "a band made visible was not painted");
		Assert.True(Shows(after, "FOOTER"), "a band made visible was not painted");

		int now = after.FindAll(l => l.Contains("body ", StringComparison.Ordinal)).Count;
		Assert.True(now < before,
			$"showing both bands should take rows from the body, but body rows went {before} -> {now}");
	}

	/// <summary>
	/// A band that GROWS taller. The inset must grow with it, or the body keeps painting into the
	/// rows the band has just claimed.
	/// </summary>
	[Fact]
	public void ABandThatGrowsTallerKeepsItsWholeStrip()
	{
		var h = Build();
		ScrollToEnd(h);

		h.Header.SetContent(new List<string> { "HEADER", "HEADER-L2", "HEADER-L3" });
		var after = h.Render();

		Assert.True(Shows(after, "HEADER-L3"),
			"the band grew but its last row is not on screen, so the body is painting over it");
		Assert.True(Shows(after, "FOOTER"), "the bottom band was lost when the top band grew");
	}

	/// <summary>
	/// The reason the value is cached at all: the per-child clip must stay O(1), so the frame cost
	/// must not grow per child as children are added.
	/// </summary>
	/// <remarks>
	/// Wall-clock in a test is a blunt instrument — this runs alongside 8,000 other tests competing
	/// for the same cores, so a single sample is not trustworthy. It takes the BEST of several
	/// rounds, since noise can only ever make a round slower, and compares the two sizes within the
	/// same round so a scheduling stall hits both. The threshold is loose for the same reason: the
	/// defect it guards against is a complexity class, which shows up as a ratio that keeps climbing
	/// with N, not as a few percent.
	/// </remarks>
	[Fact]
	public void PerChildFrameCostDoesNotGrowWithChildCount()
	{
		static double MsPerChild(int children)
		{
			var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
			var window = new Window(system) { Width = 120, Height = 40 };
			var panel = new ScrollablePanelControl { Height = 36 };
			panel.AddControl(new MarkupControl(new List<string> { "HEADER" }) { Dock = Dock.Top });
			for (int i = 0; i < children; i++)
				panel.AddControl(new MarkupControl(new List<string> { $"body {i:D5}" }));
			window.AddControl(panel);
			system.AddWindow(window);

			window.RenderAndGetVisibleContent();
			for (int i = 0; i < 12; i++) { panel.ScrollVerticalBy(1); window.RenderAndGetVisibleContent(); }

			const int Scrolls = 10;
			int dir = 1;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			for (int i = 0; i < Scrolls; i++)
			{
				dir = -dir;
				panel.ScrollVerticalBy(dir);
				window.RenderAndGetVisibleContent();
			}
			sw.Stop();
			return sw.Elapsed.TotalMilliseconds / Scrolls / children;
		}

		// Best of three rounds: noise only ever inflates a measurement, so the minimum is the
		// closest any round got to the real cost.
		double ratio = double.MaxValue;
		double bestSmall = 0, bestLarge = 0;
		for (int round = 0; round < 3; round++)
		{
			double small = MsPerChild(500);
			double large = MsPerChild(2000);
			if (large / small < ratio)
			{
				ratio = large / small;
				bestSmall = small;
				bestLarge = large;
			}
		}

		// Linear-or-better sits at or below 1. The sibling-scanning form this replaced measured
		// ~2.0 over this 4x range and kept climbing beyond it (~18x slower at 8,000 children).
		Assert.True(ratio <= 1.8,
			$"per-child frame cost grew with child count: {bestSmall * 1000:N2} us/child at 500 vs " +
			$"{bestLarge * 1000:N2} us/child at 2000 (best-of-3 ratio {ratio:F2}). A per-child paint " +
			$"hook is doing per-panel work. See ScrollLayout.GetPaintClipRect.");
	}
}
