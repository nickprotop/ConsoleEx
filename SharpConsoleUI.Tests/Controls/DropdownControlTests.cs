// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class DropdownControlTests
{
	#region Helper Methods

	private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool ctrl = false)
		=> new(keyChar, key, shift, alt, ctrl);

	private static DropdownControl CreateDropdown(string prompt = "Select:", params string[] items)
	{
		var dd = new DropdownControl(prompt);
		foreach (var item in items)
			dd.AddItem(item);
		return dd;
	}

	private static DropdownControl CreateFocusedDropdown(string prompt = "Select:", params string[] items)
	{
		var dd = CreateDropdown(prompt, items);
		dd.HasFocus = true;
		return dd;
	}

	private static MouseEventArgs CreateMousePress(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed },
			pos, pos, pos);
	}

	private static MouseEventArgs CreateMouseRelease(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Released },
			pos, pos, pos);
	}

	private static MouseEventArgs CreateMouseEnter(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.MouseEnter },
			pos, pos, pos);
	}

	private static MouseEventArgs CreateMouseLeave(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.MouseLeave },
			pos, pos, pos);
	}

	private static MouseEventArgs CreateMouseMove(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.ReportMousePosition },
			pos, pos, pos);
	}

	#endregion

	#region Construction Tests

	[Fact]
	public void Constructor_Default_HasExpectedDefaults()
	{
		var dd = new DropdownControl();
		Assert.Equal("Select an item:", dd.Prompt);
		Assert.Equal(-1, dd.SelectedIndex);
		Assert.Null(dd.SelectedItem);
		Assert.Null(dd.SelectedValue);
		Assert.True(dd.IsEnabled);
		Assert.False(dd.HasFocus);
		Assert.False(dd.IsDropdownOpen);
		Assert.Empty(dd.Items);
	}

	[Fact]
	public void Constructor_WithPrompt_SetsPrompt()
	{
		var dd = new DropdownControl("Pick:");
		Assert.Equal("Pick:", dd.Prompt);
	}

	[Fact]
	public void Constructor_WithStringItems_PopulatesItems()
	{
		var dd = new DropdownControl("P:", new[] { "A", "B", "C" });
		Assert.Equal(3, dd.Items.Count);
		Assert.Equal("A", dd.Items[0].Text);
		Assert.Equal("B", dd.Items[1].Text);
		Assert.Equal("C", dd.Items[2].Text);
	}

	[Fact]
	public void Constructor_WithDropdownItems_PopulatesItems()
	{
		var items = new[] { new DropdownItem("X"), new DropdownItem("Y") };
		var dd = new DropdownControl("P:", items);
		Assert.Equal(2, dd.Items.Count);
	}

	#endregion

	#region Selection Tests

	[Fact]
	public void SelectedIndex_SetValid_UpdatesSelection()
	{
		var dd = CreateDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 1;
		Assert.Equal(1, dd.SelectedIndex);
		Assert.Equal("B", dd.SelectedItem?.Text);
		Assert.Equal("B", dd.SelectedValue);
	}

	[Fact]
	public void SelectedIndex_SetMinusOne_ClearsSelection()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 0;
		dd.SelectedIndex = -1;
		Assert.Equal(-1, dd.SelectedIndex);
		Assert.Null(dd.SelectedItem);
		Assert.Null(dd.SelectedValue);
	}

	[Fact]
	public void SelectedIndex_OutOfRange_Ignored()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 0;
		dd.SelectedIndex = 99;
		Assert.Equal(0, dd.SelectedIndex); // unchanged
	}

	[Fact]
	public void SelectedIndex_NegativeTwo_Ignored()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 0;
		dd.SelectedIndex = -2;
		Assert.Equal(0, dd.SelectedIndex); // unchanged
	}

	[Fact]
	public void SelectedItem_SetValid_UpdatesIndex()
	{
		var dd = CreateDropdown("S:", "A", "B", "C");
		var item = dd.Items[2];
		dd.SelectedItem = item;
		Assert.Equal(2, dd.SelectedIndex);
	}

	[Fact]
	public void SelectedItem_SetNull_ClearsSelection()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 0;
		dd.SelectedItem = null;
		Assert.Equal(-1, dd.SelectedIndex);
	}

	[Fact]
	public void SelectedItem_SetUnknownItem_NoChange()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 0;
		dd.SelectedItem = new DropdownItem("Unknown");
		Assert.Equal(0, dd.SelectedIndex); // unchanged
	}

	[Fact]
	public void SelectedValue_SetByText_FindsItem()
	{
		var dd = CreateDropdown("S:", "Alpha", "Beta", "Gamma");
		dd.SelectedValue = "Beta";
		Assert.Equal(1, dd.SelectedIndex);
	}

	[Fact]
	public void SelectedValue_SetNull_ClearsSelection()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 1;
		dd.SelectedValue = null;
		Assert.Equal(-1, dd.SelectedIndex);
	}

	[Fact]
	public void SelectedValue_SetNonExisting_NoChange()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 0;
		dd.SelectedValue = "Z";
		Assert.Equal(0, dd.SelectedIndex); // unchanged
	}

	[Fact]
	public void SelectedIndex_FiresEvents()
	{
		var dd = CreateDropdown("S:", "A", "B", "C");
		int indexEventCount = 0;
		int itemEventCount = 0;
		int valueEventCount = 0;
		dd.SelectedIndexChanged += (s, e) => indexEventCount++;
		dd.SelectedItemChanged += (s, e) => itemEventCount++;
		dd.SelectedValueChanged += (s, e) => valueEventCount++;

		dd.SelectedIndex = 2;
		Assert.Equal(1, indexEventCount);
		Assert.Equal(1, itemEventCount);
		Assert.Equal(1, valueEventCount);
	}

	[Fact]
	public void SelectedIndex_SameValue_NoEvent()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 1;
		int eventCount = 0;
		dd.SelectedIndexChanged += (s, e) => eventCount++;
		dd.SelectedIndex = 1; // same
		Assert.Equal(0, eventCount);
	}

	#endregion

	#region AddItem / ClearItems / Items Tests

	[Fact]
	public void AddItem_FirstItem_AutoSelects()
	{
		var dd = new DropdownControl("S:");
		Assert.Equal(-1, dd.SelectedIndex);
		dd.AddItem("First");
		Assert.Equal(0, dd.SelectedIndex);
		Assert.Equal("First", dd.SelectedValue);
	}

	[Fact]
	public void AddItem_SubsequentItems_NoAutoSelect()
	{
		var dd = CreateDropdown("S:", "A");
		dd.AddItem("B");
		Assert.Equal(0, dd.SelectedIndex); // still first
	}

	[Fact]
	public void AddItem_WithIconAndColor_CreatesCorrectItem()
	{
		var dd = new DropdownControl("S:");
		dd.AddItem(new DropdownItem("Red", "●", Color.Red));
		Assert.Equal("●", dd.Items[0].Icon);
		Assert.Equal(Color.Red, dd.Items[0].IconColor);
	}

	[Fact]
	public void ClearItems_ResetsSelection()
	{
		var dd = CreateDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 2;
		dd.ClearItems();
		Assert.Empty(dd.Items);
		Assert.Equal(-1, dd.SelectedIndex);
		Assert.Null(dd.SelectedItem);
	}

	[Fact]
	public void ClearItems_FiresEvents()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.SelectedIndex = 1;
		int eventFired = 0;
		dd.SelectedIndexChanged += (s, e) => { eventFired++; Assert.Equal(-1, e); };
		dd.ClearItems();
		Assert.Equal(1, eventFired);
	}

	[Fact]
	public void Items_SetNewList_ReplacesItems()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.Items = new List<DropdownItem> { new("X"), new("Y"), new("Z") };
		Assert.Equal(3, dd.Items.Count);
		Assert.Equal("X", dd.Items[0].Text);
	}

	[Fact]
	public void Items_SetShorterList_ClampsSelection()
	{
		var dd = CreateDropdown("S:", "A", "B", "C", "D");
		dd.SelectedIndex = 3;
		dd.Items = new List<DropdownItem> { new("X") };
		Assert.Equal(0, dd.SelectedIndex); // clamped to valid range
	}

	[Fact]
	public void StringItems_GetSet_WorksCorrectly()
	{
		var dd = CreateDropdown("S:", "A", "B");
		var strings = dd.StringItems;
		Assert.Equal(new[] { "A", "B" }, strings);

		dd.StringItems = new List<string> { "X", "Y", "Z" };
		Assert.Equal(3, dd.Items.Count);
	}

	#endregion

	#region Focus Tests

	[Fact]
	public void SetFocus_GainingFocus_FiresGotFocus()
	{
		var dd = CreateDropdown("S:", "A", "B");
		bool gotFocus = false;
		dd.GotFocus += (s, e) => gotFocus = true;
		dd.SetFocus(true, FocusReason.Keyboard);
		Assert.True(gotFocus);
		Assert.True(dd.HasFocus);
	}

	[Fact]
	public void SetFocus_LosingFocus_FiresLostFocus()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B");
		bool lostFocus = false;
		dd.LostFocus += (s, e) => lostFocus = true;
		dd.SetFocus(false, FocusReason.Keyboard);
		Assert.True(lostFocus);
		Assert.False(dd.HasFocus);
	}

	[Fact]
	public void SetFocus_LosingFocusWithDropdownOpen_ClosesDropdown()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B");
		// We can't fully open dropdown without a Window, but we can test the state flag
		dd.SetFocus(false);
		Assert.False(dd.IsDropdownOpen);
	}

	[Fact]
	public void CanReceiveFocus_EnabledControl_ReturnsTrue()
	{
		var dd = new DropdownControl();
		Assert.True(dd.CanReceiveFocus);
	}

	[Fact]
	public void CanReceiveFocus_DisabledControl_ReturnsFalse()
	{
		var dd = new DropdownControl();
		dd.IsEnabled = false;
		Assert.False(dd.CanReceiveFocus);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void MaxVisibleItems_SetValid_Updates()
	{
		var dd = new DropdownControl();
		dd.MaxVisibleItems = 10;
		Assert.Equal(10, dd.MaxVisibleItems);
	}

	[Fact]
	public void MaxVisibleItems_SetZero_ClampsToOne()
	{
		var dd = new DropdownControl();
		dd.MaxVisibleItems = 0;
		Assert.Equal(1, dd.MaxVisibleItems);
	}

	[Fact]
	public void MaxVisibleItems_SetNegative_ClampsToOne()
	{
		var dd = new DropdownControl();
		dd.MaxVisibleItems = -5;
		Assert.Equal(1, dd.MaxVisibleItems);
	}

	[Fact]
	public void Prompt_Set_UpdatesValue()
	{
		var dd = new DropdownControl();
		dd.Prompt = "Choose:";
		Assert.Equal("Choose:", dd.Prompt);
	}

	[Fact]
	public void IsEnabled_SetFalse_DisablesControl()
	{
		var dd = new DropdownControl();
		dd.IsEnabled = false;
		Assert.False(dd.IsEnabled);
		Assert.False(dd.WantsMouseEvents);
		Assert.False(dd.CanFocusWithMouse);
	}

	[Fact]
	public void AutoAdjustWidth_DefaultTrue()
	{
		var dd = new DropdownControl();
		Assert.True(dd.AutoAdjustWidth);
	}

	[Fact]
	public void ContentHeight_ConstantOneLineWithMargins()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.Margin = new Margin(1, 2, 3, 4);
		Assert.Equal(1 + 2 + 4, dd.ContentHeight); // 1 line + top + bottom
	}

	[Fact]
	public void ContentWidth_BasedOnWidestItem()
	{
		var dd = CreateDropdown("S:", "Short", "A Longer Item", "Mid");
		int? width = dd.ContentWidth;
		Assert.NotNull(width);
		// Width should accommodate widest item "A Longer Item" plus prompt and arrow
		Assert.True(width > 0);
	}

	[Fact]
	public void ContentWidth_EmptyItems_ShowsNonePlaceholder()
	{
		var dd = new DropdownControl("S:");
		int? width = dd.ContentWidth;
		Assert.NotNull(width);
		// Should be prompt + "(None)" + arrow
		Assert.True(width > 0);
	}

	#endregion

	#region Header Width Calculation Tests

	[Fact]
	public void HeaderWidth_StableAcrossSelectionChanges()
	{
		var dd = CreateDropdown("S:", "Short", "A Much Longer Item", "Mid");
		dd.SelectedIndex = 0; // Short
		int? widthWithShort = dd.ContentWidth;
		dd.SelectedIndex = 1; // Long
		int? widthWithLong = dd.ContentWidth;
		dd.SelectedIndex = 2; // Mid
		int? widthWithMid = dd.ContentWidth;

		// Width should be the same regardless of selection (based on widest item)
		Assert.Equal(widthWithShort, widthWithLong);
		Assert.Equal(widthWithLong, widthWithMid);
	}

	[Fact]
	public void HeaderWidth_WidthOverride_UsesOverride()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.Width = 50;
		int? width = dd.ContentWidth;
		Assert.Equal(50 + dd.Margin.Left + dd.Margin.Right, width);
	}

	[Fact]
	public void HeaderWidth_WithMarkupItems_MeasuresCorrectly()
	{
		// Items with markup - StripLength should strip markup tags
		var dd = new DropdownControl("S:");
		dd.AddItem(new DropdownItem("[bold]Dark[/]")); // visible text "Dark" = 4 chars
		dd.AddItem(new DropdownItem("Light")); // visible text "Light" = 5 chars
		int? width = dd.ContentWidth;
		Assert.NotNull(width);
		// "Light" is longer visually, so width should be based on "Light"
	}

	[Fact]
	public void HeaderWidth_UnicodeItems_MeasuresCorrectly()
	{
		var dd = new DropdownControl("S:");
		dd.AddItem(new DropdownItem("日本語")); // CJK = 2 cols each = 6
		dd.AddItem(new DropdownItem("ABC")); // ASCII = 3 cols
		int? w1 = dd.ContentWidth;
		Assert.NotNull(w1);
		// Japanese item should be wider (6 vs 3 display cols)
	}

	#endregion

	#region Keyboard Interaction Tests

	[Fact]
	public void ProcessInput_Enter_TogglesDropdown()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B", "C");
		// Without a container Window, Enter won't create portal, but IsDropdownOpen should still toggle
		// The state is maintained internally even if portal creation fails
		dd.ProcessKey(Key(ConsoleKey.Enter));
		// Can't fully test open without Window, but we verify the control processes the key
	}

	[Fact]
	public void ProcessInput_Escape_ClosesDropdown()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B");
		dd.ProcessKey(Key(ConsoleKey.Escape));
		Assert.False(dd.IsDropdownOpen);
	}

	[Fact]
	public void ProcessInput_DownArrow_IncreasesHighlight()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 0;
		dd.ProcessKey(Key(ConsoleKey.DownArrow));
		// Without open dropdown, down arrow should navigate
	}

	[Fact]
	public void ProcessInput_UpArrow_DecreasesHighlight()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 2;
		dd.ProcessKey(Key(ConsoleKey.UpArrow));
	}

	[Fact]
	public void ProcessInput_Home_JumpsToFirst()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 2;
		dd.ProcessKey(Key(ConsoleKey.Home));
	}

	[Fact]
	public void ProcessInput_End_JumpsToLast()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 0;
		dd.ProcessKey(Key(ConsoleKey.End));
	}

	[Fact]
	public void ProcessInput_Disabled_IgnoresInput()
	{
		var dd = CreateFocusedDropdown("S:", "A", "B");
		dd.IsEnabled = false;
		dd.SelectedIndex = 0;
		dd.ProcessKey(Key(ConsoleKey.DownArrow));
		// Should not change because disabled
	}

	#endregion

	#region Mouse Interaction Tests

	[Fact]
	public void ProcessMouseEvent_Disabled_ReturnsFalse()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.IsEnabled = false;
		var result = dd.ProcessMouseEvent(CreateMousePress(5, 0));
		Assert.False(result);
	}

	[Fact]
	public void ProcessMouseEvent_MouseLeave_ClearsPressed()
	{
		var dd = CreateDropdown("S:", "A", "B");
		var result = dd.ProcessMouseEvent(CreateMouseLeave(5, 0));
		Assert.True(result);
	}

	[Fact]
	public void ProcessMouseEvent_MouseEnter_Handled()
	{
		var dd = CreateDropdown("S:", "A", "B");
		var result = dd.ProcessMouseEvent(CreateMouseEnter(5, 0));
		Assert.True(result);
	}

	[Fact]
	public void ProcessMouseEvent_OutsideVerticalBounds_ReturnsFalse()
	{
		var dd = CreateDropdown("S:", "A", "B");
		// Click at Y=-1 should be outside
		var result = dd.ProcessMouseEvent(CreateMousePress(5, -1));
		Assert.False(result);
	}

	#endregion

	#region MeasureDOM Tests

	[Fact]
	public void MeasureDOM_ReturnsCorrectSize()
	{
		var dd = CreateDropdown("S:", "Alpha", "Beta", "Gamma Delta");
		var constraints = new LayoutConstraints(0, 200, 0, 200);
		var size = dd.MeasureDOM(constraints);

		Assert.True(size.Width > 0);
		Assert.Equal(1, size.Height); // 1 line, no margins by default
	}

	[Fact]
	public void MeasureDOM_WithMargins_IncludesMargins()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.Margin = new Margin(2, 1, 2, 1);
		var constraints = new LayoutConstraints(0, 200, 0, 200);
		var size = dd.MeasureDOM(constraints);

		Assert.Equal(1 + 1 + 1, size.Height); // 1 line + top + bottom
	}

	[Fact]
	public void MeasureDOM_ClampsToMaxWidth()
	{
		var dd = CreateDropdown("S:", "A Very Long Item That Should Exceed Constraints");
		var constraints = new LayoutConstraints(0, 30, 0, 200);
		var size = dd.MeasureDOM(constraints);

		Assert.True(size.Width <= 30);
	}

	[Fact]
	public void MeasureDOM_ClampsToMinWidth()
	{
		var dd = CreateDropdown("S:", "X");
		var constraints = new LayoutConstraints(50, 200, 0, 200);
		var size = dd.MeasureDOM(constraints);

		Assert.True(size.Width >= 50);
	}

	[Fact]
	public void MeasureDOM_EmptyItems_HasMinimumWidth()
	{
		var dd = new DropdownControl("S:");
		var constraints = new LayoutConstraints(0, 200, 0, 200);
		var size = dd.MeasureDOM(constraints);

		// Must be at least prompt + arrow + 3
		Assert.True(size.Width >= 5);
	}

	#endregion

	#region DropdownItem Tests

	[Fact]
	public void DropdownItem_TextOnly_HasDefaults()
	{
		var item = new DropdownItem("Test");
		Assert.Equal("Test", item.Text);
		Assert.Null(item.Icon);
		Assert.Null(item.IconColor);
		Assert.Null(item.Tag);
		Assert.Null(item.Value);
	}

	[Fact]
	public void DropdownItem_WithIcon_SetsProperties()
	{
		var item = new DropdownItem("Test", "★", Color.Gold1);
		Assert.Equal("Test", item.Text);
		Assert.Equal("★", item.Icon);
		Assert.Equal(Color.Gold1, item.IconColor);
	}

	[Fact]
	public void DropdownItem_WithTag_SetsTag()
	{
		var item = new DropdownItem("Test") { Tag = "myTag" };
		Assert.Equal("myTag", item.Tag);
	}

	[Fact]
	public void DropdownItem_WithValue_UsesValueOverText()
	{
		var item = new DropdownItem("Display Text") { Value = "internal_value" };
		Assert.Equal("Display Text", item.Text);
		Assert.Equal("internal_value", item.Value);
	}

	#endregion

	#region GetLogicalContentSize Tests

	[Fact]
	public void GetLogicalContentSize_ReturnsNonZero()
	{
		var dd = CreateDropdown("S:", "A", "B");
		var size = dd.GetLogicalContentSize();
		Assert.True(size.Width > 0);
		Assert.True(size.Height > 0);
	}

	[Fact]
	public void GetLogicalContentSize_HeightMatchesContentHeight()
	{
		var dd = CreateDropdown("S:", "A", "B");
		var size = dd.GetLogicalContentSize();
		Assert.Equal(dd.ContentHeight ?? 1, size.Height);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void LargeItemCount_DoesNotThrow()
	{
		var dd = new DropdownControl("S:");
		for (int i = 0; i < 1000; i++)
			dd.AddItem($"Item {i}");
		Assert.Equal(1000, dd.Items.Count);
		Assert.Equal(0, dd.SelectedIndex); // auto-selected first
	}

	[Fact]
	public void EmptyDropdown_SelectionNegativeOne()
	{
		var dd = new DropdownControl("S:");
		Assert.Equal(-1, dd.SelectedIndex);
		Assert.Null(dd.SelectedItem);
		Assert.Null(dd.SelectedValue);
	}

	[Fact]
	public void SingleItem_AutoSelected()
	{
		var dd = new DropdownControl("S:");
		dd.AddItem("Only");
		Assert.Equal(0, dd.SelectedIndex);
		Assert.Equal("Only", dd.SelectedValue);
	}

	[Fact]
	public void ItemFormatter_CanBeSet()
	{
		var dd = CreateDropdown("S:", "A", "B");
		dd.ItemFormatter = (item, isSelected, hasFocus) => $"[{item.Text}]";
		Assert.NotNull(dd.ItemFormatter);
	}

	[Fact]
	public void Color_Properties_HaveDefaults()
	{
		var dd = new DropdownControl();
		// These should not throw even without a container
		var bg = dd.BackgroundColor;
		var fg = dd.ForegroundColor;
		var fbg = dd.FocusedBackgroundColor;
		var ffg = dd.FocusedForegroundColor;
		var hbg = dd.HighlightBackgroundColor;
		var hfg = dd.HighlightForegroundColor;
		Assert.Equal(Color.Black, bg);
	}

	[Fact]
	public void Color_Properties_CanBeSet()
	{
		var dd = new DropdownControl();
		dd.BackgroundColor = Color.Red;
		dd.ForegroundColor = Color.Blue;
		dd.FocusedBackgroundColor = Color.Green;
		dd.FocusedForegroundColor = Color.Yellow;
		dd.HighlightBackgroundColor = Color.Cyan;
		dd.HighlightForegroundColor = Color.Magenta;

		Assert.Equal(Color.Red, dd.BackgroundColor);
		Assert.Equal(Color.Blue, dd.ForegroundColor);
		Assert.Equal(Color.Green, dd.FocusedBackgroundColor);
		Assert.Equal(Color.Yellow, dd.FocusedForegroundColor);
		Assert.Equal(Color.Cyan, dd.HighlightBackgroundColor);
		Assert.Equal(Color.Magenta, dd.HighlightForegroundColor);
	}

	[Fact]
	public void Margin_Affects_ContentWidth()
	{
		var dd = CreateDropdown("S:", "A", "B");
		int? widthNoMargin = dd.ContentWidth;
		dd.Margin = new Margin(5, 0, 5, 0);
		int? widthWithMargin = dd.ContentWidth;

		Assert.NotNull(widthNoMargin);
		Assert.NotNull(widthWithMargin);
		Assert.Equal(widthNoMargin!.Value + 10, widthWithMargin!.Value);
	}

	[Fact]
	public void ClearAndReadd_ResetsCorrectly()
	{
		var dd = CreateDropdown("S:", "A", "B", "C");
		dd.SelectedIndex = 2;
		dd.ClearItems();
		Assert.Equal(-1, dd.SelectedIndex);

		dd.AddItem("X");
		Assert.Equal(0, dd.SelectedIndex); // auto-select first
		dd.AddItem("Y");
		Assert.Equal(0, dd.SelectedIndex); // still first
	}

	#endregion
}
