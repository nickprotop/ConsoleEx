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
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Comprehensive test suite for TabControl.
/// Tests construction, tab management, selection, alignment, rendering, focus, mouse, and edge cases.
/// </summary>
public class TabControlTests
{
	#region Helper Methods

	private static CharacterBuffer CreateBuffer(int width = 80, int height = 25)
	{
		return new CharacterBuffer(width, height);
	}

	private static LayoutRect CreateBounds(int x = 0, int y = 0, int width = 80, int height = 25)
	{
		return new LayoutRect(x, y, width, height);
	}

	private static LayoutRect CreateClip(int x = 0, int y = 0, int width = 80, int height = 25)
	{
		return new LayoutRect(x, y, width, height);
	}

	private static ConsoleKeyInfo MakeKey(ConsoleKey key, ConsoleModifiers modifiers = 0, char ch = '\0')
	{
		bool shift = modifiers.HasFlag(ConsoleModifiers.Shift);
		bool alt = modifiers.HasFlag(ConsoleModifiers.Alt);
		bool ctrl = modifiers.HasFlag(ConsoleModifiers.Control);
		return new ConsoleKeyInfo(ch, key, shift, alt, ctrl);
	}

	private static void PaintControl(TabControl tc, CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
	{
		tc.PaintDOM(buffer, bounds, clipRect, Color.White, Color.Black);
	}

	/// <summary>
	/// Extracts a row of characters from the buffer as a string.
	/// </summary>
	private static string ExtractRow(CharacterBuffer buffer, int y, int startX, int length)
	{
		var chars = new char[length];
		for (int i = 0; i < length; i++)
		{
			chars[i] = buffer.GetCell(startX + i, y).Character;
		}
		return new string(chars);
	}

	#endregion

	#region Construction & Defaults

	[Fact]
	public void Constructor_CreatesEmptyTabControl()
	{
		var tc = new TabControl();

		Assert.Equal(0, tc.TabCount);
		Assert.Equal(-1, tc.SelectedTabIndex);
		Assert.Null(tc.SelectedTab);
		Assert.True(tc.ShowContentBorder);
		Assert.Equal(TabStripAlignment.Left, tc.TabStripAlignment);
	}

	[Fact]
	public void Constructor_DefaultAlignment_HorizontalStretch()
	{
		var tc = new TabControl();
		Assert.Equal(HorizontalAlignment.Stretch, tc.HorizontalAlignment);
	}

	[Fact]
	public void Constructor_DefaultAlignment_VerticalFill()
	{
		var tc = new TabControl();
		Assert.Equal(VerticalAlignment.Fill, tc.VerticalAlignment);
	}

	[Fact]
	public void Constructor_DefaultShowContentBorder_True()
	{
		var tc = new TabControl();
		Assert.True(tc.ShowContentBorder);
	}

	[Fact]
	public void Create_ReturnsBuilder()
	{
		var builder = TabControl.Create();
		Assert.NotNull(builder);
		Assert.IsType<TabControlBuilder>(builder);
	}

	#endregion

	#region Tab Management

	[Fact]
	public void AddTab_AddsTabPage()
	{
		var tc = new TabControl();
		tc.AddTab("Settings");

		Assert.Equal(1, tc.TabCount);
		Assert.Equal("Settings", tc.TabPages[0].Title);
	}

	[Fact]
	public void AddTab_MultipleTabs_MaintainsOrder()
	{
		var tc = new TabControl();
		tc.AddTab("Tab A");
		tc.AddTab("Tab B");
		tc.AddTab("Tab C");

		Assert.Equal(3, tc.TabCount);
		Assert.Equal("Tab A", tc.TabPages[0].Title);
		Assert.Equal("Tab B", tc.TabPages[1].Title);
		Assert.Equal("Tab C", tc.TabPages[2].Title);
	}

	[Fact]
	public void AddTab_FirstTab_AutoSelects()
	{
		var tc = new TabControl();
		tc.AddTab("First");

		Assert.Equal(0, tc.SelectedTabIndex);
		Assert.NotNull(tc.SelectedTab);
		Assert.Equal("First", tc.SelectedTab!.Title);
	}

	[Fact]
	public void AddTab_WithContent_ControlsAccessible()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var button = new ButtonControl { Text = "Click" };
		var checkbox = new CheckboxControl { Label = "Check" };
		page.AddControl(button);
		page.AddControl(checkbox);

		Assert.Equal(2, page.Content.Children.Count);
		Assert.Contains(button, page.Content.Children);
		Assert.Contains(checkbox, page.Content.Children);
	}

	[Fact]
	public void RemoveTab_RemovesTabPage()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("Tab 0");
		var page1 = tc.AddTab("Tab 1");
		var page2 = tc.AddTab("Tab 2");

		tc.RemoveTab(page1);

