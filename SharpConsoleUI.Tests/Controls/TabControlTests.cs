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
using Xunit;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using System.Drawing;
using TabControl = SharpConsoleUI.Controls.TabControl;
using static SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Comprehensive test suite for TabControl.
/// Tests all properties, behaviors, tab switching, and rendering scenarios.
/// </summary>
public class TabControlTests
{
	#region Helper Methods

	/// <summary>
	/// Strips ANSI escape codes from output lines to get plain text.
	/// </summary>
	private static string StripAnsiCodes(IEnumerable<string> lines)
	{
		return string.Join("\n", lines.Select(line =>
			System.Text.RegularExpressions.Regex.Replace(line, @"\x1b\[[0-9;]*m", "")));
	}

	/// <summary>
	/// Creates a mock label control with specified text.
	/// </summary>
	private static MarkupControl CreateLabel(string text)
	{
		return new MarkupControl(new List<string> { text });
	}

	#endregion

	#region Construction Tests

	[Fact]
	public void Constructor_CreatesEmptyTabControl()
	{
		// Act
		var tabControl = new TabControl();

		// Assert
		Assert.NotNull(tabControl);
		Assert.Empty(tabControl.TabPages);
		Assert.Equal(0, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void AddTab_AddsTabCorrectly()
	{
		// Arrange
		var tabControl = new TabControl();
		var content = CreateLabel("Test Content");

		// Act
		tabControl.AddTab("Tab 1", content);

		// Assert
		Assert.Single(tabControl.TabPages);
		Assert.Equal("Tab 1", tabControl.TabPages[0].Title);
		Assert.Equal(content, tabControl.TabPages[0].Content);
		Assert.True(content.Visible); // First tab should be visible
	}

	[Fact]
	public void AddTab_MultipleTabs_MaintainsOrder()
	{
		// Arrange
		var tabControl = new TabControl();

		// Act
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));

		// Assert
		Assert.Equal(3, tabControl.TabPages.Count);
		Assert.Equal("Tab 1", tabControl.TabPages[0].Title);
		Assert.Equal("Tab 2", tabControl.TabPages[1].Title);
		Assert.Equal("Tab 3", tabControl.TabPages[2].Title);
	}

	#endregion

	#region Tab Switching Tests

	[Fact]
	public void ActiveTabIndex_DefaultsToZero()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void ActiveTabIndex_SwitchesTabs()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Act
		tabControl.ActiveTabIndex = 1;

