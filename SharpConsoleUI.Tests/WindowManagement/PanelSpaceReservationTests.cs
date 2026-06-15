// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Regression tests for the top/bottom status-bar (Panel) space reservation: a hidden bar must
/// RECLAIM its row (height 0), and a visible bar must reserve exactly one row. This locks in the
/// behavior discussed in discussion #46 — hidden bars do not leave dead/blank rows.
/// </summary>
public class PanelSpaceReservationTests
{
	private const int ScreenWidth = 80;
	private const int ScreenHeight = 24;

	/// <summary>Creates a system with a fixed screen size and both panels visible.</summary>
	private static ConsoleWindowSystem CreateSystemWithPanels(bool top, bool bottom)
	{
		var mockDriver = new MockConsoleDriver(ScreenWidth, ScreenHeight);
		var options = new ConsoleWindowSystemOptions(
			EnableFrameRateLimiting: false,
			EnablePerformanceMetrics: false,
			ShowTopPanel: top,
			ShowBottomPanel: bottom);
		return new ConsoleWindowSystem(mockDriver, options: options);
	}

	[Fact]
	public void TopBarVisible_ReservesOneRow_DesktopStartsAtRowOne()
	{
		var system = CreateSystemWithPanels(top: true, bottom: false);
		Assert.Equal(1, system.DesktopUpperLeft.Y);
	}

	[Fact]
	public void TopBarHidden_ReclaimsRow_DesktopStartsAtRowZero()
	{
		var system = CreateSystemWithPanels(top: false, bottom: false);
		Assert.Equal(0, system.DesktopUpperLeft.Y);
	}

	[Fact]
	public void BothBarsHidden_DesktopHeightEqualsFullScreen()
	{
		var system = CreateSystemWithPanels(top: false, bottom: false);
		Assert.Equal(ScreenHeight, system.DesktopDimensions.Height);
	}

	[Fact]
	public void BothBarsVisible_DesktopHeightIsScreenMinusTwo()
	{
		var system = CreateSystemWithPanels(top: true, bottom: true);
		Assert.Equal(ScreenHeight - 2, system.DesktopDimensions.Height);
	}

	[Fact]
	public void OnlyTopVisible_DesktopHeightIsScreenMinusOne()
	{
		var system = CreateSystemWithPanels(top: true, bottom: false);
		Assert.Equal(ScreenHeight - 1, system.DesktopDimensions.Height);
	}

	[Fact]
	public void OnlyBottomVisible_DesktopHeightIsScreenMinusOne()
	{
		var system = CreateSystemWithPanels(top: false, bottom: true);
		Assert.Equal(ScreenHeight - 1, system.DesktopDimensions.Height);
	}

	[Fact]
	public void HidingTopBarAtRuntime_GivesTheRowBack()
	{
		var system = CreateSystemWithPanels(top: true, bottom: false);
		int before = system.DesktopDimensions.Height;
		Assert.Equal(1, system.DesktopUpperLeft.Y);

		system.PanelStateService.ShowTopPanel = false;

		Assert.Equal(0, system.DesktopUpperLeft.Y);
		Assert.Equal(before + 1, system.DesktopDimensions.Height); // reclaimed exactly one row
	}

	[Fact]
	public void ShowingBottomBarAtRuntime_TakesExactlyOneRow()
	{
		var system = CreateSystemWithPanels(top: false, bottom: false);
		int before = system.DesktopDimensions.Height;

		system.PanelStateService.ShowBottomPanel = true;

		Assert.Equal(before - 1, system.DesktopDimensions.Height);
		// Bottom bar must not move the top of the desktop.
		Assert.Equal(0, system.DesktopUpperLeft.Y);
	}

	[Fact]
	public void ToggleVisibility_RecomputesEachRead_NotCachedStale()
	{
		var system = CreateSystemWithPanels(top: false, bottom: false);
		Assert.Equal(ScreenHeight, system.DesktopDimensions.Height);

		system.PanelStateService.ShowTopPanel = true;
		Assert.Equal(ScreenHeight - 1, system.DesktopDimensions.Height);

		system.PanelStateService.ShowTopPanel = false;
		Assert.Equal(ScreenHeight, system.DesktopDimensions.Height); // back to full — value survives re-read
	}

	[Fact]
	public void DesktopBottomRight_AccountsForBottomBar()
	{
		var withBar = CreateSystemWithPanels(top: false, bottom: true);
		var withoutBar = CreateSystemWithPanels(top: false, bottom: false);

		// The usable area's bottom row is one higher when the bottom bar reserves a row.
		Assert.Equal(withoutBar.DesktopBottomRight.Y - 1, withBar.DesktopBottomRight.Y);
	}
}
