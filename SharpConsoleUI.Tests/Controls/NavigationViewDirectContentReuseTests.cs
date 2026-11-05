// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Navigating away from a direct-content page and back must show that page again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this pins.</b> <see cref="NavigationView"/> hosts a direct-content page by adding it
/// to its content column, and swaps pages with
/// <c>ColumnContainer.RemoveContent(old)</c> + <c>AddContent(new)</c>. But
/// <see cref="ColumnContainer.RemoveContent"/> calls <c>Dispose()</c> on what it removes. A
/// NavigationView does not own those controls: it keeps every one of them alive in
/// <c>_directContent</c> for the lifetime of the view and re-adds the same instance when the user
/// comes back. Swapping away therefore destroyed a control the view intended to reuse, and
/// returning to the page re-added a disposed object that paints nothing.
/// </para>
/// <para>
/// The user sees a page that works the first time and is BLANK on every visit after, with its
/// title and subtitle still correct because those live in a separate header that is never swapped.
/// Reproduced on the DemoApp Chart Showcase page.
/// </para>
/// <para>
/// <b>Why the fix is in the view, not the column.</b> Disposing on removal is right for a column
/// that owns its children, which is the normal case, and changing that would leak for every other
/// caller. The NavigationView is the one place that removes a control it does not own, so it is
/// the place that has to say so.
/// </para>
/// </remarks>
public class NavigationViewDirectContentReuseTests
{
	private static ScrollablePanelControl BuildPage(string body)
	{
		var panel = new ScrollablePanelControl();
		panel.AddControl(new MarkupControl(new List<string> { body }));
		return panel;
	}

	private static (ConsoleWindowSystem System, Window Window, NavigationView Nav,
		ScrollablePanelControl First, ScrollablePanelControl Second) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();

		// Built ONCE and captured, which is what a page factory like the Chart Showcase does: the
		// instance must survive being navigated away from. The page root is a
		// ScrollablePanelControl, as every direct-content page in that demo is, and that matters:
		// its OnDisposing CLEARS its child list, so a disposed page comes back structurally empty
		// rather than merely detached. A bare leaf control survives the same round trip, because
		// AddContent reassigns its Container and it has no children to lose.
		var first = BuildPage("first page body");
		var second = BuildPage("second page body");

		var nav = new NavigationViewBuilder()
			.AddItem("First", content: first)
			.AddItem("Second", content: second)
			.WithSelectedIndex(0)
			.Build();

		var window = new Window(system) { Width = 80, Height = 24 };
		window.AddControl(nav);

		return (system, window, nav, first, second);
	}

	/// <summary>The page that is switched TO must appear; this is the control case.</summary>
	[Fact]
	public void NavigatingToASecondDirectContentPage_ShowsIt()
	{
		var (_, window, nav, _, _) = Build();

		nav.SelectedIndex = 1;
		window.RenderAndGetVisibleContent();

		var text = string.Join("\n", window.RenderAndGetVisibleContent());
		Assert.Contains("second page body", text);
	}

	[Fact]
	public void ReturningToADirectContentPage_ShowsItAgain()
	{
		var (_, window, nav, _, _) = Build();

		nav.SelectedIndex = 1;
		nav.SelectedIndex = 0;
		window.RenderAndGetVisibleContent();

		// The observable end state: the first page's text is on screen again.
		var text = string.Join("\n", window.RenderAndGetVisibleContent());
		Assert.Contains("first page body", text);
	}
}
