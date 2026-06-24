// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Behavioral coverage for ScrollablePanelControl aspects not exercised by the layout/cursor
/// contract suites: horizontal scrolling, ScrollToPosition, AutoScroll attach/detach dynamics,
/// GetVisibleHeightForControl, InsertControl ordering, the Scrolled event payload, and degenerate
/// edge cases (zero viewport, single oversized child, all-invisible children).
/// </summary>
public class ScrollablePanelBehaviorTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelBehaviorTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	private static (ScrollablePanelControl panel, Window window) Render(ScrollablePanelControl panel)
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, window);
	}

	private static MarkupControl Wide(string text) => new MarkupControl(new List<string> { text });

	#region Horizontal scrolling

	[Fact]
	public void HorizontalScroll_DefaultMode_IsNone()
	{
		Assert.Equal(ScrollMode.None, new ScrollablePanelControl().HorizontalScrollMode);
	}

	[Fact]
	public void HorizontalScroll_WhenModeNone_DoesNotScroll()
	{
		var panel = new ScrollablePanelControl { Height = 6, HorizontalScrollMode = ScrollMode.None };
		panel.AddControl(Wide(new string('x', 300)));
		var (_, _) = Render(panel);

		panel.ScrollHorizontalBy(10);
		Assert.Equal(0, panel.HorizontalScrollOffset);
	}

	[Fact]
	public void HorizontalScroll_WhenModeScroll_ScrollsAndClamps()
	{
		var panel = new ScrollablePanelControl { Height = 6, HorizontalScrollMode = ScrollMode.Scroll };
		panel.AddControl(Wide(new string('x', 300)));
		var (_, _) = Render(panel);

		panel.ScrollHorizontalBy(10);
		_out.WriteLine($"hOffset={panel.HorizontalScrollOffset} contentW={panel.TotalContentWidth} viewportW={panel.ViewportWidth}");
		Assert.True(panel.HorizontalScrollOffset > 0, "Should scroll horizontally when content is wider than viewport.");

		// Clamp at the right edge.
		panel.ScrollHorizontalBy(10000);
		int max = System.Math.Max(0, panel.TotalContentWidth - panel.ViewportWidth);
		Assert.Equal(max, panel.HorizontalScrollOffset);

		// Clamp at the left edge.
		panel.ScrollHorizontalBy(-10000);
		Assert.Equal(0, panel.HorizontalScrollOffset);
	}

	[Fact]
	public void HorizontalScroll_CanScrollLeftRight_ReflectOffset()
	{
		var panel = new ScrollablePanelControl { Height = 6, HorizontalScrollMode = ScrollMode.Scroll };
		panel.AddControl(Wide(new string('x', 300)));
		var (_, _) = Render(panel);

		Assert.False(panel.CanScrollLeft);
		Assert.True(panel.CanScrollRight);

		panel.ScrollHorizontalBy(5);
		Assert.True(panel.CanScrollLeft);
	}

	#endregion

	#region ScrollToPosition

	[Fact]
	public void ScrollToPosition_SetsVerticalOffset()
	{
		var panel = new ScrollablePanelControl { Height = 5 };
		for (int i = 0; i < 20; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		panel.ScrollToPosition(vertical: 7);
		Assert.Equal(7, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollToPosition_WithHorizontal_SetsBothWhenHEnabled()
	{
		var panel = new ScrollablePanelControl { Height = 5, HorizontalScrollMode = ScrollMode.Scroll };
		for (int i = 0; i < 20; i++) panel.AddControl(Wide(new string('x', 200)));
		var (_, _) = Render(panel);

		panel.ScrollToPosition(vertical: 6, horizontal: 4);
		Assert.Equal(6, panel.VerticalScrollOffset);
		Assert.Equal(4, panel.HorizontalScrollOffset);
	}

	[Fact]
	public void ScrollToPosition_ClampsVerticalToRange()
	{
		var panel = new ScrollablePanelControl { Height = 5 };
		for (int i = 0; i < 8; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		panel.ScrollToPosition(vertical: 9999);
		int max = System.Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);
		Assert.Equal(max, panel.VerticalScrollOffset);
	}

	#endregion

	#region AutoScroll dynamics

	[Fact]
	public void AutoScroll_KeepsAtBottom_WhenContentAdded()
	{
		var panel = new ScrollablePanelControl { Height = 5, AutoScroll = true };
		for (int i = 0; i < 10; i++) panel.AddControl(Wide($"r{i}"));
		var (_, window) = Render(panel);

		int max = System.Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);
		Assert.Equal(max, panel.VerticalScrollOffset);

		// Add more — should follow to the new bottom.
		for (int i = 10; i < 20; i++) panel.AddControl(Wide($"r{i}"));
		window.RenderAndGetVisibleContent();
		int newMax = System.Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);
		Assert.Equal(newMax, panel.VerticalScrollOffset);
	}

	[Fact]
	public void AutoScroll_DetachesWhenUserScrollsUp()
	{
		var panel = new ScrollablePanelControl { Height = 5, AutoScroll = true };
		for (int i = 0; i < 20; i++) panel.AddControl(Wide($"r{i}"));
		var (_, window) = Render(panel);

		panel.ScrollVerticalBy(-3); // user scrolls up
		Assert.False(panel.AutoScroll, "Scrolling up detaches AutoScroll.");

		// Adding content should NOT yank back to the bottom now.
		int offsetBefore = panel.VerticalScrollOffset;
		for (int i = 20; i < 25; i++) panel.AddControl(Wide($"r{i}"));
		window.RenderAndGetVisibleContent();
		Assert.True(panel.VerticalScrollOffset <= offsetBefore + 1,
			"Detached AutoScroll must not jump to the bottom on new content.");
	}

	[Fact]
	public void AutoScroll_ReattachesWhenUserScrollsToBottom()
	{
		var panel = new ScrollablePanelControl { Height = 5, AutoScroll = true };
		for (int i = 0; i < 20; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		panel.ScrollVerticalBy(-3);
		Assert.False(panel.AutoScroll);

		panel.ScrollVerticalBy(9999); // back to bottom
		Assert.True(panel.AutoScroll, "Scrolling back to the bottom re-attaches AutoScroll.");
	}

	#endregion

	#region Scrolled event

	[Fact]
	public void Scrolled_Event_CarriesDirectionAndOffsets()
	{
		var panel = new ScrollablePanelControl { Height = 5 };
		for (int i = 0; i < 20; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		ScrollEventArgs? captured = null;
		panel.Scrolled += (_, e) => captured = e;

		panel.ScrollVerticalBy(3);
		Assert.NotNull(captured);
		Assert.Equal(ScrollDirection.Vertical, captured!.Direction);
		Assert.Equal(panel.VerticalScrollOffset, captured.VerticalOffset);
	}

	[Fact]
	public void Scrolled_Event_NotFired_WhenOffsetUnchanged()
	{
		var panel = new ScrollablePanelControl { Height = 20 }; // fits content, no scroll range
		panel.AddControl(Wide("only"));
		var (_, _) = Render(panel);

		int fires = 0;
		panel.Scrolled += (_, _) => fires++;
		panel.ScrollVerticalBy(5); // clamped to 0, no change
		Assert.Equal(0, fires);
	}

	#endregion

	#region GetVisibleHeightForControl

	[Fact]
	public void GetVisibleHeightForControl_ReturnsViewportHeight_AfterLayout()
	{
		var panel = new ScrollablePanelControl { Height = 12 };
		var child = Wide("c");
		panel.AddControl(child);
		var (_, _) = Render(panel);

		int? h = panel.GetVisibleHeightForControl(child);
		Assert.Equal(panel.ViewportHeight, h);
	}

	[Fact]
	public void GetVisibleHeightForControl_ReturnsNull_BeforeLayout()
	{
		var panel = new ScrollablePanelControl();
		var child = Wide("c");
		panel.AddControl(child);
		// Not rendered → viewport unknown.
		Assert.Null(panel.GetVisibleHeightForControl(child));
	}

	#endregion

	#region InsertControl ordering

	[Fact]
	public void InsertControl_PlacesChildAtIndex_AffectingPaintOrder()
	{
		var panel = new ScrollablePanelControl { Height = 20 };
		var a = Wide("a");
		var c = Wide("c");
		panel.AddControl(a);
		panel.AddControl(c);
		var b = Wide("b");
		panel.InsertControl(1, b); // between a and c
		var (_, window) = Render(panel);

		var children = panel.Children;
		Assert.Equal(a, children[0]);
		Assert.Equal(b, children[1]);
		Assert.Equal(c, children[2]);

		// And b is painted between a and c (its ActualY is between them).
		Assert.True(a.ActualY < b.ActualY && b.ActualY < c.ActualY);
	}

	[Fact]
	public void InsertControl_ClampsIndexToBounds()
	{
		var panel = new ScrollablePanelControl { Height = 20 };
		panel.AddControl(Wide("a"));
		var x = Wide("x");
		panel.InsertControl(999, x); // clamps to end
		Assert.Equal(x, panel.Children[panel.Children.Count - 1]);
	}

	#endregion

	#region Degenerate / edge cases

	[Fact]
	public void EmptyPanel_RendersWithoutCrash_AndHasNoScrollRange()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		var (_, _) = Render(panel);
		Assert.False(panel.CanScrollUp);
		Assert.False(panel.CanScrollDown);
		Assert.Empty(panel.Children);
	}

	[Fact]
	public void AllChildrenInvisible_NoScrollRange_NoCrash()
	{
		var panel = new ScrollablePanelControl { Height = 5 };
		for (int i = 0; i < 10; i++)
		{
			var c = Wide($"r{i}");
			c.Visible = false;
			panel.AddControl(c);
		}
		var (_, _) = Render(panel);
		Assert.False(panel.CanScrollDown);
		Assert.Equal(0, panel.TotalContentHeight);
	}

	[Fact]
	public void SingleChildTallerThanViewport_ScrollRangeEqualsOverflow()
	{
		var panel = new ScrollablePanelControl { Height = 5 };
		panel.AddControl(new MarkupControl(Enumerable.Range(0, 20).Select(i => $"line{i}").ToList()));
		var (_, _) = Render(panel);

		Assert.True(panel.CanScrollDown);
		int expectedMax = System.Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);
		panel.ScrollVerticalBy(9999);
		Assert.Equal(expectedMax, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ZeroHeightPanel_DoesNotCrash()
	{
		var panel = new ScrollablePanelControl { Height = 0 };
		panel.AddControl(Wide("x"));
		var ex = Record.Exception(() => Render(panel));
		Assert.Null(ex);
	}

	[Fact]
	public void RemoveControl_NonexistentChild_NoCrash_NoChange()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		var a = Wide("a");
		panel.AddControl(a);
		var (_, _) = Render(panel);

		var stranger = Wide("stranger");
		var ex = Record.Exception(() => panel.RemoveControl(stranger));
		Assert.Null(ex);
		Assert.Single(panel.Children);
	}

	[Fact]
	public void ClearContents_DisposesAndEmpties()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 5; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		panel.ClearContents();
		Assert.Empty(panel.Children);
		Assert.False(panel.CanScrollDown);
	}

	#endregion

	#region Scrollbar interaction

	private static MouseEventArgs Mouse(int x, int y, params SharpConsoleUI.Drivers.MouseFlags[] flags)
	{
		var pos = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), pos, pos, pos);
	}

	[Fact]
	public void Scrollbar_TrackClickBelowThumb_PagesDown()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 40; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight, "precondition: scrollable");
		int sbX = panel.ViewportWidth - 1; // scrollbar column (content-relative)
		int before = panel.VerticalScrollOffset;

		// Click low in the track (below the thumb) → page down.
		panel.ProcessMouseEvent(Mouse(sbX, panel.ViewportHeight - 2, SharpConsoleUI.Drivers.MouseFlags.Button1Clicked));
		_out.WriteLine($"track page-down: before={before} after={panel.VerticalScrollOffset}");
		Assert.True(panel.VerticalScrollOffset > before, "Clicking the track below the thumb pages down.");
	}

	[Fact]
	public void Scrollbar_ThumbDrag_ScrollsProportionally()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 40; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		int sbX = panel.ViewportWidth - 1;
		// Press on the thumb (top of track) then drag down.
		panel.ProcessMouseEvent(Mouse(sbX, 1, SharpConsoleUI.Drivers.MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(sbX, 5, SharpConsoleUI.Drivers.MouseFlags.Button1Dragged));
		_out.WriteLine($"thumb drag offset={panel.VerticalScrollOffset}");
		Assert.True(panel.VerticalScrollOffset > 0, "Dragging the thumb down scrolls the content down.");

		// Release ends the drag.
		panel.ProcessMouseEvent(Mouse(sbX, 5, SharpConsoleUI.Drivers.MouseFlags.Button1Released));
		int afterRelease = panel.VerticalScrollOffset;
		// A subsequent unrelated move must not keep scrolling.
		panel.ProcessMouseEvent(Mouse(0, 0, SharpConsoleUI.Drivers.MouseFlags.Button1Dragged));
		Assert.Equal(afterRelease, panel.VerticalScrollOffset);
	}

	[Fact]
	public void Scrollbar_NotShown_WhenContentFits_TrackClickDoesNothing()
	{
		var panel = new ScrollablePanelControl { Height = 20 };
		panel.AddControl(Wide("only"));
		var (_, _) = Render(panel);

		int sbX = panel.ViewportWidth - 1;
		panel.ProcessMouseEvent(Mouse(sbX, 5, SharpConsoleUI.Drivers.MouseFlags.Button1Clicked));
		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	#endregion

	#region Thread safety (background invalidation)

	[Fact]
	public void Invalidate_FromBackgroundThread_DoesNotThrow()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 10; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		// Container?.Invalidate(Invalidation.Relayout) is documented as the one thread-safe call from a bg thread.
		var ex = Record.Exception(() =>
		{
			var tasks = Enumerable.Range(0, 8).Select(_ => System.Threading.Tasks.Task.Run(() =>
			{
				for (int i = 0; i < 50; i++) panel.Invalidate(Invalidation.Relayout);
			})).ToArray();
			System.Threading.Tasks.Task.WaitAll(tasks);
		});
		Assert.Null(ex);
	}

	[Fact]
	public void Children_Snapshot_StableDuringConcurrentReads()
	{
		// The Children getter returns a snapshot copy; reading it while iterating must not throw.
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 20; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		var ex = Record.Exception(() =>
		{
			var readers = Enumerable.Range(0, 8).Select(_ => System.Threading.Tasks.Task.Run(() =>
			{
				for (int i = 0; i < 100; i++)
				{
					foreach (var c in panel.Children) { var v = c.Visible; }
				}
			})).ToArray();
			System.Threading.Tasks.Task.WaitAll(readers);
		});
		Assert.Null(ex);
	}

	#endregion

	#region Focus edges

	[Fact]
	public void RemoveFocusedChild_ClearsFocusFromIt()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		var a = ContainerTestHelpers.CreateFocusableList("a");
		var b = ContainerTestHelpers.CreateFocusableList("b");
		panel.AddControl(a);
		panel.AddControl(b);
		var (_, window) = Render(panel);

		window.FocusManager.SetFocus(a, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();
		Assert.True(a.HasFocus);

		panel.RemoveControl(a);
		window.RenderAndGetVisibleContent();
		Assert.False(a.HasFocus, "Removing the focused child clears its focus.");
	}

	[Fact]
	public void ClearContents_WhileChildFocused_RetainsFocusOnPanel()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		var a = ContainerTestHelpers.CreateFocusableList("a");
		panel.AddControl(a);
		var (_, window) = Render(panel);
		window.FocusManager.SetFocus(a, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var ex = Record.Exception(() => panel.ClearContents());
		Assert.Null(ex);
		Assert.Empty(panel.Children);
	}

	#endregion

	#region Nested ScrollablePanel

	[Fact]
	public void NestedPanel_OuterHasScrollRange_WhenContentIncludingInnerExceedsViewport()
	{
		var inner = new ScrollablePanelControl { Height = 4 };
		for (int i = 0; i < 6; i++) inner.AddControl(Wide($"inner{i}"));

		var outer = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 6; i++) outer.AddControl(Wide($"outer{i}"));
		outer.AddControl(inner);

		var (_, _) = Render(outer);
		// Outer content = 6 fixed rows + inner panel's own height (4) = 10 > viewport 8.
		Assert.Equal(10, outer.TotalContentHeight);
		Assert.True(outer.TotalContentHeight > outer.ViewportHeight, "Outer content (incl. inner panel) exceeds the viewport.");
	}

	[Fact]
	public void NestedPanel_InnerScrollsIndependentlyOfOuter()
	{
		var inner = new ScrollablePanelControl { Height = 4 };
		for (int i = 0; i < 12; i++) inner.AddControl(Wide($"inner{i}"));

		var outer = new ScrollablePanelControl { Height = 20 }; // outer fits everything
		outer.AddControl(inner);

		var (_, _) = Render(outer);
		Assert.False(outer.CanScrollDown, "Outer fits its content; no outer scroll.");
		Assert.True(inner.CanScrollDown, "Inner overflows its own 4-row viewport.");

		inner.ScrollVerticalBy(3);
		Assert.Equal(3, inner.VerticalScrollOffset);
		Assert.Equal(0, outer.VerticalScrollOffset); // inner scroll does not move outer
	}

	#endregion
}
