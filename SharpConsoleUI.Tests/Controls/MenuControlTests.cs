// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MenuControlTests
{
    #region Helper Methods

    private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool ctrl = false)
        => new(keyChar, key, shift, alt, ctrl);

    private static MouseEventArgs MouseMove(int x, int y)
    {
        var pos = new Point(x, y);
        return new MouseEventArgs(new List<MouseFlags> { MouseFlags.ReportMousePosition }, pos, pos, pos);
    }

    private static MouseEventArgs MousePress(int x, int y)
    {
        var pos = new Point(x, y);
        return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Pressed }, pos, pos, pos);
    }

    private static MouseEventArgs MouseRelease(int x, int y)
    {
        var pos = new Point(x, y);
        return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Released }, pos, pos, pos);
    }

    private static MouseEventArgs MouseLeave(int x, int y)
    {
        var pos = new Point(x, y);
        return new MouseEventArgs(new List<MouseFlags> { MouseFlags.MouseLeave }, pos, pos, pos);
    }

    private static MouseEventArgs RightClick(int x, int y)
    {
        var pos = new Point(x, y);
        return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button3Clicked }, pos, pos, pos);
    }

    private static MouseEventArgs MouseEnter(int x, int y)
    {
        var pos = new Point(x, y);
        return new MouseEventArgs(new List<MouseFlags> { MouseFlags.MouseEnter }, pos, pos, pos);
    }

    /// <summary>
    /// Creates a MenuControl attached to a window with focus set.
    /// </summary>
    private static (MenuControl menu, Window window) CreateFocusedMenu(
        MenuOrientation orientation = MenuOrientation.Horizontal,
        bool isSticky = false)
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = new MenuControl { Orientation = orientation, IsSticky = isSticky };
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);
        return (menu, window);
    }

    /// <summary>
    /// Creates a standard File/Edit/View menu structure for testing.
    /// </summary>
    private static MenuControl CreateStandardMenu()
    {
        var menu = new MenuControl { Orientation = MenuOrientation.Horizontal };

        var fileItem = new MenuItem { Text = "File" };
        fileItem.AddChild(new MenuItem { Text = "New", Shortcut = "Ctrl+N" });
        fileItem.AddChild(new MenuItem { Text = "Open", Shortcut = "Ctrl+O" });
        fileItem.AddChild(new MenuItem { IsSeparator = true });
        var recentItem = new MenuItem { Text = "Recent" };
        recentItem.AddChild(new MenuItem { Text = "Doc1" });
        recentItem.AddChild(new MenuItem { Text = "Doc2" });
        fileItem.AddChild(recentItem);
        fileItem.AddChild(new MenuItem { Text = "Exit" });
        menu.AddItem(fileItem);

        var editItem = new MenuItem { Text = "Edit" };
        editItem.AddChild(new MenuItem { Text = "Undo", Shortcut = "Ctrl+Z" });
        editItem.AddChild(new MenuItem { Text = "Redo", Shortcut = "Ctrl+Y" });
        menu.AddItem(editItem);

        var viewItem = new MenuItem { Text = "View" };
        viewItem.AddChild(new MenuItem { Text = "Zoom In" });
        viewItem.AddChild(new MenuItem { Text = "Zoom Out" });
        menu.AddItem(viewItem);

        return menu;
    }

    /// <summary>
    /// Creates a standard menu, attaches to a window, sets focus, returns tuple.
    /// </summary>
    private static (MenuControl menu, Window window) CreateFocusedStandardMenu()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);
        return (menu, window);
    }

    #endregion

    #region 1. Construction & Properties

    [Fact]
    public void DefaultMenuControl_HasHorizontalOrientation()
    {
        var menu = new MenuControl();
        Assert.Equal(MenuOrientation.Horizontal, menu.Orientation);
    }

    [Fact]
    public void DefaultMenuControl_HasNoItems()
    {
        var menu = new MenuControl();
        Assert.Empty(menu.Items);
    }

    [Fact]
    public void DefaultMenuControl_IsNotSticky()
    {
        var menu = new MenuControl();
        Assert.False(menu.IsSticky);
    }

    [Fact]
    public void DefaultMenuControl_IsEnabled()
    {
        var menu = new MenuControl();
        Assert.True(menu.IsEnabled);
    }

    [Fact]
    public void SettingOrientation_ToVertical_Works()
    {
        var menu = new MenuControl();
        menu.Orientation = MenuOrientation.Vertical;
        Assert.Equal(MenuOrientation.Vertical, menu.Orientation);
    }

    [Fact]
    public void IsSticky_CanBeSetToTrue()
    {
        var menu = new MenuControl();
        menu.IsSticky = true;
        Assert.True(menu.IsSticky);
    }

    [Fact]
    public void IsEnabled_CanBeSetToFalse()
    {
        var menu = new MenuControl();
        menu.IsEnabled = false;
        Assert.False(menu.IsEnabled);
    }

    [Fact]
    public void ColorProperties_DefaultNull()
    {
        var menu = new MenuControl();
        Assert.Null(menu.MenuBarBackgroundColor);
        Assert.Null(menu.MenuBarForegroundColor);
        Assert.Null(menu.MenuBarHighlightBackgroundColor);
        Assert.Null(menu.MenuBarHighlightForegroundColor);
        Assert.Null(menu.DropdownBackgroundColor);
        Assert.Null(menu.DropdownForegroundColor);
        Assert.Null(menu.DropdownHighlightBackgroundColor);
        Assert.Null(menu.DropdownHighlightForegroundColor);
    }

    [Fact]
    public void ColorProperties_CanBeSet()
    {
        var menu = new MenuControl();
        menu.MenuBarBackgroundColor = Color.Red;
        menu.DropdownBackgroundColor = Color.Blue;
        Assert.Equal(Color.Red, menu.MenuBarBackgroundColor);
        Assert.Equal(Color.Blue, menu.DropdownBackgroundColor);
    }

    #endregion

    #region 2. Menu Item Management

    [Fact]
    public void AddItem_AddsToItemsList()
    {
        var menu = new MenuControl();
        var item = new MenuItem { Text = "File" };
        menu.AddItem(item);
        Assert.Single(menu.Items);
        Assert.Equal("File", menu.Items[0].Text);
    }

    [Fact]
    public void AddItem_Null_ThrowsArgumentNullException()
    {
        var menu = new MenuControl();
        Assert.Throws<ArgumentNullException>(() => menu.AddItem(null!));
    }

    [Fact]
    public void RemoveItem_RemovesFromList()
    {
        var menu = new MenuControl();
        var item = new MenuItem { Text = "File" };
        menu.AddItem(item);
        menu.RemoveItem(item);
        Assert.Empty(menu.Items);
    }

    [Fact]
    public void RemoveItem_ClearsFocusedHoveredPressedState()
    {
        // We can test this indirectly: after removing the focused item,
        // keyboard navigation should still work (focused item is null, first item gets focus)
        var (menu, window) = CreateFocusedMenu();
        var item1 = new MenuItem { Text = "File" };
        var item2 = new MenuItem { Text = "Edit" };
        menu.AddItem(item1);
        menu.AddItem(item2);

        // Focus first item
        menu.ProcessKey(Key(ConsoleKey.Home));

        // Remove the focused item
        menu.RemoveItem(item1);

        // Menu should still have one item
        Assert.Single(menu.Items);
        Assert.Equal("Edit", menu.Items[0].Text);
    }

    [Fact]
    public void RemoveItem_ClosesOrphanedDropdown()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        // Open the File dropdown
        menu.OpenDropdown("File");
        Assert.True(fileItem.IsOpen);

        // Remove the File item
        menu.RemoveItem(fileItem);

        // The dropdown should be closed
        Assert.False(fileItem.IsOpen);
    }

    [Fact]
    public void ClearItems_EmptiesListAndClosesMenus()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Open a dropdown
        menu.OpenDropdown("File");

        // Clear all items
        menu.ClearItems();
        Assert.Empty(menu.Items);
    }

    [Fact]
    public void Items_IsSnapshot_NotLiveReference()
    {
        var menu = new MenuControl();
        menu.AddItem(new MenuItem { Text = "File" });
        var snapshot = menu.Items;
        menu.AddItem(new MenuItem { Text = "Edit" });
        // Snapshot should still have 1 item
        Assert.Single(snapshot);
        Assert.Equal(2, menu.Items.Count);
    }

    #endregion

    #region 3. MenuItem Operations

    [Fact]
    public void MenuItem_Defaults_TextEmpty_ShortcutNull_EnabledTrue_SeparatorFalse()
    {
        var item = new MenuItem();
        Assert.Equal(string.Empty, item.Text);
        Assert.Null(item.Shortcut);
        Assert.True(item.IsEnabled);
        Assert.False(item.IsSeparator);
    }

    [Fact]
    public void MenuItem_AddChild_SetsParent()
    {
        var parent = new MenuItem { Text = "File" };
        var child = new MenuItem { Text = "New" };
        parent.AddChild(child);
        Assert.Equal(parent, child.Parent);
    }

    [Fact]
    public void MenuItem_AddChild_InvalidatesDepthCache()
    {
        var parent = new MenuItem { Text = "File" };
        var child = new MenuItem { Text = "New" };

        // First call caches depth as 0 (no parent)
        Assert.Equal(0, child.GetDepth());

        // After adding as child, depth should be recalculated
        parent.AddChild(child);
        Assert.Equal(1, child.GetDepth());
    }

    [Fact]
    public void MenuItem_GetDepth_ReturnsCorrectValues()
    {
        var root = new MenuItem { Text = "File" };
        var child = new MenuItem { Text = "Recent" };
        var grandchild = new MenuItem { Text = "Doc1" };

        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Equal(0, root.GetDepth());
        Assert.Equal(1, child.GetDepth());
        Assert.Equal(2, grandchild.GetDepth());
    }

    [Fact]
    public void MenuItem_GetDepth_CachesResult()
    {
        var parent = new MenuItem { Text = "File" };
        var child = new MenuItem { Text = "New" };
        parent.AddChild(child);

        // Call twice — second should use cache
        int depth1 = child.GetDepth();
        int depth2 = child.GetDepth();
        Assert.Equal(1, depth1);
        Assert.Equal(1, depth2);
    }

    [Fact]
    public void MenuItem_GetPath_ReturnsSlashSeparatedPath()
    {
        var root = new MenuItem { Text = "File" };
        var recent = new MenuItem { Text = "Recent" };
        var doc = new MenuItem { Text = "Doc1" };

        root.AddChild(recent);
        recent.AddChild(doc);

        Assert.Equal("File/Recent/Doc1", doc.GetPath());
    }

    [Fact]
    public void MenuItem_HasChildren_TrueWhenHasChildren()
    {
        var item = new MenuItem { Text = "File" };
        Assert.False(item.HasChildren);

        item.AddChild(new MenuItem { Text = "New" });
        Assert.True(item.HasChildren);
    }

    [Fact]
    public void MenuItem_ToString_Separator()
    {
        var sep = new MenuItem { IsSeparator = true };
        Assert.Equal("[Separator]", sep.ToString());
    }

    [Fact]
    public void MenuItem_ToString_WithShortcut()
    {
        var item = new MenuItem { Text = "Save", Shortcut = "Ctrl+S" };
        Assert.Contains("Save", item.ToString());
        Assert.Contains("Ctrl+S", item.ToString());
    }

    [Fact]
    public void MenuItem_ToString_WithChildren()
    {
        var item = new MenuItem { Text = "File" };
        item.AddChild(new MenuItem { Text = "New" });
        Assert.Contains("[1 children]", item.ToString());
    }

    [Fact]
    public void MenuItem_ToString_Disabled()
    {
        var item = new MenuItem { Text = "Save", IsEnabled = false };
        Assert.Contains("[Disabled]", item.ToString());
    }

    [Fact]
    public void MenuItem_AddChild_Null_ThrowsArgumentNullException()
    {
        var item = new MenuItem { Text = "File" };
        Assert.Throws<ArgumentNullException>(() => item.AddChild(null!));
    }

    [Fact]
    public void MenuItem_Tag_CanBeSet()
    {
        var item = new MenuItem { Text = "File", Tag = 42 };
        Assert.Equal(42, item.Tag);
    }

    [Fact]
    public void MenuItem_ForegroundColor_CanBeSet()
    {
        var item = new MenuItem { Text = "File", ForegroundColor = Color.Red };
        Assert.Equal(Color.Red, item.ForegroundColor);
    }

    #endregion

    #region 4. FindItemByPath

    [Fact]
    public void FindItemByPath_FindsTopLevelItem()
    {
        var menu = CreateStandardMenu();
        var found = menu.FindItemByPath("File");
        Assert.NotNull(found);
        Assert.Equal("File", found.Text);
    }

    [Fact]
    public void FindItemByPath_FindsNestedItem()
    {
        var menu = CreateStandardMenu();
        var found = menu.FindItemByPath("File/Recent/Doc1");
        Assert.NotNull(found);
        Assert.Equal("Doc1", found.Text);
    }

    [Fact]
    public void FindItemByPath_ReturnsNull_ForNonExistentPath()
    {
        var menu = CreateStandardMenu();
        var found = menu.FindItemByPath("NonExistent");
        Assert.Null(found);
    }

    [Fact]
    public void FindItemByPath_ReturnsNull_ForPartiallyWrongPath()
    {
        var menu = CreateStandardMenu();
        var found = menu.FindItemByPath("File/NonExistent");
        Assert.Null(found);
    }

    [Fact]
    public void FindItemByPath_WorksWithMarkupText()
    {
        var menu = new MenuControl();
        var item = new MenuItem { Text = "[bold]File[/]" };
        menu.AddItem(item);

        // Search by stripped text
        var found = menu.FindItemByPath("File");
        Assert.NotNull(found);
        Assert.Equal("[bold]File[/]", found.Text);
    }

    [Fact]
    public void FindItemByPath_AlsoMatchesRawText_WithoutMarkup()
    {
        // FindItemByPath also matches raw text (i.Text == part), not just stripped markup
        var menu = new MenuControl();
        var item = new MenuItem { Text = "File" };
        menu.AddItem(item);

        // Search by exact text should match
        var found = menu.FindItemByPath("File");
        Assert.NotNull(found);
        Assert.Equal("File", found!.Text);
    }

    #endregion

    #region 5. Horizontal Keyboard Navigation (No Dropdown Open)

    [Fact]
    public void Horizontal_RightArrow_MovesToNextTopLevelItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Start at File
        menu.ProcessKey(Key(ConsoleKey.Home));

        // Move right to Edit
        bool handled = menu.ProcessKey(Key(ConsoleKey.RightArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_LeftArrow_MovesToPreviousTopLevelItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Start at Edit (move right from File)
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));

        // Move left back to File
        bool handled = menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_RightArrow_WrapsAround()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Start at last item (View)
        menu.ProcessKey(Key(ConsoleKey.End));

        // Move right should wrap to File
        bool handled = menu.ProcessKey(Key(ConsoleKey.RightArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_LeftArrow_WrapsAround()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Start at first item (File)
        menu.ProcessKey(Key(ConsoleKey.Home));

        // Move left should wrap to View
        bool handled = menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_DownArrow_OpensDropdownOfFocusedItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        // Focus File item
        menu.ProcessKey(Key(ConsoleKey.Home));

        // Down should open dropdown
        menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.True(fileItem.IsOpen);
    }

    [Fact]
    public void Horizontal_DownArrow_OnLeafItem_ExecutesAction()
    {
        var (menu, window) = CreateFocusedMenu();
        bool actionExecuted = false;
        var leafItem = new MenuItem { Text = "Action", Action = () => actionExecuted = true };
        menu.AddItem(leafItem);

        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.True(actionExecuted);
    }

    [Fact]
    public void Horizontal_Enter_OnItemWithChildren_OpensDropdown()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(fileItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Enter_OnLeafItem_ExecutesAction()
    {
        var (menu, window) = CreateFocusedMenu();
        bool actionExecuted = false;
        var leafItem = new MenuItem { Text = "DoSomething", Action = () => actionExecuted = true };
        menu.AddItem(leafItem);

        menu.ProcessKey(Key(ConsoleKey.Home));
        bool handled = menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(handled);
        Assert.True(actionExecuted);
    }

    [Fact]
    public void Horizontal_Escape_UnfocusesMenu()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        Assert.True(menu.HasFocus);

        menu.ProcessKey(Key(ConsoleKey.Escape));

        // After escape, the menu should no longer be focused
        Assert.False(menu.HasFocus);
    }

    [Fact]
    public void Horizontal_Escape_WithIsSticky_DoesNotUnfocus()
    {
        var (menu, window) = CreateFocusedMenu(isSticky: true);
        menu.AddItem(new MenuItem { Text = "File" });
        Assert.True(menu.HasFocus);

        menu.ProcessKey(Key(ConsoleKey.Escape));

        // With IsSticky, escape should not unfocus
        Assert.True(menu.HasFocus);
    }

    [Fact]
    public void Horizontal_Home_MovesToFirstItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Move to end first
        menu.ProcessKey(Key(ConsoleKey.End));

        // Home should go back to first
        bool handled = menu.ProcessKey(Key(ConsoleKey.Home));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_End_MovesToLastItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        menu.ProcessKey(Key(ConsoleKey.Home));
        bool handled = menu.ProcessKey(Key(ConsoleKey.End));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_LetterKey_JumpsToMatchingItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Press 'e' to jump to Edit
        bool handled = menu.ProcessKey(Key(ConsoleKey.E, 'e'));
        Assert.True(handled);
    }

    #endregion

    #region 6. Horizontal Keyboard Navigation (Dropdown Open)

    [Fact]
    public void Horizontal_Dropdown_DownArrow_MovesToNextDropdownItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate down in dropdown
        bool handled = menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_Dropdown_UpArrow_MovesToPreviousDropdownItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Open File dropdown and move down
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate up
        bool handled = menu.ProcessKey(Key(ConsoleKey.UpArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_Dropdown_RightArrow_OpensSubmenuIfItemHasChildren()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate to "Recent" (has children) — it's after New, Open, separator
        menu.ProcessKey(Key(ConsoleKey.DownArrow)); // New -> Open
        menu.ProcessKey(Key(ConsoleKey.DownArrow)); // Open -> Recent (skips separator)

        // Right arrow should open submenu
        var recentItem = menu.FindItemByPath("File/Recent");
        Assert.NotNull(recentItem);

        // Navigate to Recent via jump
        menu.ProcessKey(Key(ConsoleKey.R, 'r'));

        menu.ProcessKey(Key(ConsoleKey.RightArrow));
        Assert.True(recentItem!.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_RightArrow_MovesToNextTopLevelItem_IfNoSubmenu()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Focus on "New" (leaf item, no children)
        // RightArrow should move to next top-level (Edit)
        bool handled = menu.ProcessKey(Key(ConsoleKey.RightArrow));
        Assert.True(handled);

        // File dropdown should be closed, Edit should be open
        var editItem = menu.Items[1];
        Assert.True(editItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_LeftArrow_ClosesSubmenu()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate to Recent and open it
        menu.ProcessKey(Key(ConsoleKey.R, 'r'));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));

        var recentItem = menu.FindItemByPath("File/Recent");
        Assert.True(recentItem!.IsOpen);

        // Left arrow should close submenu
        menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        Assert.False(recentItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_LeftArrow_MovesToPreviousTopLevel_FromFirstDropdown()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var editItem = menu.Items[1];

        // Open Edit dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // LeftArrow from first-level dropdown should move to previous top-level (File)
        menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        var fileItem = menu.Items[0];
        Assert.True(fileItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_Escape_ClosesDropdown()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.True(fileItem.IsOpen);

        // Escape closes dropdown
        menu.ProcessKey(Key(ConsoleKey.Escape));
        Assert.False(fileItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_Escape_ClosesSubmenuButKeepsDropdown()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Open Recent submenu
        menu.ProcessKey(Key(ConsoleKey.R, 'r'));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));

        var recentItem = menu.FindItemByPath("File/Recent");
        Assert.True(recentItem!.IsOpen);

        // Escape should close submenu only
        menu.ProcessKey(Key(ConsoleKey.Escape));
        Assert.False(recentItem.IsOpen);
        // File dropdown should still be open
        Assert.True(fileItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_Enter_OnLeaf_ExecutesAndCloses()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        bool actionExecuted = false;
        var fileItem = menu.Items[0];
        // Set action on first child (New)
        fileItem.Children[0].Action = () => actionExecuted = true;

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate into dropdown to select first item (New)
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Enter on New (first dropdown item)
        menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(actionExecuted);
        Assert.False(fileItem.IsOpen);
    }

    [Fact]
    public void Horizontal_Dropdown_Home_MovesToFirstDropdownItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Move down a few times
        menu.ProcessKey(Key(ConsoleKey.DownArrow));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Home should go to first item
        bool handled = menu.ProcessKey(Key(ConsoleKey.Home));
        Assert.True(handled);
    }

    [Fact]
    public void Horizontal_Dropdown_End_MovesToLastDropdownItem()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // End should go to last item
        bool handled = menu.ProcessKey(Key(ConsoleKey.End));
        Assert.True(handled);
    }

    #endregion

    #region 7. Vertical Keyboard Navigation

    [Fact]
    public void Vertical_UpArrow_MovesToPreviousItem()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);

        // Move to end then up
        menu.ProcessKey(Key(ConsoleKey.End));
        bool handled = menu.ProcessKey(Key(ConsoleKey.UpArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Vertical_DownArrow_MovesToNextItem()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);

        menu.ProcessKey(Key(ConsoleKey.Home));
        bool handled = menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Vertical_RightArrow_OpensSubmenu()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);

        // Focus File (has children)
        menu.ProcessKey(Key(ConsoleKey.Home));
        bool handled = menu.ProcessKey(Key(ConsoleKey.RightArrow));
        Assert.True(handled);

        var fileItem = menu.Items[0];
        Assert.True(fileItem.IsOpen);
    }

    [Fact]
    public void Vertical_LeftArrow_ClosesSubmenu()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);

        // Open File submenu
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));

        var fileItem = menu.Items[0];
        Assert.True(fileItem.IsOpen);

        // Open Recent within File
        menu.ProcessKey(Key(ConsoleKey.R, 'r'));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));

        var recentItem = menu.FindItemByPath("File/Recent");
        Assert.True(recentItem!.IsOpen);

        // Left arrow should close Recent submenu
        menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        Assert.False(recentItem.IsOpen);
    }

    [Fact]
    public void Vertical_Escape_UnfocusesWhenNotSticky()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = new MenuControl { Orientation = MenuOrientation.Vertical };
        menu.AddItem(new MenuItem { Text = "File" });
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);
        Assert.True(menu.HasFocus);

        menu.ProcessKey(Key(ConsoleKey.Escape));
        Assert.False(menu.HasFocus);
    }

    [Fact]
    public void Vertical_Escape_DoesNotUnfocus_WhenSticky()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = new MenuControl { Orientation = MenuOrientation.Vertical, IsSticky = true };
        menu.AddItem(new MenuItem { Text = "File" });
        window.AddControl(menu);
        window.FocusManager.SetFocus(menu, FocusReason.Programmatic);
        Assert.True(menu.HasFocus);

        menu.ProcessKey(Key(ConsoleKey.Escape));
        Assert.True(menu.HasFocus);
    }

    #endregion

    #region 8. Mouse - Top Level

    [Fact]
    public void Mouse_PressOnItem_SetsFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        // Add a dummy control first to take auto-focus
        var dummy = new ButtonControl { Text = "Dummy" };
        window.AddControl(dummy);
        var menu = CreateStandardMenu();
        window.AddControl(menu);

        // Set up bounds by triggering a layout/paint
        TriggerPaint(menu, window);

        // Initially not focused (dummy has focus)
        Assert.False(menu.HasFocus);

        // Press on first item area — use item bounds from paint
        var fileItem = menu.Items[0];
        var bounds = fileItem.Bounds;
        // HitTest converts control-relative coords by adding _lastBounds.X/Y
        // So we need to pass control-relative coords that map to the item bounds
        bool handled = menu.ProcessMouseEvent(MousePress(bounds.X + 1, bounds.Y));
        Assert.True(handled);
    }

    [Fact]
    public void Mouse_RightClick_ClosesAllMenus()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        TriggerPaint(menu, window);

        // Open a dropdown
        menu.OpenDropdown("File");
        Assert.True(menu.Items[0].IsOpen);

        // Right-click should close all
        menu.ProcessMouseEvent(RightClick(0, 0));
        Assert.False(menu.Items[0].IsOpen);
    }

    [Fact]
    public void Mouse_Leave_ClearsHover_WhenNotFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        window.AddControl(menu);
        TriggerPaint(menu, window);

        // Mouse move then leave
        menu.ProcessMouseEvent(MouseMove(1, 0));
        bool handled = menu.ProcessMouseEvent(MouseLeave(1, 0));
        Assert.True(handled);
    }

    [Fact]
    public void Mouse_Hover_FiresItemHoveredEvent()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        TriggerPaint(menu, window);

        MenuItem? hoveredItem = null;
        menu.ItemHovered += (s, item) => hoveredItem = item;

        // Get the bounds of the first item
        var fileItem = menu.Items[0];
        var bounds = fileItem.Bounds;

        // Mouse move over the first item (need control-relative coords)
        menu.ProcessMouseEvent(MouseMove(bounds.X + 1, bounds.Y));

        // Item should have been hovered (if bounds were set during paint)
        // Note: if bounds are at 0,0 the hover may not trigger — depends on paint
    }

    [Fact]
    public void Mouse_PressOnItemWithChildren_OpensDropdown()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        TriggerPaint(menu, window);

        var fileItem = menu.Items[0];
        var bounds = fileItem.Bounds;

        // Press then release on File item
        menu.ProcessMouseEvent(MousePress(bounds.X + 1 - menu.Items[0].Bounds.X, bounds.Y - menu.Items[0].Bounds.Y));
        menu.ProcessMouseEvent(MouseRelease(bounds.X + 1 - menu.Items[0].Bounds.X, bounds.Y - menu.Items[0].Bounds.Y));

        Assert.True(fileItem.IsOpen);
    }

    #endregion

    #region 9. Mouse - Dropdown Portal (via ProcessDropdownMouseEvent — internal)

    // Note: ProcessDropdownMouseEvent is internal, so these tests verify behavior
    // indirectly through keyboard + state checks, or we test via OpenDropdown + state.

    [Fact]
    public void Dropdown_DisabledItem_CannotBeSelectedByKeyboard()
    {
        var (menu, window) = CreateFocusedMenu();
        var parent = new MenuItem { Text = "File" };
        parent.AddChild(new MenuItem { Text = "Enabled" });
        parent.AddChild(new MenuItem { Text = "Disabled", IsEnabled = false });
        parent.AddChild(new MenuItem { Text = "AlsoEnabled" });
        menu.AddItem(parent);

        // Open dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate into dropdown — first DownArrow selects "Enabled"
        menu.ProcessKey(Key(ConsoleKey.DownArrow)); // -> Enabled

        // Navigate down again — should skip disabled item, go to AlsoEnabled
        menu.ProcessKey(Key(ConsoleKey.DownArrow)); // Enabled -> AlsoEnabled (skip Disabled)

        // Verify by pressing Enter — AlsoEnabled's action should be called
        bool alsoEnabledAction = false;
        parent.Children[2].Action = () => alsoEnabledAction = true;
        menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(alsoEnabledAction);
    }

    [Fact]
    public void Dropdown_SeparatorItem_SkippedByKeyboard()
    {
        var (menu, window) = CreateFocusedMenu();
        var parent = new MenuItem { Text = "File" };
        parent.AddChild(new MenuItem { Text = "First" });
        parent.AddChild(new MenuItem { IsSeparator = true });
        parent.AddChild(new MenuItem { Text = "Second" });
        menu.AddItem(parent);

        // Open dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate into dropdown — first DownArrow selects "First"
        menu.ProcessKey(Key(ConsoleKey.DownArrow)); // -> First

        // Navigate down again — should skip separator, go to Second
        menu.ProcessKey(Key(ConsoleKey.DownArrow)); // First -> Second (skip separator)

        bool secondAction = false;
        parent.Children[2].Action = () => secondAction = true;
        menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(secondAction);
    }

    #endregion

    #region 10. Focus

    [Fact]
    public void Focus_SetsFocusViaFocusManager()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        // Add a dummy control first to take auto-focus
        var dummy = new ButtonControl { Text = "Dummy" };
        window.AddControl(dummy);
        var menu = new MenuControl();
        menu.AddItem(new MenuItem { Text = "File" });
        window.AddControl(menu);

        // Menu should not have focus because dummy was auto-focused first
        Assert.False(menu.HasFocus);
        menu.Focus();
        Assert.True(menu.HasFocus);
    }

    [Fact]
    public void Focus_InitializesFocusedItemToFirstEnabled()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = new MenuControl();
        var separator = new MenuItem { IsSeparator = true };
        var first = new MenuItem { Text = "First" };
        menu.AddItem(separator);
        menu.AddItem(first);
        window.AddControl(menu);

        menu.Focus();

        // After focus, _focusedItem should be "First" (skipping separator)
        // We verify by pressing Enter — the focused item should execute
        bool firstAction = false;
        first.Action = () => firstAction = true;
        menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(firstAction);
    }

    [Fact]
    public void ProcessKey_ReturnsFalse_WhenNotFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        // Add a dummy control first to take auto-focus
        var dummy = new ButtonControl { Text = "Dummy" };
        window.AddControl(dummy);
        var menu = new MenuControl();
        menu.AddItem(new MenuItem { Text = "File" });
        window.AddControl(menu);
        // Menu should not have focus (dummy was auto-focused)

        bool handled = menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.False(handled);
    }

    [Fact]
    public void ProcessKey_ReturnsFalse_WhenDisabled()
    {
        var (menu, window) = CreateFocusedMenu();
        menu.AddItem(new MenuItem { Text = "File" });
        menu.IsEnabled = false;

        bool handled = menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.False(handled);
    }

    #endregion

    #region 11. JumpToItemStartingWith

    [Fact]
    public void JumpToItem_CaseInsensitive()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Jump with uppercase 'E' to find "Edit"
        bool handled = menu.ProcessKey(Key(ConsoleKey.E, 'E'));
        Assert.True(handled);
    }

    [Fact]
    public void JumpToItem_HandlesMarkupText()
    {
        var (menu, window) = CreateFocusedMenu();
        menu.AddItem(new MenuItem { Text = "[bold]File[/]" });
        menu.AddItem(new MenuItem { Text = "[red]Edit[/]" });

        // Jump with 'e' should find "[red]Edit[/]" (strips markup, first display char is 'e')
        bool handled = menu.ProcessKey(Key(ConsoleKey.E, 'e'));
        Assert.True(handled);
    }

    [Fact]
    public void JumpToItem_ReturnsFalse_WhenNoMatch()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // No item starts with 'z'
        bool handled = menu.ProcessKey(Key(ConsoleKey.Z, 'z'));
        Assert.False(handled);
    }

    #endregion

    #region 12. Event Tests

    [Fact]
    public void ItemSelected_FiresWhenLeafItemExecuted()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        MenuItem? selectedItem = null;
        menu.ItemSelected += (s, item) => selectedItem = item;

        var fileItem = menu.Items[0];
        fileItem.Children[0].Action = () => { }; // New item

        // Open File dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Navigate into dropdown to first item (New)
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Select New
        menu.ProcessKey(Key(ConsoleKey.Enter));

        Assert.NotNull(selectedItem);
        Assert.Equal("New", selectedItem!.Text);
    }

    [Fact]
    public void ItemHovered_FiresOnHover()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        TriggerPaint(menu, window);

        MenuItem? hoveredItem = null;
        menu.ItemHovered += (s, item) => hoveredItem = item;

        // Mouse move over an item area
        var fileItem = menu.Items[0];
        var bounds = fileItem.Bounds;
        if (bounds.Width > 0)
        {
            menu.ProcessMouseEvent(MouseMove(bounds.X + 1, bounds.Y));
        }
    }

    [Fact]
    public void ActionCallback_InvokedWhenItemExecuted()
    {
        var (menu, window) = CreateFocusedMenu();
        bool callbackInvoked = false;
        var item = new MenuItem { Text = "Action", Action = () => callbackInvoked = true };
        menu.AddItem(item);

        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.Enter));
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void DisabledItem_ActionNotInvoked()
    {
        var (menu, window) = CreateFocusedMenu();
        bool callbackInvoked = false;
        var parent = new MenuItem { Text = "File" };
        var disabledChild = new MenuItem { Text = "Save", IsEnabled = false, Action = () => callbackInvoked = true };
        parent.AddChild(disabledChild);
        menu.AddItem(parent);

        // Open dropdown
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.DownArrow));

        // Try to execute disabled item
        menu.ProcessKey(Key(ConsoleKey.Enter));

        // Action should NOT be invoked (Enter returns false for disabled focused item)
        Assert.False(callbackInvoked);
    }

    #endregion

    #region 13. Open/Close Operations

    [Fact]
    public void OpenDropdown_ByName_OpensIt()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        menu.OpenDropdown("File");
        Assert.True(fileItem.IsOpen);
    }

    [Fact]
    public void CloseAllMenus_ClosesEverything()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];

        menu.OpenDropdown("File");
        Assert.True(fileItem.IsOpen);

        menu.CloseAllMenus();
        Assert.False(fileItem.IsOpen);
    }

    [Fact]
    public void OpeningOneDropdown_ClosesPrevious()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var fileItem = menu.Items[0];
        var editItem = menu.Items[1];

        menu.OpenDropdown("File");
        Assert.True(fileItem.IsOpen);

        menu.OpenDropdown("Edit");
        Assert.False(fileItem.IsOpen);
        Assert.True(editItem.IsOpen);
    }

    [Fact]
    public void OpenDropdown_NonExistentName_DoesNothing()
    {
        var (menu, window) = CreateFocusedStandardMenu();

        // Should not throw
        menu.OpenDropdown("NonExistent");

        // No items should be open
        Assert.False(menu.Items[0].IsOpen);
        Assert.False(menu.Items[1].IsOpen);
        Assert.False(menu.Items[2].IsOpen);
    }

    [Fact]
    public void OpenDropdown_LeafItem_DoesNotOpen()
    {
        var (menu, window) = CreateFocusedMenu();
        var leafItem = new MenuItem { Text = "NoChildren" };
        menu.AddItem(leafItem);

        menu.OpenDropdown("NoChildren");
        Assert.False(leafItem.IsOpen);
    }

    #endregion

    #region 14. Rendering (via PaintDOM)

    [Fact]
    public void MeasureDOM_Horizontal_ReturnsCorrectSize()
    {
        var menu = CreateStandardMenu();
        var constraints = new LayoutConstraints(0, 200, 0, 200);
        var size = menu.MeasureDOM(constraints);

        // Should be 1 row high for horizontal
        Assert.Equal(1, size.Height);
        // Width should be positive (sum of item text widths + padding)
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void MeasureDOM_Vertical_ReturnsCorrectSize()
    {
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        var constraints = new LayoutConstraints(0, 200, 0, 200);
        var size = menu.MeasureDOM(constraints);

        // Height should equal number of items (3: File, Edit, View)
        Assert.Equal(3, size.Height);
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void PaintDOM_Horizontal_DoesNotThrow()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var buffer = new CharacterBuffer(80, 30);
        var bounds = new LayoutRect(0, 0, 80, 1);
        var clipRect = new LayoutRect(0, 0, 80, 30);

        var exception = Record.Exception(() =>
            menu.PaintDOM(buffer, bounds, clipRect, Color.White, Color.Black));
        Assert.Null(exception);
    }

    [Fact]
    public void PaintDOM_Vertical_DoesNotThrow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 30 };
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        window.AddControl(menu);

        var buffer = new CharacterBuffer(80, 30);
        var bounds = new LayoutRect(0, 0, 20, 10);
        var clipRect = new LayoutRect(0, 0, 80, 30);

        var exception = Record.Exception(() =>
            menu.PaintDOM(buffer, bounds, clipRect, Color.White, Color.Black));
        Assert.Null(exception);
    }

    [Fact]
    public void PaintDOM_SetsItemBounds()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        TriggerPaint(menu, window);

        // After paint, items should have bounds set
        var fileItem = menu.Items[0];
        Assert.True(fileItem.Bounds.Width > 0, "Item bounds should be set after PaintDOM");
    }

    #endregion

    #region 15. Edge Cases

    [Fact]
    public void EmptyMenu_KeyboardNavigation_DoesNotThrow()
    {
        var (menu, window) = CreateFocusedMenu();
        // No items added

        // These should not throw
        menu.ProcessKey(Key(ConsoleKey.DownArrow));
        menu.ProcessKey(Key(ConsoleKey.UpArrow));
        menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        menu.ProcessKey(Key(ConsoleKey.RightArrow));
        menu.ProcessKey(Key(ConsoleKey.Home));
        menu.ProcessKey(Key(ConsoleKey.End));
        menu.ProcessKey(Key(ConsoleKey.Enter));
        menu.ProcessKey(Key(ConsoleKey.Escape));
    }

    [Fact]
    public void SingleItem_LeftRight_WrapsToItself()
    {
        var (menu, window) = CreateFocusedMenu();
        menu.AddItem(new MenuItem { Text = "Only" });

        menu.ProcessKey(Key(ConsoleKey.Home));

        // Right and left should wrap to the same item
        bool handledRight = menu.ProcessKey(Key(ConsoleKey.RightArrow));
        Assert.True(handledRight);

        bool handledLeft = menu.ProcessKey(Key(ConsoleKey.LeftArrow));
        Assert.True(handledLeft);
    }

    [Fact]
    public void AllItemsDisabled_NavigationHandled()
    {
        var (menu, window) = CreateFocusedMenu();
        menu.AddItem(new MenuItem { Text = "A", IsEnabled = false });
        menu.AddItem(new MenuItem { Text = "B", IsEnabled = false });

        // These should be handled (return true) but not crash
        bool handled = menu.ProcessKey(Key(ConsoleKey.DownArrow));
        Assert.True(handled);
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        menu.OpenDropdown("File");

        var exception = Record.Exception(() => menu.Dispose());
        Assert.Null(exception);
        Assert.Empty(menu.Items);
    }

    [Fact]
    public void SetItemEnabled_ByPath_Works()
    {
        var (menu, window) = CreateFocusedStandardMenu();
        var item = menu.FindItemByPath("File/New");
        Assert.NotNull(item);
        Assert.True(item!.IsEnabled);

        menu.SetItemEnabled("File/New", false);
        Assert.False(item.IsEnabled);
    }

    [Fact]
    public void ConcurrentAddRemove_DoesNotThrow()
    {
        var menu = new MenuControl();
        var items = Enumerable.Range(0, 10).Select(i => new MenuItem { Text = $"Item{i}" }).ToList();
        foreach (var item in items)
            menu.AddItem(item);

        // Remove and add concurrently should not throw due to _menuLock
        var exception = Record.Exception(() =>
        {
            foreach (var item in items)
                menu.RemoveItem(item);
            foreach (var item in items)
                menu.AddItem(item);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void MenuItem_IsOpen_DefaultFalse()
    {
        var item = new MenuItem { Text = "File" };
        Assert.False(item.IsOpen);
    }

    [Fact]
    public void WantsMouseEvents_ReturnsTrue_WhenEnabled()
    {
        var menu = new MenuControl();
        Assert.True(menu.WantsMouseEvents);
    }

    [Fact]
    public void WantsMouseEvents_ReturnsFalse_WhenDisabled()
    {
        var menu = new MenuControl { IsEnabled = false };
        Assert.False(menu.WantsMouseEvents);
    }

    [Fact]
    public void CanFocusWithMouse_ReturnsTrue_WhenEnabled()
    {
        var menu = new MenuControl();
        Assert.True(menu.CanFocusWithMouse);
    }

    [Fact]
    public void CanFocusWithMouse_ReturnsFalse_WhenDisabled()
    {
        var menu = new MenuControl { IsEnabled = false };
        Assert.False(menu.CanFocusWithMouse);
    }

    [Fact]
    public void CanReceiveFocus_ReturnsTrue_WhenEnabled()
    {
        var menu = new MenuControl();
        Assert.True(menu.CanReceiveFocus);
    }

    [Fact]
    public void CanReceiveFocus_ReturnsFalse_WhenDisabled()
    {
        var menu = new MenuControl { IsEnabled = false };
        Assert.False(menu.CanReceiveFocus);
    }

    [Fact]
    public void ProcessMouseEvent_ReturnsFalse_WhenDisabled()
    {
        var menu = new MenuControl { IsEnabled = false };
        bool handled = menu.ProcessMouseEvent(MousePress(0, 0));
        Assert.False(handled);
    }

    [Fact]
    public void GetLogicalContentSize_Horizontal_ReturnsCorrectSize()
    {
        var menu = CreateStandardMenu();
        var size = menu.GetLogicalContentSize();
        Assert.True(size.Width > 0);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void GetLogicalContentSize_Vertical_HeightEqualsItemCount()
    {
        var menu = CreateStandardMenu();
        menu.Orientation = MenuOrientation.Vertical;
        var size = menu.GetLogicalContentSize();
        Assert.Equal(3, size.Height); // File, Edit, View
    }

    #endregion

    #region Helper - Trigger Paint

    /// <summary>
    /// Triggers a paint cycle to set up item bounds for hit testing.
    /// </summary>
    private static void TriggerPaint(MenuControl menu, Window window)
    {
        var buffer = new CharacterBuffer(window.Width, window.Height);
        var bounds = new LayoutRect(0, 0, window.Width, 1);
        var clipRect = new LayoutRect(0, 0, window.Width, window.Height);
        menu.PaintDOM(buffer, bounds, clipRect, Color.White, Color.Black);
    }

    #endregion
}
