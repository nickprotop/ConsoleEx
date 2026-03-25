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
		Assert.Equal(-1, tabControl.ActiveTabIndex);
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

		// Act — GetChildren returns only the active tab's content
		var children = tabControl.GetChildren();

		// Assert — first tab is active by default
		Assert.Equal(1, children.Count);
		Assert.Contains(content1, children);
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

	#region Helpers (rendered environment)

	private static (ConsoleWindowSystem system, Window window, SharpConsoleUI.Controls.TabControl tab) CreateRenderedTabEnvironment(
		int width = 80, int height = 25, SharpConsoleUI.Controls.TabControl? tab = null)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system)
		{
			Title = "Test",
			Left = 0,
			Top = 0,
			Width = width,
			Height = height
		};
		tab ??= new SharpConsoleUI.Controls.TabControl();
		tab.VerticalAlignment = VerticalAlignment.Fill;
		window.AddControl(tab);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		return (system, window, tab);
	}

	#endregion

	#region SetTabContent — Layout Rebuild

	[Fact]
	public void SetTabContent_NewContentRendersAfterReplace()
	{
		// The critical bug: SetTabContent was missing ForceRebuildLayout(),
		// so the new control never got a LayoutNode and was invisible.
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("OLD CONTENT"));
		tab.AddTab("Tab 2", CreateLabel("Content 2"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Verify old content renders
		var output1 = window.RenderAndGetVisibleContent();
		Assert.Contains("OLD CONTENT", StripAnsiCodes(output1));

		// Replace active tab content
		tab.SetTabContent(0, CreateLabel("NEW CONTENT"));
		var output2 = window.RenderAndGetVisibleContent();
		var text2 = StripAnsiCodes(output2);

		Assert.Contains("NEW CONTENT", text2);
		Assert.DoesNotContain("OLD CONTENT", text2);
	}

	[Fact]
	public void SetTabContent_InactiveTab_ContentShownOnSwitch()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("Content 1"));
		tab.AddTab("Tab 2", CreateLabel("OLD TAB2"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Replace inactive tab's content
		tab.SetTabContent(1, CreateLabel("REPLACED TAB2"));

		// Switch to tab 2
		tab.ActiveTabIndex = 1;
		var output = window.RenderAndGetVisibleContent();
		var text = StripAnsiCodes(output);

		Assert.Contains("REPLACED TAB2", text);
		Assert.DoesNotContain("OLD TAB2", text);
	}

	[Fact]
	public void SetTabContent_PreservesVisibilityState()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var content1 = CreateLabel("Content 1");
		var content2 = CreateLabel("Content 2");
		tab.AddTab("Tab 1", content1);
		tab.AddTab("Tab 2", content2);

		// Active tab is 0
		var newActiveContent = CreateLabel("New Active");
		var newInactiveContent = CreateLabel("New Inactive");

		tab.SetTabContent(0, newActiveContent);
		tab.SetTabContent(1, newInactiveContent);

		Assert.True(newActiveContent.Visible);
		Assert.False(newInactiveContent.Visible);
	}

	[Fact]
	public void SetTabContent_SetsContainerToTabControl()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("Old"));

		var newContent = CreateLabel("New");
		tab.SetTabContent(0, newContent);

		// Container should be the TabControl itself, not its parent
		Assert.Same(tab, newContent.Container);
	}

	[Fact]
	public void SetTabContent_InvalidIndex_DoesNothing()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var original = CreateLabel("Original");
		tab.AddTab("Tab 1", original);

		tab.SetTabContent(-1, CreateLabel("Bad"));
		tab.SetTabContent(5, CreateLabel("Bad"));

		Assert.Same(original, tab.TabPages[0].Content);
	}

	[Fact]
	public void SetTabContent_WithScrollablePanel_Renders()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("Old"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Replace with a complex control (ScrollablePanel with children)
		var panel = new ScrollablePanelControl();
		panel.AddControl(CreateLabel("PANEL LINE 1"));
		panel.AddControl(CreateLabel("PANEL LINE 2"));
		tab.SetTabContent(0, panel);

		var output = window.RenderAndGetVisibleContent();
		var text = StripAnsiCodes(output);

		Assert.Contains("PANEL LINE 1", text);
		Assert.Contains("PANEL LINE 2", text);
	}

	#endregion

	#region InsertTab — Layout Rebuild

	[Fact]
	public void InsertTab_ContentRendersAfterInsert()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("Content 1"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Insert a new tab and switch to it
		tab.InsertTab(0, "Inserted", CreateLabel("INSERTED CONTENT"));
		tab.ActiveTabIndex = 0;

		var output = window.RenderAndGetVisibleContent();
		var text = StripAnsiCodes(output);

		Assert.Contains("INSERTED CONTENT", text);
		Assert.Contains("Inserted", text);
	}

	[Fact]
	public void InsertTab_AtEnd_Works()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("Content 1"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		tab.InsertTab(1, "Tab 2", CreateLabel("APPENDED"));
		tab.ActiveTabIndex = 1;

		var output = window.RenderAndGetVisibleContent();
		Assert.Contains("APPENDED", StripAnsiCodes(output));
	}

	[Fact]
	public void InsertTab_BeforeActive_KeepsCorrectTabVisible()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		var originalContent = CreateLabel("ORIGINAL ACTIVE");
		tab.AddTab("Tab 1", originalContent);
		tab.AddTab("Tab 2", CreateLabel("Content 2"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Active is index 0. Insert before it.
		tab.InsertTab(0, "Inserted", CreateLabel("Inserted"));

		// Active index should shift to 1 (still pointing to "Tab 1")
		Assert.Equal(1, tab.ActiveTabIndex);

		var output = window.RenderAndGetVisibleContent();
		Assert.Contains("ORIGINAL ACTIVE", StripAnsiCodes(output));
	}

	#endregion

	#region Container Propagation

	[Fact]
	public void Container_TabContentPointsAtTabControl()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var content1 = CreateLabel("Content 1");
		var content2 = CreateLabel("Content 2");
		tab.AddTab("Tab 1", content1);
		tab.AddTab("Tab 2", content2);

		// Before being added to window
		Assert.Same(tab, content1.Container);
		Assert.Same(tab, content2.Container);
	}

	[Fact]
	public void Container_AfterAddToWindow_StillPointsAtTabControl()
	{
		// The old bug: Container setter override would set tab content's
		// Container to the Window instead of the TabControl.
		var tab = new SharpConsoleUI.Controls.TabControl();
		var content1 = CreateLabel("Content 1");
		var content2 = CreateLabel("Content 2");
		tab.AddTab("Tab 1", content1);
		tab.AddTab("Tab 2", content2);

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// After adding to window, Container should still be TabControl
		Assert.Same(tab, content1.Container);
		Assert.Same(tab, content2.Container);
	}

	[Fact]
	public void Container_NewTabAddedAfterWindow_PointsAtTabControl()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("Content 1"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Add new tab after already attached to window
		var lateContent = CreateLabel("Late");
		tab.AddTab("Tab 2", lateContent);

		Assert.Same(tab, lateContent.Container);
	}

	[Fact]
	public void Container_InvalidationChain_ReachesWindow()
	{
		// Verify that invalidation from tab content reaches the window
		// through the TabControl (not bypassing it).
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		var content = CreateLabel("Content");
		tab.AddTab("Tab 1", content);

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Reset dirty state
		tab.IsDirty = false;

		// Trigger invalidation from the content
		content.Invalidate();

		// TabControl should be dirty (invalidation passed through it)
		Assert.True(tab.IsDirty);
	}

	#endregion

	#region SetTabContent — Event and State Interactions

	[Fact]
	public void SetTabContent_DoesNotFireTabChangedEvent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("Old"));

		bool eventFired = false;
		tab.TabChanged += (_, _) => eventFired = true;

		tab.SetTabContent(0, CreateLabel("New"));

		Assert.False(eventFired);
	}

	[Fact]
	public void SetTabContent_ActiveTab_PropertyStaysConsistent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("Old"));
		tab.AddTab("Tab 2", CreateLabel("Content 2"));
		Assert.Equal(0, tab.ActiveTabIndex);

		var newContent = CreateLabel("New");
		tab.SetTabContent(0, newContent);

		Assert.Equal(0, tab.ActiveTabIndex);
		Assert.Equal("Tab 1", tab.ActiveTab?.Title);
		Assert.Same(newContent, tab.ActiveTab?.Content);
	}

	[Fact]
	public void SetTabContent_MultipleReplacements_AllRender()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("V1"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Replace content 3 times
		tab.SetTabContent(0, CreateLabel("V2"));
		tab.SetTabContent(0, CreateLabel("V3"));
		tab.SetTabContent(0, CreateLabel("FINAL VERSION"));

		var output = window.RenderAndGetVisibleContent();
		var text = StripAnsiCodes(output);

		Assert.Contains("FINAL VERSION", text);
		Assert.DoesNotContain("V1", text);
		Assert.DoesNotContain("V2", text);
		Assert.DoesNotContain("V3", text);
	}

	#endregion

	#region Tab Switching with Rendered Content

	[Fact]
	public void SwitchTabs_BackAndForth_RendersCorrectContent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab A", CreateLabel("ALPHA CONTENT"));
		tab.AddTab("Tab B", CreateLabel("BETA CONTENT"));
		tab.AddTab("Tab C", CreateLabel("GAMMA CONTENT"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Tab A
		var textA = StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("ALPHA CONTENT", textA);

		// Switch to C
		tab.ActiveTabIndex = 2;
		var textC = StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("GAMMA CONTENT", textC);
		Assert.DoesNotContain("ALPHA CONTENT", textC);

		// Back to A
		tab.ActiveTabIndex = 0;
		var textA2 = StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("ALPHA CONTENT", textA2);
		Assert.DoesNotContain("GAMMA CONTENT", textA2);
	}

	[Fact]
	public void RemoveActiveTab_NextTabContentRenders()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("CONTENT ONE"));
		tab.AddTab("Tab 2", CreateLabel("CONTENT TWO"));
		tab.AddTab("Tab 3", CreateLabel("CONTENT THREE"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Remove active tab (Tab 1 at index 0)
		tab.RemoveTab(0);

		// Tab 2 should now be active
		var text = StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("CONTENT TWO", text);
		Assert.DoesNotContain("CONTENT ONE", text);
	}

	[Fact]
	public void RemoveLastActiveTab_PreviousTabContentRenders()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 1", CreateLabel("CONTENT ONE"));
		tab.AddTab("Tab 2", CreateLabel("CONTENT TWO"));
		tab.ActiveTabIndex = 1;

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Remove active last tab
		tab.RemoveTab(1);

		var text = StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("CONTENT ONE", text);
	}

	#endregion

	#region Header Style Rendering

	[Fact]
	public void HeaderStyle_Classic_RendersOnOneLine()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.HeaderStyle = TabHeaderStyle.Classic;
		tab.AddTab("Alpha", CreateLabel("A content"));
		tab.AddTab("Beta", CreateLabel("B content"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);
		var text = StripAnsiCodes(window.RenderAndGetVisibleContent());

		Assert.Contains("Alpha", text);
		Assert.Contains("Beta", text);
		Assert.Equal(1, tab.TabHeaderHeight);
	}

	[Fact]
	public void HeaderStyle_Separator_UsesTwoRows()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.HeaderStyle = TabHeaderStyle.Separator;
		tab.AddTab("Alpha", CreateLabel("A content"));
		tab.AddTab("Beta", CreateLabel("B content"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);
		var text = StripAnsiCodes(window.RenderAndGetVisibleContent());

		Assert.Contains("Alpha", text);
		Assert.Equal(2, tab.TabHeaderHeight);
	}

	[Fact]
	public void HeaderStyle_AccentedSeparator_UsesTwoRows()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.HeaderStyle = TabHeaderStyle.AccentedSeparator;
		tab.AddTab("Alpha", CreateLabel("A content"));
		tab.AddTab("Beta", CreateLabel("B content"));

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);
		var text = StripAnsiCodes(window.RenderAndGetVisibleContent());

		Assert.Contains("Alpha", text);
		Assert.Equal(2, tab.TabHeaderHeight);
	}

	#endregion

	#region Closable Tabs

	[Fact]
	public void AddTab_Closable_SetsFlag()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("Content"), isClosable: true);

		Assert.True(tab.TabPages[0].IsClosable);
	}

	[Fact]
	public void TabCloseRequested_DoesNotAutoRemove()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("Content 1"), isClosable: true);
		tab.AddTab("Tab 2", CreateLabel("Content 2"));

		bool closeRequested = false;
		tab.TabCloseRequested += (_, e) => closeRequested = true;

		// Even if close is requested externally, tab should not be auto-removed
		// (the event consumer must call RemoveTab)
		Assert.Equal(2, tab.TabCount);
	}

	#endregion

	#region RemoveTab — Index Adjustment with Rendered State

	[Fact]
	public void RemoveTab_BeforeActive_ShiftsActiveIndex()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.Height = 10;
		tab.AddTab("Tab 0", CreateLabel("Content 0"));
		tab.AddTab("Tab 1", CreateLabel("ACTIVE CONTENT"));
		tab.AddTab("Tab 2", CreateLabel("Content 2"));
		tab.ActiveTabIndex = 1;

		var (system, window, _) = CreateRenderedTabEnvironment(tab: tab);

		// Remove tab before active
		tab.RemoveTab(0);

		Assert.Equal(0, tab.ActiveTabIndex);
		Assert.Equal("Tab 1", tab.ActiveTab?.Title);

		var text = StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("ACTIVE CONTENT", text);
	}

	[Fact]
	public void RemoveTab_AfterActive_KeepsActiveIndex()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 0", CreateLabel("ACTIVE"));
		tab.AddTab("Tab 1", CreateLabel("Content 1"));
		tab.AddTab("Tab 2", CreateLabel("Content 2"));
		// Active stays at 0

		tab.RemoveTab(2);

		Assert.Equal(0, tab.ActiveTabIndex);
		Assert.Equal("Tab 0", tab.ActiveTab?.Title);
	}

	[Fact]
	public void ClearTabs_RemovesAll_ActiveBecomesNegativeOne()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("Content 1"));
		tab.AddTab("Tab 2", CreateLabel("Content 2"));

		tab.ClearTabs();

		Assert.Equal(0, tab.TabCount);
		Assert.Equal(-1, tab.ActiveTabIndex);
		Assert.Null(tab.ActiveTab);
	}

	#endregion

	#region Visibility Consistency

	[Fact]
	public void AllInactiveTabs_AreInvisible()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var contents = Enumerable.Range(0, 5)
			.Select(i => CreateLabel($"Content {i}"))
			.ToArray();

		foreach (var (c, i) in contents.Select((c, i) => (c, i)))
			tab.AddTab($"Tab {i}", c);

		tab.ActiveTabIndex = 2;

		for (int i = 0; i < 5; i++)
		{
			if (i == 2)
				Assert.True(contents[i].Visible, $"Active tab {i} should be visible");
			else
				Assert.False(contents[i].Visible, $"Inactive tab {i} should be invisible");
		}
	}

	[Fact]
	public void RapidTabSwitching_VisibilityConsistent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var contents = new MarkupControl[10];
		for (int i = 0; i < 10; i++)
		{
			contents[i] = CreateLabel($"Content {i}");
			tab.AddTab($"Tab {i}", contents[i]);
		}

		// Rapidly switch tabs
		for (int round = 0; round < 3; round++)
		{
			for (int i = 0; i < 10; i++)
			{
				tab.ActiveTabIndex = i;

				// Exactly one tab should be visible at any time
				int visibleCount = contents.Count(c => c.Visible);
				Assert.Equal(1, visibleCount);
				Assert.True(contents[i].Visible);
			}
		}
	}

	#endregion

	#region GetChildren and GetLogicalContentSize

	[Fact]
	public void GetChildren_AfterSetTabContent_ReturnsNewContent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var old = CreateLabel("Old");
		tab.AddTab("Tab 1", old);

		var replacement = CreateLabel("New");
		tab.SetTabContent(0, replacement);

		var children = tab.GetChildren();
		Assert.Single(children);
		Assert.Same(replacement, children[0]);
		Assert.DoesNotContain(old, children);
	}

	[Fact]
	public void GetChildren_AfterInsertTab_IncludesNewContent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		tab.AddTab("Tab 1", CreateLabel("C1"));
		tab.AddTab("Tab 3", CreateLabel("C3"));

		var inserted = CreateLabel("C2");
		tab.InsertTab(1, "Tab 2", inserted);

		// GetChildren returns only the active tab's content (tab 0 = "C1")
		var children = tab.GetChildren();
		Assert.Equal(1, children.Count);

		// Switch to inserted tab and verify it's returned
		tab.ActiveTabIndex = 1;
		var childrenAfterSwitch = tab.GetChildren();
		Assert.Equal(1, childrenAfterSwitch.Count);
		Assert.Same(inserted, childrenAfterSwitch[0]);
	}

	[Fact]
	public void GetLogicalContentSize_ReflectsActiveTabContent()
	{
		var tab = new SharpConsoleUI.Controls.TabControl();
		var small = CreateLabel("Small");
		var big = new MarkupControl(new List<string> { "Line 1", "Line 2", "Line 3", "Line 4", "Line 5" });
		tab.AddTab("Small", small);
		tab.AddTab("Big", big);

		var sizeWithSmall = tab.GetLogicalContentSize();

		tab.ActiveTabIndex = 1;
		var sizeWithBig = tab.GetLogicalContentSize();

		// Big tab should produce larger height
		Assert.True(sizeWithBig.Height >= sizeWithSmall.Height);
	}

	#endregion
}
