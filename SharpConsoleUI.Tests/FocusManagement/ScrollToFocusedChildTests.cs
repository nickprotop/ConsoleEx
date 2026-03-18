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
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using static SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Tests that Tab-focusing a control inside a ScrollablePanelControl
/// scrolls the viewport to make the focused control visible.
/// Reproduces the DemoApp LauncherWindow scenario.
/// </summary>
public class ScrollToFocusedChildTests
{
	private static ConsoleKeyInfo TabKey => new('\t', ConsoleKey.Tab, false, false, false);
	private static ConsoleKeyInfo ShiftTabKey => new('\t', ConsoleKey.Tab, true, false, false);

	#region Core: SPC with button below viewport

	/// <summary>
	/// Minimal reproduction: ScrollablePanelControl with tall content
	/// and a button at the bottom (out of viewport). Tab focuses the SPC,
	/// then focuses the button. Viewport must scroll to show the button.
	/// </summary>
	[Fact]
	public void ScrollablePanel_TabFocusesChild_ScrollsIntoView()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		// Tall non-focusable content filling well beyond viewport
		var lines = new List<string>();
		for (int i = 0; i < 50; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		// Button at the bottom — out of initial viewport
		var button = new ButtonControl { Text = "Bottom Button" };
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

		int initialOffset = panel.VerticalScrollOffset;

		// Tab: focuses the SPC
		window.SwitchFocus(backward: false);
		Assert.True(panel.HasFocus, "Panel should have focus");

		// If in scroll mode (child not yet focused), Tab again
		if (!button.HasFocus)
			panel.ProcessKey(TabKey);

		// Re-render to apply scroll
		system.Render.UpdateDisplay();

		Assert.True(button.HasFocus, "Button should be focused after Tab");
		Assert.True(panel.VerticalScrollOffset > initialOffset,
			$"Panel should scroll to show focused button. " +
			$"Offset was {initialOffset}, now {panel.VerticalScrollOffset}");
	}

