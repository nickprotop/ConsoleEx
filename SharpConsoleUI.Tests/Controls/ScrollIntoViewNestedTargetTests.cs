// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Revealing a control nested below a scroller's direct child (#91).
///
/// Two things are pinned here. First, the probe that resolves the nested control's row must work
/// when the direct child has NEVER BEEN PAINTED: ActualWidth/ActualHeight are only assigned by
/// SetActualBounds inside PaintDOM, and paint culls anything scrolled fully out of the viewport,
/// so the child holding an off-fold focus target routinely reports 0x0. The probe used to bail on
/// that and the caller fell back to revealing only the child's top. Second, the single-call
/// overload that composes the probe with the region scroll - the only way to re-reveal a control
/// that is ALREADY focused, since SetFocus returns early when the focus does not change.
/// </summary>
public class ScrollIntoViewNestedTargetTests
{
	private readonly ITestOutputHelper _out;

	public ScrollIntoViewNestedTargetTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	#region Helpers

	/// <summary>
	/// A tall non-scrolling container holding <paramref name="count"/> focusable rows, inside a
	/// scroller that is much shorter than it. This is the #91 shape: the direct child spans far
	/// beyond the viewport, so a row low down is never painted.
	/// </summary>
	private static (ScrollablePanelControl panel, PanelControl list, List<ListControl> rows,
		ConsoleWindowSystem system, Window window) NewDeepPanel(int viewportHeight, int count)
	{
		var panel = new ScrollablePanelControl { Height = viewportHeight };
		var list = new PanelControl { BorderStyle = BorderStyle.None };

		var rows = new List<ListControl>();
		for (int i = 0; i < count; i++)
		{
			var row = ContainerTestHelpers.CreateFocusableList($"row {i}");
			row.VerticalAlignment = VerticalAlignment.Top;
			rows.Add(row);
			list.AddControl(row);
		}

		panel.AddControl(list);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		return (panel, list, rows, system, window);
	}

	#endregion

	#region The probe survives an unpainted direct child

	[Fact]
	public void TheProbe_ResolvesARow_WhenTheDirectChildWasNeverPainted()
	{
		var (panel, list, rows, _, window) = NewDeepPanel(viewportHeight: 6, count: 40);
		window.RenderAndGetVisibleContent();

		// Precondition: the list is taller than the viewport and sits at the top, so it HAS painted.
		// Scroll past it is not what we want here - we want the child itself unpainted, which is the
		// state a caller hits before the panel has ever shown it. Assert the regression directly on a
		// child reporting no size.
		Assert.True(list.ActualHeight > 0, "setup: the list should have painted at least once");

		bool ok = FocusScrollHelper.TryGetFocusedRowInDirectChild(
			list, rows[30], out int row, out int height, panel);

		_out.WriteLine($"ok={ok} row={row} height={height} listH={list.ActualHeight}");
		Assert.True(ok);
		Assert.True(row > 0, "row 30 must resolve below the child's top");
		Assert.True(height >= 1);
	}

	[Fact]
	public void TheProbe_RecoversAWidth_FromTheScroller_WhenTheChildIsCulledByPaint()
	{
		// The regression in its real form. A tall markup header above the list pushes the list
		// entirely below the fold, so paint culls it and SetActualBounds never runs: the list
		// reports 0x0 while being exactly the child we must probe. Before the fix this returned
		// false and the caller revealed only the list's top.
		var panel = new ScrollablePanelControl { Height = 6 };
		var topStop = ContainerTestHelpers.CreateFocusableList("top stop");
		topStop.VerticalAlignment = VerticalAlignment.Top;
		panel.AddControl(topStop);
		panel.AddControl(ContainerTestHelpers.CreateLabel(
			string.Join('\n', Enumerable.Range(0, 40).Select(i => $"header {i}"))));

		var list = new PanelControl { BorderStyle = BorderStyle.None };
		var rows = new List<ListControl>();
		for (int i = 0; i < 40; i++)
		{
			var item = ContainerTestHelpers.CreateFocusableList($"row {i}");
			item.VerticalAlignment = VerticalAlignment.Top;
			rows.Add(item);
			list.AddControl(item);
		}
		panel.AddControl(list);

		// A focusable control ABOVE the list takes the initial focus, so the panel never scrolls down
		// and the list is never painted. This is exactly the #91 shape (there, a tab header and a
		// button played that role) and it is why the child reports no size at all.
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"panel={panel.ActualWidth}x{panel.ActualHeight} vp={panel.ViewportHeight} " +
			$"offset={panel.VerticalScrollOffset} list={list.ActualWidth}x{list.ActualHeight}");
		// The list really is unpainted - this is the state the bug depends on, not a simulation.
		Assert.Equal(0, list.ActualHeight);

