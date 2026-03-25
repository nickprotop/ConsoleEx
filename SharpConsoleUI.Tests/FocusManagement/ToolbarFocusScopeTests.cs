// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

public class ToolbarFocusScopeTests
{
	#region Key helpers

	private static readonly ConsoleKeyInfo TabKey = new('\t', ConsoleKey.Tab, false, false, false);
	private static readonly ConsoleKeyInfo ShiftTabKey = new('\t', ConsoleKey.Tab, true, false, false);
	private static readonly ConsoleKeyInfo RightArrow = new('\0', ConsoleKey.RightArrow, false, false, false);
	private static readonly ConsoleKeyInfo LeftArrow = new('\0', ConsoleKey.LeftArrow, false, false, false);
	private static readonly ConsoleKeyInfo EnterKey = new('\r', ConsoleKey.Enter, false, false, false);

	private static void SendKey(Window window, ConsoleKeyInfo key)
		=> window.EventDispatcher!.ProcessInput(key);

	#endregion

	#region Setup helpers

	private static (ConsoleWindowSystem system, Window window) Setup()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };
		return (system, window);
	}

	private static void Activate(ConsoleWindowSystem system, Window window)
	{
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	#endregion

	#region IFocusScope contract tests (original)

	[Fact]
	public void GetNextFocus_AlwaysReturnsNull_TabExitsImmediately()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var toolbar = new ToolbarControl();
		var item1 = new ButtonControl { Text = "File" };
		var item2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(item1);
		toolbar.AddItem(item2);
		window.AddControl(toolbar);

		var scope = (IFocusScope)toolbar;
		Assert.Null(scope.GetNextFocus(item1, backward: false));
		Assert.Null(scope.GetNextFocus(item2, backward: false));
		Assert.Null(scope.GetNextFocus(item1, backward: true));
	}

	[Fact]
	public void GetInitialFocus_ReturnsSavedFocus_WhenSet()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var toolbar = new ToolbarControl();
		var item1 = new ButtonControl { Text = "File" };
		var item2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(item1);
		toolbar.AddItem(item2);
		window.AddControl(toolbar);

		var scope = (IFocusScope)toolbar;
		scope.SavedFocus = item2;
		Assert.Equal(item2, scope.GetInitialFocus(backward: false));
		Assert.Null(scope.SavedFocus); // consumed after one use
	}

	[Fact]
	public void GetInitialFocus_ReturnsFirstItem_WhenNoSavedFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var toolbar = new ToolbarControl();
		var item1 = new ButtonControl { Text = "File" };
		toolbar.AddItem(item1);
		window.AddControl(toolbar);

		var scope = (IFocusScope)toolbar;
		Assert.Equal(item1, scope.GetInitialFocus(backward: false));
	}

	[Fact]
	public void GetInitialFocus_ReturnsLastItem_WhenBackwardAndNoSavedFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var toolbar = new ToolbarControl();
		var item1 = new ButtonControl { Text = "File" };
		var item2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(item1);
		toolbar.AddItem(item2);
		window.AddControl(toolbar);

		var scope = (IFocusScope)toolbar;
		Assert.Equal(item2, scope.GetInitialFocus(backward: true));
	}

	[Fact]
	public void GetInitialFocus_ReturnsSavedFocus_IgnoresBackwardDirection()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var toolbar = new ToolbarControl();
		var item1 = new ButtonControl { Text = "File" };
		var item2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(item1);
		toolbar.AddItem(item2);
		window.AddControl(toolbar);

		var scope = (IFocusScope)toolbar;
		scope.SavedFocus = item2;
		Assert.Equal(item2, scope.GetInitialFocus(backward: true));
		Assert.Null(scope.SavedFocus);
	}

	#endregion

	#region Arrow key navigation

	[Fact]
	public void ArrowRight_MovesFocusBetweenButtons()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "File" };
		var btn2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(btn1);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		// Focus first button via Tab into toolbar
		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);
		Assert.True(window.FocusManager.IsFocused(btn1));

		SendKey(window, RightArrow);
		Assert.True(window.FocusManager.IsFocused(btn2));
	}

	[Fact]
	public void ArrowLeft_MovesFocusBackward()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "File" };
		var btn2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(btn1);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(btn2, FocusReason.Keyboard);

		SendKey(window, LeftArrow);
		Assert.True(window.FocusManager.IsFocused(btn1));
	}

	[Fact]
	public void ArrowRight_AtLastButton_StaysOnLastButton()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "File" };
		var btn2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(btn1);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(btn2, FocusReason.Keyboard);

		SendKey(window, RightArrow);
		// NavigateFocus returns false at boundary, key falls through to window
		// but focus should not have moved to btn1
		Assert.False(window.FocusManager.IsFocused(btn1));
	}

	[Fact]
	public void ArrowLeft_AtFirstButton_StaysOnFirstButton()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "File" };
		var btn2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(btn1);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);

		SendKey(window, LeftArrow);
		Assert.False(window.FocusManager.IsFocused(btn2));
	}

	#endregion

	#region Tab entry/exit

	[Fact]
	public void Tab_EntersToolbar_FocusesFirstButton()
	{
		var (system, window) = Setup();
		var beforeBtn = new ButtonControl { Text = "Before" };
		var toolbar = new ToolbarControl();
		var tbBtn1 = new ButtonControl { Text = "File" };
		var tbBtn2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(tbBtn1);
		toolbar.AddItem(tbBtn2);
		window.AddControl(beforeBtn);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(beforeBtn, FocusReason.Keyboard);

		SendKey(window, TabKey);
		Assert.True(window.FocusManager.IsFocused(tbBtn1));
	}

	[Fact]
	public void ShiftTab_EntersToolbar_FocusesLastButton()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var tbBtn1 = new ButtonControl { Text = "File" };
		var tbBtn2 = new ButtonControl { Text = "Edit" };
		toolbar.AddItem(tbBtn1);
		toolbar.AddItem(tbBtn2);
		var afterBtn = new ButtonControl { Text = "After" };
		window.AddControl(toolbar);
		window.AddControl(afterBtn);
		Activate(system, window);

		window.FocusManager.SetFocus(afterBtn, FocusReason.Keyboard);

		SendKey(window, ShiftTabKey);
		Assert.True(window.FocusManager.IsFocused(tbBtn2));
	}

	[Fact]
	public void Tab_ExitsToolbar_Forward()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var tbBtn1 = new ButtonControl { Text = "File" };
		toolbar.AddItem(tbBtn1);
		var afterBtn = new ButtonControl { Text = "After" };
		window.AddControl(toolbar);
		window.AddControl(afterBtn);
		Activate(system, window);

		window.FocusManager.SetFocus(tbBtn1, FocusReason.Keyboard);

		SendKey(window, TabKey);
		Assert.True(window.FocusManager.IsFocused(afterBtn));
	}

	[Fact]
	public void ShiftTab_ExitsToolbar_Backward()
	{
		var (system, window) = Setup();
		var beforeBtn = new ButtonControl { Text = "Before" };
		var toolbar = new ToolbarControl();
		var tbBtn1 = new ButtonControl { Text = "File" };
		toolbar.AddItem(tbBtn1);
		window.AddControl(beforeBtn);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(tbBtn1, FocusReason.Keyboard);

		SendKey(window, ShiftTabKey);
		Assert.True(window.FocusManager.IsFocused(beforeBtn));
	}

	#endregion

	#region Interaction

	[Fact]
	public void Enter_ActivatesButton()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "File" };
		bool clicked = false;
		btn1.Click += (_, _) => clicked = true;
		toolbar.AddItem(btn1);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);

		SendKey(window, EnterKey);
		Assert.True(clicked);
	}

	[Fact]
	public void HasFocus_True_WhenChildHasFocus()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "File" };
		toolbar.AddItem(btn1);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);

		Assert.True(toolbar.HasFocus);
	}

	#endregion

	#region Mixed control types

	[Fact]
	public void PromptControl_KeepsArrowKeys_ToolbarDoesNotNavigate()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var prompt = new PromptControl { Prompt = "> ", InputWidth = 20 };
		var btn2 = new ButtonControl { Text = "OK" };
		toolbar.AddItem(prompt);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		// Type some text into the prompt so RightArrow has room to move cursor
		window.FocusManager.SetFocus(prompt, FocusReason.Keyboard);
		// Type 'h'
		SendKey(window, new ConsoleKeyInfo('h', ConsoleKey.H, false, false, false));

		// Now cursor is at pos 1. Send LeftArrow — PromptControl should consume it
		SendKey(window, LeftArrow);

		// Focus should still be on prompt, not navigated to btn2
		Assert.True(window.FocusManager.IsFocused(prompt));
		Assert.False(window.FocusManager.IsFocused(btn2));
	}

	[Fact]
	public void CheckboxControl_ArrowsNavigateToolbar()
	{
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var checkbox = new CheckboxControl { Label = "Option" };
		var btn2 = new ButtonControl { Text = "OK" };
		toolbar.AddItem(checkbox);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(checkbox, FocusReason.Keyboard);

		SendKey(window, RightArrow);
		Assert.True(window.FocusManager.IsFocused(btn2));
	}

	#endregion

	#region Key bubbling

	[Fact]
	public void KeyBubbles_FromButton_ToToolbar()
	{
		// The arrow key navigation tests already prove bubbling works
		// (dispatcher sends arrow to button → button returns false → bubbles to toolbar → toolbar navigates).
		// This test explicitly verifies the mechanism: a button inside a toolbar
		// that doesn't handle RightArrow causes the toolbar to navigate.
		var (system, window) = Setup();
		var toolbar = new ToolbarControl();
		var btn1 = new ButtonControl { Text = "A" };
		var btn2 = new ButtonControl { Text = "B" };
		toolbar.AddItem(btn1);
		toolbar.AddItem(btn2);
		window.AddControl(toolbar);
		Activate(system, window);

		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);

		// RightArrow: button doesn't handle it → bubbles to toolbar → toolbar navigates
		SendKey(window, RightArrow);
		Assert.True(window.FocusManager.IsFocused(btn2),
			"Expected RightArrow to bubble from button to toolbar and navigate to btn2");

		// Navigate back
		SendKey(window, LeftArrow);
		Assert.True(window.FocusManager.IsFocused(btn1),
			"Expected LeftArrow to bubble from button to toolbar and navigate to btn1");
	}

	#endregion
}
