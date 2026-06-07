// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

public class MultilineEditSelectionManagerTests
{
	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	private static void Paint(MarkupControl control)
	{
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void Editor_ImplementsISelectableControl()
	{
		var editor = new MultilineEditControl { Content = "hello world" };
		Assert.IsAssignableFrom<ISelectableControl>(editor);
	}

	[Fact]
	public void EditorSelection_BecomesActiveSelection()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var editor = new MultilineEditControl { Content = "hello world" };
		window.AddControl(editor);

		editor.SelectRange(0, 0, 0, 5);

		Assert.True(editor.HasSelection);
		Assert.Same(editor, window.SelectionManager.ActiveSelection);
		Assert.Equal("hello", window.SelectionManager.GetSelectedText());
	}

	[Fact]
	public void MarkupSelection_ClearsEditorSelection()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var editor = new MultilineEditControl { Content = "hello world" };
		var markup = new MarkupControl(new List<string> { "AAAA BBBB" }) { EnableSelection = true };
		window.AddControl(editor);
		window.AddControl(markup);
		Paint(markup);

		editor.SelectRange(0, 0, 0, 5);
		Assert.True(editor.HasSelection);

		// Now select in the markup control — single-selection invariant clears the editor.
		markup.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		markup.ProcessMouseEvent(Mouse(4, 0, MouseFlags.Button1Dragged));

		Assert.True(markup.HasSelection);
		Assert.False(editor.HasSelection);
		Assert.Same(markup, window.SelectionManager.ActiveSelection);
	}

	[Fact]
	public void ReadOnlyEditor_DragSelectsText()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var editor = new MultilineEditControl { Content = "hello world", ReadOnly = true };
		window.AddControl(editor);
		window.FocusManager.SetFocus(editor, SharpConsoleUI.Controls.FocusReason.Programmatic);

		// Press + drag on a read-only editor must build a selection (issue #36).
		editor.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		editor.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));

		Assert.True(editor.HasSelection);
		Assert.False(editor.IsEditing); // read-only never enters edit mode
		Assert.Equal("hello", editor.GetSelectedText());
		Assert.Same(editor, window.SelectionManager.ActiveSelection);
	}
}
