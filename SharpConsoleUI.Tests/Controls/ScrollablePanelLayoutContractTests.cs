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
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Characterization tests that PIN the layout/measure/hit-test contract of
/// ScrollablePanelControl. SPC re-implements layout for its children (they are not
/// part of the window's persistent layout tree), computing each child's effective
/// height independently in PaintDOM, MeasureChildrenHeight, MeasureChildHeight and in
/// every hit-test/scroll walk. These tests lock in the observable behavior — chiefly
/// that hit-testing agrees with painting — so a future ScrollLayout refactor can be
/// verified behavior-preserving for the external NuGet consumers of this control.
/// </summary>
public class ScrollablePanelLayoutContractTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelLayoutContractTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	/// <summary>
	/// Hit-testing must route a click to the same child the paint pass drew at that Y.
	/// This is the central invariant the duplicated measure paths can violate.
	/// </summary>
	[Fact]
	public void ClickRouting_AgreesWithPaint_FillChildBelowFixedChild()
	{
		// Borderless, no-margin panel so window-Y maps directly to content-Y.
		var panel = new ScrollablePanelControl { Height = 20 };

		var topButton = ContainerTestHelpers.CreateButton("top");   // fixed, focusable
		var fillList = ContainerTestHelpers.CreateFocusableList("a", "b", "c"); // Fill, focusable
		fillList.VerticalAlignment = VerticalAlignment.Fill;
		panel.AddControl(topButton);
		panel.AddControl(fillList);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Click on the row well below the fixed top button — that area is painted by the
		// Fill list. The list (not the button) must receive focus.
		int clickY = topButton.ActualHeight + 3;
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, clickY));

		_out.WriteLine($"topButton.ActualHeight={topButton.ActualHeight} clickY={clickY} " +
			$"list.HasFocus={fillList.HasFocus} button.HasFocus={topButton.HasFocus}");

		Assert.True(fillList.HasFocus,
			$"Click at content-Y {clickY} lands in the Fill list's painted area; the list should " +
			$"receive focus, but it didn't (button.HasFocus={topButton.HasFocus}). " +
			$"Hit-test disagrees with paint.");
		Assert.False(topButton.HasFocus, "The fixed top button should not have received the click.");
	}

	/// <summary>
	/// A click on the fixed child at the top must route to it, not the Fill child.
	/// </summary>
	[Fact]
	public void ClickRouting_FixedChildAtTop_ReceivesClick()
	{
		var panel = new ScrollablePanelControl { Height = 20 };
		var topButton = ContainerTestHelpers.CreateButton("top");
		var fillList = ContainerTestHelpers.CreateFocusableList("a", "b", "c");
		fillList.VerticalAlignment = VerticalAlignment.Fill;
		panel.AddControl(topButton);
		panel.AddControl(fillList);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));

		Assert.True(topButton.HasFocus, "Click at Y=0 should route to the fixed top button.");
	}

	/// <summary>
	/// When content overflows the viewport, hit-testing must account for the scroll
	/// offset: after scrolling, a click routes to whichever child is now under the cursor.
	/// </summary>
	[Fact]
	public void ClickRouting_RespectsScrollOffset_WhenContentOverflows()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		// Many fixed focusable children so content (20 rows) clearly exceeds the 8-row
		// viewport and scrolling is actually possible.
		var lists = new List<ListControl>();
		for (int i = 0; i < 20; i++)
		{
			var l = ContainerTestHelpers.CreateFocusableList($"item{i}");
			lists.Add(l);
			panel.AddControl(l);
		}

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Scroll down so the first child(ren) move above the viewport.
		panel.ScrollVerticalBy(3);
		window.RenderAndGetVisibleContent();

		// A click at content-Y = 3 (top of viewport + scroll) lands on the child at
		// absolute index 3 (each list is 1 row tall).
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));

		_out.WriteLine("focused: " + string.Join(",", lists.Select((l, i) => l.HasFocus ? i.ToString() : "")));

		// The child at scroll offset 3 should be the one focused (index 3), not index 0.
		Assert.True(lists[3].HasFocus,
			"After scrolling by 3, a click at the top of the viewport should route to child #3.");
		Assert.False(lists[0].HasFocus, "Child #0 is scrolled out of view and must not receive the click.");
	}
}
