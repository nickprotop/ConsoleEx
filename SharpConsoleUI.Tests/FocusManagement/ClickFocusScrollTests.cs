// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Tests that clicking to focus a child inside a ScrollablePanelControl
/// does not cause a scroll jump. Verifies the fix for IFocusScope delegation
/// and ScrollChildIntoView being skipped for Mouse reason.
/// </summary>
public class ClickFocusScrollTests
{
	/// <summary>
	/// Builds a scrolled-down SPC with two buttons (one at top, one at bottom).
	/// Returns the system pre-scrolled so button2 is visible and button1 is off-screen above.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window,
		ScrollablePanelControl panel, ButtonControl button1, ButtonControl button2)
		BuildScrolledPanel()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		var button1 = new ButtonControl { Text = "Top Button" };
		panel.AddControl(button1);

		// Tall filler content to force scrolling
		var lines = new List<string>();
		for (int i = 0; i < 50; i++)
			lines.Add($"Line {i}");
		panel.AddControl(new MarkupControl(lines));

		var button2 = new ButtonControl { Text = "Bottom Button" };
		panel.AddControl(button2);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, panel, button1, button2);
	}

	#region Step 1: IFocusScope delegation skipped for Mouse

	/// <summary>
	/// SetFocus(spc, Mouse) should focus SPC itself, NOT delegate to first child.
	/// This is the core fix: mouse clicks let ProcessMouseEvent handle child targeting.
	/// </summary>
	[Fact]
	public void SetFocus_Mouse_OnSPC_DoesNotDelegateToFirstChild()
	{
		var (system, window, panel, button1, button2) = BuildScrolledPanel();

		// Clear any auto-focus
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Focus SPC with Mouse reason — should NOT delegate to first child
		window.FocusManager.SetFocus(panel, FocusReason.Mouse);

		Assert.True(window.FocusManager.IsFocused(panel),
			"SetFocus(SPC, Mouse) should focus the SPC directly");
		Assert.False(window.FocusManager.IsFocused(button1),
			"SetFocus(SPC, Mouse) should NOT delegate to first child button");
	}

	/// <summary>
	/// SetFocus(spc, Keyboard) should delegate to first child (existing behavior preserved).
	/// </summary>
	[Fact]
	public void SetFocus_Keyboard_OnSPC_DelegatesToFirstChild()
	{
		var (system, window, panel, button1, button2) = BuildScrolledPanel();

		// Clear any auto-focus
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Focus SPC with Keyboard reason — should delegate to first child
		window.FocusManager.SetFocus(panel, FocusReason.Keyboard);

		Assert.True(window.FocusManager.IsFocused(button1),
			"SetFocus(SPC, Keyboard) should delegate to first focusable child");
	}

	#endregion

	#region Step 2: ScrollChildIntoView skipped for Mouse

	/// <summary>
	/// When focusing via Mouse, scroll offset should not change.
	/// The clicked control is already visible — no scroll needed.
	/// </summary>
	[Fact]
	public void SetFocus_Mouse_SkipsScrollChildIntoView()
	{
		var (system, window, panel, button1, button2) = BuildScrolledPanel();

		// Scroll down so button2 is visible
		panel.ScrollVerticalBy(40);
		system.Render.UpdateDisplay();
		int scrollBefore = panel.VerticalScrollOffset;

		// Focus button2 via Mouse — should NOT trigger ScrollChildIntoView
		window.FocusManager.SetFocus(button2, FocusReason.Mouse);

		Assert.Equal(scrollBefore, panel.VerticalScrollOffset);
	}

	/// <summary>
	/// When focusing via Keyboard, ScrollChildIntoView should adjust scroll offset
	/// (existing behavior preserved).
	/// </summary>
	[Fact]
	public void SetFocus_Keyboard_ScrollsChildIntoView()
	{
		var (system, window, panel, button1, button2) = BuildScrolledPanel();

		// Ensure we're at top
		panel.ScrollToTop();
		system.Render.UpdateDisplay();

		// Clear focus first
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Focus button2 via Keyboard — button2 is off-screen, should scroll
		window.FocusManager.SetFocus(button2, FocusReason.Keyboard);

		Assert.True(panel.VerticalScrollOffset > 0,
			$"Keyboard focus on off-screen button should trigger scroll. Offset: {panel.VerticalScrollOffset}");
	}

	#endregion

	#region HandleClick integration

	/// <summary>
	/// HandleClick on a focusable child inside SPC should focus the child directly,
	/// not the SPC (which would then delegate to the wrong child).
	/// </summary>
	[Fact]
	public void HandleClick_OnFocusableChild_FocusesDirectly()
	{
		var (system, window, panel, button1, button2) = BuildScrolledPanel();

		// Scroll down so button2 is visible
		panel.ScrollVerticalBy(40);
		system.Render.UpdateDisplay();
		int scrollBefore = panel.VerticalScrollOffset;

		// Simulate HandleClick on button2
		window.FocusManager.HandleClick(button2);

		Assert.True(window.FocusManager.IsFocused(button2),
			"HandleClick on button should focus the button directly");
		Assert.Equal(scrollBefore, panel.VerticalScrollOffset);
	}

	/// <summary>
	/// HandleClick on a non-focusable control (e.g. MarkupControl) walks up to
	/// the nearest focusable ancestor (SPC). With the fix, SetFocus(SPC, Mouse)
	/// focuses SPC directly without delegating to first child — no scroll jump.
	/// </summary>
	[Fact]
	public void HandleClick_OnNonFocusable_FocusesSPC()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		var markup = new MarkupControl(new List<string> { "Some text content" });
		panel.AddControl(markup);

		var button = new ButtonControl { Text = "Button" };
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

		// Clear auto-focus
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// HandleClick on non-focusable MarkupControl — walks up to SPC
		window.FocusManager.HandleClick(markup);

		// SPC should be focused directly (not button via GetInitialFocus delegation)
		Assert.True(window.FocusManager.IsFocused(panel),
			"HandleClick on non-focusable child should focus the SPC directly (Mouse reason)");
		Assert.False(window.FocusManager.IsFocused(button),
			"HandleClick should NOT delegate to first child via GetInitialFocus");
	}

	#endregion

	#region Step 4: Tab after mouse click enters normally

	/// <summary>
	/// After a mouse click on SPC empty space (which focuses SPC directly),
	/// Tab should enter SPC's first child normally — NOT enter scroll mode.
	/// This verifies the _enterScrollModeOnNextInitialFocus flag removal.
	/// </summary>
	[Fact]
	public void Tab_AfterMouseClick_EntersNormally()
	{
		var panel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		var button1 = new ButtonControl { Text = "First" };
		panel.AddControl(button1);

		var button2 = new ButtonControl { Text = "Second" };
		panel.AddControl(button2);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Simulate: SetFocus(SPC, Mouse) — as if user clicked empty space
		window.FocusManager.SetFocus(panel, FocusReason.Mouse);
		Assert.True(window.FocusManager.IsFocused(panel),
			"SPC should be directly focused after mouse click");

		// Now Tab — should enter SPC normally and focus first child
		window.SwitchFocus(backward: false);

		// After Tab from SPC, focus should move to first child (button1)
		// or at least not stay on SPC in scroll mode
		var focused = window.FocusManager.FocusedControl;
		Assert.True(
			window.FocusManager.IsFocused(button1) || window.FocusManager.IsFocused(button2),
			$"Tab after mouse-focused SPC should enter a child. Focused: {focused?.GetType().Name ?? "null"}");
	}

	#endregion
}
