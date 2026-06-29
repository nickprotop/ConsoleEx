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
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Geometry regression tests for issue #60: a bordered, Fill ScrollablePanel inside a FRAMELESS MAXIMIZED
/// window lost its bottom border. Root cause: the window-content blit broke its row loop at
/// <c>windowTop + y &gt;= DesktopBottomRight.Y</c> — comparing a DESKTOP-relative row (windowTop + y) against
/// a SCREEN-absolute bound (DesktopBottomRight.Y). When the desktop origin is 0 (panels off) the two spaces
/// coincide and the off-by-one drops the content buffer's LAST row, so the panel's bottom border (which is
/// present in the buffer) was never blitted to the screen. Fixed by breaking at the desktop-relative bound
/// <c>DesktopDimensions.Height</c>, so the last row is blitted and the maximized window fills the full
/// desktop (no reserved/wasted row).
/// These tests assert the COMPOSITED-SCREEN geometry (via the rendering-diagnostics snapshot), which runs
/// the same blit path, so they fail when the fix is reverted.
/// </summary>
public class MaximizedWindowChromeGeometryTests
{
	[Fact]
	public void FramelessMaximized_FillScrollablePanel_BottomBorderRenders()
	{
		// Screen 60x20, panels off (CreateSystem disables them) -> desktop is the full screen, origin (0,0).
		var system = ChromeGeometry.CreateSystem(60, 20);

		var window = new WindowBuilder(system)
			.Frameless()
			.Maximized()
			.Build();

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithName("OutputPanel")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);

		var snap = ChromeGeometry.Render(system);

		// The maximized frameless window fills the desktop; the Fill panel fills the window, so its border
		// box IS the window's screen rect. Derive it from the window's actual bounds + the desktop origin.
		var origin = system.DesktopUpperLeft;
		var rect = new ChromeGeometry.ScreenRect(
			origin.X + window.Left, origin.Y + window.Top, window.Width, window.Height);

		// #60: the bottom border (╰────╯) must be present on the composited screen — including the panel's
		// last row, which is the desktop's last row. This is the row the blit off-by-one used to drop.
		ChromeGeometry.AssertRoundedBoxComplete(snap, rect);
	}

	[Fact]
	public void FramelessMaximized_FillsFullDesktopHeight()
	{
		// The maximized window occupies the full desktop height (no reserved row): the blit writes the last
		// desktop row correctly, so there is no need to shrink the window to avoid it.
		var system = ChromeGeometry.CreateSystem(60, 20);

		var window = new WindowBuilder(system)
			.Frameless()
			.Maximized()
			.Build();
		system.WindowStateService.AddWindow(window);

		Assert.Equal(system.DesktopDimensions.Height, window.Height);
		Assert.Equal(system.DesktopDimensions.Width, window.Width);
	}
}
