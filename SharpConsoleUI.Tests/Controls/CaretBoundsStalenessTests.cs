// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The text cursor must keep following its control after the window has seen a mouse event.
/// <para>
/// <b>The defect.</b> <c>ControlBounds.ControlContentBounds</c> is a cache with exactly one writer,
/// <c>WindowEventDispatcher.UpdateControlLayout()</c>, whose only call site is inside
/// <c>ProcessMouseEvent</c>. Both caret readers preferred that cache over the live layout node,
/// guarded on <i>the cache being empty</i> rather than on <i>the control being nested</i>. So a
/// top-level control read its position fresh from the layout node right up until the first mouse
/// event populated the cache — and never again afterwards. The caret froze wherever the pointer had
/// last seen it while the control kept being painted somewhere else.
/// </para>
/// <para>
/// That is why moving the pointer over the terminal "fixed" it and typing did not: crossing the
/// window delivers a mouse event, which refreshes the cache, which immediately goes stale again.
/// </para>
/// </summary>
public class CaretBoundsStalenessTests
{
	/// <summary>
	/// A focused prompt sitting under a markup block that grows a line at a time. Growing the block
	/// pushes the prompt down the window, which is the movement the caret has to follow.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, MarkupControl spacer, PromptControl prompt) Host()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "caret", Left = 0, Top = 0, Width = 60, Height = 20 };

		var spacer = new MarkupControl(new List<string> { "line" });
		var prompt = new PromptControl { Prompt = "> " };

		window.AddControl(spacer);
		window.AddControl(prompt);
		system.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		return (system, window, spacer, prompt);
	}

	/// <summary>Adds <paramref name="rows"/> lines above the prompt and re-renders.</summary>
	private static void Grow(ConsoleWindowSystem system, Window window, MarkupControl spacer, int rows)
	{
		var lines = new List<string>();
		for (int i = 0; i < rows; i++) lines.Add($"line {i}");
		spacer.SetContent(lines);
		window.Invalidate(Invalidation.Relayout);
		system.Render.UpdateDisplay();
	}

	private static void DeliverOneMouseEvent(Window window)
	{
		var pos = new Point(3, 3);
		window.EventDispatcher!.ProcessMouseEvent(
			new MouseEventArgs(new List<MouseFlags> { MouseFlags.ReportMousePosition }, pos, pos, pos));
	}

	[Fact]
	public void Caret_FollowsTheControl_AfterTheWindowHasSeenAMouseEvent()
	{
		var (system, window, spacer, prompt) = Host();

		// Populate the bounds cache exactly the way a real session does: one pointer movement.
		DeliverOneMouseEvent(window);
		system.Render.UpdateDisplay();

		var before = window.GetCursorContentPosition(prompt);
		Assert.NotNull(before);

		// Push the prompt three rows down the window. Nothing here touches the mouse.
		Grow(system, window, spacer, 4);

		var after = window.GetCursorContentPosition(prompt);
		Assert.NotNull(after);

		Assert.True(
			after!.Value.Y > before!.Value.Y,
			$"caret stayed at row {before.Value.Y} while the prompt moved down to row {after.Value.Y - (after.Value.Y - before.Value.Y)}+; " +
			"it is reading a bounds cache that only a mouse event refreshes");

		System.GC.KeepAlive(system);
	}

	[Fact]
	public void Caret_FollowsTheControl_BeforeAnyMouseEvent()
	{
		// The control case: with the cache still empty both readers take the node fallback, so the
		// caret tracked correctly here even while the bug was live. If this ever fails, the fallback
		// itself has broken rather than the precedence.
		var (system, window, spacer, prompt) = Host();

		var before = window.GetCursorContentPosition(prompt);
		Assert.NotNull(before);

		Grow(system, window, spacer, 4);

		var after = window.GetCursorContentPosition(prompt);
		Assert.NotNull(after);
		Assert.True(after!.Value.Y > before!.Value.Y);

		System.GC.KeepAlive(system);
	}

	[Fact]
	public void CaretVisibility_TracksTheControl_AfterAMouseEvent()
	{
		// IsCursorPositionVisible reads the same stale cache to decide WHETHER to show the caret, not
		// just where. Drive the real per-frame decision (HasInteractiveContent) rather than asserting
		// the private reader directly.
		var (system, window, spacer, prompt) = Host();

		DeliverOneMouseEvent(window);
		system.Render.UpdateDisplay();

		Grow(system, window, spacer, 6);

		bool visible = window.EventDispatcher!.HasInteractiveContent(out var cursorPosition);

		Assert.True(visible, "the focused prompt stopped reporting a visible caret after it moved");
		Assert.True(cursorPosition.Y > 0);

		System.GC.KeepAlive(system);
	}
}
