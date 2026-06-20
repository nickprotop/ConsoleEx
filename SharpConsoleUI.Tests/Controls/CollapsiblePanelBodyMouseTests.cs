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
}
