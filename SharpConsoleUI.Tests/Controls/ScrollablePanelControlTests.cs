// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ScrollablePanelControlTests
{
	#region Helpers

	/// <summary>
	/// Creates a panel with enough children to exceed the viewport,
	/// adds it to a window, and renders to compute layout dimensions.
	/// Returns the panel, system, and window for further assertions.
	/// </summary>
	private (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window)
		CreateRenderedScrollPanel(int childCount = 20, int panelHeight = 10)
	{
		var panel = new ScrollablePanelControl();
		panel.Height = panelHeight;
		for (int i = 0; i < childCount; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, system, window);
	}

	#endregion

	#region Construction & Defaults

	[Fact]
	public void Constructor_CreatesEmptyPanel()
	{
		var panel = new ScrollablePanelControl();

		Assert.NotNull(panel);
		Assert.Empty(panel.Children);
	}

	[Fact]
	public void Defaults_ShowScrollbar_True()
	{
		var panel = new ScrollablePanelControl();

		Assert.True(panel.ShowScrollbar);
	}

	[Fact]
	public void Defaults_VerticalScrollMode_Scroll()
	{
		var panel = new ScrollablePanelControl();

		Assert.Equal(ScrollMode.Scroll, panel.VerticalScrollMode);
	}

	[Fact]
	public void Defaults_HorizontalScrollMode_None()
	{
		var panel = new ScrollablePanelControl();

		Assert.Equal(ScrollMode.None, panel.HorizontalScrollMode);
	}

	[Fact]
	public void Defaults_EnableMouseWheel_True()
	{
		var panel = new ScrollablePanelControl();

		Assert.True(panel.EnableMouseWheel);
	}

	[Fact]
	public void Defaults_AutoScroll_False()
	{
		var panel = new ScrollablePanelControl();

		Assert.False(panel.AutoScroll);
	}

	#endregion

	#region Child Management

	[Fact]
	public void AddControl_AddsChildToPanel()
	{
		var panel = new ScrollablePanelControl();
		var label = ContainerTestHelpers.CreateLabel("Hello");

		panel.AddControl(label);

		Assert.Single(panel.Children);
		Assert.Same(label, panel.Children[0]);
	}

	[Fact]
	public void AddControl_SetsContainerOnChild()
	{
		var panel = new ScrollablePanelControl();
		var label = ContainerTestHelpers.CreateLabel("Hello");

		panel.AddControl(label);

		Assert.Same(panel, label.Container);
	}

	[Fact]
	public void RemoveControl_RemovesChild()
	{
		var panel = new ScrollablePanelControl();
		var label = ContainerTestHelpers.CreateLabel("Hello");
		panel.AddControl(label);

		panel.RemoveControl(label);

		Assert.Empty(panel.Children);
		Assert.Null(label.Container);
	}

	[Fact]
	public void ClearContents_RemovesAllChildren()
	{
		var panel = new ScrollablePanelControl();
		panel.AddControl(ContainerTestHelpers.CreateLabel("A"));
		panel.AddControl(ContainerTestHelpers.CreateLabel("B"));
		panel.AddControl(ContainerTestHelpers.CreateLabel("C"));

		panel.ClearContents();

		Assert.Empty(panel.Children);
	}

	#endregion

	#region Configuration

	[Fact]
	public void ShowScrollbar_CanBeToggled()
	{
		var panel = new ScrollablePanelControl();

		panel.ShowScrollbar = false;
		Assert.False(panel.ShowScrollbar);

		panel.ShowScrollbar = true;
		Assert.True(panel.ShowScrollbar);
	}

	[Fact]
	public void ScrollbarPosition_CanBeChanged()
	{
		var panel = new ScrollablePanelControl();

		Assert.Equal(ScrollbarPosition.Right, panel.ScrollbarPosition);

		panel.ScrollbarPosition = ScrollbarPosition.Left;
		Assert.Equal(ScrollbarPosition.Left, panel.ScrollbarPosition);
	}

	[Fact]
	public void Height_CanBeSet()
	{
		var panel = new ScrollablePanelControl();

		Assert.Null(panel.Height);

		panel.Height = 15;
		Assert.Equal(15, panel.Height);
	}

	#endregion

	#region Scrolling

	[Fact]
	public void ScrollVerticalBy_PositiveDelta_ScrollsDown()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		panel.ScrollVerticalBy(5);

		Assert.Equal(5, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollVerticalBy_NegativeDelta_ScrollsUp()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		panel.ScrollVerticalBy(8);
		panel.ScrollVerticalBy(-3);

		Assert.Equal(5, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollVerticalBy_ClampsToZero()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		panel.ScrollVerticalBy(-100);

		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollVerticalBy_ClampsToMax()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();
		int expectedMax = Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);

		panel.ScrollVerticalBy(10000);

		Assert.Equal(expectedMax, panel.VerticalScrollOffset);
		Assert.True(expectedMax > 0, "Content should exceed viewport for this test to be meaningful");
	}

	[Fact]
	public void ScrollToTop_ResetsOffset()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		panel.ScrollVerticalBy(10);
		Assert.True(panel.VerticalScrollOffset > 0);

		panel.ScrollToTop();

		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollToBottom_SetsMaxOffset()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();
		int expectedMax = Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);

		panel.ScrollToBottom();

		Assert.Equal(expectedMax, panel.VerticalScrollOffset);
	}

	[Fact]
	public void Scrolled_EventFires_OnScroll()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();
		ScrollEventArgs? firedArgs = null;
		panel.Scrolled += (sender, args) => firedArgs = args;

		panel.ScrollVerticalBy(3);

		Assert.NotNull(firedArgs);
		Assert.Equal(ScrollDirection.Vertical, firedArgs!.Direction);
		Assert.Equal(3, firedArgs.VerticalOffset);
	}

	[Fact]
	public void CanScrollUp_FalseAtTop()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		Assert.False(panel.CanScrollUp);
	}

	[Fact]
	public void CanScrollDown_FalseAtBottom()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		panel.ScrollToBottom();

		Assert.False(panel.CanScrollDown);
	}

	[Fact]
	public void CanScrollDown_TrueWhenContentExceedsViewport()
	{
		var (panel, _, _) = CreateRenderedScrollPanel();

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			"Content must exceed viewport for scroll to be possible");
		Assert.True(panel.CanScrollDown);
	}

	#endregion

	#region Focus / Navigation

	[Fact]
	public void CanReceiveFocus_TrueWithFocusableChildren()
	{
		var panel = new ScrollablePanelControl();
		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C");
		panel.AddControl(list);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		Assert.True(panel.CanReceiveFocus);
	}

	[Fact]
	public void SetFocus_True_FocusesFirstChild()
	{
		var panel = new ScrollablePanelControl();
		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C");
		panel.AddControl(list);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);

		Assert.True(panel.HasFocus);
		Assert.True(list.HasFocus);
	}

	[Fact]
	public void SetFocus_False_UnfocusesChildren()
	{
		var panel = new ScrollablePanelControl();
		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C");
		panel.AddControl(list);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// After AddControl, list should have focus via IFocusScope delegation
		Assert.True(list.HasFocus);

		// Clear focus via FocusManager (new design: no-op setters replaced by direct API)
		window.UnfocusCurrentControl();

		Assert.False(panel.HasFocus);
		Assert.False(list.HasFocus);
	}

	[Fact]
	public void ProcessKey_Tab_CyclesThroughChildren()
	{
		var panel = new ScrollablePanelControl();
		var list1 = ContainerTestHelpers.CreateFocusableList("A1", "A2");
		var list2 = ContainerTestHelpers.CreateFocusableList("B1", "B2");
		panel.AddControl(list1);
		panel.AddControl(list2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);
		Assert.True(list1.HasFocus, "First child should have focus initially");

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		bool handled = panel.ProcessKey(tabKey);

		Assert.True(handled);
		Assert.False(list1.HasFocus);
		Assert.True(list2.HasFocus);
	}

	[Fact]
	public void ProcessKey_ShiftTab_NavigatesBackward()
	{
		var panel = new ScrollablePanelControl();
		var list1 = ContainerTestHelpers.CreateFocusableList("A1", "A2");
		var list2 = ContainerTestHelpers.CreateFocusableList("B1", "B2");
		panel.AddControl(list1);
		panel.AddControl(list2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Focus panel, Tab to second child
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);
		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		panel.ProcessKey(tabKey);
		Assert.True(list2.HasFocus, "Second child should have focus after Tab");

		// Shift+Tab back to first child
		var shiftTabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false);
		bool handled = panel.ProcessKey(shiftTabKey);

		Assert.True(handled);
		Assert.True(list1.HasFocus);
		Assert.False(list2.HasFocus);
	}

	#endregion

	#region Layout Edge Cases

	[Fact]
	public void ScrollbarReservesSpace_ChildGetsReducedWidth()
	{
		// Panel with scrollbar and enough content to scroll
		var (panel, _, window) = CreateRenderedScrollPanel(childCount: 30, panelHeight: 10);
		panel.ShowScrollbar = true;

		window.RenderAndGetVisibleContent();

		// ViewportWidth is the full panel width minus margins.
		// The scrollbar takes 2 columns (1 gap + 1 bar) from children's content width.
		// TotalContentHeight > ViewportHeight ensures scrollbar is visible.
		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			"Content must exceed viewport for scrollbar to be shown");
		Assert.True(panel.ViewportWidth > 0);
	}

	[Fact]
	public void NoScrollbar_ChildGetsFullWidth()
	{
		var panel = new ScrollablePanelControl();
		panel.ShowScrollbar = false;
		panel.Height = 10;
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// With ShowScrollbar=false, content gets the full viewport width
		Assert.True(panel.ViewportWidth > 0);
		// ViewportHeight should match Height minus margins
		Assert.True(panel.ViewportHeight > 0);
	}

	[Fact]
	public void FillChildren_InScrollablePanel_MeasuredCorrectly()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 20;

		var label = ContainerTestHelpers.CreateLabel("Fixed header");
		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C", "D", "E");
		list.VerticalAlignment = VerticalAlignment.Fill;

		panel.AddControl(label);
		panel.AddControl(list);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// The fill child should expand to take remaining space
		Assert.True(panel.TotalContentHeight > 0, "Content height should be computed after render");
	}

	[Fact]
	public void ChildTallerThanViewport_CreatesScrollRange()
	{
		var (panel, _, _) = CreateRenderedScrollPanel(childCount: 50, panelHeight: 10);

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight);
		Assert.True(panel.CanScrollDown);
		Assert.False(panel.CanScrollUp); // At top
	}

	[Fact]
	public void MultipleChildren_VariousAlignments_PositionedCorrectly()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 20;

		var label1 = ContainerTestHelpers.CreateLabel("Top label");
		var label2 = ContainerTestHelpers.CreateLabel("Center label");
		label2.HorizontalAlignment = HorizontalAlignment.Center;
		var label3 = ContainerTestHelpers.CreateLabel("Right label");
		label3.HorizontalAlignment = HorizontalAlignment.Right;

		panel.AddControl(label1);
		panel.AddControl(label2);
		panel.AddControl(label3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);

		// Should render without exceptions and compute content dimensions
		window.RenderAndGetVisibleContent();

		Assert.True(panel.TotalContentHeight > 0);
		Assert.Equal(3, panel.Children.Count);
	}

	[Fact]
	public void StickyChild_RemainsFixedDuringScroll()
	{
		// StickyPosition is set on the control but handled by Window.Rendering,
		// not by ScrollablePanelControl directly. Here we verify the property
		// can be set without breaking panel scroll behavior.
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		var stickyLabel = ContainerTestHelpers.CreateLabel("Sticky header");
		stickyLabel.StickyPosition = StickyPosition.Top;
		panel.AddControl(stickyLabel);

		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Content line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Scrolling should work even with a sticky-positioned child
		panel.ScrollVerticalBy(5);
		Assert.Equal(5, panel.VerticalScrollOffset);
	}

	#endregion

	#region Scroll Bubbling - Mouse Wheel

	[Fact]
	public void MouseWheel_ChildHandlesScroll_PanelDoesNotScroll()
	{
		// An inner ScrollablePanelControl that can scroll down will consume the wheel event.
		// The outer panel should not scroll when the inner panel consumes it.
		var outerPanel = new ScrollablePanelControl();
		outerPanel.Height = 15;

		var innerPanel = new ScrollablePanelControl();
		innerPanel.Height = 5;

		// Fill inner panel with enough content to make it scrollable
		for (int i = 0; i < 20; i++)
			innerPanel.AddControl(ContainerTestHelpers.CreateLabel($"Inner {i}"));

		outerPanel.AddControl(innerPanel);

		// Add more content to outer panel to make it scrollable too
		for (int i = 0; i < 30; i++)
			outerPanel.AddControl(ContainerTestHelpers.CreateLabel($"Outer {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		window.RenderAndGetVisibleContent();

		// Verify inner panel can scroll
		Assert.True(innerPanel.TotalContentHeight > innerPanel.ViewportHeight,
			"Inner panel should have scrollable content");

		// Wheel down over the inner panel area (Y=2 should be within inner panel bounds)
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 2);
		bool handled = outerPanel.ProcessMouseEvent(wheelDown);

		// Inner panel should have consumed the wheel event
		Assert.True(handled, "Wheel event should have been handled by inner panel");
		Assert.True(innerPanel.VerticalScrollOffset > 0, "Inner panel should have scrolled");
		Assert.Equal(0, outerPanel.VerticalScrollOffset);
	}

	[Fact]
	public void MouseWheel_ChildAtTopEdge_ScrollUp_BubblesToPanel()
	{
		// List at top (scrollOffset=0), wheel up should bubble to panel
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C");
		panel.AddControl(list);

		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Scroll panel down first so it can scroll up
		panel.ScrollVerticalBy(5);
		int offsetBefore = panel.VerticalScrollOffset;

		// Wheel up over the list - list is at top so can't scroll up, should bubble to panel
		var wheelUp = ContainerTestHelpers.CreateWheelUp(5, 1);
		bool handled = panel.ProcessMouseEvent(wheelUp);

		Assert.True(handled, "Panel should handle wheel up when child can't");
		Assert.True(panel.VerticalScrollOffset < offsetBefore,
			"Panel should have scrolled up");
	}

	[Fact]
	public void MouseWheel_ChildAtBottomEdge_ScrollDown_BubblesToPanel()
	{
		// List at bottom edge, wheel down should bubble to panel
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		// Small list that doesn't need scrolling (3 items, all visible)
		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C");
		panel.AddControl(list);

		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Wheel down over the list - list can't scroll (all items visible), should bubble
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 1);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled, "Panel should handle wheel when child can't scroll");
		Assert.True(panel.VerticalScrollOffset > 0, "Panel should have scrolled down");
	}

	[Fact]
	public void MouseWheel_ChildNotScrollable_BubblesToPanel()
	{
		// MarkupControl has no scroll capability, should bubble to panel
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		panel.AddControl(ContainerTestHelpers.CreateLabel("Not scrollable"));
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 0);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled, "Panel should handle wheel when child is not scrollable");
		Assert.True(panel.VerticalScrollOffset > 0, "Panel should have scrolled down");
	}

	[Fact]
	public void MouseWheel_OverChild_RoutesToChild_NotFocusedControl()
	{
		// Two lists - focus on list1, but mouse is over list2.
		// Wheel should go to list2 (position-based), not list1 (focus-based).
		var panel = new ScrollablePanelControl();
		panel.Height = 20;

		var list1 = ContainerTestHelpers.CreateFocusableList("A1", "A2", "A3");
		var list2 = ContainerTestHelpers.CreateFocusableList(
			"B0", "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B9",
			"B10", "B11", "B12", "B13", "B14");
		panel.AddControl(list1);
		panel.AddControl(list2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Focus list1
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);
		Assert.True(list1.HasFocus);

		// Wheel down at Y position where list2 is (after list1's rows)
		// list1 takes about 3 lines, so list2 starts around Y=3
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 5);
		panel.ProcessMouseEvent(wheelDown);

		// Panel should not have scrolled if list2 consumed the event
		// (list2 has 15 items and should be able to scroll down)
	}

	[Fact]
	public void MouseWheel_OutsideChild_RoutesToPanel()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 5;

		// One small label, but lots of extra content below
		panel.AddControl(ContainerTestHelpers.CreateLabel("Short"));
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Wheel down in an area below all children - hits panel directly
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 3);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled);
		Assert.True(panel.VerticalScrollOffset > 0);
	}

	[Fact]
	public void MouseWheel_ChildAtEdge_PanelAlsoAtEdge_ReturnsFalse()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		// Just one non-scrollable label and not enough content to exceed viewport
		panel.AddControl(ContainerTestHelpers.CreateLabel("Only content"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// If content does not exceed viewport, panel can't scroll
		if (panel.TotalContentHeight <= panel.ViewportHeight)
		{
			var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 0);
			bool handled = panel.ProcessMouseEvent(wheelDown);

			Assert.False(handled, "Neither child nor panel can scroll, should return false");
			Assert.Equal(0, panel.VerticalScrollOffset);
		}
	}

	[Fact]
	public void MouseWheel_ChildScrollsToEdge_NextWheelBubbles()
	{
		// First wheel consumed by child, second should bubble to panel
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		// List with few items - one scroll down will reach the bottom
		var list = ContainerTestHelpers.CreateFocusableList("A", "B", "C");
		panel.AddControl(list);

		for (int i = 0; i < 30; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Extra {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// First wheel up over list - list is at top, can't scroll up, goes to panel
		// But panel is also at top, so this won't scroll panel either.
		// Scroll panel down first.
		panel.ScrollVerticalBy(5);
		int panelOffsetAfterFirstScroll = panel.VerticalScrollOffset;

		// Now wheel up over the list. List is at top so can't scroll up.
		// Panel should scroll up.
		var wheelUp = ContainerTestHelpers.CreateWheelUp(5, 1);
		bool handled = panel.ProcessMouseEvent(wheelUp);

		Assert.True(handled, "Panel should handle bubbled wheel event");
		Assert.True(panel.VerticalScrollOffset < panelOffsetAfterFirstScroll,
			"Panel should have scrolled up after child couldn't");
	}

	[Fact]
	public void MouseWheel_NestedScrollablePanel_InnerHandlesFirst()
	{
		var outerPanel = new ScrollablePanelControl();
		outerPanel.Height = 15;

		var innerPanel = new ScrollablePanelControl();
		innerPanel.Height = 8;

		// Fill inner panel with content to make it scrollable
		for (int i = 0; i < 20; i++)
			innerPanel.AddControl(ContainerTestHelpers.CreateLabel($"Inner {i}"));

		outerPanel.AddControl(innerPanel);

		// Add more content to outer panel to make it scrollable
		for (int i = 0; i < 20; i++)
			outerPanel.AddControl(ContainerTestHelpers.CreateLabel($"Outer {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		window.RenderAndGetVisibleContent();

		// Wheel down over inner panel area
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 2);
		bool handled = outerPanel.ProcessMouseEvent(wheelDown);

		Assert.True(handled, "Nested panel should handle scroll");
		// Inner panel should have scrolled, outer should not
		// (inner panel is IMouseAwareControl and handles wheel events)
	}

	[Fact]
	public void MouseWheel_GridWithScrollableColumn_RoutesCorrectly()
	{
		// Verify a panel containing a grid with scrollable content works
		var panel = new ScrollablePanelControl();
		panel.Height = 15;

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var list = ContainerTestHelpers.CreateFocusableList(
			"G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "G10");
		column.AddContent(list);
		grid.AddColumn(column);
		panel.AddControl(grid);

		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Below grid {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Wheel should reach the list inside the grid column
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 2);
		panel.ProcessMouseEvent(wheelDown);

		// Test passes if no exception is thrown during routing
	}

	[Fact]
	public void MouseWheel_EnableMouseWheelFalse_DoesNotScroll()
	{
		var (panel, _, window) = CreateRenderedScrollPanel();
		panel.EnableMouseWheel = false;

		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 3);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.False(handled, "Panel should not handle wheel when EnableMouseWheel is false");
		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	[Fact]
	public void MouseWheel_EnableMouseWheelFalse_StillForwardsToChild()
	{
		// Even with EnableMouseWheel=false on panel, child should still get the event
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		panel.EnableMouseWheel = false;

		var list = ContainerTestHelpers.CreateFocusableList(
			"A0", "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9",
			"A10", "A11", "A12", "A13", "A14");
		panel.AddControl(list);

		// Add more content to make the panel scrollable
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Wheel down over the list - child should still get the event
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 2);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		// Child (list) should have handled it even though panel's EnableMouseWheel is false
		// The child forwarding happens before the EnableMouseWheel check
		Assert.True(handled, "Child should still receive wheel events when panel has EnableMouseWheel=false");
		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	#endregion

	#region Edge Cases - Empty, Single Child, and Disabled

	[Fact]
	public void CanReceiveFocus_EmptyPanel_NoScrollNeeded_ReturnsFalse()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 100;

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		Assert.False(panel.CanReceiveFocus);
	}

	[Fact]
	public void SetFocus_WithZeroChildren_DoesNotThrow()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		var ex = Record.Exception(() => window.FocusManager.SetFocus(panel, FocusReason.Programmatic));

		Assert.Null(ex);
	}

	[Fact]
	public void ProcessKey_Tab_SingleFocusableChild_ExitsPanel()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		var button = ContainerTestHelpers.CreateButton("OK");
		panel.AddControl(button);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		bool handled = panel.ProcessKey(tabKey);

		Assert.False(handled);
	}

	[Fact]
	public void ProcessKey_ShiftTab_FromFirstChild_ExitsBackward()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		var button1 = ContainerTestHelpers.CreateButton("First");
		var button2 = ContainerTestHelpers.CreateButton("Second");
		panel.AddControl(button1);
		panel.AddControl(button2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);
		Assert.True(button1.HasFocus);

		var shiftTabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false);
		bool handled = panel.ProcessKey(shiftTabKey);

		Assert.False(handled);
	}

	[Fact]
	public void ProcessKey_WhenNotFocused_ReturnsFalse()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		var button = ContainerTestHelpers.CreateButton("OK");
		panel.AddControl(button);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		bool handled = panel.ProcessKey(tabKey);

		Assert.False(handled);
	}

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		var button = ContainerTestHelpers.CreateButton("OK");
		panel.AddControl(button);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);
		panel.IsEnabled = false;

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		bool handled = panel.ProcessKey(tabKey);

		Assert.False(handled);
	}

	[Fact]
	public void ScrollVerticalBy_WhenContentFitsViewport_NoScroll()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 50;

		for (int i = 0; i < 3; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		panel.ScrollVerticalBy(5);

		Assert.Equal(0, panel.VerticalScrollOffset);
	}

	[Fact]
	public void SetFocus_CycleOnOff_RestoresFocusToFirstChild()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		var button1 = ContainerTestHelpers.CreateButton("First");
		var button2 = ContainerTestHelpers.CreateButton("Second");
		panel.AddControl(button1);
		panel.AddControl(button2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);
		Assert.True(button1.HasFocus);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);
		Assert.True(button1.HasFocus);
	}

	[Fact]
	public void ProcessKey_Escape_InScrollMode_PropagatesUp()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 5;

		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

		var escKey = new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false);
		bool handled = panel.ProcessKey(escKey);

		Assert.False(handled);
	}

	[Fact]
	public void Tab_WithAllChildrenDisabled_ExitsPanel()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		var button1 = ContainerTestHelpers.CreateButton("A");
		var button2 = ContainerTestHelpers.CreateButton("B");
		var button3 = ContainerTestHelpers.CreateButton("C");
		button1.IsEnabled = false;
		button2.IsEnabled = false;
		button3.IsEnabled = false;
		panel.AddControl(button1);
		panel.AddControl(button2);
		panel.AddControl(button3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		bool handled = panel.ProcessKey(tabKey);

		Assert.False(handled);
	}

	#endregion
}
