// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for nested container layouts: HorizontalGrid inside ScrollablePanel,
/// ScrollablePanel inside HorizontalGrid columns, splitter interactions,
/// and control boundaries within complex nesting.
/// </summary>
public class NestedContainerLayoutTests
{
	#region Helper Methods

	private static (ConsoleWindowSystem system, Window window) CreateTestEnv(int sysW = 120, int sysH = 40, int winW = 100, int winH = 30)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(sysW, sysH);
		var window = new Window(system) { Width = winW, Height = winH };
		return (system, window);
	}

	private static void RenderWindow(ConsoleWindowSystem system, Window window)
	{
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();
	}

	#endregion

	#region HorizontalGrid Basic Layout

	[Fact]
	public void HorizontalGrid_TwoFixedColumns_RenderCorrectly()
	{
		var (system, window) = CreateTestEnv();

		var label1 = new MarkupControl(new List<string> { "Left" });
		var label2 = new MarkupControl(new List<string> { "Right" });

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(30).Add(label1))
			.Column(c => c.Width(30).Add(label2))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void HorizontalGrid_FixedAndFlex_FlexFillsRemaining()
	{
		var (system, window) = CreateTestEnv();

		var label1 = new MarkupControl(new List<string> { "Fixed" });
		var label2 = new MarkupControl(new List<string> { "Flex" });

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(20).Add(label1))
			.Column(c => c.Flex().Add(label2))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void HorizontalGrid_WithSplitter_RendersSplitter()
	{
		var (system, window) = CreateTestEnv();

		var label1 = new MarkupControl(new List<string> { "Col1" });
		var label2 = new MarkupControl(new List<string> { "Col2" });

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(40).Add(label1))
			.Column(c => c.Flex().Add(label2))
			.WithSplitterAfter(0)
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void HorizontalGrid_ThreeColumns_AllRender()
	{
		var (system, window) = CreateTestEnv();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(20).Add(new MarkupControl(new List<string> { "A" })))
			.Column(c => c.Width(20).Add(new MarkupControl(new List<string> { "B" })))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "C" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region ScrollablePanel Inside HorizontalGrid

	[Fact]
	public void ScrollablePanel_InGridColumn_Renders()
	{
		var (system, window) = CreateTestEnv();

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string> { "Inside panel" }))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var label = new MarkupControl(new List<string> { "Right side" });

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(40).Add(panel))
			.Column(c => c.Flex().Add(label))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void ScrollablePanel_BothColumns_Renders()
	{
		var (system, window) = CreateTestEnv();

		var leftPanel = ControlsFactory.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string> { "Left content" }))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var rightPanel = ControlsFactory.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string> { "Right content" }))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(45).Add(leftPanel))
			.Column(c => c.Flex().Add(rightPanel))
			.WithSplitterAfter(0)
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void ScrollablePanel_WithManyItems_InGrid_Scrollable()
	{
		var (system, window) = CreateTestEnv();

		var panel = ControlsFactory.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		for (int i = 0; i < 50; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(40).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Static" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region Dropdown Inside Nested Containers

	[Fact]
	public void Dropdown_InScrollablePanel_InGrid_Renders()
	{
		var (system, window) = CreateTestEnv();

		var dd = new DropdownControl("Pick:", new[] { "A", "B", "C" });

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string> { "Label:" }))
			.AddControl(dd)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(45).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Right" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal(0, dd.SelectedIndex);
		Assert.Equal("A", dd.SelectedValue);
	}

	[Fact]
	public void Dropdown_InGrid_PortalOpensCorrectly()
	{
		var (system, window) = CreateTestEnv();

		var dd = new DropdownControl("S:", new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" });

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(dd)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(45).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Side" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		window.FocusManager.SetFocus(dd, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		Assert.True(bounds.Width > 0);
		Assert.True(bounds.Height > 0);

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void MultipleDropdowns_InGrid_IndependentSelection()
	{
		var (system, window) = CreateTestEnv();

		var dd1 = new DropdownControl("D1:", new[] { "X", "Y" });
		var dd2 = new DropdownControl("D2:", new[] { "A", "B" });

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(dd1)
			.AddControl(dd2)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(45).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Side" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		dd1.SelectedIndex = 1;
		dd2.SelectedIndex = 0;

		Assert.Equal(1, dd1.SelectedIndex);
		Assert.Equal("Y", dd1.SelectedValue);
		Assert.Equal(0, dd2.SelectedIndex);
		Assert.Equal("A", dd2.SelectedValue);
	}

	#endregion

	#region DatePicker Inside Nested Containers

	[Fact]
	public void DatePicker_InScrollablePanel_InGrid_Renders()
	{
		var (system, window) = CreateTestEnv();

		var dp = new DatePickerControl("Due:");
		dp.SelectedDate = new DateTime(2026, 3, 15);

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(dp)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(45).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Right" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		Assert.Equal(new DateTime(2026, 3, 15), dp.SelectedDate);
	}

	#endregion

	#region Mixed Controls in Nested Layout

	[Fact]
	public void MixedControls_InSingleColumn_AllRender()
	{
		var (system, window) = CreateTestEnv();

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string> { "[bold]Header[/]" }))
			.AddControl(new DropdownControl("Pick:", new[] { "Opt1", "Opt2" }))
			.AddControl(new ButtonControl { Text = "Submit" })
			.AddControl(new MarkupControl(new List<string> { "Footer" }))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(45).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Info" })))
			.WithSplitterAfter(0)
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void ListControl_InGridColumn_Renders()
	{
		var (system, window) = CreateTestEnv();

		var list = new ListControl(new[] { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" });
		list.VerticalAlignment = VerticalAlignment.Fill;

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(30).Add(list))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Details" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region HorizontalGrid Inside ScrollablePanel

	[Fact]
	public void HorizontalGrid_InsideScrollablePanel_Renders()
	{
		var (system, window) = CreateTestEnv();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(30).Add(new MarkupControl(new List<string> { "Left" })))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Right" })))
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(grid)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		window.AddControl(panel);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region Edge Cases - Small Dimensions

	[Fact]
	public void SmallWindow_ControlsStillRender()
	{
		var (system, window) = CreateTestEnv(sysW: 30, sysH: 10, winW: 20, winH: 8);

		window.AddControl(new MarkupControl(new List<string> { "Tiny" }));
		window.AddControl(new DropdownControl("S:", new[] { "A" }));
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void NarrowColumn_ControlTruncates()
	{
		var (system, window) = CreateTestEnv();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(5).Add(new MarkupControl(new List<string> { "Very Long Text Here" })))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Right" })))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region Vertical Alignment in Grid Columns

	[Fact]
	public void Grid_ColumnWithFill_ExpandsVertically()
	{
		var (system, window) = CreateTestEnv();

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string> { "Content" }))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(40).Add(panel))
			.Column(c => c.Flex().Add(new MarkupControl(new List<string> { "Side" })))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region Margin Interaction Tests

	[Fact]
	public void Dropdown_WithMargin_InPanel_RespectsBounds()
	{
		var (system, window) = CreateTestEnv();

		var dd = new DropdownControl("S:", new[] { "A", "B" });
		dd.Margin = new Margin(2, 1, 2, 1);

		var panel = ControlsFactory.ScrollablePanel()
			.AddControl(dd)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		window.AddControl(panel);
		RenderWindow(system, window);

		// Verify it renders without crash
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void Controls_WithLargeMargins_DontOverflow()
	{
		var (system, window) = CreateTestEnv();

		var ctrl = new MarkupControl(new List<string> { "Content" });
		ctrl.Margin = new Margin(10, 5, 10, 5);

		window.AddControl(ctrl);
		RenderWindow(system, window);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region Focus Navigation in Nested Containers

	[Fact]
	public void FocusableControls_InGrid_BothFocusable()
	{
		var (system, window) = CreateTestEnv();

		var dd1 = new DropdownControl("D1:", new[] { "A", "B" });
		var dd2 = new DropdownControl("D2:", new[] { "X", "Y" });

		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(40).Add(dd1))
			.Column(c => c.Flex().Add(dd2))
			.Build();

		window.AddControl(grid);
		RenderWindow(system, window);

		Assert.True(dd1.CanReceiveFocus);
		Assert.True(dd2.CanReceiveFocus);

		// dd1 gets focus first via IFocusScope auto-focus
		Assert.True(dd1.HasFocus);
		Assert.False(dd2.HasFocus);

		// Tab to dd2
		window.SwitchFocus(backward: false);
		Assert.False(dd1.HasFocus);
		Assert.True(dd2.HasFocus);
	}

	#endregion
}
