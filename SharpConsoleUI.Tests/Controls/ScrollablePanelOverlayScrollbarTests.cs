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
using SharpConsoleUI.Tests.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Covers ScrollbarOverlay (issue #54): when enabled on a bordered panel, the vertical scrollbar
/// paints on the border instead of reserving an interior column, so content reclaims that width.
/// Borderless panels fall back to the reserved-column behavior. Default (off) is unchanged.
/// </summary>
public class ScrollablePanelOverlayScrollbarTests
{
	// A bordered, scrolling panel whose content overflows its height → vertical scrollbar is needed.
	private static (ScrollablePanelControl panel, Window window) MakeOverflowingBordered(bool overlay)
	{
		var panel = new ScrollablePanelControl
		{
			Height = 8,
			Width = 30,
			BorderStyle = BorderStyle.Single,
			ScrollbarOverlay = overlay,
		};
		for (int i = 0; i < 30; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"ROW{i}" }));

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent(); // drives PaintDOM so viewport/scrollbar state is set
		return (panel, window);
	}

	[Fact]
	public void Overlay_OnBorderedPanel_ReclaimsContentColumns()
	{
		var (plain, _) = MakeOverflowingBordered(overlay: false);
		var (overlaid, _) = MakeOverflowingBordered(overlay: true);

		// Both need a vertical scrollbar (content overflows). Overlay paints it on the border, so its
		// visible content width is WIDER than the reserved-column panel by the reserved column count.
		Assert.True(overlaid.ContentViewportWidth > plain.ContentViewportWidth,
			$"overlay width {overlaid.ContentViewportWidth} should exceed reserved width {plain.ContentViewportWidth}");
	}

	[Fact]
	public void Overlay_OnBorderlessPanel_FallsBackToReservedColumn()
	{
		// Borderless: there's no border to overlay onto, so overlay has no effect — same width as a
		// borderless non-overlay panel.
		ScrollablePanelControl Make(bool overlay)
		{
			var p = new ScrollablePanelControl { Height = 8, Width = 30, BorderStyle = BorderStyle.None, ScrollbarOverlay = overlay };
			for (int i = 0; i < 30; i++) p.AddControl(new MarkupControl(new List<string> { $"ROW{i}" }));
			var (_, w) = ContainerTestHelpers.CreateTestEnvironment();
			w.AddControl(p);
			w.RenderAndGetVisibleContent();
			return p;
		}

		Assert.Equal(Make(overlay: false).ContentViewportWidth, Make(overlay: true).ContentViewportWidth);
	}

	[Fact]
	public void Builder_OverlayScrollbar_RoundTrips()
	{
		var panel = new ScrollablePanelBuilder().WithScrollbar().OverlayScrollbar().Build();
		Assert.True(panel.ScrollbarOverlay);

		var off = new ScrollablePanelBuilder().WithScrollbar().Build();
		Assert.False(off.ScrollbarOverlay);
	}

	[Fact]
	public void Default_IsOff()
	{
		Assert.False(new ScrollablePanelControl().ScrollbarOverlay);
	}

	// A bordered, horizontally-scrolling panel whose content overflows its width → H-scrollbar needed.
	private static ScrollablePanelControl MakeHOverflowingBordered(bool overlay)
	{
		var panel = new ScrollablePanelControl
		{
			Height = 8,
			Width = 20,
			BorderStyle = BorderStyle.Single,
			HorizontalScrollMode = ScrollMode.Scroll,
			ScrollbarOverlay = overlay,
		};
		// A child far wider than the 20-col panel forces a horizontal scrollbar.
		panel.AddControl(new MarkupControl(new List<string> { new string('X', 120) }));
		var (_, w) = ContainerTestHelpers.CreateTestEnvironment();
		w.AddControl(panel);
		w.RenderAndGetVisibleContent();
		return panel;
	}

	[Fact]
	public void Overlay_OnBorderedPanel_ReclaimsContentRow_ForHorizontalScrollbar()
	{
		var plain = MakeHOverflowingBordered(overlay: false);
		var overlaid = MakeHOverflowingBordered(overlay: true);

		// The horizontal scrollbar normally reserves a content row; overlay paints it on the bottom
		// border instead, so the overlay panel's visible content height is taller.
		Assert.True(overlaid.ContentViewportHeight > plain.ContentViewportHeight,
			$"overlay height {overlaid.ContentViewportHeight} should exceed reserved height {plain.ContentViewportHeight}");
	}

	[Fact]
	public void Overlay_PaintsThumbOnBorder_KeepingFrameIntact()
	{
		var (_, window) = MakeOverflowingBordered(overlay: true);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		// The thumb block is painted (on the border column) when the scrollbar is needed...
		Assert.Contains("█", stripped);
		// ...and the border frame is still drawn (overlay overrides only the thumb cells, not the corners).
		Assert.Contains("┌", stripped);
		Assert.Contains("┐", stripped);
		Assert.Contains("└", stripped);
		Assert.Contains("┘", stripped);
	}
}
