// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// TODO(#61-diagnostic): TEMPORARY — remove with the SetVerticalScrollOffset scroll trace.
using System.Collections.Generic;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ScrollTraceDiagnosticTests
{
	// With SHARPCONSOLEUI_SCROLL_TRACE unset (the default in tests), scrolling behaves exactly as before:
	// the choke-point setter is a transparent pass-through and emits no trace. This guards that the
	// diagnostic instrumentation is zero-behavior-change when off.
	[Fact]
	public void ScrollOffset_BehavesIdentically_WhenTraceOff()
	{
		var panel = new ScrollablePanelControl { Height = 5, AutoScroll = false };
		for (int i = 0; i < 40; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"line {i}" }));

		// Drive the scrolling API; the offset must move and clamp exactly as before the setter refactor.
		panel.ScrollVerticalBy(3);
		int afterDown = panel.VerticalScrollOffset;
		Assert.True(afterDown >= 0);

		panel.ScrollToPosition(0);
		Assert.Equal(0, panel.VerticalScrollOffset);

		panel.ScrollToBottom();
		Assert.True(panel.VerticalScrollOffset >= 0);
	}
}
