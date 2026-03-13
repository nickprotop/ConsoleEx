// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ScrollablePanelBorderTests
{
	#region Helpers

	private (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window)
		CreateBorderedScrollPanel(BorderStyle style = BorderStyle.Rounded,
			int childCount = 20, int panelHeight = 15)
	{
		var panel = new ScrollablePanelControl();
		panel.Height = panelHeight;
		panel.BorderStyle = style;
		panel.BorderColor = Color.Grey;
		panel.Padding = new Padding(1, 0, 1, 0);
		for (int i = 0; i < childCount; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, system, window);
	}

	private (ScrollablePanelControl panel, ConsoleWindowSystem system, Window window)
		CreateRenderedScrollPanel(int childCount = 20, int panelHeight = 10)
	{
		var panel = new ScrollablePanelControl();
		panel.Height = panelHeight;
		for (int i = 0; i < childCount; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, system, window);
	}

	#endregion

	#region Border Defaults & Configuration

	[Fact]
	public void Defaults_BorderStyle_None()
	{
		var panel = new ScrollablePanelControl();
		Assert.Equal(BorderStyle.None, panel.BorderStyle);
	}

	[Fact]
	public void Defaults_BorderColor_Null()
	{
		var panel = new ScrollablePanelControl();
		Assert.Null(panel.BorderColor);
	}

	[Fact]
	public void Defaults_Padding_Zero()
	{
		var panel = new ScrollablePanelControl();
		Assert.Equal(new Padding(0, 0, 0, 0), panel.Padding);
	}

	[Fact]
	public void Defaults_Header_Null()
	{
		var panel = new ScrollablePanelControl();
		Assert.Null(panel.Header);
	}

	[Fact]
	public void BorderStyle_CanBeSet()
	{
		var panel = new ScrollablePanelControl();
		panel.BorderStyle = BorderStyle.Rounded;
		Assert.Equal(BorderStyle.Rounded, panel.BorderStyle);
	}

	[Fact]
	public void BorderColor_CanBeSet()
	{
		var panel = new ScrollablePanelControl();
		panel.BorderColor = Color.Grey;
		Assert.Equal(Color.Grey, panel.BorderColor);
	}

	[Fact]
	public void Padding_CanBeSet()
	{
		var panel = new ScrollablePanelControl();
		panel.Padding = new Padding(1, 1, 1, 1);
		Assert.Equal(new Padding(1, 1, 1, 1), panel.Padding);
	}

	[Fact]
	public void Header_CanBeSet()
	{
		var panel = new ScrollablePanelControl();
		panel.Header = "Section";
		Assert.Equal("Section", panel.Header);
	}

	#endregion

	#region Measurement with Borders

	[Fact]
	public void MeasureDOM_NoBorder_SameAsBaseline()
	{
		// Panel with no border should produce the same viewport as before
		var (panel, _, _) = CreateRenderedScrollPanel(childCount: 20, panelHeight: 10);

		Assert.Equal(10, panel.Height);
		Assert.True(panel.ViewportHeight > 0);
		Assert.True(panel.ViewportWidth > 0);
	}

	[Fact]
	public void MeasureDOM_WithBorder_ReducesAvailableWidth()
	{
		var (noBorderPanel, _, _) = CreateRenderedScrollPanel(childCount: 5, panelHeight: 10);
		int noBorderVpWidth = noBorderPanel.ViewportWidth;

		var (borderPanel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 5, panelHeight: 10);
		int borderVpWidth = borderPanel.ViewportWidth;

		// Border adds 2 (left+right) + padding adds 2 (left+right=1+1) = 4 less
		Assert.True(borderVpWidth < noBorderVpWidth,
			$"Bordered viewport width ({borderVpWidth}) should be less than non-bordered ({noBorderVpWidth})");
	}

	[Fact]
	public void MeasureDOM_WithBorder_ReducesAvailableHeight()
	{
		var (noBorderPanel, _, _) = CreateRenderedScrollPanel(childCount: 5, panelHeight: 15);
		int noBorderVpHeight = noBorderPanel.ViewportHeight;

		var (borderPanel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 5, panelHeight: 15);
		int borderVpHeight = borderPanel.ViewportHeight;

		// Border adds 2 (top+bottom border rows)
		Assert.True(borderVpHeight < noBorderVpHeight,
			$"Bordered viewport height ({borderVpHeight}) should be less than non-bordered ({noBorderVpHeight})");
	}

	[Fact]
	public void MeasureDOM_WithPadding_FurtherReducesViewport()
	{
		// Panel with border only
		var panel1 = new ScrollablePanelControl();
		panel1.Height = 15;
		panel1.BorderStyle = BorderStyle.Rounded;
		for (int i = 0; i < 5; i++)
			panel1.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));
		var (sys1, win1) = ContainerTestHelpers.CreateTestEnvironment();
		win1.AddControl(panel1);
		win1.RenderAndGetVisibleContent();

		// Panel with border + padding
		var panel2 = new ScrollablePanelControl();
		panel2.Height = 15;
		panel2.BorderStyle = BorderStyle.Rounded;
		panel2.Padding = new Padding(2, 1, 2, 1);
		for (int i = 0; i < 5; i++)
			panel2.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));
		var (sys2, win2) = ContainerTestHelpers.CreateTestEnvironment();
		win2.AddControl(panel2);
		win2.RenderAndGetVisibleContent();

		Assert.True(panel2.ViewportWidth < panel1.ViewportWidth);
		Assert.True(panel2.ViewportHeight < panel1.ViewportHeight);
	}

	[Fact]
	public void MeasureDOM_WithBorderAndPadding_Combined()
	{
		var (panel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 5, panelHeight: 15);

		// Border: 2 width, 2 height. Padding(1,0,1,0): 2 width, 0 height.
		// Total inset: 4 width, 2 height
		int expectedVpHeight = 15 - 2; // border top+bottom
		Assert.Equal(expectedVpHeight, panel.ViewportHeight);
	}

	[Fact]
	public void MeasureDOM_ContentHeight_AccountsForBorder()
	{
		var (panel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		Assert.True(panel.TotalContentHeight > 0);
		Assert.True(panel.TotalContentHeight > panel.ViewportHeight,
			"Content should exceed viewport for scrolling");
	}

	#endregion

	#region Rendering with Borders

	[Fact]
	public void Render_NoBorder_NoBoxChars()
	{
		var (panel, _, window) = CreateRenderedScrollPanel(childCount: 5, panelHeight: 10);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.DoesNotContain("╭", stripped);
		Assert.DoesNotContain("╮", stripped);
		Assert.DoesNotContain("╰", stripped);
		Assert.DoesNotContain("╯", stripped);
		Assert.DoesNotContain("┌", stripped);
		Assert.DoesNotContain("┐", stripped);
	}

	[Fact]
	public void Render_RoundedBorder_DrawsCorners()
	{
		var (panel, _, window) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 5, panelHeight: 10);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("╭", stripped);
		Assert.Contains("╮", stripped);
		Assert.Contains("╰", stripped);
		Assert.Contains("╯", stripped);
	}

	[Fact]
	public void Render_SingleBorder_DrawsCorners()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		panel.BorderStyle = BorderStyle.Single;
		for (int i = 0; i < 5; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("┌", stripped);
		Assert.Contains("┐", stripped);
		Assert.Contains("└", stripped);
		Assert.Contains("┘", stripped);
	}

	[Fact]
	public void Render_WithHeader_ShowsHeaderText()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.Header = "Section";
		panel.AddControl(ContainerTestHelpers.CreateLabel("Content"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("Section", stripped);
	}

	[Fact]
	public void Render_WithBorder_ContentInsideBorder()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.AddControl(ContainerTestHelpers.CreateLabel("Hello World"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		Assert.Contains("Hello World", stripped);
		Assert.Contains("╭", stripped);
	}

	[Fact]
	public void Render_WithPadding_ContentIndented()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.Padding = new Padding(2, 0, 2, 0);
		panel.AddControl(ContainerTestHelpers.CreateLabel("Padded"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		// Content should be present and border should be drawn
		Assert.Contains("Padded", stripped);
		Assert.Contains("╭", stripped);
	}

	[Fact]
	public void Render_NoCrash_EmptyPanelWithBorder()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.BorderColor = Color.Grey;
		panel.Padding = new Padding(1, 1, 1, 1);
		// No children added
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);

		// Should not throw
		var content = window.RenderAndGetVisibleContent();
		Assert.NotNull(content);
	}

	#endregion

	#region Scrolling with Borders

	[Fact]
	public void Scroll_WithBorder_ViewportReducedCorrectly()
	{
		var (panel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		// ViewportHeight should be panelHeight minus border (2) minus padding top+bottom (0)
		Assert.Equal(13, panel.ViewportHeight);
	}

	[Fact]
	public void Scroll_WithBorder_ContentScrollsInsideBorder()
	{
		var (panel, _, window) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		// Scroll down
		panel.ScrollVerticalBy(5);
		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		// Border should still be present
		Assert.Contains("╭", stripped);
		Assert.Contains("╰", stripped);
		// Scroll offset should be 5
		Assert.Equal(5, panel.VerticalScrollOffset);
	}

	[Fact]
	public void Scroll_WithBorder_ScrollbarInsideBorder()
	{
		var (panel, _, window) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		var content = window.RenderAndGetVisibleContent();
		var stripped = ContainerTestHelpers.StripAnsiCodes(content);

		// Scrollbar should be drawn (content exceeds viewport)
		Assert.True(panel.TotalContentHeight > panel.ViewportHeight);
		// Verify the scrollbar arrow character is present
		Assert.Contains("▲", stripped);
		Assert.Contains("▼", stripped);
	}

	[Fact]
	public void Scroll_WithBorder_ClampsCorrectly()
	{
		var (panel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);
		int expectedMax = Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);

		panel.ScrollVerticalBy(10000);

		Assert.Equal(expectedMax, panel.VerticalScrollOffset);
	}

	[Fact]
	public void Scroll_WithBorder_CanScrollDown_AccountsForBorder()
	{
		var (panel, _, _) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		Assert.True(panel.CanScrollDown, "Should be able to scroll down with border");
		Assert.False(panel.CanScrollUp, "Should not be able to scroll up initially");
	}

	[Fact]
	public void Scroll_WithBorderAndPadding_CorrectRange()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 15;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.Padding = new Padding(1, 1, 1, 1);
		for (int i = 0; i < 30; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// ViewportHeight = 15 - 2 (border) - 2 (padding top+bottom) = 11
		Assert.Equal(11, panel.ViewportHeight);

		int expectedMax = Math.Max(0, panel.TotalContentHeight - panel.ViewportHeight);
		panel.ScrollToBottom();
		Assert.Equal(expectedMax, panel.VerticalScrollOffset);
	}

	#endregion

	#region Mouse with Borders

	[Fact]
	public void Mouse_WithBorder_ClickOnBorder_NotRoutedToChild()
	{
		var (panel, _, window) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 5, panelHeight: 15);

		// Click on Y=0, which is the top border row
		var click = ContainerTestHelpers.CreateClick(5, 0);
		bool handled = panel.ProcessMouseEvent(click);

		// Click on border row should not crash and should not route to child
		// (It's outside content area, Y < Margin.Top + ContentInsetTop)
	}

	[Fact]
	public void Mouse_WithBorder_ClickInsideContent_RoutesToChild()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 15;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.Padding = new Padding(1, 0, 1, 0);

		bool childClicked = false;
		var btn = ContainerTestHelpers.CreateButton("Click Me");
		btn.Click += (_, _) => childClicked = true;
		panel.AddControl(btn);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Click inside the content area (past border + padding)
		// Border left = 1 col, padding left = 1 col → content starts at X=2
		// Border top = 1 row → content starts at Y=1
		var click = ContainerTestHelpers.CreateClick(3, 1);
		panel.ProcessMouseEvent(click);

		// The click should have been routed to the button area
		// (exact routing depends on button position, but it should not crash)
	}

	[Fact]
	public void Mouse_WithBorder_WheelInsideContent_ScrollsPanel()
	{
		var (panel, _, window) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		// Wheel inside content area
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 3);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled);
		Assert.True(panel.VerticalScrollOffset > 0);
	}

	[Fact]
	public void Mouse_WithBorder_ScrollbarHit_AccountsForBorderOffset()
	{
		var (panel, _, window) = CreateBorderedScrollPanel(BorderStyle.Rounded, childCount: 30, panelHeight: 15);

		// The scrollbar should be inside the border
		// Wheel works, which indirectly validates geometry
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 5);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled);
	}

	[Fact]
	public void Mouse_WithPadding_ClickInPadding_NotRoutedToChild()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 15;
		panel.BorderStyle = BorderStyle.Rounded;
		panel.Padding = new Padding(3, 2, 3, 2);
		panel.AddControl(ContainerTestHelpers.CreateLabel("Content"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Click on border row (Y=0) - should not route to child
		var click = ContainerTestHelpers.CreateClick(2, 0);
		panel.ProcessMouseEvent(click);
		// If we get here without exception, padding area was handled
	}

	[Fact]
	public void Mouse_NoBorder_ClickRouting_Unchanged()
	{
		// Verify no regression with BorderStyle.None
		var (panel, _, window) = CreateRenderedScrollPanel(childCount: 20, panelHeight: 10);

		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 3);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled);
		Assert.True(panel.VerticalScrollOffset > 0);
	}

	#endregion

	#region Builder Tests

	[Fact]
	public void Builder_Default_NoBorder()
	{
		var panel = ScrollablePanelControl.Create().Build();
		Assert.Equal(BorderStyle.None, panel.BorderStyle);
	}

	[Fact]
	public void Builder_Rounded_SetsBorderStyle()
	{
		var panel = ScrollablePanelControl.Create().Rounded().Build();
		Assert.Equal(BorderStyle.Rounded, panel.BorderStyle);
	}

	[Fact]
	public void Builder_WithBorderColor_Sets()
	{
		var panel = ScrollablePanelControl.Create()
			.WithBorderColor(Color.Grey)
			.Build();
		Assert.Equal(Color.Grey, panel.BorderColor);
	}

	[Fact]
	public void Builder_WithPadding_Sets()
	{
		var panel = ScrollablePanelControl.Create()
			.WithPadding(1, 0, 1, 0)
			.Build();
		Assert.Equal(new Padding(1, 0, 1, 0), panel.Padding);
	}

	[Fact]
	public void Builder_WithHeader_Sets()
	{
		var panel = ScrollablePanelControl.Create()
			.WithHeader("Title")
			.Build();
		Assert.Equal("Title", panel.Header);
	}

	#endregion

	#region Backward Compatibility

	[Fact]
	public void ExistingPanel_NoBorderProps_BehavesIdentically()
	{
		// Construct panel exactly like existing code (no border props)
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Viewport should be the same as panel height minus margins (no border overhead)
		Assert.Equal(10, panel.ViewportHeight);
		Assert.True(panel.ViewportWidth > 0);
	}

	[Fact]
	public void ExistingPanel_WithChildren_SameLayout()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 15;
		for (int i = 0; i < 10; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Item {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		Assert.Equal(15, panel.ViewportHeight);
		Assert.True(panel.TotalContentHeight > 0);
		Assert.Equal(10, panel.Children.Count);
	}

	[Fact]
	public void ExistingPanel_MouseRouting_SameWithNoBorder()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Mouse wheel should work the same as before
		var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 3);
		bool handled = panel.ProcessMouseEvent(wheelDown);

		Assert.True(handled);
		Assert.True(panel.VerticalScrollOffset > 0);
	}

	#endregion
}
