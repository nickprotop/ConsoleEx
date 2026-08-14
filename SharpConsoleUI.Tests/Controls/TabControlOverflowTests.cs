// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using TabControl = SharpConsoleUI.Controls.TabControl;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A header row with more tabs than it has cells. The row draws the run that fits and scrolls it so the
/// active tab is always in view; before it did neither, drawing from index 0 and clipping, so the tabs
/// past the edge were gone and the active tab with them whenever it sat late enough in the order.
/// </summary>
public class TabControlOverflowTests
{
	private const int WindowWidth = 40;

	private static string StripAnsiCodes(IEnumerable<string> lines) =>
		string.Join("\n", lines.Select(line =>
			System.Text.RegularExpressions.Regex.Replace(line, @"\x1b\[[0-9;]*m", "")));

	private static MarkupControl Label(string text) => new(new List<string> { text });

	/// <summary>Eight tabs of eleven cells each in a forty-cell window: three of them fit.</summary>
	private static (Window Window, TabControl Tabs) Crowded(int active)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = WindowWidth, Height = 12 };
		var tabs = new TabControl { Height = 8 };
		for (int i = 0; i < 8; i++)
			tabs.AddTab($"Channel {i}", Label($"Content {i}"));
		tabs.ActiveTabIndex = active;
		window.AddControl(tabs);
		return (window, tabs);
	}

	/// <summary>
	/// The headline: the tab the control says is active is one the reader can actually see. It used to be
	/// drawn only if it happened to fall inside the first few tabs' worth of cells.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	[InlineData(7)]
	public void ActiveTabIsAlwaysDrawn(int active)
	{
		var (window, _) = Crowded(active);

		var plain = StripAnsiCodes(window.RenderAndGetVisibleContent());

		Assert.Contains($"Channel {active}", plain);
	}

	/// <summary>
	/// And the row says there is more to see. Without a marker a truncated strip is indistinguishable
	/// from a complete one — which was the whole of "how do I know there are other tabs?".
	/// </summary>
	[Fact]
	public void TabsBeyondEitherEdgeAreMarked()
	{
		var (window, _) = Crowded(active: 4);

		var header = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n')[0];

		Assert.Contains('‹', header);
		Assert.Contains('›', header);
	}

	/// <summary>The first tab being active is not a scrolled state, so no left marker is drawn.</summary>
	[Fact]
	public void AStripAtItsStartIsMarkedOnlyOnTheRight()
	{
		var (window, _) = Crowded(active: 0);

		var header = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n')[0];

		Assert.DoesNotContain('‹', header);
		Assert.Contains('›', header);
	}

	/// <summary>…and the last tab being active leaves nothing past the right edge to mark.</summary>
	[Fact]
	public void AStripAtItsEndIsMarkedOnlyOnTheLeft()
	{
		var (window, _) = Crowded(active: 7);

		var header = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n')[0];

		Assert.Contains('‹', header);
		Assert.DoesNotContain('›', header);
	}

	/// <summary>A row with space for all its tabs is drawn exactly as it always was.</summary>
	[Fact]
	public void ARowWithRoomIsUnchanged()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 12 };
		var tabs = new TabControl { Height = 8 };
		tabs.AddTab("One", Label("1"));
		tabs.AddTab("Two", Label("2"));
		window.AddControl(tabs);

		var header = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n')[0];

		Assert.Contains("One", header);
		Assert.Contains("Two", header);
		Assert.DoesNotContain('‹', header);
		Assert.DoesNotContain('›', header);
	}

	/// <summary>
	/// Clicking picks the tab that is under the pointer, not the one that would be there if the row had
	/// never scrolled. The hit test reads the same layout the paint does; two copies of the arithmetic is
	/// exactly how a scrolled strip ends up selecting a tab the user cannot see.
	/// </summary>
	[Fact]
	public void ClickingAScrolledStripPicksTheTabUnderThePointer()
	{
		var (window, tabs) = Crowded(active: 7);
		window.RenderAndGetVisibleContent();

		// One cell past the ‹ marker is the first drawn tab's leading pad; two past it is its title.
		var strip = tabs.LayoutStrip(tabs.TabPages, tabs.ActiveTabIndex, tabs.ActualWidth - tabs.Margin.Left - tabs.Margin.Right);
		int firstDrawn = strip.First;
		int x = strip.Offsets[0] + 1;

		var args = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked },
			new Point(x, 0),
			new Point(x, 0),
			new Point(x, 0));
		tabs.ProcessMouseEvent(args);

		Assert.True(firstDrawn > 0, "the strip should be scrolled for this to mean anything");
		Assert.Equal(firstDrawn, tabs.ActiveTabIndex);
	}

	/// <summary>
	/// Turning the markers off gives their cells back to the tabs; the row still scrolls, because an
	/// active tab nobody can see is a defect rather than a preference.
	/// </summary>
	[Fact]
	public void IndicatorsCanBeTurnedOffAndTheRowStillScrolls()
	{
		var (window, tabs) = Crowded(active: 7);
		tabs.ShowTabScrollIndicators = false;

		var header = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n')[0];

		Assert.DoesNotContain('‹', header);
		Assert.DoesNotContain('›', header);
		Assert.Contains("Channel 7", header);
	}

	/// <summary>
	/// A control that has never been painted has no arranged width, and that means "not known" rather
	/// than "no room" — it reports every tab as drawn, which is what callers saw before any of this.
	/// </summary>
	[Fact]
	public void AnUnpaintedControlReportsEveryTab()
	{
		var tabs = new TabControl();
		for (int i = 0; i < 5; i++)
			tabs.AddTab($"Channel {i}", Label($"{i}"));

		var strip = tabs.LayoutStrip(tabs.TabPages, 0, 0);

		Assert.Equal(0, strip.First);
		Assert.Equal(5, strip.Count);
		Assert.False(strip.MoreLeft);
		Assert.False(strip.MoreRight);
	}

	/// <summary>
	/// A row too narrow for even one tab still draws that one, clipped, rather than drawing nothing: the
	/// alternative is a header row that is blank at exactly the width where it is needed most.
	/// </summary>
	[Fact]
	public void ARowNarrowerThanASingleTabStillDrawsIt()
	{
		var tabs = new TabControl();
		tabs.AddTab("A very long tab title indeed", Label("1"));
		tabs.AddTab("Another", Label("2"));

		var strip = tabs.LayoutStrip(tabs.TabPages, 1, 6);

		Assert.Equal(1, strip.Count);
		Assert.Equal(1, strip.First);
	}
}
