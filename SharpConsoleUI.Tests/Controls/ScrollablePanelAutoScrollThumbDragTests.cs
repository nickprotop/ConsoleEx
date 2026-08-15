// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Dragging the vertical scrollbar thumb away from the bottom must DETACH <see cref="ScrollablePanelControl.AutoScroll"/>,
/// exactly as scrolling up with the wheel does — and dragging it back to the bottom must re-attach.
/// <para>
/// The wheel path (<c>ScrollVerticalBy</c>) has always carried that detach/re-attach logic; the thumb-drag path
/// went through <c>ScrollVerticalTo</c>, which only moves the offset. With AutoScroll still set, the next
/// <c>PaintDOM</c> re-asserted the bottom ("autoscroll-bottom") and the thumb sprang straight back — the user
/// saw the panel move up one frame and immediately snap down again.
/// </para>
/// <para>
/// These tests RE-RENDER between the drag and the assert. That is the whole point: reading the offset straight
/// after the Move shows the drag "working" even when the bug is live, because the snap-back happens on the
/// following paint.
/// </para>
/// </summary>
public class ScrollablePanelAutoScrollThumbDragTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelAutoScrollThumbDragTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	private static MarkupControl Line(string text) => new MarkupControl(new List<string> { text });

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var pos = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), pos, pos, pos);
	}

	/// <summary>
	/// A panel pinned to the bottom by AutoScroll, inside a real window, with enough content to scroll.
	/// </summary>
	private (ScrollablePanelControl panel, Window window) AtBottom(int lines = 60, int height = 8)
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var panel = new ScrollablePanelControl { Height = height, AutoScroll = true };
		for (int i = 0; i < lines; i++) panel.AddControl(Line($"line {i}"));
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight, "precondition: content must overflow");
		Assert.True(panel.AutoScroll, "precondition: AutoScroll is on");
		int maxOffset = panel.TotalContentHeight - panel.ViewportHeight;
		Assert.Equal(maxOffset, panel.VerticalScrollOffset);
		return (panel, window);
	}

	/// <summary>
	/// The reported bug: grab the thumb at the bottom, drag it up, and the panel must STAY up. Before the
	/// fix the offset returned to the bottom on the very next paint.
	/// </summary>
	[Fact]
	public void ThumbDragUp_FromAutoScrolledBottom_StaysScrolledUp_AfterRerender()
	{
		var (panel, window) = AtBottom();
		int sbX = panel.ViewportWidth - 1;
		int trackBottom = panel.ViewportHeight - 2; // thumb sits at the bottom of the track

		// Grab the thumb where it is (bottom) and drag it up to the top of the track.
		panel.ProcessMouseEvent(Mouse(sbX, trackBottom, MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(sbX, 1, MouseFlags.Button1Dragged));
		int afterDrag = panel.VerticalScrollOffset;
		_out.WriteLine($"after drag: offset={afterDrag} autoScroll={panel.AutoScroll}");
		Assert.True(afterDrag < panel.TotalContentHeight - panel.ViewportHeight, "drag must move the offset up");

		// The snap-back only happens on the NEXT paint — this re-render is what exposes the bug.
		window.RenderAndGetVisibleContent();
		int afterRerender = panel.VerticalScrollOffset;
		_out.WriteLine($"after re-render: offset={afterRerender} autoScroll={panel.AutoScroll}");

		Assert.False(panel.AutoScroll, "Dragging the thumb up must detach AutoScroll.");
		Assert.Equal(afterDrag, afterRerender);
	}

	/// <summary>
	/// Detaching must survive content still arriving — a live log pane keeps appending while the user reads
	/// scrolled-up history. This is the situation that makes the bug painful in a real app.
	/// </summary>
	[Fact]
	public void ThumbDragUp_ThenMoreContentArrives_DoesNotJumpBackToBottom()
	{
		var (panel, window) = AtBottom();
		int sbX = panel.ViewportWidth - 1;

		panel.ProcessMouseEvent(Mouse(sbX, panel.ViewportHeight - 2, MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(sbX, 1, MouseFlags.Button1Dragged));
		panel.ProcessMouseEvent(Mouse(sbX, 1, MouseFlags.Button1Released));
		int parked = panel.VerticalScrollOffset;

		// More lines land while the user is reading history.
		for (int i = 0; i < 10; i++) panel.AddControl(Line($"appended {i}"));
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"parked={parked} after append offset={panel.VerticalScrollOffset} autoScroll={panel.AutoScroll}");
		Assert.False(panel.AutoScroll, "AutoScroll stays detached while the user reads history.");
		Assert.Equal(parked, panel.VerticalScrollOffset);
	}

	/// <summary>
	/// The other half of the contract: dragging the thumb back down to the bottom RE-ATTACHES AutoScroll,
	/// mirroring the wheel, so newly appended content follows again.
	/// </summary>
	[Fact]
	public void ThumbDragBackToBottom_ReattachesAutoScroll()
	{
		var (panel, window) = AtBottom();
		int sbX = panel.ViewportWidth - 1;
		int trackBottom = panel.ViewportHeight - 2;

		// Up first (detach), then back down to the bottom of the track (re-attach).
		panel.ProcessMouseEvent(Mouse(sbX, trackBottom, MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(sbX, 1, MouseFlags.Button1Dragged));
		window.RenderAndGetVisibleContent();
		Assert.False(panel.AutoScroll, "precondition: dragging up detached");

		panel.ProcessMouseEvent(Mouse(sbX, panel.ViewportHeight + 20, MouseFlags.Button1Dragged));
		panel.ProcessMouseEvent(Mouse(sbX, panel.ViewportHeight + 20, MouseFlags.Button1Released));
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"after drag to bottom: offset={panel.VerticalScrollOffset} autoScroll={panel.AutoScroll}");
		Assert.True(panel.AutoScroll, "Dragging the thumb back to the bottom re-attaches AutoScroll.");

		// And it genuinely follows new content again.
		int before = panel.VerticalScrollOffset;
		for (int i = 0; i < 5; i++) panel.AddControl(Line($"tail {i}"));
		window.RenderAndGetVisibleContent();
		Assert.True(panel.VerticalScrollOffset > before, "Re-attached AutoScroll follows appended content.");
		Assert.Equal(panel.TotalContentHeight - panel.ViewportHeight, panel.VerticalScrollOffset);
	}

	/// <summary>
	/// A panel that never had AutoScroll on must not acquire it just because a drag ended at the bottom of
	/// the track — re-attach restores a mode the panel was already in, it does not turn one on.
	/// </summary>
	[Fact]
	public void ThumbDragToBottom_OnNonAutoScrollPanel_DoesNotEnableAutoScroll()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 60; i++) panel.AddControl(Line($"line {i}"));
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		Assert.False(panel.AutoScroll, "precondition: AutoScroll off");

		int sbX = panel.ViewportWidth - 1;
		panel.ProcessMouseEvent(Mouse(sbX, 1, MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(sbX, panel.ViewportHeight + 20, MouseFlags.Button1Dragged));
		window.RenderAndGetVisibleContent();

		_out.WriteLine($"non-autoscroll panel dragged to bottom: autoScroll={panel.AutoScroll}");
		Assert.False(panel.AutoScroll, "A drag to the bottom must not switch AutoScroll on for a panel that never had it.");
	}
}
