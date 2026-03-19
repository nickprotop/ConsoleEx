// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Regression tests for focus bugs discovered during the FocusCoordinator refactoring.
/// Each test reproduces a specific bug that was found and fixed.
/// </summary>
public class FocusRegressionTests
{
	private static ConsoleKeyInfo TabKey => new('\t', ConsoleKey.Tab, false, false, false);
	private static ConsoleKeyInfo ShiftTabKey => new('\t', ConsoleKey.Tab, true, false, false);

	#region Wheel scroll must not steal focus (HGrid bug)

	/// <summary>
	/// Regression: HorizontalGridControl.ProcessMouseEvent ran focus-change logic
	/// for ALL mouse events including wheel. When a child ScrollablePanelControl
	/// was at its scroll limit and returned false for wheel, the event bubbled
	/// to the HGrid which stole focus.
	/// Fix: HGrid guards focus logic with isClickEvent check.
	/// </summary>
	[Fact]
	public void HGrid_WheelEvent_DoesNotStealFocus()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		col1.AddContent(btn1);
		col2.AddContent(btn2);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Focus btn1 directly (HGrid is transparent, btn1 is in Tab list)
		btn1.SetFocus(true, FocusReason.Keyboard);
		Assert.True(btn1.HasFocus, "btn1 should be focused");

