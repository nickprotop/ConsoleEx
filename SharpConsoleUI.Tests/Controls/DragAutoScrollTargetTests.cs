using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Tests.Controls;

public class DragAutoScrollTargetTests
{
	private static void Paint(MarkupControl c)
	{
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		c.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	private static MouseEventArgs Mouse(int x, int y, MouseFlags flags)
	{
		var p = new System.Drawing.Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { flags }.ToList(), p, p, p);
	}

	[Fact]
	public void Markup_LastDragRelativeY_IsClipRelative_OffsetByBoundsVsClip()
	{
		// Regression for the Window-host bug: when a MarkupControl is hosted directly in a SCROLLED
		// window, the layout shifts its bounds.Y (ActualY) by -ScrollOffset while the visible viewport
		// (clipRect) stays at the window's content top. The drag Y reported for autoscroll must be
		// CLIP-RELATIVE (relative to the visible viewport top), not the raw control-relative value —
		// otherwise edge detection inverts and the window scrolls the wrong way.
		//
		// Paint with bounds.Y == clip.Y so the press hit-tests cleanly, then assert the stored value
		// equals the conversion formula: dragY + ActualY + Margin.Top - clipTop. Here ActualY == clipTop
		// so it's the identity (the SPC/unscrolled case). The scrolled-window case (ActualY != clipTop)
		// is exercised live in the tmux verification gate, which headless layout can't reproduce.
		var c = new MarkupControl(Enumerable.Range(1, 40).Select(i => $"line {i}").ToList()) { EnableSelection = true };
		var buffer = new CharacterBuffer(45, 30);
		var bounds = new LayoutRect(0, 0, 40, 40);
		var clip = new LayoutRect(0, 0, 40, 14);
		c.PaintDOM(buffer, bounds, clip, Color.White, Color.Black);

		c.ProcessMouseEvent(Mouse(2, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(2, 20, MouseFlags.Button1Dragged));

		// ActualY(0) + Margin.Top(0) - clipTop(0) == 0, so the clip-relative value equals the drag Y.
		Assert.Equal(20, ((IDragAutoScrollTarget)c).LastDragRelativeY);
	}

	[Fact]
	public void MarkupControl_ImplementsInterface()
	{
		var c = new MarkupControl(new List<string> { "a", "b" });
		Assert.IsAssignableFrom<IDragAutoScrollTarget>(c);
	}

	[Fact]
	public void Markup_ViewportNotReady_BeforePaint()
	{
		var c = new MarkupControl(new List<string> { "a" });
		Assert.False(((IDragAutoScrollTarget)c).IsViewportReady);
	}

	[Fact]
	public void Markup_ViewportReady_AfterPaint_WithPositiveHeight()
	{
		var c = new MarkupControl(new List<string> { "a", "b", "c" });
		Paint(c);
		var t = (IDragAutoScrollTarget)c;
		Assert.True(t.IsViewportReady);
		Assert.True(t.ViewportHeightRows > 0, $"height was {t.ViewportHeightRows}");
	}

	[Fact]
	public void Markup_AutoScrollStep_NoHost_DoesNotThrow()
	{
		var c = new MarkupControl(new List<string> { "a", "b" });
		Paint(c);
		var ex = Record.Exception(() => ((IDragAutoScrollTarget)c).AutoScrollStep(3));
		Assert.Null(ex);
	}

	[Fact]
	public void MultilineEdit_ImplementsInterface()
	{
		var c = new MultilineEditControl("line1\nline2\nline3");
		Assert.IsAssignableFrom<IDragAutoScrollTarget>(c);
	}

	[Fact]
	public void MultilineEdit_ViewportHeight_IsPositive()
	{
		var c = new MultilineEditControl("a\nb\nc") { ViewportHeight = 4 };
		var t = (IDragAutoScrollTarget)c;
		Assert.True(t.ViewportHeightRows > 0);
	}

	[Fact]
	public void MultilineEdit_AutoScrollStep_Down_IncreasesOffset_Clamped()
	{
		var content = string.Join("\n", System.Linq.Enumerable.Range(1, 20));
		var c = new MultilineEditControl(content) { ViewportHeight = 5 };
		int before = c.VerticalScrollOffset;
		((IDragAutoScrollTarget)c).AutoScrollStep(3);
		Assert.True(c.VerticalScrollOffset >= before);
	}

	private sealed class FakeTarget : IDragAutoScrollTarget
	{
		public bool IsDragSelecting { get; set; } = true;
		public bool IsViewportReady { get; set; } = true;
		public int LastDragRelativeY { get; set; }
		public int ViewportHeightRows { get; set; } = 10;
		public int Scrolled;
		public int Extended;
		public void AutoScrollStep(int rows) => Scrolled += rows;
		public void ExtendSelectionToRevealedEdge(int direction) => Extended++;
	}

	[Fact]
	public void Registry_Step_DrivesTarget_WhenOutOfBounds()
	{
		var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
		var fake = new FakeTarget { LastDragRelativeY = 15, ViewportHeightRows = 10 }; // 6 below bottom
		system.RegisterDragAutoScroll(fake);

		system.StepDragAutoScrollForTest(elapsedMs: 1000);

		Assert.True(fake.Scrolled > 0, "expected downward scroll");
		Assert.True(fake.Extended > 0, "expected selection extend");
	}

	[Fact]
	public void Registry_Step_NoOp_WhenInBounds()
	{
		var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
		var fake = new FakeTarget { LastDragRelativeY = 3, ViewportHeightRows = 10 };
		system.RegisterDragAutoScroll(fake);
		system.StepDragAutoScrollForTest(1000);
		Assert.Equal(0, fake.Scrolled);
	}
}
