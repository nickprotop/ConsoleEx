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
using SharpConsoleUI.Events;
using Xunit;
using static SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Comprehensive test suite for NavigationView control.
/// </summary>
public class NavigationViewTests
{
	#region Defaults & Configuration

	[Fact]
	public void Constructor_CreatesWithDefaults()
	{
		var nav = new NavigationView();

		Assert.NotNull(nav);
		Assert.Equal(26, nav.NavPaneWidth);
		Assert.Equal(-1, nav.SelectedIndex);
		Assert.Null(nav.SelectedItem);
		Assert.Empty(nav.Items);
		Assert.True(nav.ShowContentHeader);
		Assert.Equal(BorderStyle.Rounded, nav.ContentBorderStyle);
	}

	[Fact]
	public void NavPaneWidth_CanBeSet()
	{
		var nav = new NavigationView();
		nav.NavPaneWidth = 30;
		Assert.Equal(30, nav.NavPaneWidth);
	}

	[Fact]
	public void NavPaneWidth_EnforcesMinimum()
	{
		var nav = new NavigationView();
		nav.NavPaneWidth = 5;
		Assert.Equal(10, nav.NavPaneWidth);
	}

	[Fact]
	public void ContentBorderStyle_CanBeSet()
	{
		var nav = new NavigationView();
		nav.ContentBorderStyle = BorderStyle.Single;
		Assert.Equal(BorderStyle.Single, nav.ContentBorderStyle);
	}

	[Fact]
	public void ContentBorderColor_CanBeSet()
	{
		var nav = new NavigationView();
		nav.ContentBorderColor = Color.Cyan1;
		Assert.Equal(Color.Cyan1, nav.ContentBorderColor);
	}

	[Fact]
	public void PaneHeader_CanBeSet()
	{
		var nav = new NavigationView();
		nav.PaneHeader = "[bold]Header[/]";
		Assert.Equal("[bold]Header[/]", nav.PaneHeader);
	}

	[Fact]
	public void ShowContentHeader_CanBeToggled()
	{
		var nav = new NavigationView();
		Assert.True(nav.ShowContentHeader);
		nav.ShowContentHeader = false;
		Assert.False(nav.ShowContentHeader);
	}

	[Fact]
	public void SelectionIndicator_CanBeChanged()
	{
		var nav = new NavigationView();
		nav.SelectionIndicator = '>';
		Assert.Equal('>', nav.SelectionIndicator);
	}

	#endregion

	#region Item Management

	[Fact]
	public void AddItem_NavigationItem_AddsToList()
	{
		var nav = new NavigationView();
		var item = new NavigationItem("Home");
		nav.AddItem(item);

		Assert.Single(nav.Items);
		Assert.Equal("Home", nav.Items[0].Text);
	}

	[Fact]
	public void AddItem_String_CreatesAndReturnsItem()
	{
		var nav = new NavigationView();
		var item = nav.AddItem("Settings", "⚙", "Configure settings");

		Assert.NotNull(item);
		Assert.Equal("Settings", item.Text);
		Assert.Equal("⚙", item.Icon);
		Assert.Equal("Configure settings", item.Subtitle);
		Assert.Single(nav.Items);
	}

	[Fact]
	public void AddItem_FirstItem_AutoSelects()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");

