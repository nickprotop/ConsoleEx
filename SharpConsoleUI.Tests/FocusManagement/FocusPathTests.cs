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
using System.Linq;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Tests for the FocusManager's focus path tracking.
/// Verifies that the focus path correctly represents the chain from
/// root container to focused leaf after Tab, click, and programmatic focus changes.
/// </summary>
public class FocusPathTests
{
	private static ConsoleKeyInfo TabKey => new('\t', ConsoleKey.Tab, false, false, false);
	private static ConsoleKeyInfo ShiftTabKey => new('\t', ConsoleKey.Tab, true, false, false);

	#region Basic focus path

	[Fact]
	public void FocusPath_Empty_WhenNoInteractiveControls()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		// Add a non-interactive control — no auto-focus should occur
		var label = new MarkupControl(new List<string> { "Hello" });
		window.AddControl(label);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		Assert.Empty(window.FocusManager.FocusPath);
		Assert.Null(window.FocusManager.FocusedControl);
	}

	[Fact]
	public void FocusPath_PopulatedAfterAddControl_AutoFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		var btn = new ButtonControl { Text = "Btn" };
		window.AddControl(btn);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		// AddControl auto-focuses the first interactive control through the coordinator
		Assert.Single(window.FocusManager.FocusPath);
		Assert.Equal(btn, window.FocusManager.FocusedControl);
	}

	[Fact]
	public void FocusPath_SingleControl_ContainsOnlyThatControl()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		var btn = new ButtonControl { Text = "Btn" };
		window.AddControl(btn);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		window.SwitchFocus(backward: false);

		var path = window.FocusManager.FocusPath;
		Assert.Single(path);
		Assert.Same(btn, path[0]);
		Assert.Same(btn, window.FocusManager.FocusedControl);
	}

	[Fact]
	public void FocusPath_ControlInsideSPC_ContainsSPCAndLeaf()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn = new ButtonControl { Text = "Btn" };
		panel.AddControl(btn);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		// If SPC entered scroll mode, Tab to focus child
		if (!btn.HasFocus)
			panel.ProcessKey(TabKey);

		var path = window.FocusManager.FocusPath;

		// Path should include at minimum the panel and the button
		Assert.True(path.Count >= 2, $"Path should have >= 2 entries, got {path.Count}: [{string.Join(", ", path.Select(c => c.GetType().Name))}]");
		Assert.Same(btn, window.FocusManager.FocusedControl);

		// Button should be the last entry
		Assert.Same(btn, path[^1]);

		// Panel should be in the path
		Assert.True(window.FocusManager.IsInFocusPath(panel),
			"SPC should be in focus path");
	}

	[Fact]
	public void FocusPath_DeepNesting_FullChain()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);
		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var btn = new ButtonControl { Text = "Deep" };
		innerPanel.AddControl(btn);
		col.AddContent(innerPanel);
		grid.AddColumn(col);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		// Tab through until button is focused
		for (int i = 0; i < 5 && !btn.HasFocus; i++)
		{
			if (!outerPanel.ProcessKey(TabKey))
				window.SwitchFocus(backward: false);
		}

		Assert.True(btn.HasFocus, "Button should be focused");

		var path = window.FocusManager.FocusPath;

		// Button is the leaf
		Assert.Same(btn, window.FocusManager.FocusedControl);

		// All containers in the chain should be in the path
		Assert.True(window.FocusManager.IsInFocusPath(btn), "Button should be in path");
		Assert.True(window.FocusManager.IsInFocusPath(outerPanel), "Outer panel should be in path");
	}

	#endregion

	#region GetFocusedChild

	[Fact]
	public void GetFocusedChild_ReturnsCorrectChild()
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
		Assert.True(btn1.HasFocus, "btn1 should be focused");

		var focusedChild = window.FocusManager.FocusPath.SkipWhile(c => !ReferenceEquals(c, panel)).Skip(1).FirstOrDefault();
		Assert.Same(btn1, focusedChild);
	}

	[Fact]
	public void GetFocusedChild_ReturnsNull_WhenContainerNotInPath()
	{
		// Use programmatic focus to go through coordinator
		var panel1 = new ScrollablePanelControl { Height = 10 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		panel1.AddControl(btn1);

		var panel2 = new ScrollablePanelControl { Height = 10 };
		var btn2 = new ButtonControl { Text = "Btn2" };
		panel2.AddControl(btn2);

		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		window.AddControl(panel1);
		window.AddControl(panel2);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		// Focus btn1 via coordinator
		window.FocusControl(btn1);

		// panel2 is not in the focus path — its focused child should be null
		var focusedChild = window.FocusManager.FocusPath.SkipWhile(c => !ReferenceEquals(c, panel2)).Skip(1).FirstOrDefault();
		Assert.Null(focusedChild);
	}

	#endregion

	#region IsInFocusPath

	[Fact]
	public void IsInFocusPath_TrueForAncestors_FalseForSiblings()
	{
		// Use programmatic focus to go through coordinator
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		window.AddControl(btn1);
		window.AddControl(btn2);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		window.FocusControl(btn1);
		Assert.True(btn1.HasFocus, "btn1 should be focused");

		// btn1 should be in path
		Assert.True(window.FocusManager.IsInFocusPath(btn1), "btn1 should be in path");

		// btn2 should NOT be in path
		Assert.False(window.FocusManager.IsInFocusPath(btn2), "btn2 should NOT be in path");
	}

	#endregion

	#region Focus path updates on Tab

	[Fact]
	public void FocusPath_UpdatesOnFocusControl()
	{
		// Programmatic FocusControl goes through coordinator
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		window.AddControl(btn1);
		window.AddControl(btn2);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		window.FocusControl(btn1);
		Assert.Same(btn1, window.FocusManager.FocusedControl);

		// Switch focus to btn2
		window.FocusControl(btn2);
		Assert.Same(btn2, window.FocusManager.FocusedControl);
		Assert.True(window.FocusManager.IsInFocusPath(btn2));
		Assert.False(window.FocusManager.IsInFocusPath(btn1));
	}

	#endregion

	#region Focus path on clear

	[Fact]
	public void FocusPath_ClearedOnUnfocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = 80, Height = 20
		};
		var btn = new ButtonControl { Text = "Btn" };
		window.AddControl(btn);
		system.AddWindow(window);
		system.Render.UpdateDisplay();

		window.SwitchFocus(backward: false);
		Assert.NotEmpty(window.FocusManager.FocusPath);

		window.UnfocusCurrentControl();
		Assert.Empty(window.FocusManager.FocusPath);
		Assert.Null(window.FocusManager.FocusedControl);
	}

	#endregion
}
