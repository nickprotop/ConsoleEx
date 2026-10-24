// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Border-delegated scrollbars: a region hands its scrollbar to the window frame, reclaiming the
/// column (or row) it would otherwise spend drawing one.
/// </summary>
/// <remarks>
/// The window's own body driving a thumb in the right border is this relationship, previously
/// hard-wired. These tests cover the generalisation: an arbitrary region taking the border over,
/// the new horizontal case in the bottom border, and the single-track rule that stops two
/// regions fighting over one thumb.
/// </remarks>
[Collection("RootLayout")]
public class BorderDelegatedScrollbarTests
{
	private readonly ITestOutputHelper _out;

	public BorderDelegatedScrollbarTests(ITestOutputHelper output) => _out = output;

	/// <summary>A source with no live control behind it, for the registry rules.</summary>
	private sealed class FakeSource : IBorderScrollSource
	{
		public int Offset { get; set; }
		public int Extent { get; set; } = 100;
		public int Viewport { get; set; } = 10;
		public int ScrolledTo { get; private set; } = -1;
		public void ScrollTo(int offset) => ScrolledTo = offset;
	}

	private static (ConsoleWindowSystem System, Window Window, ScrollablePanelControl Panel)
		PanelWindow(int w = 40, int h = 12, int lines = 60, string text = "line")
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Border", Width = w, Height = h, Left = 0, Top = 0 };

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		for (int i = 0; i < lines; i++)
			panel.AddControl(Builder.Markup($"{text} {i}").Build());