		Assert.Equal(0, nav.SelectedIndex);
		Assert.Equal("Home", nav.SelectedItem!.Text);
	}

	[Fact]
	public void AddItem_SecondItem_DoesNotChangeSelection()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		Assert.Equal(0, nav.SelectedIndex);
		Assert.Equal("Home", nav.SelectedItem!.Text);
	}

	[Fact]
	public void RemoveItem_ByIndex_RemovesCorrectly()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");
		nav.AddItem("About");

		nav.RemoveItem(1);

		Assert.Equal(2, nav.Items.Count);
		Assert.Equal("Home", nav.Items[0].Text);
		Assert.Equal("About", nav.Items[1].Text);
	}

	[Fact]
	public void RemoveItem_ByItem_RemovesCorrectly()
	{
		var nav = new NavigationView();
		var item = nav.AddItem("Home");
		nav.AddItem("Settings");

		nav.RemoveItem(item);

		Assert.Single(nav.Items);
		Assert.Equal("Settings", nav.Items[0].Text);
	}

	[Fact]
	public void ClearItems_RemovesAll()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");
		nav.AddItem("About");

		nav.ClearItems();

		Assert.Empty(nav.Items);
		Assert.Equal(-1, nav.SelectedIndex);
		Assert.Null(nav.SelectedItem);
	}

	[Fact]
	public void Items_ReturnsReadOnlySnapshot()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		var items = nav.Items;
		Assert.Equal(2, items.Count);
		Assert.IsAssignableFrom<IReadOnlyList<NavigationItem>>(items);
	}

	#endregion

	#region Selection

	[Fact]
	public void SelectedIndex_SetValidIndex_Changes()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		nav.SelectedIndex = 1;

		Assert.Equal(1, nav.SelectedIndex);
		Assert.Equal("Settings", nav.SelectedItem!.Text);
	}

	[Fact]
	public void SelectedIndex_InvalidIndex_NoChange()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");

		nav.SelectedIndex = 5;

		Assert.Equal(0, nav.SelectedIndex);
	}

	[Fact]
	public void SelectedIndex_NegativeIndex_NoChange()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");

		nav.SelectedIndex = -2;

		Assert.Equal(0, nav.SelectedIndex);
	}

	[Fact]
	public void SelectedIndex_SameIndex_NoOp()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		int changeCount = 0;
		nav.SelectedItemChanged += (_, _) => changeCount++;

		nav.SelectedIndex = 0; // Already 0
		Assert.Equal(0, changeCount);
	}

	[Fact]
	public void SelectedIndex_FiresChangedEvent()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		NavigationItemChangedEventArgs? args = null;
		nav.SelectedItemChanged += (_, e) => args = e;

		nav.SelectedIndex = 1;

		Assert.NotNull(args);
		Assert.Equal(0, args!.OldIndex);
		Assert.Equal(1, args.NewIndex);
		Assert.Equal("Home", args.OldItem!.Text);
		Assert.Equal("Settings", args.NewItem!.Text);
	}

	[Fact]
	public void SelectedIndex_ChangingCanCancel()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		nav.SelectedItemChanging += (_, e) => e.Cancel = true;
		nav.SelectedIndex = 1;

		Assert.Equal(0, nav.SelectedIndex); // Still Home
	}

	[Fact]
	public void SelectedIndex_ChangingEventHasCorrectArgs()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		NavigationItemChangingEventArgs? args = null;
		nav.SelectedItemChanging += (_, e) => args = e;

		nav.SelectedIndex = 1;

		Assert.NotNull(args);
		Assert.Equal(0, args!.OldIndex);
		Assert.Equal(1, args.NewIndex);
		Assert.Equal("Home", args.OldItem!.Text);
		Assert.Equal("Settings", args.NewItem!.Text);
	}

	[Fact]
	public void SelectedIndex_DisabledItem_CannotSelect()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		var item = nav.AddItem("Settings");
		item.IsEnabled = false;

		nav.SelectedIndex = 1;

		Assert.Equal(0, nav.SelectedIndex); // Still Home
	}

	#endregion

	#region Content

	[Fact]
	public void SetItemContent_FactoryInvokedOnSelection()
	{
		var nav = new NavigationView();
		var item1 = nav.AddItem("Home");
		var item2 = nav.AddItem("Settings");

		bool factoryInvoked = false;
		nav.SetItemContent(item2, panel =>
		{
			factoryInvoked = true;
		});

		nav.SelectedIndex = 1;
		Assert.True(factoryInvoked);
	}

	[Fact]
	public void SetItemContent_ByIndex_Works()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");

		bool factoryInvoked = false;
		nav.SetItemContent(1, panel =>
		{
			factoryInvoked = true;
		});

		nav.SelectedIndex = 1;
		Assert.True(factoryInvoked);
	}

	[Fact]
	public void SetItemContent_ForCurrentItem_AppliesImmediately()
	{
		var nav = new NavigationView();
		var item = nav.AddItem("Home");

		bool factoryInvoked = false;
		nav.SetItemContent(item, panel =>
		{
			factoryInvoked = true;
		});

		Assert.True(factoryInvoked);
	}

	[Fact]
	public void ContentPanel_IsAccessible()
	{
		var nav = new NavigationView();
		Assert.NotNull(nav.ContentPanel);
		Assert.IsType<ScrollablePanelControl>(nav.ContentPanel);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_ProducesCorrectDefaults()
	{
		NavigationView nav = NavigationView().Build();

		Assert.NotNull(nav);
		Assert.Equal(26, nav.NavPaneWidth);
		Assert.Empty(nav.Items);
	}

	[Fact]
	public void Builder_AddItemWithContent()
	{
		bool contentCalled = false;
		NavigationView nav = NavigationView()
			.AddItem("Home", content: panel => { contentCalled = true; })
			.Build();

		Assert.Single(nav.Items);
		Assert.Equal("Home", nav.Items[0].Text);
		Assert.True(contentCalled); // Auto-selected first item triggers content
	}

	[Fact]
	public void Builder_WithNavWidth_Applied()
	{
		NavigationView nav = NavigationView()
			.WithNavWidth(40)
			.Build();

		Assert.Equal(40, nav.NavPaneWidth);
	}

	[Fact]
	public void Builder_WithContentBorder_Applied()
	{
		NavigationView nav = NavigationView()
			.WithContentBorder(BorderStyle.Single)
			.Build();

		Assert.Equal(BorderStyle.Single, nav.ContentBorderStyle);
	}

	[Fact]
	public void Builder_WithPaneHeader_Applied()
	{
		NavigationView nav = NavigationView()
			.WithPaneHeader("[bold]Nav[/]")
			.Build();

		Assert.Equal("[bold]Nav[/]", nav.PaneHeader);
	}

	[Fact]
	public void Builder_ImplicitConversion()
	{
		NavigationView nav = NavigationView()
			.WithNavWidth(30)
			.AddItem("Home");

		Assert.IsType<NavigationView>(nav);
		Assert.Equal(30, nav.NavPaneWidth);
	}

	[Fact]
	public void Builder_WithSelectedIndex()
	{
		NavigationView nav = NavigationView()
			.AddItem("Home")
			.AddItem("Settings")
			.WithSelectedIndex(1)
			.Build();

		Assert.Equal(1, nav.SelectedIndex);
		Assert.Equal("Settings", nav.SelectedItem!.Text);
	}

	[Fact]
	public void Builder_WithAlignment_Applied()
	{
		NavigationView nav = NavigationView()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		Assert.Equal(HorizontalAlignment.Stretch, nav.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Fill, nav.VerticalAlignment);
	}

	[Fact]
	public void Builder_OnSelectedItemChanged_Fires()
	{
		bool eventFired = false;
		NavigationView nav = NavigationView()
			.AddItem("Home")
			.AddItem("Settings")
			.OnSelectedItemChanged((_, _) => eventFired = true)
			.Build();

		nav.SelectedIndex = 1;
		Assert.True(eventFired);
	}

	[Fact]
	public void Builder_WithContentPadding_Applied()
	{
		NavigationView nav = NavigationView()
			.WithContentPadding(2, 1, 2, 1)
			.Build();

		Assert.Equal(new Padding(2, 1, 2, 1), nav.ContentPadding);
	}

	[Fact]
	public void Builder_WithContentHeader_Applied()
	{
		NavigationView nav = NavigationView()
			.WithContentHeader(false)
			.Build();

		Assert.False(nav.ShowContentHeader);
	}

	#endregion

	#region NavigationItem

	[Fact]
	public void NavigationItem_Constructor_SetsProperties()
	{
		var item = new NavigationItem("Home", "🏠", "Welcome page");

		Assert.Equal("Home", item.Text);
		Assert.Equal("🏠", item.Icon);
		Assert.Equal("Welcome page", item.Subtitle);
		Assert.True(item.IsEnabled);
		Assert.Null(item.Tag);
	}

	[Fact]
	public void NavigationItem_ImplicitFromString()
	{
		NavigationItem item = "Home";

		Assert.Equal("Home", item.Text);
		Assert.Null(item.Icon);
		Assert.Null(item.Subtitle);
	}

	[Fact]
	public void NavigationItem_Tag_CanBeSet()
	{
		var item = new NavigationItem("Home") { Tag = 42 };
		Assert.Equal(42, item.Tag);
	}

	#endregion

	#region Backward Compatibility & Edge Cases

	[Fact]
	public void Control_WorksWithZeroItems()
	{
		var nav = new NavigationView();
		Assert.Equal(-1, nav.SelectedIndex);
		Assert.Null(nav.SelectedItem);

		// Should not throw
		nav.SelectedIndex = 0;
		Assert.Equal(-1, nav.SelectedIndex);
	}

	[Fact]
	public void Control_GetChildren_ReturnsGrid()
	{
		var nav = new NavigationView();
		var children = nav.GetChildren();

		Assert.Single(children);
		Assert.IsType<HorizontalGridControl>(children[0]);
	}

	[Fact]
	public void Control_IContainer_Properties()
	{
		var nav = new NavigationView();

		nav.BackgroundColor = Color.Blue;
		Assert.Equal(Color.Blue, nav.BackgroundColor);

		nav.ForegroundColor = Color.Red;
		Assert.Equal(Color.Red, nav.ForegroundColor);
	}

	[Fact]
	public void RemoveItem_SelectedItem_AdjustsSelection()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");
		nav.AddItem("About");

		nav.SelectedIndex = 1;
		nav.RemoveItem(1); // Remove selected

		// Should select the next valid item
		Assert.True(nav.SelectedIndex >= 0 && nav.SelectedIndex < nav.Items.Count);
	}

	[Fact]
	public void RemoveItem_BeforeSelected_AdjustsIndex()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");
		nav.AddItem("Settings");
		nav.AddItem("About");

		nav.SelectedIndex = 2;
		nav.RemoveItem(0);

		Assert.Equal(1, nav.SelectedIndex);
		Assert.Equal("About", nav.SelectedItem!.Text);
	}

	[Fact]
	public void RemoveItem_InvalidIndex_NoEffect()
	{
		var nav = new NavigationView();
		nav.AddItem("Home");

		nav.RemoveItem(-1);
		nav.RemoveItem(5);

		Assert.Single(nav.Items);
	}

	#endregion
}
