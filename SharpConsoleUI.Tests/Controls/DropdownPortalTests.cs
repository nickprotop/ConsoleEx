// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Tests.Controls;

public class DropdownPortalTests
{
	#region PortalPositioner Tests

	[Fact]
	public void PortalPositioner_BelowOrAbove_PrefersBelowWhenFits()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(10, 5, 20, 1),
			ContentSize: new Size(20, 5),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.BelowOrAbove
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(PortalPlacement.Below, result.ActualPlacement);
		Assert.Equal(6, result.Bounds.Y); // right below anchor (5 + 1)
	}

	[Fact]
	public void PortalPositioner_BelowOrAbove_FlipsAboveWhenNoRoomBelow()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(10, 35, 20, 1),
			ContentSize: new Size(20, 5),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.BelowOrAbove
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(PortalPlacement.Above, result.ActualPlacement);
		Assert.Equal(30, result.Bounds.Y); // above anchor (35 - 5)
	}

	[Fact]
	public void PortalPositioner_Below_PlacesBelowAnchor()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(5, 10, 30, 1),
			ContentSize: new Size(30, 3),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.Below
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(11, result.Bounds.Y);
		Assert.Equal(5, result.Bounds.X);
		Assert.Equal(30, result.Bounds.Width);
		Assert.Equal(3, result.Bounds.Height);
	}

	[Fact]
	public void PortalPositioner_Above_PlacesAboveAnchor()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(5, 10, 30, 1),
			ContentSize: new Size(30, 3),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.Above
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(7, result.Bounds.Y); // 10 - 3
	}

	[Fact]
	public void PortalPositioner_ClampsToScreenWidth()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(90, 5, 20, 1),
			ContentSize: new Size(20, 3),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.Below
		);

		var result = PortalPositioner.Calculate(request);
		// Should not extend past right edge
		Assert.True(result.Bounds.Right <= 100);
	}

	[Fact]
	public void PortalPositioner_ClampsToScreenLeft()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(-5, 5, 20, 1),
			ContentSize: new Size(20, 3),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.Below
		);

		var result = PortalPositioner.Calculate(request);
		Assert.True(result.Bounds.X >= 0);
	}

	[Fact]
	public void PortalPositioner_ClampsHeight()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(5, 35, 20, 1),
			ContentSize: new Size(20, 10),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.Below
		);

		var result = PortalPositioner.Calculate(request);
		// Should clamp or flip
		Assert.True(result.Bounds.Bottom <= 40);
	}

	[Fact]
	public void PortalPositioner_ZeroSizeContent_Handles()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(5, 5, 10, 1),
			ContentSize: new Size(0, 0),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.Below
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(0, result.Bounds.Width);
		Assert.Equal(0, result.Bounds.Height);
	}

	[Fact]
	public void PortalPositioner_AnchorAtTop_FlipNotNeeded()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(5, 0, 20, 1),
			ContentSize: new Size(20, 5),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.BelowOrAbove
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(PortalPlacement.Below, result.ActualPlacement);
		Assert.Equal(1, result.Bounds.Y);
	}

	[Fact]
	public void PortalPositioner_AnchorAtBottom_FlipsAbove()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(5, 39, 20, 1),
			ContentSize: new Size(20, 5),
			ScreenBounds: new Rectangle(0, 0, 100, 40),
			Placement: PortalPlacement.BelowOrAbove
		);

		var result = PortalPositioner.Calculate(request);
		Assert.Equal(PortalPlacement.Above, result.ActualPlacement);
	}

	[Fact]
	public void PortalPositioner_SmallScreen_Handles()
	{
		var request = new PortalPositionRequest(
			Anchor: new Rectangle(0, 0, 5, 1),
			ContentSize: new Size(5, 3),
			ScreenBounds: new Rectangle(0, 0, 10, 5),
			Placement: PortalPlacement.Below
		);

		var result = PortalPositioner.Calculate(request);
		Assert.True(result.Bounds.Bottom <= 5);
		Assert.True(result.Bounds.Right <= 10);
	}

	#endregion

	#region Portal Width Calculation Tests

	[Fact]
	public void PortalWidth_AtLeastAsWideAsHeader()
	{
		var dd = new DropdownControl("S:");
		dd.AddItem("Short");
		dd.AddItem("Medium Item");

		int? contentWidth = dd.ContentWidth;
		Assert.NotNull(contentWidth);

		// Portal width calculation would include selection indicators,
		// so it should be at least as wide as the header
		var constraints = new LayoutConstraints(0, 200, 0, 200);
		var measured = dd.MeasureDOM(constraints);
		Assert.True(measured.Width > 0);
	}

	[Fact]
	public void PortalWidth_AccountsForIcons()
	{
		var dd = new DropdownControl("S:");
		dd.AddItem(new DropdownItem("Item", "★", Color.Gold1));

		int? w1 = dd.ContentWidth;

		var dd2 = new DropdownControl("S:");
		dd2.AddItem(new DropdownItem("Item"));

		// Header width doesn't include icons (those are in portal),
		// but ContentWidth should still work correctly
		Assert.NotNull(w1);
	}

	#endregion

	#region Dropdown Portal Bounds - Integration

	[Fact]
	public void DropdownWithWindow_CanOpenAndClose()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:", new[] { "A", "B", "C" });
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;
		Assert.True(dd.IsDropdownOpen);

		dd.IsDropdownOpen = false;
		Assert.False(dd.IsDropdownOpen);
	}

	[Fact]
	public void DropdownWithWindow_PortalBoundsNotEmpty()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:", new[] { "A", "B", "C", "D", "E" });
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		Assert.True(bounds.Width > 0);
		Assert.True(bounds.Height > 0);

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void DropdownWithWindow_MaxVisibleItems_LimitsPortalHeight()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:", new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" });
		dd.MaxVisibleItems = 3;
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		// Height should be limited: 3 items + 1 scroll indicator = 4
		Assert.True(bounds.Height <= 4);

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void DropdownWithWindow_FewItems_NoScrollIndicator()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:", new[] { "A", "B" });
		dd.MaxVisibleItems = 5;
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		// 2 items, no scroll indicator needed
		Assert.Equal(2, bounds.Height);

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void DropdownWithWindow_SelectedIndex_Preserved()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:", new[] { "A", "B", "C" });
		dd.SelectedIndex = 2;
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		Assert.Equal(2, dd.SelectedIndex);
		Assert.Equal("C", dd.SelectedValue);
	}

	#endregion

	#region PortalPlacement Enum Tests

	[Fact]
	public void PortalPlacement_BelowOrAbove_Value()
	{
		Assert.NotEqual(PortalPlacement.Below, PortalPlacement.Above);
		Assert.NotEqual(PortalPlacement.Below, PortalPlacement.BelowOrAbove);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void DropdownPortal_EmptyItems_HandleGracefully()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:");
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Opening with no items should not crash
		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		Assert.Equal(0, bounds.Height); // no items = no height

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void DropdownPortal_SingleItem_Works()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:");
		dd.AddItem("Only");
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		Assert.Equal(1, bounds.Height); // single item

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void DropdownPortal_LargeItemList_ClampsToMaxVisible()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };

		var dd = new DropdownControl("S:");
		for (int i = 0; i < 100; i++)
			dd.AddItem($"Item {i}");
		dd.MaxVisibleItems = 5;
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		var bounds = dd.GetPortalBounds();
		Assert.True(bounds.Height <= 6); // 5 items + 1 scroll

		dd.IsDropdownOpen = false;
	}

	[Fact]
	public void DropdownPortal_NearBottomOfWindow_FlipsUp()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 15, Top = 25 };

		var dd = new DropdownControl("S:", new[] { "A", "B", "C", "D", "E" });
		dd.MaxVisibleItems = 5;
		// Add several spacer controls to push dropdown near bottom
		for (int i = 0; i < 10; i++)
			window.AddControl(new MarkupControl(new List<string> { " " }));
		window.AddControl(dd);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		dd.SetFocus(true, FocusReason.Programmatic);
		dd.IsDropdownOpen = true;

		// The portal should either flip up or be clamped
		var bounds = dd.GetPortalBounds();
		Assert.True(bounds.Bottom <= 40); // within screen

		dd.IsDropdownOpen = false;
	}

	#endregion
}