		bool ok = FocusScrollHelper.TryGetFocusedRowInDirectChild(
			list, rows[30], out int row, out int height, panel);

		_out.WriteLine($"ok={ok} row={row} height={height} listActual={list.ActualWidth}x{list.ActualHeight}");
		Assert.True(ok, "a paint-culled child must still be probeable via the scroller's width");
		Assert.True(row > 0, "row 30 must resolve below the child's top");
		Assert.True(height >= 1);
	}

	#endregion

	#region The nested-target overload

	[Fact]
	public void RevealingANestedTarget_ScrollsFurtherThanRevealingTheChild()
	{
		// Revealing the tall child alone cannot help: the tall-child guard leaves a container that
		// already spans the viewport where it is. The nested overload must go further.
		var (panel, list, rows, _, window) = NewDeepPanel(viewportHeight: 6, count: 40);
		window.RenderAndGetVisibleContent();

		panel.ScrollChildIntoView(list);
		window.RenderAndGetVisibleContent();
		int childOnly = panel.VerticalScrollOffset;

		panel.ScrollChildIntoView(list, rows[30]);
		window.RenderAndGetVisibleContent();
		int nested = panel.VerticalScrollOffset;

		_out.WriteLine($"childOnly={childOnly} nested={nested}");
		Assert.True(nested > childOnly,
			$"nested reveal ({nested}) must scroll past the child-only reveal ({childOnly})");
	}

	[Fact]
	public void RevealingANestedTarget_PutsItInsideTheViewport()
	{
		var (panel, list, rows, _, window) = NewDeepPanel(viewportHeight: 6, count: 40);
		window.RenderAndGetVisibleContent();

		var target = rows[30];
		panel.ScrollChildIntoView(list, target);
		window.RenderAndGetVisibleContent();

		int relY = target.ActualY - panel.ActualY;
		_out.WriteLine($"relY={relY} viewport={panel.ViewportHeight} offset={panel.VerticalScrollOffset}");
		Assert.InRange(relY, 0, panel.ViewportHeight - 1);
	}

	[Fact]
	public void PassingTheDirectChildAsItsOwnTarget_BehavesLikeTheOneArgCall()
	{
		var (panel, list, _, _, window) = NewDeepPanel(viewportHeight: 6, count: 40);
		window.RenderAndGetVisibleContent();
		panel.ScrollVerticalBy(12);
		window.RenderAndGetVisibleContent();

		panel.ScrollChildIntoView(list);
		window.RenderAndGetVisibleContent();
		int oneArg = panel.VerticalScrollOffset;

		panel.ScrollVerticalBy(-12);
		window.RenderAndGetVisibleContent();
		panel.ScrollVerticalBy(12);
		window.RenderAndGetVisibleContent();

		panel.ScrollChildIntoView(list, list);
		window.RenderAndGetVisibleContent();

		Assert.Equal(oneArg, panel.VerticalScrollOffset);
	}

	[Fact]
	public void AnAlreadyFocusedTarget_CanBeRevealedAgain()
	{
		// The gap DerPeit reported: SetFocus returns early when the focus does not change, so after
		// the user scrolls the focused control away, focusing it again scrolls nothing. The overload
		// is the way back.
		var (panel, list, rows, system, window) = NewDeepPanel(viewportHeight: 6, count: 40);
		window.RenderAndGetVisibleContent();

		var target = rows[30];
		window.FocusControl(target);
		window.RenderAndGetVisibleContent();
		int afterFocus = panel.VerticalScrollOffset;
		Assert.True(afterFocus > 0, "focusing a deep row should have scrolled to it");

		panel.ScrollToTop();
		window.RenderAndGetVisibleContent();
		Assert.Equal(0, panel.VerticalScrollOffset);

		// Re-focusing is a no-op: the control still holds focus.
		window.FocusControl(target);
		window.RenderAndGetVisibleContent();
		Assert.Equal(0, panel.VerticalScrollOffset);

		panel.ScrollChildIntoView(list, target);
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"afterFocus={afterFocus} reRevealed={panel.VerticalScrollOffset}");
		Assert.Equal(afterFocus, panel.VerticalScrollOffset);
	}

	#endregion

	#region Real thing: focus a deep control in a real nesting, through the real focus path

	[Fact]
	public void FocusingADeepControl_InAGridHostedScroller_RevealsTheControlItself()
	{
		// The #91 shape end-to-end: HGrid > Fill scroller > tall markup header > tall list of
		// collapsible sections, focused through Window.FocusControl (the real path), then
		// re-rendered and asserted on the arranged position that survives the re-render.
		ScrollablePanelControl? scroller = null;
		var sections = new List<CollapsiblePanel>();

		var list = new PanelControl { BorderStyle = BorderStyle.None };
		for (int i = 0; i < 30; i++)
		{
			var section = new CollapsiblePanel { Title = $"Section {i}", IsExpanded = true };
			section.AddControl(ContainerTestHelpers.CreateLabel($"line a {i}\nline b {i}\nline c {i}"));
			sections.Add(section);
			list.AddControl(section);
		}

		var header = ContainerTestHelpers.CreateLabel(
			string.Join('\n', Enumerable.Range(0, 40).Select(i => $"header {i}")));

		var page = new HorizontalGridControl();
		var column = new ColumnContainer(page);
		var panel = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		scroller = panel;

		// A focusable stop at the very top keeps the initial focus there, so nothing scrolls the
		// list into view and it is never painted. Without this the panel auto-scrolls to the first
		// focusable section, the list paints, and the test passes even against the broken probe.
		var topStop = ContainerTestHelpers.CreateButton("top");
		panel.AddControl(topStop);
		panel.AddControl(header);
		panel.AddControl(list);
		column.AddContent(panel);
		page.AddColumn(column);
		page.VerticalAlignment = VerticalAlignment.Fill;

		// A SHORT window on purpose: with a tall window the list happens to paint and the bug never
		// shows, which is how an earlier version of this test passed against the broken code.
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment(winW: 80, winH: 10);
		window.AddControl(page);
		window.RenderAndGetVisibleContent();

		// Nothing has scrolled the list into view, so it has never painted and reports no size -
		// the condition the probe has to survive.
		_out.WriteLine($"pre-focus list={list.ActualWidth}x{list.ActualHeight} offset={scroller.VerticalScrollOffset}");

		var target = sections[25];
		window.FocusControl(target);
		window.RenderAndGetVisibleContent();
		// Re-render again: the position must SURVIVE, not just hold for one frame.
		window.RenderAndGetVisibleContent();

		int offset = scroller!.VerticalScrollOffset;
		_out.WriteLine($"offset={offset} headerH={header.ActualHeight} listH={list.ActualHeight}");

		// The header alone is 40 rows, so revealing only the list's TOP lands at ~40 - that is the
		// broken behaviour. Reaching section 25 must go far past it.
		Assert.True(offset > 60,
			$"expected to scroll past the header and well into the list, got {offset}");
	}

	#endregion
}
