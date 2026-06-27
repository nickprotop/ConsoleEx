// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// "Real thing" regression: a HorizontalGridControl with VerticalAlignment.Fill, hosted in a
/// ScrollablePanelControl shorter than the HGC's content, must let the panel SCROLL — not squash
/// the HGC to the viewport. HGC has a hard minimum height (its tallest column) that it cannot shrink
/// below and does not scroll itself, so it must report that minimum when measured unbounded, exactly
/// like GridControl. Before the fix, HGC did not implement IFillReportsMinimumHeight, so the panel
/// measured it at the Fill slot and it squashed with no scrollbar (confirmed by a controlled A/B
/// against a GridControl, which scrolls correctly in the identical scenario).
/// </summary>
public class HorizontalGridFillScrollTests
{
	private const int ContentRows = 14;

	private static (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window) Host(IWindowControl fillChild)
	{
		var panel = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		panel.AddControl(fillChild);

		// Window much SHORTER than the 14-row content.
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment(sysW: 40, sysH: 14, winW: 30, winH: 8);
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.RenderAndGetVisibleContent();
		return (panel, system, window);
	}

	[Fact]
	public void FillHgc_TallerThanViewport_PanelCanScroll()
	{
		// A Fill HGC whose single flex column holds 14 lines of content.
		var tall = new MarkupControl(Enumerable.Range(0, ContentRows).Select(i => $"row {i}").ToList());
		var hgc = Builders.Controls.HorizontalGrid()
			.Column(c => c.Flex(1).Add(tall))
			.Build();
		hgc.VerticalAlignment = VerticalAlignment.Fill;

		var (panel, _, _) = Host(hgc);

		Assert.True(
			panel.TotalContentHeight >= ContentRows,
			$"expected panel content >= {ContentRows} (HGC's true min height), got {panel.TotalContentHeight}");
		Assert.True(panel.CanScrollDown, "panel should scroll to reveal the HGC content that overflows the viewport");
	}

	[Fact]
	public void FillHgc_FitsWhenRoomAvailable_NoScroll()
	{
		// No-regression guard: when the window is taller than the content, the Fill HGC fills and does
		// not need to scroll.
		var content = new MarkupControl(Enumerable.Range(0, 3).Select(i => $"row {i}").ToList());
		var hgc = Builders.Controls.HorizontalGrid()
			.Column(c => c.Flex(1).Add(content))
			.Build();
		hgc.VerticalAlignment = VerticalAlignment.Fill;

		var panel = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		panel.AddControl(hgc);
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment(sysW: 40, sysH: 22, winW: 30, winH: 16);
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.False(panel.CanScrollDown, "a Fill HGC with room to spare should not need to scroll");
	}
}
