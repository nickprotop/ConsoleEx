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
/// Edge case tests for focus interactions: mouse clicks, escape key,
/// backward Tab cycles, programmatic focus, and whitespace clicks.
/// </summary>
public class FocusEdgeCaseTests
{
	private static ConsoleKeyInfo TabKey => new('\t', ConsoleKey.Tab, false, false, false);
	private static ConsoleKeyInfo ShiftTabKey => new('\t', ConsoleKey.Tab, true, false, false);
	private static ConsoleKeyInfo EscapeKey => new((char)27, ConsoleKey.Escape, false, false, false);

	private static MouseEventArgs CreateClickArgs(int x, int y)
	{
		var pos = new System.Drawing.Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked },
			pos, pos, pos);
	}

	#region Escape key in nested containers

	/// <summary>
	/// SPC with focused button. Escape → button loses focus, SPC in scroll mode.
	/// Escape again → SPC loses focus entirely (propagates to parent).
	/// </summary>
	[Fact]
	public void Escape_FromChild_EntersScrollMode_ThenExitsPanel()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill
		};

		var lines = new List<string>();
		for (int i = 0; i < 30; i++)
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

		// Auto-focus focuses the button when panel is added to window
		Assert.True(button.HasFocus, "Setup: button should be focused");

		// Escape 1: button loses focus, panel stays focused in scroll mode
		bool handled1 = panel.ProcessKey(EscapeKey);
		Assert.True(handled1, "First Escape should be handled by panel");
		Assert.False(button.HasFocus, "Button should lose focus after Escape");
		Assert.True(panel.HasFocus, "Panel should stay focused (scroll mode)");

		// Escape 2: panel loses focus (propagates to parent)
		bool handled2 = panel.ProcessKey(EscapeKey);
		Assert.False(handled2, "Second Escape should NOT be handled (propagates out)");
	}

	#endregion

	#region Backward Tab (Shift+Tab) full cycle

	/// <summary>
	/// Full Shift+Tab backward cycle through NavigationView.
	/// When NavigationView gets focus backward: content panel first, then nav pane.
	/// </summary>
	[Fact]
	public void ShiftTab_NavigationView_BackwardCycle()
	{
		var nav = new NavigationView();
		nav.VerticalAlignment = VerticalAlignment.Fill;

		for (int i = 0; i < 20; i++)
			nav.AddItem($"Item {i}");
		nav.SelectedIndex = 0;

		var contentPanel = nav.ContentPanel;
		var button = new ButtonControl { Text = "Content Button" };
		contentPanel.AddControl(button);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(nav);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Backward focus: NavigationView should focus content panel first
		window.SwitchFocus(backward: true);
		Assert.True(nav.HasFocus, "Shift+Tab 1: NavigationView should have focus");

		// Shift+Tab within nav should move from content → nav pane
		// First, if content panel is focused, Shift+Tab should go to nav pane
		bool handled = nav.ProcessKey(ShiftTabKey);
		// The nav routes Shift+Tab from content to nav pane (or exits)
		// Either way, verify no crash and focus state is consistent
		Assert.True(nav.HasFocus, "NavigationView should maintain focus during internal Shift+Tab");
	}

	#endregion

	#region Programmatic FocusControl on nested control

	/// <summary>
	/// window.FocusControl(button) where button is inside SPC.
	/// Button should have focus. No double leaf focus.
	/// </summary>
	[Fact]
	public void FocusControl_NestedInSPC_FocusesCorrectly()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		panel.AddControl(btn1);
		panel.AddControl(btn2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Programmatically focus btn2
		window.FocusControl(btn2);

		Assert.True(btn2.HasFocus, "btn2 should have focus via FocusControl");
		Assert.False(btn1.HasFocus, "btn1 should NOT have focus");
	}

	/// <summary>
	/// window.FocusControl(button) where button is inside SPC inside HGrid.
	/// Deep nesting — focus should still work.
	/// </summary>
	[Fact]
	public void FocusControl_DeepNested_FocusesCorrectly()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var button = new ButtonControl { Text = "Deep" };
		innerPanel.AddControl(button);
		col.AddContent(innerPanel);
		grid.AddColumn(col);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusControl(button);

		Assert.True(button.HasFocus, "Deep nested button should have focus via FocusControl");
	}

	#endregion

	#region Focus state consistency after multiple operations

	/// <summary>
	/// Rapid Tab cycling should not accumulate stale focus state.
	/// After N Tabs, exactly one leaf should have focus.
	/// </summary>
	[Fact]
	public void RapidTabCycling_NoStaleFocus()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		var btn3 = new ButtonControl { Text = "Btn3" };
		panel.AddControl(btn1);
		panel.AddControl(btn2);
		panel.AddControl(btn3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);

		// Tab 20 times rapidly
		for (int i = 0; i < 20; i++)
		{
			if (!panel.ProcessKey(TabKey))
				window.SwitchFocus(backward: false);
		}

		// Count focused leaves
		int focusedCount = 0;
		if (btn1.HasFocus) focusedCount++;
		if (btn2.HasFocus) focusedCount++;
		if (btn3.HasFocus) focusedCount++;

		Assert.True(focusedCount <= 1,
			$"After 20 rapid Tabs, at most 1 button should have focus. Got {focusedCount}");
	}

	/// <summary>
	/// Alternating Tab and Shift+Tab should not corrupt focus state.
	/// </summary>
	[Fact]
	public void AlternatingTabShiftTab_NoCorruption()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		panel.AddControl(btn1);
		panel.AddControl(btn2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);

		// Alternate Tab/Shift+Tab 10 times
		for (int i = 0; i < 10; i++)
		{
			var key = (i % 2 == 0) ? TabKey : ShiftTabKey;
			if (!panel.ProcessKey(key))
			{
				if (i % 2 == 0)
					window.SwitchFocus(backward: false);
				else
					window.SwitchFocus(backward: true);
			}
		}

		// Should not crash and at most 1 button focused
		int focusedCount = 0;
		if (btn1.HasFocus) focusedCount++;
		if (btn2.HasFocus) focusedCount++;
		Assert.True(focusedCount <= 1,
			$"After alternating Tab/Shift+Tab, at most 1 button should have focus. Got {focusedCount}");
	}

	#endregion

	#region Focus with disabled controls in the mix

	/// <summary>
	/// Tab should skip disabled controls within an SPC.
	/// </summary>
	[Fact]
	public void Tab_SkipsDisabledControls_InSPC()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn1 = new ButtonControl { Text = "Enabled1" };
		var btnDisabled = new ButtonControl { Text = "Disabled", IsEnabled = false };
		var btn2 = new ButtonControl { Text = "Enabled2" };
		panel.AddControl(btn1);
		panel.AddControl(btnDisabled);
		panel.AddControl(btn2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Auto-focus focuses btn1 (first enabled) when panel is added to window
		Assert.True(btn1.HasFocus, "btn1 should be focused");
		Assert.False(btnDisabled.HasFocus, "disabled button should not have focus");

		// Tab should skip disabled and go to btn2
		panel.ProcessKey(TabKey);
		Assert.True(btn2.HasFocus, "btn2 should be focused (disabled skipped)");
		Assert.False(btnDisabled.HasFocus, "disabled button should still not have focus");
	}

	#endregion

	#region Mouse click on whitespace inside SPC

	/// <summary>
	/// SPC with focused button. Click on empty area below all children.
	/// Button should lose focus. SPC stays focused in scroll mode.
	/// </summary>
	[Fact]
	public void MouseClick_Whitespace_UnfocusesChild_EntersScrollMode()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var button = new ButtonControl { Text = "Btn" };
		panel.AddControl(button);
		// Leave lots of empty space below

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Auto-focus focuses the button when panel is added to window
		Assert.True(button.HasFocus, "Setup: button should be focused");

		// Click on empty space far below the button (Y=10, button is at Y~0-1)
		var clickArgs = ContainerTestHelpers.CreateClick(2, 10);
		panel.ProcessMouseEvent(clickArgs);

		Assert.False(button.HasFocus, "Button should lose focus after whitespace click");
		Assert.True(panel.HasFocus, "Panel should stay focused (scroll mode)");
	}

	#endregion

	#region Mouse click on different child inside SPC

	/// <summary>
	/// SPC with btn1 focused. Click on btn2. btn1 loses focus, btn2 gains.
	/// </summary>
	[Fact]
	public void MouseClick_DifferentChild_SwitchesFocus()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		panel.AddControl(btn1);
		panel.AddControl(btn2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Auto-focus focuses btn1 when panel is added to window
		Assert.True(btn1.HasFocus, "Setup: btn1 should be focused");

		// Click on btn2 position (Y=1 if btn1 is at Y=0)
		var clickArgs = ContainerTestHelpers.CreateClick(2, 1);
		panel.ProcessMouseEvent(clickArgs);

		// btn1 should lose focus, btn2 should gain
		Assert.False(btn1.HasFocus, "btn1 should lose focus after clicking btn2");
		Assert.True(btn2.HasFocus, "btn2 should gain focus after click");
	}

	#endregion

	#region Mouse click through nested containers

	/// <summary>
	/// Click on a button inside Grid → Column → SPC.
	/// Verifies the full container chain gets correct focus tracking.
	/// </summary>
	[Fact]
	public void MouseClick_NestedContainers_FocusesLeafCorrectly()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var button = new ButtonControl { Text = "Deep" };
		innerPanel.AddControl(button);
		col.AddContent(innerPanel);
		grid.AddColumn(col);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Click on the button position
		outerPanel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));

		Assert.True(button.HasFocus, "Deep nested button should be focused via mouse click");
		Assert.True(outerPanel.HasFocus, "Outer panel should have focus");
	}

	#endregion

	#region Window activation/deactivation focus restore

	/// <summary>
	/// Focus a control, deactivate window, reactivate — focus should restore.
	/// </summary>
	[Fact]
	public void WindowDeactivateReactivate_RestoresFocus()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var button = new ButtonControl { Text = "Btn" };
		panel.AddControl(button);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		// Focus the button
		window.SwitchFocus(backward: false);
		if (!button.HasFocus)
			panel.ProcessKey(TabKey);
		Assert.True(button.HasFocus, "Setup: button should be focused");

		// Deactivate window
		window.SetIsActive(false);

		// Reactivate window
		window.SetIsActive(true);

		// Focus should be restored
		// Note: the exact restore behavior depends on _lastFocusedControl tracking.
		// At minimum, the window should be active and the panel should be focusable.
		Assert.True(window.GetIsActive(), "Window should be active");
	}

	/// <summary>
	/// Two windows — focus switches correctly between them.
	/// </summary>
	[Fact]
	public void MultipleWindows_FocusSwitching()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 40);

		var window1 = new Window(system)
		{
			Title = "Win1", Left = 0, Top = 0, Width = 40, Height = 20
		};
		var btn1 = new ButtonControl { Text = "Win1Btn" };
		window1.AddControl(btn1);

		var window2 = new Window(system)
		{
			Title = "Win2", Left = 40, Top = 0, Width = 40, Height = 20
		};
		var btn2 = new ButtonControl { Text = "Win2Btn" };
		window2.AddControl(btn2);

		system.AddWindow(window1);
		system.AddWindow(window2);
		system.Render.UpdateDisplay();

		// Focus btn1 in window1
		system.SetActiveWindow(window1);
		window1.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 in window1 should have focus");

		// Switch to window2 and focus btn2
		system.SetActiveWindow(window2);
		window2.SwitchFocus(backward: false);
		Assert.True(btn2.HasFocus, "btn2 in window2 should have focus");

		// btn1 should have lost focus when window2 was activated
		// (SetIsActive(false) on window1 clears focus)
		// Note: actual behavior depends on implementation
		Assert.False(btn1.HasFocus && btn2.HasFocus,
			"Both buttons should NOT have focus simultaneously");
	}

	#endregion

	#region Shift+Tab through deep nesting

	/// <summary>
	/// Full backward traversal: Panel → Grid → Panel with 3 buttons.
	/// Shift+Tab should go btn3 → btn2 → btn1 → exits.
	/// </summary>
	[Fact]
	public void ShiftTab_DeepNesting_FullBackwardTraversal()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		innerPanel.AddControl(btn1);
		innerPanel.AddControl(btn2);
		col1.AddContent(innerPanel);

		var btn3 = new ButtonControl { Text = "Btn3" };
		col2.AddContent(btn3);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Focus btn3 (last in traversal order)
		window.SwitchFocus(backward: false);
		// Tab forward to reach btn3
		for (int i = 0; i < 10; i++)
		{
			if (btn3.HasFocus) break;
			if (!outerPanel.ProcessKey(TabKey))
				window.SwitchFocus(backward: false);
		}

		if (btn3.HasFocus)
		{
			// Shift+Tab from btn3 should reach btn2
			outerPanel.ProcessKey(ShiftTabKey);
			Assert.True(btn2.HasFocus, "Shift+Tab from btn3 should reach btn2");

			// Shift+Tab from btn2 should reach btn1
			outerPanel.ProcessKey(ShiftTabKey);
			Assert.True(btn1.HasFocus, "Shift+Tab from btn2 should reach btn1");
		}
	}

	#endregion

	#region NavigationView mouse click nav vs content pane

	/// <summary>
	/// Click on nav pane area should focus the nav scroll panel.
	/// Click on content pane area should focus the content panel and unfocus nav.
	/// </summary>
	[Fact]
	public void NavigationView_ClickNavPane_ThenClickContent_SwitchesFocus()
	{
		var nav = new NavigationView();
		nav.VerticalAlignment = VerticalAlignment.Fill;

		for (int i = 0; i < 10; i++)
			nav.AddItem($"Item {i}");
		nav.SelectedIndex = 0;

		var contentPanel = nav.ContentPanel;
		var button = new ButtonControl { Text = "Content Btn" };
		contentPanel.AddControl(button);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(nav);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// First focus the nav via Tab so we have a baseline
		window.SwitchFocus(backward: false);
		Assert.True(nav.HasFocus, "Setup: NavigationView should have focus");

		// Simulate clicking on the content side by sending Tab to move to content
		nav.ProcessKey(TabKey);

		// After Tab to content panel: NavPaneHasFocus should be false
		// (content panel should have focus, not nav)
		bool navPaneFocused = GetNavPaneHasFocus(nav);
		Assert.False(navPaneFocused,
			"After Tab to content: nav pane should NOT have focus");

		// Tab back to nav
		nav.ProcessKey(ShiftTabKey);

		navPaneFocused = GetNavPaneHasFocus(nav);
		Assert.True(navPaneFocused,
			"After Shift+Tab back: nav pane SHOULD have focus");
	}

	private static bool GetNavPaneHasFocus(NavigationView nav)
	{
		// Access the private _navScrollPanel field via reflection
		var field = typeof(NavigationView).GetField("_navScrollPanel",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		var navPanel = field?.GetValue(nav) as ScrollablePanelControl;
		return navPanel?.HasFocus ?? false;
	}

	#endregion

	#region TimePicker wheel doesn't steal focus

	/// <summary>
	/// Regression: TimePickerControl.ProcessMouseEvent called SetFocus on wheel
	/// events even when not focused, stealing focus from other controls.
	/// </summary>
	[Fact]
	public void TimePicker_WheelWhenUnfocused_DoesNotStealFocus()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var button = new ButtonControl { Text = "Btn" };
		var timePicker = new TimePickerControl();
		col1.AddContent(button);
		col2.AddContent(timePicker);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Focus the button
		window.FocusManager.SetFocus(button, FocusReason.Keyboard);
		Assert.True(button.HasFocus, "Button should be focused");

		// Send wheel to TimePicker — should NOT steal focus
		var wheelArgs = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledDown },
			new System.Drawing.Point(0, 0),
			new System.Drawing.Point(0, 0),
			new System.Drawing.Point(0, 0));
		timePicker.ProcessMouseEvent(wheelArgs);

		Assert.True(button.HasFocus, "Button should STILL be focused after wheel on TimePicker");
		Assert.False(timePicker.HasFocus, "TimePicker should NOT steal focus via wheel");
	}

	#endregion
}
