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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class CollapsiblePanelBodyMouseTests
{
	private static MouseEventArgs Click(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, pos, pos, pos);
	}

	[Fact]
	public void NonCollapsibleBodyClick_RaisesMouseClick()
	{
		// A non-collapsible, headerless panel with only a passive markup child: a body click must
		// raise the panel's own MouseClick (no body child consumes it).
		var panel = new CollapsiblePanel { Collapsible = false, ShowHeader = false, Width = 20 };
		panel.AddControl(new MarkupControl(new List<string> { "passive" }));

		bool clicked = false;
		panel.MouseClick += (_, _) => clicked = true;

		panel.ProcessMouseEvent(Click(2, 0)); // body row 0

		Assert.True(clicked, "non-collapsible body click should raise MouseClick");
	}

	[Fact]
	public void FocusableBodyChildClick_IsConsumed_DoesNotRaisePanelMouseClick()
	{
		// A focusable body child (a Button) gates the HasFocusableTarget check: clicking it must be
		// consumed (focus was taken) and must NOT surface as the panel's own MouseClick.
		var panel = new CollapsiblePanel { Collapsible = false, ShowHeader = false, Width = 20 };
		panel.AddControl(new ButtonControl()); // focusable, no handler

		bool clicked = false;
		panel.MouseClick += (_, _) => clicked = true;

		bool consumed = panel.ProcessMouseEvent(Click(2, 0)); // body row 0

		Assert.True(consumed, "click on a focusable body child must be consumed");
		Assert.False(clicked, "a focusable body child must not surface the panel's own MouseClick");
	}

	[Fact]
	public void TwoRapidPassiveBodyClicks_RaiseMouseDoubleClick_NotTwoClicks()
	{
		// Manual double-click detection: two Button1Clicked within the threshold on a passive-child
		// panel must raise a single MouseDoubleClick, not two MouseClicks.
		var panel = new CollapsiblePanel { Collapsible = false, ShowHeader = false, Width = 20 };
		panel.AddControl(new MarkupControl(new List<string> { "passive" }));

		int clicks = 0;
		int doubleClicks = 0;
		panel.MouseClick += (_, _) => clicks++;
		panel.MouseDoubleClick += (_, _) => doubleClicks++;

		panel.ProcessMouseEvent(Click(2, 0));
		panel.ProcessMouseEvent(Click(2, 0));

		Assert.Equal(1, doubleClicks);
		Assert.Equal(1, clicks); // first click fires MouseClick; the second collapses into the double-click
	}
}