		window.AddControl(panel);
		system.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		return (system, window, panel);
	}

	private static void Render(ConsoleWindowSystem system) => system.Render.UpdateDisplay();

	#region Registry rules

	[Fact]
	public void NothingIsDelegatedByDefault()
	{
		var (_, window, _) = PanelWindow();

		Assert.Null(window.BorderScrollSource(ScrollAxis.Vertical));
		Assert.Null(window.BorderScrollSource(ScrollAxis.Horizontal));
	}

	[Fact]
	public void DelegatingBothAxesRegistersBoth()
	{
		var (_, window, _) = PanelWindow();
		var source = new FakeSource();

		window.DelegateScrollbarToBorder(source, ScrollAxis.Both);

		Assert.Same(source, window.BorderScrollSource(ScrollAxis.Vertical));
		Assert.Same(source, window.BorderScrollSource(ScrollAxis.Horizontal));
	}

	[Fact]
	public void ASecondRegionOnTheSameAxisIsRefused()
	{
		// The border is a single track per axis. Two regions sharing one would fight over the
		// thumb, so the second registration must be refused rather than silently winning.
		var (_, window, _) = PanelWindow();

		window.DelegateScrollbarToBorder(new FakeSource(), ScrollAxis.Vertical);

		var ex = Assert.Throws<InvalidOperationException>(
			() => window.DelegateScrollbarToBorder(new FakeSource(), ScrollAxis.Vertical));
		Assert.Contains("already delegated", ex.Message);
	}

	[Fact]
	public void TheSameRegionMayReRegisterIdempotently()
	{
		var (_, window, _) = PanelWindow();
		var source = new FakeSource();

		window.DelegateScrollbarToBorder(source, ScrollAxis.Vertical);
		window.DelegateScrollbarToBorder(source, ScrollAxis.Vertical);

		Assert.Same(source, window.BorderScrollSource(ScrollAxis.Vertical));
	}

	[Fact]
	public void ReleasingAnAxisFreesItForAnotherRegion()
	{
		var (_, window, _) = PanelWindow();
		var first = new FakeSource();
		var second = new FakeSource();

		window.DelegateScrollbarToBorder(first, ScrollAxis.Vertical);
		window.DelegateScrollbarToBorder(null, ScrollAxis.Vertical);
		window.DelegateScrollbarToBorder(second, ScrollAxis.Vertical);

		Assert.Same(second, window.BorderScrollSource(ScrollAxis.Vertical));
	}

	[Fact]
	public void TheAxesAreIndependent()
	{
		var (_, window, _) = PanelWindow();
		var vertical = new FakeSource();
		var horizontal = new FakeSource();

		window.DelegateScrollbarToBorder(vertical, ScrollAxis.Vertical);
		window.DelegateScrollbarToBorder(horizontal, ScrollAxis.Horizontal);

		Assert.Same(vertical, window.BorderScrollSource(ScrollAxis.Vertical));
		Assert.Same(horizontal, window.BorderScrollSource(ScrollAxis.Horizontal));
	}

	#endregion

	#region Rendering

	[Fact]
	public void TheRightBorderTracksTheDelegatedRegionNotTheWindow()
	{
		// The point of delegation: the thumb follows the REGION's offset. The window body here
		// is not scrolled at all, so a thumb below the top proves the border read the panel.
		var (system, window, panel) = PanelWindow();

		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForVertical(panel), ScrollAxis.Vertical);
		Render(system);

		int top = ThumbRow(system, window);
		Assert.True(top >= 0, "a delegated scrollable region must draw a thumb in the right border");

		panel.ScrollVerticalBy(40);
		Render(system);

		int scrolled = ThumbRow(system, window);
		_out.WriteLine($"thumb row: {top} -> {scrolled}");
		Assert.True(scrolled > top, $"the thumb must descend with the region (was {top}, now {scrolled})");
	}

	[Fact]
	public void TheBottomBorderDrawsAHorizontalThumb()
	{
		// New surface: the bottom border has never drawn a thumb of any kind.
		var (system, window, panel) = PanelWindow(w: 30, h: 10, lines: 5, text: "a fairly long line of content");
		panel.HorizontalScrollMode = ScrollMode.Scroll;

		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForHorizontal(panel), ScrollAxis.Horizontal);
		Render(system);

		var before = BottomBorderRow(system, window);
		_out.WriteLine($"bottom: '{before}'");
		Assert.Contains('█', before);

		int leftThumb = before.IndexOf('█');

		panel.ScrollHorizontalBy(20);
		Render(system);

		var after = BottomBorderRow(system, window);
		_out.WriteLine($"bottom: '{after}'");
		int rightThumb = after.IndexOf('█');

		Assert.True(rightThumb > leftThumb,
			$"the horizontal thumb must move right with the region (was {leftThumb}, now {rightThumb})");
	}

	[Fact]
	public void ARegionWithNothingToScrollDrawsPlainFrame()
	{
		// Delegation is not a demand to draw a track: a region that fits shows ordinary border.
		var (system, window, panel) = PanelWindow(lines: 2);

		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForHorizontal(panel), ScrollAxis.Horizontal);
		Render(system);

		var bottom = BottomBorderRow(system, window);
		_out.WriteLine($"bottom: '{bottom}'");

		Assert.DoesNotContain('█', bottom);
		Assert.DoesNotContain('░', bottom);
	}

	[Fact]
	public void AnUndelegatedWindowIsUnchanged()
	{
		// The regression guard: every existing window delegates nothing, and must render exactly
		// as before - a vertical thumb from the window body, a plain bottom border.
		var (system, window, _) = PanelWindow();
		Render(system);

		var bottom = BottomBorderRow(system, window);
		Assert.DoesNotContain('█', bottom);
		Assert.DoesNotContain('░', bottom);
	}

	#endregion

	[Fact]
	public void DelegatingHidesTheRegionsOwnScrollbar()
	{
		// The bug this guards: delegation that leaves the interior bar in place draws TWO
		// scrollbars and reclaims none of the space the caller delegated in order to reclaim.
		var (_, window, panel) = PanelWindow();
		Assert.True(panel.ShowScrollbar);

		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForVertical(panel), ScrollAxis.Vertical);

		Assert.False(panel.ShowScrollbar);
	}

	[Fact]
	public void ReleasingRestoresTheRegionsOwnScrollbar()
	{
		var (_, window, panel) = PanelWindow();

		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForVertical(panel), ScrollAxis.Vertical);
		window.DelegateScrollbarToBorder(null, ScrollAxis.Vertical);

		Assert.True(panel.ShowScrollbar);
	}

	[Fact]
	public void ReleasingDoesNotResurrectAScrollbarTheCallerHadAlreadyHidden()
	{
		// Restore puts back what WAS there, not an assumed default.
		var (_, window, panel) = PanelWindow();
		panel.ShowScrollbar = false;

		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForVertical(panel), ScrollAxis.Vertical);
		window.DelegateScrollbarToBorder(null, ScrollAxis.Vertical);

		Assert.False(panel.ShowScrollbar);
	}

	[Fact]
	public void ABottomDockSurvivesDelegationAndScrolling()
	{
		// A docked bottom band must hold its row while a border-delegated body scrolls beneath
		// it, at every offset - not only before the body has moved.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Bands", Width = 40, Height = 12, Left = 0, Top = 0 };

		window.DockTop.Add(Builder.Markup("TOPBAND").Build());
		window.DockBottom.Add(Builder.Markup("BOTTOMBAND").Build());

		var panel = Builder.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		for (int i = 0; i < 40; i++)
			panel.AddControl(Builder.Markup($"p {i}").Build());
		window.DockBody.Add(panel);

		system.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.DelegateScrollbarToBorder(PanelBorderScrollSource.ForVertical(panel), ScrollAxis.Vertical);

		foreach (int scroll in new[] { 0, 5, 20, 10_000 })
		{
			if (scroll > 0) panel.ScrollVerticalBy(scroll);
			system.Render.UpdateDisplay();

			var shot = GoldenBuffer.Capture(window, $"scroll-{scroll}");
			var lines = shot.ToText().Split('\n');
			_out.WriteLine($"--- scrolled {scroll} ---");
			_out.WriteLine(string.Join("\n", lines));

			Assert.Contains("TOPBAND", lines[0]);
			Assert.True(lines.Any(l => l.Contains("BOTTOMBAND")),
				$"the bottom dock vanished at scroll offset {scroll}");
		}
	}

	[Fact]
	public void TheThumbReachesTheBottomOfTheTrackWhenDockedBandsShortenTheBody()
	{
		// The bug: the thumb was scaled into the BODY viewport but drawn along the BORDER track.
		// They were equal while the body filled the frame, so the old code conflated them -
		// until docked bands made the body shorter. At full scroll the thumb stopped partway,
		// and with enough band rows it fell off the drawn track entirely and vanished.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Thumb", Width = 40, Height = 16, Left = 0, Top = 0 };

		// Bands on both edges, so the body is several rows shorter than the border track.
		window.DockTop.Add(Builder.Markup("TOP").Build());
		window.DockTop.Add(Builder.Markup("TOP2").Build());
		window.DockBottom.Add(Builder.Markup("BOTTOM").Build());
		window.DockBottom.Add(Builder.Markup("BOTTOM2").Build());
		for (int i = 0; i < 60; i++)
			window.DockBody.Add(Builder.Markup($"body {i}").Build());

		system.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();

		int trackLength = window.Height - 2;

		int top = ThumbRow(system, window);
		Assert.True(top >= 0, "a scrollable window must draw a thumb");

		window.ScrollBy(10_000);
		system.Render.UpdateDisplay();

		int bottom = ThumbRow(system, window);
		_out.WriteLine($"track {trackLength}, thumb {top} -> {bottom}");

		Assert.True(bottom >= 0, "the thumb vanished at full scroll - it fell off the track");
		Assert.True(bottom > top, "the thumb must descend as the body scrolls");

		// At the end the thumb sits on the last track row before the arrow cap, not partway.
		// ThumbRow reports the border loop's y, which is one ahead of the track-relative row,
		// so the final thumb lands at trackLength - 1 rather than - 2.
		Assert.Equal(trackLength - 1, bottom);
	}

	[Fact]
	public void TheBorderScrollbarHasArrowCaps()
	{
		// The frame reads the same way as an interior scrollbar, which has always had caps.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Caps", Width = 40, Height = 14, Left = 0, Top = 0 };
		for (int i = 0; i < 60; i++)
			window.DockBody.Add(Builder.Markup($"line {i}").Build());

		system.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		int x = window.Left + window.Width - 1;
		var up = snapshot.GetBack(x, window.Top + 1).Character;
		var down = snapshot.GetBack(x, window.Top + window.Height - 2).Character;

		Assert.Equal(new System.Text.Rune(ControlDefaults.BorderScrollUpArrow), up);
		Assert.Equal(new System.Text.Rune(ControlDefaults.BorderScrollDownArrow), down);
	}

	#region Buffer helpers

	// Borders are painted by BorderRenderer straight to the driver, so they never appear in the
	// window's own content buffer (what GoldenBuffer captures). Reading them means a full system
	// render and the console snapshot - the same mechanism BorderRenderingTests uses.

	/// <summary>The row of the thumb in the right border, or -1.</summary>
	private static int ThumbRow(ConsoleWindowSystem system, Window window)
	{
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		int x = window.Left + window.Width - 1;
		for (int y = 1; y < window.Height - 1; y++)
			if (snapshot.GetBack(x, window.Top + y).Character == new System.Text.Rune('\u2588'))
				return y;
		return -1;
	}

	/// <summary>The bottom border row as text.</summary>
	private static string BottomBorderRow(ConsoleWindowSystem system, Window window)
	{
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var row = new System.Text.StringBuilder(window.Width);
		int y = window.Top + window.Height - 1;
		for (int x = 0; x < window.Width; x++)
			row.Append(snapshot.GetBack(window.Left + x, y).Character.ToString());
		return row.ToString();
	}

	#endregion
}