	/// <summary>
	/// Same test with AutoScroll=true — verifies autoScroll doesn't override
	/// the scroll-to-child offset.
	/// </summary>
	[Fact]
	public void ScrollablePanel_AutoScroll_TabFocusesChild_DoesNotSnapBack()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = true
		};

		// Tall content + button at bottom
		var lines = new List<string>();
		for (int i = 0; i < 50; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var button = new ButtonControl { Text = "Bottom Button" };
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

		// With autoScroll, panel starts at bottom. Scroll to top.
		panel.ScrollVerticalBy(-10000);
		system.Render.UpdateDisplay();
		Assert.Equal(0, panel.VerticalScrollOffset);

		// Tab: focuses the SPC
		window.SwitchFocus(backward: false);

		// Tab to button
		if (!button.HasFocus)
			panel.ProcessKey(TabKey);

		Assert.True(button.HasFocus, "Button should be focused");

		// Re-render — autoScroll should NOT snap offset back to bottom
		system.Render.UpdateDisplay();

		// Button is at line ~51 (50 content lines + 1). Offset should be near there.
		Assert.True(panel.VerticalScrollOffset > 0,
			"Panel should have scrolled to show the button");
	}

	#endregion

	#region NavigationView: content panel with button below viewport

	/// <summary>
	/// The DemoApp LauncherWindow scenario:
	/// NavigationView → left nav (scrollable) + right content panel.
	/// Content panel has tall description + button at bottom (out of view).
	/// Tab 1 → focuses NavigationView.
	/// Tab 2 → focuses button in content panel.
	/// Content panel viewport must scroll to show the button.
	/// </summary>
	[Fact]
	public void NavigationView_TabToContentButton_ScrollsContentPanel()
	{
		var nav = new NavigationView();
		nav.VerticalAlignment = VerticalAlignment.Fill;

		// Add enough nav items to make left pane scrollable
		for (int i = 0; i < 20; i++)
			nav.AddItem($"Item {i}");
		nav.SelectedIndex = 0;

		// Populate content panel with tall content + button at bottom
		var contentPanel = nav.ContentPanel;
		var lines = new List<string>();
		for (int j = 0; j < 40; j++)
			lines.Add($"Description line {j}");
		contentPanel.AddControl(new MarkupControl(lines));

		var launchButton = new ButtonControl { Text = "Launch Demo" };
		contentPanel.AddControl(launchButton);

		// Create environment (80x20 window — content panel viewport ~15 rows)
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(nav);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int initialOffset = contentPanel.VerticalScrollOffset;

		// Tab 1: focuses NavigationView → FocusNavPane (nav scroll panel gets focus)
		window.SwitchFocus(backward: false);

		// Tab 2: Nav pane Tab → FocusContentPanel (content panel enters scroll mode)
		nav.ProcessKey(TabKey);

		// Tab 3: Content panel Tab in scroll mode → focuses button + scrolls into view
		nav.ProcessKey(TabKey);

		// Re-render
		system.Render.UpdateDisplay();

		Assert.True(launchButton.HasFocus,
			"Launch button should be focused after Tab through NavigationView");
		Assert.True(contentPanel.VerticalScrollOffset > initialOffset,
			$"Content panel should scroll to show the focused button. " +
			$"Offset was {initialOffset}, now {contentPanel.VerticalScrollOffset}");
	}

	/// <summary>
	/// Full 8-Tab cycle through NavigationView: verifies the exact focus sequence,
	/// that the cycle repeats correctly, and that no double-focus occurs.
	/// Expected: NavPane → ContentPanel(scroll) → Button → [cycle] NavPane → ContentPanel(scroll) → Button → [cycle] NavPane → ContentPanel(scroll)
	/// </summary>
	[Fact]
	public void NavigationView_EightTabs_FullFocusCycle()
	{
		var nav = new NavigationView();
		nav.VerticalAlignment = VerticalAlignment.Fill;

		for (int i = 0; i < 20; i++)
			nav.AddItem($"Item {i}");
		nav.SelectedIndex = 0;

		var contentPanel = nav.ContentPanel;
		var lines = new List<string>();
		for (int j = 0; j < 40; j++)
			lines.Add($"Description line {j}");
		contentPanel.AddControl(new MarkupControl(lines));

		var launchButton = new ButtonControl { Text = "Launch Demo" };
		contentPanel.AddControl(launchButton);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(nav);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// All leaf controls that could have focus — used for double-focus detection
		var allLeafControls = new List<IInteractiveControl> { launchButton };
		// Add all nav item markup controls (they're not focusable, but check anyway)
		// The key focusable leaves are: launchButton + any toolbar items

		// Double-focus detector: at most ONE leaf (non-container) control should have HasFocus=true.
		// Containers (NavigationView, ScrollablePanelControl) may have HasFocus=true alongside
		// their focused descendant — that's expected. The bug is two LEAF controls with focus.
		void AssertNoDoubleFocus(string step)
		{
			var focusedLeaves = new List<string>();
			var focusedContainers = new List<string>();
			var flatList = window.GetAllFocusableControlsFlattened();
			foreach (var ctrl in flatList)
			{
				if (!ctrl.HasFocus) continue;
				if (ctrl is IWindowControl wc)
				{
					if (ctrl is IContainerControl)
						focusedContainers.Add(wc.GetType().Name);
					else
						focusedLeaves.Add(wc.GetType().Name);
				}
			}
			// Also check the button directly (might not be in flatList if inside opaque container)
			if (launchButton.HasFocus && !focusedLeaves.Contains("ButtonControl"))
				focusedLeaves.Add("LaunchButton");

			Assert.True(focusedLeaves.Count <= 1,
				$"{step}: Double LEAF focus detected! Leaves: [{string.Join(", ", focusedLeaves)}], Containers: [{string.Join(", ", focusedContainers)}]");
		}

		// Helper: press Tab — try nav first, fall back to window.SwitchFocus
		void PressTab()
		{
			if (!nav.ProcessKey(TabKey))
				window.SwitchFocus(backward: false);
		}

		// === Tab 1: Window-level → NavigationView → NavPane (scroll mode) ===
		window.SwitchFocus(backward: false);
		Assert.True(nav.HasFocus, "Tab 1: NavigationView should have focus");
		Assert.False(launchButton.HasFocus, "Tab 1: Button should NOT have focus");
		AssertNoDoubleFocus("Tab 1");

		// === Tab 2: NavPane Tab → FocusContentPanel (content panel scroll mode) ===
		PressTab();
		Assert.True(nav.HasFocus, "Tab 2: NavigationView should still have focus");
		Assert.False(launchButton.HasFocus, "Tab 2: Button should NOT have focus yet");
		AssertNoDoubleFocus("Tab 2");

		// === Tab 3: Content panel Tab → focuses button + scrolls into view ===
		PressTab();
		system.Render.UpdateDisplay();
		Assert.True(launchButton.HasFocus, "Tab 3: Button should be focused");
		Assert.True(contentPanel.VerticalScrollOffset > 0,
			"Tab 3: Content panel should scroll to show button");
		AssertNoDoubleFocus("Tab 3");

		// === Tab 4: Button is last child → Tab exits content panel → exits nav → SwitchFocus → NavPane ===
		PressTab();
		Assert.False(launchButton.HasFocus, "Tab 4: Button should lose focus");
		Assert.True(nav.HasFocus, "Tab 4: NavigationView should have focus (cycled)");
		AssertNoDoubleFocus("Tab 4");

		// === Tab 5: NavPane Tab → FocusContentPanel again ===
		PressTab();
		Assert.True(nav.HasFocus, "Tab 5: NavigationView should have focus");
		Assert.False(launchButton.HasFocus, "Tab 5: Button not focused (content panel scroll mode)");
		AssertNoDoubleFocus("Tab 5");

		// === Tab 6: Content panel Tab → button focused again + scroll ===
		PressTab();
		system.Render.UpdateDisplay();
		Assert.True(launchButton.HasFocus, "Tab 6: Button should be focused again");
		Assert.True(contentPanel.VerticalScrollOffset > 0,
			"Tab 6: Content panel should be scrolled to show button");
		AssertNoDoubleFocus("Tab 6");

		// === Tab 7: Exit content panel → exit nav → SwitchFocus → NavPane ===
		PressTab();
		Assert.False(launchButton.HasFocus, "Tab 7: Button should lose focus");
		Assert.True(nav.HasFocus, "Tab 7: NavigationView should have focus (cycled)");
		AssertNoDoubleFocus("Tab 7");

		// === Tab 8: NavPane Tab → FocusContentPanel ===
		PressTab();
		Assert.True(nav.HasFocus, "Tab 8: NavigationView should have focus");
		Assert.False(launchButton.HasFocus, "Tab 8: Button not focused (content panel scroll mode)");
		AssertNoDoubleFocus("Tab 8");
	}

	#endregion

	#region Shift+Tab in scroll mode

	/// <summary>
	/// When SPC is in scroll mode and Shift+Tab is pressed,
	/// it should focus the LAST child (not exit the SPC).
	/// </summary>
	[Fact]
	public void ScrollablePanel_ShiftTab_InScrollMode_FocusesLastChild()
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

		// Focus panel — enters scroll mode if NeedsScrolling
		window.SwitchFocus(backward: false);

		// If in scroll mode (no child focused yet), Shift+Tab should focus last child
		if (!btn1.HasFocus && !btn2.HasFocus)
		{
			bool handled = panel.ProcessKey(ShiftTabKey);
			Assert.True(handled, "Shift+Tab in scroll mode should be handled");
			Assert.True(btn2.HasFocus, "Shift+Tab should focus the last focusable child");
		}
	}

	#endregion
}
