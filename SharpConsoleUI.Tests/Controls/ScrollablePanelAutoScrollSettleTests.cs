using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// #62: an AutoScroll ScrollablePanel must reach the bottom after content is added WITHOUT the
/// consumer having to nudge the mouse. AutoScroll is applied in PaintDOM (the paint phase), after
/// ScrollLayout has already arranged the children at the previous offset, so the offset change is
/// one frame stale. The panel must therefore schedule the settling frame itself (Invalidate), which
/// the host's dirty check (PendingWork) then turns into an automatic re-render.
/// </summary>
public class ScrollablePanelAutoScrollSettleTests
{
	private readonly ITestOutputHelper _out;
	public ScrollablePanelAutoScrollSettleTests(ITestOutputHelper o) { _out = o; }

	private static ScrollablePanelControl MakeSub()
	{
		var sub = new ScrollablePanelControl
		{
			BorderStyle = BorderStyle.None,
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = true,
		};
		sub.AddControl(new MarkupControl(new List<string>
		{
			"[markdown]# Test Header\n\n- item 1\n- item 2\n- item 3\n\n---\n\n| col1 | col2 |\n|---|---|\n| a | b |\n[/]"
		}));
		return sub;
	}

	private static CollapsiblePanel MakeMain(int i)
	{
		var main = new CollapsiblePanel { Title = $"Main Panel {i}", MaxContentHeight = 8 };
		main.AddControl(MakeSub());
		return main;
	}

	[Fact]
	public void AutoScroll_AfterAddingNestedContent_SettlesAtBottom_WithoutExternalRepaint()
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		var outer = new ScrollablePanelControl
		{
			BorderStyle = BorderStyle.Rounded,
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = true,
		};
		window.AddControl(outer);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		for (int i = 0; i < 6; i++) outer.AddControl(MakeMain(i));

		// The frame that consumes the AddControl relayout applies AutoScroll in PaintDOM, AFTER the
		// children were arranged at the old offset. The panel must request a RELAYOUT (not merely a
		// repaint) so the host re-arranges the children at the new offset without any external input
		// (the "move the mouse" the bug reporter needed). A plain repaint re-runs PaintDOM but leaves
		// the children at their stale arranged positions, which is the bug.
		var firstFrame = window.RenderAndGetVisibleContent();
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
		// Children were arranged before AutoScroll moved the offset, so this first frame still shows
		// the top (the bug's stale frame) — proving the offset change has not yet been applied to layout.
		Assert.Contains(firstFrame, l => l.Contains("Main Panel 0"));

		// The host's render loop keeps rendering while the window is dirty. The panel re-arranges at
		// the new offset each frame; nested panel/markdown heights settle over a few frames, so the
		// offset converges in a bounded number of frames — all WITHOUT any external input. It is
		// converged once the offset stops moving AND no further AutoScroll-driven Relayout is pending
		// (any residual dirtiness is at most a Repaint from the markdown content re-rendering).
		int frames = 0;
		while (frames < 20)
		{
			int before = outer.VerticalScrollOffset;
			window.RenderAndGetVisibleContent();
			frames++;
			if (outer.VerticalScrollOffset == before && window.PendingWork != FrameWork.Relayout)
				break;
		}

		int max = System.Math.Max(0, outer.TotalContentHeight - outer.ViewportHeight);
		Assert.True(max > 0, "Content must overflow for this scenario to be meaningful.");
		Assert.Equal(max, outer.VerticalScrollOffset);
		Assert.True(outer.AutoScroll, "AutoScroll must stay attached across content additions.");
		Assert.True(frames < 20, $"AutoScroll did not converge ({frames} frames).");
		_out.WriteLine($"converged after {frames} self-scheduled frames; offset={outer.VerticalScrollOffset} max={max}");

		// The settled output actually shows the BOTTOM: the last panel is visible and the first has
		// scrolled off. This requires the children to have been re-arranged at the new offset, which
		// only a Relayout does — so it fails if AutoScroll merely repaints without scheduling layout.
		var settled = window.RenderAndGetVisibleContent();
		Assert.Contains(settled, l => l.Contains("Main Panel 5"));
		Assert.DoesNotContain(settled, l => l.Contains("Main Panel 0"));

		// My fix only requests Relayout when the offset moves. Offset is now stable, so it must NOT
		// keep requesting Relayout (that would be a busy CPU loop).
		Assert.Equal(max, outer.VerticalScrollOffset);
		Assert.NotEqual(FrameWork.Relayout, window.PendingWork);
	}
}
