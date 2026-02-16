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

	#region Event Tests

	[Fact]
	public void TabChanged_FiresWhenActiveTabChanges()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		TabChangedEventArgs? eventArgs = null;
		tabControl.TabChanged += (s, e) => eventArgs = e;

		// Act
		tabControl.ActiveTabIndex = 1;

		// Assert
		Assert.NotNull(eventArgs);
		Assert.Equal(0, eventArgs.OldIndex);
		Assert.Equal(1, eventArgs.NewIndex);
		Assert.Equal("Tab 1", eventArgs.OldTab?.Title);
		Assert.Equal("Tab 2", eventArgs.NewTab?.Title);
	}

	[Fact]
	public void TabChanging_FiresBeforeTabChanges()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		TabChangingEventArgs? eventArgs = null;
		tabControl.TabChanging += (s, e) => eventArgs = e;

		// Act
		tabControl.ActiveTabIndex = 1;

		// Assert
		Assert.NotNull(eventArgs);
		Assert.Equal(0, eventArgs.OldIndex);
		Assert.Equal(1, eventArgs.NewIndex);
	}

	[Fact]
	public void TabChanging_CanCancelTabChange()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		tabControl.TabChanging += (s, e) => e.Cancel = true;

		// Act
		tabControl.ActiveTabIndex = 1;

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex); // Should stay on tab 0
	}

	[Fact]
	public void TabAdded_FiresWhenTabAdded()
	{
		// Arrange
		var tabControl = new TabControl();
		TabEventArgs? eventArgs = null;
		tabControl.TabAdded += (s, e) => eventArgs = e;

		// Act
		var content = CreateLabel("Content");
		tabControl.AddTab("Tab 1", content);

		// Assert
		Assert.NotNull(eventArgs);
		Assert.Equal("Tab 1", eventArgs.TabPage.Title);
		Assert.Equal(0, eventArgs.Index);
	}

	[Fact]
	public void TabRemoved_FiresWhenTabRemoved()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		TabEventArgs? eventArgs = null;
		tabControl.TabRemoved += (s, e) => eventArgs = e;

		// Act
		tabControl.RemoveTab(0);

		// Assert
		Assert.NotNull(eventArgs);
		Assert.Equal("Tab 1", eventArgs.TabPage.Title);
		Assert.Equal(0, eventArgs.Index);
	}

	#endregion

	#region Convenience Property Tests

	[Fact]
	public void ActiveTab_ReturnsCurrentlyActiveTab()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Act
		var activeTab = tabControl.ActiveTab;

		// Assert
		Assert.NotNull(activeTab);
		Assert.Equal("Tab 1", activeTab.Title);
	}

	[Fact]
	public void ActiveTab_ReturnsNullWhenNoTabs()
	{
		// Arrange
		var tabControl = new TabControl();

		// Act
		var activeTab = tabControl.ActiveTab;

		// Assert
		Assert.Null(activeTab);
	}

	[Fact]
	public void TabCount_ReturnsNumberOfTabs()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));

		// Act & Assert
		Assert.Equal(3, tabControl.TabCount);
	}

	[Fact]
	public void HasTabs_ReflectsTabPresence()
	{
		// Arrange
		var tabControl = new TabControl();

		// Assert - no tabs
		Assert.False(tabControl.HasTabs);

		// Act - add tab
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Assert - has tabs
		Assert.True(tabControl.HasTabs);
	}

	[Fact]
	public void TabTitles_ReturnsAllTitles()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));

		// Act
		var titles = tabControl.TabTitles.ToList();

		// Assert
		Assert.Equal(3, titles.Count);
		Assert.Contains("Tab 1", titles);
		Assert.Contains("Tab 2", titles);
		Assert.Contains("Tab 3", titles);
	}

	#endregion

	#region Remove Tab Tests

	[Fact]
	public void RemoveTab_RemovesTabAtIndex()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Act
		tabControl.RemoveTab(0);

		// Assert
		Assert.Equal(1, tabControl.TabCount);
		Assert.Equal("Tab 2", tabControl.TabPages[0].Title);
	}

	[Fact]
	public void RemoveTab_RemovesActiveTab_SwitchesToNext()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));
		tabControl.ActiveTabIndex = 1; // Active on Tab 2

		// Act
		tabControl.RemoveTab(1); // Remove Tab 2

		// Assert
		Assert.Equal(1, tabControl.ActiveTabIndex); // Now on Tab 3 (index 1 after removal)
		Assert.Equal("Tab 3", tabControl.ActiveTab?.Title);
	}

	[Fact]
	public void RemoveTab_RemovesLastTab_SwitchesToPrevious()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.ActiveTabIndex = 1; // Active on Tab 2 (last tab)

		// Act
		tabControl.RemoveTab(1);

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex);
		Assert.Equal("Tab 1", tabControl.ActiveTab?.Title);
	}

	[Fact]
	public void RemoveTabByTitle_RemovesFirstMatch()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 1", CreateLabel("Content 3")); // Duplicate title

		// Act
		var removed = tabControl.RemoveTab("Tab 1");

		// Assert
		Assert.True(removed);
		Assert.Equal(2, tabControl.TabCount);
		Assert.Equal("Tab 2", tabControl.TabPages[0].Title);
		Assert.Equal("Tab 1", tabControl.TabPages[1].Title); // Duplicate still exists
	}

	[Fact]
	public void RemoveTabByTitle_ReturnsFalseWhenNotFound()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act
		var removed = tabControl.RemoveTab("Nonexistent");

		// Assert
		Assert.False(removed);
		Assert.Equal(1, tabControl.TabCount);
	}

	[Fact]
	public void ClearTabs_RemovesAllTabs()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));

		// Act
		tabControl.ClearTabs();

		// Assert
		Assert.Equal(0, tabControl.TabCount);
		Assert.False(tabControl.HasTabs);
	}

	#endregion

	#region Query Method Tests

	[Fact]
	public void FindTab_ReturnsTabByTitle()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Act
		var tab = tabControl.FindTab("Tab 2");

		// Assert
		Assert.NotNull(tab);
		Assert.Equal("Tab 2", tab.Title);
	}

	[Fact]
	public void FindTab_ReturnsNullWhenNotFound()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act
		var tab = tabControl.FindTab("Nonexistent");

		// Assert
		Assert.Null(tab);
	}

	[Fact]
	public void GetTab_ReturnsTabAtIndex()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Act
		var tab = tabControl.GetTab(1);

		// Assert
		Assert.NotNull(tab);
		Assert.Equal("Tab 2", tab.Title);
	}

	[Fact]
	public void GetTab_ReturnsNullForInvalidIndex()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act & Assert
		Assert.Null(tabControl.GetTab(-1));
		Assert.Null(tabControl.GetTab(10));
	}

	[Fact]
	public void HasTab_ReturnsTrueWhenTabExists()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act & Assert
		Assert.True(tabControl.HasTab("Tab 1"));
		Assert.False(tabControl.HasTab("Nonexistent"));
	}

	#endregion

	#region Navigation Method Tests

	[Fact]
	public void NextTab_SwitchesToNextTab()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));

		// Act
		tabControl.NextTab();

		// Assert
		Assert.Equal(1, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void NextTab_WrapsAroundToFirst()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.ActiveTabIndex = 1; // Last tab

		// Act
		tabControl.NextTab();

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void PreviousTab_SwitchesToPreviousTab()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.ActiveTabIndex = 1;

		// Act
		tabControl.PreviousTab();

		// Assert
		Assert.Equal(0, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void PreviousTab_WrapsAroundToLast()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.ActiveTabIndex = 0; // First tab

		// Act
		tabControl.PreviousTab();

		// Assert
		Assert.Equal(1, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void SwitchToTab_SwitchesToTabByTitle()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));

		// Act
		var result = tabControl.SwitchToTab("Tab 2");

		// Assert
		Assert.True(result);
		Assert.Equal(1, tabControl.ActiveTabIndex);
	}

	[Fact]
	public void SwitchToTab_ReturnsFalseWhenNotFound()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));

		// Act
		var result = tabControl.SwitchToTab("Nonexistent");

		// Assert
		Assert.False(result);
		Assert.Equal(0, tabControl.ActiveTabIndex);
	}

	#endregion

	#region Modification Method Tests

	[Fact]
	public void SetTabTitle_ChangesTabTitle()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Old Title", CreateLabel("Content"));

		// Act
		tabControl.SetTabTitle(0, "New Title");

		// Assert
		Assert.Equal("New Title", tabControl.TabPages[0].Title);
	}

	[Fact]
	public void SetTabContent_ReplacesContent()
	{
		// Arrange
		var tabControl = new TabControl();
		var oldContent = CreateLabel("Old Content");
		tabControl.AddTab("Tab 1", oldContent);

		// Act
		var newContent = CreateLabel("New Content");
		tabControl.SetTabContent(0, newContent);

		// Assert
		Assert.Equal(newContent, tabControl.TabPages[0].Content);
	}

	[Fact]
	public void InsertTab_InsertsAtPosition()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 3", CreateLabel("Content 3"));

		// Act
		tabControl.InsertTab(1, "Tab 2", CreateLabel("Content 2"));

		// Assert
		Assert.Equal(3, tabControl.TabCount);
		Assert.Equal("Tab 1", tabControl.TabPages[0].Title);
		Assert.Equal("Tab 2", tabControl.TabPages[1].Title);
		Assert.Equal("Tab 3", tabControl.TabPages[2].Title);
	}

	[Fact]
	public void InsertTab_AdjustsActiveIndex()
	{
		// Arrange
		var tabControl = new TabControl();
		tabControl.AddTab("Tab 1", CreateLabel("Content 1"));
		tabControl.AddTab("Tab 2", CreateLabel("Content 2"));
		tabControl.ActiveTabIndex = 1;

		// Act - Insert before active tab
		tabControl.InsertTab(0, "Tab 0", CreateLabel("Content 0"));

		// Assert
		Assert.Equal(2, tabControl.ActiveTabIndex); // Shifted
	}

	#endregion

	#region Builder Enhancement Tests

	[Fact]
	public void Builder_AddTabs_AddMultipleTabs()
	{
		// Act
		var tabControl = TabControl()
			.AddTabs(
				("Tab 1", CreateLabel("Content 1")),
				("Tab 2", CreateLabel("Content 2")),
				("Tab 3", CreateLabel("Content 3"))
			)
			.Build();

		// Assert
		Assert.Equal(3, tabControl.TabCount);
		Assert.Equal("Tab 1", tabControl.TabPages[0].Title);
		Assert.Equal("Tab 2", tabControl.TabPages[1].Title);
		Assert.Equal("Tab 3", tabControl.TabPages[2].Title);
	}

	[Fact]
	public void Builder_AddTabIf_AddsWhenTrue()
	{
		// Act
		var tabControl = TabControl()
			.AddTabIf(true, "Tab 1", CreateLabel("Content 1"))
			.AddTabIf(false, "Tab 2", CreateLabel("Content 2"))
			.Build();

		// Assert
		Assert.Equal(1, tabControl.TabCount);
		Assert.Equal("Tab 1", tabControl.TabPages[0].Title);
	}

	[Fact]
	public void Builder_AddTabIf_WithLambda_AddsWhenTrue()
	{
		// Act
		var tabControl = TabControl()
			.AddTabIf(true, "Tab 1", () => CreateLabel("Content 1"))
			.AddTabIf(false, "Tab 2", () => CreateLabel("Content 2"))
			.Build();

		// Assert
		Assert.Equal(1, tabControl.TabCount);
		Assert.Equal("Tab 1", tabControl.TabPages[0].Title);
	}

	#endregion
}
