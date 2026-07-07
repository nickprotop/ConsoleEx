// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout
{
	// Resolves Placement values against a headless usable desktop of a known size. The test system is built
	// with ShowTopPanel/ShowBottomPanel = false so GetTopStatusHeight()/GetBottomStatusHeight() are both 0,
	// making the usable desktop exactly ScreenSize (originY = 0). Assertions read DesktopUpperLeft.Y so they
	// stay correct even if that offset is non-zero.
	public class WindowPlacementServiceTests
	{
		private static ConsoleWindowSystem NewSystem(int w, int h)
			=> TestWindowSystemBuilder.CreateTestSystem(w, h);

		[Fact]
		public void Resolve_SnapFull_FillsUsableDesktop()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.Full));
			Assert.Equal(new Rectangle(0, oy + 0, 100, 40), r);
		}

		[Fact]
		public void Resolve_Maximized_EqualsSnapFull()
		{
			var sys = NewSystem(100, 40);
			var a = sys.WindowPlacementService.Resolve(Placement.Maximized);
			var b = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.Full));
			Assert.Equal(b, a);
		}

		[Fact]
		public void Resolve_LeftHalf_And_RightHalf_EvenWidth()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var left = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.LeftHalf));
			var right = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.RightHalf));
			Assert.Equal(new Rectangle(0, oy + 0, 50, 40), left);
			Assert.Equal(new Rectangle(50, oy + 0, 50, 40), right);
		}

		[Fact]
		public void Resolve_LeftHalf_And_RightHalf_OddWidth_RemainderToLeft()
		{
			var sys = NewSystem(101, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var left = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.LeftHalf));
			var right = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.RightHalf));
			Assert.Equal(new Rectangle(0, oy + 0, 51, 40), left);
			Assert.Equal(new Rectangle(51, oy + 0, 50, 40), right);
		}

		[Fact]
		public void Resolve_TopHalf_And_BottomHalf_OddHeight_RemainderToTop()
		{
			var sys = NewSystem(100, 41);
			int oy = sys.DesktopUpperLeft.Y;
			var top = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.TopHalf));
			var bottom = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.BottomHalf));
			Assert.Equal(new Rectangle(0, oy + 0, 100, 21), top);
			Assert.Equal(new Rectangle(0, oy + 21, 100, 20), bottom);
		}

		[Fact]
		public void Resolve_Quadrants_EvenSize()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			Assert.Equal(new Rectangle(0, oy + 0, 50, 20), sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.TopLeft)));
			Assert.Equal(new Rectangle(50, oy + 0, 50, 20), sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.TopRight)));
			Assert.Equal(new Rectangle(0, oy + 20, 50, 20), sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.BottomLeft)));
			Assert.Equal(new Rectangle(50, oy + 20, 50, 20), sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.BottomRight)));
		}

		[Fact]
		public void Resolve_CenterExplicit_CentersAndClamps()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Center(20, 10));
			// centered: x = (100-20)/2 = 40, y = (40-10)/2 = 15
			Assert.Equal(new Rectangle(40, oy + 15, 20, 10), r);
		}

		[Fact]
		public void Resolve_CenterExplicit_OversizedClampsToDesktop()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Center(500, 500));
			Assert.Equal(new Rectangle(0, oy + 0, 100, 40), r);
		}

		[Fact]
		public void Resolve_AnchorTopRight_WithMargin()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Anchor(SharpConsoleUI.Layout.Anchor.TopRight, 20, 10, 1));
			// right edge: x = W - w - margin = 100 - 20 - 1 = 79; top: y = margin = 1
			Assert.Equal(new Rectangle(79, oy + 1, 20, 10), r);
		}

		[Fact]
		public void Resolve_AnchorBottomLeft_WithMargin()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Anchor(SharpConsoleUI.Layout.Anchor.BottomLeft, 20, 10, 2));
			// x = margin = 2; y = H - h - margin = 40 - 10 - 2 = 28
			Assert.Equal(new Rectangle(2, oy + 28, 20, 10), r);
		}

		[Fact]
		public void Resolve_CenterPreset_MediumIsSixtyPercent()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Center(SizePreset.Medium));
			// Medium = 0.6 -> w = 60, h = 24; centered x = 20, y = 8
			Assert.Equal(new Rectangle(20, oy + 8, 60, 24), r);
		}

		[Fact]
		public void Resolve_Fraction_TopLeft()
		{
			var sys = NewSystem(100, 40);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Fraction(SharpConsoleUI.Layout.Anchor.TopLeft, 0.25, 0.5));
			// w = 25, h = 20, at top-left corner (margin 0)
			Assert.Equal(new Rectangle(0, oy + 0, 25, 20), r);
		}

		[Fact]
		public void Resolve_NarrowShortDesktop_ClampsToAtLeastOne()
		{
			var sys = NewSystem(1, 1);
			int oy = sys.DesktopUpperLeft.Y;
			var r = sys.WindowPlacementService.Resolve(Placement.Snap(SnapZone.RightHalf));
			// W=1,H=1: RightHalf x = 1/2 + 1%2 = 1, width = 1/2 = 0 -> clamped to 1, then x clamped so x+w<=W => x=0
			Assert.Equal(new Rectangle(0, oy + 0, 1, 1), r);
		}

		[Fact]
		public void Placement_ValueEquality()
		{
			Assert.Equal(Placement.Snap(SnapZone.LeftHalf), Placement.Snap(SnapZone.LeftHalf));
			Assert.NotEqual(Placement.Snap(SnapZone.LeftHalf), Placement.Snap(SnapZone.RightHalf));
			Assert.Equal(Placement.Center(20, 10), Placement.Center(20, 10));
			Assert.NotEqual(Placement.Center(20, 10), Placement.Center(20, 11));
		}
	}
}
