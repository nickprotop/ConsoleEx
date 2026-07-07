// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout
{
	// Live-state ("Model A") tests for Window.Placement: setting a placement snaps the window, a desktop
	// resize re-resolves it (survives the re-render), and a manual drag/resize detaches it. The test system
	// is built with ShowTopPanel/ShowBottomPanel = false so the usable desktop equals ScreenSize
	// (originY = 0), but assertions read DesktopUpperLeft.Y so they stay correct if that ever changes.
	public class WindowPlacementLiveStateTests
	{
		private static Window AddWindow(ConsoleWindowSystem system)
		{
			var window = new Window(system) { Title = "Placed", Width = 20, Height = 8 };
			system.AddWindow(window);
			return window;
		}

		private static void Render(Window window)
		{
			window.RenderAndGetVisibleContent(new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		}

		[Fact]
		public void Placement_Set_SnapsWindowToZone()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
			var window = AddWindow(system);

			window.Placement = Placement.Snap(SnapZone.LeftHalf);

			var expected = system.WindowPlacementService.Resolve(Placement.Snap(SnapZone.LeftHalf));
			Assert.Equal(expected.X, window.Left);
			Assert.Equal(expected.Y, window.Top);
			Assert.Equal(expected.Width, window.Width);
			Assert.Equal(expected.Height, window.Height);
			Assert.Equal(WindowState.Normal, window.State);
		}

		[Fact]
		public void Placement_ReResolvesOnDesktopResize()
		{
			// Real-thing test: drive the actual resize path end-to-end and assert the window re-occupies
			// LeftHalf of the NEW desktop, surviving a re-render.
			var driver = new MockConsoleDriver(100, 40);
			var system = new ConsoleWindowSystem(driver, options: new ConsoleWindowSystemOptions(
				EnableFrameRateLimiting: false,
				EnablePerformanceMetrics: false,
				ShowTopPanel: false,
				ShowBottomPanel: false));
			system.WireScreenResizeForTest();

			var window = new Window(system) { Title = "Placed", Width = 20, Height = 8 };
			system.AddWindow(window);

			window.Placement = Placement.Snap(SnapZone.LeftHalf);
			Render(window);

			// Sanity: occupies LeftHalf of 100x40.
			var before = system.WindowPlacementService.Resolve(Placement.Snap(SnapZone.LeftHalf));
			Assert.Equal(before.Width, window.Width);

			// Drive the real resize path.
			driver.SimulateScreenResize(80, 30);
			system.DrainPendingUIActionsForTest();
			Render(window);

			// Now it must occupy LeftHalf of the NEW 80x30 desktop, and the placement is still sticky.
			var expected = system.WindowPlacementService.Resolve(Placement.Snap(SnapZone.LeftHalf));
			Assert.Equal(expected.X, window.Left);
			Assert.Equal(expected.Y, window.Top);
			Assert.Equal(expected.Width, window.Width);
			Assert.Equal(expected.Height, window.Height);
			Assert.Equal(Placement.Snap(SnapZone.LeftHalf), window.Placement);
			Assert.Equal(WindowState.Normal, window.State);
		}

		[Fact]
		public void ManualDrag_ClearsPlacement()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
			var window = AddWindow(system);

			window.Placement = Placement.Snap(SnapZone.LeftHalf);
			int leftBefore = window.Left;

			system.WindowStateService.StartDrag(window, new Point(window.Left + 1, window.Top));

			Assert.Null(window.Placement);
			Assert.Equal(WindowState.Normal, window.State);
			// It did NOT snap back / jump: bounds stay where the placement left them.
			Assert.Equal(leftBefore, window.Left);
		}

		[Fact]
		public void ManualResize_ClearsPlacement()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
			var window = AddWindow(system);

			window.Placement = Placement.Snap(SnapZone.RightHalf);
			Assert.NotNull(window.Placement);

			system.WindowStateService.StartResize(window, ResizeDirection.BottomRight, new Point(window.Left + window.Width, window.Top + window.Height));

			Assert.Null(window.Placement);
			Assert.Equal(WindowState.Normal, window.State);
		}

		[Fact]
		public void PlacementApply_DoesNotSelfClear()
		{
			// Applying a placement internally calls SetSize/SetPosition; the _applyingPlacement guard must
			// keep any interaction-clear from firing so the placement stays set after its own apply.
			var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
			var window = AddWindow(system);

			window.Placement = Placement.Snap(SnapZone.RightHalf);

			Assert.Equal(Placement.Snap(SnapZone.RightHalf), window.Placement);
		}
	}
}
