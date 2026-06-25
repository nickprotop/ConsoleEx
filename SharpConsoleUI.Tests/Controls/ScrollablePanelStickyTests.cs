// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression net for "a bare ScrollablePanelControl balloons to its full content height and the
/// WINDOW scrolls it instead of the panel scrolling internally" — especially visible with sticky
/// controls, where the oversized panel slides behind the sticky region.
///
/// The panel is an <see cref="IScrollableContainer"/>: a self-bounding viewport. It must take the
/// space available between the sticky regions and scroll its own content, WITHOUT the consumer
/// having to opt in via <c>VerticalAlignment.Fill</c> or an explicit <c>Height</c>.
/// </summary>
public class ScrollablePanelStickyTests
{
	private static MarkupControl TallChild(string title, int lines)
	{
		var body = new List<string>();
		for (int i = 0; i < lines; i++) body.Add($"{title} line {i}");
		return new MarkupControl(body);
	}

	private static ScrollablePanelControl OverflowingPanel(VerticalAlignment align, int? height = null)
	{
		var spc = new ScrollablePanelControl { BorderStyle = BorderStyle.Single, VerticalAlignment = align };
		if (height.HasValue) spc.Height = height.Value;
		for (int p = 0; p < 4; p++) spc.AddControl(TallChild($"Panel{p}", 8)); // 32 content lines
		return spc;
	}

	[Fact]
	public void BareSpc_UnderStickyFooters_FillsMiddle_ScrollsInternally_WindowDoesNotScroll()
	{
		// Window content height = 28; two sticky footers = 2 rows; scrollable middle = 26.
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		var spc = OverflowingPanel(VerticalAlignment.Top); // bare: no Fill, no Height
		window.AddControl(spc);

		var foot1 = ContainerTestHelpers.CreateLabel("[FOOTER 1]"); foot1.StickyPosition = StickyPosition.Bottom;
		var foot2 = ContainerTestHelpers.CreateLabel("[FOOTER 2]"); foot2.StickyPosition = StickyPosition.Bottom;
		window.AddControl(foot1);
		window.AddControl(foot2);

		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		var ab = window.GetLayoutNode(spc)!.AbsoluteBounds;

		Assert.Equal(26, ab.Height); // fills the sticky-reduced middle, NOT its 34-row content
		Assert.True(spc.HasVerticalScrollbar, "panel must own its scrollbar");
		Assert.True(spc.CanScrollDown, "panel must be internally scrollable");
		Assert.Equal(0, window.Renderer!.MaxScrollOffset); // the WINDOW must not scroll
	}

	[Fact]
	public void BareSpc_NoSticky_FillsWindow_ScrollsInternally_WindowDoesNotScroll()
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		var spc = OverflowingPanel(VerticalAlignment.Top);
		window.AddControl(spc);

		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		var ab = window.GetLayoutNode(spc)!.AbsoluteBounds;

		Assert.Equal(28, ab.Height); // fills the whole window content area
		Assert.True(spc.HasVerticalScrollbar);
		Assert.True(spc.CanScrollDown);
		Assert.Equal(0, window.Renderer!.MaxScrollOffset);
	}

	[Fact]
	public void ExplicitHeight_IsStillHonored()
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		var spc = OverflowingPanel(VerticalAlignment.Top, height: 12);
		window.AddControl(spc);

		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		Assert.Equal(12, window.GetLayoutNode(spc)!.AbsoluteBounds.Height);
		Assert.True(spc.HasVerticalScrollbar);
	}

	[Theory]
	[InlineData(32, 10)] // content overflows the unbounded default -> capped to the default
	[InlineData(3, 3)]   // content smaller than the default -> shrink to fit (no dead space)
	public void Measure_WithUnboundedHeight_CapsToDefault(int contentLines, int expectedHeight)
	{
		var spc = new ScrollablePanelControl(); // no border/padding/margin -> chrome = 0
		spc.AddControl(TallChild("x", contentLines));

		// An effectively-unbounded height constraint (e.g. nested in an auto-sizing host).
		var size = spc.MeasureDOM(new LayoutConstraints(1, 50, 1, int.MaxValue));

		Assert.Equal(ControlDefaults.ScrollablePanelDefaultUnboundedHeight, 10); // pin the default
		Assert.Equal(expectedHeight, size.Height);
	}
}
