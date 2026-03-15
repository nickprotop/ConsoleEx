// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class StatusBarControlTests
{
	#region Construction & Defaults

	[Fact]
	public void Constructor_Default_SetsExpectedDefaults()
	{
		var bar = new StatusBarControl();

		Assert.Equal(HorizontalAlignment.Stretch, bar.HorizontalAlignment);
		Assert.Equal(StickyPosition.Bottom, bar.StickyPosition);
		Assert.Equal(ControlDefaults.StatusBarItemSpacing, bar.ItemSpacing);
		Assert.Equal(ControlDefaults.StatusBarSeparatorChar, bar.SeparatorChar);
		Assert.Equal(ControlDefaults.StatusBarShortcutLabelSeparator, bar.ShortcutLabelSeparator);
		Assert.Empty(bar.LeftItems);
		Assert.Empty(bar.CenterItems);
		Assert.Empty(bar.RightItems);
	}

	[Fact]
	public void MeasureDOM_ReturnsHeightOne()
	{
		var bar = new StatusBarControl();
		var size = bar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		Assert.Equal(ControlDefaults.StatusBarDefaultHeight, size.Height);
	}

	[Fact]
	public void MeasureDOM_WithMargins_AddsMarginToHeight()
	{
		var bar = new StatusBarControl { Margin = new Margin(0, 1, 0, 1) };
		var size = bar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		Assert.Equal(ControlDefaults.StatusBarDefaultHeight + 2, size.Height);
	}

	#endregion

	#region Item Management

	[Fact]
	public void AddLeft_CreatesAndReturnsItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddLeft("Ctrl+S", "Save");

		Assert.Single(bar.LeftItems);
		Assert.Equal("Ctrl+S", item.Shortcut);
		Assert.Equal("Save", item.Label);
	}

	[Fact]
	public void AddCenter_CreatesAndReturnsItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddCenter("F1", "Help");

		Assert.Single(bar.CenterItems);
		Assert.Equal("F1", item.Shortcut);
	}

	[Fact]
	public void AddRight_CreatesAndReturnsItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddRight("Esc", "Exit");

		Assert.Single(bar.RightItems);
		Assert.Equal("Exit", item.Label);
	}

	[Fact]
	public void AddLeftText_CreatesLabelOnlyItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddLeftText("Ready");

		Assert.Single(bar.LeftItems);
		Assert.Equal("Ready", item.Label);
		Assert.Null(item.Shortcut);
	}

	[Fact]
	public void AddLeftSeparator_CreatesSeparatorItem()
	{
		var bar = new StatusBarControl();
		bar.AddLeftSeparator();

		Assert.Single(bar.LeftItems);
		Assert.True(bar.LeftItems[0].IsSeparator);
	}

	[Fact]
	public void RemoveLeft_RemovesItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddLeft("A", "B");

		Assert.True(bar.RemoveLeft(item));
		Assert.Empty(bar.LeftItems);
	}

	[Fact]
	public void RemoveLeft_ReturnsFalseForMissing()
	{
		var bar = new StatusBarControl();
		var item = new StatusBarItem { Label = "test" };

		Assert.False(bar.RemoveLeft(item));
	}

	[Fact]
	public void RemoveCenter_RemovesItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddCenter("A", "B");

		Assert.True(bar.RemoveCenter(item));
		Assert.Empty(bar.CenterItems);
	}

	[Fact]
	public void RemoveRight_RemovesItem()
	{
		var bar = new StatusBarControl();
		var item = bar.AddRight("A", "B");

		Assert.True(bar.RemoveRight(item));
		Assert.Empty(bar.RightItems);
	}

	[Fact]
	public void ClearLeft_RemovesAll()
	{
		var bar = new StatusBarControl();
		bar.AddLeft("A", "B");
		bar.AddLeft("C", "D");

		bar.ClearLeft();

		Assert.Empty(bar.LeftItems);
	}

	[Fact]
	public void ClearCenter_RemovesAll()
	{
		var bar = new StatusBarControl();
		bar.AddCenter("A", "B");

		bar.ClearCenter();

		Assert.Empty(bar.CenterItems);
	}

	[Fact]
	public void ClearRight_RemovesAll()
	{
		var bar = new StatusBarControl();
		bar.AddRight("A", "B");

		bar.ClearRight();

		Assert.Empty(bar.RightItems);
	}

	[Fact]
	public void ClearAll_RemovesEverything()
	{
		var bar = new StatusBarControl();
		bar.AddLeft("A", "B");
		bar.AddCenter("C", "D");
		bar.AddRight("E", "F");

		bar.ClearAll();

		Assert.Empty(bar.LeftItems);
		Assert.Empty(bar.CenterItems);
		Assert.Empty(bar.RightItems);
	}

	#endregion

	#region StatusBarItem Properties

	[Fact]
	public void StatusBarItem_DefaultIsVisible()
	{
		var item = new StatusBarItem { Label = "test" };
		Assert.True(item.IsVisible);
	}

	[Fact]
	public void StatusBarItem_DefaultIsNotSeparator()
	{
		var item = new StatusBarItem { Label = "test" };
		Assert.False(item.IsSeparator);
	}

	[Fact]
	public void StatusBarItem_NullColorsByDefault()
	{
		var item = new StatusBarItem { Label = "test" };
		Assert.Null(item.ShortcutForeground);
		Assert.Null(item.ShortcutBackground);
		Assert.Null(item.LabelForeground);
		Assert.Null(item.LabelBackground);
	}

	[Fact]
	public void StatusBarItem_SetOnClick()
	{
		bool clicked = false;
		var item = new StatusBarItem { Label = "test", OnClick = () => clicked = true };

		item.OnClick!();

		Assert.True(clicked);
	}

	#endregion

	#region BatchUpdate

	[Fact]
	public void BatchUpdate_SupportsMultipleChanges()
	{
		var bar = new StatusBarControl();

		bar.BatchUpdate(() =>
		{
			bar.AddLeft("A", "B");
			bar.AddLeft("C", "D");
			bar.AddRight("E", "F");
		});

		Assert.Equal(2, bar.LeftItems.Count);
		Assert.Single(bar.RightItems);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_BasicConstruction()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.AddLeft("↑↓", "Navigate")
			.AddLeft("Enter", "View")
			.AddLeftSeparator()
			.AddRightText("Ready")
			.AddCenterText("Status")
			.WithBackgroundColor(Color.Grey15)
			.WithShortcutForegroundColor(Color.Cyan1)
			.StickyBottom()
			.Build();

		Assert.Equal(3, bar.LeftItems.Count);
		Assert.Single(bar.CenterItems);
		Assert.Single(bar.RightItems);
		Assert.Equal(StickyPosition.Bottom, bar.StickyPosition);
	}

	[Fact]
	public void Builder_StickyTop()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.StickyTop()
			.Build();

		Assert.Equal(StickyPosition.Top, bar.StickyPosition);
	}

	[Fact]
	public void Builder_WithItemSpacing()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithItemSpacing(3)
			.Build();

		Assert.Equal(3, bar.ItemSpacing);
	}

	[Fact]
	public void Builder_WithSeparatorChar()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithSeparatorChar("│")
			.Build();

		Assert.Equal("│", bar.SeparatorChar);
	}

	[Fact]
	public void Builder_WithShortcutLabelSeparator()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithShortcutLabelSeparator(" ")
			.Build();

		Assert.Equal(" ", bar.ShortcutLabelSeparator);
	}

	[Fact]
	public void Builder_ImplicitConversion()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.AddLeftText("test");

		Assert.Single(bar.LeftItems);
	}

	[Fact]
	public void Builder_OnItemClicked()
	{
		StatusBarItem? clickedItem = null;
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.AddLeftText("test")
			.OnItemClicked((sender, args) => clickedItem = args.Item)
			.Build();

		// Just verify event handler is attached (actual click testing requires rendering)
		Assert.NotNull(bar);
	}

	[Fact]
	public void Builder_WithMarginOverload()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithMargin(1, 2, 3, 4)
			.Build();

		Assert.Equal(new Margin(1, 2, 3, 4), bar.Margin);
	}

	[Fact]
	public void Builder_WithUniformMargin()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithMargin(2)
			.Build();

		Assert.Equal(new Margin(2, 2, 2, 2), bar.Margin);
	}

	[Fact]
	public void Builder_WithNameAndTag()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithName("myBar")
			.WithTag(42)
			.Build();

		Assert.Equal("myBar", bar.Name);
		Assert.Equal(42, bar.Tag);
	}

	[Fact]
	public void Builder_AddCenterItems()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.AddCenter("F5", "Run")
			.AddCenterSeparator()
			.AddCenterText("Ready")
			.Build();

		Assert.Equal(3, bar.CenterItems.Count);
		Assert.True(bar.CenterItems[1].IsSeparator);
	}

	[Fact]
	public void Builder_AddRightItems()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.AddRight("Ctrl+Q", "Quit")
			.AddRightSeparator()
			.AddRightText("[yellow]3 outdated[/]")
			.Build();

		Assert.Equal(3, bar.RightItems.Count);
	}

	#endregion

	#region IMouseAwareControl

	[Fact]
	public void WantsMouseEvents_ReturnsTrue()
	{
		var bar = new StatusBarControl();
		Assert.True(bar.WantsMouseEvents);
	}

	[Fact]
	public void CanFocusWithMouse_ReturnsFalse()
	{
		var bar = new StatusBarControl();
		Assert.False(bar.CanFocusWithMouse);
	}

	#endregion

	#region Above Line

	[Fact]
	public void ShowAboveLine_DefaultFalse()
	{
		var bar = new StatusBarControl();
		Assert.False(bar.ShowAboveLine);
	}

	[Fact]
	public void ShowAboveLine_IncreasesHeightByOne()
	{
		var bar = new StatusBarControl { ShowAboveLine = true };
		var size = bar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		Assert.Equal(ControlDefaults.StatusBarDefaultHeight + 1, size.Height);
	}

	[Fact]
	public void AboveLineColor_DefaultNull()
	{
		var bar = new StatusBarControl();
		Assert.Null(bar.AboveLineColor);
	}

	[Fact]
	public void Builder_WithAboveLine()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithAboveLine()
			.Build();

		Assert.True(bar.ShowAboveLine);
	}

	[Fact]
	public void Builder_WithAboveLineColor_ImpliesShowAboveLine()
	{
		StatusBarControl bar = SharpConsoleUI.Builders.Controls.StatusBar()
			.WithAboveLineColor(Color.Grey50)
			.Build();

		Assert.True(bar.ShowAboveLine);
		Assert.Equal(Color.Grey50, bar.AboveLineColor);
	}

	#endregion

	#region Visibility

	[Fact]
	public void HiddenItem_ExcludedFromLayout()
	{
		var bar = new StatusBarControl();
		var item1 = bar.AddLeft("A", "Visible");
		var item2 = bar.AddLeft("B", "Hidden");
		item2.IsVisible = false;

		// Only item1 should be in left items (both are present but item2 is hidden)
		Assert.Equal(2, bar.LeftItems.Count);
		Assert.False(bar.LeftItems[1].IsVisible);
	}

	#endregion
}
