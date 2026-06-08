// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Comprehensive matrix for the terminal-cursor visibility/position contract of a focusable,
/// cursor-providing child (PromptControl / MultilineEditControl) hosted inside a
/// ScrollablePanelControl.
///
/// The window-level cursor logic (WindowEventDispatcher.HasInteractiveContent →
/// TranslateLogicalCursorToWindow + IsCursorPositionVisible) is unaware of the panel's INTERNAL
/// scroll. The panel must therefore report the cursor honestly: visible and correctly positioned
/// when the focused child is within the viewport, and hidden when it has scrolled out of view.
///
/// These tests define the correct behavior across the full range of cases so the fix can be
/// verified and the planned ScrollLayout refactor stays behavior-preserving.
/// </summary>
public class ScrollablePanelCursorVisibilityTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelCursorVisibilityTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	#region Helpers

	private static (ScrollablePanelControl panel, Window window) Build(
		int panelHeight, int rowsAbove, out PromptControl prompt, int rowsBelow = 0,
		BorderStyle border = BorderStyle.None)
	{
		var panel = new ScrollablePanelControl { Height = panelHeight, BorderStyle = border };
		for (int i = 0; i < rowsAbove; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"above{i}" }));
		prompt = new PromptControl { Prompt = "> " };
		panel.AddControl(prompt);
		for (int i = 0; i < rowsBelow; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"below{i}" }));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, window);
	}

	private static bool CursorVisible(Window window, out System.Drawing.Point pos)
		=> window.EventDispatcher!.HasInteractiveContent(out pos);

	#endregion

	#region Visible when in view

	[Fact]
	public void Cursor_Visible_WhenFocusedPromptIsInViewport()
	{
		var (panel, window) = Build(panelHeight: 10, rowsAbove: 2, out var prompt);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"pos={pos} promptY={prompt.ActualY} scroll={panel.VerticalScrollOffset}");
		Assert.True(visible, "Cursor must be visible when the focused prompt is in the viewport.");
	}

	[Fact]
	public void Cursor_Visible_AfterScrollingPromptIntoView()
	{
		var (panel, window) = Build(panelHeight: 5, rowsAbove: 12, out var prompt);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		panel.ScrollChildIntoView(prompt);
		window.RenderAndGetVisibleContent();

		Assert.True(CursorVisible(window, out _), "Cursor visible once the prompt is scrolled into view.");
	}

	#endregion

	#region Hidden when scrolled out of view

	[Fact]
	public void Cursor_Hidden_WhenFocusedPromptScrolledBelowViewport()
	{
		// Prompt below many rows; bring into view, then scroll back to top so it's below the viewport.
		var (panel, window) = Build(panelHeight: 5, rowsAbove: 12, out var prompt);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		panel.ScrollChildIntoView(prompt);
		window.RenderAndGetVisibleContent();
		Assert.True(CursorVisible(window, out _), "precondition: visible in view");

		panel.ScrollToTop();
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"after ScrollToTop: visible={visible} pos={pos} promptY={prompt.ActualY} scroll={panel.VerticalScrollOffset} viewport={panel.ViewportHeight}");
		Assert.False(visible, "Cursor must be hidden when the focused prompt is below the viewport.");
	}

	[Fact]
	public void Cursor_Hidden_WhenFocusedPromptScrolledAboveViewport()
	{
		// Prompt near the top; scroll down past it so it's above the viewport.
		var (panel, window) = Build(panelHeight: 5, rowsAbove: 1, out var prompt, rowsBelow: 15);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();
		Assert.True(CursorVisible(window, out _), "precondition: visible at top");

		panel.ScrollVerticalBy(10); // push prompt above the viewport
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"scrolled down: visible={visible} pos={pos} promptY={prompt.ActualY} scroll={panel.VerticalScrollOffset}");
		Assert.False(visible, "Cursor must be hidden when the focused prompt is above the viewport.");
	}

	[Fact]
	public void Cursor_Visible_FocusingOffViewportPrompt_AutoScrollsItIntoView()
	{
		// Focusing a child below the viewport auto-scrolls it into view, so its cursor becomes
		// visible. (Counterpart to the scrolled-away-after-focus cases below.)
		var (panel, window) = Build(panelHeight: 4, rowsAbove: 20, out var prompt);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"auto-scrolled: visible={visible} pos={pos} promptY={prompt.ActualY} scroll={panel.VerticalScrollOffset}");
		Assert.True(panel.VerticalScrollOffset > 0, "Focusing the off-view prompt should auto-scroll it into view.");
		Assert.True(visible, "Cursor visible after the focused prompt is auto-scrolled into view.");
	}

	#endregion

	#region Position correctness (when visible)

	[Fact]
	public void CursorPosition_RowMatchesPromptViewportRow()
	{
		var (panel, window) = Build(panelHeight: 10, rowsAbove: 3, out var prompt);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		Assert.True(CursorVisible(window, out var pos), "precondition: visible");
		// Prompt is the 4th control (3 single-row markups above) → content row 3, scroll 0.
		// Window border adds +1, so window-Y should be 3 + 1 = 4.
		_out.WriteLine($"pos={pos}");
		Assert.Equal(4, pos.Y);
	}

	[Fact]
	public void CursorPosition_RowTracksScrollOffset()
	{
		var (panel, window) = Build(panelHeight: 6, rowsAbove: 4, out var prompt, rowsBelow: 10);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();
		CursorVisible(window, out var posBefore);

		panel.ScrollVerticalBy(2); // prompt moves up by 2 rows on screen
		window.RenderAndGetVisibleContent();

		if (CursorVisible(window, out var posAfter))
		{
			_out.WriteLine($"before={posBefore} after={posAfter}");
			Assert.Equal(posBefore.Y - 2, posAfter.Y);
		}
		// (If the scroll pushed it off-view, that's covered by the hidden-above test.)
	}

	#endregion

	#region Border / padding offsets

	[Fact]
	public void Cursor_Visible_WithBorderedPanel_AccountsForInset()
	{
		var (panel, window) = Build(panelHeight: 10, rowsAbove: 1, out var prompt, border: BorderStyle.Single);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"bordered: visible={visible} pos={pos} promptY={prompt.ActualY}");
		Assert.True(visible, "Cursor visible in a bordered panel when the prompt is in view.");
	}

	[Fact]
	public void Cursor_Hidden_WithBorderedPanel_WhenScrolledAway()
	{
		var (panel, window) = Build(panelHeight: 5, rowsAbove: 12, out var prompt, border: BorderStyle.Single);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		panel.ScrollChildIntoView(prompt);
		window.RenderAndGetVisibleContent();
		Assert.True(CursorVisible(window, out _), "precondition: visible in view (bordered)");

		panel.ScrollToTop();
		window.RenderAndGetVisibleContent();
		Assert.False(CursorVisible(window, out _), "Cursor hidden when scrolled away in a bordered panel.");
	}

	#endregion

	#region MultilineEdit (multi-row cursor provider)

	[Fact]
	public void Cursor_Hidden_WhenEditorScrolledOutOfView()
	{
		var panel = new ScrollablePanelControl { Height = 5 };
		for (int i = 0; i < 12; i++) panel.AddControl(new MarkupControl(new List<string> { $"r{i}" }));
		var edit = new MultilineEditControl { ViewportHeight = 3, IsEditing = true };
		edit.SetContent("alpha\nbeta\ngamma");
		panel.AddControl(edit);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(edit, FocusReason.Programmatic);
		panel.ScrollChildIntoView(edit);
		window.RenderAndGetVisibleContent();
		Assert.True(CursorVisible(window, out _), "precondition: editor cursor visible in view");

		panel.ScrollToTop();
		window.RenderAndGetVisibleContent();
		Assert.False(CursorVisible(window, out _), "Editor cursor must hide when scrolled out of view.");
	}

	/// <summary>
	/// A tall, content-sized MLE inside a smaller panel: its cursor row is near the TOP of the
	/// editor (visible in the panel viewport) → cursor shown.
	/// </summary>
	[Fact]
	public void Cursor_Visible_WhenEditorCursorRowInsidePanelViewport()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		// Content-sized editor (ViewportHeight large enough to not scroll internally) with many lines.
		var edit = new MultilineEditControl { ViewportHeight = 30, IsEditing = true };
		edit.SetContent(string.Join("\n", Enumerable.Range(0, 20).Select(i => $"line{i}")));
		panel.AddControl(edit);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(edit, FocusReason.Programmatic);
		edit.GoToLine(1); // near the top
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"top-line: visible={visible} pos={pos} editScroll-ish panelScroll={panel.VerticalScrollOffset}");
		Assert.True(visible, "Cursor at the top of a tall editor should be visible in the panel viewport.");
	}

	/// <summary>
	/// The user's case: the editor's cursor moves toward its END, past the bottom of the panel
	/// viewport. The terminal cursor must NOT remain painted below the visible panel area.
	/// </summary>
	[Fact]
	public void Cursor_Hidden_WhenEditorCursorRowBelowPanelViewport()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		// Content-sized editor that does NOT scroll internally (large own viewport), so moving the
		// cursor down moves its row down in the panel's content space.
		var edit = new MultilineEditControl { ViewportHeight = 30, IsEditing = true };
		edit.SetContent(string.Join("\n", Enumerable.Range(0, 20).Select(i => $"line{i}")));
		panel.AddControl(edit);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(edit, FocusReason.Programmatic);

		// Move the cursor to the end (line 19) — well below the 6-row panel viewport.
		edit.GoToEnd();
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"end-line: visible={visible} pos={pos} panelScroll={panel.VerticalScrollOffset} editActualY={edit.ActualY} viewport={panel.ViewportHeight}");

		// Correct behavior: either the panel scrolled so the cursor row is in view (visible &
		// in-viewport), OR the cursor is hidden. It must NEVER be reported visible at a row
		// outside the panel viewport.
		if (visible)
		{
			int panelRelative = pos.Y - 1 - panel.ActualY; // window→content→panel-relative
			Assert.InRange(panelRelative, 0, panel.ViewportHeight - 1);
		}
		// If not visible, that's acceptable too (cursor row clipped, not scrolled in).
	}

	/// <summary>
	/// An MLE with a small fixed ViewportHeight scrolls INTERNALLY to keep its cursor visible
	/// within its own viewport, which sits inside the panel viewport. Moving to the end keeps the
	/// cursor visible (the editor scrolled, not the panel).
	/// </summary>
	[Fact]
	public void Cursor_Visible_WhenEditorScrollsInternallyToKeepCursorInOwnViewport()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		var edit = new MultilineEditControl { ViewportHeight = 4, IsEditing = true }; // small → scrolls internally
		edit.SetContent(string.Join("\n", Enumerable.Range(0, 20).Select(i => $"line{i}")));
		panel.AddControl(edit);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(edit, FocusReason.Programmatic);

		edit.GoToEnd();
		edit.EnsureCursorVisible();
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"internal-scroll end: visible={visible} pos={pos} editViewport=4 panelH=10");
		Assert.True(visible, "Editor that scrolls internally keeps its cursor visible within the panel.");
		int panelRelative = pos.Y - 1 - panel.ActualY;
		Assert.InRange(panelRelative, 0, panel.ViewportHeight - 1);
	}

	#endregion

	#region Unfocused

	[Fact]
	public void Cursor_Hidden_WhenPromptNotFocused()
	{
		var (panel, window) = Build(panelHeight: 10, rowsAbove: 1, out var prompt);
		// Explicitly clear focus so nothing is focused.
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"focused={window.FocusManager.FocusedControl?.GetType().Name ?? "null"} promptFocus={prompt.HasFocus}");
		Assert.False(prompt.HasFocus, "Prompt should not be focused in this test.");
		Assert.False(CursorVisible(window, out _), "No cursor when nothing is focused.");
	}

	#endregion
}
