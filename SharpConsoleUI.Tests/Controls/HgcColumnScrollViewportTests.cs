// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression coverage for the HGC-over-GridControl reimplementation: a scroll-capable control
/// (ScrollablePanelControl / MultilineEditControl), which defaults to <see cref="HorizontalAlignment.Left"/>,
/// placed inside a HorizontalGrid COLUMN that is NARROWER than the whole grid must be arranged at the
/// COLUMN width — not at its own (full-grid-width) DesiredSize. The column body is laid out by
/// <see cref="VerticalStackLayout"/>, whose Left/Center/Right branches arranged a child at its raw desired
/// width; an oversized scroller therefore painted its border + scrollbar past the column's right edge
/// (invisible) even though mouse-scroll still worked, and a MultilineEdit reported the wrong width.
/// The fix clamps a non-stretch child's arranged width to the column width (mirroring GridLayout.AlignAxis).
///
/// Each case is paired with the same control in a plain GridControl Star cell, which already clamped, to
/// pin the parity the user reported ("works in a plain Grid cell").
/// </summary>
public class HgcColumnScrollViewportTests
{
	private static ScrollablePanelControl OverflowingPanel()
	{
		var spc = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill };
		for (int i = 0; i < 60; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"line {i:00}" }));
		return spc;
	}

	private static MultilineEditControl OverflowingEditor()
	{
		var mle = new MultilineEditControl { VerticalAlignment = VerticalAlignment.Fill };
		var lines = new List<string>();
		for (int i = 0; i < 60; i++)
			lines.Add($"line {i:00}");
		mle.SetContentLines(lines);
		return mle;
	}

	// A fixed-width "explorer" column next to a flex content column, so the content column is strictly
	// narrower than the whole grid (the condition under which an unclamped child overflows).
	private static (T content, ColumnContainer contentCol, Window window) BuildTwoColumnHgc<T>(T content)
		where T : class, IWindowControl
	{
		var hgc = new HorizontalGridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};

		var explorerCol = new ColumnContainer(hgc) { Width = 26, VerticalAlignment = VerticalAlignment.Fill };
		explorerCol.AddContent(new MarkupControl(new List<string> { "explorer" }));
		hgc.AddColumn(explorerCol);

		var contentCol = new ColumnContainer(hgc) { VerticalAlignment = VerticalAlignment.Fill };
		contentCol.AddContent(content);
		hgc.AddColumn(contentCol);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(hgc);
		window.RenderAndGetVisibleContent();
		return (content, contentCol, window);
	}

	private static (T content, Window window) BuildPlainGridStarCell<T>(T content)
		where T : class, IWindowControl
	{
		var grid = new GridControl { VerticalAlignment = VerticalAlignment.Fill };
		grid.ColumnDefinitions.Add(GridLength.Cells(26));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "explorer" }), 0, 0);
		grid.Place(content, 0, 1);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		return (content, window);
	}

	// ------------------------------------------------------------------
	// ScrollablePanel in an HGC flex column
	// ------------------------------------------------------------------
	[Fact]
	public void Spc_InHgcFlexColumn_ArrangedAtColumnWidth_ScrollbarVisible()
	{
		var (spc, contentCol, _) = BuildTwoColumnHgc(OverflowingPanel());

		// The panel must NOT be arranged wider than its column (the bug arranged it at the full grid width).
		Assert.Equal(contentCol.ActualWidth, spc.ActualWidth);
		Assert.True(spc.ActualWidth < 90, $"panel must fit its narrow column, got {spc.ActualWidth}");

		// And its viewport must be bounded so a scrollbar is needed and shown.
		Assert.True(spc.ViewportHeight > 0);
		Assert.True(spc.TotalContentHeight > spc.ViewportHeight,
			$"content ({spc.TotalContentHeight}) must overflow the viewport ({spc.ViewportHeight})");
		Assert.True(spc.HasVerticalScrollbar, "a scrollbar must be shown for overflowing content in an HGC column");
	}

	[Fact]
	public void Spc_InPlainGridStarCell_ArrangedAtCellWidth_ScrollbarVisible()
	{
		var (spc, _) = BuildPlainGridStarCell(OverflowingPanel());

		Assert.True(spc.ActualWidth > 0 && spc.ActualWidth < 90,
			$"plain-grid parity: panel should fit its star cell, got {spc.ActualWidth}");
		Assert.True(spc.TotalContentHeight > spc.ViewportHeight);
		Assert.True(spc.HasVerticalScrollbar, "plain-grid parity: a scrollbar must be shown");
	}

	// ------------------------------------------------------------------
	// MultilineEdit in an HGC flex column (the "wrong width" repro)
	// ------------------------------------------------------------------
	[Fact]
	public void Mle_InHgcFlexColumn_ArrangedAtColumnWidth()
	{
		var (mle, contentCol, _) = BuildTwoColumnHgc(OverflowingEditor());

		Assert.Equal(contentCol.ActualWidth, mle.ActualWidth);
		Assert.True(mle.ActualWidth > 0 && mle.ActualWidth < 90,
			$"editor must be arranged at its (narrow) column width, got {mle.ActualWidth}");
		Assert.True(mle.ActualHeight > 0 && mle.ActualHeight < 1000,
			$"editor must be arranged at a bounded height, got {mle.ActualHeight}");
	}

	[Fact]
	public void Mle_InPlainGridStarCell_ArrangedAtCellWidth()
	{
		var (mle, _) = BuildPlainGridStarCell(OverflowingEditor());

		Assert.True(mle.ActualWidth > 0 && mle.ActualWidth < 90,
			$"plain-grid parity: editor should fit its star cell, got {mle.ActualWidth}");
		Assert.True(mle.ActualHeight > 0 && mle.ActualHeight < 1000,
			$"editor must be arranged at a bounded height, got {mle.ActualHeight}");
	}

	// ------------------------------------------------------------------
	// Real demo topology: NavigationView's right content pane (DemoApp main window).
	// ------------------------------------------------------------------
	[Fact]
	public void NavigationView_ContentPanel_OverflowingContent_ShowsScrollbar()
	{
		var nav = new NavigationView { VerticalAlignment = VerticalAlignment.Fill };
		var item = nav.AddItem("Page");
		nav.SetItemContent(item, panel =>
		{
			for (int i = 0; i < 60; i++)
				panel.AddControl(new MarkupControl(new List<string> { $"line {i:00}" }));
		});

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(nav);
		window.RenderAndGetVisibleContent();

		var spc = nav.ContentPanel;
		Assert.True(spc.ViewportHeight > 0, "content panel must have a real (bounded) viewport height");
		Assert.True(spc.TotalContentHeight > spc.ViewportHeight,
			$"content ({spc.TotalContentHeight}) must overflow the viewport ({spc.ViewportHeight})");
		Assert.True(spc.HasVerticalScrollbar, "NavigationView content pane must show a scrollbar for overflowing content");
	}
}
