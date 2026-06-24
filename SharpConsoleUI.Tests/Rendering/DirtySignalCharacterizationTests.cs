// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Characterization tests pinning the OBSERVABLE invalidation state (Window.PendingWork) at every
/// site the single-dirty-signal consolidation touches. Written GREEN against the current dual-signal
/// code, they must STAY green after _invalidated is deleted — proving the refactor is behavior-
/// preserving. They assert via PendingWork (the surviving signal), never via _invalidated (the one
/// being removed), so they are valid on both sides of the change.
/// </summary>
public class DirtySignalCharacterizationTests
{
	private static (ConsoleWindowSystem system, Window window) NewWindow(int w = 40, int h = 12)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(w + 6, h + 6);
		var window = new Window(system) { Title = "Dirty", Left = 0, Top = 0, Width = w, Height = h };
		system.AddWindow(window);
		return (system, window);
	}

	private static List<string> Render(Window window)
		=> window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, Math.Max(1, window.Width), Math.Max(1, window.Height)) });

	private static MarkupControl AddLines(Window window, int count)
	{
		var lines = new List<string>();
		for (int i = 0; i < count; i++) lines.Add($"line {i:D3}");
		var c = new MarkupControl(lines) { VerticalAlignment = VerticalAlignment.Top };
		window.AddControl(c);
		return c;
	}

	private static ConsoleKeyInfo Key(ConsoleKey k)
		=> new((char)0, k, shift: false, alt: false, control: false);

	// --- Invalidate(): both intents land as PendingWork ---

	[Fact]
	public void Invalidate_Repaint_SetsPendingWorkRepaint()
	{
		var (_, window) = NewWindow();
		Render(window); // drain initial Relayout
		Assert.Equal(FrameWork.None, window.PendingWork);
		window.Invalidate(Invalidation.Repaint);
		Assert.Equal(FrameWork.Repaint, window.PendingWork);
	}

	[Fact]
	public void Invalidate_Relayout_SetsPendingWorkRelayout()
	{
		var (_, window) = NewWindow();
		Render(window);
		window.Invalidate(Invalidation.Relayout);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	// --- A render consumes pending work back to None ---

	[Fact]
	public void Render_ConsumesPendingWorkToNone()
	{
		var (_, window) = NewWindow();
		AddLines(window, 5);
		Assert.NotEqual(FrameWork.None, window.PendingWork); // adding a control dirtied it
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	// --- UpdateContentOrder (reorder) dirties at Relayout ---

	[Fact]
	public void UpdateContentOrder_SetsPendingWorkRelayout()
	{
		var (_, window) = NewWindow();
		var a = AddLines(window, 2);
		var b = AddLines(window, 2);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		window.UpdateContentOrder(b, 0);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	// --- ForceRebuildLayout dirties at Relayout ---

	[Fact]
	public void ForceRebuildLayout_SetsPendingWorkRelayout()
	{
		var (_, window) = NewWindow();
		AddLines(window, 3);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		window.ForceRebuildLayout();
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	// --- Scroll keys via the REAL dispatcher path each dirty at Relayout ---

	[Theory]
	[InlineData(ConsoleKey.UpArrow)]
	[InlineData(ConsoleKey.DownArrow)]
	[InlineData(ConsoleKey.PageUp)]
	[InlineData(ConsoleKey.PageDown)]
	[InlineData(ConsoleKey.Home)]
	[InlineData(ConsoleKey.End)]
	public void ScrollKey_ViaDispatcher_SetsPendingWorkRelayout(ConsoleKey key)
	{
		var (_, window) = NewWindow();
		AddLines(window, 100); // overflow so scrolling is meaningful
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		window.EventDispatcher!.ProcessInput(Key(key));

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	// --- Mouse wheel via the REAL dispatcher path dirties at Relayout ---

	[Theory]
	[InlineData(MouseFlags.WheeledUp)]
	[InlineData(MouseFlags.WheeledDown)]
	public void WheelScroll_ViaDispatcher_SetsPendingWorkRelayout(MouseFlags wheel)
	{
		var (_, window) = NewWindow();
		AddLines(window, 100);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		var pos = new Point(2, 2);
		var args = new MouseEventArgs(new List<MouseFlags> { wheel }, pos, pos, pos);
		window.EventDispatcher!.ProcessMouseEvent(args);

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	// --- Off-screen render (no visible regions) re-raises so the window stays consumable ---

	[Fact]
	public void OffScreenRender_LeavesWindowDirty_ThenOnScreenRenderConsumes()
	{
		var (_, window) = NewWindow();
		AddLines(window, 5);
		Assert.NotEqual(FrameWork.None, window.PendingWork);

		// Off-screen: empty visible-regions list → re-raise path (isInRenderingPipeline == false).
		window.RenderAndGetVisibleContent(new List<Rectangle>());
		Assert.NotEqual(FrameWork.None, window.PendingWork); // still dirty, will retry

		// On-screen render consumes it.
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	// --- RenderLock unlock re-raises at Relayout ---

	[Fact]
	public void RenderLockUnlock_SetsPendingWorkRelayout()
	{
		var (_, window) = NewWindow();
		AddLines(window, 3);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		window.RenderLock = true;
		window.RenderLock = false; // unlock → Request(Relayout)

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}
}