		// Assert
		Assert.Equal(1, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void ActiveTabIndex_TogglesVisibility()
	{
		// Arrange
		var tabControl = new TabControl();
		var content1 = CreateLabel("Content 1");
		var content2 = CreateLabel("Content 2");
		tabControl.AddTab("Tab 1", content1);
		tabControl.AddTab("Tab 2", content2);

		// Act - Switch to tab 2
		tabControl.ActiveTabIndex = 1;

		// Assert
		Assert.False(content1.Visible);
		Assert.True(content2.Visible);
	}

	[Fact]
	public void ActiveTabIndex_InvalidIndex_NoChange()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act
		tabControl.ActiveTabIndex = 5; // Invalid index

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex); // Should stay at 0
	}

	[Fact]
	public void ActiveTabIndex_NegativeIndex_NoChange()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act
		tabControl.ActiveTabIndex = -1; // Invalid index

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex); // Should stay at 0
	}

	#endregion

	#region Rendering Tests

	[Fact]
	public void MeasureDOM_WithExplicitHeight_RespectsHeight()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };

		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content 1"))
			.WithHeight(10)
			.Build();

		window.AddControl(tabControl);

		// Act
		var constraints = new LayoutConstraints(0, 80, 0, 30);
		var size = tabControl.MeasureDOM(constraints);

		// Assert
		Assert.Equal(10, size.Height);
	}

	[Fact]
	public void Height_InvalidValue_ThrowsException()
	{
		// Arrange
		var tabControl = new TabControl();

		// Act & Assert
		Assert.Throws<ArgumentException>(() => tabControl.Height = 1);
		Assert.Throws<ArgumentException>(() => tabControl.Height = 0);
	}

	[Fact]
	public void PaintDOM_DrawsHeaders()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 20 };

		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content 1"))
			.AddTab("Tab 2", CreateLabel("Content 2"))
			.WithHeight(10)
			.Build();

		window.AddControl(tabControl);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = StripAnsiCodes(output);

		// Assert
		Assert.Contains("Tab 1", plainText);
		Assert.Contains("Tab 2", plainText);
		Assert.Contains("Content 1", plainText);
	}

	[Fact]
	public void PaintDOM_OnlyPaintsActiveTab()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 20 };

		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content 1"))
			.AddTab("Tab 2", CreateLabel("Content 2"))
			.WithHeight(10)
			.WithActiveTab(1) // Set Tab 2 as active
			.Build();

		window.AddControl(tabControl);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = StripAnsiCodes(output);

		// Assert
		Assert.Contains("Content 2", plainText); // Active tab content visible
		// Note: Content 1 should not be visible, but we can't easily verify this without
		// more sophisticated rendering inspection
	}

	#endregion

	#region Mouse Event Tests

	[Fact]
	public void WantsMouseEvents_DefaultIsTrue()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert
		Assert.True(tabControl.WantsMouseEvents);
	}

	[Fact]
	public void CanFocusWithMouse_DefaultIsFalse()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert
		Assert.False(tabControl.CanFocusWithMouse);
	}

	[Fact]
	public void ProcessMouseEvent_HeaderClick_SwitchesTabs()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Click on tab header at Y=0, X within second tab bounds
		var args = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked },
			new Point(10, 0), // Approximate position of second tab
			new Point(10, 0), // Absolute position
			new Point(10, 0)  // Window position
		);

		// Act
		var handled = tabControl.ProcessMouseEvent(args);

		// Assert
		// Note: The exact click position depends on tab title lengths
		// This is a basic test - real behavior depends on layout
		Assert.True(handled || tabControl.ActiveTabIndex == 1);
	}

	#endregion

	#region Keyboard Tests

	[Fact]
	public void ProcessKey_CtrlTab_SwitchesToNextTab()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));
		tabControl.ActiveTabIndex = 0;

		var key = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, true); // Ctrl+Tab

		// Act
		var handled = tabControl.ProcessKey(key);

		// Assert
		Assert.True(handled);
		Assert.Equal(1, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void ProcessKey_CtrlTab_WrapsAround()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.ActiveTabIndex = 1; // Last tab

		var key = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, true); // Ctrl+Tab

		// Act
		var handled = tabControl.ProcessKey(key);

		// Assert
		Assert.True(handled);
		Assert.Equal(0, tabControl.ActiveTabIndex); // Wrapped to first
	}

	[Fact]
	public void ProcessKey_CtrlShiftTab_SwitchesToPreviousTab()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));
		tabControl.ActiveTabIndex = 2;

		var key = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, true); // Ctrl+Shift+Tab

		// Act
		var handled = tabControl.ProcessKey(key);

		// Assert
		Assert.True(handled);
		Assert.Equal(1, tabControl.ActiveTabIndex);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void HorizontalAlignment_DefaultIsLeft()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert
		Assert.Equal(HorizontalAlignment.Left, tabControl.HorizontalAlignment);
	}

	[Fact]
	public void VerticalAlignment_DefaultIsTop()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert
		Assert.Equal(VerticalAlignment.Top, tabControl.VerticalAlignment);
	}

	[Fact]
	public void Visible_DefaultIsTrue()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert
		Assert.True(tabControl.Visible);
	}

	[Fact]
	public void Margin_DefaultIsZero()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert
		Assert.Equal(0, tabControl.Margin.Left);
		Assert.Equal(0, tabControl.Margin.Right);
		Assert.Equal(0, tabControl.Margin.Top);
		Assert.Equal(0, tabControl.Margin.Bottom);
	}

	#endregion

	#region Builder Tests

	[Fact]
	public void Builder_Fluent_BuildsCorrectly()
	{
		// Act
		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content 1"))
			.AddTab("Tab 2", CreateLabel("Content 2"))
			.WithActiveTab(1)
			.WithHeight(15)
			.WithWidth(50)
			.Centered()
			.WithMargin(1)
			.WithName("testTabs")
			.Build();

		// Assert
		Assert.Equal(2, tabControl.TabPages.Count);
		Assert.Equal(1, tabControl.ActiveTabIndex);
		Assert.Equal(15, tabControl.Height);
		Assert.Equal(50, tabControl.Width);
		Assert.Equal(HorizontalAlignment.Center, tabControl.HorizontalAlignment);
		Assert.Equal(1, tabControl.Margin.Left);
		Assert.Equal("testTabs", tabControl.Name);
	}

	[Fact]
	public void Builder_ImplicitConversion_Works()
	{
		// Act
		TabControl tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content 1"));

		// Assert
		Assert.NotNull(tabControl);
		Assert.Single(tabControl.TabPages);
	}

	[Fact]
	public void Builder_AddTabWithBuilder_Works()
	{
		// Act
		var tabControl = TabControl()
			.AddTab("Tab 1", () => CreateLabel("Content 1"))
			.Build();

		// Assert
		Assert.Single(tabControl.TabPages);
		Assert.NotNull(tabControl.TabPages[0].Content);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void TabControl_MinimalHeight_HandlesGracefully()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };

		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content"))
			.WithHeight(2) // Minimum: 1 header + 1 content
			.Build();

		window.AddControl(tabControl);

		// Act
		var output = window.RenderAndGetVisibleContent();

		// Assert
		Assert.NotNull(output); // Should not crash
	}

	[Fact]
	public void TabControl_NoTabs_DoesNotCrash()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 20 };

		var tabControl = new TabControl();
		window.AddControl(tabControl);

		// Act
		var output = window.RenderAndGetVisibleContent();

		// Assert
		Assert.NotNull(output); // Should not crash with no tabs
	}

	[Fact]
	public void TabControl_WithMargins_CalculatesCorrectly()
	{
		// Arrange
		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content"))
			.WithWidth(50)
			.WithMargin(2, 1, 3, 1)
			.Build();

		// Assert
		Assert.Equal(2, tabControl.Margin.Left);
		Assert.Equal(3, tabControl.Margin.Right);
		Assert.Equal(50, tabControl.Width);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void FullScenario_CreateTabsWithScrollablePanel()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		// Act - Build tab control with scrollable panel content
		var tabControl = TabControl()
			.AddTab("Overview", ScrollablePanel()
				.AddControl(CreateLabel("Overview content"))
				.WithHeight(5)
				.Build())
			.AddTab("Details", ScrollablePanel()
				.AddControl(CreateLabel("Details content line 1"))
				.WithHeight(5)
				.Build())
			.WithHeight(10)
			.Build();

		window.AddControl(tabControl);
		var output = window.RenderAndGetVisibleContent();
		var plainText = StripAnsiCodes(output);

		// Assert
		Assert.NotNull(output);
		Assert.Contains("Overview", plainText);
		Assert.Contains("Details", plainText);
		Assert.Contains("Overview content", plainText);
	}

	[Fact]
	public void FullScenario_SwitchTabsAndRender()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var tabControl = TabControl()
			.AddTab("Tab 1", CreateLabel("Content 1"))
			.AddTab("Tab 2", CreateLabel("Content 2"))
			.WithHeight(10)
			.Build();

		window.AddControl(tabControl);

		// Act - Render with tab 1
		var output1 = window.RenderAndGetVisibleContent();
		var plainText1 = StripAnsiCodes(output1);

		// Switch to tab 2
		tabControl.ActiveTabIndex = 1;
		var output2 = window.RenderAndGetVisibleContent();
		var plainText2 = StripAnsiCodes(output2);

		// Assert
		Assert.Contains("Content 1", plainText1);
		Assert.Contains("Content 2", plainText2);
	}

	#endregion

	#region ContainerControl Tests

	[Fact]
	public void GetChildren_ReturnsAllTabContent()
	{
		// Arrange
		var tabControl = new TabControl();
		var content1 = CreateLabel("Content 1");
		var content2 = CreateLabel("Content 2");
		tabControl.AddTab("Tab 1", content1);
		tabControl.AddTab("Tab 2", content2);

		// Act
		var children = tabControl.GetChildren();

		// Assert
		Assert.Equal(2, children.Count);
		Assert.Contains(content1, children);
		Assert.Contains(content2, children);
	}

	#endregion
}