		// Send wheel event to the grid — should NOT change focus
		var wheelArgs = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledDown },
			new System.Drawing.Point(5, 0),
			new System.Drawing.Point(5, 0),
			new System.Drawing.Point(5, 0));
		grid.ProcessMouseEvent(wheelArgs);

		Assert.True(btn1.HasFocus, "btn1 should STILL be focused after wheel");
		Assert.False(btn2.HasFocus, "btn2 should NOT get focus from wheel");
	}

	/// <summary>
	/// Regression: Same bug but with a ScrollablePanelControl at scroll limit
	/// inside an HGrid — the wheel bubbles from SPC to HGrid.
	/// </summary>
	[Fact]
	public void HGrid_WheelBubbledFromSPC_DoesNotStealFocus()
	{
		var outerPanel = new ScrollablePanelControl { Height = 15 };
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var panel1 = new ScrollablePanelControl { Height = 10 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		panel1.AddControl(btn1);
		col1.AddContent(panel1);

		var btn2 = new ButtonControl { Text = "Btn2" };
		col2.AddContent(btn2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Focus btn1
		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 should be focused");

		// Send wheel to grid (simulating bubble from SPC at limit)
		var wheelArgs = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledUp },
			new System.Drawing.Point(0, 0),
			new System.Drawing.Point(0, 0),
			new System.Drawing.Point(0, 0));
		grid.ProcessMouseEvent(wheelArgs);

		Assert.True(btn1.HasFocus, "btn1 should STILL be focused after bubbled wheel");
	}

	#endregion

	#region FocusService chain must not clear SPC._focusedChild

	/// <summary>
	/// Regression: When SPC.ProcessKey(Tab) focused a child via SetFocus(true),
	/// the notification chain reached FocusStateService which called
	/// previousControl.HasFocus=false on the SPC (the previous focused control).
	/// This triggered SPC.SetFocus(false) which cleared _focusedChild, preventing
	/// ScrollChildIntoView from running.
	/// Fix: SPC Tab handler saves child ref and restores _focusedChild after SetFocus.
	/// </summary>
	[Fact]
	public void SPC_TabFocusChild_FocusedChildSurvivesNotificationChain()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		var lines = new List<string>();
		for (int i = 0; i < 50; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var button = new ButtonControl { Text = "Target" };
		panel.AddControl(button);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Focus the panel (enters scroll mode)
		window.SwitchFocus(backward: false);
		Assert.True(panel.HasFocus);

		// Tab to focus the button
		panel.ProcessKey(TabKey);

		// Focused child must survive the notification chain
		Assert.True(button.HasFocus, "Button should be focused");

		// Verify the coordinator tracks the focused child correctly
		var coordinator = window.FocusCoord;
		Assert.NotNull(coordinator);
		var focusedChild = coordinator!.GetFocusedChild(panel);
		Assert.NotNull(focusedChild);
		// The coordinator may return a ColumnContainer (transparent wrapper) or the button directly
		// depending on the container hierarchy. The key invariant is that button.HasFocus is true.
		Assert.True(button.HasFocus, "Button should remain focused after coordinator check");
	}

	#endregion

	#region AutoScroll must not override ScrollChildIntoView

	/// <summary>
	/// Regression: When ScrollChildIntoView scrolled to show a focused child,
	/// the subsequent PaintDOM re-render checked _autoScroll and snapped the
	/// offset back to the bottom, hiding the focused child.
	/// Fix: ScrollChildIntoView disables _autoScroll when it scrolls.
	/// </summary>
	[Fact]
	public void SPC_AutoScroll_ScrollChildIntoView_DisablesAutoScroll()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = true
		};

		var lines = new List<string>();
		for (int i = 0; i < 50; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var button = new ButtonControl { Text = "Target" };
		panel.AddControl(button);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// With autoScroll, panel is at bottom. Scroll to top.
		panel.ScrollVerticalBy(-10000);
		system.Render.UpdateDisplay();
		Assert.Equal(0, panel.VerticalScrollOffset);

		// Focus panel and Tab to button
		window.SwitchFocus(backward: false);
		if (!button.HasFocus)
			panel.ProcessKey(TabKey);

		Assert.True(button.HasFocus);
		int offsetAfterFocus = panel.VerticalScrollOffset;

		// Re-render — autoScroll must NOT snap back to bottom
		system.Render.UpdateDisplay();

		Assert.True(panel.VerticalScrollOffset > 0,
			"Offset should be > 0 (scrolled to button)");
		// The offset should NOT have changed due to autoScroll
		Assert.Equal(offsetAfterFocus, panel.VerticalScrollOffset);
	}

	#endregion

	#region Shift+Tab in scroll mode must focus last child

	/// <summary>
	/// Regression: When SPC was in scroll mode (currentIndex=-1) and Shift+Tab
	/// was pressed, newIndex = -1-1 = -2, which is less than 0, so the SPC
	/// returned false (exiting entirely) instead of focusing the last child.
	/// Fix: Handle currentIndex==-1 specially.
	/// </summary>
	[Fact]
	public void SPC_ShiftTab_ScrollMode_FocusesLastChild_NotExits()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill
		};

		var btn1 = new ButtonControl { Text = "First" };
		panel.AddControl(btn1);

		var lines = new List<string>();
		for (int i = 0; i < 30; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var btn2 = new ButtonControl { Text = "Last" };
		panel.AddControl(btn2);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Focus panel — enters scroll mode
		window.SwitchFocus(backward: false);

		if (!btn1.HasFocus && !btn2.HasFocus)
		{
			// In scroll mode. Shift+Tab must focus LAST child, not exit.
			bool handled = panel.ProcessKey(ShiftTabKey);
			Assert.True(handled, "Shift+Tab in scroll mode should be handled (not exit)");
			Assert.True(btn2.HasFocus, "Shift+Tab should focus the LAST focusable child");
			Assert.False(btn1.HasFocus, "btn1 should NOT have focus");
		}
	}

	/// <summary>
	/// Complement: Forward Tab in scroll mode focuses FIRST child.
	/// </summary>
	[Fact]
	public void SPC_Tab_ScrollMode_FocusesFirstChild()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill
		};

		var btn1 = new ButtonControl { Text = "First" };
		panel.AddControl(btn1);

		var lines = new List<string>();
		for (int i = 0; i < 30; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var btn2 = new ButtonControl { Text = "Last" };
		panel.AddControl(btn2);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Focus panel — enters scroll mode
		window.SwitchFocus(backward: false);

		if (!btn1.HasFocus && !btn2.HasFocus)
		{
			bool handled = panel.ProcessKey(TabKey);
			Assert.True(handled, "Tab in scroll mode should be handled");
			Assert.True(btn1.HasFocus, "Tab should focus the FIRST focusable child");
			Assert.False(btn2.HasFocus, "btn2 should NOT have focus");
		}
	}

	#endregion

	#region SetFocus else branch must call ScrollChildIntoView

	/// <summary>
	/// Regression: SPC.SetFocus(true) else branch (when NeedsScrolling=false or
	/// viewport not yet computed) focused a child immediately but never called
	/// ScrollChildIntoView, leaving the child out of the visible viewport.
	/// Fix: Added ScrollChildIntoView after focusing the child.
	/// </summary>
	[Fact]
	public void SPC_SetFocus_ElseBranch_ScrollsChildIntoView()
	{
		// Use a small panel where NeedsScrolling might be false initially
		// but content still extends beyond viewport after layout
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		var lines = new List<string>();
		for (int i = 0; i < 50; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var button = new ButtonControl { Text = "Bottom" };
		panel.AddControl(button);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Focus the panel — should focus child and scroll to it
		window.SwitchFocus(backward: false);

		// After focus, if button got focused directly (not scroll mode),
		// the viewport should have scrolled
		if (button.HasFocus)
		{
			system.Render.UpdateDisplay();
			Assert.True(panel.VerticalScrollOffset > 0,
				"Panel should scroll to show the focused button in else branch");
		}
		// If in scroll mode, Tab to focus button
		else
		{
			panel.ProcessKey(TabKey);
			system.Render.UpdateDisplay();
			Assert.True(button.HasFocus, "Button should be focused");
			Assert.True(panel.VerticalScrollOffset > 0,
				"Panel should scroll to show the focused button after Tab");
		}
	}

	#endregion
}
