// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Characterization tests that PIN the layout / measure / hit-test / scroll contract of
/// ScrollablePanelControl (SPC).
///
/// SPC is a self-painting container: its children are NOT part of the window's persistent
/// layout tree, so SPC re-derives each child's vertical slot (top + height) itself. That slot
/// must be computed identically everywhere it is consumed — paint, hit-testing, coordinate
/// translation and scroll-into-view — or clicks land on the wrong child, coordinates desync,
/// and scroll jumps to the wrong place.
///
/// These tests lock that behavior in so the planned `ScrollLayout : ILayoutContainer` refactor
/// (which would make SPC children real layout nodes) can be verified behavior-preserving for
/// the external NuGet consumers of this control.
/// </summary>
public class ScrollablePanelLayoutContractTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelLayoutContractTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	#region Helpers

	/// <summary>A borderless, no-margin panel so window-Y maps directly to content-Y.</summary>
	private static (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window) NewPanel(int height)
	{
		var panel = new ScrollablePanelControl { Height = height };
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		return (panel, system, window);
	}

	private static ListControl FocusableRow(string label, VerticalAlignment va = VerticalAlignment.Top)
	{
		var l = ContainerTestHelpers.CreateFocusableList(label);
		l.VerticalAlignment = va;
		return l;
	}

	#endregion

	#region Click routing == paint (the central invariant)

	[Fact]
	public void ClickRouting_AgreesWithPaint_FillChildBelowFixedChild()
	{
		var (panel, _, window) = NewPanel(20);
		var topButton = ContainerTestHelpers.CreateButton("top");
		var fillList = FocusableRow("a", VerticalAlignment.Fill);
		panel.AddControl(topButton);
		panel.AddControl(fillList);
		window.RenderAndGetVisibleContent();

		int clickY = topButton.ActualHeight + 3; // inside the Fill child's painted area
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, clickY));

		_out.WriteLine($"topH={topButton.ActualHeight} clickY={clickY} list={fillList.HasFocus} btn={topButton.HasFocus}");
		Assert.True(fillList.HasFocus, "Click in the Fill child's painted area must route to it.");
		Assert.False(topButton.HasFocus);
	}

	[Fact]
	public void ClickRouting_FixedChildAtTop_ReceivesClick()
	{
		var (panel, _, window) = NewPanel(20);
		var topButton = ContainerTestHelpers.CreateButton("top");
		var fillList = FocusableRow("a", VerticalAlignment.Fill);
		panel.AddControl(topButton);
		panel.AddControl(fillList);
		window.RenderAndGetVisibleContent();

		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));
		Assert.True(topButton.HasFocus, "Click at Y=0 routes to the fixed top child.");
	}

	[Fact]
	public void ClickRouting_StackedFixedChildren_EachReceivesItsOwnRow()
	{
		var (panel, _, window) = NewPanel(20);
		var rows = new List<ListControl>();
		for (int i = 0; i < 5; i++) { var r = FocusableRow($"row{i}"); rows.Add(r); panel.AddControl(r); }
		window.RenderAndGetVisibleContent();

		// Each single-line list occupies one content row. Click each and verify routing.
		for (int i = 0; i < rows.Count; i++)
		{
			panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, i));
			Assert.True(rows[i].HasFocus, $"Click at Y={i} should focus row {i}.");
		}
	}

	[Fact]
	public void ClickRouting_RespectsScrollOffset_WhenContentOverflows()
	{
		var (panel, _, window) = NewPanel(8);
		var rows = new List<ListControl>();
		for (int i = 0; i < 20; i++) { var r = FocusableRow($"item{i}"); rows.Add(r); panel.AddControl(r); }
		window.RenderAndGetVisibleContent();

		panel.ScrollVerticalBy(3);
		window.RenderAndGetVisibleContent();

		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));
		Assert.True(rows[3].HasFocus, "After scrolling 3, top-of-viewport click routes to child #3.");
		Assert.False(rows[0].HasFocus, "Scrolled-out child #0 must not receive the click.");
	}

	[Fact]
	public void ClickRouting_BelowAllChildren_HitsNothing()
	{
		var (panel, _, window) = NewPanel(20);
		var a = FocusableRow("a");
		var b = FocusableRow("b");
		panel.AddControl(a);
		panel.AddControl(b);
		window.RenderAndGetVisibleContent();

		// Two single-row children occupy Y=0,1. A click at Y=10 is empty content.
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 10));
		Assert.False(a.HasFocus);
		Assert.False(b.HasFocus);
	}

	#endregion

	#region Child slot layout (GetVisibleChildLayout source of truth)

	[Fact]
	public void ChildLayout_FixedChildren_StackContiguouslyByContentHeight()
	{
		var (panel, _, window) = NewPanel(20);
		var top = FocusableRow("top");
		var bottom = FocusableRow("bottom");
		panel.AddControl(top);
		panel.AddControl(bottom);
		window.RenderAndGetVisibleContent();

		// Painted bounds: the two rows are adjacent (each 1 tall), no gap.
		Assert.Equal(top.ActualY + top.ActualHeight, bottom.ActualY);
	}

	[Fact]
	public void ChildLayout_FillChild_ExpandsToFillRemainingViewport()
	{
		var (panel, _, window) = NewPanel(15);
		var fixedTop = FocusableRow("top");
		var fill = FocusableRow("fill", VerticalAlignment.Fill);
		panel.AddControl(fixedTop);
		panel.AddControl(fill);
		window.RenderAndGetVisibleContent();

		// Fill child should occupy (viewport - fixed) rows, far more than its 1-row content.
		Assert.True(fill.ActualHeight >= panel.ViewportHeight - fixedTop.ActualHeight - 1,
			$"Fill child height {fill.ActualHeight} should ~= viewport {panel.ViewportHeight} - fixed {fixedTop.ActualHeight}.");
	}

	[Fact]
	public void ChildLayout_TwoFillChildren_SplitRemainingSpaceRoughlyEvenly()
	{
		var (panel, _, window) = NewPanel(20);
		var fixedTop = FocusableRow("top");
		var fillA = FocusableRow("A", VerticalAlignment.Fill);
		var fillB = FocusableRow("B", VerticalAlignment.Fill);
		panel.AddControl(fixedTop);
		panel.AddControl(fillA);
		panel.AddControl(fillB);
		window.RenderAndGetVisibleContent();

		// Two Fill children share the remaining space; their heights differ by at most 1
		// (integer division remainder).
		Assert.True(System.Math.Abs(fillA.ActualHeight - fillB.ActualHeight) <= 1,
			$"Two Fill children should split evenly: A={fillA.ActualHeight} B={fillB.ActualHeight}.");
		Assert.True(fillA.ActualHeight > 1 && fillB.ActualHeight > 1, "Both Fill children should expand.");
	}

	[Fact]
	public void ChildLayout_ContentSizedChild_TallerThanViewport_KeepsFullHeight_NotClamped()
	{
		// A content-sized child taller than the viewport must report its full height so the
		// panel can scroll through it (it must NOT be clamped to the viewport).
		var (panel, _, window) = NewPanel(5);
		var big = new MarkupControl(Enumerable.Range(0, 12).Select(i => $"line {i}").ToList());
		panel.AddControl(big);
		window.RenderAndGetVisibleContent();

		Assert.True(panel.CanScrollDown, "A child taller than the viewport must create scroll range.");
		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			$"Content {panel.TotalContentHeight} should exceed viewport {panel.ViewportHeight}.");
	}

	#endregion

	#region Mouse-event coordinate translation

	[Fact]
	public void MouseForward_TranslatesYRelativeToChild_BelowFixedSibling()
	{
		var (panel, _, window) = NewPanel(20);
		var spacer = FocusableRow("spacer");           // 1 row at Y=0
		var target = new ListControl(new[] { "x", "y", "z" }); // multi-row, mouse-aware
		panel.AddControl(spacer);
		panel.AddControl(target);
		window.RenderAndGetVisibleContent();

		int? receivedY = null;
		target.MouseClick += (_, e) => receivedY = e.Position.Y;

		// Click on the 2nd visible row of the target. target starts at content-Y=1, so its
		// own row index 1 is at content-Y=2 → child-relative Y must be 1.
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 2));

		_out.WriteLine($"receivedY={receivedY}");
		Assert.True(receivedY.HasValue, "Target should have received the forwarded click.");
		Assert.Equal(1, receivedY!.Value);
	}

	[Fact]
	public void MouseForward_TranslatesY_WithScrollOffsetApplied()
	{
		var (panel, _, window) = NewPanel(6);
		// Fixed spacer + a tall target list, content overflows so we can scroll.
		var spacer = new MarkupControl(new List<string> { "s0", "s1", "s2" }); // 3 rows
		var target = new ListControl(Enumerable.Range(0, 10).Select(i => $"r{i}").ToArray());
		panel.AddControl(spacer);
		panel.AddControl(target);
		window.RenderAndGetVisibleContent();

		panel.ScrollVerticalBy(4); // scroll so spacer is partly/fully above viewport
		window.RenderAndGetVisibleContent();

		int? receivedY = null;
		target.MouseClick += (_, e) => receivedY = e.Position.Y;

		// Click at viewport top (Y=0). content-Y = 0 + scrollOffset(4) = 4. target starts at
		// content-Y=3, so child-relative Y = 4 - 3 = 1.
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));

		_out.WriteLine($"scroll-translated receivedY={receivedY}");
		Assert.True(receivedY.HasValue, "Target should receive the click after scrolling.");
		Assert.Equal(1, receivedY!.Value);
	}

	#endregion

	#region Scroll-into-view uses the same layout

	[Fact]
	public void ScrollIntoView_ChildBelowViewport_ScrollsSoItIsVisible()
	{
		var (panel, _, window) = NewPanel(5);
		var rows = new List<ListControl>();
		for (int i = 0; i < 15; i++) { var r = FocusableRow($"r{i}"); rows.Add(r); panel.AddControl(r); }
		window.RenderAndGetVisibleContent();

		var target = rows[12]; // well below the 5-row viewport
		panel.ScrollChildIntoView(target);
		window.RenderAndGetVisibleContent();

		// After scroll-into-view, the target's painted Y must be within the viewport.
		int relY = target.ActualY - panel.ActualY; // panel-relative
		_out.WriteLine($"target.ActualY={target.ActualY} panel.ActualY={panel.ActualY} relY={relY} viewport={panel.ViewportHeight}");
		Assert.InRange(relY, 0, panel.ViewportHeight - 1);
	}

	[Fact]
	public void ScrollIntoView_ChildAboveViewport_ScrollsUpToShowIt()
	{
		var (panel, _, window) = NewPanel(5);
		var rows = new List<ListControl>();
		for (int i = 0; i < 15; i++) { var r = FocusableRow($"r{i}"); rows.Add(r); panel.AddControl(r); }
		window.RenderAndGetVisibleContent();

		panel.ScrollVerticalBy(10); // push the early rows above the viewport
		window.RenderAndGetVisibleContent();

		var target = rows[1];
		panel.ScrollChildIntoView(target);
		window.RenderAndGetVisibleContent();

		int relY = target.ActualY - panel.ActualY;
		Assert.InRange(relY, 0, panel.ViewportHeight - 1);
	}

	[Fact]
	public void ScrollIntoView_AlreadyVisibleChild_DoesNotScroll()
	{
		var (panel, _, window) = NewPanel(10);
		var rows = new List<ListControl>();
		for (int i = 0; i < 20; i++) { var r = FocusableRow($"r{i}"); rows.Add(r); panel.AddControl(r); }
		window.RenderAndGetVisibleContent();
		panel.ScrollVerticalBy(5);
		window.RenderAndGetVisibleContent();

		int before = panel.VerticalScrollOffset;
		// rows[6] is visible (viewport shows content rows 5..14 after scrolling 5).
		panel.ScrollChildIntoView(rows[6]);
		Assert.Equal(before, panel.VerticalScrollOffset);
	}

	#endregion

	#region Scroll clamping & range

	[Fact]
	public void ContentFitsViewport_NoScrollRange()
	{
		var (panel, _, window) = NewPanel(20);
		panel.AddControl(FocusableRow("only"));
		window.RenderAndGetVisibleContent();

		Assert.False(panel.CanScrollUp);
		Assert.False(panel.CanScrollDown);
	}

	[Fact]
	public void ScrollOffset_ClampsWhenContentShrinks()
	{
		var (panel, _, window) = NewPanel(5);
		var rows = new List<ListControl>();
		for (int i = 0; i < 20; i++) { var r = FocusableRow($"r{i}"); rows.Add(r); panel.AddControl(r); }
		window.RenderAndGetVisibleContent();
		panel.ScrollVerticalBy(15);
		window.RenderAndGetVisibleContent();
		int deepOffset = panel.VerticalScrollOffset;
		Assert.True(deepOffset > 0);

		// Remove most children so content now fits; offset must clamp back to 0 on next paint.
		for (int i = 19; i >= 2; i--) panel.RemoveControl(rows[i]);
		window.RenderAndGetVisibleContent();

		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	#endregion

	#region Invisible children

	[Fact]
	public void InvisibleChild_OccupiesNoSlot_FollowingChildMovesUp()
	{
		var (panel, _, window) = NewPanel(20);
		var a = FocusableRow("a");
		var hidden = FocusableRow("hidden");
		var c = FocusableRow("c");
		panel.AddControl(a);
		panel.AddControl(hidden);
		panel.AddControl(c);
		hidden.Visible = false;
		window.RenderAndGetVisibleContent();

		// 'c' should sit directly under 'a' since 'hidden' takes no space.
		Assert.Equal(a.ActualY + a.ActualHeight, c.ActualY);
	}

	[Fact]
	public void InvisibleChild_DoesNotReceiveClicks()
	{
		var (panel, _, window) = NewPanel(20);
		var a = FocusableRow("a");
		var hidden = FocusableRow("hidden");
		panel.AddControl(a);
		panel.AddControl(hidden);
		hidden.Visible = false;
		window.RenderAndGetVisibleContent();

		// 'a' occupies Y=0. Y=1 would have been 'hidden' if visible; it must hit nothing.
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 1));
		Assert.False(hidden.HasFocus);
	}

	#endregion

	#region Audit desync probes (DESYNC #1 measure-vs-paint, #4 cursor staleness)

	/// <summary>
	/// DESYNC #1: a content-sized panel (no explicit Height) whose ONLY child is a Fill child,
	/// hosted in a bounded Fill TabControl, must report a content height equal to the space the
	/// Fill child is actually painted into — not a degenerate value. Probes that the panel's own
	/// MeasureDOM/content-height path agrees with the paint path for Fill children.
	/// </summary>
	[Fact]
	public void ContentSizedPanel_WithFillChild_ContentHeightMatchesPaintedChildHeight()
	{
		var fillList = FocusableRow("only", VerticalAlignment.Fill);
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().Build(); // no explicit Height
		panel.AddControl(fillList);

		var tabs = SharpConsoleUI.Builders.Controls.TabControl().Fill().Build();
		tabs.AddTab("t", panel);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(tabs);
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"viewport={panel.ViewportHeight} content={panel.TotalContentHeight} childH={fillList.ActualHeight}");

		// The Fill child fills the viewport; the panel's measured content height must equal the
		// child's painted height (the single child). They must not disagree.
		Assert.Equal(fillList.ActualHeight, panel.TotalContentHeight);
	}

	/// <summary>
	/// DESYNC #4: GetLogicalCursorPosition reads the focused child's ActualY (set during paint)
	/// and subtracts the scroll offset. After scrolling a focused, actively-editing child into
	/// view and rendering, the reported cursor must land inside the viewport — not off-screen due
	/// to a stale ActualY.
	/// </summary>
	[Fact]
	public void LogicalCursorPosition_ReflectsScrollOffset_AfterRender()
	{
		var (panel, _, window) = NewPanel(5);
		// Fixed spacer rows push the editor below the 5-row viewport so a scroll is required.
		for (int i = 0; i < 8; i++) panel.AddControl(FocusableRow($"s{i}"));
		var edit = new MultilineEditControl { ViewportHeight = 3, IsEditing = true };
		edit.SetContent("hello");
		panel.AddControl(edit);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(edit, FocusReason.Programmatic);
		panel.ScrollChildIntoView(edit);
		window.RenderAndGetVisibleContent();

		var pos = panel.GetLogicalCursorPosition();
		_out.WriteLine($"cursor={pos} editActualY={edit.ActualY} panelActualY={panel.ActualY} scroll={panel.VerticalScrollOffset} viewport={panel.ViewportHeight}");

		Assert.True(pos.HasValue, "Focused, editing child should yield a logical cursor position.");
		// Cursor Y is panel-relative (content space minus scroll). The editor was scrolled into
		// view, so the cursor must be within the viewport rows.
		Assert.InRange(pos!.Value.Y, 0, panel.ViewportHeight - 1);
	}

	#endregion
}
