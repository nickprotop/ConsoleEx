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
using System.IO;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

// #66: clicking a button inside a ToolbarControl scrolled the enclosing ScrollablePanel up. Cause:
// ToolbarControl.SetItemFocus used FocusReason.Keyboard even on a mouse click, so the panel scrolled the
// (already-visible) clicked item into view. A mouse click must not move the viewport — you can't click
// what you can't see.
[Collection("EnvSerial")]
public class ToolbarClickScrollTests
{
	private static (Window window, ScrollablePanelControl panel, ToolbarControl toolbar, ButtonControl button)
		BuildScrolledToolbar()
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment(sysW: 120, sysH: 40, winW: 120, winH: 40);
		var panel = new ScrollablePanelControl { AutoScroll = true, BorderStyle = BorderStyle.Rounded, VerticalAlignment = VerticalAlignment.Fill };
		window.AddControl(panel);

		var button = new ButtonControl { Text = "Button1" };
		var toolbar = Builders.Controls.Toolbar().Build();
		toolbar.AddItem(button);
		panel.AddControl(toolbar); // toolbar near the TOP

		for (int i = 0; i < 60; i++) // tall filler below -> button scrolls off-screen-above at the bottom
			panel.AddControl(new MarkupControl(new List<string> { $"filler line {i}" }));

		for (int i = 0; i < 25 && window.PendingWork == FrameWork.Relayout; i++) window.RenderAndGetVisibleContent();
		panel.ScrollToBottom();
		for (int i = 0; i < 5; i++) window.RenderAndGetVisibleContent();
		return (window, panel, toolbar, button);
	}

	private static MouseEventArgs Click(int x, int y)
	{ var p = new Point(x, y); return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, p, p, p); }

	[Fact]
	public void ClickingToolbarButton_DoesNotScrollThePanel()
	{
		var savedIn = Console.In;
		Console.SetIn(TextReader.Null);
		try
		{
			var (window, panel, toolbar, button) = BuildScrolledToolbar();
			int before = panel.VerticalScrollOffset;
			Assert.True(before > 0, "precondition: panel must be scrolled down");

			toolbar.ProcessMouseEvent(Click(1, 0)); // click the (off-screen-above) toolbar button
			for (int i = 0; i < 5; i++) window.RenderAndGetVisibleContent();

			Assert.Equal(before, panel.VerticalScrollOffset); // #66: a click must not move the viewport
			Assert.True(window.FocusManager.IsFocused(button), "the button should still receive focus on click");
		}
		finally { Console.SetIn(savedIn); }
	}

	[Fact]
	public void KeyboardNavToOffscreenButton_StillScrollsIntoView()
	{
		var savedIn = Console.In;
		Console.SetIn(TextReader.Null);
		try
		{
			var (window, panel, toolbar, button) = BuildScrolledToolbar();
			int before = panel.VerticalScrollOffset;

			// Keyboard focus onto the toolbar item — Keyboard reason SHOULD scroll it into view (no regression).
			window.FocusManager.SetFocus(button, FocusReason.Keyboard);
			for (int i = 0; i < 5; i++) window.RenderAndGetVisibleContent();

			Assert.NotEqual(before, panel.VerticalScrollOffset); // keyboard focus follows into view
		}
		finally { Console.SetIn(savedIn); }
	}
}
