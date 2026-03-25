// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Tests for IInteractiveControl.WantsTabKey — controls opt in to receive
/// Tab/Shift+Tab instead of having them intercepted for focus traversal.
/// </summary>
public class WantsTabKeyTests
{
	private readonly ITestOutputHelper _out;
	public WantsTabKeyTests(ITestOutputHelper output) => _out = output;

	private static readonly ConsoleKeyInfo TabKey = new('\t', ConsoleKey.Tab, false, false, false);
	private static readonly ConsoleKeyInfo ShiftTabKey = new('\t', ConsoleKey.Tab, true, false, false);

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

	/// <summary>
	/// Sends a key through the full dispatcher pipeline (not SwitchFocus which bypasses WantsTabKey).
	/// </summary>
	private static void SendKey(Window window, ConsoleKeyInfo key)
	{
		window.EventDispatcher!.ProcessInput(key);
	}

	#region Default WantsTabKey behavior

	[Fact]
	public void DefaultWantsTabKey_IsFalse()
	{
		var button = new ButtonControl { Text = "Test" };
		Assert.False(((IInteractiveControl)button).WantsTabKey);
	}

	[Fact]
	public void ListControl_WantsTabKey_IsFalse()
	{
		var list = new ListControl(new[] { "A", "B" });
		Assert.False(((IInteractiveControl)list).WantsTabKey);
	}

	#endregion

	#region MultilineEditControl WantsTabKey

	[Fact]
	public void MultilineEdit_ViewMode_WantsTabKeyFalse()
	{
		var editor = new MultilineEditControl();
		Assert.False(editor.IsEditing);
		Assert.False(editor.WantsTabKey);
	}

	[Fact]
	public void MultilineEdit_EditMode_WantsTabKeyTrue()
	{
		var editor = new MultilineEditControl();
		editor.IsEditing = true;
		Assert.True(editor.WantsTabKey);
	}

	[Fact]
	public void MultilineEdit_EditModeReadOnly_WantsTabKeyFalse()
	{
		var editor = new MultilineEditControl { ReadOnly = true };
		editor.IsEditing = true;
		Assert.False(editor.WantsTabKey);
	}

	#endregion

	#region Tab inserts indent in edit mode

	[Fact]
	public void EditMode_TabInsertsSpaces_FocusDoesNotMove()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Before" };
		var editor = new MultilineEditControl();
		editor.Content = "hello";
		editor.IsEditing = true;
		var btn2 = new ButtonControl { Text = "After" };

		window.AddControl(btn);
		window.AddControl(editor);
		window.AddControl(btn2);
		Activate(system, window);

