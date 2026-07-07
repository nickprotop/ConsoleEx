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
/// Regression coverage for <see cref="ScrollablePanelControl"/>'s gesture-capture mouse model. A Button1
/// press captures the sub-region it lands on (vertical/horizontal scrollbar or content); every subsequent
/// resent press/drag routes to the captured region WITHOUT re-hit-testing. This structurally prevents a
/// scrollbar thumb-drag from leaking into the content pass-through when the pointer leaves the track (SGR
/// re-sends Button1Pressed on every motion-while-held).
/// </summary>
public class ScrollablePanelGestureCaptureTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelGestureCaptureTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	private static (ScrollablePanelControl panel, Window window) Render(ScrollablePanelControl panel)
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, window);
	}

	private static MarkupControl Wide(string text) => new MarkupControl(new List<string> { text });

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var pos = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), pos, pos, pos);
	}

	/// <summary>
	/// The core capture win: a vertical-scrollbar thumb-drag whose pointer moves OFF the scrollbar column
	/// (into the content area, x=0) keeps scrolling. Before capture, the resent Button1Pressed at an
	/// off-track X would re-hit-test into content and stop the scroll; the capture glues the gesture to the
	/// scrollbar regardless of pointer position.
	/// </summary>
	[Fact]
	public void VScrollbarThumbDrag_PointerLeavesTrackIntoContent_KeepsScrolling()
	{
		var panel = new ScrollablePanelControl { Height = 8 };
		for (int i = 0; i < 40; i++) panel.AddControl(Wide($"r{i}"));
		var (_, _) = Render(panel);

		Assert.True(panel.TotalContentHeight > panel.ViewportHeight, "precondition: scrollable");
		int sbX = panel.ViewportWidth - 1; // vertical scrollbar column (content-relative)

		// Press on the thumb (top of the track), then drag DOWN but with the pointer moved off the
		// scrollbar column into the content area (x = 0). SGR resends Button1Pressed on motion.
		panel.ProcessMouseEvent(Mouse(sbX, 1, MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(0, 4, MouseFlags.Button1Pressed));
		int afterFirst = panel.VerticalScrollOffset;
		_out.WriteLine($"after off-track move to y=4: offset={afterFirst}");
		Assert.True(afterFirst > 0, "Thumb-drag off the track column must keep scrolling (offset tracks the drag).");

		// Continue dragging further down, still off-track: the offset must keep increasing.
		panel.ProcessMouseEvent(Mouse(0, 6, MouseFlags.Button1Dragged));
		int afterSecond = panel.VerticalScrollOffset;
		_out.WriteLine($"after off-track drag to y=6: offset={afterSecond}");
		Assert.True(afterSecond > afterFirst, "Dragging further down (still off-track) scrolls further down.");

		// Release ends the gesture; a later unrelated move must not keep scrolling.
		panel.ProcessMouseEvent(Mouse(0, 6, MouseFlags.Button1Released));
		int afterRelease = panel.VerticalScrollOffset;
		panel.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Dragged));
		Assert.Equal(afterRelease, panel.VerticalScrollOffset);
	}

	/// <summary>
	/// A fresh content click (a gesture that captures the Content region) still forwards to the child under
	/// the cursor: clicking a focusable button in the content area focuses it. This confirms the content
	/// pass-through survives the gesture-capture conversion.
	/// </summary>
	[Fact]
	public void FreshContentClick_ForwardsToChild()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		var button1 = ContainerTestHelpers.CreateButton("First");
		var button2 = ContainerTestHelpers.CreateButton("Second");
		panel.AddControl(button1);
		panel.AddControl(button2);
		var (_, window) = Render(panel);

		// Adding the panel auto-focuses the first child; the second is our forwarding target.
		Assert.True(button1.HasFocus);
		Assert.False(button2.HasFocus);

		// Click inside the content area on the SECOND button's row.
		int clickX = panel.Margin.Left + panel.ContentInsetLeftInternal + 1;
		int clickY = panel.Margin.Top + panel.ContentInsetTopInternal + 1; // row 1 = second button
		panel.ProcessMouseEvent(Mouse(clickX, clickY, MouseFlags.Button1Pressed));
		panel.ProcessMouseEvent(Mouse(clickX, clickY, MouseFlags.Button1Released, MouseFlags.Button1Clicked));

		_out.WriteLine($"button1.HasFocus={button1.HasFocus} button2.HasFocus={button2.HasFocus}");
		Assert.True(button2.HasFocus, "A fresh content click must forward to (and focus) the child under the cursor.");
	}
}
