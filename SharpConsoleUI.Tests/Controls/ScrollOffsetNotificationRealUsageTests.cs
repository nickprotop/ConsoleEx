// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// <see cref="ScrollablePanelControl.VerticalScrollOffsetChanged"/> as a consumer actually meets it.
/// </summary>
/// <remarks>
/// <para>
/// The event exists so a virtualizing consumer can keep a window over a long transcript in step with
/// what is on screen. That consumer never sets the offset itself: it scrolls, or the user does, by
/// key and by wheel. A test that calls <c>ScrollVerticalBy</c> proves the event carries the right
/// numbers and says nothing about whether it fires when the panel is actually scrolled.
/// </para>
/// <para>
/// So this drives the real paths: keys through the input dispatch, and a wheel event through the
/// control's own mouse handling, inside a hosted window with content long enough to scroll, and
/// asserts the notification survives a re-render.
/// </para>
/// </remarks>
public class ScrollOffsetNotificationRealUsageTests
{
	private const int Width = 40;
	private const int Height = 12;

	/// <summary>A panel in a real window, holding more content than fits.</summary>
	private static (ConsoleWindowSystem System, Window Window, ScrollablePanelControl Panel, List<ScrollOffsetChangedEventArgs> Seen) Host()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
		var window = new Window(system) { Title = "Panel", Left = 0, Top = 0, Width = Width, Height = Height };

		var panel = new ScrollablePanelControl { Height = Height - 4 };
		for (int i = 0; i < 60; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"line {i:D2}"));

		window.AddControl(panel);
		system.AddWindow(window);
		Render(window);
		window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

		var seen = new List<ScrollOffsetChangedEventArgs>();
		panel.VerticalScrollOffsetChanged += (_, e) => seen.Add(e);
		return (system, window, panel, seen);
	}

	private static void Render(Window window) =>
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });

	private static void PressDown(ConsoleWindowSystem system, int times)
	{
		for (int i = 0; i < times; i++)
		{
			system.InputStateService.EnqueueKey(
				new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));
			system.Input.ProcessInput();
		}
	}

	/// <summary>
	/// A key through the real dispatch scrolls the panel and tells the consumer, and the offset the
	/// event reported is the offset the panel still has after the frame is composed.
	/// </summary>
	[Fact]
	public void ScrollingByKey_RaisesTheEvent_AndTheOffsetSurvivesARerender()
	{
		var (system, window, panel, seen) = Host();

		PressDown(system, 8);
		Render(window);

		Assert.NotEmpty(seen);

		// The last notification must describe where the panel actually ended up: a consumer that
		// trusted a stale number would window the wrong part of the transcript.
		Assert.Equal(panel.VerticalScrollOffset, seen[^1].Offset);
	}

	/// <summary>
	/// Each notification starts where the previous one ended. A virtualizing consumer uses the pair
	/// to move its window by a delta, so a gap between them would lose rows.
	/// </summary>
	[Fact]
	public void SuccessiveScrolls_ChainPreviousOffsetToOffset()
	{
		var (system, _, _, seen) = Host();

		PressDown(system, 8);

		Assert.NotEmpty(seen);
		for (int i = 1; i < seen.Count; i++)
			Assert.Equal(seen[i - 1].Offset, seen[i].PreviousOffset);
	}

	/// <summary>The wheel is the other way a user scrolls, and it must notify the same way a key does.</summary>
	[Fact]
	public void ScrollingByWheel_RaisesTheEvent()
	{
		var (_, _, panel, seen) = Host();

		var at = new Point(2, 2);
		panel.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledDown }, at, at, at));

		Assert.NotEmpty(seen);
		Assert.Equal(panel.VerticalScrollOffset, seen[^1].Offset);
	}

	/// <summary>
	/// An offset that did not move raises nothing. Scrolling up at the top is the common case, and a
	/// consumer woken for it would rebuild its window for no reason on every key the user holds.
	/// </summary>
	[Fact]
	public void ScrollingAgainstTheTop_RaisesNothing()
	{
		var (system, _, _, seen) = Host();

		for (int i = 0; i < 3; i++)
		{
			system.InputStateService.EnqueueKey(
				new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
			system.Input.ProcessInput();
		}

		Assert.Empty(seen);
	}
}
