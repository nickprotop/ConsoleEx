// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Extensive behavioral tests for <see cref="NavigationView"/> — keyboard navigation,
/// collapsible headers, sub-items, separators, events, the content toolbar, focus routing,
/// and item visibility. Complements <see cref="NavigationViewTests"/> (API/config) and
/// <see cref="NavigationViewResponsiveTests"/> (display modes).
///
/// Tests named <c>Bug_*</c> document confirmed defects and assert the DESIRED behavior, so
/// they fail until the bug is fixed. See the XML doc on each for the root cause.
/// </summary>
public class NavigationViewBehaviorTests
{
	#region Helpers

	/// <summary>
	/// Builds a NavigationView inside a rendered window and focuses the nav pane so that
	/// <see cref="NavigationView.ProcessKey"/> routes to the nav-pane key handler.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, NavigationView nav) RenderedNav(
		NavigationView nav, int width = 100, int height = 30)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system)
		{
			Title = "Test", Left = 0, Top = 0, Width = width, Height = height
		};
		nav.VerticalAlignment = VerticalAlignment.Fill;
		window.AddControl(nav);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		return (system, window, nav);
	}

	/// <summary>Focuses the nav pane so nav-pane keys are processed.</summary>
	private static void FocusNav(Window window, NavigationView nav)
	{
		window.FocusManager.SetFocus(nav.NavScrollPanel, FocusReason.Keyboard);
	}

	private static ConsoleKeyInfo Key(ConsoleKey key, ConsoleModifiers mods = 0) =>
		new ConsoleKeyInfo('\0', key,
			mods.HasFlag(ConsoleModifiers.Shift),
			mods.HasFlag(ConsoleModifiers.Alt),
			mods.HasFlag(ConsoleModifiers.Control));

	/// <summary>
	/// Mimics the window dispatcher's key routing for a nav-pane key: the focused control
	/// (the nav <see cref="ScrollablePanelControl"/>) gets the key first; only if it does NOT
	/// consume it does the key bubble up to the <see cref="NavigationView"/>. This reproduces
	/// the real end-to-end path that bug 2 depends on (the panel consuming arrows to scroll).
	/// </summary>
	private static void DispatchNavKey(NavigationView nav, ConsoleKeyInfo key)
	{
		if (!nav.NavScrollPanel.ProcessKey(key))
			nav.ProcessKey(key);
	}

	#endregion

	#region Keyboard Navigation — Items

	[Fact]
	public void DownArrow_MovesSelectionToNextItem()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		nav.AddItem("C");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		Assert.Equal(0, nav.SelectedIndex); // first item auto-selected
		nav.ProcessKey(Key(ConsoleKey.DownArrow));
		Assert.Equal(1, nav.SelectedIndex);
		nav.ProcessKey(Key(ConsoleKey.DownArrow));
		Assert.Equal(2, nav.SelectedIndex);
	}

	[Fact]
	public void UpArrow_MovesSelectionToPreviousItem()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.SelectedIndex = 1;
		nav.ProcessKey(Key(ConsoleKey.UpArrow));
		Assert.Equal(0, nav.SelectedIndex);
	}

	[Fact]
	public void DownArrow_AtLastItem_DoesNotWrapOrChange()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.SelectedIndex = 1;
		var handled = nav.ProcessKey(Key(ConsoleKey.DownArrow));
		Assert.Equal(1, nav.SelectedIndex);
		Assert.False(handled);
	}

	[Fact]
	public void UpArrow_AtFirstItem_DoesNotChange()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		Assert.Equal(0, nav.SelectedIndex);
		nav.ProcessKey(Key(ConsoleKey.UpArrow));
		Assert.Equal(0, nav.SelectedIndex);
	}

	[Fact]
	public void Home_SelectsFirstEnabledItem()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		nav.AddItem("C");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.SelectedIndex = 2;
		nav.ProcessKey(Key(ConsoleKey.Home));
		Assert.Equal(0, nav.SelectedIndex);
	}

	[Fact]
	public void End_SelectsLastEnabledItem()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		nav.AddItem("C");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.ProcessKey(Key(ConsoleKey.End));
		Assert.Equal(2, nav.SelectedIndex);
	}

	[Fact]
	public void DownArrow_SkipsDisabledItems()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		var disabled = nav.AddItem("B");
		disabled.IsEnabled = false;
		nav.AddItem("C");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.ProcessKey(Key(ConsoleKey.DownArrow));
		Assert.Equal(2, nav.SelectedIndex); // skipped disabled index 1
	}

	[Fact]
	public void Enter_OnItem_FiresItemInvoked()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		NavigationItemChangedEventArgs? invoked = null;
		nav.ItemInvoked += (_, e) => invoked = e;

		nav.SelectedIndex = 1;
		nav.ProcessKey(Key(ConsoleKey.Enter));

		Assert.NotNull(invoked);
		Assert.Equal(1, invoked!.NewIndex);
		Assert.Equal("B", invoked.NewItem?.Text);
	}

	[Fact]
	public void Spacebar_OnItem_FiresItemInvoked()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		int invokedCount = 0;
		nav.ItemInvoked += (_, _) => invokedCount++;

		nav.ProcessKey(Key(ConsoleKey.Spacebar));
		Assert.Equal(1, invokedCount);
	}

	#endregion

	#region Collapsible Headers — mouse

	[Fact]
	public void Header_StartsExpanded()
	{
		var header = NavigationItem.CreateHeader("Group");
		Assert.True(header.IsExpanded);
		Assert.Equal(NavigationItemType.Header, header.ItemType);
	}

	[Fact]
	public void ToggleHeaderExpanded_HidesAndShowsChildren()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		var (_, _, _) = RenderedNav(nav);

		Assert.True(header.IsExpanded);

		nav.ToggleHeaderExpanded(header);
		Assert.False(header.IsExpanded);

		nav.ToggleHeaderExpanded(header);
		Assert.True(header.IsExpanded);
	}

	[Fact]
	public void CollapsingHeader_MovesSelectionOffHiddenChild()
	{
		var nav = new NavigationView();
		nav.AddItem("Top");
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		var (_, _, _) = RenderedNav(nav);

		// Select the child, then collapse — selection must leave the now-hidden child.
		int childIndex = nav.Items.ToList().FindIndex(i => i == child);
		nav.SelectedIndex = childIndex;
		Assert.Equal(child, nav.SelectedItem);

		nav.ToggleHeaderExpanded(header);
		Assert.NotEqual(child, nav.SelectedItem);
	}

	[Fact]
	public void AddItemToHeader_WhenCollapsed_NewChildHidden()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var (_, _, _) = RenderedNav(nav);

		nav.ToggleHeaderExpanded(header); // collapse
		var child = nav.AddItemToHeader(header, "Child");

		int idx = nav.Items.ToList().FindIndex(i => i == child);
		Assert.False(nav.NavScrollPanel.Children[idx].Visible);
	}

	[Fact]
	public void AddItemToHeader_NonHeaderParent_Throws()
	{
		var nav = new NavigationView();
		var item = nav.AddItem("Plain");
		Assert.Throws<ArgumentException>(() => nav.AddItemToHeader(item, "Child"));
	}

	#endregion

	#region Collapsible Headers — keyboard reachability (bug 1 fix)

	/// <summary>
	/// Regression for bug 1: a collapsed header must be reachable via arrow keys so it can be
	/// re-expanded from the keyboard. Headers are navigable selection stops (even though they
	/// carry <c>IsEnabled = false</c>); MoveSelection lands on them.
	/// </summary>
	[Fact]
	public void CollapsedHeader_IsReachableByKeyboard_SoItCanBeReExpanded()
	{
		var nav = new NavigationView();
		nav.AddItem("Top");
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child1");
		nav.AddItemToHeader(header, "Child2");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		// Collapse the header (as a mouse click would).
		nav.ToggleHeaderExpanded(header);
		Assert.False(header.IsExpanded);

		// From the top item, arrow down repeatedly trying to reach the header.
		nav.SelectedIndex = 0;
		bool landedOnHeader = false;
		for (int i = 0; i < nav.Items.Count + 2; i++)
		{
			if (nav.SelectedItem == header) { landedOnHeader = true; break; }
			nav.ProcessKey(Key(ConsoleKey.DownArrow));
		}

		Assert.True(landedOnHeader,
			"a collapsed header should be reachable by arrow keys so it can be re-expanded; " +
			"currently MoveSelection skips all Header items, leaving it keyboard-unreachable");
	}

	/// <summary>
	/// A header can become the selected index even though it carries <c>IsEnabled = false</c>
	/// (that flag only marks it as a non-content target). This is what makes a header reachable
	/// for keyboard expand/collapse.
	/// </summary>
	[Fact]
	public void Header_CanBecomeSelectedIndex_SoItIsKeyboardReachable()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child");
		var (_, _, _) = RenderedNav(nav);

		int headerIndex = nav.Items.ToList().FindIndex(i => i == header);
		nav.SelectedIndex = headerIndex;

		Assert.Equal(header, nav.SelectedItem);
	}

	/// <summary>
	/// Regression for bug 1 (keyboard-side): a collapsed header can be re-expanded purely from
	/// the keyboard — arrow to the header, press Enter, and it expands.
	/// </summary>
	[Fact]
	public void Enter_CanToggleHeaderFromKeyboard()
	{
		var nav = new NavigationView();
		nav.AddItem("Top");
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.ToggleHeaderExpanded(header); // collapse it
		Assert.False(header.IsExpanded);

		// Try to reach the header by keyboard and press Enter to re-expand it.
		nav.SelectedIndex = 0;
		for (int i = 0; i < nav.Items.Count + 2; i++)
		{
			nav.ProcessKey(Key(ConsoleKey.Enter)); // would toggle IF header were current
			if (header.IsExpanded) break;
			nav.ProcessKey(Key(ConsoleKey.DownArrow));
		}

		Assert.True(header.IsExpanded,
			"a collapsed header should be re-expandable from the keyboard; currently it cannot be, " +
			"because headers are neither selectable (IsEnabled=false) nor skipped-to by MoveSelection");
	}

	/// <summary>Mouse click on a header toggles its expansion (the working path).</summary>
	[Fact]
	public void ToggleHeaderExpanded_DirectCall_TogglesExpansion()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child");
		var (_, _, _) = RenderedNav(nav);

		Assert.True(header.IsExpanded);
		nav.ToggleHeaderExpanded(header);
		Assert.False(header.IsExpanded);
		nav.ToggleHeaderExpanded(header);
		Assert.True(header.IsExpanded);
	}

	// --- Tree-style Left/Right semantics (Tab owns panel-switching, not arrows) ---

	[Fact]
	public void RightArrow_OnCollapsedHeader_Expands()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		int headerIndex = nav.Items.ToList().FindIndex(i => i == header);
		nav.SelectedIndex = headerIndex;
		nav.ToggleHeaderExpanded(header); // collapse
		Assert.False(header.IsExpanded);

		nav.ProcessKey(Key(ConsoleKey.RightArrow));
		Assert.True(header.IsExpanded);
		Assert.Equal(header, nav.SelectedItem); // stays on the header
	}

	[Fact]
	public void RightArrow_OnExpandedHeader_MovesSelectionToFirstChild()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		int headerIndex = nav.Items.ToList().FindIndex(i => i == header);
		nav.SelectedIndex = headerIndex;
		Assert.True(header.IsExpanded);

		nav.ProcessKey(Key(ConsoleKey.RightArrow));
		Assert.Equal(child, nav.SelectedItem);
	}

	[Fact]
	public void RightArrow_OnPlainItem_DoesNothing_AndDoesNotSwitchPanels()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.SelectedIndex = 0;
		bool handled = nav.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.False(handled); // not consumed — no panel switch
		Assert.True(nav.NavScrollPanel.HasFocus); // focus stayed on the nav pane
		Assert.False(nav.ContentPanel.HasFocus);
	}

	[Fact]
	public void LeftArrow_OnExpandedHeader_Collapses()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		int headerIndex = nav.Items.ToList().FindIndex(i => i == header);
		nav.SelectedIndex = headerIndex;
		Assert.True(header.IsExpanded);

		nav.ProcessKey(Key(ConsoleKey.LeftArrow));
		Assert.False(header.IsExpanded);
		Assert.Equal(header, nav.SelectedItem); // stays on the header
	}

	[Fact]
	public void LeftArrow_OnSubItem_MovesSelectionToParentHeader()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		int childIndex = nav.Items.ToList().FindIndex(i => i == child);
		nav.SelectedIndex = childIndex;

		nav.ProcessKey(Key(ConsoleKey.LeftArrow));
		Assert.Equal(header, nav.SelectedItem); // ascended to parent (still expanded)
		Assert.True(header.IsExpanded);
	}

	[Fact]
	public void LeftArrow_OnTopLevelItem_DoesNothing()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.SelectedIndex = 0;
		bool handled = nav.ProcessKey(Key(ConsoleKey.LeftArrow));
		Assert.False(handled);
		Assert.Equal(0, nav.SelectedIndex);
	}

	/// <summary>
	/// Selecting a header is allowed (for keyboard expand/collapse) but a header is not a content
	/// page: selecting it must not switch the content panel. Headers have no content factory, so
	/// the content panel's children remain whatever the last real item populated.
	/// </summary>
	[Fact]
	public void SelectingHeader_DoesNotSwitchContent()
	{
		var nav = new NavigationView();
		var item = nav.AddItem("Page");
		nav.SetItemContent(item, panel => panel.AddControl(new MarkupControl(new List<string> { "page-content" })));
		var header = nav.AddHeader("Group");
		nav.AddItemToHeader(header, "Child");
		var (_, _, _) = RenderedNav(nav);

		nav.SelectedIndex = 0; // the content item
		int contentChildrenBefore = nav.ContentPanel.Children.Count;

		int headerIndex = nav.Items.ToList().FindIndex(i => i == header);
		nav.SelectedIndex = headerIndex; // selecting the header

		Assert.Equal(header, nav.SelectedItem);
		Assert.Equal(contentChildrenBefore, nav.ContentPanel.Children.Count);
	}

	#endregion

	#region Nav pane scrolling vs. item navigation (bug 2 fix)

	/// <summary>
	/// Regression for bug 2: when the nav items overflow the pane height, Up/Down arrows must
	/// move the SELECTED item (not scroll the viewport independently). The nav
	/// ScrollablePanelControl is configured with <c>ArrowKeyScrolling = false</c> so the arrow
	/// falls through to NavigationView.ProcessNavPaneKey → MoveSelection; the panel still
	/// auto-scrolls to keep the selection visible (verified separately).
	/// </summary>
	[Fact]
	public void OverflowingNavPane_ArrowMovesSelection_NotJustScroll()
	{
		var nav = new NavigationView();
		// Many items, short window → nav pane overflows and needs scrolling.
		for (int i = 0; i < 40; i++)
			nav.AddItem($"Item {i}");
		var (_, window, _) = RenderedNav(nav, width: 100, height: 10);
		FocusNav(window, nav);

		Assert.True(nav.NavScrollPanel.TotalContentHeight > nav.NavScrollPanel.ViewportHeight,
			"precondition: the nav pane must actually overflow (need scrolling) for this repro");

		int before = nav.SelectedIndex;
		DispatchNavKey(nav, Key(ConsoleKey.DownArrow));

		Assert.Equal(before + 1, nav.SelectedIndex);
	}

	/// <summary>
	/// Arrow navigation must keep the selected item in view: moving the selection down past the
	/// bottom of the viewport scrolls the nav pane so the selection stays visible (focus follows
	/// selection). This is the "scroll must still happen" requirement — driven by selection, not
	/// by the arrow key scrolling the viewport independently.
	/// </summary>
	[Fact]
	public void OverflowingNavPane_ArrowDown_ScrollsToKeepSelectionVisible()
	{
		var nav = new NavigationView();
		for (int i = 0; i < 40; i++)
			nav.AddItem($"Item {i}");
		var (system, window, _) = RenderedNav(nav, width: 100, height: 10);
		FocusNav(window, nav);

		int viewport = nav.NavScrollPanel.ViewportHeight;
		Assert.True(viewport > 0 && nav.NavScrollPanel.TotalContentHeight > viewport,
			"precondition: nav pane overflows");

		int startOffset = nav.NavScrollPanel.VerticalScrollOffset;

		// Move the selection well past the bottom of the viewport.
		for (int i = 0; i < viewport + 3; i++)
		{
			DispatchNavKey(nav, Key(ConsoleKey.DownArrow));
			system.Render.UpdateDisplay(); // process any deferred scroll-into-view
		}

		Assert.True(nav.SelectedIndex >= viewport,
			$"selection should have advanced past the viewport (selected={nav.SelectedIndex}, viewport={viewport})");
		Assert.True(nav.NavScrollPanel.VerticalScrollOffset > startOffset,
			"the nav pane should have scrolled to keep the selected item visible, but " +
			$"offset stayed at {nav.NavScrollPanel.VerticalScrollOffset}");
	}

	/// <summary>
	/// Control case: when items do NOT overflow, arrow-down moves selection (works today).
	/// This proves the bug is specific to the overflow/scrolling state. Uses the same
	/// deliver-then-bubble dispatch as the bug repro above.
	/// </summary>
	[Fact]
	public void NonOverflowingNavPane_ArrowMovesSelection()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, window, _) = RenderedNav(nav, width: 100, height: 30);
		FocusNav(window, nav);

		Assert.False(nav.NavScrollPanel.TotalContentHeight > nav.NavScrollPanel.ViewportHeight,
			"precondition: the nav pane should NOT need scrolling in this case");

		int before = nav.SelectedIndex;
		DispatchNavKey(nav, Key(ConsoleKey.DownArrow));
		Assert.Equal(before + 1, nav.SelectedIndex);
	}

	#endregion

	#region Events

	[Fact]
	public void SelectedItemChanged_FiresWithCorrectArgs()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, _, _) = RenderedNav(nav);

		NavigationItemChangedEventArgs? changed = null;
		nav.SelectedItemChanged += (_, e) => changed = e;

		nav.SelectedIndex = 1;

		Assert.NotNull(changed);
		Assert.Equal(0, changed!.OldIndex);
		Assert.Equal(1, changed.NewIndex);
		Assert.Equal("A", changed.OldItem?.Text);
		Assert.Equal("B", changed.NewItem?.Text);
	}

	[Fact]
	public void SelectedItemChanging_CancelPreventsChange()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, _, _) = RenderedNav(nav);

		nav.SelectedItemChanging += (_, e) => e.Cancel = true;
		nav.SelectedIndex = 1;

		Assert.Equal(0, nav.SelectedIndex);
	}

	[Fact]
	public void ItemInvoked_NotFired_OnSelectionChange()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		nav.AddItem("B");
		var (_, _, _) = RenderedNav(nav);

		int invoked = 0;
		nav.ItemInvoked += (_, _) => invoked++;

		nav.SelectedIndex = 1; // selection change is not an invoke
		Assert.Equal(0, invoked);
	}

	#endregion

	#region Content Toolbar

	[Fact]
	public void AddContentToolbarButton_MakesToolbarVisible()
	{
		var nav = new NavigationView();
		Assert.False(nav.ContentToolbar.Visible);

		nav.AddContentToolbarButton("Save");
		Assert.True(nav.ContentToolbar.Visible);
	}

	[Fact]
	public void AddContentToolbarButton_ReturnsButtonWithText_AndAddsToToolbar()
	{
		var nav = new NavigationView();
		var button = nav.AddContentToolbarButton("Save", (_, _) => { });

		Assert.NotNull(button);
		Assert.Equal("Save", button.Text);
		Assert.Contains(button, nav.ContentToolbar.Items);
	}

	[Fact]
	public void ClearContentToolbar_HidesToolbar()
	{
		var nav = new NavigationView();
		nav.AddContentToolbarButton("Save");
		Assert.True(nav.ContentToolbar.Visible);

		nav.ClearContentToolbar();
		Assert.False(nav.ContentToolbar.Visible);
	}

	[Fact]
	public void RemoveContentToolbarItem_RemovesAndHidesWhenEmpty()
	{
		var nav = new NavigationView();
		var button = nav.AddContentToolbarButton("Save");
		nav.RemoveContentToolbarItem(button);
		Assert.False(nav.ContentToolbar.Visible);
	}

	[Fact]
	public void AddContentToolbarSeparator_AddsItem()
	{
		var nav = new NavigationView();
		nav.AddContentToolbarButton("A");
		nav.AddContentToolbarSeparator();
		nav.AddContentToolbarButton("B");
		Assert.True(nav.ContentToolbar.Visible);
	}

	#endregion

	#region Focus routing

	[Fact]
	public void ClickOnNavSide_FocusesNavPane()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		var (_, window, _) = RenderedNav(nav);

		var pos = new System.Drawing.Point(1, 3);
		var args = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked },
			pos, pos, pos);
		nav.ProcessMouseEvent(args);

		Assert.True(nav.NavScrollPanel.HasFocus);
	}

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse()
	{
		var nav = new NavigationView();
		nav.AddItem("A");
		var (_, window, _) = RenderedNav(nav);
		FocusNav(window, nav);

		nav.IsEnabled = false;
		Assert.False(nav.ProcessKey(Key(ConsoleKey.DownArrow)));
	}

	#endregion

	#region Item visibility

	[Fact]
	public void CollapsedHeader_ChildControlsNotVisible()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var c1 = nav.AddItemToHeader(header, "C1");
		var c2 = nav.AddItemToHeader(header, "C2");
		var (_, _, _) = RenderedNav(nav);

		nav.ToggleHeaderExpanded(header);

		var items = nav.Items.ToList();
		int i1 = items.FindIndex(i => i == c1);
		int i2 = items.FindIndex(i => i == c2);
		Assert.False(nav.NavScrollPanel.Children[i1].Visible);
		Assert.False(nav.NavScrollPanel.Children[i2].Visible);
	}

	[Fact]
	public void ExpandedHeader_ChildControlsVisible()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		var (_, _, _) = RenderedNav(nav);

		// expanded by default
		int idx = nav.Items.ToList().FindIndex(i => i == child);
		Assert.True(nav.NavScrollPanel.Children[idx].Visible);
	}

	#endregion

	#region Headers & separators

	[Fact]
	public void AddHeader_CreatesHeaderTypeItem()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		Assert.Equal(NavigationItemType.Header, header.ItemType);
		Assert.Contains(header, nav.Items);
	}

	[Fact]
	public void AddHeader_WithColor_StoresHeaderColor()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group", Color.Red);
		Assert.Equal(Color.Red, header.HeaderColor);
	}

	[Fact]
	public void SubItem_HasParentHeaderSet()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		Assert.Equal(header, child.ParentHeader);
	}

	[Fact]
	public void AddItemToHeader_InsertsAfterHeader()
	{
		var nav = new NavigationView();
		nav.AddItem("Top");
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");

		var items = nav.Items.ToList();
		int headerIdx = items.FindIndex(i => i == header);
		int childIdx = items.FindIndex(i => i == child);
		Assert.Equal(headerIdx + 1, childIdx);
	}

	#endregion
}
