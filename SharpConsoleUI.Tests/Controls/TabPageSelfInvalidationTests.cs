// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for TabControl/TabPage self-invalidation: when a consumer mutates a TabPage
/// in place (Title / IsClosable / Content) on a page that is owned by a TabControl, the owning
/// control must automatically invalidate its window — WITHOUT the consumer calling
/// Invalidate() (or any TabControl method) manually. Tag/Tooltip are not rendered, so they must
/// NOT invalidate.
///
/// Each test uses the "real thing" path: a real ConsoleWindowSystem + Window hosting the
/// TabControl, rendered once to drain PendingWork to None, then a pure page-level mutation,
/// then an assertion on window.PendingWork.
/// </summary>
public class TabPageSelfInvalidationTests
{
	#region Helpers

	private static (ConsoleWindowSystem system, Window window, TabControl tabs) NewTabWindow(int w = 50, int h = 20)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(w + 4, h + 4);
		var window = new Window(system)
		{
			Title = "Tabs",
			Left = 0,
			Top = 0,
			Width = w,
			Height = h
		};
		var tabs = new TabControl();
		tabs.AddTab("One", new MarkupControl(new List<string> { "page one" }));
		tabs.AddTab("Two", new MarkupControl(new List<string> { "page two" }));
		window.AddControl(tabs);
		system.AddWindow(window);
		return (system, window, tabs);
	}

	private static void Render(Window window)
	{
		var region = new List<Rectangle> { new Rectangle(0, 0, Math.Max(1, window.Width), Math.Max(1, window.Height)) };
		window.RenderAndGetVisibleContent(region);
	}

	private static TabPage FirstPage(TabControl tabs) => tabs.TabPages[0];

	#endregion

	[Fact]
	public void TitleChange_OnOwnedPage_Invalidates_WithoutManualInvalidate()
	{
		var (_, window, tabs) = NewTabWindow();
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Pure consumer-level mutation — no tabs.Invalidate() / no SetTabTitle() call.
		FirstPage(tabs).Title = "One (renamed, wider)";

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void IsClosableChange_OnOwnedPage_Invalidates_WithoutManualInvalidate()
	{
		var (_, window, tabs) = NewTabWindow();
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// IsClosable adds a close affordance column to the header → layout change.
		FirstPage(tabs).IsClosable = true;

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void ContentChange_OnOwnedPage_Invalidates_WithoutManualInvalidate()
	{
		var (_, window, tabs) = NewTabWindow();
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		FirstPage(tabs).Content = new MarkupControl(new List<string> { "replaced content" });

		Assert.NotEqual(FrameWork.None, window.PendingWork);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void TagAndTooltipChange_DoNotInvalidate()
	{
		var (_, window, tabs) = NewTabWindow();
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Tag/Tooltip are metadata / not rendered — must not invalidate.
		FirstPage(tabs).Tag = new object();
		FirstPage(tabs).Tooltip = "hover text";

		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void SameValue_IsNoOp_DoesNotInvalidate()
	{
		var (_, window, tabs) = NewTabWindow();
		var page = FirstPage(tabs);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Setting the identical value short-circuits before notifying the owner.
		page.Title = page.Title;
		page.IsClosable = page.IsClosable;

		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void RemovedPage_IsDetached_DoesNotInvalidate()
	{
		var (_, window, tabs) = NewTabWindow();
		var page = FirstPage(tabs);
		tabs.RemoveTab(1); // remove "Two"; keep a reference to page 0 still attached
		var extracted = tabs.TabPages[0]; // still "One"

		// Now extract "One" so it is detached.
		tabs.ExtractTab(0);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Mutating the detached page must NOT invalidate the (now empty) control.
		extracted.Title = "detached rename";

		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void SetTabTitle_DoesNotDoubleInvalidate_AndStillWorks()
	{
		// The internal SetTabTitle mutates Title under the lock; the re-entrancy guard must
		// prevent a second invalidation while the method's own Invalidate still fires.
		var (_, window, tabs) = NewTabWindow();
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		tabs.SetTabTitle(0, "Via SetTabTitle");

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}
}