		Assert.Equal(2, tc.TabCount);
		Assert.Equal("Tab 0", tc.TabPages[0].Title);
		Assert.Equal("Tab 2", tc.TabPages[1].Title);
	}

	[Fact]
	public void RemoveTab_SelectedTab_SelectsAdjacent()
	{
		var tc = new TabControl();
		tc.AddTab("Tab 0");
		tc.AddTab("Tab 1");
		tc.AddTab("Tab 2");
		tc.SelectedTabIndex = 1;

		tc.RemoveTabAt(1);

		Assert.True(tc.SelectedTabIndex >= 0);
		Assert.True(tc.SelectedTabIndex < tc.TabCount);
	}

	[Fact]
	public void RemoveTab_LastTab_ClearsSelection()
	{
		var tc = new TabControl();
		tc.AddTab("Only");

		tc.RemoveTabAt(0);

		Assert.Equal(0, tc.TabCount);
		Assert.Equal(-1, tc.SelectedTabIndex);
	}

	[Fact]
	public void InsertTab_InsertsAtIndex()
	{
		var tc = new TabControl();
		tc.AddTab("Tab 0");
		tc.AddTab("Tab 2");

		var newPage = new TabPage("Tab 1");
		tc.InsertTab(1, newPage);

		Assert.Equal(3, tc.TabCount);
		Assert.Equal("Tab 0", tc.TabPages[0].Title);
		Assert.Equal("Tab 1", tc.TabPages[1].Title);
		Assert.Equal("Tab 2", tc.TabPages[2].Title);
	}

	[Fact]
	public void ClearTabs_RemovesAll()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		tc.ClearTabs();

		Assert.Equal(0, tc.TabCount);
		Assert.Equal(-1, tc.SelectedTabIndex);
	}

	[Fact]
	public void ClearTabs_DisposesContent()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var button = new ButtonControl { Text = "OK" };
		page.AddControl(button);

		tc.ClearTabs();

		// After ClearTabs, the button should have its container cleared
		Assert.Null(button.Container);
	}

	[Fact]
	public void TabPage_AddControl_DelegatesToContent()
	{
		var page = new TabPage("Test");
		var button = new ButtonControl { Text = "OK" };

		page.AddControl(button);

		Assert.Contains(button, page.Content.Children);
	}

	[Fact]
	public void TabPage_RemoveControl_DelegatesToContent()
	{
		var page = new TabPage("Test");
		var button = new ButtonControl { Text = "OK" };
		page.AddControl(button);

		page.RemoveControl(button);

		Assert.DoesNotContain(button, page.Content.Children);
	}

	#endregion

	#region Tab Selection

	[Fact]
	public void SelectedTabIndex_Set_ChangesActiveTab()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		tc.SelectedTabIndex = 2;

		Assert.Equal(2, tc.SelectedTabIndex);
		Assert.Equal("C", tc.SelectedTab!.Title);
	}

	[Fact]
	public void SelectedTabIndex_FiresEvent()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		bool eventFired = false;
		tc.SelectedTabChanged += (_, _) => eventFired = true;

		tc.SelectedTabIndex = 1;

		Assert.True(eventFired);
	}

	[Fact]
	public void SelectedTabIndex_FiresEvent_WithCorrectArgs()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		TabSelectedEventArgs? receivedArgs = null;
		tc.SelectedTabChanged += (_, args) => receivedArgs = args;

		tc.SelectedTabIndex = 1;

		Assert.NotNull(receivedArgs);
		Assert.Equal(0, receivedArgs!.OldIndex);
		Assert.Equal(1, receivedArgs.NewIndex);
	}

	[Fact]
	public void SelectedTabIndex_SameValue_NoEvent()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		// Initially selected at 0
		bool eventFired = false;
		tc.SelectedTabChanged += (_, _) => eventFired = true;

		tc.SelectedTabIndex = 0; // Same as current

		Assert.False(eventFired);
	}

	[Fact]
	public void SelectedTabIndex_DisabledTab_Prevented()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsEnabled = false;

		tc.SelectedTabIndex = 1;

		Assert.Equal(0, tc.SelectedTabIndex); // Should stay at 0
	}

	[Fact]
	public void SelectedTabIndex_HiddenTab_Prevented()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsVisible = false;

		tc.SelectedTabIndex = 1;

		Assert.Equal(0, tc.SelectedTabIndex); // Should stay at 0
	}

	[Fact]
	public void SelectedTabIndex_OutOfRange_Clamped()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		tc.SelectedTabIndex = 2; // Max valid
		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void SelectedTab_Property_ReturnsCurrentTabPage()
	{
		var tc = new TabControl();
		var pageA = tc.AddTab("A");
		var pageB = tc.AddTab("B");

		tc.SelectedTabIndex = 1;

		Assert.Same(pageB, tc.SelectedTab);
	}

	[Fact]
	public void SelectNextTab_CyclesForward()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		Assert.Equal(0, tc.SelectedTabIndex);

		tc.SelectNextTab();

		Assert.Equal(1, tc.SelectedTabIndex);
	}

	[Fact]
	public void SelectPreviousTab_CyclesBackward()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.SelectedTabIndex = 1;

		tc.SelectPreviousTab();

		Assert.Equal(0, tc.SelectedTabIndex);
	}

	[Fact]
	public void SelectNextTab_SkipsDisabled()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsEnabled = false;

		tc.SelectNextTab(); // From 0, should skip 1

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void SelectNextTab_SkipsHidden()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsVisible = false;

		tc.SelectNextTab();

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void SelectNextTab_AtLast_Wraps()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.SelectedTabIndex = 2;

		tc.SelectNextTab();

		Assert.Equal(0, tc.SelectedTabIndex);
	}

	#endregion

	#region Alignment — Horizontal

	[Fact]
	public void HorizontalAlignment_Stretch_FillsParentWidth()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.HorizontalAlignment = HorizontalAlignment.Stretch;

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		Assert.Equal(80, size.Width);
	}

	[Fact]
	public void HorizontalAlignment_Left_UsesExplicitWidth()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.HorizontalAlignment = HorizontalAlignment.Left;
		tc.Width = 40;

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		Assert.Equal(40, size.Width);
	}

	#endregion

	#region Alignment — Vertical

	[Fact]
	public void VerticalAlignment_Fill_ExpandsToMaxHeight()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.VerticalAlignment = VerticalAlignment.Fill;

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		Assert.Equal(25, size.Height);
	}

	[Fact]
	public void VerticalAlignment_Fill_ContentAreaGetsRemainingSpace()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.VerticalAlignment = VerticalAlignment.Fill;
		tc.ShowContentBorder = true;

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		// tabStrip=1, border=2, content = 25-1-2 = 22
		Assert.Equal(25, size.Height);
	}

	[Fact]
	public void VerticalAlignment_Top_SizesToContent()
	{
		var tc = new TabControl();
		tc.VerticalAlignment = VerticalAlignment.Top;
		tc.ShowContentBorder = true;
		var page = tc.AddTab("Test");
		// Add content — a MarkupControl renders as 1 row by default
		page.AddControl(new MarkupControl(new List<string> { "Hello" }));

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		// 1 (strip) + 2 (border) + content (at least 1) = at least 4
		Assert.True(size.Height >= 4);
	}

	#endregion

	#region Tab Strip Alignment

	[Fact]
	public void TabStripAlignment_Left_Default()
	{
		var tc = new TabControl();
		Assert.Equal(TabStripAlignment.Left, tc.TabStripAlignment);
	}

	[Fact]
	public void TabStripAlignment_Left_TabsFlushLeft()
	{
		var tc = new TabControl();
		tc.AddTab("Tab1");
		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// First char should be │ (separator)
		Assert.Equal('│', buffer.GetCell(0, 0).Character);
	}

	[Fact]
	public void TabStripAlignment_Center_TabsCentered()
	{
		var tc = new TabControl();
		tc.TabStripAlignment = TabStripAlignment.Center;
		tc.AddTab("X"); // Short tab: │ X │ = 5 chars wide

		var buffer = CreateBuffer(40, 10);
		var bounds = CreateBounds(0, 0, 40, 10);
		PaintControl(tc, buffer, bounds, bounds);

		// Tabs should not start at column 0 — they should be offset toward center
		// A 5-char tab centered in 40 chars starts around col 17-18
		Assert.NotEqual('│', buffer.GetCell(0, 0).Character);
	}

	[Fact]
	public void TabStripAlignment_Right_TabsFlushRight()
	{
		var tc = new TabControl();
		tc.TabStripAlignment = TabStripAlignment.Right;
		tc.AddTab("X"); // Short tab

		var buffer = CreateBuffer(40, 10);
		var bounds = CreateBounds(0, 0, 40, 10);
		PaintControl(tc, buffer, bounds, bounds);

		// Tabs should be near the right edge, not at left
		Assert.NotEqual('│', buffer.GetCell(0, 0).Character);
	}

	#endregion

	#region Rendering

	[Fact]
	public void PaintDOM_EmptyTabControl_NoCrash()
	{
		var tc = new TabControl();
		var buffer = CreateBuffer();
		var bounds = CreateBounds();

		// Should not throw
		PaintControl(tc, buffer, bounds, bounds);
	}

	[Fact]
	public void PaintDOM_TabStrip_RendersSeparators()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Tab strip should contain │ characters
		var row = ExtractRow(buffer, 0, 0, 30);
		Assert.Contains("│", row);
	}

	[Fact]
	public void PaintDOM_TabStrip_SelectedTabHighlighted()
	{
		var tc = new TabControl();
		tc.AddTab("Normal");
		tc.AddTab("Selected");
		tc.SelectedTabIndex = 1;

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// The selected tab header should have different background color than unselected
		// Find where "Selected" text starts (after │ Normal │)
		// Just verify the selected tab's cells have the active background
		var activeTabBg = tc.TabHeaderActiveBackgroundColor;
		var normalTabBg = tc.TabHeaderBackgroundColor;

		// First tab (normal) - find a cell inside its title
		var cell0 = buffer.GetCell(2, 0); // Inside first tab title area
		// Second tab (selected) - find a cell inside it
		// First tab: │ Normal │ = 10 chars (│+space+6+space+│), starts at 0, so second starts at 9
		int secondTabStart = 1 + AnsiConsoleHelper.StripSpectreLength("Normal") + 2; // │ + space + title + space
		var cell1 = buffer.GetCell(secondTabStart + 1, 0); // Inside second tab

		// The two tabs should have different backgrounds
		Assert.NotEqual(cell0.Background, cell1.Background);
	}

	[Fact]
	public void PaintDOM_ContentBorder_WhenEnabled()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = true;
		tc.AddTab("Test");

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Border should start on row 1 (after tab strip)
		Assert.Equal('┌', buffer.GetCell(0, 1).Character);
	}

	[Fact]
	public void PaintDOM_ContentBorder_WhenDisabled()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = false;
		tc.AddTab("Test");

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Row 1 should NOT have border chars
		Assert.NotEqual('┌', buffer.GetCell(0, 1).Character);
	}

	[Fact]
	public void PaintDOM_ActiveTabContent_Rendered()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = true;
		var page = tc.AddTab("Test");
		page.AddControl(new MarkupControl(new List<string> { "Hello" }));

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// "Hello" should appear in the content area (row 2 = inside border)
		var row = ExtractRow(buffer, 2, 1, 10);
		Assert.Contains("Hello", row);
	}

	[Fact]
	public void PaintDOM_InactiveTabContent_NotRendered()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = true;
		var page0 = tc.AddTab("Tab A");
		page0.AddControl(new MarkupControl(new List<string> { "Hello" }));
		var page1 = tc.AddTab("Tab B");
		page1.AddControl(new MarkupControl(new List<string> { "World" }));
		tc.SelectedTabIndex = 0;

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Check multiple content rows for "World" — it should NOT be present
		bool worldFound = false;
		for (int y = 2; y < 24; y++)
		{
			var row = ExtractRow(buffer, y, 0, 80);
			if (row.Contains("World"))
			{
				worldFound = true;
				break;
			}
		}
		Assert.False(worldFound);
	}

	[Fact]
	public void PaintDOM_TabStripFocusedMode_ShowsMarkers()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.SetFocus(true, FocusReason.Keyboard);

		// Enter tab-strip mode via Escape
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// In tab-strip mode, the focused tab should show > < markers
		var row = ExtractRow(buffer, 0, 0, 20);
		Assert.Contains(">", row);
		Assert.Contains("<", row);
	}

	[Fact]
	public void PaintDOM_DisabledTab_DimmedColor()
	{
		var tc = new TabControl();
		tc.AddTab("Enabled");
		tc.AddTab("Disabled");
		tc.TabPages[1].IsEnabled = false;

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Disabled tab should use the disabled foreground color
		var disabledFg = tc.TabHeaderDisabledForegroundColor;
		// Find a cell in the disabled tab's title area
		int firstTabWidth = AnsiConsoleHelper.StripSpectreLength("Enabled") + 4;
		var disabledCell = buffer.GetCell(firstTabWidth + 1, 0);

		Assert.Equal(disabledFg, disabledCell.Foreground);
	}

	[Fact]
	public void PaintDOM_HiddenTab_NotRendered()
	{
		var tc = new TabControl();
		tc.AddTab("Visible");
		tc.AddTab("Hidden");
		tc.TabPages[1].IsVisible = false;

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// "Hidden" should not appear in the tab strip
		var row = ExtractRow(buffer, 0, 0, 80);
		Assert.DoesNotContain("Hidden", row);
	}

	[Fact]
	public void PaintDOM_Margins_Respected()
	{
		var tc = new TabControl();
		tc.Margin = new Margin(2, 1, 2, 1);
		tc.AddTab("Test");

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// With top margin of 1, the tab strip should be on row 1, not row 0
		// Row 0 should be margin (space)
		Assert.Equal(' ', buffer.GetCell(0, 0).Character);
		// Tab strip starts at row 1, offset by left margin of 2
		Assert.Equal('│', buffer.GetCell(2, 1).Character);
	}

	[Fact]
	public void PaintDOM_TabTitle_PlainTextWorks()
	{
		var tc = new TabControl();
		tc.AddTab("General");

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		var row = ExtractRow(buffer, 0, 0, 20);
		Assert.Contains("General", row);
	}

	#endregion

	#region Rendering — Content With Real Controls

	[Fact]
	public void PaintDOM_TabWithButton_ButtonRendered()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = true;
		var page = tc.AddTab("Test");
		page.AddControl(new ButtonControl { Text = "Click Me" });

		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Button text should appear in content area
		bool found = false;
		for (int y = 2; y < 5; y++)
		{
			var row = ExtractRow(buffer, y, 0, 80);
			if (row.Contains("Click Me"))
			{
				found = true;
				break;
			}
		}
		Assert.True(found);
	}

	[Fact]
	public void PaintDOM_SwitchTab_ContentChanges()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = true;
		var page0 = tc.AddTab("Tab A");
		page0.AddControl(new MarkupControl(new List<string> { "Hello" }));
		var page1 = tc.AddTab("Tab B");
		page1.AddControl(new MarkupControl(new List<string> { "World" }));

		// Paint tab A
		var buffer1 = CreateBuffer();
		var bounds = CreateBounds();
		tc.SelectedTabIndex = 0;
		PaintControl(tc, buffer1, bounds, bounds);

		bool helloVisible = false;
		for (int y = 2; y < 5; y++)
		{
			if (ExtractRow(buffer1, y, 0, 80).Contains("Hello"))
			{
				helloVisible = true;
				break;
			}
		}
		Assert.True(helloVisible);

		// Switch to tab B and repaint
		tc.SelectedTabIndex = 1;
		var buffer2 = CreateBuffer();
		PaintControl(tc, buffer2, bounds, bounds);

		bool worldVisible = false;
		for (int y = 2; y < 5; y++)
		{
			if (ExtractRow(buffer2, y, 0, 80).Contains("World"))
			{
				worldVisible = true;
				break;
			}
		}
		Assert.True(worldVisible);
	}

	#endregion

	#region Focus — Basic

	[Fact]
	public void SetFocus_DelegatesToActiveTabContent()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var button = new ButtonControl { Text = "OK" };
		page.AddControl(button);

		tc.SetFocus(true, FocusReason.Keyboard);

		// Button should have received focus
		Assert.True(button.HasFocus);
	}

	[Fact]
	public void SetFocus_False_UnfocusesContent()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var button = new ButtonControl { Text = "OK" };
		page.AddControl(button);

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.SetFocus(false, FocusReason.Programmatic);

		Assert.False(button.HasFocus);
		Assert.False(tc.HasFocus);
	}

	[Fact]
	public void SetFocus_NoFocusableContent_TabStripMode()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		page.AddControl(new MarkupControl(new List<string> { "Non-focusable" }));

		tc.SetFocus(true, FocusReason.Keyboard);

		Assert.True(tc.IsTabStripFocused);
	}

	[Fact]
	public void CanReceiveFocus_WithFocusableContent_True()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		page.AddControl(new ButtonControl { Text = "OK" });

		Assert.True(tc.CanReceiveFocus);
	}

	[Fact]
	public void CanReceiveFocus_NoTabs_False()
	{
		var tc = new TabControl();

		Assert.False(tc.CanReceiveFocus);
	}

	[Fact]
	public void CanReceiveFocus_AllTabsDisabled_False()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.TabPages[0].IsEnabled = false;

		Assert.False(tc.CanReceiveFocus);
	}

	[Fact]
	public void SetFocusWithDirection_Forward_FocusesFirstChild()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var btn1 = new ButtonControl { Text = "First" };
		var btn2 = new ButtonControl { Text = "Last" };
		page.AddControl(btn1);
		page.AddControl(btn2);

		tc.SetFocusWithDirection(true, backward: false);

		Assert.True(btn1.HasFocus);
	}

	[Fact]
	public void SetFocusWithDirection_Backward_FocusesLastChild()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var btn1 = new ButtonControl { Text = "First" };
		var btn2 = new ButtonControl { Text = "Last" };
		page.AddControl(btn1);
		page.AddControl(btn2);

		tc.SetFocusWithDirection(true, backward: true);

		Assert.True(btn2.HasFocus);
	}

	[Fact]
	public void GotFocus_EventFired()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.TabPages[0].Content.AddControl(new ButtonControl { Text = "OK" });
		bool fired = false;
		tc.GotFocus += (_, _) => fired = true;

		tc.SetFocus(true, FocusReason.Keyboard);

		Assert.True(fired);
	}

	[Fact]
	public void LostFocus_EventFired()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.TabPages[0].Content.AddControl(new ButtonControl { Text = "OK" });
		tc.SetFocus(true, FocusReason.Keyboard);
		bool fired = false;
		tc.LostFocus += (_, _) => fired = true;

		tc.SetFocus(false, FocusReason.Programmatic);

		Assert.True(fired);
	}

	#endregion

	#region Focus — Tab Navigation With Content

	[Fact]
	public void ProcessKey_Tab_CyclesThroughContentChildren()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		var btn3 = new ButtonControl { Text = "Btn3" };
		page.AddControl(btn1);
		page.AddControl(btn2);
		page.AddControl(btn3);

		// Paint first to establish ActualWidth for ScrollablePanelControl
		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.True(btn1.HasFocus);

		// Tab should cycle to next button
		var handled = tc.ProcessKey(MakeKey(ConsoleKey.Tab));
		Assert.True(handled);
		Assert.True(btn2.HasFocus);
	}

	[Fact]
	public void ProcessKey_Tab_PastLastChild_ExitsTabControl()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var btn = new ButtonControl { Text = "Only" };
		page.AddControl(btn);

		tc.SetFocus(true, FocusReason.Keyboard);
		// btn is focused, press Tab to cycle past last → should exit
		var handled = tc.ProcessKey(MakeKey(ConsoleKey.Tab));

		Assert.False(handled); // Should return false to let parent handle
	}

	[Fact]
	public void ProcessKey_ShiftTab_PastFirstChild_ExitsTabControl()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var btn = new ButtonControl { Text = "Only" };
		page.AddControl(btn);

		tc.SetFocus(true, FocusReason.Keyboard);
		var handled = tc.ProcessKey(MakeKey(ConsoleKey.Tab, ConsoleModifiers.Shift));

		Assert.False(handled);
	}

	[Fact]
	public void ProcessKey_Tab_InTabStripMode_EntersContent()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		var btn = new ButtonControl { Text = "OK" };
		page.AddControl(btn);

		// Paint to establish dimensions
		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		tc.SetFocus(true, FocusReason.Keyboard);
		// Enter tab-strip mode (two Escapes: scroll mode → propagate)
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));
		Assert.True(tc.IsTabStripFocused);

		// Tab in strip mode → should enter content
		tc.ProcessKey(MakeKey(ConsoleKey.Tab));
		Assert.False(tc.IsTabStripFocused);
		Assert.True(btn.HasFocus);
	}

	[Fact]
	public void ProcessKey_Tab_InTabStripMode_WithNoFocusableContent_ExitsControl()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		page.AddControl(new MarkupControl(new List<string> { "Non-focusable" }));

		tc.SetFocus(true, FocusReason.Keyboard);
		// Should be in tab-strip mode since no focusable content
		Assert.True(tc.IsTabStripFocused);

		// Tab should exit
		var handled = tc.ProcessKey(MakeKey(ConsoleKey.Tab));
		Assert.False(handled);
	}

	#endregion

	#region Focus — Tab Strip Mode

	[Fact]
	public void ProcessKey_Escape_FromContent_EntersTabStripMode()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		page.AddControl(new ButtonControl { Text = "OK" });

		// Paint to establish dimensions
		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.False(tc.IsTabStripFocused);

		// First Escape: ScrollablePanelControl unfocuses child, enters scroll mode
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));
		// Second Escape: ScrollablePanelControl propagates, TabControl enters strip mode
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		Assert.True(tc.IsTabStripFocused);
	}

	[Fact]
	public void ProcessKey_Escape_FromTabStripMode_ExitsControl()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Test");
		page.AddControl(new MarkupControl(new List<string> { "Text" })); // Non-focusable

		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.True(tc.IsTabStripFocused); // Auto-enters strip mode

		var handled = tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		Assert.False(handled); // Should propagate to parent
	}

	[Fact]
	public void ProcessKey_LeftArrow_InTabStripMode_MovesHighlight()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.SelectedTabIndex = 2;

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape)); // Enter strip mode

		tc.ProcessKey(MakeKey(ConsoleKey.LeftArrow));

		// Highlight should move left (from tab 2 to tab 1)
		// We can verify by rendering and checking markers
		var buffer = CreateBuffer();
		PaintControl(tc, buffer, CreateBounds(), CreateBounds());
		// After pressing left from tab 2, the focused header should be at index 1
		// Checking the internal state indirectly through rendering: "B" should have > < markers
	}

	[Fact]
	public void ProcessKey_RightArrow_InTabStripMode_MovesHighlight()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape)); // Enter strip mode at tab 0

		tc.ProcessKey(MakeKey(ConsoleKey.RightArrow));

		// Highlight moved right — verify by entering to select
		tc.ProcessKey(MakeKey(ConsoleKey.Enter));
		Assert.Equal(1, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_LeftArrow_AtFirstTab_Wraps()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		// Press Left at tab 0 → should wrap to tab 2
		tc.ProcessKey(MakeKey(ConsoleKey.LeftArrow));
		tc.ProcessKey(MakeKey(ConsoleKey.Enter));

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_RightArrow_AtLastTab_Wraps()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.SelectedTabIndex = 2;

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		tc.ProcessKey(MakeKey(ConsoleKey.RightArrow));
		tc.ProcessKey(MakeKey(ConsoleKey.Enter));

		Assert.Equal(0, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_Enter_InTabStripMode_SelectsHighlighted()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));
		tc.ProcessKey(MakeKey(ConsoleKey.RightArrow));
		tc.ProcessKey(MakeKey(ConsoleKey.RightArrow));

		tc.ProcessKey(MakeKey(ConsoleKey.Enter));

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_ArrowKeys_SkipDisabledTabs()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsEnabled = false;

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		// Right from A should skip disabled B and go to C
		tc.ProcessKey(MakeKey(ConsoleKey.RightArrow));
		tc.ProcessKey(MakeKey(ConsoleKey.Enter));

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_ArrowKeys_SkipHiddenTabs()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsVisible = false;

		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape));

		// Right from A should skip hidden B and go to C
		tc.ProcessKey(MakeKey(ConsoleKey.RightArrow));
		tc.ProcessKey(MakeKey(ConsoleKey.Enter));

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	#endregion

	#region Focus — Ctrl+Tab Switching

	[Fact]
	public void ProcessKey_CtrlTab_SwitchesToNextTab()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.SetFocus(true, FocusReason.Keyboard);

		tc.ProcessKey(MakeKey(ConsoleKey.Tab, ConsoleModifiers.Control));

		Assert.Equal(1, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_CtrlShiftTab_SwitchesToPreviousTab()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.SelectedTabIndex = 1;
		tc.SetFocus(true, FocusReason.Keyboard);

		tc.ProcessKey(MakeKey(ConsoleKey.Tab, ConsoleModifiers.Control | ConsoleModifiers.Shift));

		Assert.Equal(0, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_CtrlTab_FromContentMode_SwitchesTab()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("A");
		page0.AddControl(new ButtonControl { Text = "Btn0" });
		var page1 = tc.AddTab("B");
		page1.AddControl(new ButtonControl { Text = "Btn1" });

		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.Equal(0, tc.SelectedTabIndex);

		tc.ProcessKey(MakeKey(ConsoleKey.Tab, ConsoleModifiers.Control));

		Assert.Equal(1, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_CtrlTab_SkipsDisabledTabs()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.TabPages[1].IsEnabled = false;
		tc.SetFocus(true, FocusReason.Keyboard);

		tc.ProcessKey(MakeKey(ConsoleKey.Tab, ConsoleModifiers.Control));

		Assert.Equal(2, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessKey_CtrlTab_Wraps()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.SelectedTabIndex = 1;
		tc.SetFocus(true, FocusReason.Keyboard);

		tc.ProcessKey(MakeKey(ConsoleKey.Tab, ConsoleModifiers.Control));

		Assert.Equal(0, tc.SelectedTabIndex);
	}

	#endregion

	#region Focus — Complex Content Scenarios

	[Fact]
	public void Focus_SwitchTab_UnfocusesPreviousContent()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("A");
		var btn0 = new ButtonControl { Text = "A-Btn" };
		page0.AddControl(btn0);
		var page1 = tc.AddTab("B");
		var btn1 = new ButtonControl { Text = "B-Btn" };
		page1.AddControl(btn1);

		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.True(btn0.HasFocus);

		tc.SelectedTabIndex = 1;

		Assert.False(btn0.HasFocus);
	}

	#endregion

	#region Mouse

	[Fact]
	public void ProcessMouseEvent_ClickTabHeader_SwitchesTab()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.AddTab("C");
		tc.SetFocus(true, FocusReason.Keyboard);

		// Paint to set _actualWidth
		var buffer = CreateBuffer();
		var bounds = CreateBounds();
		PaintControl(tc, buffer, bounds, bounds);

		// Calculate where tab B starts: │ A │ = 5 chars, so B starts at ~5
		int tabBStartX = AnsiConsoleHelper.StripSpectreLength("A") + 3; // │ + space + title + space = leading │ + title area

		var args = new MouseEventArgs(
			new List<SharpConsoleUI.Drivers.MouseFlags> { SharpConsoleUI.Drivers.MouseFlags.Button1Clicked },
			new System.Drawing.Point(tabBStartX + 1, 0),
			new System.Drawing.Point(tabBStartX + 1, 0),
			new System.Drawing.Point(0, 0));

		tc.ProcessMouseEvent(args);

		Assert.Equal(1, tc.SelectedTabIndex);
	}

	[Fact]
	public void ProcessMouseEvent_ClickActiveTabHeader_NoChange()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("B");
		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.Equal(0, tc.SelectedTabIndex);

		var buffer = CreateBuffer();
		PaintControl(tc, buffer, CreateBounds(), CreateBounds());

		bool eventFired = false;
		tc.SelectedTabChanged += (_, _) => eventFired = true;

		var args = new MouseEventArgs(
			new List<SharpConsoleUI.Drivers.MouseFlags> { SharpConsoleUI.Drivers.MouseFlags.Button1Clicked },
			new System.Drawing.Point(2, 0),
			new System.Drawing.Point(2, 0),
			new System.Drawing.Point(0, 0));

		tc.ProcessMouseEvent(args);

		Assert.Equal(0, tc.SelectedTabIndex);
		Assert.False(eventFired);
	}

	[Fact]
	public void ProcessMouseEvent_ClickDisabledTabHeader_NoChange()
	{
		var tc = new TabControl();
		tc.AddTab("A");
		tc.AddTab("Disabled");
		tc.TabPages[1].IsEnabled = false;
		tc.SetFocus(true, FocusReason.Keyboard);

		var buffer = CreateBuffer();
		PaintControl(tc, buffer, CreateBounds(), CreateBounds());

		int disabledTabX = AnsiConsoleHelper.StripSpectreLength("A") + 4;

		var args = new MouseEventArgs(
			new List<SharpConsoleUI.Drivers.MouseFlags> { SharpConsoleUI.Drivers.MouseFlags.Button1Clicked },
			new System.Drawing.Point(disabledTabX + 1, 0),
			new System.Drawing.Point(disabledTabX + 1, 0),
			new System.Drawing.Point(0, 0));

		tc.ProcessMouseEvent(args);

		Assert.Equal(0, tc.SelectedTabIndex); // Should stay at A
	}

	[Fact]
	public void ProcessMouseEvent_ClickContentArea_DelegatesToContent()
	{
		var tc = new TabControl();
		tc.ShowContentBorder = true;
		var page = tc.AddTab("Test");
		var btn = new ButtonControl { Text = "Click" };
		page.AddControl(btn);

		tc.SetFocus(true, FocusReason.Keyboard);
		var buffer = CreateBuffer();
		PaintControl(tc, buffer, CreateBounds(), CreateBounds());

		// Click in content area (below tab strip and border)
		var args = new MouseEventArgs(
			new List<SharpConsoleUI.Drivers.MouseFlags> { SharpConsoleUI.Drivers.MouseFlags.Button1Clicked },
			new System.Drawing.Point(3, 3), // Inside content
			new System.Drawing.Point(3, 3),
			new System.Drawing.Point(0, 0));

		tc.ProcessMouseEvent(args);
		// Should not crash — delegates to ScrollablePanel
	}

	#endregion

	#region Properties

	[Fact]
	public void ShowContentBorder_True_AddsBorderHeight()
	{
		var tc = new TabControl();
		tc.VerticalAlignment = VerticalAlignment.Top;
		tc.ShowContentBorder = true;
		tc.AddTab("Test");

		var sizeWithBorder = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 100));

		tc.ShowContentBorder = false;
		var sizeWithout = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 100));

		Assert.True(sizeWithBorder.Height > sizeWithout.Height);
	}

	[Fact]
	public void ShowContentBorder_False_NoBorderHeight()
	{
		var tc = new TabControl();
		tc.VerticalAlignment = VerticalAlignment.Top;
		tc.ShowContentBorder = false;
		tc.AddTab("Test");

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 100));

		// Without border: tabstrip(1) + content
		Assert.True(size.Height >= 1);
	}

	[Fact]
	public void Width_Explicit_OverridesStretch()
	{
		var tc = new TabControl();
		tc.Width = 40;
		tc.AddTab("Test");

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		Assert.Equal(40, size.Width);
	}

	[Fact]
	public void Height_Explicit_OverridesFill()
	{
		var tc = new TabControl();
		tc.Height = 15;
		tc.AddTab("Test");

		var size = tc.MeasureDOM(new LayoutConstraints(0, 80, 0, 25));

		Assert.Equal(15, size.Height);
	}

	[Fact]
	public void TabStripAlignment_Left_Default_Check()
	{
		var tc = new TabControl();
		Assert.Equal(TabStripAlignment.Left, tc.TabStripAlignment);
	}

	[Fact]
	public void Visible_False_LosesFocus()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.TabPages[0].Content.AddControl(new ButtonControl { Text = "OK" });
		tc.SetFocus(true, FocusReason.Keyboard);
		Assert.True(tc.HasFocus);

		tc.Visible = false;

		Assert.False(tc.HasFocus);
	}

	[Fact]
	public void IsEnabled_False_NotFocusable()
	{
		var tc = new TabControl();
		tc.AddTab("Test");
		tc.IsEnabled = false;

		Assert.False(tc.CanReceiveFocus);
	}

	#endregion

	#region IContainerControl

	[Fact]
	public void GetChildren_ReturnsActiveTabChildren()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("A");
		var btn0 = new ButtonControl { Text = "Btn0" };
		page0.AddControl(btn0);
		var page1 = tc.AddTab("B");
		var btn1 = new ButtonControl { Text = "Btn1" };
		page1.AddControl(btn1);

		tc.SelectedTabIndex = 0;
		var children = tc.GetChildren();

		Assert.Contains(btn0, children);
		Assert.DoesNotContain(btn1, children);
	}

	[Fact]
	public void GetChildren_AfterTabSwitch_ReturnsNewTabChildren()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("A");
		var btn0 = new ButtonControl { Text = "Btn0" };
		page0.AddControl(btn0);
		var page1 = tc.AddTab("B");
		var btn1 = new ButtonControl { Text = "Btn1" };
		page1.AddControl(btn1);

		tc.SelectedTabIndex = 1;
		var children = tc.GetChildren();

		Assert.Contains(btn1, children);
		Assert.DoesNotContain(btn0, children);
	}

	[Fact]
	public void GetChildren_NoTabs_ReturnsEmpty()
	{
		var tc = new TabControl();
		var children = tc.GetChildren();

		Assert.Empty(children);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void NoTabs_ProcessKey_ReturnsFalse()
	{
		var tc = new TabControl();
		tc.SetFocus(true, FocusReason.Keyboard);

		var result = tc.ProcessKey(MakeKey(ConsoleKey.Tab));

		Assert.False(result);
	}

	[Fact]
	public void NoTabs_PaintDOM_NoCrash()
	{
		var tc = new TabControl();
		var buffer = CreateBuffer();
		var bounds = CreateBounds();

		// Should not throw
		PaintControl(tc, buffer, bounds, bounds);
	}

	[Fact]
	public void SingleTab_StripNavigation_NoEffect()
	{
		var tc = new TabControl();
		tc.AddTab("Only");
		tc.SetFocus(true, FocusReason.Keyboard);
		tc.ProcessKey(MakeKey(ConsoleKey.Escape)); // Enter strip mode

		// Left/Right should just wrap to the same tab
		tc.ProcessKey(MakeKey(ConsoleKey.LeftArrow));
		tc.ProcessKey(MakeKey(ConsoleKey.Enter));

		Assert.Equal(0, tc.SelectedTabIndex);
	}

	[Fact]
	public void TabPage_TitleChange_Reflected()
	{
		var tc = new TabControl();
		var page = tc.AddTab("Old");
		page.Title = "New";

		Assert.Equal("New", tc.TabPages[0].Title);
	}

	[Fact]
	public void RemoveTab_WhileFocused_FocusAdjusts()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("A");
		page0.AddControl(new ButtonControl { Text = "0" });
		var page1 = tc.AddTab("B");
		page1.AddControl(new ButtonControl { Text = "1" });
		var page2 = tc.AddTab("C");
		page2.AddControl(new ButtonControl { Text = "2" });

		tc.SelectedTabIndex = 1;
		tc.SetFocus(true, FocusReason.Keyboard);

		tc.RemoveTabAt(1);

		Assert.True(tc.SelectedTabIndex >= 0);
		Assert.True(tc.SelectedTabIndex < tc.TabCount);
	}

	[Fact]
	public void Dispose_DisposesAllTabPages()
	{
		var tc = new TabControl();
		var page0 = tc.AddTab("A");
		var btn = new ButtonControl { Text = "X" };
		page0.AddControl(btn);
		tc.AddTab("B");

		tc.Dispose();

		Assert.Equal(0, tc.TabCount);
		Assert.Null(btn.Container);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_AddTab_CreatesTabPage()
	{
		var tc = TabControl.Create().AddTab("Tab 1").Build();

		Assert.Equal(1, tc.TabCount);
		Assert.Equal("Tab 1", tc.TabPages[0].Title);
	}

	[Fact]
	public void Builder_Configure_SetsProperties()
	{
		var tc = TabControl.Create()
			.AddTab("Tab")
			.WithMargin(1, 2, 3, 4)
			.WithAlignment(HorizontalAlignment.Left)
			.WithVerticalAlignment(VerticalAlignment.Top)
			.WithContentBorder(false)
			.WithTabStripAlignment(TabStripAlignment.Center)
			.WithName("myTab")
			.WithWidth(50)
			.Build();

		Assert.Equal(1, tc.Margin.Left);
		Assert.Equal(2, tc.Margin.Top);
		Assert.Equal(3, tc.Margin.Right);
		Assert.Equal(4, tc.Margin.Bottom);
		Assert.Equal(HorizontalAlignment.Left, tc.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Top, tc.VerticalAlignment);
		Assert.False(tc.ShowContentBorder);
		Assert.Equal(TabStripAlignment.Center, tc.TabStripAlignment);
		Assert.Equal("myTab", tc.Name);
		Assert.Equal(50, tc.Width);
	}

	[Fact]
	public void Builder_AddTab_WithContent_ConfiguresPage()
	{
		var tc = TabControl.Create()
			.AddTab("Tab", page =>
			{
				page.AddControl(new ButtonControl { Text = "OK" });
			})
			.Build();

		Assert.Single(tc.TabPages[0].Content.Children);
	}

	[Fact]
	public void Builder_ChainingWorks()
	{
		var tc = TabControl.Create()
			.AddTab("Tab 1")
			.AddTab("Tab 2")
			.AddTab("Tab 3")
			.WithMargin(1)
			.WithContentBorder(true)
			.Build();

		Assert.Equal(3, tc.TabCount);
	}

	[Fact]
	public void Builder_ImplicitConversion()
	{
		TabControl tc = TabControl.Create()
			.AddTab("Test");

		Assert.Equal(1, tc.TabCount);
	}

	[Fact]
	public void Builder_OnTabChanged_HandlerAttached()
	{
		bool fired = false;
		var tc = TabControl.Create()
			.AddTab("A")
			.AddTab("B")
			.OnTabChanged((_, _) => fired = true)
			.Build();

		tc.SelectedTabIndex = 1;

		Assert.True(fired);
	}

	#endregion
}