		// Focus editor
		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);

		// Press Tab through dispatcher — should indent, NOT move focus
		SendKey(window, TabKey);

		// Editor should still have focus (Tab was consumed for indentation)
		Assert.True(editor.HasFocus, "Editor should still have focus after Tab in edit mode");
		// Content should have spaces inserted
		Assert.Contains("    ", editor.Content);
		_out.WriteLine($"Content after Tab: '{editor.Content}'");
	}

	[Fact]
	public void EditMode_ShiftTabDedents_FocusDoesNotMove()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Before" };
		var editor = new MultilineEditControl();
		editor.Content = "    hello";
		editor.IsEditing = true;

		window.AddControl(btn);
		window.AddControl(editor);
		Activate(system, window);

		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);

		// Press Shift+Tab through dispatcher — should dedent
		SendKey(window, ShiftTabKey);

		Assert.True(editor.HasFocus, "Editor should still have focus after Shift+Tab in edit mode");
		Assert.Equal("hello", editor.Content.TrimEnd('\n'));
		_out.WriteLine($"Content after Shift+Tab: '{editor.Content}'");
	}

	#endregion

	#region View mode Tab moves focus

	[Fact]
	public void ViewMode_TabMovesFocus()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Before" };
		var editor = new MultilineEditControl();
		editor.Content = "hello";
		// IsEditing defaults to false (view mode)
		var btn2 = new ButtonControl { Text = "After" };

		window.AddControl(btn);
		window.AddControl(editor);
		window.AddControl(btn2);
		Activate(system, window);

		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);
		Assert.False(editor.IsEditing);

		// Tab should move focus to btn2 (view mode, WantsTabKey=false)
		SendKey(window, TabKey);

		Assert.False(editor.HasFocus, "Editor should lose focus on Tab in view mode");
		Assert.True(btn2.HasFocus, "btn2 should gain focus");
	}

	[Fact]
	public void ViewMode_ShiftTabMovesFocusBackward()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Before" };
		var editor = new MultilineEditControl();
		editor.Content = "hello";

		window.AddControl(btn);
		window.AddControl(editor);
		Activate(system, window);

		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);

		// Shift+Tab should move focus backward to btn
		SendKey(window, ShiftTabKey);

		Assert.False(editor.HasFocus);
		Assert.True(btn.HasFocus, "btn should gain focus on Shift+Tab in view mode");
	}

	#endregion

	#region Shift+Tab consumed even when nothing to dedent

	[Fact]
	public void EditMode_ShiftTabNothingToDedent_StillConsumed()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Before" };
		var editor = new MultilineEditControl();
		editor.Content = "hello"; // no leading spaces — Shift+Tab has nothing to remove
		editor.IsEditing = true;

		window.AddControl(btn);
		window.AddControl(editor);
		Activate(system, window);

		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);

		// Shift+Tab with no leading spaces — still consumed in edit mode (no accidental focus escape)
		SendKey(window, ShiftTabKey);

		Assert.True(editor.HasFocus, "Shift+Tab should be consumed in edit mode even with nothing to dedent");
	}

	#endregion

	#region ReadOnly editor Tab moves focus

	[Fact]
	public void ReadOnlyEditMode_TabMovesFocus()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Before" };
		var editor = new MultilineEditControl { ReadOnly = true };
		editor.Content = "hello";
		editor.IsEditing = true; // Even in edit mode, readonly Tab doesn't insert
		var btn2 = new ButtonControl { Text = "After" };

		window.AddControl(btn);
		window.AddControl(editor);
		window.AddControl(btn2);
		Activate(system, window);

		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);
		Assert.False(editor.WantsTabKey); // ReadOnly → WantsTabKey=false

		SendKey(window, TabKey);

		Assert.False(editor.HasFocus, "Readonly editor should lose focus on Tab");
		Assert.True(btn2.HasFocus);
	}

	#endregion

	#region Escape exits edit mode, then Tab moves focus

	[Fact]
	public void EscapeThenTab_ExitsEditModeThenMovesFocus()
	{
		var (system, window) = Setup();

		var btn = new ButtonControl { Text = "Target" };
		var editor = new MultilineEditControl { EscapeExitsEditMode = true };
		editor.Content = "hello";
		editor.IsEditing = true;

		window.AddControl(editor);
		window.AddControl(btn);
		Activate(system, window);

		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.IsEditing);
		Assert.True(editor.WantsTabKey);

		// Press Escape — exits edit mode
		var escKey = new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false);
		editor.ProcessKey(escKey);

		Assert.False(editor.IsEditing, "Escape should exit edit mode");
		Assert.False(editor.WantsTabKey, "WantsTabKey should be false after exiting edit mode");

		// Now Tab should move focus
		SendKey(window, TabKey);
		Assert.True(btn.HasFocus, "Tab should move focus after Escape exited edit mode");
	}

	#endregion

	#region Multiple editors — Tab stays within editing editor

	[Fact]
	public void TwoEditors_TabStaysInEditingOne()
	{
		var (system, window) = Setup();

		var editor1 = new MultilineEditControl { Name = "Editor1" };
		editor1.Content = "first";
		editor1.IsEditing = true;

		var editor2 = new MultilineEditControl { Name = "Editor2" };
		editor2.Content = "second";
		// editor2 is NOT in edit mode

		window.AddControl(editor1);
		window.AddControl(editor2);
		Activate(system, window);

		window.FocusManager.SetFocus(editor1, FocusReason.Keyboard);
		Assert.True(editor1.HasFocus);

		// Tab should stay in editor1 (edit mode)
		SendKey(window, TabKey);
		Assert.True(editor1.HasFocus, "Tab should stay in editing editor1");

		// Exit edit mode
		editor1.IsEditing = false;

		// Now Tab should move to editor2
		SendKey(window, TabKey);
		Assert.True(editor2.HasFocus, "Tab should move to editor2 after exiting edit mode");
	}

	#endregion
}
